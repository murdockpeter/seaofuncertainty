# Modular Theater Connections

Operational areas retain stable IDs, local axial coordinates, projection, and geographic origin. A future campaign layer connects areas through named Exit and Reinforcement regions; crossing an exit emits a transfer record containing formation ID, source area/hex, destination area/entry region, elapsed Ready-Time, and campaign state. The destination scenario resolves the entry hex from data, never hardcoded runtime logic.

The MVP selector reads `ScenarioCatalog.All()` and therefore accepts another data-defined scenario card without new screen code. Network messages and telemetry continue to send scenario ID, operational-area ID, and axial coordinates rather than scene objects or world-space positions.
