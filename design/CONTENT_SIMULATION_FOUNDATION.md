# Content and Simulation Foundation

Status: approved post-core-loop foundation, 2026-09-08. Numerical balance remains subject to recorded playtests.

## Formation roles

The shared action grammar remains authoritative. Formation roles differ through their operating purpose, ratings, range profiles, terrain access, logistics compatibility, standing mission, and scoring contribution—not through duplicate versions of Move, Search, or Strike.

### Surface groups

Surface groups are the maneuver, escort, sea-control, and screen specialists. Their baseline identity is:

- a Move standing mission toward the operational objective;
- eligibility to establish and contest control;
- eligibility to satisfy carrier escort objectives;
- access to naval ports and anchorages;
- a 7/9/11-hex Passive/Active/Focused sensor envelope;
- a 3/6/9-hex Light/Standard/Heavy weapon envelope;
- the ability to establish persistent Patrol/Screen areas and provide nearby Support.

This is deliberately different from a carrier group’s longer-range force projection and headquarters role. A new surface-only action is not justified until telemetry shows the shared Patrol, Support, control, and escort tools fail to create the intended decisions.

### Carrier and air operations

Carrier groups are force-projection headquarters. They provide the longest naval strike reach, begin with a Strike standing mission, matter directly to preservation/withdrawal scoring, and uniquely conduct HQ Recovery at compatible logistics access.

Air groups remain formations in the rules and save schema, but represent mission packages rather than continuously tracked aircraft. Their position is the current mission operating area. A Move activation abstracts launch, transit, on-station activity, and recovery; it uses the 4/6/8-hex air mission radii, ignores Land and Littoral movement cost, but still respects restricted areas and friendly stacking. Air groups use airfields for replenishment, cannot control objectives, and provide long-range Search, Strike, and Support.

Changing air groups into individually launched counters would multiply timing, basing, interception, and hidden-information state without evidence that it improves the operational decisions. Reconsider only after air-range playtests show that the mission-package abstraction collapses geography or creates unintuitive persistence.

### Submarines

Submarines retain the common Contact and combat procedure for this foundation. Their distinctiveness currently comes from low Signature, passive-search preference, ASW Support interaction, naval movement, and their own sensor/weapon envelopes.

A separate submarine Contact/combat procedure is not yet earned. Reopen it if recorded tests show at least one of these recurring failures: submarines are localized too quickly, non-ASW search is too effective, surface and subsurface attacks feel interchangeable, or players cannot express uncertainty about depth and datum age. Any replacement must preserve side-owned Contacts and must not leak a hidden submarine’s exact position or type.

## Search and Signature doctrine

Each formation kind owns a scenario-defined Passive/Active/Focused range profile. Search resolution combines the searcher’s effective rating, declared mode, applicable Support, target Signature, and actual range. Passive preserves discretion, Active gains a modifier and makes the searcher Loud, and Focused gains the largest modifier while consuming Command Attention. Cautious movement lowers Signature through the next action; High Tempo movement, Active Search, and Aggressive Patrol increase it.

Previews may show the acting formation’s rating, mode modifier, public range, and declared area. Hidden target Signature, exact position, and existence remain private until resolution.

## Weapons and layered defense

Every formation carries a finite Light/Standard/Heavy salvo inventory defined by authored scenario data. A committed Strike expends one matching salvo even when the selected Contact aim is empty, stale, or false. Heavy availability remains limited by Endurance, damage, and applicable Destruction effects. Replenishment at a compatible fixed or mobile logistics source restores magazines to their printed maxima; no other action reloads them. Logistics groups carry only a limited Light self-defense salvo.

Missile defense is scenario-defined by formation kind and resolves as deterministic modifiers inside the existing combat comparison:

- Outer defense applies against Light attacks.
- Area defense applies against Light and Standard attacks.
- Point defense applies against every salvo.
- A formation with an EW rating contributes one additional defensive modifier.
- Heavy attacks saturate outer and area layers but remain subject to point defense and EW.

The defender sees the resolved layer summary after commitment. Pre-commitment previews continue to hide target defenses.

## Electronic warfare and cyber

EW and Cyber are printed formation ratings, not invisible AI bonuses. EW reduces effective Signature at a rate of one per two rating points and contributes to layered defense. Cyber contributes one Search exploitation modifier per two rating points; opposing EW subtracts its full rating during private Search resolution. These effects use the same owned Contact, hidden-target, and telemetry boundaries as other Search and combat calculations.

## Logistics and repair

`LogisticsGroup` represents fleet trains, tenders, and mobile repair support. A friendly, combat-capable logistics group provides logistics access within one hex. Ports, anchorages, and airfields remain scenario-defined fixed facilities. Replenishment restores one Endurance step, all printed magazines, one damage step, the major-action track, and one selected Destruction card. Logistics groups cannot establish control, satisfy transit or escort objectives, deny an objective, or count as surviving combat capability.

## Weather and environment

Weather is authoritative scenario state with a nonnegative severity. Severity subtracts from Search mode modifiers. Air mission packages add severity to the Ready-Time cost of complex actions; naval High Tempo movement in non-clear weather adds one Ready-Time. Scheduled events may change the current weather and severity. Presentation consumes the same named state but never invents a rules effect.

## Command architectures

- Centralized command provides four Command Attention slots.
- Mission Command provides the established three-slot baseline.
- Distributed command provides two slots and relies more heavily on Standing Missions and automatic triggers.

Architectures are assigned per side in scenario data and persist in saves. A scheduled doctrine event may change an architecture; slot state is reconciled deterministically.

## Scenario authoring and timeline events

Scenario metadata and complete formation definitions are loaded from `Assets/Resources/Data/scenario-catalog.json`. The compiled catalog retains operational-area factories as a safe migration fallback, while the distributable JSON is the active authoring source selected by `ScenarioCatalog.All()` and `ScenarioCatalog.Find()`.

The Unity menu **Sea of Uncertainty → Scenario Catalog Editor** edits scenario metadata, command architectures, formation ratings, EW/Cyber values, positions, Ready-Time, and magazine maxima. It validates stable IDs and required formation data before writing formatted JSON.

Scheduled events have stable IDs, operational time, type, and payload. The timeline processes events before the next later formation activation. Implemented types add a formation through a named Reinforcement region, change weather, or change one side’s command architecture. Entry chooses the first legal axial hex in stable order and events plus spawned formations survive save/load.

## Connected-theater contract

Connected theaters operate through a campaign layer above an individual `PrototypeGame`:

1. A formation may declare exit only from a scenario-defined Exit region.
2. The source scenario removes it at the completion of that action and emits a transfer record containing stable formation ID, side, source scenario/area/hex, destination scenario and entry-region ID, departure Ready-Time, travel duration, ratings, Endurance, damage, weapon, entropy-card, mission, and campaign-state data.
3. Travel advances off-map without granting Search, Strike, control, or Reaction opportunities unless a future route explicitly defines an interdiction event.
4. At arrival time, the destination resolves a legal hex from its Reinforcement region in stable axial order. If all entry hexes are occupied or prohibited, arrival waits one Ready-Time and retries; it never reveals hidden occupancy to the opposing side.
5. The destination preserves the formation ID and state, schedules it no earlier than the transfer arrival time, and creates Contacts only through normal information rules.
6. Saves and deterministic replay store the transfer record and route ID, never a Unity object or world-space coordinate.

The contract is defined here; campaign routing UI and executable transfers remain future implementation work.

## Evidence and balance

The separate submarine procedure is now bounded to depth state, ASW Search interaction, public Contact domain, and identified-datum requirements. It does not create a second hidden-information model. Numerical magazine sizes, defense layers, weather severity, command capacity, and reinforcement timing remain subject to recorded balance testing.
