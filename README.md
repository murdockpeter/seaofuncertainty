# Sea of Uncertainty

An operational naval wargame prototype about acting effectively inside incomplete, aging, and contested information.

## Play the current Unity slice

1. Open `SeaOfUncertainty` in Unity `6000.2.12f1`.
2. Open `Assets/Scenes/Prototype.unity`.
3. Press Play.
4. Choose **Begin Operation**, select **Meridian Veil**, review the briefing, and lock the fixed test deployment.
5. Use the secure handoff screen whenever control passes between sides.
6. The highlighted formation is Ready. Choose Move, Search, Strike, Recover, or Hold.
7. For Move, choose a commitment and then a destination hex. For Search or Strike, choose a commitment and then an enemy Contact.
8. Use **Menu** during play to save, load, restart, adjust settings, or return to the main menu. A saved operation adds **Continue** to the main menu.

The scenario concludes at operational Time 16 with provisional scoring for objective control, carrier preservation, and enemy formations rendered Crippled or Destroyed.

During action selection, the 3D command map highlights legal destinations and eligible Contacts. Hover an enemy Contact after choosing Search or Strike to inspect the complete calculation and outcome probabilities. Heavy Salvo requires a final confirmation. Hover ratings and status elements for contextual rules help, and use **Inspect** on the after-action feed for the full event history.

The current baseline uses 20 nautical miles between adjacent hex centers and two hours per Ready-Time point. These physical-scale values are provisional until dedicated movement, sensor, and weapon-range playtests are complete.

## Playtest data

The game records structured playtest telemetry throughout an operation. Use **Menu → Export Playtest Data** for a partial session, or finish at Time 16 for an automatic export. The results screen includes a structured debrief form.

Each export contains:

- `session.json` with the complete session and feedback;
- `events.csv` for spreadsheet analysis;
- `feedback.txt` for quick reading.

On Windows, exports are stored under `%USERPROFILE%\AppData\LocalLow\Sea of Uncertainty Design Lab\Sea of Uncertainty\Playtests\<session-id>`. The exact path is displayed after export and on the results screen. Saved operations preserve their active telemetry session.

The prototype is deliberately pass-and-play for now. Each side sees only its own formations and its current Contacts. Multiplayer is out of scope until the rules loop is stable.

## What this slice tests

- continuous Ready Time instead of alternating turns;
- movement commitment versus signature, friction, and future tempo;
- Contact Location, Identity, and Age;
- bounded search and combat dice;
- light/standard/heavy strike commitment;
- reaction (currently defaults to Defend);
- entropy, cohesion, endurance, and damage;
- three Command Slots per side.

The implementation is data-oriented: game rules live in `Assets/Scripts/Core`, while the prototype presentation lives in `Assets/Scripts/Prototype`. This separation is intentional so numerical changes stay cheap.

The runtime presentation now uses Unity UI Toolkit. Shared component styling and design tokens live in `Assets/Resources/UI/SeaTheme.uss`; the old immediate-mode controller remains disabled as a development fallback. The retained-mode layout scales at smaller desktop resolutions, expands its map on ultrawide displays, and responds to live window-size changes.

Keyboard controls:

- `Tab` and `Shift+Tab`: move through controls;
- `1`–`5`: Move, Search, Strike, Recover, and Hold;
- arrow keys while the tactical map is focused: move the map cursor;
- `Enter` or `Space`: commit the highlighted map selection;
- `Escape`: close an overlay or open the operation menu.

3D map mouse controls:

- mouse wheel: bounded zoom;
- middle-button drag: pan;
- right-button drag: rotate in 30-degree steps;
- left click: select a hex or eligible Contact;
- `Home` while the map is focused: reset the camera.

Gamepad and joystick polling is intentionally disabled while robust dead-zone and one-step focus handling are developed.

## Visual direction

The first generated project asset is `Assets/Resources/Art/tactical-archipelago-v1.png`. It contains no UI, hexes, text, or pieces, allowing every gameplay layer to stay dynamic and accessible. The palette uses midnight navy, sonar cyan, command amber, disruption violet, and damage red.

Generated art is provisional and should be tracked in an asset ledger before public release. Later production art can replace it without changing the game model.

## Design documents

- Primary development backlog: `TODO.md`
- Approved 20 nm prototype rulings: `design/20NM_SCALE_RULES.md`
- 3D MVP budgets and acceptance: `design/3D_MVP_BUDGETS_AND_ACCEPTANCE.md`
- 3D MVP go/no-go review: `design/3D_GO_NO_GO_REVIEW.md`

Regenerate the packaged Luzon coastline from the existing local campaign polygon library with:

```powershell
node tools/build-luzon-coastline.cjs
```

The generated Unity resource is `Assets/Resources/Geography/luzon-strait-coastline.json`. The geographic mesh is presentation-only; authoritative movement and terrain remain on the 20 nm axial grid.
- Original rules archive: `rules/SeaOfUncertainty/index.html`
- Rules deep dive and implementation decisions: `design/RULES_DEEP_DIVE.md`
- Playtest protocol: `design/PLAYTEST_PROTOCOL.md`
- 3D implementation constitution: `design/3D_DESIGN_CONSTITUTION.md`
