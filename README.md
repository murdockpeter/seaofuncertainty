# Sea of Uncertainty

An operational naval wargame prototype about acting effectively inside incomplete, aging, and contested information.

## Play the current Unity slice

1. Open `SeaOfUncertainty` in Unity `6000.2.12f1`.
2. Open `Assets/Scenes/Prototype.unity`.
3. Press Play.
4. Choose **Begin Operation**, select **Meridian Veil**, review the briefing, and lock the fixed test deployment.
5. Use the secure handoff screen whenever control passes between sides.
6. The highlighted formation is Ready. Choose Move, Search, Strike, Recover, Hold, Patrol/Screen, Support, Replenish, or declare a Synchronized Strike.
7. For Move, choose a commitment and destination hex. For Search, choose any highlighted area hex. For Strike, choose an eligible Contact and then one aim hex inside its possible area.
8. Use **Menu** during play to save, load, restart, adjust settings, or return to the main menu. A saved operation adds **Continue** to the main menu.

Meridian Veil concludes at operational Time 16 and scores five operational aims: control, transit, escort, denial, and carrier preservation or withdrawal. Operational points decide first; surviving combat capability, lower damage burden, and proximity break ties. Damage never awards points directly.

During action selection, the 3D command map highlights legal destinations, Search areas, and eligible Strike Contacts. Strike previews never expose hidden occupancy or defense; an empty, stale, or false aim reports only “no confirmed effect.” Heavy Salvo requires a final confirmation. Hover ratings and status elements for contextual rules help, and use **Inspect** on the after-action feed for the full event history.

The current baseline uses 20 nautical miles between adjacent hex centers and two hours per Ready-Time point. These physical-scale values are provisional until dedicated movement, sensor, and weapon-range playtests are complete.

Post-alpha simulation uses finite Light/Standard/Heavy magazines, salvo-dependent outer/area/point missile defense, printed EW/Cyber ratings, submarine deep/shallow state and ASW datum requirements, fixed and mobile logistics, authoritative weather severity, scenario-specific command architectures, and deterministic scheduled events. These values are externally authored and remain balance candidates.

Entropy uses complete 12-card Friction, Disruption, and Destruction decks. Universal source penalties stack with attached card effects. Recover discards one selected Friction or Disruption card; Replenishment handles damage and Destruction repair. Printed Entropy responses cost one Command Slot and must be used before the affected formation completes its next own Action.

Each side's 24-card Command Response deck starts with three cards, has a five-card hand limit, and draws after every three completed Major Actions. Synchronized Strikes reserve two to four formations at a future Strike Time, hold one Command Slot, and resolve a single defender Reaction followed by sequential Defense erosion.

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

Authoritative actions can also be represented as versioned, stable-ID commands independent of the UI. Replay documents store the scenario ID, initial seed, ordered commands, and before/after state digests so a fresh simulation can reproduce a session or identify the first divergence. The contract is documented in `design/AUTHORITATIVE_REPLAY_AND_PRINT.md`.

Online matches use Unity Multiplayer Services Sessions for lobby membership and reconnect, DTLS-encrypted Relay for Internet traversal, and Unity Transport directly for reliable, fragmented command/receipt payloads. The listen-server authority sends each commander only a side-scoped snapshot; signed replay checkpoints support interrupted-match recovery. Production activation still requires a linked Unity cloud project and two-client privacy, latency, reconnect, and host-migration validation. See `design/MULTIPLAYER_PRODUCTION_ARCHITECTURE.md`.

The online validation screen appears in the Unity Editor and development builds; a non-development build requires `-enableOnlinePreview`. **Direct IP** host/join is always available there without Unity Cloud over a configurable UDP port (LAN/VPN recommended; public hosts may need UDP port forwarding). **Unity Relay** optionally adds managed join codes, NAT traversal, DTLS encryption, IP concealment, and migration coordination. Remote Config key `multiplayer_enabled` controls admission to new Relay sessions without affecting Direct IP. Both paths exercise authenticated seats, readiness, host-only authority start, clean leave, and live RTT measurement; playable command routing and public activation remain gated on privacy and two-build QA.

Scenario metadata and formation definitions live in `Assets/Resources/Data/scenario-catalog.json`. Open **Sea of Uncertainty → Scenario Catalog Editor** to edit and validate that catalog in Unity; operational-area terrain remains in its validated area definition.

Use **Sea of Uncertainty → Generate Print-and-Play Proofs** to regenerate formation cards, counters and Contacts, operational tracks, both complete card decks, and scenario/player aids beneath `SeaOfUncertainty/generated/print-and-play`. These HTML files are content proofs; final bleed, safe-area, licensing, and imposition work remains open.

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

The 3D theater uses a physical Earth-radius spherical cap at the scenario's nautical-mile scale, plus multi-scale procedural surface normals, sun glint, depth-driven ocean coloration, subtle current bands, coastline foam, horizon haze, moving cloud shadows, and data-driven weather hooks. Geographic theaters can render tiled measured elevation along the globe normal; the Luzon theater uses a baked two-arc-minute subset of NOAA NCEI ETOPO 2022 for both seafloor color zones and land relief, with 4x land vertical exaggeration for legibility at the 20-NM hex scale. Curved picking, overlays, formations, and movement remain aligned to the authoritative hex grid; map-space labels and distinct port/airfield geometry remain presentation-only and do not alter hex terrain or rules.

The command bar's **HEXES OFF / HEXES ON** control provides the same persistent grid toggle. The overlay uses traditional flat-top hexes at the authoritative 20-NM scale, and the visibility preference is saved between sessions.

Gamepad and joystick UI navigation uses dedicated axes, configurable dead-zone filtering, and one-step focus movement after the stick recenters.

## Visual direction

The public-prototype presentation is code-owned: procedural formation models, materials, markings, wakes, environmental textures, effects, and labeled UI glyphs keep gameplay layers dynamic and accessible. The palette uses midnight navy, sonar cyan, command amber, disruption violet, and damage red. Asset approval and provenance are recorded in `design/PRODUCTION_ART_DIRECTION.md` and `design/3D_ASSET_LEDGER.md`.

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
- Command Response and Synchronized Strike rulings: `design/COMMAND_RESPONSE_AND_SYNCHRONIZED_STRIKE_RULES.md`
- Movement, terrain, stacking, and control rulings: `design/MOVEMENT_TERRAIN_CONTROL_RULES.md`
- Search and spatial uncertainty rulings and information audit: `design/SEARCH_SPATIAL_UNCERTAINTY_RULES.md`
- Content/simulation role decisions and connected-theater contract: `design/CONTENT_SIMULATION_FOUNDATION.md`
- Authoritative command/replay and print generator contract: `design/AUTHORITATIVE_REPLAY_AND_PRINT.md`
- Automated unit, integration, presentation, and build-validation suites: `design/TEST_SUITE_ARCHITECTURE.md`
- Server-authoritative multiplayer Phase 1 and loopback fault model: `design/MULTIPLAYER_PHASE1.md`
- Production multiplayer Sessions, Relay, transport, lobby, and recovery architecture: `design/MULTIPLAYER_PRODUCTION_ARCHITECTURE.md`
- Playtest protocol: `design/PLAYTEST_PROTOCOL.md`
- 3D implementation constitution: `design/3D_DESIGN_CONSTITUTION.md`
