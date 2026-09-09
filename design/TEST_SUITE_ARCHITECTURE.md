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

## Build-validation suite

`PrototypeSceneBuilder.RunBuildValidationSuite` performs the Windows build, fails on an unsuccessful build report, and verifies the executable, player-data directory, global managers, and enabled authoritative scene.

## Compatibility entry point

`PrototypeSceneBuilder.RunCoreSmokeTests` remains available for existing automation. It is now a small orchestrator that runs the unit, integration, and presentation suites. Build validation stays independent because it is slower and mutates build artifacts.
