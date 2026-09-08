# Sea of Uncertainty

An operational naval wargame prototype about acting effectively inside incomplete, aging, and contested information.

## Play the current Unity slice

1. Open `SeaOfUncertainty` in Unity `6000.2.12f1`.
2. Open `Assets/Scenes/Prototype.unity`.
3. Press Play.
4. Choose **Begin Operation**, select **Meridian Veil**, review the briefing, and lock the fixed test deployment.
5. Use the secure handoff screen whenever control passes between sides.
6. The highlighted formation is Ready. Choose Move, Search, Strike, Recover, Hold, Patrol/Screen, Support, or Replenish.
7. For Move, choose a commitment and destination hex. For Search, choose any highlighted area hex. For Strike, choose an eligible Contact.
8. Use **Menu** during play to save, load, restart, adjust settings, or return to the main menu. A saved operation adds **Continue** to the main menu.

The scenario concludes at operational Time 16 with provisional scoring for objective control, carrier preservation, and enemy formations rendered Crippled or Destroyed.

During action selection, the 3D command map highlights legal destinations, Search areas, and eligible Strike Contacts. Heavy Salvo requires a final confirmation. Hover ratings and status elements for contextual rules help, and use **Inspect** on the after-action feed for the full event history.

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
- cross-side priority passing only when both sides are tied at the earliest Ready Time;
- movement commitment versus signature, friction, and future tempo;
- Contact Location, Identity, and Age;
- bounded search and combat dice;
- light/standard/heavy strike commitment;
- player-selected Defend, Evade, Counterattack, and Hold reactions;
- persistent Patrol/Screen areas, interception postures, and targeted Support assignments;
- typed logistics access and multi-part Replenishment service packages;
- individual Command Attention slots, structured Standing Missions, automatic triggers, Push Through, and HQ Recovery;
- entropy, cohesion, endurance, and damage;
- three Command Slots per side.

The implementation is data-oriented: game rules live in `Assets/Scripts/Core`, while the prototype presentation lives in `Assets/Scripts/Prototype`. This separation is intentional so numerical changes stay cheap.

The runtime presentation now uses Unity UI Toolkit. Shared component styling and design tokens live in `Assets/Resources/UI/SeaTheme.uss`; the old immediate-mode controller remains disabled as a development fallback. The retained-mode layout scales at smaller desktop resolutions, expands its map on ultrawide displays, and responds to live window-size changes.

The operation-mode screen supports **Solo vs AI** (player commands Blue) and **Local Hotseat**. The prototype AI controls Red through the same continuous Ready-Time scheduler and legal action methods as a human. Its decisions use only Red formations, Red-owned Contacts, public terrain, and the public objective; it receives no combat or initiative bonuses.

Keyboard controls:

- `Tab` and `Shift+Tab`: move through controls;
- `1`–`5`: Move, Search, Strike, Recover, and Hold;
- `G`: toggle the persistent map hex grid;
- `C`: focus the active formation;
- `O`: focus the operational objective;
- `V`: toggle optional edge scrolling;
- `F9` / `F10`: save and recall a command-camera view;
- arrow keys while the tactical map is focused: move the map cursor;
- `Enter` or `Space`: commit the highlighted map selection;
- `Escape`: close an overlay or open the operation menu.

3D map mouse controls:

- mouse wheel: bounded zoom;
- middle-button drag: pan;
- right-button drag: freely rotate and tilt the camera;
- left click: select a hex or eligible Contact;
- `Home`: reset the camera.

Full keyboard camera controls work anywhere on the operation screen when no modal is open:

- `W/A/S/D`: move across the operational area;
- `Q/E`: rotate left/right;
- `R/F`: tilt up/down;
- `Z/X` or keypad `+/-`: zoom in/out;
- hold `Shift`: accelerate keyboard camera movement.

The 3D theater uses multi-scale procedural surface normals, sun glint, shallow-water coloration, bathymetric contours, coastline foam, layered terrain relief, horizon haze, moving cloud shadows, and data-driven weather hooks. Map-space labels and distinct port/airfield geometry remain presentation-only and do not alter hex terrain or rules.

The command bar's **HEXES OFF / HEXES ON** control provides the same persistent grid toggle, and the preference is saved between sessions.

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
- Patrol/Screen and Support rulings: `design/PATROL_SUPPORT_RULES.md`
- Replenishment and logistics rulings: `design/REPLENISHMENT_RULES.md`
- Command Attention and Standing Mission rulings: `design/COMMAND_MISSIONS_RULES.md`
- Movement, terrain, stacking, and control rulings: `design/MOVEMENT_TERRAIN_CONTROL_RULES.md`
- Search and spatial uncertainty rulings and information audit: `design/SEARCH_SPATIAL_UNCERTAINTY_RULES.md`
- Playtest protocol: `design/PLAYTEST_PROTOCOL.md`
- 3D implementation constitution: `design/3D_DESIGN_CONSTITUTION.md`
