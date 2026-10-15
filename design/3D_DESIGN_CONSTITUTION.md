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

## Command interface

- Issue orders through the active formation's contextual palette on the 3D map. Mouse, number-key shortcuts, and controller navigation use the same command dispatch and posture/weapon pickers.
- Keep a compact status strip for current Time, active formation, next friendly readiness, and actual command capacity/strain. Do not maintain a second permanent action panel or sidebars.
- Open the scrollable formation inspector with **I** and the friendly Ready queue with **T**. Queue rows inspect the selected friendly formation. Enemy Contacts expose only the owning side's known Contact report.
- Keep Push Through, HQ Recovery, mission assignment, and synchronized-strike declaration/resolution/retask/abort accessible through map orders. A ready synchronized strike directs conflicting shortcuts back to its resolution palette.
- Use **Space** on the idle map for orders, **1–3** for posture/weapon pickers, **4–9** for the remaining action families, and **Escape** to close an overlay, cancel map targeting, or open the menu. **Tab/Shift+Tab** remain focus navigation. Controller button 3 opens map orders; cancel backs out of targeting before pausing.
- Preserve cards, logs, rules, settings, save/load, grid controls, action previews, and audio descriptions. Private action feedback must be cleared or replaced with side-visible log content at a commander handoff.
- Keep formation and Contact markers compact at rest. Hover reveals a bounded, noninteractive detail card; objective geography uses a gold compass reticle with subdued ground emphasis. **O**, **C**, and the map cursor expose the same information without requiring mouse hover. Objective details show executable public rules and friendly presence, never hidden enemy control or contested status.

## Vertical domains

Aircraft altitude and submarine depth are discrete operational states. Models may be vertically offset for readability, but world-space height is not authoritative distance or terrain clearance.

## Information integrity

3D presentation must never reveal an enemy formation’s true type, position, route, or state beyond the active side’s Contact information. Pass-and-play handoff clears selection, camera clues, overlays, and transient hidden-state visuals.

## Modularity

Operational areas own geography, scale, valid hexes, locations, presentation settings, and regions. Scenarios reference an operational area and own forces, setup, objectives, horizon, weather, special rules, and victory conditions. Runtime logic must not depend on a specific theater.

Load authentic land geometry beyond the playable projection through the rendered table boundary so terrain never appears to terminate inside the command surface. Do not synthesize, stretch, or snap coastline vertices; interior polygon coordinates remain unchanged.

## Visual evolution

Every implementation pass should improve an appropriate part of the 3D presentation toward a believable maritime theater. Terrain relief, surface variation, lighting, atmosphere, models, and effects should become progressively richer while operational symbols, selectable areas, and imperfect-information boundaries remain immediately readable. Cosmetic simulation never changes deterministic outcomes.

Formation models are a dedicated visual-evolution track whenever that track is active. The track was formally reopened on 2026-09-08 and the code-owned production set is now the approved baseline. Carrier groups, surface groups, submarines, and air groups remain recognizable by hull or airframe silhouette, wake or depth treatment, recognition marking, and type-specific distant symbol—not color alone. Formation model detail is cosmetic and never discloses hidden information or changes authoritative position, range, or state. Changes follow `PRODUCTION_ART_DIRECTION.md` and update `3D_ASSET_LEDGER.md` in the same review.

The baseline daylight rig places its warm directional sun at east-northeast (067.5° true relative to map north), with the cool fill opposing it and no gameplay consequence.

## Solo AI evolution

Treat the solo opponent as an evolving core system, not a finished one-off feature. Every meaningful development pass should improve an appropriate aspect of AI decision quality, information discipline, strategic variety, explainability, diagnostics, or automated evaluation whenever the change touches gameplay or creates a relevant opportunity.

AI improvements must continue to use the authoritative rules, continuous Ready-Time scheduler, and only information legitimately available to the controlled side. The AI receives no hidden-information access, timing exception, combat bonus, or other compensating advantage unless a separately identified difficulty option explicitly discloses it.

Prefer measurable improvements: deterministic scenario simulations, decision-distribution reports, no-stall tests, tactical fixtures, and comparisons against the previous baseline. Preserve enough decision rationale in telemetry to explain why the AI selected Strike, Search, Move, Recover, or Hold.
