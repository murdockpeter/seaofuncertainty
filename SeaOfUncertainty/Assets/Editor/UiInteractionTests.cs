using System;
using System.Linq;
using SeaOfUncertainty.Core;
using SeaOfUncertainty.Prototype;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SeaOfUncertainty.Editor
{
    public static class UiInteractionTests
    {
        [MenuItem("Sea of Uncertainty/Tests/Run UI Interaction Suite")]
        public static void Run()
        {
            bool oldRunInBackground = Application.runInBackground;
            var host = new GameObject("UI Interaction Test Host");
            SeaPrototypeController backend = host.AddComponent<SeaPrototypeController>();
            SeaUIToolkitController ui = host.AddComponent<SeaUIToolkitController>();
            backend.EditorEnsureInitializedForTests();
            ui.EditorEnsureInitializedForTests();

            bool oldAge = backend.AgeTwoPenalty;
            float oldMaster = backend.MasterVolume, oldEffects = backend.EffectsVolume, oldAmbient = backend.AmbientVolume, oldText = backend.TextScale, oldDeadZone = backend.ControllerDeadZone;
            bool oldDescriptions = backend.AudioDescriptions, oldContrast = backend.HighContrast, oldMotion = backend.ReducedMotion, oldGrid = backend.HexGridVisible, oldController = backend.ControllerNavigation;
            try
            {
                Assert(ui.EditorRoot != null && ui.EditorView == "Main", "UI initializes on the main menu");

                Assert(ui.EditorInvokeButton("FIELD MANUAL") && ui.EditorOverlayOpen && ui.EditorRoot.Q<VisualElement>("dialog-fieldmanual") != null, "Field Manual button opens its named modal");
                Assert(ui.EditorInvokeButton("CLOSE") && !ui.EditorOverlayOpen, "Modal Close button returns to the underlying screen");

                Assert(ui.EditorInvokeButton("SETTINGS") && ui.EditorOverlayOpen, "Settings button opens the Settings modal");
                Toggle contrast = ui.EditorToggle("High-contrast side colors");
                Toggle motion = ui.EditorToggle("Reduced motion");
                Toggle grid = ui.EditorToggle("Show hex grid on the map");
                Toggle descriptions = ui.EditorToggle("Show text descriptions for audio cues");
                Slider master = ui.EditorSlider("Master volume");
                Slider effects = ui.EditorSlider("Effects volume");
                Slider ambience = ui.EditorSlider("Music / ambient volume");
                Slider deadZone = ui.EditorSlider("Controller stick dead zone");
                DropdownField textScale = ui.EditorDropdown("Text size");
                Assert(contrast != null && motion != null && grid != null && descriptions != null && master != null && effects != null && ambience != null && deadZone != null && textScale != null, "Settings modal exposes every tested field");
                contrast.value = true; motion.value = true; grid.value = true; descriptions.value = false;
                master.value = 61f; effects.value = 42f; ambience.value = 23f; deadZone.value = 68f; textScale.index = 2;
                Assert(ui.EditorInvokeButton("APPLY") && !ui.EditorOverlayOpen, "Apply commits settings and closes the modal");
                Assert(backend.HighContrast && backend.ReducedMotion && backend.HexGridVisible && !backend.AudioDescriptions && Mathf.Approximately(backend.MasterVolume, .61f) && Mathf.Approximately(backend.EffectsVolume, .42f) && Mathf.Approximately(backend.AmbientVolume, .23f) && Mathf.Approximately(backend.TextScale, 1.5f) && Mathf.Approximately(backend.ControllerDeadZone, .68f), "Settings interaction reaches persisted backend state");

                ui.EditorShowSettings();
                Button fullscreen = ui.EditorRoot.Query<Button>().ToList().FirstOrDefault(button => button.text.StartsWith("SWITCH TO ", StringComparison.Ordinal));
                Assert(fullscreen?.userData is Action && SeaUIToolkitController.EditorRequestedFullscreenMode(false) == FullScreenMode.FullScreenWindow && SeaUIToolkitController.EditorRequestedFullscreenMode(true) == FullScreenMode.Windowed, "Fullscreen control is wired to the native/windowed transition policy");
                Assert(ui.EditorInvokeButton("CANCEL") && !ui.EditorOverlayOpen, "Settings Cancel closes without another commit");

                backend.ToolkitNewScenario("meridian-veil");
                backend.ToolkitSetOperationMode(OperationMode.LocalHotseat);
                ui.EditorShowGame();
                Assert(!ui.EditorPersistentCardHandVisible, "The 3D command map no longer loses permanent screen space to the card hand");
                Assert(!ui.EditorEdgeHudVisible && ui.EditorRoot.Q<VisualElement>(className: "hud-hidden") != null, "The operation opens as a primarily 3D surface with the peripheral HUD hidden");
                ui.EditorToggleEdgeHud();
                Assert(ui.EditorEdgeHudVisible && ui.EditorRoot.Q<VisualElement>(className: "hud-hidden") == null, "The complete edge HUD can be restored as one optional layer");
                ui.EditorToggleEdgeHud();
                ui.EditorShowFormationOrders();
                bool continuation = ui.EditorRoot.Q<VisualElement>("dialog-activeformationorders") != null;
                Assert(continuation && ui.EditorRoot.Q<ScrollView>(className: "context-scroll") != null && ui.EditorRoot.Q<Button>(className: "context-close") != null, "The active Formation opens a bounded, scrollable contextual order palette over the 3D map");
                Assert(ui.EditorInvokeButton("⇢  MOVE ORDERS  ›"), "The polished command palette preserves primary order navigation");
                Button normalMove = ui.EditorRoot.Query<Button>().ToList().FirstOrDefault(button => button.text.StartsWith("NORMAL  •", StringComparison.Ordinal));
                Assert(normalMove?.userData is Action, "The contextual Move submenu exposes movement posture choices");
                ((Action)normalMove.userData)();
                Assert(ui.EditorActionMode == "Move" && !ui.EditorOverlayOpen, "A contextual order returns directly to 3D destination selection");
                Side activeSide = backend.Game.Active.Side;
                CommandResponseDeckState deck = backend.Game.CommandResponseDecks.First(item => item.Side == activeSide);
                deck.Hand.Clear(); deck.DrawPile.Remove("C-12"); deck.DiscardPile.Remove("C-12"); deck.Hand.Add("C-12");
                ContactState contact = backend.Game.Contacts.First(item => item.Owner == activeSide && !item.IsLost);
                contact.Age = 2;
                ui.EditorShowResponseHand();
                Assert(ui.EditorRoot.Q<VisualElement>("dialog-bluecommandtable") != null || ui.EditorRoot.Q<VisualElement>("dialog-redcommandtable") != null, "The card-table overlay opens above the command map");
                Assert(ui.EditorRoot.Query<VisualElement>(className: "physical-card").ToList().Count == 1, "Each held card receives a physical card face in the overlay");
                Assert(ui.EditorInvokeButton("SELECT TARGET & PLAY") && ui.EditorOverlayOpen && ui.EditorRoot.Query<Button>().ToList().Any(button => button.text.Contains(contact.LastKnownPosition.ToString())), "Response card targeting advances from hand to owned-Contact choices");
                Button contactChoice = ui.EditorRoot.Query<Button>().ToList().First(button => button.text.Contains(contact.LastKnownPosition.ToString()));
                Assert(contactChoice.userData is Action, "Contact target choice retains its production callback");
                ((Action)contactChoice.userData)();
                Assert(contact.Age == 0 && !deck.Hand.Contains("C-12") && deck.DiscardPile.Contains("C-12") && !ui.EditorOverlayOpen, "Targeted response resolves through the UI and returns to play");

                contact.Age = 0;
                contact.Location = LocationQuality.High;
                contact.LastKnownPosition = backend.Game.Active.Position;
                ui.EditorShowStrikeAim(contact);
                Assert(ui.EditorOverlayOpen && ui.EditorRoot.Q<VisualElement>("dialog-selectstrikeaimhex") != null && ui.EditorRoot.Query<Button>().ToList().Any(button => button.text.StartsWith("HEX ", StringComparison.Ordinal)), "Strike targeting modal exposes information-safe aim choices");
                Assert(ui.EditorInvokeButton("CANCEL") && !ui.EditorOverlayOpen, "Strike targeting can be cancelled without committing");

                ui.EditorShowHandoff();
                string visibleText = string.Join("\n", ui.EditorRoot.Query<Label>().ToList().Select(label => label.text));
                Assert(ui.EditorView == "Handoff" && backend.Game.Formations.Where(item => item.Side != backend.Game.Active.Side).All(item => !visibleText.Contains(item.Name)), "Hotseat handoff conceals the non-active side's formation identities");
                Assert(ui.EditorInvokeButton("ASSUME COMMAND") && ui.EditorView == "Game", "Handoff assumption returns to the game screen");

                Debug.Log("Sea of Uncertainty UI interaction tests passed: immersive HUD toggle, card targeting, handoff privacy, settings, fullscreen policy, and modal navigation.");
            }
            finally
            {
                backend.ToolkitSetSettings(oldAge, oldMaster, oldEffects, oldAmbient, oldDescriptions, oldContrast, oldMotion, oldGrid, oldText, oldController, oldDeadZone);
                Application.runInBackground = oldRunInBackground;
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("UI interaction test failed: " + message);
        }
    }
}
