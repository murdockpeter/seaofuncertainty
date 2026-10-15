# Scenario catalog authoring

The distributable catalog is `SeaOfUncertainty/Assets/Resources/Data/scenario-catalog.json`. Open **Sea of Uncertainty > Scenario Catalog Editor** in Unity to edit it. Expand **Scenario Definition** and the nested lists. **Duplicate Selected** creates an independent, explicitly authored copy; change its stable ID. **Add Scenario** starts a blank operation, which remains invalid until it has forces and executable objectives. **Validate** and **Save JSON** use the same resolver and validation as runtime loading. Invalid catalogs cannot be saved through the editor.

## Catalog version 4

Each operation must specify `TheaterTemplateId` using one of these stable IDs:

- `meridian-veil-archipelago`
- `luzon-strait-northern-approaches`

The theater supplies fresh map geometry, scale, projection, coastline/elevation references, named facilities, area regions, restrictions, the central objective, presentation, and baseline sensor/defense profiles. It never supplies another operation's forces, objectives, Contacts, scheduled reinforcements, or other events. A new ID is added to the runtime catalog and selector without a compiled scenario factory. A catalog entry with an existing operation ID replaces that operation.

| Fields | Resolution |
| --- | --- |
| ID, display name, summary, horizon, weather/severity, special rules, victory text, Blue/Red command architecture | Authored values; no metadata inheritance. Weather severity is 0–3. |
| Formations, Contacts, Objectives, ScheduledEvents | Always authored; omitted or empty lists mean none. Forces must include an initial combat formation for each side; objectives must include at least one scored rule. |
| DeploymentRegions, ReinforcementRegions, ExitRegions, LogisticsRegions | Always authored; omitted or empty lists mean none. |
| AreaRegions, RestrictedAreas | Inherit theater values while the corresponding `Replace...` flag is false. When true, the authored list replaces the whole list, including `[]` to clear it. |
| SensorRanges, WeaponRanges, MissileDefenseProfiles | Inherit baseline profiles while the corresponding `Replace...` flag is false. When true, replace the whole list. Exactly one valid profile per formation kind is required, so clearing these profiles is invalid. |
| Objective, Presentation | Inherit while `OverrideObjective` / `OverridePresentation` is false; replace when true. |

Use `[]` for an empty list. A nonempty list behind a disabled replacement flag is not applied. There is no item-by-item merge. Reusing a region ID across roles is allowed only when its ordered hex definitions agree; duplicates within a single list and conflicting definitions are rejected.

`FormationDefinition` supplies side, kind, initial coordinates/Ready Time, ratings, and current/maximum Light, Standard, and Heavy inventories. Signature may be negative; other ratings and inventories must be nonnegative. Current inventories cannot exceed capacity. Weapon ranges are conventional, formation-kind profiles with `Light`, `Standard`, and `Heavy` ranges in hexes. They apply to ordinary strikes, counterattack eligibility, synchronized strikes, AI decisions, map previews, and UI range labels. Batteries have zero conventional ranges. Ballistic weapons use the separate per-formation profile described below.

## Ballistic capabilities

Expand each formation's `BallisticWeapons`: set Range (hexes), AttackModifier (-6..6), Capacity, Ammunition, TargetDomains, MinimumLocation, and MinimumIdentity (at least General for an enabled profile). Defaults grant no weapons. Subsurface targeting also requires explicit AllowShallowSubmarines / AllowDeepSubmarines settings. These profiles are independent per formation, including carrier/surface/submarine/air/logistics abstractions and the appended BallisticMissileBattery kind.

Enable `SupportsBallisticReload` only on compatible logistics regions. A formation must also have ordinary logistics access at its current hex; a battery uses an Airfield and cannot receive nearby fleet-train service. Ordinary replenishment never grants unconfigured ballistic reloads. See [ballistic launch rules](BALLISTIC_LAUNCH_RULES.md) for damage restrictions, Contact legality, expenditure, launch exposure, and launch restrictions. Configure `BallisticDefense` separately with rating 0..6, current/capacity interceptor stocks, coverage radius, protected domains, self/ready restrictions, and submarine permissions. Enable `SupportsInterceptorReload` independently on compatible logistics regions. See [ballistic defense rules](BALLISTIC_DEFENSE_RULES.md) for automatic selection, expenditure, synchronized coverage and privacy.

## Objectives and events

Supported objective kinds remain Control, Transit, Escort, Denial, and Withdrawal. Set positive points and a descriptive title. Control uses the shared rules control radius and the area's central `Objective`. Transit and Withdrawal reference existing Blue/Red regions. Escort and Withdrawal require a deployed carrier on each side; designated convoy targets are a later feature. Escort/Denial evaluate a minimum radius of one hex. The briefing lists descriptions generated from these executable rules alongside the authored titles, avoiding a hardcoded carrier-preservation mission for every operation. Narrative victory text does not create additional scoring rules.

Contacts must refer to deployed opposing formations with valid owner/quality values, positions, and nonnegative age. Reinforcements cannot have initial Contacts before they enter.

Scheduled events need unique IDs and times between zero and the operation horizon. Weather changes supply weather/severity. Command changes supply side/architecture. Reinforcements supply a formation with a globally unique ID, a matching side, and an entry `RegionId` from `ReinforcementRegions` or area regions. Entry placement uses stable hex order and retries when friendly occupation blocks every legal entry hex. Event resolution does not mutate the authored reinforcement definition.

Batteries deploy and reinforce only on unrestricted Land; their movement routes remain on contiguous Land. Land battery setup regions do not require a water route to the naval objective. Naval deployments must be in bounds, on water or at a port/anchorage, and outside restricted areas; air packages may occupy land but still respect boundaries/restrictions. Setup, reinforcement, exit, and transit/withdrawal destination regions are checked for navigable access to the central objective. Logistics regions must contain a port, airfield, or anchorage. Invalid references, enum values, profiles, inventories, duplicate IDs, and unreachable destinations produce errors naming the operation.

## Compatibility and migration

The shipped version 1 catalog remains supported explicitly: it may override only the two compiled operation IDs, retains compiled objectives/Contacts/events/regions, and replaces formations only when its formation list is nonempty. Unknown IDs in version 1 now report an error with upgrade guidance. Missing catalog resources retain the compiled catalog; malformed content and unsupported catalog versions report errors rather than quietly reverting to different operations.

Opening v1/v2/v3 in the editor upgrades resolved definitions to v4 in memory; **Save JSON** persists the upgrade after validation. Operational schema v7 added launch profiles; v8 adds interception profiles and a separate interceptor reload-service flag. Catalogs v1/v2, pre-v7 scenarios, and pre-v12 game saves explicitly migrate to zero ballistic capability. Missing legacy battery-kind profiles receive defaults (zero conventional ranges). Pre-v13 saves and pre-v4 catalogs explicitly receive zero interception capability while retaining their existing ballistic launch capability. Game saves are now v13; operation envelopes remain v4. New selections and file loads use `Require` for explicit unknown-ID errors; `Find` retains the documented legacy runtime fallback.

## Verification

With Unity closed, run:

```powershell
.\tools\run-tests.ps1 -Suite ScenarioAuthoring
.\tools\run-tests.ps1 -Suite Unit,Integration,ContentSimulation,AdvancedContentSimulation,Presentation,Regression,SharedArchitecture
```

The authoring checks are also included in AdvancedContentSimulation. They resolve a third authored operation through JSON, start it, exercise its scoring/ranges/weather/reinforcement, round-trip saved state, verify independent objects, compare both original operations before/after catalog migration, and reject invalid authoring boundaries. The fixture is test content; the four expansion scenarios remain separate work.

Validation on 2026-10-15: ScenarioAuthoring, Unit, Integration, ContentSimulation, AdvancedContentSimulation, Presentation, Regression, SharedArchitecture, and AiEvaluation passed in Unity 6000.2.12f1. After the final port-deployment and deferred-reinforcement fixes, ScenarioAuthoring, Regression, AdvancedContentSimulation, SharedArchitecture, and AiEvaluation were rerun and passed. Windows build validation and manual editor/playtest QA were not run for this change.
