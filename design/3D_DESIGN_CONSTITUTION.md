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
- Mouse wheel zoom is bounded; middle-button drag pans; right-button drag rotates in 30-degree steps.
- Pitch remains bounded and cannot reach a horizon-level or straight-down view.
- Hexes are subtle at rest and emphasized contextually for movement, Search, Strike, objectives, and selection.
- At later production zoom levels, close 3D models transition to operational symbols and then aggregated markers.

## Vertical domains

Aircraft altitude and submarine depth are discrete operational states. Models may be vertically offset for readability, but world-space height is not authoritative distance or terrain clearance.

## Information integrity

3D presentation must never reveal an enemy formation’s true type, position, route, or state beyond the active side’s Contact information. Pass-and-play handoff clears selection, camera clues, overlays, and transient hidden-state visuals.

## Modularity

Operational areas own geography, scale, valid hexes, locations, presentation settings, and regions. Scenarios reference an operational area and own forces, setup, objectives, horizon, weather, special rules, and victory conditions. Runtime logic must not depend on a specific theater.
