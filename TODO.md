# Sea of Uncertainty - Definitive TODO

Last audited against the repository: **2026-09-08**
Unity baseline: **6000.2.12f1**

This is the single authoritative project backlog. Design notes explain intent, but work is planned and tracked here.

Checkbox rules:

- `[x]` means implemented and supported by repository evidence such as an automated test, successful build, or runtime probe.
- `[ ]` means incomplete. A line may describe the verified portion after **Partial:** without implying completion.
- **Decision** means the rule must be written before its dependent implementation is considered final.
- **Human QA** and **Playtest** items cannot be completed by automated tests alone.
- **Parked** work is intentionally deferred and is not a current blocker.

## Current milestone - complete the core decision loop

Work these sections approximately in order. Do not expand simulation detail until the five missing gameplay systems have been playtested together.

### 1. Player-selected reactions

- [x] Implement Defend with its +1 Defense modifier.
- [x] Implement C-18 Orderly Withdrawal as a prepared one-use Evade: +1 Defense, then a deterministic withdrawal of up to two valid hexes away from the attacker.
- [x] Display prepared Orderly Withdrawal status and the resolved reaction in previews, the dossier, log, and telemetry outcome.
- [x] **Decision:** A formation's Reaction refreshes immediately after it completes its own Action; advancing Time alone does not refresh it.
- [x] **Decision:** Reactions do not change Ready Time.
- [x] **Decision:** Choose reactions after Strike commitment and before the roll; Solo routes the choice to the human or AI defender, and Local Hotseat uses a secure defender handoff.
- [x] Let the defender choose Defend, Evade, Counterattack, or Hold during a reaction window.
- [x] Implement ordinary Evade destination selection and one-hex post-combat movement.
- [x] Implement Counterattack targeting, sequencing, Contact requirements, Light return Strike, and restrictions.
- [x] Implement Hold as a reaction.
- [x] Enforce one Reaction between the formation's own Actions.
- [x] Enforce Disrupted, Disorganized, Crippled, Fire Control Hit, missing-Contact, Destroyed, and replenishing restrictions.
- [x] Teach the AI to select a legal reaction and Evade destination without hidden information.
- [x] Display Reaction available/spent/unavailable state on formations and the timeline.
- [x] Add authoritative reaction tests for timing, refresh, all choices, restrictions, Solo/Hotseat routing, AI, save/load, and information discipline.

### 2. Patrol / Screen

- [x] **Decision:** Define Patrol/Screen as a radius-one area centered in the screener's hex or an adjacent hex.
- [x] **Decision:** Resolve the one extra interception after enemy movement commits inside the area; keep it separate from the ordinary Reaction.
- [x] Define Defensive, Balanced, and Aggressive postures.
- [x] Implement Patrol/Screen as a playable one-Time action.
- [x] Assign a protected formation, objective, or area.
- [ ] Add named route-segment assignment after plotted routes exist.
- [x] Display Patrol/Screen areas and relationships on the map and timeline.
- [x] Trigger legal interception when enemy movement enters a screened area.
- [x] Apply posture benefits, costs, Signature changes, and entropy interactions.
- [x] Add Patrol/Screen AI use, telemetry, save/load, and deterministic tests.

### 3. Support

- [x] **Decision:** Support persists until used, range breaks, the supporter acts again, or either participant becomes unavailable.
- [x] **Decision:** Support is +1; same-kind assignments do not stack and Search plus ASW Search is capped at +1 against submarines.
- [x] Implement Support as a playable action with supporting formation, recipient, action, and bonus selection.
- [x] Implement Strike, Search, Defense, ASW Search, and stored synchronization Support.
- [x] Schedule the supporting formation's next Ready Time.
- [x] Display active Support relationships on the map, dossiers, and timeline.
- [x] Connect F-08, F-10, D-06, D-11, X-08, and C-24 to the finished Support rules.
- [x] Add Support AI use, telemetry, save/load, and deterministic tests.
- [ ] Consume stored synchronization Support when the synchronized-action parent system is implemented.

### 4. Replenishment and logistics

- [x] Store data-driven ports, airfields, logistics regions, and terrain locations in operational areas.
- [x] Apply the existing logistics-location check to relevant Recover timing.
- [x] **Decision:** Naval formations use scenario-region Ports/Anchorages; air groups use scenario-region Airfields; facilities are neutral until ownership exists.
- [x] **Decision:** One service package improves Endurance and Damage one step, reloads Heavy Salvo, resets major-action wear, and repairs one selected Destruction card.
- [x] Implement Replenish as a playable three-Time action.
- [x] Restrict reactions while replenishing and restore them when the formation becomes Ready.
- [x] Restore Endurance and Heavy Salvo expenditure according to the final rule.
- [x] Repair eligible capability loss and Destruction cards.
- [x] Display valid logistics locations, typed access, X-10 timing, and restoration preview.
- [x] Connect X-10 and existing F-06 logistics timing to the finished system.
- [x] Add Replenishment AI use, telemetry, save/load, and deterministic tests.

### 5. Command Attention and Standing Missions

- [x] Track three baseline Command Slots per side.
- [x] Temporarily occupy and release a Command Slot during Focused Search.
- [x] Store a current Mission task on every formation and display it in the dossier.
- [x] Assign initial Mission tasks by formation role.
- [x] Implement C-04 Rapid Replan: select a formation and playable task, occupy no Command Slot, and add +1 Time to its next Ready scheduling.
- [x] Reject Rapid Replan when Broken Link prevents new orders.
- [x] **Decision:** Standardize `spend`, `use`, `assign`, `occupy`, and `strain` terminology.
- [x] **Decision:** Define action-resolution, next-formation-action, and timed-delivery Slot release conditions.
- [x] Model three individual Free, Occupied, or Strained Slot states with purpose and release data.
- [x] **Decision:** Finalize Task, Objective, Posture, and Trigger vocabularies.
- [x] **Decision:** Define Contact Located, Entropy Marked, Logistics Required, and Objective Reached automatic authorizations.
- [x] Implement the full Standing Mission editor.
- [x] Determine whether an action follows the current Mission by its Task.
- [x] Allow mission-following actions without Command Attention.
- [x] Occupy Command Attention through resolution for immediate out-of-mission retasking.
- [x] Implement delayed mission changes, Broken Link behavior, and trigger-authorized changes.
- [x] Implement F-03, F-12, D-02, D-04, C-03, and C-17 against the final Mission/Command rules.
- [x] Implement Command Strain, its two-Strain Slot reduction, Restore Command, and HQ Recovery.
- [x] Implement Push Through and gate Disorganized formations' complex Actions.
- [x] Instrument every Mission change, out-of-mission action, Slot occupation, release, and strain event.

## Rules completion backlog

### Movement, terrain, stacking, and control

- [x] Implement deterministic axial-hex movement with 20 nautical miles per adjacent hex.
- [x] Implement Cautious, Normal, and High Tempo distances for surface, carrier, submarine, and air formations.
- [x] Cap movement by effective Move and apply air-specific mission radii.
- [x] Reject non-air movement ending on Land and add the current Littoral Ready-Time cost.
- [x] Make High Tempo Loud, mark Friction, and apply supported Endurance/card consequences.
- [x] Highlight legal destinations and preview path, distance, and time cost in 3D.
- [x] **Decision:** Cautious Signature -1 and High Tempo Loud persist until the formation completes its next Action.
- [x] **Decision:** Straits stop non-air movement on entry; restricted areas are impassable; one friendly formation may end per hex; hidden enemies may coexist without blocking movement; combat-capable non-air formations control within one hex unless contested.
- [x] Implement deterministic route legality, route previews, Littoral route cost, Strait stopping, restricted areas, friendly stacking, fog-safe enemy co-occupancy, and objective control.
- [ ] Implement weather and sea-state movement effects after playtesting earns them.

### Search and spatial uncertainty

- [x] Implement Passive, Active, and Focused Search modifiers and ranges at 20 nm scale.
- [x] Search a selected hex plus its radius-one footprint, including blind areas without Contacts.
- [x] Create and improve side-owned Contacts without exposing hidden enemy state.
- [x] Track Contact Location, Identity, Age, degradation, and loss.
- [x] Apply the optional Age-2 targeting penalty.
- [x] Display Search calculation, legal area, range, and success threshold before commitment.
- [x] Prevent enemy identities and exact positions from leaking through failed Search or previews covered by automated tests.
- [x] **Decision:** At Age 0, High is one exact hex, Medium is radius one, and Low is radius two; Age or observed movement adds up to two anonymous rings.
- [x] **Decision:** The searching player declares Location or Identity priority before resolution; success improves one step per Contact and excess success grants no extra steps.
- [x] **Decision:** Age advances only with elapsed operational Time; Location degrades on reaching Age 3 and once per further elapsed Time, with Low becoming Lost.
- [x] Implement discrete possible-hex geometry, boundary clipping, and anonymous expansion as targets move or Contacts age.
- [x] Add data-driven formation-sensor and scenario-specific maximum ranges.
- [x] Define and implement private Search-based False Contact disproof with indistinguishable undisproved presentation.
- [x] Complete and test the information-discipline audit for maps, previews, actions, cards, AI, operational logs, and save/load.

### Strike, combat, and damage

- [x] Implement formation/salvo-specific Strike ranges at 20 nm scale.
- [x] Implement Light, Standard, and Heavy salvo modifiers and Heavy expenditure.
- [x] Implement targeting modifiers, Attack-minus-Defense bands, bounded d6 results, and probability previews.
- [x] Require confirmation for Heavy Salvo.
- [x] Implement the current damage ladder through Light, Heavy, Crippled, and Destroyed.
- [x] Reduce Crippled movement, prevent its Heavy Salvo, and exclude Destroyed formations from play.
- [x] Apply supported Destruction-card effects to Move, Search, Strike, Defense, Command, and salvo availability.
- [ ] **Decision:** Define Light damage's exact impairment and duration.
- [ ] **Decision:** Define how repeated damage combines.
- [ ] Implement limited Crippled reactions after the final reaction rules exist.
- [ ] **Decision:** Define Strikes against uncertain areas and when the hidden target's actual position is tested.
- [ ] Support misses caused by stale or incorrect Location information.
- [ ] Prevent uncertain-Strike UI from revealing whether a Contact is real.

### Entropy and recovery

- [x] Track universal Friction, Disruption, and Destruction sources and Cohesion summary.
- [x] Store 36 structured Entropy cards: 12 per source.
- [x] Maintain deterministic per-source draw/discard piles.
- [x] Draw a physical card for every Entropy event, including repeated events from an already-marked source.
- [x] Stack attached cards, queue private per-side reveals, and preserve unseen reveals through save/load.
- [x] Let Recover select and discard one Friction or Disruption card while retaining the source if matching cards remain.
- [x] Apply 22 of 36 effects whose parent systems exist.
- [x] Implement immediate printed Command responses for F-01, F-02, and F-07.
- [ ] **Decision:** Finalize the exhaustive complex-Action and major-Action lists.
- [ ] **Decision:** Finalize whether universal source penalties stack with individual card effects after paired playtests.
- [ ] Implement the remaining 4 effects after their Synchronization or uncertainty-geometry parent systems exist: F-02, F-05, D-03, D-12.
- [ ] Implement every remaining printed entropy response and its exact play window/cost.
- [x] Define and implement Destruction-card repair and discard through Replenishment.

### Command Response deck

- [x] Store all 24 Command Response definitions with stable IDs, effects, costs, and target types.
- [x] Create deterministic private decks and starting three-card hands for both sides.
- [x] Display Response cards as physical cards in the bottom hand and in a readable modal.
- [x] Select Formation, Contact, Reaction, Hex, and Mission targets for supported cards.
- [x] Move successfully played cards from hand to discard and preserve decks/hands through save/load.
- [x] Implement and enable 19 of 24 Command Responses, including Rapid Replan and prepared Orderly Withdrawal.
- [x] Keep unsupported cards visible but disabled with an explicit parent-system message.
- [ ] **Decision:** Finalize deck construction, hand limit, discard reshuffle, draw triggers, and exact play windows.
- [ ] Implement C-02 and C-10 after Synchronized Strikes exist.
- [x] Implement C-03 Mission Command and C-17 Flash Order against Standing Mission triggers and Command Strain.
- [x] Implement C-24 Task Group Reshuffle against active Screen/Support assignments.
- [ ] Add gameplay draw/replacement triggers beyond the starting hand.
- [ ] Teach the AI to evaluate and play supported Response cards without hidden information.

### Synchronized Strikes

- [ ] **Decision:** Define participant reservation, pre-Strike actions, Strike Time, and Ready scheduling.
- [ ] **Decision:** Define the number/timing of defender reactions and abort/stale-target/retask costs.
- [ ] Declare participants and a future Strike Time.
- [ ] Occupy and release the required Command Slot.
- [ ] Reserve and validate participants at resolution.
- [ ] Resolve sequential Defense penalties.
- [ ] Mark Friction for three or more independent participants.
- [ ] Handle lost/degraded Contact information.
- [ ] Display the synchronized event on the operational timeline.
- [ ] Add AI, telemetry, save/load, and deterministic tests.

### Endurance

- [x] Track Ready, Extended, and Critical Endurance.
- [x] Degrade after the current three-major-action prototype threshold and reset the counter.
- [x] Apply current Critical movement and Heavy Salvo restrictions.
- [ ] **Decision:** Finalize the major-Action list, degradation trigger, and reset behavior through paired testing.
- [ ] Display progress toward the next Endurance degradation.
- [ ] Apply the final Ready, Extended, and Critical rules.

### Scenario objectives and victory

- [x] Store scenario horizon, objective, weather, special rules, and victory-condition text as data.
- [x] End scenarios at their configured horizon and show a results screen.
- [x] Apply provisional scoring for objective proximity/control, carrier preservation, and crippled/destroyed opponents.
- [x] Automatically record and export final scores.
- [ ] Define and implement the final T16 Meridian Veil scoring model.
- [ ] Define scenario-specific transit, escort, denial, and withdrawal objectives.
- [ ] Keep damage/destruction subordinate to operational objectives in final scoring.
- [ ] Add scoring tests for ties, elimination, contested control, and every scenario objective type.

## Verified digital foundation

### Architecture, data, and determinism

- [x] Separate authoritative core rules from Unity presentation objects.
- [x] Use seedable deterministic random resolution and stable logical IDs.
- [x] Implement continuous Ready-Time scheduling and deterministic same-Time ordering.
- [x] Pass cross-side tie priority away from the side that acted most recently.
- [x] Continue one side's activations when the opponent has no formation at the earliest Ready Time.
- [x] Create data-driven OperationalArea and Scenario models independent of runtime scene objects.
- [x] Store scale, projection, dimensions, valid hexes, terrain, locations, regions, presentation profiles, deployments, objectives, and scenario rules as data.
- [x] Implement schema migration and validation for operational areas, scenarios, and saved game state.
- [x] Reject incompatible scenario saves with a clear message.
- [x] Provide an editor operational-area preview and validation report.
- [x] Convert Meridian Veil and the Luzon Strait pilot to the data-driven architecture.
- [x] Validate key geographic-to-hex distances in the Luzon sector.

### Application flow and persistence

- [x] Implement main menu, operation-mode selection, scenario selection, briefing, fixed deployment review, and operation start.
- [x] Implement Solo vs AI and Local Hotseat using the same core rules.
- [x] Implement secure pass-device handoffs that clear private visual state.
- [x] Implement pause, restart, return to menu, and confirmed exit controls.
- [x] Save/load operational state, scenario identity, mode, human side, decks, hands, pending reveals, telemetry, and feedback.
- [x] Add Continue for an existing save.
- [x] Implement a field manual, contextual tooltips, action previews, and event inspector.
- [x] Implement results and structured playtest-debrief screens.

### UI, display, accessibility, and audio

- [x] Replace the active immediate-mode interface with Unity UI Toolkit while retaining the old controller as a disabled fallback.
- [x] Use reusable UI components, design tokens, and a packaged runtime theme.
- [x] Support the tested 1280x720, 1600x900, 1920x1080, and 1800x760 layouts without known clipping.
- [x] Use a dynamic map render target and native borderless fullscreen instead of scaling a fixed low-resolution image.
- [x] Add windowed/native-fullscreen switching and display the current/desktop resolution.
- [x] Add mouse controls and keyboard shortcuts for actions, map cursor, camera, grid, overlays, and focus commands.
- [x] Add persistent settings for master audio, high-contrast sides, reduced motion, optional hex grid, and the Age-2 rule.
- [x] Make reduced motion suppress or freeze camera, marker, water, combat-effect, and active-ring animation where implemented.
- [x] Add redundant formation/status shapes so primary map state is not color-only.
- [x] Add a pulsing gold ring around the active formation with a strong static reduced-motion state.
- [x] Generate distinct audio signatures for all eight ActionKind values and play cues for completed human-visible actions.
- [x] Keep hidden Solo AI action audio from leaking its action choice.
- [ ] **Human QA:** Test interactive resizing, native fullscreen, DPI 100/125/150/200%, multi-monitor behavior, mouse-only operation, and the full keyboard sweep.
- [ ] Validate the complete palette with color-blind simulators and human testers.
- [ ] Add scalable text controls.
- [ ] Complete keyboard-only navigation through every modal and map workflow.
- [ ] Re-enable controller navigation with calibrated dead zones and one-step-per-input focus behavior.
- [ ] Add subtitles/text descriptions for every audio event.
- [ ] Add separate effects, music/ambient, and master volume controls.
- [ ] Test screen-reader-compatible menu and rules structures where feasible.

### 3D operational map and presentation

- [x] Preserve the deterministic 2D axial grid as authoritative beneath the 3D presentation.
- [x] Implement hex/world conversion, contextual hex highlighting, and an optional persistent grid.
- [x] Implement bounded pan, zoom, orbit, tilt, reset, active/objective focus, edge scrolling, and save/recall camera views.
- [x] Display cursor hex, terrain, nautical-mile range, north, camera heading, and scale cues.
- [x] Render only side-authorized formations and Contacts independently of zoom level.
- [x] Distinguish certain, uncertain, stale, false, and lost Contacts without exposing hidden identities.
- [x] Render prototype carrier, surface, submarine, and air silhouettes with zoom-dependent operational symbols.
- [x] Display selection/readiness, cohesion, damage, entropy, Loud state, paths, movement, Search areas, Strike ranges, and objective control.
- [x] Implement pooled prototype effects for Search, detection, movement wake, launch, interception, impact, damage, and recovery.
- [x] Implement procedural ocean motion/normals, sun glint, bathymetry, shallow water, coastline foam, land relief, haze, weather hooks, and cloud shadows.
- [x] Import Natural Earth coastline polygons for the Luzon pilot and continue real geography beyond the playable grid.
- [x] Add map-space geographic labels and distinct port/airfield geometry.
- [x] Keep presentation effects independent of authoritative movement, range, information, and outcomes.
- [x] Implement marker/effect pooling and measured prototype performance budgets.
- [ ] **Parked:** Replace prototype formation counters with production meshes, materials, markings, wakes, and licensed provenance after art direction reopens them.
- [ ] Create a formal production art-direction guide and asset approval workflow.
- [ ] Complete the asset provenance/licensing ledger for every distributable asset.
- [ ] Replace provisional generated art before public release where licensing or quality requires it.
- [ ] Add production-quality action/rating icons and Contact/status/card visuals.

### AI and information discipline

- [x] Implement a deterministic baseline AI using only its formations, owned Contacts, public terrain, and objectives.
- [x] Execute AI decisions through the same legal action methods and Ready-Time scheduler as humans.
- [x] Complete full-scenario no-stall AI smoke coverage.
- [x] Test AI recognition of a valid Heavy-only Strike opportunity.
- [x] Prevent AI-only bonuses and suppress hidden AI card/audio disclosures in Solo mode where implemented.
- [ ] Add AI decision-distribution telemetry by action, formation type, phase, and situation.
- [ ] Add tactical AI fixtures for prosecution, objective movement, entropy recovery, weapon commitment, reactions, cards, and disengagement.
- [ ] Add baseline-versus-candidate simulations for measurable AI improvement.
- [ ] Add strategic variety while preserving reproducibility for a fixed seed/profile.

### Telemetry, tests, and builds

- [x] Record action/mode choice, legal alternatives, decision duration, Ready changes, Contacts, combat, entropy, scoring, and outcomes.
- [x] Export session JSON, event CSV, and feedback text.
- [x] Preserve an active telemetry session through save/load.
- [x] Provide automated core smoke coverage for rules, scaling, migration, save/load, information security, camera/map behavior, AI legality, and telemetry serialization.
- [x] Provide regression coverage for independent Entropy reveals and save/load.
- [x] Provide card expansion coverage for 36 Entropy cards, 24 Response cards, physical decks/hands, stacking, supported effects, Rapid Replan, and Orderly Withdrawal.
- [x] Produce successful Windows builds for the current Unity project.
- [x] Runtime-probe the packaged player at 1920x1080 without detected exceptions after the latest rules changes.
- [ ] Split the large editor smoke method into maintainable unit, integration, presentation, and build-validation suites.
- [ ] Add automated UI interaction coverage for card targeting, handoff privacy, settings, fullscreen, and modal navigation.
- [ ] Add performance captures on minimum and target Windows hardware.

## Content and simulation after core-loop alpha

- [ ] Define distinct surface-formation behavior.
- [ ] Define a separate submarine Contact/combat procedure if testing earns it.
- [ ] Decide whether air groups remain formations or become mission packages.
- [ ] Add carrier and air-operation behavior.
- [ ] Add nuanced Search and Signature profiles.
- [ ] Add layered missile defense.
- [ ] Add detailed weapon inventories and reload restrictions.
- [ ] Add electronic-warfare and cyber capabilities.
- [ ] Add logistics formations and repair facilities.
- [ ] Add weather and environmental rules beyond presentation hooks.
- [ ] Add different command architectures.
- [ ] Move scenario and formation definitions into editable Unity data assets or an external authoring format.
- [ ] Add a scenario editor.
- [ ] Add scheduled future events and reinforcements to the operational timeline.
- [ ] Define how connected theaters handle off-map movement and transitions in gameplay.

## Human playtest program

- [ ] **Playtest:** Complete at least one full Meridian Veil Solo session without developer intervention.
- [ ] **Playtest:** Complete at least one full Meridian Veil Local Hotseat session with secure handoffs.
- [ ] **Playtest:** Validate Luzon maneuver space, chokepoints, Search ranges, and scenario duration.
- [ ] **Playtest:** Test selection of small formations, dense Contacts, coastal hexes, and overlapping markers.
- [ ] **Playtest:** Compare automatic Defend with player-selected reactions.
- [ ] **Playtest:** Compare Age-2 targeting penalty on versus off.
- [ ] **Playtest:** Compare High Tempo always marking Friction versus conditional marking.
- [ ] **Playtest:** Compare universal entropy penalties plus cards versus cards replacing universal penalties.
- [ ] **Playtest:** Compare Endurance degradation after three versus four major Actions.
- [ ] **Playtest:** Compare two versus three Command Slots.
- [ ] **Playtest:** Compare player-chosen Contact improvement versus fixed Location-first improvement.
- [ ] **Playtest:** Compare exact-hex Contact labels with spatial uncertainty regions.
- [ ] **Playtest:** Test sequential Defense penalties for second and third synchronized attacks.
- [ ] Record obvious choices, repeated lookups, inactive-player lulls, unfair rolls, unused actions, and dominant actions after every session.

## Print-and-play and multiplayer

Do not prioritize this section until the core loop and information model are stable.

### Shared authoritative architecture

- [x] Keep authoritative rules and stable IDs independent of Unity scene objects.
- [x] Keep random outcomes seedable and reproducible.
- [x] Maintain side-specific permissible information views in the current local game.
- [x] Version and migrate current save/scenario state.
- [ ] Define an authoritative command/event format.
- [ ] Add deterministic replay from recorded commands and the random seed.

### Print-and-play

- [ ] Generate formation cards from the same data used by Unity.
- [ ] Generate formation counters and Contact markers.
- [ ] Generate Ready-Time, Command, Endurance, weapon, and entropy tracks.
- [ ] Generate the 36-card Entropy and 24-card Response decks from structured data.
- [ ] Generate scenario setup sheets and player aids from structured rules data.
- [ ] Conduct blind rules teach-and-play tests.
- [ ] Produce print-ready files with bleed, safe areas, and licensing attribution.

### Multiplayer

- [ ] Complete and human-test Local Hotseat privacy before networking.
- [ ] Select multiplayer authority and transport architecture.
- [ ] Synchronize commands and authoritative state transitions rather than UI objects.
- [ ] Add lobby, invitations, match setup, reconnection, and interrupted-match recovery.
- [ ] Validate hidden information on the authority.
- [ ] Add desynchronization detection, recovery, replay, and dispute diagnostics.
- [ ] Conduct latency, reconnect, and adversarial information-leak testing.

## Release gates

### Core-loop alpha

- [x] All eight gameplay verbs are playable: Move, Search, Strike, Patrol/Screen, Support, Recover, Replenish, and Hold.
- [ ] The defender receives a meaningful, legal reaction choice during combat in blind human testing. **Implemented:** Defend, Evade, Counterattack, Hold, secure Hotseat handoff, Solo human routing, and AI choice are active.
- [ ] Players can explain why every formation acts next in blind testing.
- [ ] Players understand calculations and consequences before committing in blind testing.
- [ ] Contact uncertainty has a clear spatial meaning.
- [x] A complete scenario produces a scored operational outcome.
- [x] Playtest telemetry exports successfully.

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
