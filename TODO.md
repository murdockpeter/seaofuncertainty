# Sea of Uncertainty — Active TODO

Last refreshed against the repository: **2026-09-09**
Unity baseline: **6000.2.12f1**
Completed work through the content/simulation, authoritative replay, and print-proof milestones is preserved in [`design/archive/TODO_CORE_LOOP_ALPHA.md`](design/archive/TODO_CORE_LOOP_ALPHA.md).

This file contains only unfinished work. Current status: **259 completed items / 40 active items remaining**.

Checkbox rules:

- `[ ]` means incomplete.
- **Human QA** and **Playtest** items require human evidence and cannot be closed by automated checks alone.
- Release gates remain open until their stated evidence exists, even when their underlying feature is implemented.
- Playtest-dependent simulation changes stay deferred until the relevant comparison has been run.

## 1. Human playtest program

Run the baseline sessions before using comparison results to alter rules or simulation detail. Use `design/PLAYTEST_PROTOCOL.md` and record observations after every session.

### Baseline sessions and usability

- [ ] **Playtest:** Complete at least one full Meridian Veil Solo session without developer intervention.
- [ ] **Playtest:** Complete at least one full Meridian Veil Local Hotseat session with secure handoffs.
- [ ] **Playtest:** Validate Luzon maneuver space, chokepoints, Search ranges, and scenario duration.
- [ ] **Playtest:** Test selection of small formations, dense Contacts, coastal hexes, and overlapping markers.
- [ ] Record obvious choices, repeated lookups, inactive-player lulls, unfair rolls, unused actions, and dominant actions after every session.

### Rules comparisons

- [ ] **Playtest:** Compare automatic Defend with player-selected reactions.
- [ ] **Playtest:** Compare Age-2 targeting penalty on versus off.
- [ ] **Playtest:** Compare High Tempo always marking Friction versus conditional marking.
- [ ] **Playtest:** Compare universal entropy penalties plus cards versus cards replacing universal penalties.
- [ ] **Playtest:** Compare Endurance degradation after three versus four major Actions.
- [ ] **Playtest:** Compare two versus three Command Slots.
- [ ] **Playtest:** Compare player-chosen Contact improvement versus fixed Location-first improvement.
- [ ] **Playtest:** Compare exact-hex Contact labels with spatial uncertainty regions.
- [ ] **Playtest:** Test sequential Defense penalties for second and third synchronized attacks.

### Physical and accessibility validation

- [ ] Conduct blind rules teach-and-play tests.
- [ ] **Human QA:** Test interactive resizing, native fullscreen, DPI 100/125/150/200%, multi-monitor behavior, mouse-only operation, and the full keyboard sweep.
- [ ] Validate the complete palette with color-blind simulators and human testers.
- [ ] Test screen-reader-compatible menu and rules structures where feasible. **Implemented:** stable semantic names for dialog, region, action, text, and audio-description elements; human assistive-technology verification remains required.

## 2. Playtest-dependent and deferred simulation

- [ ] Add named Patrol/Screen route-segment assignment after plotted routes exist.
- [ ] Implement additional weather and sea-state movement effects only after playtesting earns them. **Implemented baseline:** authoritative weather severity already affects Search, air operations, and naval High Tempo movement.

## 3. Target-hardware validation

- [ ] Add performance captures on minimum and target Windows hardware.

## 4. Print production

Content-proof HTML is generated from the runtime catalogs under `SeaOfUncertainty/generated/print-and-play`. The remaining work is production and human validation.

- [ ] Produce print-ready files with bleed, safe areas, and licensing attribution.

## 5. Multiplayer

The production network foundation is implemented, but public activation remains gated on human-tested Local Hotseat privacy and live two-client service validation.

- [ ] Complete and human-test Local Hotseat privacy before networking.
- [ ] Conduct latency, reconnect, host-migration, and adversarial information-leak testing with two authenticated builds over direct IP and Unity Relay. **Automated:** deterministic loopback fault, wire-authentication, side-scoped response, tamper, interrupted-recovery, activation-gate, and RTT-protocol tests pass. **Developer harness:** account-free direct-IP host/join plus optional private Relay, readiness, host-only authority start, clean leave, and live RTT controls are available; the Unity Cloud project is not linked yet, but it no longer blocks direct testing.

## 6. Release gates

### Core-loop alpha

- [ ] The defender receives a meaningful, legal reaction choice during combat in blind human testing. **Implemented:** Defend, Evade, Counterattack, Hold, secure Hotseat handoff, Solo human routing, and AI choice are active.
- [ ] Players can explain why every formation acts next in blind testing.
- [ ] Players understand calculations and consequences before committing in blind testing.
- [ ] Contact uncertainty has a clear spatial meaning in blind testing.

### Rules alpha

- [ ] Every implemented rule has an approved written ruling and deterministic test.
- [ ] Every Entropy and Command Response card either functions fully or is excluded from playable decks.
- [ ] No dominant action persists across target playtests.
- [ ] The inactive player regularly receives meaningful decisions.
- [ ] Entropy produces understandable, recoverable capability problems.
- [ ] Solo AI completes scenarios legally and provides credible baseline opposition in human testing.

### Public prototype

- [ ] New players complete a scenario without designer assistance.
- [ ] Save/load, fullscreen, settings, and supported resolutions pass target-hardware QA.
- [ ] Accessibility baseline passes keyboard, contrast, reduced-motion, audio-description, and text-scaling review.
- [ ] Every distributed asset has recorded provenance and suitable licensing.
- [ ] Crash, rules, telemetry, performance, and usability testing pass on target hardware.
- [ ] Unsupported cards and unfinished systems are either completed or removed from public decks/UI.

## Completed milestone summary

The archived roadmap records repository evidence for these completed bodies of work:

- all eight core gameplay verbs and continuous Ready-Time scheduling;
- reactions, Patrol/Screen, Support, Replenishment, Command Attention, and Standing Missions;
- movement, Search uncertainty, combat, damage, Entropy, card decks, synchronized strikes, Endurance, and scenario scoring;
- save migration, telemetry, application flow, UI Toolkit, accessibility implementation, audio, and Windows builds;
- 3D operational-map presentation and information-disciplined deterministic AI;
- post-alpha surface, air, carrier, submarine, missile-defense, weapon, EW/cyber, logistics, weather, command-architecture, scenario-authoring, and scheduled-event systems;
- authoritative commands, deterministic replay, and generated print-and-play content proofs.
