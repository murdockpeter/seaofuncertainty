# Modular Theater Connections

The authoritative gameplay contract now lives in `CONTENT_SIMULATION_FOUNDATION.md`. Operational areas retain stable IDs, local axial coordinates, projection, and geographic origin. A campaign layer connects them through named Exit and Reinforcement regions and serializable transfer records; Unity scene objects and world-space positions never cross that boundary.

The MVP selector reads `ScenarioCatalog.All()` and therefore accepts another data-defined scenario card without new screen code. Network messages and telemetry continue to send scenario ID, operational-area ID, and axial coordinates rather than scene objects or world-space positions.
