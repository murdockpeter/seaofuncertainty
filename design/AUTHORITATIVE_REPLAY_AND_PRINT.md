# Authoritative replay and print generation

## Command contract

`AuthoritativeCommand` is the portable description of an intended game-state mutation. Version 1 uses stable IDs and explicit values rather than references to Unity scene objects or controls. Supported command kinds cover ordinary formation actions, Standing Missions, Command Responses, Entropy responses, Push Through, HQ command restoration, and the declaration, retasking, resolution, or abortion of Synchronized Strikes.

Each command states the issuing side and acting formation. The processor rejects missing formations, ownership mismatches, unsupported versions, inactive actors, unusable owned Contacts, and rule-invalid choices through the same `PrototypeGame` methods used by human and AI presentation layers. Commands never carry private target state.

## Event and replay contract

An `AuthoritativeEvent` records whether a command was accepted, its outcome, operational Time before and after resolution, and SHA-256 digests of authoritative save state before and after resolution. A replay document contains:

- format version;
- scenario ID;
- initial random seed;
- ordered commands;
- corresponding resolved events.

Replay constructs a fresh scenario from the catalog and seed, then applies commands in sequence. It stops at the first unexpected pre-state, acceptance result, post-state, or operational Time and reports that sequence as a divergence. Event text is diagnostic; state equality is determined by the digest.

This is the foundation for future network command synchronization, reconnect replay, desynchronization reporting, and dispute inspection. It does not select a network authority or transport.

## Print-and-play proofs

Use **Sea of Uncertainty → Generate Print-and-Play Proofs** in Unity. The generator reads the same scenario, formation, Entropy, and Command Response catalogs used at runtime and writes regenerable HTML proof sheets plus a JSON manifest beneath `SeaOfUncertainty/generated/print-and-play`.

Generated files include:

- formation cards with ratings and weapon inventories;
- formation counters, owned Contact markers, and False Contacts;
- Ready-Time, Command, Endurance, weapon, and Entropy tracks;
- all 36 Entropy cards and 24 Command Response cards;
- scenario setup tables, special rules, victory conditions, and an action aid.

These are functional content proofs. Bleed, safe-area preflight, final imposition, visual polish, and licensing attribution remain part of the later print-ready release task.

## Verification

`SharedArchitectureTests.Run` records and JSON-round-trips a 45-command AI sequence, reconstructs a fresh game from scenario plus seed, verifies every state digest, confirms the final digest, rejects an ownership mismatch, and detects a deliberately altered first command. It also regenerates and inspects the complete proof set and card counts.
