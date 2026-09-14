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

- [x] **GIA-010:** The bathymetric palette now uses an explicit four-stop shoal/shelf/slope/abyss ramp blended at increased strength over a calmed base layer, so depth transitions read clearly instead of one wide shallow-to-slope gradient.
- [x] **GIA-011:** Restrained directional surface streaking now runs along the theater's shared wind direction (the same heading sea-state whitecaps use), fading out entirely on calm seas and strengthening with configured sea state.
- [x] **GIA-012:** Current bands now live on their own tileable detail layer that drifts slowly and nonrepeating (two incommensurate periods plus a slow creep) under Reduced Motion gating, fully independent of the static, geographically anchored bathymetric base texture.
- [x] **GIA-013:** Coastlines now layer a narrow static wet-shore darkening band beneath the foam and dry-land demarcation.
- [x] **GIA-014:** Foam intensity now follows each shoreline stroke's exposure to the theater's prevailing wind (windward shores probe for the actual seaward side rather than assuming polygon winding) and the configured sea state, with a deliberately visible, staggered per-stroke breathing pulse — distinct from the sea-state whitecaps' much subtler animation — that freezes at full base intensity under Reduced Motion instead of hiding.
- [x] **GIA-015:** Shallow shoal water (depth < 40 m) now carries restrained, noise-driven reef/sandbar mottling as a color-only hint layered into the existing bathymetric palette; it is cosmetic exactly like every other depth band and implies nothing about authoritative traversability.
- [x] **GIA-016:** A fourth, fine-grained, low-weight octave in the ocean normal map breaks the specular highlight into a scattered, view-dependent glitter path instead of one smooth glossy blob.
- [x] **GIA-017:** Water glossiness and metallic were nudged up to strengthen Standard's own physically-based grazing-angle Fresnel response for gentler horizon brightening, without introducing a custom shader (the project's build pipeline was found this session to silently drop unused non-Standard shader variants) or meaningfully affecting hex-grid contrast.
- [x] **GIA-018:** Reviewed. The bathymetric palette's shoal/shelf/slope/abyss bands remain distinguishable under simulated protanopia, deuteranopia, and tritanopia because the ramp is built on luminance contrast, not hue alone. "High-contrast side colors" is correctly scoped to Blue/Red formation identification in the 2D UI and does not need to touch ocean color. Only the "Clear" and "Haze" weather presets are exercised by real scenario content; the "Overcast"/"Rain" lighting and precipitation branches exist but are currently unreachable by any scenario or test — noted as a follow-up, not fixed here.

## Terrain and landforms

- [x] **GIA-020:** Terrain shading normals now blend toward neighboring grid normals most strongly on low-angle facets and preserve high-angular-difference ridgelines; measured ETOPO vertex heights remain untouched.
- [x] **GIA-021:** Elevation- and slope-aware material blending now uses the existing ETOPO mesh and measured gradients; completed as part of GIA-003.
- [x] **GIA-022:** A single broad canopy-density field now favors low, gently sloped terrain and recedes on steep or high exposed ground, avoiding high-frequency vegetation noise.
- [x] **GIA-023:** Low-elevation samples sitting below their four measured neighbors receive restrained moist valley/drainage shading; the treatment does not invent authoritative river geometry beyond ETOPO's resolution.
- [x] **GIA-024:** Elevated shoreline samples adjacent to measured open water receive a subdued exposed-rock tint distinct from gentle beach terrain, without implying gameplay traversability.
- [x] **GIA-025:** Shared cross-tile face normals, selective interior smoothing, clipped table-edge coastline closures, and the wet-shore transition now form one seam-controlled treatment validated at high and low camera pitch.
- [x] **GIA-026:** The current static 72,800-sample terrain and 17-tile draw footprint were assessed below the threshold where another mesh would justify its memory and transition cost; distance LOD remains deliberately off, with explicit thresholds for larger future theaters.
- [x] **GIA-027:** Fictional theaters without geographic resources use a deliberately schematic hachure/contour material on procedural hex landforms and never claim measured ETOPO elevation.

## Atmosphere, lighting, and sky

- [x] **GIA-030:** A camera-following, texture-driven maritime gradient replaces the flat clear color beyond the curved operational surface.
- [x] **GIA-031:** Distance-layered aerial haze now grades distant terrain directly, reducing far-field contrast without a screen-space slab or uniformly washing the foreground.
- [x] **GIA-032:** The aerial-perspective blend follows actual camera-to-terrain distance and derives its restrained density from theater visibility and haze configuration.
- [x] **GIA-033:** The directional-light cloud cookie combines broad, medium, and soft noise scales and drifts along the shared theater wind vector.
- [x] **GIA-034:** A low-intensity, shadow-free cloud ambient light softens clouded scenes without adding gameplay-obscuring cloud geometry.
- [x] **GIA-035:** Dawn, daylight, overcast, dusk, and night branches now select curated sky, key-light, fill, and environmental-tint profiles with overlay luminance validation.
- [x] **GIA-036:** Every visible friendly formation receives a restrained surface contact shadow, including an offset aircraft shadow, while low-sun profiles retain cool ambient fill.
- [x] **GIA-037:** Environment-only weather grading subtly tints water and terrain; Blue, Red, Contact, and warning materials remain outside the grade.

## Formations, wakes, and motion

- [x] **GIA-040:** Formation-facing and local-space trail orientation are now audited across every visible formation kind; resting heading persists across state rebuilds, movement heading follows the globe tangent, and camera orbit remains presentation-only.
- [x] **GIA-041:** Moving carriers now produce a broad centerline propwash, gently diverging main-hull wake, and narrower independently aligned port/starboard escort wakes.
- [x] **GIA-042:** Moving surface and logistics groups retain paired tapered wakes whose length and spread scale from actual hex commitment and Cautious, Normal, or High Tempo movement mode.
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
