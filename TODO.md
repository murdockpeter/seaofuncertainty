# Sea of Uncertainty — 1.0 Beta Closure Plan

Last refreshed against the repository: **2026-09-11**

Unity baseline: **6000.2.12f1**

This is the single authoritative backlog for reaching a stable **1.0 beta**. Completed implementation history remains in [`design/archive/TODO_CORE_LOOP_ALPHA.md`](design/archive/TODO_CORE_LOOP_ALPHA.md). Detailed test procedures remain in the linked design documents.

## Release target

The supported 1.0 beta is:

- Windows 10/11, 64-bit;
- Solo vs AI and Local Hotseat;
- Meridian Veil and Luzon Strait — Northern Approaches;
- mouse and keyboard as primary input, with the existing controller-navigation baseline;
- versioned save/load, settings persistence, playtest export, and the current rules PDF.

Online Preview remains an explicitly experimental developer feature. Public Internet multiplayer is not a 1.0 beta dependency.

Non-blocking visual polish is tracked separately in [`design/GRAPHICS_INCREMENTAL_AWESOMENESS.md`](design/GRAPHICS_INCREMENTAL_AWESOMENESS.md). Graphics items do not become beta gates unless they address readability, accessibility, information security, stability, or measured performance.

Priority meanings:

- **P0:** required before distributing the 1.0 beta;
- **P1:** required before declaring the beta broadly stable;
- **Deferred:** deliberately outside the 1.0 beta scope.

## P0 — Scope and release identity

- [ ] **B10-001:** Approve and freeze the supported beta scope above.
- [ ] **B10-002:** Freeze rules and scenario data except for fixes supported by playtest evidence.
- [ ] **B10-003:** Set and display a beta version such as `1.0.0-beta.1` in the main menu, debrief, logs, saves, and playtest exports.
- [ ] **B10-004:** Add separate development and release build commands; the distributable build must not use `BuildOptions.Development`.
- [ ] **B10-005:** Label Online Preview as experimental and keep it unavailable in ordinary release flow unless explicitly enabled.
- [ ] **B10-006:** Produce a clean distributable archive containing the player, current rules PDF, quick-start instructions, licenses/attribution, and known issues.
- [ ] **B10-007:** Record the build commit, Unity version, content/schema versions, build time, and SHA-256 checksum with every beta package.

## P0 — Save, recovery, and data integrity

- [ ] **B10-010:** Replace direct save overwrite with an atomic temporary-write-and-replace operation.
- [ ] **B10-011:** Retain one last-known-good save backup and offer recovery when the primary save is unreadable.
- [ ] **B10-012:** Present clear, non-destructive UI for corrupted, incompatible, or newer-version saves.
- [ ] **B10-013:** Verify save/load after every major action family, reaction window, synchronized strike state, and completed scenario.
- [ ] **B10-014:** Verify settings and accessibility preferences persist after restart on a clean Windows profile.
- [ ] **B10-015:** Confirm quitting from every modal and handoff state cannot silently lose or expose hidden information.
- [ ] **B10-016:** Add build/version and hardware context to playtest exports and diagnostic logs.
- [ ] **B10-017:** Provide an in-game way to open or identify the save, playtest, and `Player.log` locations.

## P0 — New-player onboarding and rules truth

- [ ] **B10-020:** Add a concise first-run path covering camera control, active formation, Ready Time, selecting an order, previewing consequences, and ending an activation.
- [ ] **B10-021:** Ensure every disabled primary action explains why it is unavailable and what would make it legal.
- [ ] **B10-022:** Ensure Move, Search, Strike, Recover, Hold, Patrol/Screen, Support, Replenishment, Missions, cards, reactions, and synchronized strikes have discoverable in-game help.
- [ ] **B10-023:** Verify the player can explain why the highlighted formation acts next.
- [ ] **B10-024:** Verify Contact Location, Identity, Age, possible areas, stale information, and false Contacts have an understandable spatial meaning.
- [ ] **B10-025:** Verify calculations, costs, risks, and likely consequences are visible before commitment without leaking hidden state.
- [ ] **B10-026:** Regenerate the rules PDF from the frozen runtime catalogs and approved rulings.
- [ ] **B10-027:** Audit every in-game term, value, shortcut, and scenario condition against the packaged rules PDF.
- [ ] **B10-028:** Verify every playable Entropy and Command Response card is mechanically supported, documented, and covered by a deterministic test; otherwise remove it from the beta deck.

## P0 — Required human playtests

Use [`design/PLAYTEST_PROTOCOL.md`](design/PLAYTEST_PROTOCOL.md) and attach the exported telemetry and observer notes to each result.

- [ ] **B10-030:** A new player completes one full Meridian Veil Solo game without developer intervention.
- [ ] **B10-031:** Two players complete one full Meridian Veil Local Hotseat game with secure handoffs and no information leak.
- [ ] **B10-032:** Complete one full Luzon game focused on maneuver space, chokepoints, Search reach, scenario duration, and objective pressure.
- [ ] **B10-033:** Test selection of small formations, dense Contacts, coastal hexes, and overlapping markers in the 3D-first interface.
- [ ] **B10-034:** Conduct one blind rules teach in which the designer does not answer gameplay questions during play.
- [ ] **B10-035:** Record obvious choices, repeated lookups, inactive-player lulls, unfair outcomes, unused actions, dominant actions, and decision time after every session.
- [ ] **B10-036:** Confirm the defender regularly receives a meaningful, legal reaction decision.
- [ ] **B10-037:** Confirm Solo AI completes scenarios legally and provides credible baseline opposition to a human player.
- [ ] **B10-038:** Confirm no player waits through more than two opposing actions without a meaningful decision or useful planning opportunity.

## P0 — Windows display and input QA

Use [`design/UI_ACCESSIBILITY_QA.md`](design/UI_ACCESSIBILITY_QA.md) and record the build, display, GPU, Windows scale, input device, and result.

- [ ] **B10-040:** Exercise 1280×720, 1600×900, 1920×1080, and 2560×1080 through all screens and required modals.
- [ ] **B10-041:** Continuously resize the player and verify no required control clips, overlaps, disappears, or becomes unreachable.
- [ ] **B10-042:** Test Windows display scaling at 100%, 125%, 150%, and 200%.
- [ ] **B10-043:** Switch between windowed and native borderless fullscreen ten times without drift or lost settings.
- [ ] **B10-044:** Move between monitors with different DPI/resolution and repeat fullscreen transitions.
- [ ] **B10-045:** Complete the primary game flow with mouse only.
- [ ] **B10-046:** Complete the primary game flow with keyboard only, including every modal and Hotseat handoff.
- [ ] **B10-047:** Verify camera, map cursor, right-click formation orders, overlays, and edge-HUD toggle remain reliable at every supported resolution.
- [ ] **B10-048:** Verify controller focus, dead zones, submit/cancel, held-stick behavior, and modal focus return.

## P0 — Accessibility acceptance

- [ ] **B10-050:** Inspect all screens and modals at 100%, 125%, and 150% text scale.
- [ ] **B10-051:** Validate the complete palette under protanopia, deuteranopia, tritanopia, and achromatopsia simulation.
- [ ] **B10-052:** Ask at least one color-vision-deficient tester to distinguish sides, Contacts, eligibility, Entropy, damage, and active state.
- [ ] **B10-053:** Complete a Windows Narrator or NVDA traversal of menus, Field Manual, Settings, action controls, cards, reaction dialog, handoff, and results.
- [ ] **B10-054:** Confirm no required state is conveyed only by color, animation, or sound.
- [ ] **B10-055:** Verify reduced motion suppresses nonessential movement without hiding gameplay information.
- [ ] **B10-056:** Verify Master, Effects, and Music/Ambient levels affect only their intended audio and persist after restart.
- [ ] **B10-057:** Confirm hidden Solo AI actions produce no revealing audio cue, description, rendered formation, log entry, or tooltip.

## P0 — Performance, stability, and provenance

Use the budgets in [`design/3D_MVP_BUDGETS_AND_ACCEPTANCE.md`](design/3D_MVP_BUDGETS_AND_ACCEPTANCE.md).

- [ ] **B10-060:** Capture minimum-hardware performance at 1280×720; meet 30 fps at the 95th percentile without sustained stalls.
- [ ] **B10-061:** Capture target-hardware performance at 1920×1080; target 60 fps with no sustained period below 45 fps.
- [ ] **B10-062:** Measure startup, scenario load, save, load, map rebuild, and shutdown times.
- [ ] **B10-063:** Run at least three complete AI-vs-AI scenarios under profiling and check for memory growth, exceptions, deadlocks, or illegal decisions.
- [ ] **B10-064:** Verify triangle, material, draw-call, texture-memory, transient-VFX, and UI-time budgets with the curved ETOPO terrain and textured ocean enabled.
- [ ] **B10-065:** Run the unit, integration, presentation, and build-validation suites from a clean checkout; archive the logs with the release candidate.
- [ ] **B10-066:** Complete an asset ledger audit for every distributed texture, dataset, font, audio source, icon, rule image, and generated derivative.
- [ ] **B10-067:** Verify Natural Earth, NOAA ETOPO 2022, GEBCO, Unity, and all other required notices appear in the package and appropriate in-game credits.
- [ ] **B10-068:** Run a clean-machine launch test from the exact archive intended for distribution.

## P0 — Release-candidate sign-off

- [ ] **B10-070:** Triage every beta-candidate finding as P0, P1, accepted known issue, or deferred.
- [ ] **B10-071:** Resolve all open P0 crash, data-loss, information-leak, soft-lock, inaccessible-workflow, and rules-integrity defects.
- [ ] **B10-072:** Publish the accepted P1/known-issues list with workarounds where available.
- [ ] **B10-073:** Verify a complete Solo game and complete Hotseat game using the packaged release candidate rather than an Editor/development build.
- [ ] **B10-074:** Confirm the packaged executable, rules PDF, attribution, version, checksum, and known-issues document all describe the same release candidate.
- [ ] **B10-075:** Approve the 1.0 beta go/no-go review.

## P1 — Balance and usability during beta

Run paired comparisons by changing only one variable at a time.

- [ ] **B10-100:** Compare automatic Defend with player-selected reactions.
- [ ] **B10-101:** Compare the Age-2 targeting penalty on versus off.
- [ ] **B10-102:** Compare High Tempo always marking Friction versus conditional Friction.
- [ ] **B10-103:** Compare universal Entropy penalties plus cards versus cards replacing universal penalties.
- [ ] **B10-104:** Compare Endurance degradation after three versus four major Actions.
- [ ] **B10-105:** Compare two versus three Command Slots.
- [ ] **B10-106:** Compare player-chosen Contact improvement versus fixed Location-first improvement.
- [ ] **B10-107:** Compare exact-hex Contact labels with spatial uncertainty regions.
- [ ] **B10-108:** Test sequential Defense penalties for second and third synchronized attacks.
- [ ] **B10-109:** Review action frequency, activation delays, Contact lifetime, Search efficiency, combat/damage distribution, Command occupancy, Entropy duration, objective presence, and decision time.
- [ ] **B10-110:** Fix sustained dominant actions, non-decisions, excessive inactive-player downtime, or scenario outcomes that are insensitive to player choices.

## Deferred beyond the 1.0 beta

- [ ] Add named Patrol/Screen route-segment assignment after plotted routes are designed.
- [ ] Add further weather and sea-state movement effects only when playtest evidence supports them.
- [ ] Produce final commercial print files with bleed, safe areas, imposition, and print-vendor proofs.
- [ ] Expand beyond the current 24×20 geographic theater only after measured performance and maneuver-space evidence justify streaming or grid chunking.
- [ ] Enable public online play only after Local Hotseat privacy passes, the complete playable UI routes through side-scoped authority receipts, and two authenticated builds pass Relay latency, reconnect, host-migration, and adversarial information-leak testing.

## 1.0 beta exit criteria

The beta is ready to distribute when:

1. every **P0** checkbox is complete or explicitly accepted in the published known-issues list without involving crash, data loss, hidden-information leakage, rules corruption, or an inaccessible primary workflow;
2. blind Solo and Local Hotseat sessions complete without designer intervention;
3. the release build and packaged rules agree;
4. minimum/target hardware and display/accessibility evidence is attached;
5. the exact packaged release candidate passes the automated suites and clean-machine launch test.
