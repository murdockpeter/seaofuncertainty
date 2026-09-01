# Sea of Uncertainty — 3D Design Constitution

Status: approved baseline for prototype implementation. These decisions may be revised through playtesting.

## Authority

The rules model remains a deterministic two-dimensional axial hex topology. The 3D map is a presentation and interaction layer. Camera position, animation, model offsets, wakes, projectiles, and effects never change formation location, range, visibility, or outcomes.

## Scale

- Adjacent hex centers are exactly 20 nautical miles apart.
- One Ready-Time point represents two hours.
- Rules continue to use integer hex distance and Ready Time; the interface also displays nautical miles and elapsed hours.
- Existing movement and range values remain provisional until dedicated scale playtests approve formation-specific values.

## Camera and interaction

- Use an angled operational command-table camera, not unrestricted free flight.
- Permit full bounded command-camera control: WASD translation, continuous orbit and tilt, keyboard/mouse zoom, accelerated movement, and a reliable reset pose.
- Mouse wheel zoom is bounded; middle-button drag pans; right-button drag continuously orbits and tilts.
- Pitch remains bounded and cannot reach a horizon-level or straight-down view.
- Hexes are subtle at rest and emphasized contextually for movement, Search, Strike, objectives, and selection.
- At later production zoom levels, close 3D models transition to operational symbols and then aggregated markers.

## Vertical domains

Aircraft altitude and submarine depth are discrete operational states. Models may be vertically offset for readability, but world-space height is not authoritative distance or terrain clearance.

## Information integrity

3D presentation must never reveal an enemy formation’s true type, position, route, or state beyond the active side’s Contact information. Pass-and-play handoff clears selection, camera clues, overlays, and transient hidden-state visuals.

## Modularity

Operational areas own geography, scale, valid hexes, locations, presentation settings, and regions. Scenarios reference an operational area and own forces, setup, objectives, horizon, weather, special rules, and victory conditions. Runtime logic must not depend on a specific theater.

Load authentic land geometry beyond the playable projection through the rendered table boundary so terrain never appears to terminate inside the command surface. Do not synthesize, stretch, or snap coastline vertices; interior polygon coordinates remain unchanged.

## Visual evolution

Every implementation pass should improve an appropriate part of the 3D presentation toward a believable maritime theater. Terrain relief, surface variation, lighting, atmosphere, models, and effects should become progressively richer while operational symbols, selectable areas, and imperfect-information boundaries remain immediately readable. Cosmetic simulation never changes deterministic outcomes.

Formation models are a dedicated visual-evolution track whenever that track is active. The current art direction freezes the approved platform counters, labels, selection rings, and distant symbols; do not change them during general environmental or gameplay passes until that direction is explicitly reopened. When reopened, carrier groups, surface groups, submarines, and air groups must remain recognizable by shape—not color alone—while side markings and distant operational symbols retain command clarity. Formation model detail is cosmetic and never discloses hidden information or changes authoritative position, range, or state.

The baseline daylight rig places its warm directional sun at east-northeast (067.5° true relative to map north), with the cool fill opposing it and no gameplay consequence.

## Solo AI evolution

Treat the solo opponent as an evolving core system, not a finished one-off feature. Every meaningful development pass should improve an appropriate aspect of AI decision quality, information discipline, strategic variety, explainability, diagnostics, or automated evaluation whenever the change touches gameplay or creates a relevant opportunity.

AI improvements must continue to use the authoritative rules, continuous Ready-Time scheduler, and only information legitimately available to the controlled side. The AI receives no hidden-information access, timing exception, combat bonus, or other compensating advantage unless a separately identified difficulty option explicitly discloses it.

Prefer measurable improvements: deterministic scenario simulations, decision-distribution reports, no-stall tests, tactical fixtures, and comparisons against the previous baseline. Preserve enough decision rationale in telemetry to explain why the AI selected Strike, Search, Move, Recover, or Hold.
