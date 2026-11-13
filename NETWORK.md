# Network boundaries

The running app permits HTTPS or WSS on port 443 to Steam and Mega Crit, QuickMatch's relay as described below, and explicit outbound LAN-game connections described in the next section. Domain boundaries, TLS, port restrictions, redirects, proxy bypass, Host-header checks, and URL override protections are enforced. The allowed hosts:

| What | Allowed hosts | Scheme and port | Rule in the code |
| --- | --- | --- | --- |
| The Steam connection and the launcher's Steam web calls: sign-in, ownership, lobbies, cloud calls, the game download | `steampowered.com`, `steamcommunity.com`, `steamcontent.com`, `steamstatic.com`, `steamserver.net`, and names under them | HTTPS or WSS, 443 | `src/STS2Mobile.Steam/Network/SteamNetworkPolicy.cs` (`IsAllowed`) |
| Steam Cloud's file storage, for Get (GET only) and Send (PUT or POST only) | `steamcloud-<name>.s3.dualstack.<region>.amazonaws.com`, `steamcloud-<name>.storage.googleapis.com`, `steamcloud<name>.blob.core.windows.net` | HTTPS, 443 | `SteamNetworkPolicy.IsCloudStorage` and `src/STS2Mobile.Steam/Network/CloudStorageAccess.cs` |
| The game's own web requests | `megacrit.com`, `steampowered.com`, `steamcommunity.com`, `steamcontent.com`, `steamstatic.com`, and names under them | HTTPS, 443 | `src/STS2Mobile.Platform/GameNetworkPolicy.cs` |
| QuickMatch's relay, only during a match, only in a build that has one | the one host named at build time, paths `/v1/relay/` plus `auto`, `apac`, `na` or `eu` | WSS, 443 | `RelayNetworkPolicy` in `src/STS2Mobile.Platform/RelayConnection.cs` |

Every web/relay rule above refuses IP addresses, user names and passwords in the address, and any other port. The clients that use them follow no redirects and use no proxy, cookies or overridden Host header.

Steam Cloud saves move only when the player asks on the launcher's cloud saves screen and confirms what it shows; nothing syncs on its own. The Steam Cloud calls themselves (listing files, asking for a download, beginning and ending an upload batch, beginning and committing a file upload, deleting a file) go over the signed-in Steam connection. The file contents go to and from Steam Cloud's storage hosts on Amazon S3, Google Cloud Storage, and Azure, at the signed address Steam gives for each file. Only Steam Cloud's own bucket names in each provider's virtual-host form, over HTTPS on port 443, are accepted, and only by two clients:

- The cloud-download client may GET from those hosts, for Get.
- The cloud-upload client may only PUT or POST, to those hosts or to Steam's own hosts, for Send. It refuses every other method, so it cannot read or delete anything.

They are not general app destinations, and both clients keep every other rule above (no redirects, no Host override, no proxy). A Send writes the saves that differ from Steam Cloud's, first keeping Steam Cloud's copies of what it replaces in a backup on the phone. It removes from Steam Cloud only files that the phone no longer has inside a profile the phone has, such as a finished run's `current_run.save`, and never run history. Each upload logs the storage host it went to (host only; the signed path is never logged).

The game's download comes from Steam's content servers, under `steamcontent.com`, through the same policy-checked client. Servers Steam offers outside that policy (other hosts, plain HTTP, proxies) are skipped. It starts only when the player asks.

Steam-lobby matchmaking runs through QuickMatch, an optional code mod built into the app when setup has it (it runs only with mods on). A build without QuickMatch can still join a desktop LAN host directly, including over Tailscale. Its lobbies (search, host, join, lobby data and lobby chat) go over the signed-in Steam connection, like the cloud save calls. The game’s own Steamworks multiplayer does not run on iOS. Its ENet client is separately enabled for manual LAN joining, with Mods off as described below; raw sockets and ENet hosting remain disabled. QuickMatch's voice chat is the only use of the microphone. A build that carries QuickMatch says so in its microphone text; iOS asks the first time the player turns their mic on in a match, never at launch. Recording runs only while the mic is on, the voice is offered only when the build has the relay, and QuickMatch sends it only over the relay, encrypted, to the players of that match. A build without QuickMatch has no microphone text and never records.

QuickMatch's relay is the one additional web destination outside Steam and Mega Crit; explicit LAN peers are a separate transport below. It is not part of QuickMatch's release: whoever builds the app chooses it at build time, with `QUICKMATCH_RELAY=wss://<host>` (or `https://<host>`) in the environment when running `./setup.sh`, or a `.local/QuickMatchRelay.cfg` file of `key = value` lines and `#` comments (`relay = https://<host>`, and optionally `enabled = true` or `false`). Setup checks either setting as the app does, stops on a problem, and builds the relay into the app as `QuickMatchRelay.cfg`. The app accepts only a host name on port 443, with no IP address, path or query. The app connects to the relay only during a QuickMatch match, and only to that one host, over WSS on port 443, to the relay's room paths only, with no proxy, redirects, cookies or Host override. It carries QuickMatch's encrypted game messages between the players of one match. The Steam sign-in, its tokens and any Steam credential never go to it. The relay sees each player's address, the match's room token, Steam IDs, message sizes and timing. With neither setting the build has no relay and never connects to one: QuickMatch matches over Steam lobby chat, which is slower, with no voice chat.

## Direct LAN joining

With Mods off, the game’s Multiplayer > Join screen offers a manual IP field and Join LAN button.
A desktop hosts using the built-in `--fastmp` mode. No discovery broadcast or desktop mod is required.
The app declares `NSLocalNetworkUsageDescription`; iOS controls permission to reach the local network.

`LanNetworkPolicy` permits literal RFC 1918 IPv4 addresses, Tailscale IPv4 (`100.64.0.0/10`), and
Tailscale IPv6 (`fd7a:115c:a1e0::/48`), on a user-entered port from 1–65535 (default UDP 33771).
It rejects DNS names, public IPs, loopback, link-local and IPv4-mapped IPv6. There is no DNS resolution.
The join UI validates before calling the game, and the patcher injects the same guard at the game’s actual
`Godot.ENetConnection.ConnectToHost` call. Unexpected client shapes or signatures fail preparation.
Other ENet openers and `System.Net.Sockets` methods stay disabled. The app does not listen for LAN games.

The game’s native ENet handshake and game messages go to the selected host; the launcher’s Steam password,
refresh token and Steam connection do not. Native LAN traffic is separate from the HTTPS-only web policy;
Tailscale provides tunnel encryption when that network is used. Connect only to a host you intend to play
with. This patch does not add a gameplay-password or identity-authentication protocol to the game.

The build-time setup process downloads toolchains and dependencies from the pinned sources in its manifest. These build-time downloads are separate from the app's runtime network policy.

Steam credentials remain in device-local protected storage. The Keychain service is derived from the installed app's bundle identifier. Build tools do not request Steam credentials.
