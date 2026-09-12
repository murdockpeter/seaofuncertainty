# Graphics Incremental Awesomeness TODO

Last refreshed: **2026-09-11**

This is the separate, non-blocking visual-evolution backlog for Sea of Uncertainty. Its purpose is to make the game a little more convincing, readable, and delightful with each small pass without turning graphics work into an uncontrolled 1.0 beta dependency.

The release-critical backlog remains [`TODO.md`](../TODO.md). An item here becomes a release blocker only when it fixes a readability, accessibility, information-security, crash, or performance defect.

## How we use this list

- Prefer one conversation-sized visual improvement at a time.
- Preserve the sober maritime command-table direction; readability outranks spectacle.
- Improve one visual layer without casually redesigning the others.
- Keep authoritative rules on the axial hex grid. Cosmetic height, animation, trails, lighting, and effects never alter outcomes.
- Never reveal hidden units, exact enemy state, paths, ranges, or damage through presentation.
- Make color redundant with silhouette, text, border, glyph, motion, or pattern.
- Honor Reduced Motion and the current minimum-resolution presentation.
- Compare before/after captures at approximately 24°, 33°, and 58° camera pitch before keeping a change.
- Stay within the budgets in [`3D_MVP_BUDGETS_AND_ACCEPTANCE.md`](3D_MVP_BUDGETS_AND_ACCEPTANCE.md).
- Record every new or modified external asset in [`3D_ASSET_LEDGER.md`](3D_ASSET_LEDGER.md).

## Current visual foundation

- [x] 3D-first operational command table with optional edge HUD.
- [x] Physical Earth-radius globe curvature at the 20-NM operational scale.
- [x] Curved Natural Earth coastline geometry without ocean cut-through.
- [x] Traditional flat-top, edge-sharing hex presentation.
- [x] NOAA ETOPO-derived land elevation with restrained 4× visual exaggeration.
- [x] ETOPO bathymetry-driven ocean color zones, coastal shelf transitions, current bands, and animated normal detail.
- [x] Distinct carrier, surface, submarine, and air-group silhouettes with distant operational symbols.
- [x] Active-formation highlighting and formation-context order menu.
- [x] Procedural sunlight, terrain texture, coastal foam, haze, cloud shadows, and weather hooks.
- [x] Labeled, shape-redundant Contacts, state cues, order glyphs, and contextual legal-area overlays.

## Next five small chips

These are the preferred near-term sequence, one at a time.

- [ ] **GIA-001 — Wake fidelity:** Make every surface/carrier wake originate at the stern, align with actual formation movement/facing, taper consistently, and disappear when stationary or inappropriate.
- [ ] **GIA-002 — Air trail fidelity:** Replace ambiguous gray air-group lines with altitude-readable contrails that follow the aircraft heading, fade cleanly, and remain distinct from surface wakes and plotted routes.
- [ ] **GIA-003 — Elevation-aware land material:** Blend lowland green, upland olive, exposed-rock gray, and slope shading from measured height and gradient without making the theater look like a satellite photograph.
- [ ] **GIA-004 — Sea-state whitecaps:** Add sparse, view-stable whitecaps whose density follows configured sea state and which vanish under Reduced Motion where animation would distract.
- [ ] **GIA-005 — Label decluttering:** Add priority, collision avoidance, compact leader lines, and distance-based fading for formation and geographic labels.

## Ocean and coastline

- [ ] **GIA-010:** Refine the bathymetric palette so abyss, basin, slope, shelf, and shoal transitions remain visible at both high and low camera angles.
- [ ] **GIA-011:** Add restrained directional surface streaking derived from wind and sea-state profiles.
- [ ] **GIA-012:** Give current bands very slow, nonrepeating drift without sliding the geographically anchored bathymetric zones.
- [ ] **GIA-013:** Add a narrow wet-shore darkening band between coastal foam and dry land.
- [ ] **GIA-014:** Vary foam intensity by exposed coastline orientation and sea state while keeping it cosmetic.
- [ ] **GIA-015:** Introduce shallow reef/sandbar hints around appropriate littoral islands without implying authoritative traversability.
- [ ] **GIA-016:** Improve sun glitter into a broken, view-dependent path rather than a uniform glossy response.
- [ ] **GIA-017:** Add gentle horizon reflection and fresnel brightening while preserving grid contrast.
- [ ] **GIA-018:** Review ocean coloration under all weather palettes, high contrast, and color-vision simulations.

## Terrain and landforms

- [ ] **GIA-020:** Smooth terrain normals selectively so measured ridgelines remain clear without faceted spikes at low camera pitch.
- [ ] **GIA-021:** Add elevation- and slope-aware material blending using the existing ETOPO mesh.
- [ ] **GIA-022:** Add broad vegetation variation appropriate to Luzon and Taiwan without random high-frequency noise.
- [ ] **GIA-023:** Improve large-river and valley readability where the source resolution supports it.
- [ ] **GIA-024:** Add restrained coastal cliff treatment where steep measured slopes meet the sea.
- [ ] **GIA-025:** Blend terrain-tile boundaries and coastline edges under extreme lighting and low-angle views.
- [ ] **GIA-026:** Add distant terrain LOD or simplified relief only when measured performance justifies it.
- [ ] **GIA-027:** Develop a compatible visual treatment for fictional theaters that does not pretend to be measured geography.

## Atmosphere, lighting, and sky

- [ ] **GIA-030:** Replace the flat clear color beyond the globe with a restrained sky-to-horizon gradient.
- [ ] **GIA-031:** Improve maritime haze so distant terrain loses contrast gradually rather than appearing uniformly faded.
- [ ] **GIA-032:** Add soft aerial perspective tied to camera distance and weather visibility.
- [ ] **GIA-033:** Refine cloud shadows with multiple scales, softer edges, and wind-consistent drift.
- [ ] **GIA-034:** Add subtle cloud illumination without creating gameplay-obscuring cloud geometry.
- [ ] **GIA-035:** Create curated dawn, daylight, overcast, and dusk lighting profiles with tested overlay contrast.
- [ ] **GIA-036:** Improve formation contact shadows and ambient fill at low sun angles.
- [ ] **GIA-037:** Add restrained color grading per weather profile while keeping affiliation and warning colors stable.

## Formations, wakes, and motion

- [ ] **GIA-040:** Audit heading and wake orientation for every formation type, movement mode, camera angle, and state rebuild.
- [ ] **GIA-041:** Give carriers a broader, gently diverging stern wake and escorts narrower independent wakes.
- [ ] **GIA-042:** Give surface groups tapered wakes scaled by recent movement commitment.
- [ ] **GIA-043:** Keep submarines free of surface wakes unless an explicitly visible surfaced state is ever introduced.
- [ ] **GIA-044:** Make aircraft contrails originate from engines/wing roots, taper with age, and remain visibly elevated above the sea.
- [ ] **GIA-045:** Refine formation hull proportions, superstructure silhouettes, and recognition markings without increasing information leakage.
- [ ] **GIA-046:** Add restrained material differentiation among hull, deck, canopy, sensor, and recognition surfaces.
- [ ] **GIA-047:** Improve formation bank/turn/settle animation during movement while preserving exact authoritative endpoints.
- [ ] **GIA-048:** Refine close-to-symbol LOD transitions to avoid popping and preserve selection targets.
- [ ] **GIA-049:** Add subtle selected/hovered model response that remains legible without relying on the label alone.

## Orders, Contacts, and action effects

- [ ] **GIA-050:** Refine movement previews into clean directional route ribbons with unambiguous origin and destination.
- [ ] **GIA-051:** Give Passive, Active, and Focused Search distinct but related sweep treatments.
- [ ] **GIA-052:** Improve Strike launch, interception, defense erosion, impact, and no-confirmed-effect sequencing without revealing hidden outcomes.
- [ ] **GIA-053:** Make synchronized strikes visually communicate participant timing and volley order without exposing concealed formations.
- [ ] **GIA-054:** Refine Contact uncertainty areas with age-dependent line weight/pattern rather than color alone.
- [ ] **GIA-055:** Improve contradictory-fix and false-Contact presentation so ambiguity is obvious but not accidentally resolved.
- [ ] **GIA-056:** Give Friction, Disruption, Destruction, Loud, cohesion, and endurance cues a unified visual grammar.
- [ ] **GIA-057:** Add subtle completion acknowledgement to each major order while respecting Reduced Motion.

## Labels and cartography

- [ ] **GIA-060:** Establish explicit label priorities for active formation, Contacts, objectives, ports, airfields, straits, and background geography.
- [ ] **GIA-061:** Prevent labels from covering selectable formations, Contacts, right-click menus, or major objective markers.
- [ ] **GIA-062:** Add leader lines only when displacement is necessary and keep them visually subordinate.
- [ ] **GIA-063:** Fade or simplify geographic labels with zoom while retaining important chokepoints and objectives.
- [ ] **GIA-064:** Improve scale, heading, range, weather, and camera-state cartography as a coherent minimal instrument layer.
- [ ] **GIA-065:** Explore subtle latitude/longitude or operational-region markings when they materially improve orientation.
- [ ] **GIA-066:** Review every label at 1280×720, ultrawide, and 150% text scaling.

## Menus and 3D interaction polish

- [ ] **GIA-070:** Continue refining the formation order menu’s spacing, hierarchy, anchoring, and screen-edge avoidance.
- [ ] **GIA-071:** Add compact icon-plus-text order affordances that match the established action language.
- [ ] **GIA-072:** Improve menu opening/closing transitions with a Reduced Motion alternative.
- [ ] **GIA-073:** Refine active, hovered, legal, illegal, and committed formation states across model, ring, label, and menu.
- [ ] **GIA-074:** Ensure overlays never obscure reaction prompts, handoffs, card choices, or critical result text.
- [ ] **GIA-075:** Explore more information physically attached to the command table while keeping the edge HUD optional.

## Camera and command-table presentation

- [ ] **GIA-080:** Refine default camera poses for both theaters using the curved world and measured terrain.
- [ ] **GIA-081:** Improve camera focus transitions without overshoot, motion sickness, or lost orientation.
- [ ] **GIA-082:** Add a subtle visual north reference that remains useful after free orbit.
- [ ] **GIA-083:** Refine the visible edge/thickness of the operational globe segment so it feels intentional at low angles.
- [ ] **GIA-084:** Explore a restrained command-room or darkness treatment beyond the operational surface.
- [ ] **GIA-085:** Review near/far clipping, shadow distance, and horizon composition at every allowed pitch and zoom.

## Optional future delights

- [ ] **GIA-090:** Regional storm cells with readable precipitation shafts and sea darkening.
- [ ] **GIA-091:** Moonlit/night operations palette with navigation-light restraint and accessible overlays.
- [ ] **GIA-092:** Very subtle bioluminescent or phosphorescent wakes as a special scenario treatment, never a default.
- [ ] **GIA-093:** Cinematic scenario-opening camera move with immediate skip and Reduced Motion bypass.
- [ ] **GIA-094:** High-quality still-image/photo mode that does not expose hidden information.
- [ ] **GIA-095:** Curated visual themes for future geographic theaters while sharing the core command language.

## Definition of done for one graphics chip

Each completed item should include:

1. a clear visual purpose and rollback path;
2. before/after captures from high, medium, and low camera pitch;
3. verification at 1280×720 and 1920×1080;
4. checks for overlay contrast, label collision, hidden-information discipline, high contrast, and Reduced Motion;
5. a clean player log and the relevant automated presentation/integration tests;
6. a Windows development build for human review;
7. performance comparison against the previous visual baseline;
8. an updated asset/provenance ledger when assets or generators change.

The goal is cumulative polish: many disciplined five-percent improvements, not periodic visual rewrites.
