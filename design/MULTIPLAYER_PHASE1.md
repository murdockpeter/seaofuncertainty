# Multiplayer Phase 1 — authoritative loopback foundation

## Scope and gate

Phase 1 builds and tests the information-security and synchronization boundary without selecting or integrating an Internet transport. Public networking, lobbies, invitations, deployment, and interrupted-match persistence remain gated on a human-tested Local Hotseat privacy session and a production transport decision.

## Authority model

`AuthoritativeMatchServer` owns the complete `PrototypeGame`, seed, replay document, Contact-reference mappings, sequence counters, receipts, and dispute diagnostics. A client submits a versioned `ClientCommandEnvelope` containing:

- match and side identity;
- per-side command sequence;
- the digest of the client's expected side view;
- one existing `AuthoritativeCommand`.

The authority rejects wrong-match, wrong-version, ownership, out-of-order, and stale-view submissions before mutation. Accepted rule choices execute through `AuthoritativeCommandProcessor`; duplicate transport delivery returns the original receipt without applying the command twice.

## Information boundary

`SideSnapshot` contains only:

- the viewer's formations;
- the viewer's Command state and Response hand;
- the viewer's permissible operational log;
- public scenario, Time, active side, and weather state;
- Contacts owned by the viewer.

Contact snapshots use opaque `TRACK-B-001` / `TRACK-R-001` references and omit the authoritative target formation ID. The server translates an opaque reference only after envelope validation. An opponent's formation ID and name therefore do not cross the authority boundary even when resolving a Strike against that Contact.

Full-state hashes remain server-private. Rejected receipts expose an opaque diagnostic ID and the affected side-view digest; the corresponding `ServerDisputeDiagnostic` retains client-view, authoritative-view, and full-authority hashes for trusted operator inspection. This avoids turning the full state hash into a client-visible information oracle.

## Recovery and fault behavior

Reconnect compares the client's last acknowledged sequence and side-view digest. A current client resumes without redundant state. A stale client receives a fresh side-scoped snapshot and next expected sequence. The existing replay document provides deterministic authority-side event evidence.

`LoopbackMultiplayerTransport` is the Phase 1 transport adapter and deterministic fault harness. It can introduce:

- fixed tick latency;
- periodic packet loss;
- duplicate delivery;
- reverse-order ready batches.

It is not the production network transport.

## Verification

`MultiplayerFoundationTests.Run` verifies both side snapshots, opaque Contact translation, hidden-ID/name exclusion, latency, loss, duplicate idempotency, out-of-order rejection, stale-view rejection, reconnect recovery, replay-event count, and server-private diagnostics. Production-transport latency and adversarial testing remain open.
