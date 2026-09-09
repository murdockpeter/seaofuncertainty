# Multiplayer production architecture

## Decision

Sea of Uncertainty uses a two-player authoritative listen-server topology for its first online release:

- Unity Multiplayer Services SDK 1.2 Sessions own lobby membership, join codes, session properties, reconnect, Relay allocation, heartbeat, and host migration coordination.
- Unity Relay carries DTLS-encrypted traffic, avoids exposing player IP addresses, and handles NAT/firewall traversal.
- Unity Transport is used directly through a custom `INetworkHandler`. Netcode for GameObjects is intentionally not used because the game synchronizes versioned decisions and side-scoped state, not scene objects.
- The Blue host owns `AuthoritativeMatchServer`. A future dedicated server can replace the listen host behind the same command and snapshot contract without changing the rules engine.

The package is pinned as `com.unity.services.multiplayer` 1.2.0. Its resolved Unity Transport dependency is recorded in `Packages/packages-lock.json`.

## Lobby and match flow

`MultiplayerMatchLifecycle` provides the transport-neutral lifecycle:

1. Create a private two-seat lobby and reserve Blue for the host.
2. Share a normalized join code or `seaofuncertainty://join/<code>` invitation.
3. Join the Red seat, publish member-visible name/side/ready properties, and reject third players.
4. Require both connected commanders to mark ready; only the authenticated host may lock setup and start.
5. Start Relay with DTLS after setup, so pre-match choices finish before gameplay networking begins.
6. Submit only `ClientCommandEnvelope` messages. The host returns a `CommandReceipt` only to the requesting transport peer.

`UnityMultiplayerSessionGateway` maps that lifecycle onto MPS Sessions. Public indexed properties contain only scenario and rules compatibility. Seed, phase, player name, side, and ready state are members-only. Full simulation state is never stored as a public session property.

## Activation surface

The operation-mode screen exposes **Online Multiplayer** in every build, Editor and release alike. Two paths share the same authoritative lifecycle and wire protocol:

- **Unity Relay** is the primary path: managed membership/join codes, NAT traversal, DTLS encryption, IP concealment, and host-migration coordination. It requires the Unity project to be linked to a Cloud project (`ProjectSettings.cloudProjectId`/`organizationId`); an unlinked project surfaces this as an actionable setup error rather than a raw exception.
- **Direct IP** needs no Unity account or Cloud project. A host listens on a chosen UDP port; a client supplies a numeric IP address, port, and private match code. Both seats authenticate, ready, and start the authority through the same UI. Internet hosts may need firewall and UDP port-forwarding configuration. The UI only shows Direct IP as a fallback while Relay is disabled (see below) — it is not offered side-by-side with Relay under normal conditions.

Both paths can measure guest-to-authority round-trip time through the state-free ping/pong protocol. Direct IP uses Unity Transport's reliable-sequenced UDP pipeline rather than TCP because the existing command framing, ordering, fragmentation, and retransmission guarantees are already implemented there. Direct IP itself is not encrypted and exposes the host address, so Internet use should run through a trusted LAN/VPN until application-layer encryption is added; Relay remains the safer public-Internet path.

This currently stops at the authority transport boundary: routing the complete playable command UI through side-scoped receipts is separate follow-on work.

## Remote kill switch

`MultiplayerKillSwitch` (`Assets/Scripts/Prototype/MultiplayerKillSwitch.cs`) fetches the Unity Remote Config boolean `multiplayer_enabled` (and string `multiplayer_disabled_message`) before every Relay host/join attempt (`OnlineMultiplayerCoordinator.HostAsync`/`JoinAsync`), and the online screen also fetches it proactively on entry to decide which buttons to draw. Setting `multiplayer_enabled` to `false` in the Unity Dashboard and publishing stops new Relay sessions for every client within seconds, with no rebuild, and reveals Direct IP as a fallback in the UI — this is the operator response to a Unity Gaming Services budget alert. The fetch fails open (defaults to enabled) if Remote Config is unreachable, so a transient network issue never blocks legitimate play.

## Network protocol and privacy

`AuthoritativeUtpTransport` uses Unity Transport's reliable-sequenced and fragmentation pipeline with a 64 KiB application payload ceiling. The authority-side `AuthoritativeNetworkHostBridge` routes every response back to the originating peer instead of broadcasting it.

`AuthoritativeWireProtocol` supports command, receipt, reconnect, and ping/pong packets. Nested commands, receipts, and reconnect snapshots are encoded as explicit JSON payload strings so an error response cannot accidentally serialize a default snapshot. Invalid credentials receive an error with no game state.

Reconnect credentials are high-entropy values distinct from shareable join codes. Public lobby copies omit even their digests. The authority stores only credential digests and compares them without an early-exit character comparison.

## Reconnect, interruption, and host migration

A momentary reconnect first restores MPS session membership, then sends the player's last acknowledged sequence and known side-view digest. A current player resumes without a redundant snapshot; a stale player receives a fresh snapshot for only the reserved side.

Authority checkpoints contain the lobby's private credential digests and the deterministic authoritative replay, never raw reconnect credentials. They are HMAC-SHA256 signed. Restore rejects modified data, rebuilds the simulation by replay, marks both seats disconnected, and resumes only after both authenticate. Packets from before the checkpoint are recognized as recovered duplicates and cannot mutate state twice.

The gateway supplies the same checkpoint bytes to MPS's custom `IMigrationDataHandler`. Relay region preservation is enabled when Relay starts. The next elected host must apply the migrated authority checkpoint before accepting commands.

## Automated evidence

`MultiplayerFoundationTests.Run` covers:

- lobby creation, invite normalization, capacity, ready state, and host-only start;
- credential separation, unauthorized wire responses, and opponent-side submission rejection;
- signed checkpoint tamper rejection and deterministic replay restoration;
- side-scoped reconnect snapshots and recovered duplicate suppression;
- opaque Contact references and hidden enemy ID/name exclusion;
- deterministic latency, loss, duplication, reversal, stale-view recovery, and diagnostics;
- state-free ping/pong packets for live RTT measurement.
- release/development gating for the online validation surface.

The suite is included in `RunAllRegressionTests`.

## Remaining live gates

These require external state and cannot be certified by a batch test:

- link the Unity project to its production UGS project/environment and review Authentication/DSA notification requirements;
- complete the Local Hotseat privacy playtest before enabling public online play;
- run host and guest builds through Relay on separate networks;
- capture RTT and packet behavior under target latency/loss, force reconnects, terminate the host during play, and verify elected-host recovery;
- attempt adversarial packets and inspect both clients, logs, and traffic captures for hidden formation IDs, names, full-state hashes, private hands, and opponent-only logs.

Do not close the remaining multiplayer TODO gate until this evidence is attached to a playtest or QA record.
