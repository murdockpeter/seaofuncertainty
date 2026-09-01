# Sea of Uncertainty — Development TODO

This is the primary project backlog. Check items only after the feature is implemented and validated in an appropriate Unity build or playtest.

Status convention:

- `[x]` Complete and validated.
- `[ ]` Not complete.
- Items labeled **Decision required** must have a written ruling before dependent implementation begins.

## CURRENT PRIORITY — 3D operational map and modular 20 nm theaters

Standing evolution criteria for every meaningful implementation pass:

- [ ] Improve an appropriate aspect of land, sea, lighting, atmosphere, models, effects, or other 3D presentation while preserving operational readability.
- [ ] Formation-counter evolution is currently parked: preserve the approved counters unchanged until art direction explicitly reopens them.
- [ ] When the pass touches gameplay or provides a relevant opportunity, improve or measurably evaluate Solo vs AI decision quality, information discipline, strategic variety, explainability, or diagnostics.
- [ ] Keep AI bound to the same authoritative rules and continuous Ready-Time scheduler, without undisclosed bonuses or hidden-information access.

Complete this program before beginning any remaining P1–P5 feature work, except critical prototype bug fixes. Preserve the deterministic hex rules model and print-and-play compatibility: 3D is the presentation and interaction layer, while authoritative movement, range, Contacts, and control remain hex based.

Standing visual criterion: every implementation pass should include an appropriate cosmetic improvement toward a believable maritime theater without reducing information clarity or changing authoritative rules.

### 3D design constitution

- [x] **Decision required:** Approve “3D presentation over deterministic 2D hex topology” as the authoritative model.
- [x] **Decision required:** Approve an angled command-table camera rather than unrestricted free-flight navigation.
- [x] **Decision required:** Define the permitted camera pitch, rotation, pan, and zoom ranges.
- [x] **Decision required:** Decide whether rotation is free, stepped, or disabled during play.
- [x] **Decision required:** Define when hexes are always visible, contextually visible, or hidden.
- [x] **Decision required:** Define zoom-level transitions between 3D models, operational symbols, and aggregated markers.
- [x] **Decision required:** Decide how aircraft altitude and submarine depth are represented as discrete operational states.
- [x] Write a short 3D visual and interaction specification before production implementation.
- [x] Establish the rule that visual animation cannot alter authoritative outcomes or formation locations.

### Physical scale and time

- [x] **Decision required:** Define one adjacent hex-center step as exactly 20 nautical miles.
- [x] **Decision required:** Define the real-world duration represented by one Ready-Time point.
- [x] **Decision required:** Recalculate Cautious, Normal, and High-Tempo surface movement at the approved time scale.
- [x] **Decision required:** Define movement rates for surface groups, carriers, submarines, and air groups.
- [x] **Decision required:** Define whether movement cost varies for littoral water, straits, weather, or other terrain.
- [x] **Decision required:** Recalculate Search ranges and modifiers at 20 nm per hex.
- [x] **Decision required:** Recalculate weapon and mission Strike ranges at 20 nm per hex.
- [x] **Decision required:** Define air mission radius, transit time, loiter behavior, and recovery timing.
- [x] **Decision required:** Define Reaction and interception ranges at the new scale.
- [x] **Decision required:** Reassess the scenario horizon and victory timing after rescaling.
- [x] Record every approved scale ruling in the rules and player aid.
- [x] Add automated conversion helpers for hex range to nautical miles and Ready Time to elapsed time.
- [x] Add unit tests for distance, range boundaries, travel time, and scale labels.

### Modular theater architecture

- [x] Create a data-driven `OperationalArea` definition independent of scenarios and rules logic.
- [x] Give every operational area a stable ID, display name, version, and attribution metadata.
- [x] Store map projection, geographic origin, orientation, 20 nm scale, dimensions, and valid-hex mask.
- [x] Store land, water depth category, straits, restricted areas, ports, airfields, and named locations by hex.
- [x] Store camera bounds, starting camera pose, lighting profile, water profile, and presentation assets.
- [x] Store deployment, reinforcement, exit, logistics, weather, and objective regions as data.
- [x] Create a data-driven `Scenario` definition that references an operational area.
- [x] Move scenario forces, setup, objectives, horizon, weather, special rules, and victory conditions into scenario data.
- [x] Support scenarios that activate only a rectangular or irregular subset of a larger theater.
- [x] Preserve stable axial hex coordinates across saves, playtest telemetry, print maps, and multiplayer messages.
- [x] Add schema/version migration for operational-area, scenario, and save data.
- [x] Add validation that reports missing assets, invalid hexes, overlapping IDs, and unreachable setup zones.
- [x] Add an editor preview for operational-area hexes, terrain tags, locations, and scenario bounds.

### Migrate the existing prototype first

- [x] Convert the current fictional 12×10 archipelago into the first `OperationalArea` definition.
- [x] Convert Meridian Veil into the first data-driven `Scenario` definition.
- [x] Remove hardcoded map dimensions, setup positions, objective location, and scenario horizon from runtime game/map logic.
- [x] Load the existing 2D prototype entirely through the new area and scenario interfaces.
- [x] Confirm existing saves either migrate safely or fail with a clear incompatibility message.
- [x] Re-run all current rules, save/load, telemetry, and interface smoke tests after migration.
- [x] Treat successful behavior parity on Meridian Veil as the architecture gate for 3D work.

### 3D command-map foundation

- [x] Create a dedicated runtime 3D operational-map scene and additive UI scene/loading structure.
- [x] Establish world units and floating-origin strategy appropriate for a large theater.
- [x] Generate or import a hex mesh aligned exactly with authoritative axial coordinates.
- [x] Implement reliable hex-to-world and world-to-hex conversion.
- [x] Implement contextual hex highlighting for hover, selection, legal movement, Search, Strike, and objectives.
- [x] Add a persistent optional full-map hex grid with a command-bar toggle, keyboard shortcut, and subdued terrain-aware styling.
- [x] Tune persistent-grid contrast and line weight for legibility at operational zoom levels without overpowering the terrain.
- [x] Implement data-driven terrain/coastline mesh ingestion with land and water masks.
- [x] Add stylized ocean rendering that remains readable beneath overlays and markers.
- [x] Add a warm shadow-casting maritime sun with cool sky fill.
- [x] Place the maritime sun at an east-northeast source azimuth with opposing cool fill.
- [x] Add textured, gently animated ocean geometry with visible wave relief.
- [x] Add procedural land coloration and dimensional highland relief to polygon coastlines.
- [x] Render authentic Natural Earth polygons beyond the playable projection through the table edges without stretching interior geography.
- [x] Replace prototype cylinder landforms with deterministic irregular island and shoal meshes.
- [x] Separate shoal and ocean depth surfaces to eliminate coplanar rendering artifacts.
- [x] Add island terrain, shoreline treatment, bathymetry cues, and atmospheric distance treatment.
- [x] Implement the approved command-table camera with bounded pan, zoom, pitch, and rotation.
- [x] Implement mouse selection, middle-drag pan, continuous right-drag orbit/tilt, wheel zoom, and camera reset.
- [x] Expand to full command-camera control with WASD movement, continuous orbit/tilt, keyboard zoom, speed boost, and theater bounds.
- [x] Route command-camera keys game-wide during operation play so WASD remains responsive when non-map UI previously retained focus.
- [x] Add smooth camera acceleration/deceleration, optional edge scrolling, active/objective focus commands, and save/recall command views.
- [x] Resize and left-align formation timeline typography so long names and readiness details remain legible.
- [x] Prevent clicks through UI and resolve ambiguous overlapping map selections consistently.
- [x] Add 3D hovered-hex highlighting and legal-movement preview vectors.
- [x] Add live hex, terrain, nautical-mile range, north, and camera-heading readouts.
- [x] Add a north indicator, scale bar, cursor hex coordinate, nautical-mile range readout, and camera orientation cue.
- [x] Preserve the current UI Toolkit screens and panels while replacing only the tactical-map presentation.

### Formations, Contacts, and information in 3D

- [x] Create a formation-view layer that reads state without owning gameplay logic.
- [x] Create prototype 3D representations for surface groups, carrier groups, air groups, and submarines.
- [x] Replace stretched-cube formation blockouts with tapered multivessel carrier/surface silhouettes, detailed submarine geometry, and a recognizable swept-wing aircraft profile.
- [ ] **Parked by current art direction:** Add production-quality generated or licensed formation meshes with consistent physical proportions and asset provenance; retain the present platform counters until explicitly revisited.
- [ ] **Parked by current art direction:** Add further formation-counter material, marking, wake, propulsion, or idle-motion changes; retain the present platform counters until explicitly revisited.
- [x] Use visual offsets within a hex without changing the authoritative hex position.
- [x] Define selection rings, side indicators, readiness state, cohesion, and damage presentation.
- [x] Add prototype active-formation readiness beacons and damage/entropy shape cues.
- [x] Represent unknown Contacts without revealing formation type, exact identity, or hidden state.
- [x] Visually distinguish certain, uncertain, stale, false, and lost Contact information.
- [x] Implement zoom-dependent transition from 3D formation models to readable operational symbols.
- [x] Implement movement paths, destination previews, time costs, and arrival animations.
- [x] Implement Search-area visualization without exposing hidden enemy positions.
- [x] Implement Strike-range and target previews without exposing unavailable information.
- [x] Ensure pass-and-play handoff clears selection, camera clues, overlays, and hidden visual state.

### 3D effects and presentation prototype

- [x] Create restrained prototype effects for Search sweeps, detection, movement wakes, launches, interception, impacts, and damage.
- [x] Represent Friction, Disruption, Destruction, Loud status, and recovery without relying on color alone.
- [x] Add day/night and weather hooks without allowing presentation to obscure required information.
- [x] Respect reduced-motion settings for camera movement, marker animation, water, and combat effects.
- [x] Establish placeholder asset budgets and naming conventions for models, materials, textures, VFX, and audio.
- [x] Document which generated assets are temporary and retain prompt/source/license metadata.

### 3D environmental polish

- [x] Add multi-scale procedural ocean normals, stronger sun glint, sea-state tuning, and layered surface motion.
- [x] Add shallow-water coloration, terrain-aware bathymetric contours, and coastline foam.
- [x] Seat geographic land close to the animated waterline and replace hex-centered shallow-water blobs with noise-varied, coastline-distance continental shelves.
- [x] Add denser coastline-aligned terrain shoulders and normal-mapped land relief.
- [x] Add horizon haze, data-driven clear/haze/rain/night hooks, moving cloud shadows, and optional precipitation streaks.
- [x] Replace the generic effect sphere with pooled, category-specific Search, detection, wake, launch, interception, impact, and damage effects.
- [x] Add map-space geographic labels plus distinct port piers and airfield runway geometry.
- [x] Keep all environmental presentation independent from authoritative hex terrain, range, visibility, and outcomes.

### South China Sea pilot operational area

- [x] **Decision required:** Choose the Luzon Strait and northern approaches as the first focused South China Sea test sector.
- [x] **Decision required:** Choose geographic fidelity, projection, coastline source, bathymetry source, and attribution requirements.
- [x] **Decision required:** Approve neutral treatment of disputed boundaries, claims, and place names.
- [x] **Decision required:** Keep early factions fictional on real geography.
- [x] Build the selected pilot sector at 20 nm between adjacent hex centers.
- [x] Import and validate generalized coastline, islands, water, ports, airfields, and named locations for the pilot sector.
- [x] Verify real geographic distances against hex distances at multiple points across the sector.
- [x] Create at least one short scenario that tests transit, Search, chokepoints, and objective control.
- [ ] **Human playtest gate:** Playtest whether the selected sector is large enough for maneuver but small enough for meaningful decisions.
- [x] Define how future sectors connect through shared coordinates, off-map movement, or theater transitions.
- [x] Design a theater/operational-area selector that can accept additional regions without code changes.
- [x] Replace provisional per-hex Luzon land blobs with continuous coastline polygons cropped from the local Natural Earth 1:10m campaign library.
- [x] Keep polygon coastline presentation independent from authoritative 20 nm terrain and movement data.

### Performance, testing, and acceptance gates

- [x] Set minimum and target Windows hardware profiles and frame-rate budgets.
- [x] Set polygon, material, texture-memory, draw-call, VFX, and UI budgets.
- [x] Implement object pooling for markers and transient effects; keep paths and highlights persistent and allocation-free.
- [x] Add measured-scope level of detail and document thresholds for chunking, culling, and asynchronous loading.
- [x] Verify deterministic gameplay outcomes remain owned by the presentation-independent core rules tests.
- [x] Add automated tests for hex/world conversion, edge selection, camera bounds, and scenario-area validation.
- [ ] **Human QA gate:** Finish interactive window resizing, fullscreen, DPI 125/150%, mouse-only operation, and keyboard-shortcut sweep. (Automated/runtime probes pass at 1280×720, 1600×900, 1920×1080, and 1800×760 ultrawide.)
- [x] Add automated information-security coverage for side filtering, hidden identities, Contact states, and zoom-independent view data.
- [ ] **Human playtest gate:** Conduct usability tests for selecting small units, dense Contacts, coastal hexes, and overlapping markers.
- [x] Confirm a complete scenario can be played without enabling permanent hex-grid display.
- [x] Confirm telemetry and saves use stable logical IDs and coordinates rather than scene-object references.
- [x] Produce a Windows development build of the 3D Meridian Veil migration.
- [x] Produce and runtime-probe a Windows development build of the South China Sea pilot scenario.
- [x] Complete a formal go/no-go review before expanding to the full South China Sea theater.

## Current foundation

- [x] Read and analyze the v0.5 rules, player aid, design constitution, design notes, and effect-card sheets.
- [x] Document the core decision model and principal rules gaps in `design/RULES_DEEP_DIVE.md`.
- [x] Create a Unity 6 project using Unity 6000.2.12f1.
- [x] Separate the presentation layer from the core rules model.
- [x] Create an initial 12×10 operational map.
- [x] Create an eight-formation test scenario.
- [x] Implement the continuous Ready-Time formation timeline.
- [x] Implement deterministic same-time ordering for prototype reproducibility.
- [x] Implement side-specific formation and Contact visibility.
- [x] Implement Contact Location, Identity, Age, degradation, and loss.
- [x] Implement Cautious, Normal, and High-Tempo movement.
- [x] Implement Passive, Active, and Focused Search.
- [x] Implement Light, Standard, and Heavy Strike commitments.
- [x] Implement combat bands and bounded 1d6 outcomes.
- [x] Implement preliminary damage, entropy, cohesion, endurance, and Command state.
- [x] Implement preliminary Recover and Hold actions.
- [x] Add a formation dossier and after-action feed.
- [x] Generate and integrate the first custom tactical-map background.
- [x] Create and pass core rules smoke tests.
- [x] Produce a Windows development build.

## P0 — Make the prototype properly testable

### Game flow

- [x] Add a main menu.
- [x] Add a scenario-selection screen.
- [x] Add a scenario briefing with objectives, forces, horizon, and special rules.
- [x] Add a clear deployment or initial setup phase.
- [x] Add formal scenario victory conditions.
- [x] Add an end-of-scenario results screen.
- [x] Add restart and return-to-menu controls.
- [x] Add a confirmed exit-to-desktop option to the title screen and in-game menu.
- [x] Add save and load.
- [x] Add settings for display, audio, accessibility, and gameplay options.
- [x] Add an in-game rules reference and glossary.
- [x] Add a privacy/pass-device screen when control changes sides.
- [x] Add initial gameplay and audio settings controls.
- [x] Add save-state JSON round-trip smoke coverage.
- [x] Add selectable Solo vs AI and Local Hotseat command modes.
- [x] Preserve command mode and human side in saved operations.

### Action clarity

- [x] Highlight valid movement destinations.
- [x] Highlight eligible Search targets or areas.
- [x] Allow Search to target a highlighted map hex when no Contact exists; resolve no detection, consume Time, and advance the Ready queue.
- [x] Highlight eligible Strike targets.
- [x] Preview base Action Time and final next Ready Time.
- [x] Preview Signature, Friction, Endurance, weapons, and Command consequences.
- [x] Show the complete Search calculation before commitment.
- [x] Show Search success probability before commitment.
- [x] Show the complete Attack and Defense calculation before commitment.
- [x] Show each possible combat outcome and its probability before commitment.
- [x] Add confirmation for Heavy Salvo and other irreversible commitments.
- [x] Add tooltips for every rating, status, mode, Contact quality, and entropy source.
- [x] Add a detailed event inspector explaining every state change.
- [x] Clearly explain why the current formation acts next.

### Playtest instrumentation

- [x] Record every chosen action and mode.
- [x] Record legal alternatives available when an action is chosen.
- [x] Record player decision duration.
- [x] Record Ready-Time changes and consecutive activations by side.
- [x] Record Contact creation, improvement, aging, degradation, and loss.
- [x] Record Search calculation and outcomes.
- [x] Record combat bands, rolls, damage, and reactions.
- [x] Record Command Slot usage and occupancy duration.
- [x] Record entropy creation, effects, duration, and recovery.
- [x] Record objective control and scenario scoring over Time.
- [x] Add a playtest-session export format.
- [x] Add a structured feedback form based on `design/PLAYTEST_PROTOCOL.md`.

### Interface foundation

- [x] Replace the prototype immediate-mode interface with Unity UI Toolkit.
- [x] Establish reusable visual components and design tokens.
- [x] Support 16:9 at 1920×1080 without clipping.
- [x] Support common smaller desktop resolutions.
- [x] Support ultrawide and window resizing.
- [x] Add mouse and keyboard navigation.

## P1 — Complete the core decision loop

### Reactions

- [ ] **Decision required:** Define exactly when a formation regains its Reaction.
- [ ] **Decision required:** Decide whether Reactions change Ready Time.
- [ ] Define the complete Reaction sequence.
- [ ] Let the defending player choose Defend, Evade, Counterattack, or Hold.
- [ ] Implement Defend.
- [ ] Implement Evade movement and legal destinations.
- [ ] Implement Counterattack targeting, sequencing, and restrictions.
- [ ] Implement Hold as a Reaction.
- [ ] Enforce one Reaction between the formation’s own Actions.
- [ ] Enforce Disrupted and Disorganized Reaction restrictions.
- [ ] Display when a formation’s Reaction is available or spent.

### Patrol and Screen

- [ ] **Decision required:** Define Patrol/Screen area size and geometry.
- [ ] **Decision required:** Define the interception procedure and `+1 interception Reaction`.
- [ ] Implement Defensive, Balanced, and Aggressive postures.
- [ ] Display Patrol and Screen areas on the map.
- [ ] Assign a protected friendly formation, objective, or area.
- [ ] Trigger interception when eligible enemy movement enters an area.
- [ ] Apply Defensive Reaction bonuses and Aggressive Signature costs.

### Support

- [ ] **Decision required:** Define Support duration and whether it persists until used.
- [ ] **Decision required:** Define the meaning of `+1 synchronization`.
- [ ] Select the supporting formation, recipient, action, and bonus type.
- [ ] Implement Strike, Search, Defense, ASW Search, and synchronization Support.
- [ ] Schedule the supporting formation’s next Ready Time.
- [ ] Show active Support relationships on the map and timeline.

### Damage

- [ ] **Decision required:** Define the exact Light-damage impairment and duration.
- [ ] **Decision required:** Define how repeated damage combines.
- [ ] Implement the approved damage ladder.
- [ ] Implement reduced movement for Crippled formations.
- [ ] Prevent Crippled formations from using Heavy Salvo.
- [ ] Implement limited Crippled reactions.
- [ ] Remove or mark Destroyed formations combat ineffective.
- [ ] Explain damage changes in the event inspector.

### Entropy effects

- [x] **Baseline decision:** Entropy cards stack with universal source penalties during MVP playtests.
- [x] **Baseline decision:** Every Entropy event draws a physical card, including repeated events from an already-marked source.
- [x] **Baseline decision:** Matching cards stack in the owning side's visible hand; Recover discards one selected Friction/Disruption card, while Destruction cards await repair/reorganization.
- [ ] Define the exhaustive list of complex Actions.
- [x] Convert all 12 Friction effects into structured game data.
- [x] Convert all 12 Disruption effects into structured game data.
- [x] Convert all 12 Destruction effects into structured game data.
- [ ] Draw and apply an effect for every Entropy event. **In progress:** deterministic physical-deck draw/stacking plus supported Move, Search, Strike, Defense, Command, Endurance, Heavy-Salvo, and selected-card Recover hooks are live; mission, synchronization, Support, and uncertain-location hooks remain.
- [x] Display active effect cards on the formation dossier and reveal newly drawn friendly cards.
- [x] Queue card reveals independently by side, interrupt before solo-AI continuation, and preserve unseen reveals through save/load.
- [ ] Implement printed responses to entropy effects.
- [ ] Remove or retain attached cards according to the approved recovery ruling.

### Command Attention

- [ ] **Decision required:** Define `spend`, `use`, `assign`, and `occupy` or replace them with one consistent term.
- [ ] **Decision required:** Define when Command Slots become free.
- [ ] Model each Command Slot’s state and release condition.
- [ ] Implement immediate retasking.
- [ ] Implement Focused Search Slot occupation using the final lifecycle.
- [ ] Implement Push Through.
- [ ] Implement Command Strain.
- [ ] Reduce available Slots at two Command Strain.
- [ ] Implement Restore Command and HQ Recovery.
- [ ] Implement forcing a Disorganized formation into a complex Action.

### Replenishment and logistics

- [ ] **Decision required:** Define scenario logistics access.
- [ ] **Decision required:** Define exactly what one Replenish action restores.
- [ ] Implement Replenish as a three-Time action.
- [ ] Restrict reactions while replenishing.
- [ ] Restore Endurance.
- [ ] Restore Weapon Expenditure.
- [ ] Repair eligible capability and Destruction effects.
- [ ] Display valid logistics locations and access.

### Scenario objective

- [ ] Define and implement the first T16 test scenario.
- [ ] Score Inner Sea control over Time.
- [ ] Score formation preservation.
- [ ] Score transit, escort, denial, or withdrawal objectives.
- [ ] Use damage/destruction only as a supporting score or tie-breaker.

## P2 — Advanced operational systems

### Operational clock rulings

- [x] **Decision required:** On a same-Time cross-side tie, prefer the side that did not act most recently.
- [x] **Decision required:** When only one side has formations at the earliest Ready Time, that side continues acting without artificial alternation.
- [x] Retain lower Entropy, higher Command, then stable formation ID as deterministic within-side ordering.
- [x] Display same-Time tie resolution and the computed activation queue.
- [ ] Support scheduled future events on the operational timeline.

### Standing Missions

- [ ] **Decision required:** Define the allowed Task vocabulary.
- [ ] **Decision required:** Define Objective, Posture, and Trigger vocabulary.
- [ ] **Decision required:** Define what a Trigger authorizes automatically.
- [ ] Implement the full Task / Objective / Posture / Trigger Standing Mission editor. **In progress:** playable Task assignment and Rapid Replan are active.
- [ ] Determine whether each action follows the current Mission.
- [ ] Allow mission-following actions without Command Attention.
- [ ] Charge Command Attention for immediate retasking.
- [ ] Implement delayed mission changes and Broken Link restrictions.
- [ ] Instrument every out-of-mission action.

### Spatial information model

- [ ] **Decision required:** Define High, Medium, and Low Location geometrically.
- [x] **Decision required:** Search targets a selected hex with a radius-1 footprint; selecting a Contact searches its last-known area.
- [ ] **Decision required:** Decide who chooses a successful Search improvement.
- [ ] **Decision required:** Decide whether excess success can improve multiple steps.
- [ ] **Decision required:** Define Contact aging and degradation timing precisely.
- [ ] Implement High Location as exact positional information.
- [ ] Implement Medium Location uncertainty geometry.
- [ ] Implement Low Location uncertainty geometry.
- [ ] Expand uncertainty as a target moves and information ages.
- [x] Support blind area searches.
- [x] Support detecting a previously unknown formation.
- [ ] Add scenario- and sensor-specific maximum Search ranges.
- [x] Prevent Search information leaks through preview text and no-detection logs; audit the remaining action paths separately.

### Strikes against uncertain information

- [ ] **Decision required:** Determine how a Strike selects an uncertainty area.
- [ ] **Decision required:** Determine when the hidden target’s real position is tested.
- [ ] Resolve whether a target is inside the attacked area before combat.
- [ ] Support misses caused by stale or incorrect Location information.
- [ ] Prevent the Strike interface from revealing whether a Contact is real.

### Synchronized strikes

- [ ] **Decision required:** Define whether participants are reserved before Strike Time.
- [ ] **Decision required:** Define whether participants may act before Strike Time.
- [ ] **Decision required:** Define how individual next Ready Times are scheduled.
- [ ] **Decision required:** Define the number and timing of defender Reactions.
- [ ] **Decision required:** Define abort, stale-targeting, and retask costs.
- [ ] Declare participants and Strike Time.
- [ ] Occupy the required Command Slot.
- [ ] Reserve eligible participants.
- [ ] Verify participant readiness at Strike Time.
- [ ] Resolve sequential Defense penalties.
- [ ] Mark Friction for three or more independent participants.
- [ ] Handle lost or degraded Contact information.
- [ ] Show synchronization as a future timeline event.

### Deception

- [ ] Define formations and scenarios capable of deception.
- [ ] Create a False Contact through Support.
- [ ] Reinforce an existing False Contact.
- [ ] Make False Contacts mechanically indistinguishable until disproven.
- [ ] Define and implement disproof conditions.
- [ ] Record physical reallocations caused by deception.

### Command Response cards

- [ ] **Decision required:** Define deck construction, hand limit, discard, reshuffle, and draw triggers.
- [ ] **Decision required:** Define the exact play window for every response.
- [x] Convert all 24 Command Response cards into structured game data.
- [x] Implement deterministic private starting three-card hands and save/load persistence.
- [ ] Implement every card play window, cost, discard, and acquisition trigger. **In progress:** supported Formation, Contact, Reaction, deception, Rapid Replan, and prepared Orderly Withdrawal effects play from hand and discard; Synchronization, Support, Replenish, and fully interactive reaction-window cards remain gated on their parent systems.
- [x] Add a readable response-hand interface with target selection and explicit unavailable-system states.

### Endurance

- [ ] **Decision required:** Define the exhaustive major-Action list.
- [ ] **Decision required:** Decide whether Endurance uses actions, elapsed Time, or scenario triggers.
- [ ] **Decision required:** Decide whether the action counter resets after degradation.
- [ ] Apply the final Ready, Extended, and Critical rules.
- [ ] Display progress toward the next Endurance degradation.
- [ ] Test three versus four major Actions in paired sessions.

## P3 — Presentation and accessibility

### Art direction

- [ ] Create a formal art-direction guide.
- [ ] Create an asset provenance and licensing ledger.
- [ ] Establish generated-art replacement and approval states.
- [ ] Create distinct Blue and Red formation silhouettes.
- [ ] Create formation-specific generated artwork.
- [ ] Create bespoke icons for every action and rating.
- [ ] Create bespoke Contact, status, and effect-card visuals.
- [ ] Create map variants for additional scenarios.
- [ ] Replace provisional generated imagery with appropriately licensed or commissioned production art.

### Map presentation

- [ ] Animate sonar and search sweeps.
- [ ] Animate Contact creation, improvement, aging, and loss.
- [ ] Display movement routes and costs.
- [ ] Display Search and Strike ranges.
- [ ] Display Screen, Patrol, Support, and synchronization relationships.
- [x] Visually distinguish certain, uncertain, stale, false, and lost information.
- [x] Add objective-control visualization.

### Effects and sound

- [ ] Create launch, interception, impact, damage, recovery, and destruction effects.
- [ ] Create distinct effects for Friction, Disruption, and Destruction.
- [ ] Establish an audio style guide.
- [ ] Add readiness, detection, warning, launch, impact, damage, and command audio cues.
- [ ] Add ambient operational-map sound.
- [ ] Add audio volume controls and mute options.

### Accessibility

- [ ] Use redundant symbols so game state never depends on color alone.
- [ ] Validate a color-blind-safe palette.
- [ ] Support scalable text and UI.
- [ ] Support reduced motion.
- [ ] Support high-contrast mode.
- [ ] Support complete keyboard navigation.
- [ ] Re-enable controller navigation with calibrated dead zones and one-step-per-input focus handling.
- [ ] Add subtitles and textual descriptions for all audio events.
- [ ] Test screen-reader-compatible menu and rules-reference structures where feasible.

## P4 — Additional simulation and content

Begin these only after repeated playtests validate the core loop.

- [ ] Define distinct surface-formation behavior.
- [ ] Define a separate submarine Contact and combat procedure if testing earns it.
- [ ] Decide whether air groups are persistent formations or mission packages.
- [ ] Add carrier and air-operation behavior.
- [ ] Add nuanced Search and Signature profiles.
- [ ] Add layered missile defense.
- [ ] Add detailed weapon inventories and reload restrictions.
- [ ] Add electronic-warfare and cyber capabilities.
- [ ] Add logistics formations and repair facilities.
- [ ] Add terrain, coast, strait, stacking, and occupancy rules.
- [ ] Add weather and environmental effects.
- [ ] Add different command architectures.
- [ ] Move scenario and formation definitions into editable data assets.
- [ ] Add a scenario editor.
- [x] Add a baseline deterministic AI that acts only on formations, Contacts, terrain, and objectives legitimately available to its side.
- [x] Resolve consecutive AI formations through the continuous Ready-Time queue without adding AI-only bonuses or traditional turns.
- [x] Add full-scenario AI legality and no-stall smoke coverage.
- [ ] Add AI decision-distribution telemetry by action, formation type, scenario phase, and operational situation.
- [ ] Add tactical AI fixtures for Contact prosecution, objective movement, Entropy recovery, weapon commitment, and disengagement.
- [ ] Add baseline-versus-candidate simulation comparisons so AI changes can demonstrate improvement rather than merely different behavior.
- [ ] Add strategic variety without sacrificing deterministic reproducibility for a fixed scenario seed and AI profile.

## P5 — Print-and-play and multiplayer

### Shared architecture

- [ ] Keep all authoritative rules independent of Unity presentation objects.
- [ ] Make random results seedable and reproducible.
- [ ] Define the authoritative command/event format.
- [ ] Define each side’s permissible information view.
- [ ] Add deterministic replay from recorded commands and random seed.
- [ ] Add state-version migration for saved games.

### Print-and-play

- [ ] Generate formation cards from the same source data used by Unity.
- [ ] Generate counters and Contact markers.
- [ ] Generate Ready-Time, Command, Endurance, weapon, and entropy tracks.
- [ ] Generate effect and response card decks.
- [ ] Generate scenario setup sheets.
- [ ] Update the player aid from final rules data.
- [ ] Conduct blind rules teach-and-play tests.
- [ ] Produce print-ready files with bleed, safe areas, and licensing attribution.

### Multiplayer

- [ ] Complete local hot-seat privacy handling.
- [ ] Select the multiplayer authority and transport architecture.
- [ ] Synchronize commands and authoritative state transitions, not UI objects.
- [ ] Add lobby, invitations, and match setup.
- [ ] Add reconnection and interrupted-match recovery.
- [ ] Add hidden-information validation on the authority.
- [ ] Add desynchronization detection and recovery.
- [ ] Add multiplayer replays and dispute diagnostics.
- [ ] Conduct latency, reconnect, and adversarial information-leak testing.

## Formal playtest comparisons

- [ ] Test Age-2 targeting penalty on versus off.
- [ ] Test High Tempo always marking Friction versus only when Extended/Critical.
- [ ] Test universal entropy penalty plus card versus card replacing the universal penalty.
- [ ] Test Endurance degradation after three versus four major Actions.
- [ ] Test two versus three Command Slots.
- [ ] Test automatic Defend versus player-selected Reaction.
- [ ] Test Contact improvement chosen by player versus fixed Location-first progression.
- [ ] Test exact-hex Contact labels versus spatial uncertainty regions.
- [ ] Test synchronization Defense penalties for second and third attacks.

## Release gates

### Core-loop alpha

- [ ] Move–Search–Strike–React loop is complete.
- [ ] Players can explain why every formation acts next.
- [ ] Players see calculations and consequences before committing.
- [ ] Contact uncertainty has a clear spatial meaning.
- [ ] A scenario produces a scored operational outcome.
- [ ] Playtest telemetry exports successfully.

### Rules alpha

- [ ] Every action in the v0.5 menu is implemented.
- [ ] All rulings required by implemented systems are documented.
- [ ] No dominant action persists across the target test scenarios.
- [ ] The inactive player regularly receives meaningful decisions.
- [ ] Entropy creates recoverable capability problems rather than unexplained punishment.

### Public prototype

- [ ] New players can complete a scenario without designer assistance.
- [ ] Save/load and settings are reliable.
- [ ] Accessibility baseline is met.
- [ ] All distributed assets have recorded provenance and suitable licenses.
- [ ] Crash, rules, telemetry, and usability testing passes on target hardware.
