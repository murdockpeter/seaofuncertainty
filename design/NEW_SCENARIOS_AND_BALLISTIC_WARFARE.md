# New scenarios and ballistic warfare

Planning baseline: 2026-10-15. This document proposes implementation; it does not describe shipped rules. The user selected planning first, four scenarios on existing theaters, and capability-based ballistic missile defense. Numerical values and scenario details below remain design proposals until implementation and playtesting.

## Intended outcome

Add four operations with different deployments, objectives, and missile-defense decisions. Support ballistic missile units and explicit ballistic attack/defense capabilities throughout the formation model. Equipped formations can protect nearby friendly task groups. A zero capability is valid: sharing the model does not grant every submarine, aircraft package, or fleet train an interceptor.

The original Meridian Veil and Northern Gateway scenarios remain regression baselines. The new package expands the two-scenario beta target; scope and content freeze remain outstanding in TODO.md.

## What the repository supports today

- The runtime has two compiled scenario factories. The JSON catalog overrides entries only when their IDs match those factories; a new JSON ID is currently skipped.
- The authored schema exposes metadata and formations, but does not expose the runtime's complete area, objective, Contact, range, defense, region, or event definitions.
- Missile defense uses formation-kind profiles with Outer, Area, and Point modifiers selected by salvo size. There is no ballistic weapon category or separate interceptor magazine.
- Formation kinds are CarrierGroup, SurfaceGroup, Submarine, AirGroup, and LogisticsGroup. There is no land-based missile formation or explicit task-group membership model. Existing group formations already abstract multiple platforms.
- Save data is currently version 11. Scenario schema is 4 and authored catalog version is 1. Inspect the versions again when implementing and migrate every affected format.
- Victory rules include Control, Transit, Escort, Denial, and Withdrawal. Escort currently refers to a carrier; a convoy objective needs explicit target selection rather than a briefing-text change.

## Implementation order

### 1. Make scenario authoring complete (SCN-BM-001)

Add a stable theater-template reference for new authored scenario IDs. Initially allow the Meridian Veil Archipelago and the existing Luzon theater. Resolve fresh area/state objects for each scenario so editing one scenario cannot mutate another through shared lists.

Allow scenario-local objectives, regions, initial Contacts, sensor/weapon/defense profiles, scheduled events, deployments, command architectures, and weather. Define explicitly whether each field inherits or replaces template content; empty lists must be distinguishable from omitted overrides where clearing is supported. Do not accidentally inherit the template's forces, reinforcements, or scoring conditions.

Validate unique IDs, enum values, references, legal terrain/deployment, finite nonnegative inventories, objective targets, and reachable entry/exit regions. Surface clear authoring errors for unknown templates and IDs. Preserve legacy runtime fallback only through the documented compatibility path; authoring must report invalid input.

Update the Scenario Catalog Editor and selector. A new valid catalog entry must appear and load under its own stable ID without another compiled scenario factory. Briefing text must agree with executable scoring.

### 2. Add ballistic weapons and launch units (SCN-BM-002)

Represent weapon delivery separately from Light/Standard/Heavy commitment: salvo size is not a weapon type. Add explicit authored ballistic profiles with range, attack modifier, allowed target domains, and ammunition limits. All formation types use the capability schema; values are set per formation rather than inferred solely from its kind.

Introduce a mobile land-based BallisticMissileBattery formation. Append new enum values to preserve serialized numeric meanings. Define land deployment and movement, search/contact domain, signature, launch exposure, logistics access, damage restrictions, and AI behavior. Existing naval movement rules cannot be reused unchanged for land units. Audit every formation-kind switch and default in rules, presentation, scoring, and print generation.

Initial ballistic engagements use the normal Contact and aim-hex system. A launch requires a legal owned Contact, permitted target domain/quality, range, ammunition, and an authorized action. Spend ammunition after commitment even for an empty, stale, or false aim, and report only no confirmed effect when appropriate. Launch exposure must follow an explicit information rule; it cannot automatically disclose the battery's exact position.

Permit ballistic loadouts on carrier/surface/submarine/air abstractions when authored, with zero as the default for legacy data. Distinguish the capability of a carrier group from the equipment of its represented escorts. The first implementation resolves launch and interception within the existing Strike window; missile flight tracks and extra strategic time scales are future work.

### 3. Add ballistic defense and group coverage (SCN-BM-003)

Use explicit ballistic interception capability, interceptor capacity/current inventory, coverage radius, legal protected domains, and any self-defense restrictions. Existing generic missile-defense and EW bonuses must not automatically counter ballistic attacks. Specify any applicable terminal-defense or EW effects explicitly in the ballistic rules.

Every formation can be represented as a protected target when the attack profile permits that target domain. Only equipped formations can intercept. Air and submarine targets require explicit domain and depth eligibility; a nearby escort must not silently make an otherwise illegal attack legal.

Protect the current group abstractions through friendly spatial coverage. No new platform-by-platform fleet roster or persistent task-group membership is needed for the first slice. A carrier group's own interceptor capability can represent its organic escorts; separately modeled escorts contribute only under the coverage rules.

Proposed first-slice defense procedure:

1. After a legal ballistic strike commits and the normal target/reaction procedure applies, collect eligible friendly defenders, including the target if equipped.
2. Check coverage distance, protected domain, current damage, readiness/status restrictions, and remaining interceptors against authoritative state.
3. Select one provider by highest effective interception rating, then shortest distance, then stable formation ID. Use a bounded modifier in the existing combat comparison; do not add unbounded stacked bonuses or a new per-missile simulation.
4. Spend one interception package from that provider and apply its configured modifier. Each package abstracts engagement of the committed salvo. Salvo-dependent modifier/consumption must be explicit if later introduced.
5. Resolve combat and record the interception result in side-scoped receipts and telemetry. Never disclose an otherwise unknown escort's identity or position to the attacker.

Use automatic provider selection initially so the feature fits the existing single reaction window. Whether a provider also spends its ordinary Reaction must be encoded and explained consistently; proposed baseline is separate ammunition expenditure without consuming that Reaction. Destroyed, crippled, or replenishing providers cannot contribute. Replenishment restores interceptors only through a compatible authored logistics service.

Synchronized participants resolve in existing stable order. Recompute providers after each hit/expenditure so depleted or destroyed coverage cannot protect later participants. Test overlapping coverage, no available provider, self-defense, coverage boundaries, and exhaustion.

### 4. Carry the rules through the product (SCN-BM-004)

- Core/authority: version ballistic weapon selection and serialized state through commands, synchronized-strike declarations, snapshots, replay digests, and save migration. Legacy commands and saves retain conventional behavior and zero ballistic capability.
- AI: choose legal weapon types and targets using only its authorized knowledge; manage finite ammunition and defensive positioning. Hidden enemy defense must not become an AI input.
- UI: expose owned launch capability, ballistic/interceptor ammunition, friendly coverage, legal target reasons, and action costs. Previews cannot expose hidden occupancy, enemy interception providers, or predicted defense arithmetic.
- Presentation: add a readable battery marker/model, appropriate Contact representation, and missile/defense cues that respect visibility. Audit land-unit selection and terrain handling.
- Documentation/output: update combat, reaction, formation-role, logistics, and scoring rulings; regenerate rules and scenario/formation aids from the same catalogs. Avoid promising support solely in descriptive text.

## Four-scenario draft pack (SCN-BM-005)

### Narrows Shield — Meridian Veil Archipelago

A compact escort-defense operation. Blue must hold the passage and maintain a capable carrier with its escort coverage; Red uses limited ballistic salvos to force Blue away from the objective. Begin with a small force and few weapon profiles so players can learn coverage and ammunition decisions. Use explicit control, escort, denial, and carrier-preservation objectives.

### Silent Battery — Meridian Veil Archipelago

A search-and-suppression operation featuring a mobile land battery, naval search platforms, and defended task groups. The battery must relocate or risk discovery after launch. Make operational control and denial the initial scoring basis; add a target-specific neutralization objective only if implemented and documented. Do not award invented points for damage through briefing text alone.

### Northern Umbrella — Luzon theater

A wider carrier-transit operation. Blue must move through a declared transit corridor while maintaining coverage; Red pressures separate approaches with ballistic launchers and other strike formations. Different entry regions and command architectures create a choice between concentrated protection and dispersed maneuver. Define transit for designated formations rather than any incidental combatant.

### Last Passage — Luzon theater

A withdrawal and logistics operation with limited interceptor stocks, a protected fleet train, and scheduled pressure. Require a designated convoy/logistics formation to reach an exit with a surviving escort; Red seeks to prevent passage. This requires generalized escort and designated-formation transit/withdrawal scoring because existing logistics groups do not count toward those objectives. Keep geography fixed and vary weather, deployment, and timing through authored data.

Each scenario needs its own stable ID, briefing, side objectives, legal deployment, duration, reinforcement/weather schedule, loadouts, and Solo/Hotseat verification. Check battery land sites and exit accessibility against actual map terrain before authoring coordinates. Names and force balance are provisional.

## Acceptance and validation (SCN-BM-006)

1. A third, previously unknown authored scenario ID appears in the selector, loads the referenced theater, and uses its own forces/objectives/events. Invalid references produce an actionable validation failure.
2. Both original scenarios retain their behavior and load old saves without gaining new weapons or defenses. Do not rewrite legacy enum meanings.
3. Ballistic launches enforce contact, domain, range, inventory, and damage boundaries. Empty/false/stale aims spend the committed magazine without revealing why the strike failed.
4. Interception checks self and allied coverage, radius boundaries, zero stock, overlapping providers, provider loss, target-domain restrictions, and stable tie selection. Exactly one inventory expenditure occurs for each committed interception.
5. Synchronized attacks exhaust magazines in deterministic participant order. Save/load across reaction and synchronized-strike states preserves inventory and selected weapon type; replay produces matching digests.
6. Side-scoped UI, AI, receipts, logs, network snapshots, and effects disclose no unauthorized launcher or defending escort information.
7. All four scenarios have executable, differentiated victory conditions and legal deployments. Seeded AI runs complete without deadlocks or illegal actions, followed by human Solo and secure Hotseat tests and balance notes.
8. Run focused Unit, Integration, ContentSimulation, AdvancedContentSimulation, and affected Presentation checks during implementation; run the broader regression/authority coverage before integration. Use tools/run-tests.ps1 with Unity closed. Record actual results and limitations; this planning change does not run or claim runtime tests.

Add meaningful boundary checks to the existing suite matching each failure domain. Validate catalog syntax and references separately from gameplay balance. Finish the feature and content playtests before returning to beta scope/content freeze and release packaging.
