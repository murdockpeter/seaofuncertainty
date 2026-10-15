# Sea of Uncertainty - Short-term TODO

Updated: **2026-10-15**. Unity: **6000.2.12f1**.

This is the **only active TODO list**. The immediate goal is to validate the current 3D command interface, make the next GitHub checkpoint reviewable, and improve reliability before resuming the larger scenario expansion. Work through the sections in order; add findings here rather than opening another backlog.

## Current baseline

Scenario authoring v2, the shared map-order palette, compact command status, on-demand inspectors/Ready queue, and hover-first markers are implemented. Scenario authoring has merged; the UI/marker implementation and TODO reset are checkpointed on `feature/map-command-ui` for PR review. Presentation, Unit, Regression, SharedArchitecture, and Windows build checks passed on 2026-10-15. Those automated results do not complete the visible-playtest tasks below.

## Now - review the build and close this checkpoint

- [ ] **Review the visible UI in both theaters.** Check Meridian Veil and Northern Gateway at 1280x720 and 1920x1080, windowed/fullscreen, 100% and 150% text size, and the available Windows DPI settings. Capture objective hover, formation/Contact cards, orders, inspector, Ready queue, reactions, and card overlays. Fix clipped text, tiny selection targets, overlap, and cards escaping the map.
- [ ] **Verify complete command access.** Exercise mouse-only and keyboard-only play through every action family, Missions, Push Through, HQ Recovery, and synchronized-strike declaration/resolution/retask/abort. Check Space, 1-9, O/C/I/T/H/L/G, map-cursor targeting, Escape, and modal focus return; spot-check controller submit/cancel and order access. Hover cards must not intercept clicks or be the only way to obtain required information.
- [ ] **Check information privacy during real play.** Test Solo and both directions of Hotseat handoff. Inspect unknown and contradictory Contacts, objective presence text, the Ready queue, feedback, logs, audio descriptions, and reopened overlays. Record and fix any disclosure of enemy identities, actual positions, private orders, or hidden objective control.
- [ ] **Complete three observed games.** Play Solo once on each original scenario and one complete Meridian Veil Hotseat game. Export playtest data; record stalls, illegal AI behavior, unclear readiness/objectives, unused actions, reaction decisions, and repeated rule lookups. Use the [playtest protocol](design/PLAYTEST_PROTOCOL.md).
- [ ] **Resolve the findings that block play.** Give each observed issue a reproduction, expected behavior, and verification result. Address crashes, data loss, information leaks, soft locks, and inaccessible commands before cosmetic refinements. Keep accepted minor issues explicit rather than treating a passing test suite as visual approval.
- [ ] **Prove the scenario editor workflow visibly.** Duplicate an existing operation, assign a third stable ID, validate/save as catalog v2, reload, select, and play it with its own objectives/forces/events. Confirm both original scenarios remain intact. Record the result and remove temporary fixture content unless intentionally approved for the catalog. Follow the [authoring guide](design/SCENARIO_AUTHORING.md).
- [ ] **Complete UI PR review and integration.** The implementation checkpoint includes the UI, marker polish, TODO archives, and the scene's regenerated camera object IDs. Attach the remaining manual results: build/worktree state, scenarios/modes, display/text settings, outcomes, and accepted issues. Rerun affected suites after fixes and rebuild if runtime content changed, then review and merge the PR into `main`.

## Next - a small reliability and usability package

- [ ] **Make saving atomic and retain a good backup.** Write to a temporary file, flush it, and replace the primary save safely while retaining the last valid save. Test failed writes/replacements without destroying the previous save. Carried forward from the archived beta save-integrity work.
- [ ] **Make failed loads recoverable.** Handle corrupt, truncated, incompatible/newer saves and missing authored scenario IDs with clear messages. Offer a valid backup when available; do not replace the running game or overwrite the save on failure. Preserve the documented legacy-save compatibility path explicitly.
- [ ] **Explain unavailable actions before commitment.** Show the reason and legal alternative for unavailable orders, weapons, support, logistics, command attention, and synchronized participants. Keep displayed ranges, ammunition, costs, and risks consistent with the authoritative preview without revealing hidden state.
- [ ] **Teach the current map interface.** Add a short first-run guide and refresh the Field Manual for map orders, camera movement, Ready Time, objective/Contact hover, keyboard alternatives, cards, reactions, and save/load. Remove instructions referring to the deleted sidebar/action-panel or edge-HUD toggle.
- [ ] **Run a focused performance/stability check.** Observe a sustained session on the current Windows machine, including dense Contacts, coastal selection, repeated hovering, camera movement, and modal opening. Record frame rate, obvious stutter, memory growth, and player-log exceptions; fix regressions in the new interface before adding more visual effects. Formal release hardware budgets remain in the [performance specification](design/3D_MVP_BUDGETS_AND_ACCEPTANCE.md).

## After this checkpoint

Resume ballistic weapon profiles and launch-unit capabilities, followed by interception, product integration, and the four authored operations in the [scenario/ballistic plan](design/NEW_SCENARIOS_AND_BALLISTIC_WARFARE.md). Scenario authoring support is already implemented; the ballistic rules and four-scenario pack are not. Promote the next concrete implementation slice here when it is scheduled.

Broader beta packaging, content freeze, full accessibility/hardware acceptance, online production work, and speculative graphics polish remain in the archive. They are deferred from this short-term queue, not completed or waived; public distribution still requires an explicit release review.

## Evidence and history

For each completed review, record the build/commit or dirty-worktree state, scenario/mode, hardware/display settings, result, and relevant screenshot/log/playtest export. Keep generated output under `SeaOfUncertainty/TestResults/`; record the review summary in the PR or a dated playtest note.

All previous TODO/checklist snapshots, including unfinished work and the original alpha history, are listed in the [archive index](design/archive/README.md). Rules and design documents remain references; they are not competing active TODO lists.
