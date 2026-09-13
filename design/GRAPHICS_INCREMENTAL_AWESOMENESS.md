# Graphics Incremental Awesomeness TODO

Last refreshed: **2026-09-13**

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
- [x] Traditional flat-top, edge-sharing hex presentation positioned at sea-surface depth so solid land naturally occludes it.
- [x] NOAA ETOPO-derived land elevation with restrained 4× visual exaggeration.
- [x] ETOPO bathymetry-driven ocean color zones, coastal shelf transitions, current bands, and animated normal detail.
- [x] Distinct carrier, surface, submarine, and air-group silhouettes with distant operational symbols.
- [x] Active-formation highlighting and formation-context order menu.
- [x] Procedural sunlight, terrain texture, coastal foam, haze, cloud shadows, and weather hooks.
- [x] Labeled, shape-redundant Contacts, state cues, order glyphs, and contextual legal-area overlays.

## Next five small chips

These are the preferred near-term sequence, one at a time.

- [x] **GIA-001 — Wake fidelity:** Surface wakes now originate at the stern only while a carrier, surface group, or logistics group is moving, follow formation-local heading, taper through a widening V, and fade after arrival; submarines and stationary surface formations remain wake-free.
- [x] **GIA-002 — Air trail fidelity:** Air groups now use paired, layered blue-white contrails at aircraft altitude: a feathered noisy mist envelope surrounds a narrower fading vapor core, both aligned in formation-local space and visually distinct from surface wakes and plotted routes.
- [x] **GIA-003 — Elevation-aware land material:** The ETOPO grid now drives a theater-scale coastal-green, lowland, upland-olive, and high-rock palette plus subdued aspect and steepness shading from measured local gradients; restrained broad mottling preserves the command-table style. Shared neighboring face normals remove terrain-tile lighting seams while retaining the original 4× relief and interior terrain detail.
- [x] **GIA-004 — Sea-state whitecaps:** Sparse deterministic foam strokes now remain anchored to deep-water points on the curved globe, scale their count and intensity from the theater's configured sea state, breathe very subtly, and disappear entirely under Reduced Motion.
- [x] **GIA-005 — Label decluttering:** Active formations, actionable Contacts, other Contacts, formations, objectives, straits, facilities, and background geography now follow explicit priorities; screen-space collision candidates displace lower-priority labels, compact leader lines preserve their anchors, and distance fading reduces clutter without hiding critical labels.

## Ocean and coastline

- [ ] **GIA-010:** Refine the bathymetric palette so abyss, basin, slope, shelf, and shoal transitions remain visible at both high and low camera angles.
- [ ] **GIA-011:** Add restrained directional surface streaking derived from wind and sea-state profiles.
- [ ] **GIA-012:** Give current bands very slow, nonrepeating drift without sliding the geographically anchored bathymetric zones.
- [x] **GIA-013:** Coastlines now layer a narrow static wet-shore darkening band beneath the foam and dry-land demarcation.
- [ ] **GIA-014:** Vary foam intensity by exposed coastline orientation and sea state while keeping it cosmetic. First-stage static broken-foam masking is implemented; exposure weighting and deliberately visible, staggered opacity motion remain pending visual approval.
- [ ] **GIA-015:** Introduce shallow reef/sandbar hints around appropriate littoral islands without implying authoritative traversability.
- [ ] **GIA-016:** Improve sun glitter into a broken, view-dependent path rather than a uniform glossy response.
- [ ] **GIA-017:** Add gentle horizon reflection and fresnel brightening while preserving grid contrast.
- [ ] **GIA-018:** Review ocean coloration under all weather palettes, high contrast, and color-vision simulations.

## Terrain and landforms

- [ ] **GIA-020:** Smooth terrain normals selectively so measured ridgelines remain clear without faceted spikes at low camera pitch.
- [x] **GIA-021:** Elevation- and slope-aware material blending now uses the existing ETOPO mesh and measured gradients; completed as part of GIA-003.
- [ ] **GIA-022:** Add broad vegetation variation appropriate to Luzon and Taiwan without random high-frequency noise.
- [ ] **GIA-023:** Improve large-river and valley readability where the source resolution supports it.
- [ ] **GIA-024:** Add restrained coastal cliff treatment where steep measured slopes meet the sea.
- [ ] **GIA-025:** Blend terrain-tile boundaries and coastline edges under extreme lighting and low-angle views. Terrain-tile lighting seams use shared neighboring face normals at the original 4× relief, and coastline strokes omit artificial closures where land exits any theater edge; broader coastline and multi-angle validation remain.
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
- [x] **GIA-043:** Submarines remain free of surface wakes unless an explicitly visible surfaced state is introduced later.
- [x] **GIA-044:** Aircraft contrails originate as paired wing-root trails, expand into textured translucent mist around a narrow core, fade with age-distance, follow formation heading, and remain visibly elevated above the sea; completed as part of GIA-002.
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
