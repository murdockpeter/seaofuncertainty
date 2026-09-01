# 3D MVP Budgets and Acceptance

## Windows profiles

- Minimum: Windows 10/11, 4-core CPU, 8 GB RAM, DirectX 11 GPU with 2 GB VRAM, 1280×720, 30 fps at the 95th percentile frame.
- Target: Windows 11, 6-core CPU, 16 GB RAM, GTX 1060/RX 580 class or better with 4 GB VRAM, 1920×1080, 60 fps target and no sustained period below 45 fps.

## Scene budgets

- 100k visible triangles, 100 materials, 180 draw calls, 512 MB texture memory, 64 simultaneous transient VFX, and 8 ms UI CPU time at target profile.
- Current procedural markers use shared materials and no runtime texture allocation beyond the map RenderTexture.
- The 24×20 pilot is the measurement ceiling for the MVP. Async theater streaming is deferred until a measured hitch exceeds 100 ms or a later theater exceeds this footprint. Grid chunking/culling is required before a full-basin map.

## Acceptance matrix

Automated: core determinism, both scenario schemas, hex/world round-trip, map edges, camera reset/bounds, save v1 migration, save v2 IDs, incompatible save error, geographic distance tolerance, land movement rejection, and information-side filtering.

Manual build sweep: 1280×720, 1600×900, 1920×1080, 2560×1080, resize, fullscreen/windowed, Windows DPI 100/125/150%, mouse-only actions, keyboard focus/shortcuts, reduced motion, and permanent-grid-free completion.

Human gates: dense-contact selection, coastal selection, overlapping markers, maneuver-space quality, and full-scenario usability require observed playtest sessions and may not be checked solely from automated tests.

## Go/no-go baseline

Go for MVP playtesting when both theaters load from the selector, rules smoke tests pass, a Windows build completes, no hidden formation is instantiated for the viewing side, and no P0 crash/data-loss defect is open. Do not expand to a full South China Sea theater until the human gates produce evidence that 20 nm decisions are legible and interesting.
