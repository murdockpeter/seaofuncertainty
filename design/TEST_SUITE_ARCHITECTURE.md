# Automated test-suite architecture

The Unity editor checks are divided by failure domain so a narrow change does not require diagnosing one monolithic result.

## Unit suite

`PrototypeSceneBuilder.RunUnitTestSuite` covers pure rules boundaries, hex math, Entropy and Command Response catalog shape, physical deck behavior, save-safe Entropy queues, capability modifiers, and Ready-Time tie selection.

## Integration suite

`PrototypeSceneBuilder.RunIntegrationTestSuite` covers scenario data and migration, operational geography, 3D map integration, gameplay actions, information discipline, save/load, full-scenario AI completion, scoring, and telemetry serialization.

## Presentation suite

`PrototypeSceneBuilder.RunPresentationTestSuite` composes the accessibility contract checks with `UiInteractionTests.Run`. The interaction fixture instantiates the production UI Toolkit controller and invokes the same callbacks carried by its visible buttons. It covers:

- Field Manual and Settings modal navigation;
- settings-field application into persistent backend state;
- native-fullscreen/windowed transition policy and control wiring;
- targeted C-12 Command Response selection and resolution;
- information-safe Strike aim choices and cancellation;
- secure Hotseat handoff identity concealment and assumption of command.

The fixture restores the user's original persistent settings before destroying its temporary host.

Presentation also includes `OperationalAudioTests.Run`, available independently as `tools/run-tests.ps1 -Suite Audio`. It checks synthesized cue headroom, continuity, variation, stereo ambience, hidden-information privacy and isolated random state, and exports audition WAVs. See [audio direction](AUDIO_DIRECTION.md).

## Build-validation suite

`PrototypeSceneBuilder.RunBuildValidationSuite` performs the Windows build, fails on an unsuccessful build report, and verifies the executable, player-data directory, global managers, and enabled authoritative scene.

## Compatibility entry point

`PrototypeSceneBuilder.RunCoreSmokeTests` remains available for existing automation. It is now a small orchestrator that runs the unit, integration, and presentation suites. Build validation stays independent because it is slower and mutates build artifacts.


## Continuation and production UI evidence

`tools/run-tests.ps1 -Suite Continuation` runs save/replay/reaction continuation and Narrows Shield checks; Regression also includes it. It compares seeded full games with repeated save/load against uninterrupted execution, rather than relying only on an immediate loaded-state digest.

After a development Windows build, `tools/capture-acceptance.ps1 -Visible` runs the production screens and emits PNGs, player logs, telemetry and a capture report under `SeaOfUncertainty/TestResults/acceptance-2026-10-19/final`. This checks windowed display sizes, text scales, a fullscreen capture, and automated sessions. It does not certify human usability, real mouse/controller input, Windows DPI combinations, sustained frame-rate budgets or balance. Preserve those checks as open until observed.
