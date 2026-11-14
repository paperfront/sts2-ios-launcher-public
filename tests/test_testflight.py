from pathlib import Path
import plistlib
import sys
import tempfile
import unittest
import zipfile

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'tools/mono'))
import testflight


def values():
    return {'APPLE_TEAM_ID': 'ABCDEFGHIJ', 'ASC_KEY_ID': '1234567890',
            'ASC_ISSUER_ID': '12345678-1234-1234-1234-123456789012',
            'ASC_PRIVATE_KEY': '-----BEGIN PRIVATE KEY-----\nTEST-ONLY-PLACEHOLDER\n-----END PRIVATE KEY-----'}


class TestFlightTests(unittest.TestCase):
    def test_missing_or_malformed_secrets_fail_without_echoing_values(self):
        for name in testflight.SECRET_NAMES:
            with self.subTest(name=name):
                env = values()
                del env[name]
                with self.assertRaises(ValueError):
                    testflight.Credentials.read(env)
                env[name] = 'PRIVATE-INVALID-VALUE'
                with self.assertRaises(ValueError) as error:
                    testflight.Credentials.read(env)
                self.assertNotIn('PRIVATE-INVALID-VALUE', str(error.exception))

    def test_private_key_is_temporary_and_restricts_file_permissions(self):
        credentials = testflight.Credentials.read(values())
        with tempfile.TemporaryDirectory() as temp:
            with testflight.installed_key(credentials, Path(temp)) as key:
                self.assertEqual(key.stat().st_mode & 0o777, 0o600)
                self.assertEqual(key.read_text().strip(), values()['ASC_PRIVATE_KEY'])
            self.assertFalse(key.exists())
            self.assertNotIn(values()['ASC_PRIVATE_KEY'], repr(credentials))

    def test_key_is_removed_after_failure_and_existing_key_is_not_overwritten(self):
        credentials = testflight.Credentials.read(values())
        with tempfile.TemporaryDirectory() as temp:
            with self.assertRaises(RuntimeError):
                with testflight.installed_key(credentials, Path(temp)) as key:
                    raise RuntimeError('simulated export failure')
            self.assertFalse(key.exists())
            key.write_text('pre-existing key')
            with self.assertRaises(FileExistsError):
                with testflight.installed_key(credentials, Path(temp)):
                    pass
            self.assertEqual(key.read_text(), 'pre-existing key')

    def test_xcode_build_phases_do_not_inherit_apple_secret_values(self):
        env = values() | {'PATH': '/bin'}
        self.assertEqual(testflight.clean_environment(env), {'PATH': '/bin'})

    def test_export_is_internal_only_and_keeps_the_ci_build_number(self):
        options = testflight.export_options('ABCDEFGHIJ')
        self.assertTrue(options['testFlightInternalTestingOnly'])
        self.assertFalse(options['manageAppVersionAndBuildNumber'])
        self.assertEqual(options['signingStyle'], 'automatic')
        self.assertEqual(options['method'], 'app-store-connect')

    def test_wrong_app_or_version_cannot_be_uploaded(self):
        expected = {'CFBundleIdentifier': 'io.github.paperfront.sts2mobile',
                    'CFBundleShortVersionString': '1.0.0', 'CFBundleVersion': '7'}
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / 'app.ipa'
            for changed in [None, *expected]:
                info = dict(expected)
                if changed:
                    info[changed] = 'different'
                with zipfile.ZipFile(path, 'w') as archive:
                    archive.writestr('Payload/FullGame.app/Info.plist', plistlib.dumps(info))
                if changed:
                    with self.assertRaises(ValueError):
                        testflight.verify_ipa(path, expected)
                else:
                    testflight.verify_ipa(path, expected)
