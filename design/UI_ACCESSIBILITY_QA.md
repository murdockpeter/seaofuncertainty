# UI, Accessibility, and Audio QA

Use a clean Windows build and record the build commit, display model, GPU, Windows scaling, input device, and result for every run. A checkbox requires direct observation; automated smoke tests are supporting evidence only.

## Display matrix

- [ ] Resize continuously from 1280×720 through 1920×1080. Check the main menu, briefing, operation screen, Settings, Field Manual, reaction handoff, card targeting, and results.
- [ ] Repeat at Windows scaling 100%, 125%, 150%, and 200%.
- [ ] Switch between windowed and native borderless fullscreen ten times without resolution drift or clipped controls.
- [ ] Move the window between monitors with different resolution and DPI, then repeat fullscreen transitions on each display.
- [ ] Complete all primary workflows with a mouse only.

## Keyboard and controller

- [ ] Complete menu, scenario selection, briefing, setup, one full activation, reaction selection, every modal, and debrief using only Tab/Shift+Tab, arrow keys, Enter/Space, and Escape.
- [ ] Confirm arrow keys move the focused map cursor and Enter/Space commits the highlighted legal target.
- [ ] With a controller, confirm each stick deflection advances exactly one focus target and requires returning to center before another step.
- [ ] Test controller dead-zone settings at 30%, 55%, and 90%, including a controller with known stick drift.
- [ ] Confirm controller A submits and B cancels without double activation.
- [ ] Confirm a modal returns focus to its invoking control when closed.

## Text, color, and assistive technology

- [ ] At 100%, 125%, and 150% text size, inspect all screens and every modal for clipping, overlap, lost controls, and unreadable card text.
- [ ] Run the complete palette through protanopia, deuteranopia, tritanopia, and achromatopsia simulators; verify that shape, border, text, and status labels preserve meaning.
- [ ] Ask at least one color-vision-deficient tester to distinguish sides, Contacts, eligibility, entropy sources, damage, and active formation state.
- [ ] With Windows Narrator or NVDA, traverse the main menu, Field Manual, Settings, reaction dialog, and action controls. Confirm dialogs, named actions, labeled fields, and the audio-description status are announced in a useful order.

## Audio

- [ ] Verify Master affects all sound, Effects affects each of the eight action cues, and Music/Ambient affects only the open-sea bed.
- [ ] Confirm each visible action cue has the matching on-screen text description.
- [ ] Confirm hidden Solo AI actions produce neither a revealing cue nor description.
- [ ] Confirm settings persist after restarting the player.

## Acceptance

Pass when there is no inaccessible primary workflow, no state conveyed only by color or sound, no repeat controller movement from a held stick, no clipped required control in the matrix, and no hidden-information leak.
