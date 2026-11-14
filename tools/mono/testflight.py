"""Archive the checked Mono export and upload an internal-only TestFlight build using automatic signing."""
from __future__ import annotations

import argparse
from contextlib import contextmanager
from dataclasses import dataclass
import os
from pathlib import Path
import plistlib
import re
import shutil
import subprocess
import tempfile
import uuid
import zipfile

SECRET_NAMES = ("APPLE_TEAM_ID", "ASC_KEY_ID", "ASC_ISSUER_ID", "ASC_PRIVATE_KEY")


@dataclass(frozen=True, repr=False)
class Credentials:
    team: str
    key_id: str
    issuer: str
    private_key: str

    @classmethod
    def read(cls, env: dict) -> Credentials:
        if any(not env.get(name, "").strip() for name in SECRET_NAMES):
            raise ValueError("TestFlight requires APPLE_TEAM_ID, ASC_KEY_ID, ASC_ISSUER_ID and ASC_PRIVATE_KEY.")
        team, key_id, issuer, private_key = (env[name].strip() for name in SECRET_NAMES)
        if not re.fullmatch(r"[A-Z0-9]{10}", team) or not re.fullmatch(r"[A-Z0-9]{10}", key_id):
            raise ValueError("APPLE_TEAM_ID and ASC_KEY_ID must each be 10 uppercase letters/digits.")
        try:
            uuid.UUID(issuer)
        except ValueError:
            raise ValueError("ASC_ISSUER_ID must be the Issuer ID UUID from App Store Connect.") from None
        if not private_key.startswith("-----BEGIN PRIVATE KEY-----\n") or not private_key.endswith("-----END PRIVATE KEY-----"):
            raise ValueError("ASC_PRIVATE_KEY must contain the complete downloaded .p8 text, including BEGIN/END lines.")
        return cls(team, key_id, issuer, private_key)

    def arguments(self, path: Path) -> list[str]:
        return ["-allowProvisioningUpdates", "-authenticationKeyPath", str(path),
                "-authenticationKeyID", self.key_id, "-authenticationKeyIssuerID", self.issuer]


def clean_environment(env: dict) -> dict:
    return {key: value for key, value in env.items() if key not in SECRET_NAMES}


@contextmanager
def installed_key(credentials: Credentials, home: Path):
    # altool searches this standard directory; never overwrite a pre-existing local key.
    folder = home / ".appstoreconnect/private_keys"
    folder.mkdir(parents=True, exist_ok=True, mode=0o700)
    key = folder / f"AuthKey_{credentials.key_id}.p8"
    descriptor = os.open(key, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    try:
        with os.fdopen(descriptor, "w") as stream:
            stream.write(credentials.private_key + "\n")
        yield key
    finally:
        key.unlink(missing_ok=True)


def export_options(team: str) -> dict:
    return {"method": "app-store-connect", "teamID": team, "signingStyle": "automatic", "destination": "export",
            "testFlightInternalTestingOnly": True, "manageAppVersionAndBuildNumber": False,
            "uploadSymbols": False, "stripSwiftSymbols": True}


def verify_ipa(ipa: Path, expected: dict) -> None:
    with zipfile.ZipFile(ipa) as archive:
        plists = [name for name in archive.namelist() if re.fullmatch(r"Payload/[^/]+\.app/Info.plist", name)]
        if len(plists) != 1:
            raise ValueError("The exported IPA must contain exactly one app.")
        info = plistlib.loads(archive.read(plists[0]))
    for key in ("CFBundleIdentifier", "CFBundleShortVersionString", "CFBundleVersion"):
        if info.get(key) != expected.get(key):
            raise ValueError(f"TestFlight export changed {key}; refusing to upload a different app/version.")


def deliver(credentials: Credentials, upload: bool) -> None:
    # These depend on the provisioned toolchain, so --check-secrets remains usable before setup.
    import build_mono as build
    import scan_no_game_files

    project = build.EXPORT / "FullGame.xcodeproj"
    if not project.is_dir() or not (build.APP / "Info.plist").is_file():
        raise ValueError("Run scripts/build-mono.sh --unsigned first; TestFlight must archive the patched Mono export.")
    info = plistlib.loads((build.APP / "Info.plist").read_bytes())
    bundle = info["CFBundleIdentifier"]
    if not re.fullmatch(r"[A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)+", bundle):
        raise ValueError("The built app has an invalid bundle identifier.")
    output = build.OUT / "testflight"
    shutil.rmtree(output, ignore_errors=True)
    output.mkdir(parents=True)
    archive = output / "FullGame.xcarchive"
    ipa_folder = output / "ipa"
    options = output / "ExportOptions.plist"
    options.write_bytes(plistlib.dumps(export_options(credentials.team)))
    env = clean_environment(build.environment())
    # Do not expose the key's contents to Xcode build phases or their environment logs.
    with installed_key(credentials, Path.home()) as key:
        auth = credentials.arguments(key)
        build.step("Archiving the patched Mono app for TestFlight")
        build.run(["xcodebuild", "-project", str(project), "-scheme", "FullGame", "-configuration", "Release",
                   "-destination", "generic/platform=iOS", "-archivePath", str(archive),
                   "-derivedDataPath", str(output / "derived"), "-hideShellScriptEnvironment", *auth,
                   f"DEVELOPMENT_TEAM={credentials.team}", f"PRODUCT_BUNDLE_IDENTIFIER={bundle}",
                   "CODE_SIGN_STYLE=Automatic", "CODE_SIGN_IDENTITY=Apple Development", "SKIP_INSTALL=NO",
                   *build.STRIP_SETTINGS, *build.version_settings(env), "archive"],
                  "testflight-archive", env=env, cwd=build.EXPORT)
        build.step("Exporting with Xcode-managed App Store distribution signing")
        build.run(["xcodebuild", "-exportArchive", "-archivePath", str(archive), "-exportOptionsPlist", str(options),
                   "-exportPath", str(ipa_folder), *auth], "testflight-export", env=env)
        ipas = list(ipa_folder.glob("*.ipa"))
        if len(ipas) != 1:
            raise ValueError("Xcode did not export exactly one TestFlight IPA.")
        ipa = ipas[0]
        verify_ipa(ipa, info)
        if scan_no_game_files.scan(ipa):
            raise ValueError("The signed IPA failed the no-game-files scan; refusing to upload.")
        with tempfile.TemporaryDirectory(prefix="sts2-testflight-check-") as temp:
            with zipfile.ZipFile(ipa) as package:
                package.extractall(temp)
            apps = list((Path(temp) / "Payload").glob("*.app"))
            if len(apps) != 1:
                raise ValueError("The signed IPA must have exactly one app.")
            result = subprocess.run(["codesign", "--verify", "--deep", "--strict", str(apps[0])], env=env, capture_output=True)
            if result.returncode:
                raise ValueError("The exported app's distribution signature failed verification.")
        if upload:
            build.step("Uploading the internal-only build to App Store Connect")
            build.run(["xcrun", "altool", "--upload-app", "--type", "ios", "--file", str(ipa),
                       "--apiKey", credentials.key_id, "--apiIssuer", credentials.issuer], "testflight-upload", env=env)
            print("Upload accepted. Apple must process the build before it appears in TestFlight.", flush=True)
        print(f"Signed IPA: {ipa.relative_to(build.ROOT)}", flush=True)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check-secrets", action="store_true")
    parser.add_argument("--upload", action="store_true", help="upload after signing and validation")
    args = parser.parse_args()
    credentials = Credentials.read(os.environ)
    if args.check_secrets:
        with installed_key(credentials, Path.home()) as key:
            result = subprocess.run(["openssl", "pkey", "-in", str(key), "-noout"], capture_output=True,
                                    env=clean_environment(dict(os.environ)))
            if result.returncode:
                raise ValueError("ASC_PRIVATE_KEY could not be parsed as a private key. Paste the original .p8 file contents.")
        print("All four TestFlight secrets have the expected format; the private key parses successfully.")
    else:
        deliver(credentials, args.upload)


if __name__ == "__main__":
    try:
        main()
    except (ValueError, RuntimeError, OSError, subprocess.CalledProcessError) as error:
        raise SystemExit(f"TestFlight stopped: {error}") from None
