# STS2 Mobile

Play Slay the Spire 2 on your iPhone. Build this app on your own Mac, or, with no Mac, install the
released IPA from Windows with your own Apple ID. When you open it and sign in to Steam with an account that
owns the game, it downloads the game from Steam and prepares it for the phone.

Neither this repository nor the released app holds any of the game: you still need to own it on Steam. It is
an unofficial fan project, not made or endorsed by Mega Crit or Valve.

## Is it safe?

- **Your password goes only to Steam.** You sign in on Steam's own servers, as the Steam app does. The app
  never stores your password, only the sign-in Steam gives back, encrypted on your iPhone.
- **There is no server of ours.** The app talks only to Steam and Mega Crit, plus a multiplayer relay if your
  build has one, and a friend’s private LAN/Tailscale address when you explicitly join a LAN game.
  There are no analytics, ads or tracking, and the game’s own crash reporting and stats uploads are taken out.
- **Nothing happens behind your back.** The game downloads, updates and syncs saves with Steam Cloud only when
  you press the button for it.
- **You can read every line.** The app is built from this code, on your Mac or by this repository's
  GitHub Actions workflow, whose log anyone can read.

The details are in [Security and privacy](#security-and-privacy) below.

## LAN multiplayer with friends (no mods)

An iPhone can host, and other iPhones can join it over the same Wi-Fi or Tailscale. Leave **Mods off**
and use the same game version on every device (the pinned beta is v0.113.0). This integration still
needs real-device gameplay testing; automated builds do not establish multiplayer compatibility.

1. Put the phones on the same Wi-Fi, or connect them to a Tailscale network that permits connections
   between them. Each player signs in to a Steam account that owns the game.
2. On the hosting iPhone, open **Multiplayer > LAN host address**. Choose this phone’s Wi-Fi address
   (such as `192.168.1.20`) or Tailscale address, then tap **Use and copy address**. You can also enter
   this phone’s Tailscale IP manually if it is not listed. This selects an interface; it does not start a server.
3. Close that panel and choose **Host** and a game mode. The game’s built-in ENet server starts on the
   selected interface at UDP port `33771`. Share the copied address with your friends.
4. On each joining iPhone, open **Multiplayer > Join**, paste the address and tap **Join LAN**.
   IPv4 can be entered as `192.168.1.20` or `192.168.1.20:33771`; Tailscale IPv6 uses `[address]:33771`.
5. Allow **Local Network** access when iOS asks. Keep the host app open in the foreground throughout
   the session. If hosting fails after changing networks, refresh and select the phone’s current address.

To continue a saved run, the host chooses its LAN address again and uses the game’s normal **Load**
action. Joining players connect to that address again. Save/load, quitting the lobby, and server teardown
stay in the game’s native flow. Switching multiplayer modes can affect saves, so keep a backup.

A Windows/Mac player can also host without installing mods: add `--fastmp` under **Steam > Slay the
Spire 2 > Properties > Launch Options**, fully restart the game, and use **Multiplayer > Host**.
The iPhones join the desktop’s Wi-Fi or Tailscale IP. Its firewall must permit the game’s UDP port.

There is no broadcast discovery, public matchmaking or added room-password protocol. Only share an
address reachable on your chosen Wi-Fi/Tailscale network; guest Wi-Fi may isolate devices, and Tailscale
access rules must permit the connection. The phone binds only the selected private/Tailscale interface,
never a wildcard. QuickMatch and its optional relay/voice features are separate and are not required.

The native LAN mode follows the same `fastmp` switch used by the desktop game. The Android launcher
documents the [desktop setup](https://github.com/Ekyso/StS2-Launcher#lan-multiplayer); this iOS integration
is independent. Steam ownership verification remains required before the game starts.

## What you need

To build it yourself (with no Mac, see [Install without a Mac](#install-without-a-mac-windows)):

- A Mac with Apple silicon (M1 or later) and about 20 GB free.
- Xcode 27.2 (other versions are refused), opened once, with your Apple ID added in **Settings > Apple
  Accounts**. A free Apple ID works.
- An iPhone and its cable, with about 5.5 GB free during setup and 2.5 GB after. Tested on an iPhone 11 Pro;
  the build allows iOS 15 or later, but other models are untested.
- A Steam account that owns Slay the Spire 2. The game does not need to be on the Mac.

## Install

1. Fork this repository on GitHub and clone your fork:

   ```sh
   git clone https://github.com/<you>/sts2-ios-launcher.git
   cd sts2-ios-launcher
   ```

2. Connect the iPhone by cable, unlock it and tap **Trust** if it asks. Turn on **Settings > Privacy &
   Security > Developer Mode** and restart it. (If Developer Mode is missing, open Xcode's **Window >
   Devices and Simulators** with the iPhone connected, then look again.)
3. Run:

   ```sh
   ./setup.sh --install
   ```

   The first run downloads its own tools (about 6 GB, into this folder) and takes about 15 minutes; later
   runs take about 5. If it finds more than one team or iPhone, it lists them: run it again with
   `--team <id>` or `--device <udid>`.
4. With a free Apple ID, trust the app once: **Settings > General > VPN & Device Management**, tap your Apple
   ID, then **Trust**.

## Install without a Mac (Windows)

The app cannot be built on Windows (Apple's tools for iPhone apps run only on a Mac), so this repository's
GitHub Actions workflow builds it on GitHub's Macs and publishes it on the **Releases** page as an IPA file.

**What the IPA is.** An unsigned iPhone app. It holds none of the game and no one's personal information.
An iPhone only installs apps signed by an Apple ID, so a sideloading tool signs it with **your own Apple ID**
on your PC and installs it. A free Apple ID works. Sideloadly, AltStore and SideStore need no account of their
own: they only ask for your Apple ID, which they use with Apple to make the signature, and Apple may ask for
your two-factor code.

**You need:** an iPhone (iOS 15 or later) and its cable, a Windows PC, an Apple ID, and a Steam account that
owns Slay the Spire 2. The phone needs about 5.5 GB free during setup and 2.5 GB after.

### With Sideloadly

1. Download the newest `STS2Mobile-<version>.ipa` from this repository's **Releases** page.
2. On the PC, install iTunes from Apple's website (not the Microsoft Store version), so Windows can talk to
   the iPhone, then install [Sideloadly](https://sideloadly.io).
3. Connect the iPhone by cable, unlock it and tap **Trust**.
4. Open Sideloadly, drop the IPA on it, pick your iPhone, type your Apple ID and press **Start**. Type the
   Apple ID password and, if asked, the two-factor code.
5. On the iPhone, turn on **Settings > Privacy & Security > Developer Mode** and restart. The switch appears
   only once a sideloaded app is installed; if the iPhone already has it on, skip this.
6. Trust your Apple ID once: **Settings > General > VPN & Device Management**, tap your Apple ID, then
   **Trust**.
7. Open **STS2 Mobile** and follow [First launch](#first-launch): sign in to Steam, and the app downloads the
   game from Steam.

**Every 7 days with a free Apple ID** the app stops opening. Do step 4 again with the same IPA and the same
Apple ID: it installs over the old app, so the game, your saves and your sign-in stay. **Do not delete the
app from the iPhone**: that deletes the game and the saves on it. A newer release installs over it the same
way. A free Apple ID can have three sideloaded apps at once.

### With AltStore or SideStore

Install [AltStore](https://altstore.io) (with AltServer on the PC) or [SideStore](https://sidestore.io) by their
own instructions, then add this source in their **Sources** tab:

```text
https://github.com/MikeMikerson/sts2-ios-launcher-public/releases/latest/download/altstore-source.json
```

Install **STS2 Mobile** from the source, then do steps 5 to 7 above. AltStore and SideStore show new releases
and renew the 7 days for you (AltStore while AltServer runs on the same Wi-Fi). They count toward the three
apps.

### Your own build, still without a Mac

This fork runs **Actions > Build IPA** automatically on branch pushes and external pull requests. You can
also use **Run workflow** to start a build manually. After a successful run, download **STS2Mobile-unsigned-ipa**
from its **Artifacts**; the zip contains `FullGame.ipa` and the app icon. Artifacts are retained for 7 days.
The IPA is unsigned: sideload it with your own Apple ID using the instructions above. No Apple or Steam
credentials are needed by the build. A same-repository pull request uses its branch-push build to avoid duplication. Pushing a tag such as
`v1.0.0` to your fork also publishes a release with its own AltStore source, at the address above with your
fork's owner and name.

## First launch

Open **STS2 Mobile**. It walks you through signing in to Steam (password and Steam Guard code, or a QR code
scanned with the Steam app), checks that you own the game, downloads it (about 1.2 GB), prepares it (about 2
minutes) and warms up its effects (about 6 minutes, once). Keep the app open meanwhile; if it is interrupted
it carries on where it stopped. When it asks, close the app and open it again.

After that it opens on **Play**, which starts the game without connecting to Steam, so you can play offline:
your ownership was already checked when you signed in and downloaded. Below it, **Game version** updates,
switches or deletes the game, **Cloud saves** gets your saves from Steam Cloud or sends them there, **Steam
account** changes the account or signs out, and **Mods** manages mods. None of them touch your saves without
asking.

## Internal TestFlight from GitHub Actions

Register `io.github.paperfront.sts2mobile` as an explicit App ID (no optional capabilities) and create
its iOS app record in App Store Connect. Under this repository’s **Settings > Secrets and variables >
Actions**, add `APPLE_TEAM_ID`, `ASC_KEY_ID`, `ASC_ISSUER_ID`, and `ASC_PRIVATE_KEY` (the complete `.p8`
text). Use an **Admin team API key** so Xcode can use cloud-managed distribution certificates.
An App Manager key can pass development signing and still fail distribution export with a cloud signing
permission error. A generated key’s role cannot be edited: create an Admin key and replace `ASC_KEY_ID`
and `ASC_PRIVATE_KEY` together. No `.p12` or manually downloaded provisioning profile is required.

Choose **Actions > Build IPA > Run workflow**, select the LAN branch, and check **Sign and upload to
internal TestFlight**. The workflow builds and scans the patched Mono app, archives that same export,
lets Xcode manage distribution signing, validates the signed IPA, and uploads it to App Store Connect.
Apple must process the upload before it appears under the app’s TestFlight tab. Add it to your internal
tester group there. The build is marked internal-only; it cannot be distributed to external testers or
the App Store. Normal push/PR builds remain unsigned and never receive the Apple secrets.

## Every 7 days, with a free Apple ID

A free Apple ID's apps stop opening after 7 days. Connect the iPhone and run `./setup.sh --install` again. It
reinstalls over the old app, so the game, your saves and your sign-in stay. **Do not delete the app from the
iPhone**: that deletes the game and the saves on it. Saves on the iPhone reach Steam Cloud only when you press
**Send** under Cloud saves.

## Mods and multiplayer

- **Mods > Import from Files** takes a mod's zip: a resource pack, a code mod, or both. Resource packs that
  contain scripts are refused. A code mod runs with the same access as the app, including your Steam sign-in,
  just as code mods do on a PC, so the app warns you and asks first. Only add mods you trust.
- **Online co-op** works through QuickMatch, a matchmaking mod built into the app when its release is
  available, with voice chat when the build has a relay (see [Multiplayer](#multiplayer) below). Until
  QuickMatch's release is pinned in `tools/dependencies.json`, a fork builds without it, and so without
  QuickMatch matchmaking; everything else, including the mod-free LAN join path, works independently.
  The game’s own Steamworks online transport does not run on iOS.
- **LAN co-op** uses the game’s built-in ENet host/client with Mods off. An iPhone or a desktop can host;
  phones join by Wi-Fi or Tailscale IP. See [LAN multiplayer](#lan-multiplayer-with-friends-no-mods).

## Game versions

The upstream app has been tested with v0.113.0 from Steam’s beta branch, and an iPhone still on v0.111.0 keeps
playing it as tested. This fork’s LAN integration still needs device testing. A newer Beta is still offered, marked
untested, and patched on the iPhone in a tolerant mode; if a change the game needs cannot be made to this
build, the app tells you then. An untested Stable build is not offered until the app is tested with it. The
app never updates the game by itself.

## Troubleshooting

`./setup.sh --install` stops with a message that says what to do, and keeps its logs in `.work/logs/`.

- **"No Apple ID is signed in to Xcode"**: add it in Xcode's **Settings > Apple Accounts**.
- **"Developer Mode is off"**, **"is not paired"**, **"No iPhone is connected"**: see step 2, and keep the
  iPhone unlocked and connected for the whole build.
- **A signing error** (in `.work/logs/mono-xcode.log`): open Xcode's **Settings > Apple Accounts**, wait for
  your team to load, and run the command again. A free Apple ID allows 10 new app ids a week, so keep
  `.local/device.json` rather than making new ones.
- **The app limit**: a free Apple ID can have 3 of its apps on the iPhone. Delete one of the others.
- **"Untrusted Developer"**: see step 4. **"Unable to verify app"**: connect the iPhone to the internet and
  trust it again.
- **"This checkout is validated with Xcode 27.2"**: install that version, or point to it with
  `DEVELOPER_DIR=/path/to/Xcode.app/Contents/Developer ./setup.sh --install`.
- **A tool download fails**: run the command again.
- **Steam's Stable version is not offered**: choose the Beta if Steam has one, or wait for an update of this
  app. Nothing is downloaded or deleted.
- **Steam Guard**: use the code from the Steam app or Steam's email. The QR sign-in needs the Steam app on
  another device.

## Security and privacy

This section is for anyone who wants to check the claims above. Each point names where it lives in the code.

### Your Steam sign-in

- The app signs in with SteamKit2, an open-source Steam client library, directly to Steam's servers. Your
  password goes only there and is never written to disk. The QR sign-in sends no password at all.
- The sign-in is kept on this iPhone: only your account name and the refresh token Steam issues (no
  password, no Steam Guard data), and, once Steam has confirmed that the account owns the game, its Steam ID
  and Steam name, in one file encrypted with **AES-256-GCM**
  (`src/STS2Mobile.FullGame/KeychainSecureStorage.cs`). The key is random, 256 bits, and kept only in the
  **iOS Keychain** (`src/STS2Mobile.Platform/AppleKeychain.cs`), readable only while the iPhone is unlocked
  and only on this device (`kSecAttrAccessibleWhenUnlockedThisDeviceOnly`). It is never in iCloud or device
  backups and does not move to another phone; without it the file is useless, and the app asks you to sign
  in again.
- Steam checks that the account owns the game when you sign in and before every download, update or switch
  of version (`src/STS2Mobile.Steam/Auth/OwnershipVerifier.cs`). Play does not ask again: the game could only
  be downloaded after that check, so Play and the game's own start check use the stored sign-in, its Steam ID
  and Steam name, and need no connection (`src/STS2Mobile.Launcher/SteamLoginSession.cs`). A sign-in stored
  by an older version of the app has no Steam ID yet, so its next Play checks with Steam once. Signing out
  ends this, and so does a later check that finds the account no longer owns the game. Offline, Cloud saves
  and Game version say that Steam could not be reached, and QuickMatch cannot find or host a match. The one
  wait is the first Play on an iPhone with no saves yet, which still offers your Steam Cloud saves: offline,
  that gives up after about 15 seconds and the game starts.
- **Steam account > Sign out** signs out of Steam on this iPhone: it deletes the stored sign-in and its key,
  and asks Steam to end the sign-in. The game and your saves stay, and you sign in again to play. If Steam
  cannot be reached it still signs out on the phone, and you can remove the sign-in under **Authorized
  Devices** in your Steam account settings.
- The app's files are not shared with the Files app or a computer (file sharing is off).

### What the app connects to

Web and relay connections use policies that allow only HTTPS or WSS on port 443 to named hosts,
and refuse IP addresses, redirects, proxies, cookies and overridden Host headers
(`src/STS2Mobile.Steam/Network`, `src/STS2Mobile.Platform/GameNetworkPolicy.cs`). The allowed destinations:

- **Steam**: sign-in, the ownership check, the game download (from `steamcontent.com`), Steam Cloud and lobbies.
- **Steam Cloud's storage** (on Amazon, Google and Microsoft clouds, at the address Steam gives for each file):
  only when you press Get or Send. The client that uploads to it can only write, never read or delete. A
  Send may still remove a cloud file the iPhone no longer has (such as a finished run's save), through Steam's
  own cloud API over the signed-in connection, never through the storage hosts.
- **Mega Crit's and Steam's websites**, for the game's own web requests.
- **A QuickMatch relay**, only if the build has one, only during a match (below).
- **Private LAN/Tailscale game peers**, when you choose Host/Load or enter an IP and tap Join LAN.

Nothing else. [NETWORK.md](NETWORK.md) lists each rule and the allowed hosts.

### What was removed from the game

When the app prepares the game on your iPhone, it patches the game's code
(`src/STS2Mobile.GamePatcher`):

- Sentry crash reporting is removed completely.
- Run, achievement and settings stats, and the feedback upload (logs and a screenshot), are removed: nothing
  is uploaded to anyone.
- Raw network sockets are refused. ENet hosting binds only the selected private/Tailscale interface;
  ENet joining permits private/Tailscale IP addresses only.

The app's own log (`Documents/full-game-startup.log`) stays on the iPhone and is never sent anywhere.

### Multiplayer

- QuickMatch finds and hosts matches through Steam lobbies, over your signed-in Steam connection.
- **Relay.** The person who builds the app chooses whether to use one: `QUICKMATCH_RELAY=wss://<host>
  ./setup.sh --install`, or a `.local/QuickMatchRelay.cfg` file with `relay = https://<host>`. Without one,
  matches use Steam lobby chat (slower, no voice). With one, the app connects to that one host only during a
  match, and sends QuickMatch's encrypted game messages to the other players. Your Steam sign-in never goes
  there; the relay sees your IP address, Steam ID and the timing and size of messages.
- **Microphone.** Used only for QuickMatch's voice chat. iOS asks the first time you turn your mic on in a
  match, never at launch. It records only while the mic is on, and the voice goes encrypted to the players in
  that match over the relay. A build without QuickMatch never asks for the microphone.

### Files and mods

- **Import from Files** sees only the one zip you choose, as a copy. Paths that leave the mod's folder, links
  and oversized files are refused (`src/STS2Mobile.Platform/ModPackageImport.cs`).
- Code mods are trusted code, as on a PC: they are checked for some dangerous calls, but that is not a
  sandbox. The app warns before adding one.

### The build

- Every toolchain and archive setup downloads is checked against a hash pinned in `tools/dependencies.json`
  before use: SHA-512 for Godot, its export templates, .NET and the LibraryBuilder pack, SHA-256 for the rest.
  `setup.sh` pins uv, which installs Python, by SHA-256.
- Python packages install only as the wheels whose SHA-256 `tools/requirements.txt` pins
  (`--require-hashes`, no source builds). Every NuGet package, direct or transitive, is locked to its content
  hash in the `packages*.lock.json` next to each project, and a restore that would differ stops the build.
- The .NET iOS workload comes from Microsoft's signed NuGet feed through `dotnet workload install`, at the
  workload set version pinned in `tools/dependencies.json`.
- The repository and the built app hold no file from the game. The build ends by scanning the app for game
  files (`tools/scan_no_game_files.py`).

## For developers

- [BUILDING.md](BUILDING.md): the build pipeline, the checks, the tests and the test switches.
- [NETWORK.md](NETWORK.md): every network rule, in full.
- [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md): the components the app is built from, and their licenses.
- [LICENSE](LICENSE): MIT, for this repository's own code. Third-party parts keep their own licenses.
