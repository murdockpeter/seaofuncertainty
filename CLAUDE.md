# Sea of Uncertainty

Operational naval wargame prototype. Unity project lives in `SeaOfUncertainty/` (Unity **6000.2.12f1**, single scene `Assets/Scenes/Prototype.unity`). The scene, UI, and 3D theater are built in code; there are almost no authored prefabs.

## Layout and layering

Three assemblies, enforced by `.asmdef` files:

| Assembly | Folder | May reference |
|---|---|---|
| `SeaOfUncertainty.Core` | `Assets/Scripts/Core` | UnityEngine only (no Prototype, no UnityEditor) |
| `SeaOfUncertainty.Prototype` | `Assets/Scripts/Prototype` | Core, UnityEngine, UI Toolkit, Unity Services/Transport |
| `SeaOfUncertainty.Editor` | `Assets/Editor` | Core, Prototype, UnityEditor (editor-only) |

- **Core** is the rules engine. `PrototypeGame` owns all game state; actions are `bool Verb(..., out string message)` and every action funnels through `AuthorizeAction` → `CompleteAction` → `AdvanceToNextFormation` (continuous Ready Time, no turns). Keep it deterministic: use the game's seeded `System.Random`, never `UnityEngine.Random`, wall-clock time, or unordered iteration in rules.
- **Prototype** is presentation and platform adapters. UI never mutates rules state except through `PrototypeGame` methods (or `AuthoritativeCommandProcessor`).
- Hidden information is a rule: each side sees only its own formations and Contacts. Never expose opposing state in UI, logs, previews, AI inputs, or network snapshots.
- Card IDs: use the constants in `EntropyCardIds` / `CommandResponseIds` (Core), not string literals like `"D-04"`.

## Data and saves

- Scenarios: `Assets/Resources/Data/scenario-catalog.json` (edit via **Sea of Uncertainty → Scenario Catalog Editor**).
- Save format is versioned (`PrototypeGame.SaveData.Version`); changing serialized state requires bumping the version and handling migration (`OperationalDataMigration`). All persisted/networked types must stay `JsonUtility`-serializable (public fields, `[Serializable]`, no nullable/dictionary fields).

## Design docs are the spec

Rules rulings live in `design/*.md`; the backlog is `TODO.md`; original rules in `rules/`. When behavior and docs disagree, check the relevant design doc before changing either, and update the doc alongside rule changes.

Keep `TODO.md` as the only active work queue. Historical TODO lists and QA checklists live under `design/archive/`; promote a specific archived item into the root TODO when it is scheduled rather than creating another competing backlog.

## Tests

Close the Unity Editor, then from the repo root:

```powershell
.\tools\run-tests.ps1            # all suites except the slow Windows build
.\tools\run-tests.ps1 -List      # suite names
.\tools\run-tests.ps1 -Suite Unit,Integration
```

Logs go to `SeaOfUncertainty/TestResults/` (git-ignored). Suites are static `Run*` methods in `Assets/Editor` using a local `Assert(condition, label)`; failures are collected and reported together at the end of each suite. Add new checks to the suite matching the failure domain (see `design/TEST_SUITE_ARCHITECTURE.md`). Some presentation checks read source files as text (e.g. `SeaUIToolkitController.cs`), so renaming the identifiers they look for breaks tests.

## Conventions

- Match the existing style: block-style `namespace SeaOfUncertainty.X { }`, `sealed` classes, expression-bodied helpers, long descriptive assertion labels.
- Do not commit generated logs or verification screenshots; put screenshots under `SeaOfUncertainty/TestResults/` too.
