# Implementation and acceptance record — 2026-10-19

Baseline: commit `7b8ea4f`, branch `feature/ballistic-interception`, with the uncommitted implementation package described here. Unity `6000.2.12f1`, Windows development player. This records automated execution and agent inspection of rendered screenshots; it does not certify human play, balance, real controller input or Windows DPI combinations.

## Delivered

- Game save v14 records combat seed/draw progress. Operation envelope v5 records pending individual, Solo AI and synchronized defender decisions. Hotseat reload returns to the secure defender handoff. The reaction screen offers Save Operation. Legacy saves remain loadable with an explicit seed-1978/draw-zero migration; their unrecorded original future rolls cannot be reconstructed.
- Replay v2 includes random progress in its digest; the preceding replay v1/v13 digest shape and v1/v2 authority recovery remain supported. Historical replays still require matching scenario content and rules. Comprehensive recovery of artifacts from older catalogs/builds remains an open compatibility audit.
- Shared read-only checks explain authorization, Search, Support, Mission and Entropy-response restrictions before commitment. Weapon menus show costs and unavailable reasons; logistics suggests alternatives; synchronized aims can open per-participant eligibility details. Amber palette commands open an explanation through mouse or keyboard.
- The first operation offers a map quick start, reopenable from the Field Manual. Both scroll at large text sizes. Instructions cover the current map interface, Ready Time, inspection, camera, cards, reactions and save recovery. Briefings now show authored special rules and the actual defender-choice behavior.
- `narrows-shield` is the third catalog operation: six formations, T12, finite ballistic/interceptor stocks without reload, and four existing executable objectives. Both opening carriers have escort coverage. The original two live operations and compiled conventional theater templates remain intact. Formation/scenario print proofs and their manifest include the new operation.

## Bugs found and corrected

| Reproduction | Expected behavior | Verification |
|---|---|---|
| Save after combat/search, then continue | Later rolls and outcomes match uninterrupted play | Six seeded games compared after every subsequent action, with a reload every five actions |
| Load while Entropy cards are attached | Attached cards stay out of the draw pile | Deck completeness now counts attached cards; repeated-load comparisons retain identical deck state |
| Save a committed individual/AI/synchronized reaction | Resume the same choice; spend ammunition exactly once | Both Hotseat directions, Solo AI routing, synchronized declaration/reaction/resolution and repeated resolution checks |
| Display secure Hotseat handoff | Neither side's formation names appear before command is assumed | Production UI assertions and rendered handoff captures |
| Patrol reacquires a previously lost target | Reuse its report; telemetry continues recording | Gameplay regression reproduces reacquisition; telemetry also accepts duplicate reports already present in older saves |
| Open orders at 1280×720 / 150% text | Recover and Hold remain inside the palette | Short availability styling replaces the overflowing status suffix; inspected captures |
| Open inspector at large text sizes | Rating values stay inside their cells | Cells grow with content; ballistic descriptions follow the stat grid; inspected captures |
| Open Field Manual, long briefing or card table | Text remains scrollable and controls remain accessible | Bounded guide/manual/briefing content; cards grow to contain their action buttons; screenshot review |

## Automated results

`tools/run-tests.ps1` passed Regression (including Unit, Integration, Presentation and continuation checks), SharedArchitecture, ScenarioAuthoring, ContentSimulation, AdvancedContentSimulation, AiEvaluation and Windows Build. Presentation and Build were rerun after the final presentation adjustments. Logs are under `SeaOfUncertainty/TestResults/`.

Repeated-load continuation results:

| Operation | Seed | Actions | Launches | Interceptions | Final time |
|---|---:|---:|---:|---:|---:|
| Meridian Veil | 4200 | 90 | 6 | 5 | T16 |
| Meridian Veil | -1729 | 70 | 6 | 5 | T16 |
| Northern Gateway | 4200 | 134 | 5 | 2 | T24 |
| Northern Gateway | -1729 | 136 | 5 | 2 | T24 |
| Narrows Shield | 4200 | 39 | 2 | 2 | T12 |
| Narrows Shield | -1729 | 39 | 2 | 2 | T12 |

Every compared action produced the same state digest as uninterrupted execution, including future combat randomness, cards, magazines and interceptor stocks. Additional checks cover replay v1/v2, declaration/resolution boundaries, invalid random checkpoints, and no expenditure from previews.

## Visible development-player evidence

Run `tools/capture-acceptance.ps1 -Visible` after a Windows development build. Final evidence is under `SeaOfUncertainty/TestResults/acceptance-2026-10-19/final/`:

- PNGs for all three operations at 1280×720 and 1920×1080, each at 100% and 150% text, plus a native borderless-fullscreen capture.
- Briefing, setup, map/objective focus, orders, friendly/Contact inspectors, card table, Ready queue, guide/manual and reaction/handoff screens.
- `capture-report.txt` records actual dimensions, panel coordinates, display mode, graphics device and managed-memory samples. UI geometry is compared in panel coordinates, separately from screenshot pixels.
- Final run: **175 PNGs**, four completed sessions, no player-log exceptions, and no flags from the automated offscreen-button check. Representative screenshots were inspected, including the corrected card, inspector, handoff and briefing layouts. Hardware: Windows 11 (10.0.26200), NVIDIA GeForce RTX 4070 Laptop GPU.
- `Player.log`, session JSON exports and results captures. Earlier `screens`, `visible` and `reviewed` directories are investigative runs, including a discarded black hidden-window capture and the run that exposed the Contact/telemetry failure. Use `final` for acceptance evidence.

Four production-backend automated sessions completed using seed 1978, with decisions for both sides selected by AI:

| Operation | Mode | Actions | Result |
|---|---|---:|---|
| Meridian Veil | Solo | 79 | T16, completed |
| Northern Gateway | Solo | 132 | T24, completed |
| Narrows Shield | Solo | 40 | T12, completed |
| Meridian Veil | Local Hotseat | 79 | T16, completed |

These sessions exercise rendering, telemetry and completion through the production backend; individual defender UI choices and privacy are tested separately. They do not represent human decisions or observed balance approval. Captures use muted audio, reduced motion and a visible grid; check Settings before personal play. Memory samples and accelerated automated sessions are not a sustained performance-budget certification.

## Still open

- Accepted cosmetic finding: at 1920x1080 / 150% text, the results side-score panels extend beyond the central hero frame while remaining readable within the viewport. Review narrower results layouts during manual acceptance.
- Human-observed Solo games in both original scenarios and a complete Meridian Veil Hotseat game, using the playtest protocol; a human Narrows Shield balance review.
- Mouse-only/keyboard-only play across every action family, real controller input, native Windows DPI settings, audio-description/privacy review and the full visible acceptance matrix.
- A visible scenario-editor duplicate/save/reload/play demonstration, and sustained frame-rate/stutter/memory observation on release hardware.
- Historical replay/checkpoint artifacts from older catalog/rule versions. The supported format paths above do not recreate missing historical content or random state.

The active [TODO](../TODO.md) retains these as unfinished acceptance work.
