using System;
using System.Collections.Generic;
using System.Linq;

namespace SeaOfUncertainty.Core
{
    public sealed class PrototypeGame
    {
        [Serializable]
        public sealed class SaveData
        {
            public int Version = 9;
            public string ScenarioId;
            public string OperationalAreaId;
            public int Time;
            public string ActiveFormationId;
            public bool AgeTwoTargetingPenalty;
            public bool HasLastActingSide;
            public Side LastActingSide;
            public List<FormationState> Formations = new List<FormationState>();
            public List<ContactState> Contacts = new List<ContactState>();
            public List<SideState> Sides = new List<SideState>();
            public List<EntropyDeckState> EntropyDecks = new List<EntropyDeckState>();
            public List<EntropyDrawNotice> PendingEntropyReveals = new List<EntropyDrawNotice>();
            public List<EntropyResponseWindowState> EntropyResponseWindows = new List<EntropyResponseWindowState>();
            public List<CommandResponseDeckState> CommandResponseDecks = new List<CommandResponseDeckState>();
            public List<string> Log = new List<string>();
            public List<OperationalLogEntry> LogEntries = new List<OperationalLogEntry>();
        }

        private readonly Random random;
        public readonly List<FormationState> Formations = new List<FormationState>();
        public readonly List<ContactState> Contacts = new List<ContactState>();
        public readonly Dictionary<Side, SideState> Sides = new Dictionary<Side, SideState>();
        public readonly List<EntropyDeckState> EntropyDecks = new List<EntropyDeckState>();
        public readonly List<EntropyDrawNotice> PendingEntropyReveals = new List<EntropyDrawNotice>();
        public readonly List<EntropyResponseWindowState> EntropyResponseWindows = new List<EntropyResponseWindowState>();
        public readonly List<CommandResponseDeckState> CommandResponseDecks = new List<CommandResponseDeckState>();
        public readonly List<string> Log = new List<string>();
        public readonly List<OperationalLogEntry> LogEntries = new List<OperationalLogEntry>();
        public event Action<FormationState, ActionKind> ActionCompleted;
        public event Action<FormationState, string, string> CommandEvent;
        public ScenarioDefinition Scenario { get; }
        public OperationalAreaDefinition Area => Scenario.Area;
        public int Time { get; private set; }
        public FormationState Active { get; private set; }
        public Side? LastActingSide => lastActingSide;
        public bool AgeTwoTargetingPenalty = true;
        private Side? lastActingSide;
        private readonly int seed;

        public PrototypeGame(int seed = 1978, ScenarioDefinition scenario = null)
        {
            this.seed = seed;
            random = new Random(seed);
            Scenario = OperationalDataMigration.Migrate(scenario ?? ScenarioCatalog.MeridianVeil());
            InitializeEntropyDecks(seed);
            Sides[Side.Blue] = new SideState { Side = Side.Blue };
            Sides[Side.Red] = new SideState { Side = Side.Red };
            EnsureCommandSlotStates();
            InitializeCommandResponseDecks(seed);
            CreateScenario();
            AdvanceToNextFormation();
        }

        public FormationState Find(string id) => Formations.FirstOrDefault(f => f.Id == id);
        public ContactState ContactFor(Side owner, string targetId) => Contacts.FirstOrDefault(c => c.Owner == owner && c.TargetId == targetId && !c.IsLost);
        public int SearchModifierFor(FormationState formation, SearchMode mode) => mode == SearchMode.Active && formation != null && formation.HasEffect("D-05") ? 0 : Rules.SearchModifier(mode);
        public int SearchRangeFor(FormationState formation, SearchMode mode)
        {
            if (formation == null) return Rules.SearchRange(mode);
            SensorRangeDefinition profile = Scenario.SensorRanges?.FirstOrDefault(range => range.Kind == formation.Kind);
            return profile?.Range(mode) ?? Rules.SearchRange(mode);
        }

        public IReadOnlyList<HexCoord> ContactPossibleHexes(ContactState contact)
        {
            if (contact == null || contact.IsLost) return new List<HexCoord>();
            int radius = Rules.ContactUncertaintyRadius(contact);
            var centers = new List<HexCoord> { contact.LastKnownPosition };
            if (contact.HasContradictoryPosition) centers.Add(contact.ContradictoryPosition);
            return Enumerable.Range(0, Area.Width)
                .SelectMany(q => Enumerable.Range(0, Area.Height).Select(r => new HexCoord(q, r)))
                .Where(Area.Contains)
                .Where(hex => centers.Any(center => HexCoord.Distance(center, hex) <= radius))
                .OrderBy(hex => centers.Min(center => HexCoord.Distance(center, hex)))
                .ThenBy(hex => hex.Q)
                .ThenBy(hex => hex.R)
                .ToList();
        }

        public IReadOnlyList<string> VisibleLog(Side side)
            => LogEntries.Where(entry => !entry.IsPrivate || entry.Audience == side).Select(entry => entry.Text).ToList();

        public bool Patrol(FormationState formation, HexCoord center, PatrolPosture posture, FormationState protectedFormation, out string message)
        {
            if (formation != Active) { message = "Only the highlighted Ready formation may act."; return false; }
            if (!Area.Contains(center) || HexCoord.Distance(formation.Position, center) > 1) { message = "A Screen must be centered in this Formation's hex or an adjacent hex."; return false; }
            if (protectedFormation != null && (protectedFormation.Side != formation.Side || protectedFormation.IsDestroyed || HexCoord.Distance(center, protectedFormation.Position) > Rules.PatrolRadius))
            { message = "The protected Formation must be friendly and inside the Screen area."; return false; }
            if (!AuthorizeAction(formation, ActionKind.Patrol, out message)) return false;
            formation.PatrolActive = true;
            formation.PatrolCenter = center;
            formation.PatrolProtectedFormationId = protectedFormation?.Id;
            formation.PatrolPosture = posture;
            formation.PatrolInterceptionAvailable = true;
            CompleteAction(formation, ActionKind.Patrol, false, $"{formation.Name} established a {posture} Screen at {center} (radius {Rules.PatrolRadius}){(protectedFormation == null ? string.Empty : " protecting " + protectedFormation.Name)}.", loudAfterAction: posture == PatrolPosture.Aggressive);
            message = Log[0];
            return true;
        }

        public bool Support(FormationState supporter, FormationState recipient, SupportKind kind, out string message)
        {
            if (supporter != Active) { message = "Only the highlighted Ready formation may act."; return false; }
            if (recipient == null || recipient.IsDestroyed || recipient.Side != supporter.Side || recipient == supporter) { message = "Choose another friendly Formation to receive Support."; return false; }
            if (HexCoord.Distance(supporter.Position, recipient.Position) > Rules.SupportRange) { message = $"Support range is {Rules.SupportRange} hexes."; return false; }
            if (supporter.SupportBlockedUntilRecover) { message = "Fuel Priority Conflict prevents Support until this Formation Recovers."; return false; }
            if ((supporter.Kind == FormationKind.AirGroup || supporter.Kind == FormationKind.CarrierGroup) && supporter.HasEffect("X-08")) { message = "Hangar Damage makes this Formation's air Support unavailable."; return false; }
            if (kind == SupportKind.Synchronization && !CanParticipateInSynchronization(supporter)) { message = supporter.HasEffect("D-12") ? "Compromised Plot prevents Synchronization until Recover." : "Staff Overload prevents Synchronization until this Formation completes another Action or spends its printed response."; return false; }
            if (!AuthorizeAction(supporter, ActionKind.Support, out message)) return false;
            bool lost = supporter.HasEffect("D-11");
            int coordinationDelay = kind == SupportKind.Synchronization && supporter.HasEffect("F-05") ? 1 : 0;
            ClearSupport(supporter);
            if (lost) ResolveAttachedEffect(supporter, "D-11");
            else
            {
                foreach (FormationState existing in Formations.Where(candidate => candidate != supporter && candidate.SupportActive && candidate.SupportRecipientId == recipient.Id && candidate.SupportKind == kind).ToList()) ClearSupport(existing);
                supporter.SupportActive = true;
                supporter.SupportRecipientId = recipient.Id;
                supporter.SupportKind = kind;
            }
            CompleteAction(supporter, ActionKind.Support, false, lost
                ? $"{supporter.Name}'s {kind} Support for {recipient.Name} was lost to misrouted orders."
                : $"{supporter.Name} assigned {kind} Support (+1) to {recipient.Name} within range {Rules.SupportRange}.{(coordinationDelay > 0 ? " Coordination Drift added +1 Time." : string.Empty)}", coordinationDelay);
            message = Log[0];
            return true;
        }

        public int PendingSupportBonus(FormationState recipient, SupportKind kind)
            => EligibleSupporter(recipient, kind) == null ? 0 : 1;

        public bool CanParticipateInSynchronization(FormationState formation)
            => formation != null && !formation.IsDestroyed && !formation.HasEffect("F-02") && !formation.HasEffect("D-12");

        public int PatrolDefenseBonus(FormationState recipient)
            => EligibleScreen(recipient, true) == null ? 0 : 1;

        public IReadOnlyList<CommandSlotState> CommandSlotsFor(Side side)
        {
            EnsureCommandSlotStates();
            return Sides[side].SlotStates;
        }

        public bool ActionFollowsMission(FormationState formation, ActionKind action) => formation != null && formation.Mission == action;

        public ActionKind? TriggeredMissionFor(FormationState formation)
        {
            if (formation == null) return null;
            switch (formation.MissionTrigger)
            {
                case MissionTrigger.ContactLocated:
                    return Contacts.Any(contact => contact.Owner == formation.Side && !contact.IsLost && contact.Location >= LocationQuality.Low) ? ActionKind.Strike : (ActionKind?)null;
                case MissionTrigger.EntropyMarked:
                    return formation.Friction || formation.Disruption ? ActionKind.Recover : (ActionKind?)null;
                case MissionTrigger.LogisticsRequired:
                    return HasLogisticsAccess(formation) && NeedsReplenishment(formation) ? ActionKind.Replenish : (ActionKind?)null;
                case MissionTrigger.ObjectiveReached:
                    return formation.Position.Equals(Area.Objective) ? ActionKind.Patrol : (ActionKind?)null;
                default: return null;
            }
        }

        public bool AssignStandingMission(FormationState formation, ActionKind task, MissionObjectiveKind objective, string objectiveId, HexCoord objectiveHex, MissionPosture posture, MissionTrigger trigger, out string message)
        {
            if (formation == null || Active == null || formation.Side != Active.Side) { message = "Choose a friendly Formation under the active side's control."; return false; }
            if (formation.IsDestroyed) { message = "Destroyed formations cannot receive a Standing Mission."; return false; }
            if (formation.PendingMissionChange || CommandSlotsFor(formation.Side).Any(slot => slot.Status == CommandSlotStatus.Occupied && slot.FormationId == formation.Id && slot.Purpose == "Standing Mission Change")) { message = "Command Attention is already committed to this Formation's Mission change."; return false; }
            if (formation.MissionChangeLockedUntilAction) { message = "Delegated Authority prevents this Formation from changing Mission until its next Action."; return false; }
            if (formation.HasEffect("D-04")) { message = "Broken Link prevents this Formation from receiving new orders."; return false; }
            bool unchanged = formation.Mission == task && formation.MissionObjective == objective && formation.MissionObjectiveId == objectiveId && formation.MissionObjectiveHex.Equals(objectiveHex) && formation.MissionPosture == posture && formation.MissionTrigger == trigger;
            if (unchanged) { message = "That Standing Mission is already assigned."; return false; }
            int slots = 1 + (formation.HasEffect("F-03") ? 1 : 0);
            if (!TryOccupyCommand(formation.Side, slots, formation.HasEffect("D-02") ? "Delayed Mission Order" : "Standing Mission Change", formation.Id, formation.HasEffect("D-02") ? Time + 1 : -1, out message)) return false;
            if (formation.HasEffect("D-02"))
            {
                formation.PendingMissionChange = true;
                formation.PendingMissionTask = task;
                formation.PendingMissionObjective = objective;
                formation.PendingMissionObjectiveId = objectiveId;
                formation.PendingMissionObjectiveHex = objectiveHex;
                formation.PendingMissionPosture = posture;
                formation.PendingMissionTrigger = trigger;
                formation.MissionDeliveryTime = Time + 1;
                AddLog($"T{Time:00}  COMMAND OCCUPIED — {formation.Name}'s Mission order will arrive at T{formation.MissionDeliveryTime:00}; existing Mission continues.", formation.Side);
                message = $"Mission change scheduled for T{formation.MissionDeliveryTime:00}. {slots} Command Slot{(slots == 1 ? string.Empty : "s")} occupied until delivery.";
                return true;
            }
            ApplyStandingMission(formation, task, objective, objectiveId, objectiveHex, posture, trigger);
            if (formation.HasEffect("F-12")) formation.NextReadyTimeBonus++;
            AddLog($"T{Time:00}  MISSION ASSIGNED — {formation.Name}: {task}, {objective}, {posture}, trigger {trigger}. {slots} Command Slot{(slots == 1 ? string.Empty : "s")} occupied until its next Action.", formation.Side);
            CommandEvent?.Invoke(formation, "MissionChanged", $"{task}; {objective}; {posture}; {trigger}");
            message = Log[0];
            return true;
        }

        public bool PushThrough(FormationState formation, out string message)
        {
            if (formation != Active || formation.EntropySources < 3) { message = "Push Through is available only to the active Disorganized Formation."; return false; }
            if (formation.PushThroughReady) { message = "Push Through is already prepared."; return false; }
            MarkCommandStrain(formation.Side, 1, "Push Through");
            formation.PushThroughReady = true;
            AddLog($"T{Time:00}  PUSH THROUGH — {formation.Name} may perform one complex Action; {formation.Side} marked 1 Command Strain.", formation.Side);
            CommandEvent?.Invoke(formation, "PushThrough", $"Command Strain {Sides[formation.Side].CommandStrain}");
            message = Log[0];
            return true;
        }

        public bool RestoreCommand(FormationState headquarters, out string message)
        {
            if (headquarters != Active || headquarters.Kind != FormationKind.CarrierGroup) { message = "HQ Recovery requires the active Carrier Group."; return false; }
            if (!HasLogisticsAccess(headquarters)) { message = "HQ Recovery requires compatible logistics access."; return false; }
            SideState side = Sides[headquarters.Side];
            if (side.CommandStrain < 1) { message = "This side has no Command Strain to restore."; return false; }
            side.CommandStrain = Math.Max(0, side.CommandStrain - 2);
            ReconcileStrainedSlots(side);
            CommandEvent?.Invoke(headquarters, "CommandRestored", $"HQ Recovery; strain {side.CommandStrain}");
            CompleteAction(headquarters, ActionKind.Recover, false, $"{headquarters.Name} conducted HQ Recovery and removed up to 2 Command Strain.");
            message = Log[0];
            return true;
        }

        public IReadOnlyList<OperationalLocationDefinition> LogisticsFacilitiesFor(FormationState formation)
        {
            if (formation == null) return new List<OperationalLocationDefinition>();
            HashSet<HexCoord> accessHexes = new HashSet<HexCoord>((Scenario.LogisticsRegions ?? new List<OperationalRegionDefinition>()).SelectMany(region => region.Hexes ?? new List<HexCoord>()));
            return Area.Locations.Where(location => accessHexes.Contains(location.Hex) &&
                (formation.Kind == FormationKind.AirGroup ? location.Kind == LocationKind.Airfield : location.Kind == LocationKind.Port || location.Kind == LocationKind.Anchorage))
                .OrderBy(location => HexCoord.Distance(formation.Position, location.Hex)).ThenBy(location => location.Id, StringComparer.Ordinal).ToList();
        }

        public bool HasLogisticsAccess(FormationState formation)
            => formation != null && LogisticsFacilitiesFor(formation).Any(location => location.Hex.Equals(formation.Position));

        public IReadOnlyList<string> RepairableDestructionCards(FormationState formation)
            => (formation?.ActiveEffectCardIds ?? new List<string>()).Where(id => EntropyEffectCatalog.Find(id)?.Source == EntropySource.Destruction).ToList();

        public bool NeedsReplenishment(FormationState formation)
            => formation != null && !formation.IsDestroyed && (formation.Endurance != Endurance.Ready || formation.WeaponExpended || formation.Damage != DamageState.None || RepairableDestructionCards(formation).Count > 0 || formation.MajorActions > 0);

        public string ReplenishmentPreview(FormationState formation)
        {
            if (formation == null) return "No Formation selected.";
            var restores = new List<string>();
            if (formation.Endurance != Endurance.Ready) restores.Add($"Endurance {formation.Endurance} → {(Endurance)((int)formation.Endurance - 1)}");
            if (formation.WeaponExpended) restores.Add("Heavy Salvo reloaded");
            if (formation.Damage != DamageState.None) restores.Add($"Damage {formation.Damage} → {PreviousDamage(formation.Damage)}");
            int cards = RepairableDestructionCards(formation).Count;
            if (cards > 0) restores.Add($"repair and discard one of {cards} Destruction card{(cards == 1 ? string.Empty : "s")}");
            if (formation.MajorActions > 0) restores.Add("major-action Endurance track reset");
            return restores.Count == 0 ? "Nothing currently requires replenishment." : string.Join("; ", restores) + ".";
        }

        public bool Replenish(FormationState formation, string destructionCardId, out string message)
        {
            if (formation != Active) { message = "Only the highlighted Ready formation may act."; return false; }
            if (!HasLogisticsAccess(formation)) { message = formation.Kind == FormationKind.AirGroup ? "Replenishment requires an Airfield in a scenario logistics region." : "Replenishment requires a Port or Anchorage in a scenario logistics region."; return false; }
            if (!NeedsReplenishment(formation)) { message = "This Formation has no Endurance, weapon, damage, Destruction, or action-track loss to restore."; return false; }
            IReadOnlyList<string> repairable = RepairableDestructionCards(formation);
            if (!string.IsNullOrEmpty(destructionCardId) && !repairable.Contains(destructionCardId)) { message = "Choose an attached Destruction card to repair."; return false; }
            if (!AuthorizeAction(formation, ActionKind.Replenish, out message)) return false;
            string preview = ReplenishmentPreview(formation);
            int additionalTime = formation.HasEffect("X-10") ? 1 : 0;
            if (formation.Endurance > Endurance.Ready) formation.Endurance--;
            formation.WeaponExpended = false;
            formation.Damage = PreviousDamage(formation.Damage);
            if (formation.Damage != DamageState.Light) formation.LightDamageExpiresAfterAction = 0;
            formation.MajorActions = 0;
            string repaired = repairable.Count == 0 ? null : string.IsNullOrEmpty(destructionCardId) ? repairable[0] : destructionCardId;
            if (!string.IsNullOrEmpty(repaired)) RemoveAttachedEffect(formation, repaired);
            formation.Loud = false;
            formation.Replenishing = true;
            CompleteAction(formation, ActionKind.Replenish, false, $"{formation.Name} replenished at {formation.Position}: {preview}{(string.IsNullOrEmpty(repaired) ? string.Empty : " Repaired " + repaired + ".")}", additionalTime);
            message = Log[0];
            return true;
        }

        public SaveData CaptureState()
        {
            return new SaveData
            {
                Time = Time,
                ScenarioId = Scenario.Id,
                OperationalAreaId = Area.Id,
                ActiveFormationId = Active?.Id,
                AgeTwoTargetingPenalty = AgeTwoTargetingPenalty,
                HasLastActingSide = lastActingSide.HasValue,
                LastActingSide = lastActingSide.GetValueOrDefault(),
                Formations = new List<FormationState>(Formations),
                Contacts = new List<ContactState>(Contacts),
                Sides = new List<SideState>(Sides.Values),
                EntropyDecks = EntropyDecks.Select(CloneDeck).ToList(),
                PendingEntropyReveals = PendingEntropyReveals.Select(notice => new EntropyDrawNotice { CardId = notice.CardId, FormationId = notice.FormationId }).ToList(),
                EntropyResponseWindows = EntropyResponseWindows.Select(window => new EntropyResponseWindowState { FormationId = window.FormationId, CardId = window.CardId, ExpiresAfterCompletedActions = window.ExpiresAfterCompletedActions }).ToList(),
                CommandResponseDecks = CommandResponseDecks.Select(CloneResponseDeck).ToList(),
                Log = new List<string>(Log),
                LogEntries = LogEntries.Select(entry => new OperationalLogEntry { Text = entry.Text, IsPrivate = entry.IsPrivate, Audience = entry.Audience }).ToList()
            };
        }

        public void RestoreState(SaveData data)
        {
            if (data == null || data.Version < 1 || data.Version > 9) throw new ArgumentException("Unsupported or empty save data.");
            if (data.Version >= 2 && !string.IsNullOrEmpty(data.ScenarioId) && !string.Equals(data.ScenarioId, Scenario.Id, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException($"Save scenario {data.ScenarioId} does not match loaded scenario {Scenario.Id}.");
            Formations.Clear();
            Formations.AddRange(data.Formations ?? new List<FormationState>());
            foreach (FormationState formation in Formations)
            {
                if (formation.ActiveEffectCardIds == null) formation.ActiveEffectCardIds = new List<string>();
                if (formation.ResolvedEffectCardIds == null) formation.ResolvedEffectCardIds = new List<string>();
                if (data.Version < 8 && formation.Damage == DamageState.Light) formation.LightDamageExpiresAfterAction = formation.CompletedActions + 1;
            }
            Contacts.Clear();
            Contacts.AddRange(data.Contacts ?? new List<ContactState>());
            Sides.Clear();
            foreach (SideState side in data.Sides ?? new List<SideState>()) Sides[side.Side] = side;
            if (!Sides.ContainsKey(Side.Blue)) Sides[Side.Blue] = new SideState { Side = Side.Blue };
            if (!Sides.ContainsKey(Side.Red)) Sides[Side.Red] = new SideState { Side = Side.Red };
            EnsureCommandSlotStates();
            if (data.Version >= 4 && data.EntropyDecks != null && data.EntropyDecks.Count > 0)
            {
                EntropyDecks.Clear();
                EntropyDecks.AddRange(data.EntropyDecks.Select(CloneDeck));
            }
            EnsureEntropyDeckCompleteness();
            PendingEntropyReveals.Clear();
            if (data.Version >= 4 && data.PendingEntropyReveals != null) PendingEntropyReveals.AddRange(data.PendingEntropyReveals.Select(notice => new EntropyDrawNotice { CardId = notice.CardId, FormationId = notice.FormationId }));
            EntropyResponseWindows.Clear();
            if (data.Version >= 9 && data.EntropyResponseWindows != null) EntropyResponseWindows.AddRange(data.EntropyResponseWindows.Select(window => new EntropyResponseWindowState { FormationId = window.FormationId, CardId = window.CardId, ExpiresAfterCompletedActions = window.ExpiresAfterCompletedActions }));
            else foreach (FormationState formation in Formations) foreach (string id in formation.ActiveEffectCardIds.Where(id => EntropyEffectCatalog.Find(id)?.ResponseWindow != EntropyResponseWindow.None))
                EntropyResponseWindows.Add(new EntropyResponseWindowState { FormationId = formation.Id, CardId = id, ExpiresAfterCompletedActions = formation.CompletedActions + 1 });
            CommandResponseDecks.Clear();
            if (data.Version >= 5 && data.CommandResponseDecks != null && data.CommandResponseDecks.Count > 0) CommandResponseDecks.AddRange(data.CommandResponseDecks.Select(CloneResponseDeck));
            else InitializeCommandResponseDecks(seed);
            Log.Clear();
            Log.AddRange(data.Log ?? new List<string>());
            LogEntries.Clear();
            if (data.Version >= 7 && data.LogEntries != null) LogEntries.AddRange(data.LogEntries.Select(entry => new OperationalLogEntry { Text = entry.Text, IsPrivate = entry.IsPrivate, Audience = entry.Audience }));
            Time = data.Time;
            AgeTwoTargetingPenalty = data.AgeTwoTargetingPenalty;
            lastActingSide = data.Version >= 3 && data.HasLastActingSide ? data.LastActingSide : (Side?)null;
            Active = Find(data.ActiveFormationId) ?? Rules.NextReady(Formations, lastActingSide);
            if (Active != null && Active.Replenishing && Active.ReadyTime <= Time) Active.Replenishing = false;
        }

        public EntropyEffectDefinition PendingEntropyEffectFor(Side side)
        {
            EntropyDrawNotice notice = PendingNoticeFor(side);
            return EntropyEffectCatalog.Find(notice?.CardId);
        }

        public FormationState PendingEntropyFormationFor(Side side) => Find(PendingNoticeFor(side)?.FormationId);

        public void ConsumePendingEntropyEffect(Side side)
        {
            EntropyDrawNotice notice = PendingNoticeFor(side);
            if (notice != null) PendingEntropyReveals.Remove(notice);
        }

        public IReadOnlyList<CommandResponseDefinition> ResponseHand(Side side)
        {
            CommandResponseDeckState deck = CommandResponseDecks.First(state => state.Side == side);
            return deck.Hand.Select(CommandResponseCatalog.Find).Where(card => card != null).ToList();
        }

        public bool RespondToEntropy(FormationState formation, string cardId, out string message)
        {
            if (formation == null || !formation.HasEffect(cardId)) { message = "That effect is not active."; return false; }
            EntropyEffectDefinition card = EntropyEffectCatalog.Find(cardId);
            EntropyResponseWindowState window = EntropyResponseWindows.FirstOrDefault(item => item.FormationId == formation.Id && item.CardId == cardId && formation.CompletedActions < item.ExpiresAfterCompletedActions);
            if (card == null || string.IsNullOrEmpty(card.Response) || card.ResponseWindow == EntropyResponseWindow.None) { message = "This effect has no printed Command response."; return false; }
            if (window == null) { message = "That effect's response window has closed; Recover it normally."; return false; }
            if (!TryOccupyCommand(formation.Side, card.ResponseCommandCost, "Entropy Response", formation.Id, -1, out message)) return false;
            ResolveAttachedEffect(formation, cardId);
            EntropyResponseWindows.Remove(window);
            AddLog($"T{Time:00}  COMMAND RESPONSE — {formation.Name} cancelled {cardId} {card.Title}; {card.ResponseCommandCost} Command Slot occupied until its next completed Action.", formation.Side);
            message = Log[0];
            return true;
        }

        public bool CanRespondToEntropy(FormationState formation, string cardId)
        {
            EntropyEffectDefinition card = EntropyEffectCatalog.Find(cardId);
            return formation != null && formation.HasEffect(cardId) && card != null && !string.IsNullOrEmpty(card.Response) && card.ResponseWindow != EntropyResponseWindow.None &&
                Sides.TryGetValue(formation.Side, out SideState side) && side.CommandSlots >= card.ResponseCommandCost &&
                EntropyResponseWindows.Any(item => item.FormationId == formation.Id && item.CardId == cardId && formation.CompletedActions < item.ExpiresAfterCompletedActions);
        }

        public bool PlayCommandResponse(Side side, string cardId, FormationState formation, ContactState contact, HexCoord? hex, out string message)
            => PlayCommandResponse(side, cardId, formation, contact, hex, null, out message);

        public bool PlayCommandResponse(Side side, string cardId, FormationState formation, ContactState contact, HexCoord? hex, ActionKind? mission, out string message)
        {
            CommandResponseDeckState deck = CommandResponseDecks.First(state => state.Side == side);
            CommandResponseDefinition card = CommandResponseCatalog.Find(cardId);
            if (card == null || !deck.Hand.Contains(cardId)) { message = "That Command Response is not in hand."; return false; }
            if (!card.MechanicallySupported) { message = $"{card.Title} requires a game system that is not active in this prototype."; return false; }
            if (formation != null && formation.Side != side) { message = "Command Responses may target only friendly formations."; return false; }
            if (!ApplyCommandResponse(card, side, formation, contact, hex, mission, out message)) return false;
            deck.Hand.Remove(cardId);
            deck.DiscardPile.Add(cardId);
            string targetDetail = card.Id == "C-04" || card.Id == "C-17" ? $" {formation.Name} is now assigned to {mission}." : card.Id == "C-18" ? $" {formation.Name} prepared an Evade and two-hex withdrawal." : string.Empty;
            AddLog($"T{Time:00}  RESPONSE PLAYED — {side}: {card.Id} {card.Title}.{targetDetail} {card.Play}{(string.IsNullOrEmpty(card.Cost) ? string.Empty : " Cost: " + card.Cost)}", side);
            message = Log[0];
            return true;
        }

        public EntropyEffectDefinition MarkEntropy(FormationState formation, EntropySource source)
        {
            if (formation == null) return null;
            SetEntropyMarked(formation, source, true);
            EntropyDeckState deck = EntropyDecks.First(state => state.Source == source);
            EntropyEffectDefinition card = DrawApplicableCard(deck, formation.Kind);
            if (card == null) return null;
            if (formation.ActiveEffectCardIds == null) formation.ActiveEffectCardIds = new List<string>();
            formation.ActiveEffectCardIds.Add(card.Id);
            PendingEntropyReveals.Add(new EntropyDrawNotice { CardId = card.Id, FormationId = formation.Id });
            if (card.ResponseWindow != EntropyResponseWindow.None)
                EntropyResponseWindows.Add(new EntropyResponseWindowState { FormationId = formation.Id, CardId = card.Id, ExpiresAfterCompletedActions = formation.CompletedActions + (formation == Active ? 2 : 1) });
            ApplyImmediateCardEffect(formation, card);
            AddLog($"T{Time:00}  CARD PULL â€” {formation.Name}: {card.Id} {card.Title}. {card.Effect}", formation.Side);
            return card;
        }

        public IReadOnlyList<FormationState> ActivationQueue()
        {
            var remaining = Formations.Where(formation => !formation.IsDestroyed).ToList();
            var queue = new List<FormationState>(remaining.Count);
            Side? previous = lastActingSide;
            while (remaining.Count > 0)
            {
                FormationState next = Rules.NextReady(remaining, previous);
                if (next == null) break;
                queue.Add(next);
                remaining.Remove(next);
                previous = next.Side;
            }
            return queue;
        }

        public bool Move(FormationState formation, HexCoord destination, MoveMode mode, out string message)
        {
            if (formation != Active) { message = "Only the highlighted Ready formation may act."; return false; }
            if (mode == MoveMode.HighTempo && formation.HasEffect("F-11")) { message = "Checklist Churn prevents High Tempo until this Formation Holds or Recovers."; return false; }
            if (!TryGetMovePath(formation, destination, mode, out List<HexCoord> path, out message)) return false;
            if (!AuthorizeAction(formation, ActionKind.Move, out message)) return false;
            foreach (ContactState contact in Contacts.Where(contact => contact.Owner != formation.Side && contact.TargetId == formation.Id && !contact.IsLost))
                contact.MovementUncertainty = Math.Min(2, contact.MovementUncertainty + 1);
            formation.Position = destination;
            if (formation.HasEffect("F-07")) ResolveAttachedEffect(formation, "F-07");
            if (mode == MoveMode.HighTempo) MarkEntropy(formation, EntropySource.Friction);
            if (mode == MoveMode.HighTempo && formation.HasEffect("F-08")) formation.SupportBlockedUntilRecover = true;
            if (mode == MoveMode.HighTempo && formation.HasEffect("F-04")) DegradeEndurance(formation);
            int distance = path.Count - 1;
            bool crossedLittoral = formation.Kind != FormationKind.AirGroup && path.Skip(1).Any(hex => Area.TerrainAt(hex) == OperationalTerrain.Littoral);
            int terrainTime = crossedLittoral ? 1 : 0;
            string interception = ResolvePatrolInterception(formation);
            ExpireOutOfRangeSupport(formation);
            CompleteAction(formation, ActionKind.Move, mode == MoveMode.HighTempo, $"{formation.Name} moved {distance} hexes ({mode}) via {string.Join("-", path)}{(terrainTime > 0 ? " through littoral waters" : string.Empty)}.{interception}", terrainTime, mode == MoveMode.Cautious ? -1 : 0, mode == MoveMode.HighTempo);
            message = Log[0];
            return true;
        }

        public bool IsLegalMoveDestination(FormationState formation, HexCoord destination, MoveMode mode)
            => TryGetMovePath(formation, destination, mode, out _, out _);

        public IReadOnlyList<HexCoord> LegalMoveDestinations(FormationState formation, MoveMode mode)
        {
            if (formation == null) return new List<HexCoord>();
            return Enumerable.Range(0, Area.Width)
                .SelectMany(q => Enumerable.Range(0, Area.Height).Select(r => new HexCoord(q, r)))
                .Where(Area.Contains)
                .Where(destination => IsLegalMoveDestination(formation, destination, mode))
                .OrderBy(destination => destination.Q)
                .ThenBy(destination => destination.R)
                .ToList();
        }

        public bool TryGetMovePath(FormationState formation, HexCoord destination, MoveMode mode, out List<HexCoord> path, out string message)
        {
            path = new List<HexCoord>();
            if (formation == null) { message = "Choose a Formation to move."; return false; }
            int allowed = Rules.MoveAllowance(formation, mode);
            if (!Area.Contains(destination)) { message = "That hex is outside the active operational area."; return false; }
            if (destination.Equals(formation.Position)) { message = "Choose a different destination hex."; return false; }
            if (IsRestricted(destination)) { message = "That destination is inside a restricted area."; return false; }
            if (Formations.Any(other => other != formation && !other.IsDestroyed && other.Side == formation.Side && other.Position.Equals(destination))) { message = "Only one friendly Formation may end in a hex."; return false; }
            if (!CanEnterMovementHex(formation, destination, true)) { message = "Naval formations may enter Land hexes only at a Port or Anchorage."; return false; }

            var frontier = new Queue<HexCoord>();
            var parents = new Dictionary<HexCoord, HexCoord>();
            var steps = new Dictionary<HexCoord, int> { [formation.Position] = 0 };
            frontier.Enqueue(formation.Position);
            while (frontier.Count > 0)
            {
                HexCoord current = frontier.Dequeue();
                int currentSteps = steps[current];
                if (current.Equals(destination)) break;
                if (currentSteps >= allowed) continue;
                if (!current.Equals(formation.Position) && formation.Kind != FormationKind.AirGroup && Area.TerrainAt(current) == OperationalTerrain.Strait) continue;
                foreach (HexCoord next in Neighbors(current).Where(Area.Contains).OrderBy(hex => MovementTerrainPriority(formation, hex)).ThenBy(hex => hex.Q).ThenBy(hex => hex.R))
                {
                    if (steps.ContainsKey(next) || IsRestricted(next) || !CanEnterMovementHex(formation, next, next.Equals(destination))) continue;
                    steps[next] = currentSteps + 1;
                    parents[next] = current;
                    frontier.Enqueue(next);
                }
            }

            if (!steps.ContainsKey(destination))
            {
                message = $"No legal route reaches that hex within the {allowed}-hex {mode} allowance; Land, restricted areas, and intervening Straits block routes.";
                return false;
            }
            HexCoord cursor = destination;
            path.Add(cursor);
            while (!cursor.Equals(formation.Position)) { cursor = parents[cursor]; path.Add(cursor); }
            path.Reverse();
            message = $"Legal {path.Count - 1}-hex route.";
            return true;
        }

        public bool HasControlPresence(Side side, HexCoord objective)
            => Formations.Any(formation => formation.Side == side && formation.Kind != FormationKind.AirGroup && !formation.IsDestroyed && formation.Damage < DamageState.Crippled && HexCoord.Distance(formation.Position, objective) <= Rules.ControlRadius);

        public bool Controls(Side side, HexCoord objective)
            => HasControlPresence(side, objective) && !HasControlPresence(side == Side.Blue ? Side.Red : Side.Blue, objective);

        private bool IsRestricted(HexCoord hex) => Area.RestrictedAreas != null && Area.RestrictedAreas.Any(region => region.Hexes != null && region.Hexes.Contains(hex));

        private bool CanEnterMovementHex(FormationState formation, HexCoord hex, bool isDestination)
        {
            if (formation.Kind == FormationKind.AirGroup || Area.TerrainAt(hex) != OperationalTerrain.Land) return true;
            return isDestination && (HasFacility(hex, LocationKind.Port) || HasFacility(hex, LocationKind.Anchorage));
        }

        private int MovementTerrainPriority(FormationState formation, HexCoord hex)
        {
            if (formation.Kind == FormationKind.AirGroup) return 0;
            OperationalTerrain terrain = Area.TerrainAt(hex);
            return terrain == OperationalTerrain.DeepWater ? 0 : terrain == OperationalTerrain.Littoral ? 1 : terrain == OperationalTerrain.Strait ? 2 : 3;
        }

        private static IEnumerable<HexCoord> Neighbors(HexCoord hex)
        {
            int[,] even = { { 1, 0 }, { 1, -1 }, { 0, -1 }, { -1, -1 }, { -1, 0 }, { 0, 1 } };
            int[,] odd = { { 1, 1 }, { 1, 0 }, { 0, -1 }, { -1, 0 }, { -1, 1 }, { 0, 1 } };
            int[,] offsets = (hex.Q & 1) == 0 ? even : odd;
            for (int i = 0; i < 6; i++) yield return new HexCoord(hex.Q + offsets[i, 0], hex.R + offsets[i, 1]);
        }

        public bool Search(FormationState searcher, FormationState target, SearchMode mode, out string message)
        {
            if (target == null) { message = "Search needs a Contact or an area hex."; return false; }
            ContactState contact = ContactFor(searcher.Side, target.Id);
            if (contact == null) { message = "Search must select one of your Contacts or an area hex."; return false; }
            return SearchArea(searcher, contact.LastKnownPosition, mode, SearchPriority.Location, out message);
        }

        public bool SearchArea(FormationState searcher, HexCoord center, SearchMode mode, out string message)
            => SearchArea(searcher, center, mode, SearchPriority.Location, out message);

        public bool SearchArea(FormationState searcher, HexCoord center, SearchMode mode, SearchPriority priority, out string message)
        {
            if (searcher != Active) { message = "Only the highlighted Ready formation may act."; return false; }
            if (!Area.Contains(center)) { message = "That Search area is outside the operational area."; return false; }
            int centerRange = HexCoord.Distance(searcher.Position, center);
            int maximumRange = SearchRangeFor(searcher, mode);
            if (centerRange > maximumRange) { message = $"{mode} Search range is {maximumRange} hexes ({maximumRange * Area.NauticalMilesPerHex} nm)."; return false; }
            SideState side = Sides[searcher.Side];
            if (mode == SearchMode.Focused && searcher.HasEffect("D-08")) { message = "Jammed Circuits prevents Focused Search until Recover."; return false; }
            bool freeFocused = mode == SearchMode.Focused && searcher.FreeFocusedSearch;
            int attentionEstimate = ActionFollowsMission(searcher, ActionKind.Search) || TriggeredMissionFor(searcher) == ActionKind.Search ? 0 : 1;
            if (mode == SearchMode.Focused && !freeFocused && side.CommandSlots < 1 + attentionEstimate) { message = $"Focused Search needs {1 + attentionEstimate} free Command Slot{(attentionEstimate == 0 ? string.Empty : "s")} including immediate retasking."; return false; }
            if (!AuthorizeAction(searcher, ActionKind.Search, out message)) return false;
            if (mode == SearchMode.Focused && !freeFocused && !TryOccupyCommand(searcher.Side, 1, "Focused Search", searcher.Id, -1, out message)) return false;
            int searchSupport = ConsumeSupportBonus(searcher, SupportKind.Search);
            int aswSupport = ConsumeSupportBonus(searcher, SupportKind.AswSearch);

            var detected = new List<string>();
            foreach (FormationState target in Formations.Where(candidate => candidate.Side != searcher.Side && !candidate.IsDestroyed && HexCoord.Distance(center, candidate.Position) <= Rules.SearchAreaRadius && HexCoord.Distance(searcher.Position, candidate.Position) <= maximumRange))
            {
                int range = HexCoord.Distance(searcher.Position, target.Position);
                int modeModifier = SearchModifierFor(searcher, mode);
                int finalValue = searcher.EffectiveSearch + modeModifier + (target.Kind == FormationKind.Submarine ? Math.Max(searchSupport, aswSupport) : searchSupport) + target.EffectiveSignature - range;
                int required = Rules.SearchTarget(finalValue);
                int roll = Roll();
                if (roll < required) continue;
                ContactState contact = Contacts.FirstOrDefault(candidate => candidate.Owner == searcher.Side && candidate.TargetId == target.Id);
                if (contact == null)
                {
                    contact = new ContactState { Owner = searcher.Side, TargetId = target.Id, LastKnownPosition = target.Position, Location = LocationQuality.Low, Identity = IdentityQuality.Unknown };
                    Contacts.Add(contact);
                }
                else if (priority == SearchPriority.Location && contact.Location < LocationQuality.High) contact.Location++;
                else if (priority == SearchPriority.Identity && contact.Identity < IdentityQuality.Identified) contact.Identity++;
                else if (contact.Location < LocationQuality.High) contact.Location++;
                else if (contact.Identity < IdentityQuality.Identified) contact.Identity++;
                contact.LastKnownPosition = target.Position;
                contact.Age = 0;
                contact.MovementUncertainty = 0;
                contact.HasContradictoryPosition = false;
                contact.IsFalse = false;
                contact.IsLost = false;
                string label = contact.Identity == IdentityQuality.Identified ? target.Name : "Contact at " + contact.LastKnownPosition;
                detected.Add($"{label}: {contact.Summary}");
            }

            foreach (ContactState falseContact in Contacts.Where(candidate => candidate.Owner == searcher.Side && candidate.IsFalse && !candidate.IsLost && HexCoord.Distance(center, candidate.LastKnownPosition) <= Rules.SearchAreaRadius && HexCoord.Distance(searcher.Position, candidate.LastKnownPosition) <= maximumRange).ToList())
            {
                int range = HexCoord.Distance(searcher.Position, falseContact.LastKnownPosition);
                int finalValue = searcher.EffectiveSearch + SearchModifierFor(searcher, mode) + searchSupport - range;
                if (Roll() < Rules.SearchTarget(finalValue)) continue;
                falseContact.IsLost = true;
                detected.Add($"False Contact at {falseContact.LastKnownPosition} disproved");
            }

            string outcome = detected.Count == 0 ? "no detections" : string.Join("; ", detected);
            CompleteAction(searcher, ActionKind.Search, false, $"{searcher.Name} searched area {center} (radius {Rules.SearchAreaRadius}, {mode}, {priority} priority) — {outcome}.", loudAfterAction: mode == SearchMode.Active);
            message = Log[0];
            return true;
        }

        public bool Strike(FormationState attacker, FormationState target, Salvo salvo, Reaction reaction, out CombatResult result, out string message)
            => Strike(attacker, target, salvo, reaction, null, out result, out message);

        public bool Strike(FormationState attacker, FormationState target, Salvo salvo, Reaction reaction, HexCoord? evadeDestination, out CombatResult result, out string message)
        {
            ContactState contact = target == null ? null : ContactFor(attacker.Side, target.Id);
            if (contact == null) { result = null; message = "A usable Contact is required to Strike."; return false; }
            return StrikeContact(attacker, contact, contact.LastKnownPosition, salvo, reaction, evadeDestination, out result, out message);
        }

        public bool StrikeContact(FormationState attacker, ContactState contact, HexCoord aim, Salvo salvo, Reaction reaction, HexCoord? evadeDestination, out CombatResult result, out string message)
        {
            result = null;
            if (attacker != Active) { message = "Only the highlighted Ready formation may act."; return false; }
            if (contact == null || contact.Owner != attacker.Side || contact.IsLost || !Contacts.Contains(contact)) { message = "Choose one of your usable Contacts."; return false; }
            if (!ContactPossibleHexes(contact).Contains(aim)) { message = "Choose an aim hex inside the Contact's possible area."; return false; }
            int strikeRange = Rules.StrikeRange(attacker.Kind, salvo);
            if (HexCoord.Distance(attacker.Position, aim) > strikeRange) { message = $"{attacker.Kind} {salvo} Strike range is {strikeRange} hexes ({strikeRange * Area.NauticalMilesPerHex} nm)."; return false; }
            if (salvo == Salvo.Heavy && !attacker.CanHeavySalvo)
            { message = "Heavy Salvo is unavailable to this formation."; return false; }
            FormationState target = contact.IsFalse ? null : Find(contact.TargetId);
            if (target == null || target.IsDestroyed || !target.Position.Equals(aim))
            {
                if (!AuthorizeAction(attacker, ActionKind.Strike, out message)) return false;
                int missedAttack = attacker.EffectiveStrike + Rules.SalvoModifier(salvo) + ConsumeSupportBonus(attacker, SupportKind.Strike) + Rules.TargetingModifier(contact, AgeTwoTargetingPenalty);
                if (salvo == Salvo.Heavy) attacker.WeaponExpended = true;
                if (attacker.HasEffect("F-04")) DegradeEndurance(attacker);
                result = new CombatResult { Attack = missedAttack, Damage = DamageState.None, ResultingDamage = DamageState.None, Reaction = Reaction.None };
                CompleteAction(attacker, ActionKind.Strike, false, $"{attacker.Name} struck Contact area {aim}: no confirmed effect.");
                message = Log[0];
                return true;
            }
            IReadOnlyList<Reaction> legalReactions = AvailableReactions(attacker, target);
            Reaction resolvedReaction = reaction;
            if (!legalReactions.Contains(resolvedReaction)) { message = $"{resolvedReaction} is not a legal Reaction for {target.Name}."; return false; }

            int evadeAllowance = resolvedReaction == Reaction.Evade ? (target.OrderlyWithdrawalReady ? 2 : 1) : 0;
            List<HexCoord> legalEvadeDestinations = evadeAllowance > 0 ? LegalEvadeDestinations(attacker, target, evadeAllowance).ToList() : new List<HexCoord>();
            if (resolvedReaction == Reaction.Evade && evadeDestination.HasValue && !legalEvadeDestinations.Contains(evadeDestination.Value))
            { message = "Choose a legal Evade destination farther from the attacker."; return false; }
            if (!AuthorizeAction(attacker, ActionKind.Strike, out message)) return false;
            int salvoModifier = salvo == Salvo.Standard && attacker.HasEffect("X-11") ? Rules.SalvoModifier(Salvo.Light) : Rules.SalvoModifier(salvo);
            int strikeSupport = ConsumeSupportBonus(attacker, SupportKind.Strike);
            int defenseSupport = ConsumeSupportBonus(target, SupportKind.Defense);
            int screenDefense = ConsumeScreenDefenseBonus(target);
            int attack = attacker.EffectiveStrike + salvoModifier + strikeSupport + Rules.TargetingModifier(contact, AgeTwoTargetingPenalty);
            int defense = target.EffectiveDefense + (resolvedReaction == Reaction.Defend || resolvedReaction == Reaction.Evade ? 1 : 0) + defenseSupport + screenDefense - (target.Destruction ? 1 : 0);
            int difference = attack - defense;
            CombatBand band = Rules.BandFor(difference);
            int roll = Roll();
            DamageState damage = Rules.DamageFor(band, roll);
            DamageState resultingDamage = ApplyDamage(target, damage);
            target.ReactionDefenseBonus = 0;
            if (salvo == Salvo.Heavy) attacker.WeaponExpended = true;
            if (attacker.HasEffect("F-04")) DegradeEndurance(attacker);
            bool withdrew = false;
            HexCoord withdrawalDestination = target.Position;
            if (resolvedReaction != Reaction.None) target.HasReacted = true;
            if (resolvedReaction == Reaction.Evade)
            {
                if (!target.IsDestroyed)
                {
                    withdrawalDestination = evadeDestination ?? (legalEvadeDestinations.Count > 0 ? legalEvadeDestinations[0] : target.Position);
                    withdrew = !withdrawalDestination.Equals(target.Position);
                    if (withdrew)
                    {
                        target.Position = withdrawalDestination;
                        contact.LastKnownPosition = withdrawalDestination;
                        contact.Age = 0;
                        contact.HasContradictoryPosition = false;
                    }
                }
                if (target.OrderlyWithdrawalReady) target.OrderlyWithdrawalReady = false;
            }

            bool counterattacked = false;
            int counterattackRoll = 0;
            DamageState counterattackDamage = DamageState.None;
            DamageState counterattackResultingDamage = DamageState.None;
            string counterattack = string.Empty;
            if (resolvedReaction == Reaction.Counterattack && !target.IsDestroyed)
            {
                ContactState returnContact = ContactFor(target.Side, attacker.Id);
                int returnAttack = target.EffectiveStrike + Rules.SalvoModifier(Salvo.Light) + Rules.TargetingModifier(returnContact, AgeTwoTargetingPenalty);
                int returnDefense = attacker.EffectiveDefense - (attacker.Destruction ? 1 : 0);
                CombatBand returnBand = Rules.BandFor(returnAttack - returnDefense);
                counterattackRoll = Roll();
                counterattackDamage = Rules.DamageFor(returnBand, counterattackRoll);
                counterattackResultingDamage = ApplyDamage(attacker, counterattackDamage);
                counterattacked = true;
                counterattack = $" Counterattack: {returnAttack} vs {returnDefense}, {returnBand}, rolled {counterattackRoll} — {DamageResolutionText(counterattackDamage, counterattackResultingDamage)}.";
            }

            result = new CombatResult { Attack = attack, Defense = defense, Difference = difference, Band = band, Roll = roll, Damage = damage, ResultingDamage = resultingDamage, Reaction = resolvedReaction, Withdrew = withdrew, WithdrawalDestination = withdrawalDestination, Counterattacked = counterattacked, CounterattackRoll = counterattackRoll, CounterattackDamage = counterattackDamage, CounterattackResultingDamage = counterattackResultingDamage };
            string withdrawal = resolvedReaction == Reaction.Evade ? withdrew ? $" Evaded to {withdrawalDestination}." : " Evade had no legal safer destination." : string.Empty;
            string targetLabel = contact.Identity == IdentityQuality.Identified ? target.Name : $"Contact area {aim}";
            CompleteAction(attacker, ActionKind.Strike, false, $"{attacker.Name} struck {targetLabel}: {attack} vs {defense}, {band}, rolled {roll} — {DamageResolutionText(damage, resultingDamage)}. Reaction: {resolvedReaction}.{withdrawal}{counterattack}");
            message = Log[0];
            return true;
        }

        public Reaction ReactionFor(FormationState target, Reaction fallback = Reaction.Defend)
            => target == null || target.HasReacted || target.Replenishing ? Reaction.None : fallback;

        public static ReactionControl ReactionController(OperationMode mode, Side humanSide, Side defenderSide)
            => mode == OperationMode.LocalHotseat ? ReactionControl.HumanHandoff : defenderSide == humanSide ? ReactionControl.HumanDirect : ReactionControl.Ai;

        public IReadOnlyList<Reaction> AvailableReactions(FormationState attacker, FormationState target)
        {
            if (attacker == null || target == null || target.IsDestroyed || target.HasReacted || target.Replenishing) return new[] { Reaction.None };
            if (target.Damage == DamageState.Crippled) return new[] { Reaction.Defend, Reaction.Hold };
            var reactions = new List<Reaction> { Reaction.Defend, Reaction.Evade, Reaction.Hold };
            ContactState returnContact = ContactFor(target.Side, attacker.Id);
            bool counterattackLegal = target.Damage != DamageState.Crippled && target.EntropySources < 2 && !target.HasEffect("X-09") &&
                returnContact != null && HexCoord.Distance(target.Position, returnContact.LastKnownPosition) <= Rules.StrikeRange(target.Kind, Salvo.Light);
            if (counterattackLegal) reactions.Insert(2, Reaction.Counterattack);
            return reactions;
        }

        public IReadOnlyList<HexCoord> LegalEvadeDestinations(FormationState attacker, FormationState target, int allowance = 1)
        {
            if (attacker == null || target == null || target.IsDestroyed || allowance < 1) return new List<HexCoord>();
            ContactState threatContact = ContactFor(target.Side, attacker.Id);
            HexCoord threatPosition = threatContact?.LastKnownPosition ?? target.Position;
            int currentRange = HexCoord.Distance(threatPosition, target.Position);
            return Enumerable.Range(0, Area.Width)
                .SelectMany(q => Enumerable.Range(0, Area.Height).Select(r => new HexCoord(q, r)))
                .Where(Area.Contains)
                .Where(candidate => HexCoord.Distance(target.Position, candidate) >= 1 && HexCoord.Distance(target.Position, candidate) <= allowance)
                .Where(candidate => TryGetMovePath(target, candidate, allowance == 1 ? MoveMode.Cautious : MoveMode.Normal, out _, out _))
                .Where(candidate => threatContact == null || HexCoord.Distance(threatPosition, candidate) > currentRange)
                .OrderByDescending(candidate => threatContact == null ? 0 : HexCoord.Distance(threatPosition, candidate))
                .ThenByDescending(candidate => HexCoord.Distance(target.Position, candidate))
                .ThenBy(candidate => candidate.Q)
                .ThenBy(candidate => candidate.R)
                .ToList();
        }

        public bool Hold(FormationState formation, out string message)
        {
            if (formation != Active) { message = "Only the highlighted Ready formation may act."; return false; }
            if (!AuthorizeAction(formation, ActionKind.Hold, out message)) return false;
            if (formation.HasEffect("F-11")) ResolveAttachedEffect(formation, "F-11");
            CompleteAction(formation, ActionKind.Hold, false, $"{formation.Name} held position and went quiet.");
            message = Log[0];
            return true;
        }

        public bool Recover(FormationState formation, out string message)
        {
            EntropySource source = formation != null && formation.Friction ? EntropySource.Friction : EntropySource.Disruption;
            return Recover(formation, source, null, out message);
        }

        public bool Recover(FormationState formation, EntropySource source, string cardId, out string message)
        {
            if (formation != Active) { message = "Only the highlighted Ready formation may act."; return false; }
            if (source == EntropySource.Destruction || !IsEntropyMarked(formation, source)) { message = "Choose an attached Friction or Disruption card to Recover."; return false; }
            if (!string.IsNullOrEmpty(cardId) && (!(formation.ActiveEffectCardIds?.Contains(cardId) ?? false) || EntropyEffectCatalog.Find(cardId)?.Source != source)) { message = "That card is not attached to this Formation."; return false; }
            if (!AuthorizeAction(formation, ActionKind.Recover, out message)) return false;
            int additionalTime = (formation.HasEffect("F-06") && !IsLogisticsSupported(formation.Position) ? 1 : 0) + (formation.HasEffect("X-10") ? 1 : 0);
            string discarded = DiscardOneEntropyEffect(formation, source, cardId);
            formation.SupportBlockedUntilRecover = false;
            CompleteAction(formation, ActionKind.Recover, false, $"{formation.Name} recovered one {source} effect{(string.IsNullOrEmpty(discarded) ? string.Empty : " (" + discarded + ")")}. {(IsEntropyMarked(formation, source) ? "Additional matching cards remain." : "The source is now clear.")}", additionalTime);
            message = Log[0];
            return true;
        }

        private bool AuthorizeAction(FormationState formation, ActionKind action, out string message)
        {
            bool complex = Rules.IsComplexAction(action);
            bool consumePushThrough = false;
            if (complex && formation.EntropySources >= 3)
            {
                if (!formation.PushThroughReady) { message = "A Disorganized Formation must Push Through before attempting a complex Action."; return false; }
                consumePushThrough = true;
            }
            if (ActionFollowsMission(formation, action))
            {
                if (consumePushThrough) formation.PushThroughReady = false;
                formation.LastActionFollowedMission = true;
                message = "Standing Mission followed without Command Attention.";
                return true;
            }
            ActionKind? triggered = TriggeredMissionFor(formation);
            if (triggered == action)
            {
                if (formation.HasEffect("D-04") && !formation.TriggerMissionCommandReady) { message = "Broken Link blocks this trigger-authorized Mission change without Mission Command."; return false; }
                if (formation.HasEffect("D-04")) formation.TriggerMissionCommandReady = false;
                if (consumePushThrough) formation.PushThroughReady = false;
                formation.Mission = action;
                formation.LastActionFollowedMission = true;
                AddLog($"T{Time:00}  MISSION TRIGGER — {formation.Name} automatically changed Task to {action} under {formation.MissionTrigger}.", formation.Side);
                CommandEvent?.Invoke(formation, "MissionTrigger", $"{formation.MissionTrigger}; {action}");
                message = "Standing Mission Trigger authorized the action without Command Attention.";
                return true;
            }
            if (formation.HasEffect("D-04")) { message = "Broken Link permits only the current Mission or an authorized Trigger."; return false; }
            if (!TryOccupyCommand(formation.Side, 1, "Immediate Retask", formation.Id, -1, out message)) return false;
            if (consumePushThrough) formation.PushThroughReady = false;
            formation.LastActionFollowedMission = false;
            AddLog($"T{Time:00}  COMMAND OCCUPIED — {formation.Name} received an immediate out-of-Mission {action} order until action resolution.", formation.Side);
            CommandEvent?.Invoke(formation, "OutOfMissionAction", action.ToString());
            return true;
        }

        private void EnsureCommandSlotStates()
        {
            foreach (SideState side in Sides.Values)
            {
                if (side.SlotStates == null) side.SlotStates = new List<CommandSlotState>();
                while (side.SlotStates.Count < 3) side.SlotStates.Add(new CommandSlotState { Index = side.SlotStates.Count + 1, Status = CommandSlotStatus.Free });
                if (side.SlotStates.Count > 3) side.SlotStates.RemoveRange(3, side.SlotStates.Count - 3);
                ReconcileStrainedSlots(side);
                SyncCommandCount(side);
            }
        }

        private bool TryOccupyCommand(Side sideValue, int count, string purpose, string formationId, int releaseTime, out string message)
        {
            EnsureCommandSlotStates();
            SideState side = Sides[sideValue];
            List<CommandSlotState> free = side.SlotStates.Where(slot => slot.Status == CommandSlotStatus.Free).OrderBy(slot => slot.Index).Take(count).ToList();
            if (free.Count < count) { message = $"{count} free Command Slot{(count == 1 ? " is" : "s are")} required; {side.CommandSlots} available."; return false; }
            foreach (CommandSlotState slot in free)
            {
                slot.Status = CommandSlotStatus.Occupied;
                slot.Purpose = purpose;
                slot.FormationId = formationId;
                slot.ReleaseTime = releaseTime;
            }
            SyncCommandCount(side);
            AddLog($"T{Time:00}  SLOT OCCUPIED — {sideValue} assigned {count} Command Slot{(count == 1 ? string.Empty : "s")} to {purpose}{(string.IsNullOrEmpty(formationId) ? string.Empty : " for " + formationId)}.", sideValue);
            CommandEvent?.Invoke(Find(formationId), "SlotOccupied", $"{count}; {purpose}; release {(releaseTime < 0 ? "after Action" : "T" + releaseTime.ToString("00"))}");
            message = $"Occupied {count} Command Slot{(count == 1 ? string.Empty : "s")} for {purpose}.";
            return true;
        }

        private void ReleaseCommandForFormation(FormationState formation)
        {
            if (formation == null) return;
            SideState side = Sides[formation.Side];
            List<CommandSlotState> releasing = side.SlotStates.Where(slot => slot.Status == CommandSlotStatus.Occupied && slot.FormationId == formation.Id && slot.ReleaseTime < 0).ToList();
            foreach (CommandSlotState slot in releasing) FreeSlot(slot);
            ReconcileStrainedSlots(side);
            SyncCommandCount(side);
            if (releasing.Count > 0) AddLog($"T{Time:00}  SLOT RELEASED — {formation.Side} freed {releasing.Count} Command Slot{(releasing.Count == 1 ? string.Empty : "s")} after {formation.Name}'s Action.", formation.Side);
            if (releasing.Count > 0) CommandEvent?.Invoke(formation, "SlotReleased", $"{releasing.Count}; after Action");
        }

        private void ReleaseTimedCommandAndDeliverMissions()
        {
            foreach (FormationState formation in Formations.Where(candidate => candidate.PendingMissionChange && candidate.MissionDeliveryTime <= Time).ToList())
            {
                ApplyStandingMission(formation, formation.PendingMissionTask, formation.PendingMissionObjective, formation.PendingMissionObjectiveId, formation.PendingMissionObjectiveHex, formation.PendingMissionPosture, formation.PendingMissionTrigger);
                formation.PendingMissionChange = false;
                if (formation.HasEffect("F-12")) formation.NextReadyTimeBonus++;
                AddLog($"T{Time:00}  MISSION DELIVERED — {formation.Name}'s delayed order is now active.", formation.Side);
                CommandEvent?.Invoke(formation, "MissionDelivered", formation.Mission.ToString());
            }
            foreach (SideState side in Sides.Values)
            {
                List<CommandSlotState> releasing = side.SlotStates.Where(slot => slot.Status == CommandSlotStatus.Occupied && slot.ReleaseTime >= 0 && slot.ReleaseTime <= Time).ToList();
                foreach (CommandSlotState slot in releasing) FreeSlot(slot);
                ReconcileStrainedSlots(side);
                SyncCommandCount(side);
                if (releasing.Count > 0) AddLog($"T{Time:00}  SLOT RELEASED — {side.Side} freed {releasing.Count} timed Command Slot{(releasing.Count == 1 ? string.Empty : "s")}.", side.Side);
                if (releasing.Count > 0) CommandEvent?.Invoke(null, "SlotReleased", $"{side.Side}; {releasing.Count}; timed");
            }
        }

        private void MarkCommandStrain(Side sideValue, int amount, string reason)
        {
            SideState side = Sides[sideValue];
            side.CommandStrain = Math.Min(6, side.CommandStrain + Math.Max(0, amount));
            ReconcileStrainedSlots(side);
            SyncCommandCount(side);
            AddLog($"T{Time:00}  COMMAND STRAIN — {sideValue} marked {amount} for {reason}; total {side.CommandStrain}, free Slots {side.CommandSlots}/3.", sideValue);
            CommandEvent?.Invoke(null, "CommandStrain", $"{sideValue}; +{amount}; {reason}; total {side.CommandStrain}");
        }

        private static void ApplyStandingMission(FormationState formation, ActionKind task, MissionObjectiveKind objective, string objectiveId, HexCoord objectiveHex, MissionPosture posture, MissionTrigger trigger)
        {
            formation.Mission = task;
            formation.MissionObjective = objective;
            formation.MissionObjectiveId = objectiveId;
            formation.MissionObjectiveHex = objectiveHex;
            formation.MissionPosture = posture;
            formation.MissionTrigger = trigger;
        }

        private static void FreeSlot(CommandSlotState slot)
        {
            slot.Status = CommandSlotStatus.Free;
            slot.Purpose = null;
            slot.FormationId = null;
            slot.ReleaseTime = -1;
        }

        private static void ReconcileStrainedSlots(SideState side)
        {
            int desired = Math.Min(3, Math.Max(0, side.CommandStrain) / 2);
            List<CommandSlotState> strained = side.SlotStates.Where(slot => slot.Status == CommandSlotStatus.Strained).OrderByDescending(slot => slot.Index).ToList();
            foreach (CommandSlotState slot in strained.Skip(desired)) FreeSlot(slot);
            int missing = desired - side.SlotStates.Count(slot => slot.Status == CommandSlotStatus.Strained);
            foreach (CommandSlotState slot in side.SlotStates.Where(slot => slot.Status == CommandSlotStatus.Free).OrderByDescending(slot => slot.Index).Take(Math.Max(0, missing)))
            {
                slot.Status = CommandSlotStatus.Strained;
                slot.Purpose = "Command Strain";
                slot.FormationId = null;
                slot.ReleaseTime = -1;
            }
        }

        private static void SyncCommandCount(SideState side) => side.CommandSlots = side.SlotStates.Count(slot => slot.Status == CommandSlotStatus.Free);

        private void CompleteAction(FormationState formation, ActionKind action, bool generatedFriction, string entry, int additionalTime = 0, int movementSignatureAfter = 0, bool loudAfterAction = false)
        {
            if (action != ActionKind.Patrol) ClearPatrol(formation);
            if (action != ActionKind.Support) ClearSupport(formation);
            bool acceptedRisk = formation.IgnoreEntropyNextAction;
            int cost = Rules.ActionTime(action) + additionalTime + formation.NextReadyTimeBonus;
            bool complex = Rules.IsComplexAction(action);
            if (formation.Friction && complex && !acceptedRisk) cost++;
            if (formation.HasEffect("F-01")) cost++;
            if (action == ActionKind.Strike && formation.HasEffect("F-09")) cost++;
            if (formation.HasEffect("X-12")) cost++;
            formation.ReadyTime = Time + cost;
            formation.HasReacted = false;
            if (Rules.IsMajorAction(action))
            {
                formation.MajorActions++;
                if (formation.MajorActions >= 3)
                {
                    formation.MajorActions = 0;
                    if (formation.Endurance < Endurance.Critical) formation.Endurance++;
                }
            }
            if (generatedFriction && !formation.Friction) MarkEntropy(formation, EntropySource.Friction);
            if (acceptedRisk) MarkEntropy(formation, EntropySource.Friction);
            formation.CommandBonus = 0;
            formation.MoveBonus = 0;
            formation.SignatureBonus = 0;
            formation.MovementSignatureModifier = movementSignatureAfter;
            formation.Loud = loudAfterAction;
            formation.FreeFocusedSearch = false;
            formation.SuppressDestructionNextAction = false;
            formation.IgnoreEntropyNextAction = false;
            formation.NextReadyTimeBonus = 0;
            formation.MissionChangeLockedUntilAction = false;
            formation.CompletedActions++;
            EntropyResponseWindows.RemoveAll(window => window.FormationId == formation.Id && formation.CompletedActions >= window.ExpiresAfterCompletedActions);
            if (formation.HasEffect("F-02") && !EntropyResponseWindows.Any(window => window.FormationId == formation.Id && window.CardId == "F-02")) ResolveAttachedEffect(formation, "F-02");
            if (formation.Damage == DamageState.Light && formation.LightDamageExpiresAfterAction > 0 && formation.CompletedActions >= formation.LightDamageExpiresAfterAction)
            {
                formation.Damage = DamageState.None;
                formation.LightDamageExpiresAfterAction = 0;
            }
            ReleaseCommandForFormation(formation);
            AddLog($"T{Time:00}  {entry} Next Ready T{formation.ReadyTime:00}.", action == ActionKind.Strike ? (Side?)null : formation.Side);
            lastActingSide = formation.Side;
            ActionCompleted?.Invoke(formation, action);
            AdvanceToNextFormation();
        }

        private void AdvanceToNextFormation()
        {
            FormationState next = Rules.NextReady(Formations, lastActingSide);
            if (next == null) { Active = null; return; }
            if (next.ReadyTime > Time)
            {
                int delta = next.ReadyTime - Time;
                Time = next.ReadyTime;
                foreach (ContactState contact in Contacts.Where(c => !c.IsLost))
                {
                    int previousAge = contact.Age;
                    contact.Age += delta;
                    int degradationSteps = Math.Max(0, contact.Age - 2) - Math.Max(0, previousAge - 2);
                    for (int step = 0; step < degradationSteps && !contact.IsLost; step++)
                    {
                        if (contact.Location == LocationQuality.Low) contact.IsLost = true;
                        else contact.Location--;
                    }
                }
            }
            ReleaseTimedCommandAndDeliverMissions();
            Active = Rules.NextReady(Formations, lastActingSide);
            if (Active != null && Active.Replenishing && Active.ReadyTime <= Time) Active.Replenishing = false;
        }

        private DamageState ApplyDamage(FormationState target, DamageState damage)
        {
            if (damage == DamageState.None) return target.Damage;
            DamageState previous = target.Damage;
            DamageState combined = Rules.CombineDamage(previous, damage);
            target.Damage = combined;
            if (combined == DamageState.Light)
            {
                target.LightDamageExpiresAfterAction = target.CompletedActions + (target == Active ? 2 : 1);
            }
            else target.LightDamageExpiresAfterAction = 0;
            if (combined >= DamageState.Heavy && combined > previous) MarkEntropy(target, EntropySource.Destruction);
            if (target.IsDestroyed)
            {
                ClearPatrol(target);
                ClearSupport(target);
                foreach (FormationState formation in Formations.Where(candidate => candidate.SupportRecipientId == target.Id)) ClearSupport(formation);
            }
            return target.Damage;
        }

        private static string DamageResolutionText(DamageState rolled, DamageState resulting)
            => rolled == resulting ? rolled.ToString() : rolled == DamageState.None ? $"no damage; remains {resulting}" : $"{rolled}; cumulative state {resulting}";

        private FormationState EligibleSupporter(FormationState recipient, SupportKind kind)
        {
            if (recipient == null || recipient.IsDestroyed) return null;
            return Formations.Where(candidate => !candidate.IsDestroyed && candidate.Side == recipient.Side && candidate.SupportActive && candidate.SupportRecipientId == recipient.Id && candidate.SupportKind == kind)
                .Where(candidate => HexCoord.Distance(candidate.Position, recipient.Position) <= Rules.SupportRange)
                .OrderBy(candidate => candidate.Id, StringComparer.Ordinal).FirstOrDefault();
        }

        private FormationState EligibleScreen(FormationState recipient, bool defensiveOnly)
        {
            if (recipient == null || recipient.IsDestroyed) return null;
            return Formations.Where(candidate => !candidate.IsDestroyed && candidate.Side == recipient.Side && candidate.PatrolActive)
                .Where(candidate => !defensiveOnly || candidate.PatrolPosture == PatrolPosture.Defensive)
                .Where(candidate => HexCoord.Distance(candidate.PatrolCenter, recipient.Position) <= Rules.PatrolRadius)
                .OrderBy(candidate => candidate.Id, StringComparer.Ordinal).FirstOrDefault();
        }

        private int ConsumeSupportBonus(FormationState recipient, SupportKind kind)
        {
            FormationState supporter = EligibleSupporter(recipient, kind);
            if (supporter == null) return 0;
            ClearSupport(supporter);
            if (recipient.HasEffect("F-10")) { ResolveAttachedEffect(recipient, "F-10"); return 0; }
            if (recipient.HasEffect("D-06")) { ResolveAttachedEffect(recipient, "D-06"); return 0; }
            return 1;
        }

        private int ConsumeScreenDefenseBonus(FormationState recipient)
        {
            FormationState screener = EligibleScreen(recipient, true);
            if (screener == null) return 0;
            if (recipient.HasEffect("F-10")) { ResolveAttachedEffect(recipient, "F-10"); return 0; }
            return 1;
        }

        private string ResolvePatrolInterception(FormationState movingEnemy)
        {
            FormationState screener = Formations.Where(candidate => !candidate.IsDestroyed && candidate.Side != movingEnemy.Side && candidate.PatrolActive && candidate.PatrolInterceptionAvailable)
                .Where(candidate => HexCoord.Distance(candidate.PatrolCenter, movingEnemy.Position) <= Rules.PatrolRadius)
                .OrderBy(candidate => candidate.Id, StringComparer.Ordinal).FirstOrDefault();
            if (screener == null) return string.Empty;
            screener.PatrolInterceptionAvailable = false;
            ContactState contact = ContactFor(screener.Side, movingEnemy.Id);
            if (contact == null)
            {
                contact = new ContactState { Owner = screener.Side, TargetId = movingEnemy.Id, Location = LocationQuality.Low, Identity = IdentityQuality.General };
                Contacts.Add(contact);
            }
            contact.LastKnownPosition = movingEnemy.Position;
            contact.Age = 0;
            contact.MovementUncertainty = 0;
            contact.HasContradictoryPosition = false;
            contact.IsLost = false;
            int attack = screener.EffectiveStrike + (screener.PatrolPosture == PatrolPosture.Aggressive ? 1 : 0);
            int defense = movingEnemy.EffectiveDefense - (movingEnemy.Destruction ? 1 : 0);
            CombatBand band = Rules.BandFor(attack - defense);
            int roll = Roll();
            DamageState damage = Rules.DamageFor(band, roll);
            DamageState resultingDamage = ApplyDamage(movingEnemy, damage);
            return $" {screener.Name} intercepted from its {screener.PatrolPosture} Screen: {attack} vs {defense}, {band}, rolled {roll} — {DamageResolutionText(damage, resultingDamage)}.";
        }

        private void ExpireOutOfRangeSupport(FormationState moved)
        {
            foreach (FormationState supporter in Formations.Where(candidate => candidate.SupportActive && (candidate == moved || candidate.SupportRecipientId == moved.Id)).ToList())
            {
                FormationState recipient = Find(supporter.SupportRecipientId);
                if (recipient == null || HexCoord.Distance(supporter.Position, recipient.Position) > Rules.SupportRange) ClearSupport(supporter);
            }
        }

        private static void ClearPatrol(FormationState formation)
        {
            if (formation == null) return;
            formation.PatrolActive = false;
            formation.PatrolProtectedFormationId = null;
            formation.PatrolInterceptionAvailable = false;
        }

        private static void ClearSupport(FormationState formation)
        {
            if (formation == null) return;
            formation.SupportActive = false;
            formation.SupportRecipientId = null;
        }

        private void InitializeEntropyDecks(int seed)
        {
            EntropyDecks.Clear();
            var cardRandom = new Random(seed ^ 0x5EAF1978);
            foreach (EntropySource source in Enum.GetValues(typeof(EntropySource)))
            {
                List<string> drawPile = EntropyEffectCatalog.For(source).Select(card => card.Id).ToList();
                for (int index = drawPile.Count - 1; index > 0; index--)
                {
                    int swap = cardRandom.Next(index + 1);
                    string value = drawPile[index]; drawPile[index] = drawPile[swap]; drawPile[swap] = value;
                }
                EntropyDecks.Add(new EntropyDeckState { Source = source, DrawPile = drawPile });
            }
        }

        private void EnsureEntropyDeckCompleteness()
        {
            foreach (EntropySource source in Enum.GetValues(typeof(EntropySource)))
            {
                EntropyDeckState deck = EntropyDecks.FirstOrDefault(state => state.Source == source);
                if (deck == null)
                {
                    deck = new EntropyDeckState { Source = source };
                    EntropyDecks.Add(deck);
                }
                if (deck.DrawPile == null) deck.DrawPile = new List<string>();
                if (deck.DiscardPile == null) deck.DiscardPile = new List<string>();
                HashSet<string> known = new HashSet<string>(deck.DrawPile.Concat(deck.DiscardPile), StringComparer.OrdinalIgnoreCase);
                foreach (string id in EntropyEffectCatalog.For(source).Select(card => card.Id)) if (!known.Contains(id)) deck.DrawPile.Add(id);
            }
        }

        private void InitializeCommandResponseDecks(int gameSeed)
        {
            CommandResponseDecks.Clear();
            foreach (Side side in Enum.GetValues(typeof(Side)))
            {
                var responseRandom = new Random(gameSeed ^ 0x43A2D170 ^ ((int)side * 7919));
                List<string> drawPile = CommandResponseCatalog.All.Select(card => card.Id).ToList();
                for (int index = drawPile.Count - 1; index > 0; index--)
                {
                    int swap = responseRandom.Next(index + 1);
                    string value = drawPile[index]; drawPile[index] = drawPile[swap]; drawPile[swap] = value;
                }
                var deck = new CommandResponseDeckState { Side = side, DrawPile = drawPile };
                CommandResponseDecks.Add(deck);
                for (int draw = 0; draw < 3; draw++) DrawCommandResponse(side);
            }
        }

        public CommandResponseDefinition DrawCommandResponse(Side side)
        {
            CommandResponseDeckState deck = CommandResponseDecks.First(state => state.Side == side);
            if (deck.DrawPile.Count == 0 && deck.DiscardPile.Count > 0)
            {
                deck.DrawPile.AddRange(deck.DiscardPile);
                deck.DiscardPile.Clear();
            }
            if (deck.DrawPile.Count == 0) return null;
            string id = deck.DrawPile[0];
            deck.DrawPile.RemoveAt(0);
            deck.Hand.Add(id);
            return CommandResponseCatalog.Find(id);
        }

        private EntropyEffectDefinition DrawApplicableCard(EntropyDeckState deck, FormationKind kind)
        {
            if (deck.DrawPile.Count == 0) RefillEntropyDeck(deck);
            int attempts = deck.DrawPile.Count;
            while (attempts-- > 0)
            {
                string id = deck.DrawPile[0];
                deck.DrawPile.RemoveAt(0);
                EntropyEffectDefinition card = EntropyEffectCatalog.Find(id);
                if (EntropyEffectCatalog.AppliesTo(card, kind)) return card;
                deck.DiscardPile.Add(id);
            }
            return null;
        }

        private static void RefillEntropyDeck(EntropyDeckState deck)
        {
            if (deck.DiscardPile == null || deck.DiscardPile.Count == 0) return;
            // Deterministic cut keeps save/replay behavior stable while modeling a physical discard reshuffle.
            for (int index = deck.DiscardPile.Count - 1; index >= 0; index--) deck.DrawPile.Add(deck.DiscardPile[index]);
            deck.DiscardPile.Clear();
        }

        private void ClearEntropy(FormationState formation, EntropySource source)
        {
            SetEntropyMarked(formation, source, false);
            if (formation.ActiveEffectCardIds == null) return;
            EntropyDeckState deck = EntropyDecks.First(state => state.Source == source);
            foreach (string id in formation.ActiveEffectCardIds.Where(id => EntropyEffectCatalog.Find(id)?.Source == source).ToList())
            {
                formation.ActiveEffectCardIds.Remove(id);
                formation.ResolvedEffectCardIds?.Remove(id);
                EntropyResponseWindows.RemoveAll(window => window.FormationId == formation.Id && window.CardId == id);
                deck.DiscardPile.Add(id);
            }
        }

        private string DiscardOneEntropyEffect(FormationState formation, EntropySource source, string requestedId = null)
        {
            string id = !string.IsNullOrEmpty(requestedId) ? requestedId : (formation.ActiveEffectCardIds ?? new List<string>()).FirstOrDefault(candidate => EntropyEffectCatalog.Find(candidate)?.Source == source);
            if (string.IsNullOrEmpty(id))
            {
                SetEntropyMarked(formation, source, false);
                return string.Empty;
            }
            formation.ActiveEffectCardIds.Remove(id);
            formation.ResolvedEffectCardIds?.Remove(id);
            EntropyResponseWindows.RemoveAll(window => window.FormationId == formation.Id && window.CardId == id);
            EntropyDecks.First(deck => deck.Source == source).DiscardPile.Add(id);
            bool remains = formation.ActiveEffectCardIds.Any(candidate => EntropyEffectCatalog.Find(candidate)?.Source == source);
            SetEntropyMarked(formation, source, remains);
            return id;
        }

        private bool ApplyCommandResponse(CommandResponseDefinition card, Side side, FormationState formation, ContactState contact, HexCoord? hex, ActionKind? mission, out string message)
        {
            switch (card.Id)
            {
                case "C-01":
                    if (!RemoveMatchingEffect(formation, EntropySource.Friction)) { message = "Choose a Formation with an active Friction effect."; return false; }
                    MarkCommandStrain(side, 1, card.Title);
                    break;
                case "C-04":
                    if (formation == null || !mission.HasValue) { message = "Choose a friendly Formation and its new Mission."; return false; }
                    if (formation.Mission == mission.Value) { message = $"{formation.Name} is already assigned to {mission.Value}."; return false; }
                    if (formation.HasEffect("D-04")) { message = "Broken Link prevents this Formation from receiving a new Mission."; return false; }
                    formation.Mission = mission.Value;
                    formation.NextReadyTimeBonus++;
                    CommandEvent?.Invoke(formation, "MissionChanged", $"C-04; {mission.Value}");
                    break;
                case "C-03":
                    if (formation == null || !formation.HasEffect("D-04")) { message = "Choose a Broken Link Formation."; return false; }
                    if (formation.MissionTrigger == MissionTrigger.OnReady) { message = "Assign a conditional Standing Mission Trigger before preparing Mission Command."; return false; }
                    formation.TriggerMissionCommandReady = true;
                    break;
                case "C-05":
                    if (formation == null) { message = "Choose a friendly Formation."; return false; }
                    formation.FreeFocusedSearch = true;
                    break;
                case "C-06":
                    if (formation == null || !formation.Destruction) { message = "Choose a Formation with Destruction."; return false; }
                    formation.SuppressDestructionNextAction = true;
                    break;
                case "C-07":
                case "C-22":
                    if (formation == null) { message = "Choose a friendly Formation."; return false; }
                    formation.CommandBonus++;
                    if (card.Id == "C-07") formation.MissionChangeLockedUntilAction = true;
                    break;
                case "C-08":
                    if (formation == null || formation.EntropySources == 0) { message = "Choose a Formation affected by Entropy."; return false; }
                    formation.IgnoreEntropyNextAction = true;
                    break;
                case "C-09":
                    if (!RemoveSpecificEffect(formation, "D-02", "F-02")) { message = "Choose a Formation with Communications Latency or Staff Overload."; return false; }
                    break;
                case "C-11":
                    if (formation == null || !formation.Destruction) { message = "Choose a Formation with Destruction."; return false; }
                    formation.SuppressDestructionNextAction = true;
                    break;
                case "C-12":
                    if (contact == null || contact.Owner != side || contact.IsLost) { message = "Choose one of your usable Contacts."; return false; }
                    contact.Age = 0;
                    break;
                case "C-13":
                    if (!RemoveSpecificEffect(formation, "F-01", "F-12")) { message = "Choose a Formation with Delayed Execution or Hasty Retasking."; return false; }
                    break;
                case "C-14":
                    if (formation == null) { message = "Choose a friendly Formation."; return false; }
                    formation.MoveBonus++;
                    formation.SignatureBonus++;
                    break;
                case "C-15":
                    if (!RemoveSpecificEffect(formation, "D-08", "D-05")) { message = "Choose a Formation with Jammed Circuits or Sensor Saturation."; return false; }
                    break;
                case "C-16":
                    if (formation == null) { message = "Choose a friendly Formation."; return false; }
                    formation.ReactionDefenseBonus++;
                    break;
                case "C-17":
                    if (formation == null || !formation.HasEffect("D-04") || !mission.HasValue) { message = "Choose a Broken Link Formation and its new Mission."; return false; }
                    if (formation.Mission == mission.Value) { message = $"{formation.Name} is already assigned to {mission.Value}."; return false; }
                    formation.Mission = mission.Value;
                    if (formation.HasEffect("F-12")) formation.NextReadyTimeBonus++;
                    MarkCommandStrain(side, 1, card.Title);
                    CommandEvent?.Invoke(formation, "MissionChanged", $"C-17; {mission.Value}");
                    break;
                case "C-18":
                    if (formation == null) { message = "Choose a friendly Formation to prepare for withdrawal."; return false; }
                    if (formation.OrderlyWithdrawalReady) { message = "That Formation already has an Orderly Withdrawal prepared."; return false; }
                    formation.OrderlyWithdrawalReady = true;
                    break;
                case "C-19":
                    if (formation == null || formation.Endurance == Endurance.Ready) { message = "Choose a Formation with degraded Endurance."; return false; }
                    formation.Endurance--;
                    break;
                case "C-20":
                    if (formation == null || formation.Damage == DamageState.None || (!formation.Friction && !formation.Disruption)) { message = "Choose a damaged Formation with Friction or Disruption."; return false; }
                    RemoveMatchingEffect(formation, formation.Friction ? EntropySource.Friction : EntropySource.Disruption);
                    formation.NextReadyTimeBonus++;
                    break;
                case "C-21":
                    if (!hex.HasValue || !Area.Contains(hex.Value) || !Formations.Any(candidate => candidate.Side == side && HexCoord.Distance(candidate.Position, hex.Value) <= 2)) { message = "Choose a valid hex within 2 hexes of a friendly Formation."; return false; }
                    Side observer = side == Side.Blue ? Side.Red : Side.Blue;
                    Contacts.Add(new ContactState { Owner = observer, TargetId = NextFalseContactId(), LastKnownPosition = hex.Value, Location = LocationQuality.Low, Identity = IdentityQuality.Unknown, IsFalse = true });
                    break;
                case "C-23":
                    if (contact == null || contact.Owner != side || contact.IsLost || contact.Location == LocationQuality.High) { message = "Choose one of your Contacts below High Location."; return false; }
                    contact.Location++;
                    break;
                case "C-24":
                    if (formation == null) { message = "Choose the friendly Formation that will receive the reassigned Screen or Support."; return false; }
                    FormationState assignment = Formations.Where(candidate => candidate.Side == side && !candidate.IsDestroyed && candidate.SupportActive && candidate.SupportRecipientId != formation.Id)
                        .Where(candidate => HexCoord.Distance(candidate.Position, formation.Position) <= Rules.SupportRange)
                        .OrderBy(candidate => candidate.Id, StringComparer.Ordinal).FirstOrDefault();
                    if (assignment != null) assignment.SupportRecipientId = formation.Id;
                    else
                    {
                        assignment = Formations.Where(candidate => candidate.Side == side && !candidate.IsDestroyed && candidate.PatrolActive && candidate.PatrolProtectedFormationId != formation.Id)
                            .Where(candidate => HexCoord.Distance(candidate.PatrolCenter, formation.Position) <= Rules.PatrolRadius)
                            .OrderBy(candidate => candidate.Id, StringComparer.Ordinal).FirstOrDefault();
                        if (assignment == null) { message = "No active Screen or Support assignment can cover that Formation in the same area."; return false; }
                        assignment.PatrolProtectedFormationId = formation.Id;
                    }
                    break;
                default:
                    message = $"{card.Title} is not supported by the active rules model.";
                    return false;
            }
            message = $"Played {card.Id} {card.Title}.";
            return true;
        }

        private bool RemoveMatchingEffect(FormationState formation, EntropySource source)
        {
            if (formation == null) return false;
            string id = (formation.ActiveEffectCardIds ?? new List<string>()).FirstOrDefault(candidate => EntropyEffectCatalog.Find(candidate)?.Source == source);
            return RemoveAttachedEffect(formation, id);
        }

        private bool RemoveSpecificEffect(FormationState formation, params string[] ids)
        {
            if (formation == null) return false;
            string id = ids.FirstOrDefault(candidate => formation.ActiveEffectCardIds != null && formation.ActiveEffectCardIds.Contains(candidate));
            return RemoveAttachedEffect(formation, id);
        }

        private bool RemoveAttachedEffect(FormationState formation, string id)
        {
            EntropyEffectDefinition effect = EntropyEffectCatalog.Find(id);
            if (formation == null || effect == null || formation.ActiveEffectCardIds == null || !formation.ActiveEffectCardIds.Remove(id)) return false;
            formation.ResolvedEffectCardIds?.Remove(id);
            EntropyResponseWindows.RemoveAll(window => window.FormationId == formation.Id && window.CardId == id);
            bool remains = formation.ActiveEffectCardIds.Any(candidate => EntropyEffectCatalog.Find(candidate)?.Source == effect.Source);
            SetEntropyMarked(formation, effect.Source, remains);
            EntropyDecks.First(deck => deck.Source == effect.Source).DiscardPile.Add(id);
            return true;
        }

        private static void ResolveAttachedEffect(FormationState formation, string id)
        {
            if (formation?.ActiveEffectCardIds == null || !formation.ActiveEffectCardIds.Contains(id)) return;
            if (formation.ResolvedEffectCardIds == null) formation.ResolvedEffectCardIds = new List<string>();
            if (!formation.ResolvedEffectCardIds.Contains(id)) formation.ResolvedEffectCardIds.Add(id);
        }

        private HexCoord NearbyValidHex(HexCoord origin, int maximumDistance)
        {
            for (int distance = 1; distance <= maximumDistance; distance++)
                for (int q = 0; q < Area.Width; q++) for (int r = 0; r < Area.Height; r++)
                {
                    var candidate = new HexCoord(q, r);
                    if (Area.Contains(candidate) && HexCoord.Distance(origin, candidate) == distance) return candidate;
                }
            return origin;
        }

        private string NextFalseContactId()
        {
            int suffix = 1;
            while (Contacts.Any(contact => contact.TargetId == $"FALSE-{suffix:00}")) suffix++;
            return $"FALSE-{suffix:00}";
        }

        private void ApplyImmediateCardEffect(FormationState formation, EntropyEffectDefinition card)
        {
            ContactState contact = Contacts.Where(candidate => candidate.Owner == formation.Side && !candidate.IsLost)
                .OrderByDescending(candidate => candidate.Location).ThenByDescending(candidate => candidate.Identity).ThenBy(candidate => candidate.TargetId).FirstOrDefault();
            if (card.Id == "D-01" && contact != null)
            {
                if (contact.Location > LocationQuality.Low) contact.Location--;
                else if (contact.Identity > IdentityQuality.Unknown) contact.Identity--;
                ResolveAttachedEffect(formation, card.Id);
            }
            else if (card.Id == "D-07")
            {
                ContactState realContact = Contacts.Where(candidate => candidate.Owner == formation.Side && !candidate.IsLost && !candidate.IsFalse)
                    .OrderByDescending(candidate => candidate.Location).ThenByDescending(candidate => candidate.Identity).ThenBy(candidate => candidate.TargetId).FirstOrDefault();
                if (realContact != null)
                {
                    HexCoord falseHex = NearbyValidHex(realContact.LastKnownPosition, 2);
                    Contacts.Add(new ContactState { Owner = formation.Side, TargetId = NextFalseContactId(), LastKnownPosition = falseHex, Location = LocationQuality.Low, Identity = IdentityQuality.Unknown, IsFalse = true });
                    ResolveAttachedEffect(formation, card.Id);
                }
            }
            else if (card.Id == "D-03" && contact != null)
            {
                contact.HasContradictoryPosition = true;
                contact.ContradictoryPosition = NearbyValidHex(contact.LastKnownPosition, 1);
                ResolveAttachedEffect(formation, card.Id);
            }
            else if (card.Id == "D-09" && contact != null)
            {
                contact.Age++;
                ResolveAttachedEffect(formation, card.Id);
            }
            else if (card.Id == "D-10")
            {
                ContactState identified = Contacts.Where(candidate => candidate.Owner == formation.Side && !candidate.IsLost && candidate.Identity == IdentityQuality.Identified).OrderBy(candidate => candidate.TargetId).FirstOrDefault();
                if (identified != null) identified.Identity = IdentityQuality.General;
                ResolveAttachedEffect(formation, card.Id);
            }
        }

        private bool IsLogisticsSupported(HexCoord hex) => Area.Locations.Any(location => location.Hex.Equals(hex) && (location.Kind == LocationKind.Port || location.Kind == LocationKind.Airfield || location.Kind == LocationKind.Anchorage));
        private bool HasFacility(HexCoord hex, LocationKind kind) => Area.Locations.Any(location => location.Hex.Equals(hex) && location.Kind == kind);
        private static DamageState PreviousDamage(DamageState damage) => damage == DamageState.Destroyed ? DamageState.Destroyed : damage == DamageState.Crippled ? DamageState.Heavy : damage == DamageState.Heavy ? DamageState.Light : damage == DamageState.Light ? DamageState.None : DamageState.None;
        private static void DegradeEndurance(FormationState formation) { if (formation.Endurance < Endurance.Critical) formation.Endurance++; }
        private static bool IsEntropyMarked(FormationState formation, EntropySource source) => source == EntropySource.Friction ? formation.Friction : source == EntropySource.Disruption ? formation.Disruption : formation.Destruction;
        private static void SetEntropyMarked(FormationState formation, EntropySource source, bool marked)
        {
            if (source == EntropySource.Friction) formation.Friction = marked;
            else if (source == EntropySource.Disruption) formation.Disruption = marked;
            else formation.Destruction = marked;
        }
        private static EntropyDeckState CloneDeck(EntropyDeckState deck) => new EntropyDeckState { Source = deck.Source, DrawPile = new List<string>(deck.DrawPile ?? new List<string>()), DiscardPile = new List<string>(deck.DiscardPile ?? new List<string>()) };
        private static CommandResponseDeckState CloneResponseDeck(CommandResponseDeckState deck) => new CommandResponseDeckState
        {
            Side = deck.Side,
            DrawPile = new List<string>(deck.DrawPile ?? new List<string>()),
            Hand = new List<string>(deck.Hand ?? new List<string>()),
            DiscardPile = new List<string>(deck.DiscardPile ?? new List<string>())
        };
        private EntropyDrawNotice PendingNoticeFor(Side side) => PendingEntropyReveals.FirstOrDefault(notice => Find(notice.FormationId)?.Side == side);

        private int Roll() => random.Next(1, 7);
        private void AddLog(string text, Side? audience = null)
        {
            Log.Insert(0, text);
            LogEntries.Insert(0, new OperationalLogEntry { Text = text, IsPrivate = audience.HasValue, Audience = audience.GetValueOrDefault() });
            if (Log.Count > 8) Log.RemoveAt(Log.Count - 1);
            if (LogEntries.Count > 8) LogEntries.RemoveAt(LogEntries.Count - 1);
        }

        private void CreateScenario()
        {
            if (Scenario == null || Scenario.Area == null) throw new InvalidOperationException("A valid scenario and operational area are required.");
            foreach (FormationDefinition definition in Scenario.Formations)
            {
                if (!Area.Contains(new HexCoord(definition.Q, definition.R))) throw new InvalidOperationException($"Formation {definition.Id} is outside {Area.DisplayName}.");
                Formations.Add(new FormationState
                {
                    Id = definition.Id,
                    Name = definition.Name,
                    Side = definition.Side,
                    Kind = definition.Kind,
                    Position = new HexCoord(definition.Q, definition.R),
                    ReadyTime = definition.ReadyTime,
                    Endurance = Endurance.Ready,
                    Mission = definition.Kind == FormationKind.CarrierGroup ? ActionKind.Strike : definition.Kind == FormationKind.SurfaceGroup ? ActionKind.Move : ActionKind.Search,
                    MissionObjective = MissionObjectiveKind.OperationalObjective,
                    MissionObjectiveHex = Area.Objective,
                    MissionPosture = MissionPosture.Balanced,
                    MissionTrigger = definition.Kind == FormationKind.SurfaceGroup ? MissionTrigger.ObjectiveReached : MissionTrigger.ContactLocated,
                    LastActionFollowedMission = true,
                    Ratings = definition.Ratings
                });
            }
            foreach (ContactDefinition definition in Scenario.Contacts)
            {
                Contacts.Add(new ContactState { Owner = definition.Owner, TargetId = definition.TargetId, LastKnownPosition = new HexCoord(definition.Q, definition.R), Location = definition.Location, Identity = definition.Identity, Age = definition.Age });
            }
            AddLog($"Exercise {Scenario.DisplayName.ToUpperInvariant()} initialized. {Formations.Count} formations. Central objective: hold {Area.Objective} at T{Scenario.Horizon}.");
        }
    }
}
