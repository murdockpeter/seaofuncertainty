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
                Assert(ui.EditorRoot.Q<VisualElement>(className: "command-status") != null && ui.EditorRoot.Q<VisualElement>(className: "command-dock") == null
                    && ui.EditorRoot.Q<VisualElement>(className: "dossier") == null && ui.EditorRoot.Q<VisualElement>(className: "timeline") == null,
                    "The operation has compact command status without permanent sidebars or duplicate action controls");
                TestMapCommands(ui, backend);
                TestMarkerCards(ui, backend);
                TestBallisticOrders(ui, backend);
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
                deck.Hand.Clear(); deck.DrawPile.Remove(CommandResponseIds.UpdateThePlot); deck.DiscardPile.Remove(CommandResponseIds.UpdateThePlot); deck.Hand.Add(CommandResponseIds.UpdateThePlot);
                ContactState contact = backend.Game.Contacts.First(item => item.Owner == activeSide && !item.IsLost);
                contact.Age = 2;
                ui.EditorShowResponseHand();
                Assert(ui.EditorRoot.Q<VisualElement>("dialog-bluecommandtable") != null || ui.EditorRoot.Q<VisualElement>("dialog-redcommandtable") != null, "The card-table overlay opens above the command map");
                Assert(ui.EditorRoot.Query<VisualElement>(className: "physical-card").ToList().Count == 1, "Each held card receives a physical card face in the overlay");
                Assert(ui.EditorInvokeButton("SELECT TARGET & PLAY") && ui.EditorOverlayOpen && ui.EditorRoot.Query<Button>().ToList().Any(button => button.text.Contains(contact.LastKnownPosition.ToString())), "Response card targeting advances from hand to owned-Contact choices");
                Button contactChoice = ui.EditorRoot.Query<Button>().ToList().First(button => button.text.Contains(contact.LastKnownPosition.ToString()));
                Assert(contactChoice.userData is Action, "Contact target choice retains its production callback");
                ((Action)contactChoice.userData)();
                Assert(contact.Age == 0 && !deck.Hand.Contains(CommandResponseIds.UpdateThePlot) && deck.DiscardPile.Contains(CommandResponseIds.UpdateThePlot) && !ui.EditorOverlayOpen, "Targeted response resolves through the UI and returns to play");

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

                TestChecks.ThrowIfAny("UI interaction");
                Debug.Log("Sea of Uncertainty UI interaction tests passed: compact command status, contextual commands, inspector privacy, card targeting, handoff privacy, settings, fullscreen policy, and modal navigation.");
            }
            finally
            {
                backend.ToolkitSetSettings(oldAge, oldMaster, oldEffects, oldAmbient, oldDescriptions, oldContrast, oldMotion, oldGrid, oldText, oldController, oldDeadZone);
                Application.runInBackground = oldRunInBackground;
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static string VisibleText(SeaUIToolkitController ui)
            => string.Join("\n", ui.EditorRoot.Query<Label>().ToList().Select(label => label.text).Concat(ui.EditorRoot.Query<Button>().ToList().Select(button => button.text)));

        private static void TestBallisticOrders(SeaUIToolkitController ui, SeaPrototypeController backend)
        {
            backend.ToolkitNewScenario("meridian-veil");
            FormationState actor = backend.Game.Active;
            actor.BallisticWeapons = new BallisticWeaponProfile { Range = 5, AttackModifier = 1, Capacity = 2, Ammunition = 2, TargetDomains = new System.Collections.Generic.List<ContactDomain> { ContactDomain.Surface } };
            FormationState target = backend.Game.Formations.First(item => item.Side != actor.Side && item.Kind == FormationKind.SurfaceGroup);
            target.Position = new HexCoord(actor.Position.Q + 1, actor.Position.R);
            target.HasReacted = true;
            ContactDomain ownedDomain = actor.Kind == FormationKind.Submarine ? ContactDomain.Subsurface : actor.Kind == FormationKind.AirGroup ? ContactDomain.Air : ContactDomain.Surface;
            actor.BallisticDefense = new BallisticDefenseProfile { InterceptionRating = 3, Interceptors = 1, Capacity = 1, CoverageRadius = 1,
                ProtectedDomains = new System.Collections.Generic.List<ContactDomain> { ownedDomain }, AllowDeepSubmarines = ownedDomain == ContactDomain.Subsurface,
                AllowShallowSubmarines = ownedDomain == ContactDomain.Subsurface, CanInterceptWhileDeep = ownedDomain == ContactDomain.Subsurface };
            target.BallisticDefense = new BallisticDefenseProfile { InterceptionRating = 5, Interceptors = 2, Capacity = 2, CoverageRadius = 1, ProtectedDomains = new System.Collections.Generic.List<ContactDomain> { ContactDomain.Surface } };
            ContactState report = backend.Game.ContactFor(actor.Side, target.Id);
            report.LastKnownPosition = target.Position; report.Location = LocationQuality.High; report.Identity = IdentityQuality.General; report.Domain = ContactDomain.Surface; report.Age = 0;
            ui.EditorShowGame();
            ui.EditorShowInspector();
            Assert(VisibleText(ui).Contains(actor.BallisticDefense.Summary) && VisibleText(ui).Contains("AUTOMATIC PROTECTION: " + actor.Name)
                && !VisibleText(ui).Contains(target.BallisticDefense.Summary), "Owned inspector shows interceptor stocks and friendly coverage without enemy defense details");
            Assert(ui.EditorInvokeButton("SHOW DEFENSE COVERAGE") && !ui.EditorOverlayOpen
                && VisibleText(ui).Contains("BALLISTIC COVERAGE: " + actor.Name), "Inspector offers a keyboard-accessible pinned friendly coverage overlay");
            ui.EditorShowInspector();
            Assert(ui.EditorInvokeButton("HIDE DEFENSE COVERAGE") && !VisibleText(ui).Contains("BALLISTIC COVERAGE: " + actor.Name), "Pinned coverage can be dismissed without issuing an order");
            Assert(MapMarkerDetails.Formation(backend.Game, actor).Description.Contains(actor.BallisticDefense.Summary)
                && !MapMarkerDetails.Contact(backend.Game, report, report.LastKnownPosition).Description.Contains("Ballistic defense"), "Hover details expose defense capability only for owned formations");
            ui.EditorShowFormationOrders();
            Button orders = ui.EditorRoot.Query<Button>().ToList().First(item => item.text.Contains("STRIKE ORDERS"));
            Assert(ui.EditorInvokeButton(orders.text), "Strike palette opens for an equipped formation");
            Button ballistic = ui.EditorRoot.Query<Button>().ToList().First(item => item.text.StartsWith("BALLISTIC STANDARD", StringComparison.Ordinal));
            Assert(ui.EditorInvokeButton(ballistic.text) && ui.EditorActionMode == "Strike", "Ballistic order selects weapon delivery separately from salvo commitment");
            Assert(backend.ToolkitFriendlyLaunchCue(actor.Side) == null && ui.EditorRoot.Q<TacticalMapElement>().EditorEffectCount == 0, "Selecting a strike order does not fabricate a committed launch cue");
            int conventional = actor.Weapons.Standard;
            ui.EditorShowStrikeAim(report);
            Button aim = ui.EditorRoot.Query<Button>().ToList().First(item => item.text.StartsWith("HEX ", StringComparison.Ordinal));
            Assert(ui.EditorInvokeButton(aim.text) && actor.BallisticWeapons.Ammunition == 1 && actor.Weapons.Standard == conventional, "Production aim callback commits a ballistic package and preserves conventional ammunition");
            Assert(target.BallisticDefense.Interceptors == 1 && actor.BallisticDefense.Interceptors == 1, "Production ballistic attack spends one eligible enemy interception package and leaves friendly defense unchanged");
            Assert(backend.ToolkitFriendlyInterceptionCue(actor.Side) == null && backend.ToolkitFriendlyInterceptionCue(target.Side)?.FormationId == target.Id
                && MapMarkerDetails.LatestFriendlyInterception(backend.Game, actor.Side) == null
                && MapMarkerDetails.LatestFriendlyInterception(backend.Game, target.Side).Contains(target.Name), "Interception cue and receipt belong only to the defender's side");
            Assert(backend.ToolkitFriendlyLaunchCue(actor.Side)?.FormationId == actor.Id && backend.ToolkitFriendlyLaunchCue(target.Side) == null, "Committed launch produces a cue only for its owner's side");
            actor.Kind = FormationKind.BallisticMissileBattery;
            actor.Position = backend.Game.Area.Terrain.First(item => item.Terrain == OperationalTerrain.Land).Hex;
            PrototypeGame.SaveData renderState = backend.Game.CaptureState();
            renderState.ActiveFormationId = actor.Id;
            backend.Game.RestoreState(renderState);
            actor = backend.Game.Find(actor.Id);
            target = backend.Game.Find(target.Id);
            TacticalMapElement tactical = ui.EditorRoot.Q<TacticalMapElement>();
            // The attack may have opened a handoff, so create its next owner-scoped map first.
            if (tactical == null) { ui.EditorShowGame(); tactical = ui.EditorRoot.Q<TacticalMapElement>(); }
            tactical.SetState(backend.Game, ToolkitActionMode.None, MoveMode.Normal, SearchMode.Passive, Salvo.Standard);
            tactical.ShowFriendlyBallisticCue(backend.ToolkitFriendlyInterceptionCue(target.Side));
            Assert(tactical.EditorEffectCount == 0, "Actual enemy interception event cannot render on the attacker's map");
            tactical.ShowFriendlyBallisticCue(new SeaPrototypeController.FriendlyBallisticCue { Side = actor.Side, FormationId = target.Id, Position = target.Position, Kind = OperationalEffectKind.Interception });
            Assert(tactical.EditorEffectCount == 0, "A mismatched cue audience cannot bypass provider ownership checks");
            tactical.ShowFriendlyBallisticCue(backend.ToolkitFriendlyLaunchCue(actor.Side));
            Assert(tactical.EditorEffectCount == 1, "Owned committed launch event reaches the 3D effect renderer");
            using (var presentation = new OperationalMap3D(backend.Game.Area, 320, 180))
            {
                presentation.SetState(backend.Game, ToolkitActionMode.None, MoveMode.Normal, SearchMode.Passive, Salvo.Standard);
                presentation.SetBallisticCoverage(actor);
                Assert(presentation.BallisticCoverageProviderId == actor.Id && presentation.BallisticCoverageHexCount == backend.Game.FriendlyBallisticCoverageHexes(actor.Side, actor).Count,
                    "3D coverage footprint uses the authoritative hex-distance boundary");
                actor.BallisticDefense.Interceptors = 0;
                presentation.SetState(backend.Game, ToolkitActionMode.None, MoveMode.Normal, SearchMode.Passive, Salvo.Standard);
                presentation.SetBallisticCoverage(actor);
                Assert(presentation.BallisticCoverageHexCount > 0 && presentation.BallisticProtectedFormationCount == 0
                    && MapMarkerDetails.BallisticDefense(backend.Game, actor.Side, actor).Contains("depleted"), "Depleted defense retains a nominal range outline with no protected target rings and explains its status");
                presentation.SetBallisticCoverage(target);
                Assert(presentation.BallisticCoverageProviderId == null && presentation.BallisticCoverageHexCount == 0, "Enemy formation input cannot render enemy defense coverage");
                Assert(backend.Game.Active.Id == actor.Id && backend.Game.Active.Kind == FormationKind.BallisticMissileBattery, "Battery render fixture retains the selected owned battery");
                Assert(presentation.ContainsRenderedName("Mobile Ballistic Launcher Chassis"), "Owned battery renders its land launcher chassis");
                Assert(presentation.ContainsRenderedName("Ballistic Launch Canister"), "Owned battery renders its launch canister");
                Assert(presentation.ContainsRenderedName("BallisticMissileBattery Distant Operational Symbol"), "Owned battery renders its distant symbol");
            }
            ui.EditorShowGame();
            ui.EditorShowGame();
            Assert(ui.EditorRoot.Q<TacticalMapElement>().EditorEffectCount == 0, "Reopening the same command map does not replay an already presented ballistic cue");
            backend.ToolkitNewScenario("meridian-veil");
            Assert(backend.ToolkitFriendlyLaunchCue(actor.Side) == null && backend.ToolkitFriendlyInterceptionCue(target.Side) == null, "Restarting an operation clears previous launch and interception cues");
            ui.EditorShowGame();
        }

        private static void TestMarkerCards(SeaUIToolkitController ui, SeaPrototypeController backend)
        {
            TacticalMapElement map = ui.EditorRoot.Q<TacticalMapElement>();
            Assert(map != null && !map.EditorMarkerCardVisible, "Marker details are hidden until hover or keyboard inspection");
            map.FocusObjective();
            Assert(map.EditorMarkerCardVisible && map.EditorMarkerCardText.Contains("OPERATIONAL OBJECTIVE") && map.EditorMarkerCardText.Contains("6 VP") && map.EditorMarkerCardText.Contains("2 VP"),
                "Objective focus exposes executable Control and Denial scoring without a permanent label panel");
            FormationState enemy = backend.Game.Formations.First(item => item.Side != backend.Game.Active.Side);
            HexCoord originalPosition = enemy.Position;
            string publicObjective = MapMarkerDetails.Objective(backend.Game).Description;
            enemy.Position = backend.Game.Area.Objective;
            Assert(MapMarkerDetails.Objective(backend.Game).Description == publicObjective, "Objective hover cannot disclose hidden enemy presence or contested control");
            enemy.Position = originalPosition;
            Assert(MapMarkerDetails.Formation(backend.Game, enemy) == null, "Marker details reject hidden enemy formation state");
            map.EditorInspectHex(backend.Game.Active.Position);
            Assert(map.EditorMarkerCardText.Contains(backend.Game.Active.Name) && map.EditorMarkerCardText.Contains("MAGAZINES"), "Friendly hover retains name, readiness and finite magazines");
            ContactState contact = backend.Game.Contacts.First(item => item.Owner == backend.Game.Active.Side);
            map.EditorInspectHex(contact.LastKnownPosition);
            string enemyName = backend.Game.Find(contact.TargetId).Name;
            Assert(map.EditorMarkerCardText.Contains("CONTACT REPORT") && !map.EditorMarkerCardText.Contains(enemyName), "Unidentified Contact hover does not reveal the target identity");
            contact.HasContradictoryPosition = true;
            contact.ContradictoryPosition = new HexCoord(4, 3);
            map.EditorInspectHex(contact.ContradictoryPosition);
            Assert(map.EditorMarkerCardText.Contains("REPORTED HEX " + contact.ContradictoryPosition), "Contradictory Contact markers retain their own reported fix");
            contact.HasContradictoryPosition = false;
            ContactState foreign = backend.Game.Contacts.First(item => item.Owner != backend.Game.Active.Side);
            Assert(MapMarkerDetails.Contact(backend.Game, foreign, foreign.LastKnownPosition) == null, "Hover never uses another side's Contact report");
            VisualElement card = map.Q<VisualElement>("map-marker-card");
            Assert(card.pickingMode == PickingMode.Ignore && card.Query<Label>().ToList().All(label => label.pickingMode == PickingMode.Ignore), "Hover details never intercept map orders or camera input");
            foreach (Vector2 anchor in new[] { Vector2.zero, new Vector2(1279, 719), new Vector2(640, 360) })
            {
                Rect bounds = TacticalMapElement.EditorMarkerCardBounds(anchor, new Vector2(1280, 720), new Vector2(320, 280));
                Assert(bounds.xMin >= 12f && bounds.xMax <= 1268f && bounds.yMin >= 52f && bounds.yMax <= 708f, "Marker card stays inside the map at " + anchor);
            }
            map.EditorClearMarkerInfo();
            Assert(!map.EditorMarkerCardVisible, "Leaving a marker clears the information card");
            ui.EditorShowGame();
            Assert(!ui.EditorRoot.Q<TacticalMapElement>().EditorMarkerCardVisible, "Rebuilding the command surface clears transient marker information");
        }

        private static void TestMapCommands(SeaUIToolkitController ui, SeaPrototypeController backend)
        {
            Side side = backend.Game.Active.Side;
            foreach (CommandArchitecture architecture in Enum.GetValues(typeof(CommandArchitecture)))
            {
                backend.Game.Sides[side].Architecture = architecture;
                ui.EditorShowGame();
                int capacity = architecture == CommandArchitecture.Centralized ? 4 : architecture == CommandArchitecture.Distributed ? 2 : 3;
                Assert(VisibleText(ui).Contains($"/{capacity} FREE"), "Command status reflects " + architecture + " capacity");
            }
            backend.Game.Sides[side].Architecture = CommandArchitecture.MissionCommand;
            ui.EditorShowGame();
            Assert(ui.EditorGameShortcut(KeyCode.I) && ui.EditorRoot.Q<VisualElement>("dialog-formationinspector") != null, "Inspector keyboard shortcut opens an on-demand scrollable dossier");
            Assert(ui.EditorRoot.Q<ScrollView>(className: "inspector-scroll") != null && VisibleText(ui).Contains("MAGAZINES"), "Inspector retains magazines, ratings and formation state");
            Assert(ui.EditorInvokeButton("CLOSE") && !ui.EditorOverlayOpen, "Inspector returns to the unobstructed map");
            Assert(ui.EditorGameShortcut(KeyCode.T) && ui.EditorRoot.Q<VisualElement>("dialog-friendlyreadyqueue") != null, "Ready queue remains accessible by keyboard");
            string queueText = VisibleText(ui);
            Assert(backend.Game.Formations.Where(item => item.Side != side).All(item => !queueText.Contains(item.Name)), "Ready queue never lists hidden enemy identities");
            FormationState friendly = backend.Game.Formations.First(item => item.Side == side && item != backend.Game.Active);
            Button row = ui.EditorRoot.Query<Button>().ToList().First(button => button.text.Contains(friendly.Name));
            ((Action)row.userData)();
            Assert(VisibleText(ui).Contains(friendly.Name.ToUpperInvariant()) && ui.EditorRoot.Q<VisualElement>("dialog-formationinspector") != null, "Queue inspection shows the chosen friendly formation rather than always the active one");
            ui.EditorInvokeButton("CLOSE");
            ContactState forbidden = backend.Game.Contacts.First(item => item.Owner != side);
            ui.EditorInspectContact(forbidden);
            Assert(!ui.EditorOverlayOpen, "Inspector refuses another side's Contact");
            ContactState owned = backend.Game.Contacts.First(item => item.Owner == side);
            owned.Identity = IdentityQuality.Unknown;
            ui.EditorInspectContact(owned);
            Assert(ui.EditorRoot.Q<VisualElement>("dialog-contactintelligence") != null && !VisibleText(ui).Contains(backend.Game.Find(owned.TargetId).Name), "Unknown Contact inspection conceals the authoritative target name");
            ui.EditorInvokeButton("CLOSE");

            Assert(ui.EditorGameShortcut(KeyCode.Alpha1) && ui.EditorRoot.Q<VisualElement>("dialog-moveorders") != null, "Move shortcut opens the shared posture picker");
            Assert(ui.EditorInvokeButton("×"), "Context close preserves a usable map");
            Assert(ui.EditorGameShortcut(KeyCode.Alpha2) && ui.EditorRoot.Query<Button>().ToList().Count(button => button.text.Contains("LOCATION") || button.text.Contains("IDENTITY")) == 6,
                "Search shortcut retains every posture and intelligence priority");
            ui.EditorInvokeButton("×");
            Assert(ui.EditorGameShortcut(KeyCode.Alpha3) && ui.EditorRoot.Q<VisualElement>("dialog-strikeorders") != null, "Strike shortcut opens the shared finite-magazine picker");
            ui.EditorInvokeButton("×");
            Assert(ui.EditorGameShortcut(KeyCode.Alpha9) && ui.EditorOverlayOpen, "Synchronized declaration has a working keyboard path");
            ui.EditorShowGame();

            FormationState active = backend.Game.Active;
            active.Friction = active.Disruption = active.Destruction = true;
            int strain = backend.Game.Sides[side].CommandStrain;
            ui.EditorShowFormationOrders();
            Assert(ui.EditorInvokeButton("PUSH THROUGH • +1 STRAIN") && active.PushThroughReady && backend.Game.Sides[side].CommandStrain == strain + 1,
                "Push Through is available through map orders and updates authoritative state");

            backend.ToolkitNewScenario("meridian-veil");
            FormationState carrier = backend.Game.Formations.First(item => item.Side == Side.Blue && item.Kind == FormationKind.CarrierGroup);
            PrototypeGame.SaveData state = backend.Game.CaptureState();
            state.ActiveFormationId = carrier.Id;
            state.Formations.First(item => item.Id == carrier.Id).ReadyTime = state.Time;
            backend.Game.RestoreState(state);
            backend.Game.Sides[Side.Blue].CommandStrain = 2;
            ui.EditorShowGame();
            ui.EditorShowFormationOrders();
            Assert(ui.EditorInvokeButton("HQ RECOVERY • 2") && backend.Game.Sides[Side.Blue].CommandStrain == 0, "HQ Recovery remains reachable through map orders at a compatible logistics location");

            backend.ToolkitNewScenario("meridian-veil");
            ui.EditorShowGame();
            FormationState leader = backend.Game.Active;
            ContactState contact = backend.Game.Contacts.First(item => item.Owner == leader.Side);
            var strike = new SynchronizedStrikeState { Id = "UI-SYNC", Side = leader.Side, LeaderId = leader.Id, ContactTargetId = contact.TargetId, Aim = contact.LastKnownPosition, StrikeTime = backend.Game.Time };
            strike.Participants.Add(new SynchronizedStrikeParticipant { FormationId = leader.Id });
            backend.Game.SynchronizedStrikes.Add(strike);
            leader.SynchronizedStrikeId = strike.Id;
            leader.IsSynchronizedStrikeLeader = true;
            ui.EditorShowGame();
            Assert(ui.EditorInvokeButton("SYNC READY • ORDERS") && ui.EditorRoot.Query<Button>().ToList().Any(button => button.text == "RESOLVE VOLLEY"), "A ready synchronized strike surfaces its resolution controls in map orders");
            Assert(!ui.EditorRoot.Query<Button>().ToList().Any(button => button.text.Contains("MOVE ORDERS")), "Reserved strike resolution does not expose conflicting ordinary orders");
            ui.EditorInvokeButton("×");
            Assert(ui.EditorGameShortcut(KeyCode.Alpha1) && ui.EditorRoot.Q<VisualElement>("dialog-activeformationorders") != null, "Keyboard commands also respect ready synchronized-strike state");
            Assert(ui.EditorInvokeButton("RETASK • +1 TIME / FRICTION") && ui.EditorOverlayOpen, "Retasking remains reachable from the shared palette");
            ui.EditorShowGame();
            contact.IsLost = true;
            ui.EditorShowFormationOrders();
            Assert(ui.EditorRoot.Query<Button>().ToList().Any(button => button.text == "CONTINUE BLIND"), "Lost Contact keeps explicit blind-resolution access");
            leader.Friction = true;
            Assert(ui.EditorInvokeButton("ABORT • +1 TIME / FRICTION") && !backend.Game.SynchronizedStrikes.Contains(strike), "Abort remains reachable and releases the reservation");
            backend.ToolkitNewScenario("meridian-veil");
            backend.ToolkitSetOperationMode(OperationMode.LocalHotseat);
            ui.EditorShowGame();
            string previousCommanderFormation = backend.Game.Active.Name;
            Assert(ui.EditorGameShortcut(KeyCode.Alpha5) && ui.EditorView == "Handoff", "The shared Hold command retains secure hotseat handoff");
            Assert(ui.EditorInvokeButton("ASSUME COMMAND") && !VisibleText(ui).Contains(previousCommanderFormation), "Persistent feedback does not reveal the previous side's private action after handoff");
            backend.ToolkitNewScenario("meridian-veil");
            ui.EditorShowGame();
        }

        private static void Assert(bool condition, string message)
        {
            TestChecks.Check(condition, "UI interaction: " + message);
        }
    }
}
