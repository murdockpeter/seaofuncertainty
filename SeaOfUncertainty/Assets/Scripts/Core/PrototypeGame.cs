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
            public int Version = 5;
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
            public List<CommandResponseDeckState> CommandResponseDecks = new List<CommandResponseDeckState>();
            public List<string> Log = new List<string>();
        }

        private readonly Random random;
        public readonly List<FormationState> Formations = new List<FormationState>();
        public readonly List<ContactState> Contacts = new List<ContactState>();
        public readonly Dictionary<Side, SideState> Sides = new Dictionary<Side, SideState>();
        public readonly List<EntropyDeckState> EntropyDecks = new List<EntropyDeckState>();
        public readonly List<EntropyDrawNotice> PendingEntropyReveals = new List<EntropyDrawNotice>();
        public readonly List<CommandResponseDeckState> CommandResponseDecks = new List<CommandResponseDeckState>();
        public readonly List<string> Log = new List<string>();
        public event Action<FormationState, ActionKind> ActionCompleted;
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
            InitializeCommandResponseDecks(seed);
            CreateScenario();
            AdvanceToNextFormation();
        }

        public FormationState Find(string id) => Formations.FirstOrDefault(f => f.Id == id);
        public ContactState ContactFor(Side owner, string targetId) => Contacts.FirstOrDefault(c => c.Owner == owner && c.TargetId == targetId && !c.IsLost);
        public int SearchModifierFor(FormationState formation, SearchMode mode) => mode == SearchMode.Active && formation != null && formation.HasEffect("D-05") ? 0 : Rules.SearchModifier(mode);

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
                CommandResponseDecks = CommandResponseDecks.Select(CloneResponseDeck).ToList(),
                Log = new List<string>(Log)
            };
        }

        public void RestoreState(SaveData data)
        {
            if (data == null || data.Version < 1 || data.Version > 5) throw new ArgumentException("Unsupported or empty save data.");
            if (data.Version >= 2 && !string.IsNullOrEmpty(data.ScenarioId) && !string.Equals(data.ScenarioId, Scenario.Id, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException($"Save scenario {data.ScenarioId} does not match loaded scenario {Scenario.Id}.");
            Formations.Clear();
            Formations.AddRange(data.Formations ?? new List<FormationState>());
            foreach (FormationState formation in Formations)
            {
                if (formation.ActiveEffectCardIds == null) formation.ActiveEffectCardIds = new List<string>();
                if (formation.ResolvedEffectCardIds == null) formation.ResolvedEffectCardIds = new List<string>();
            }
            Contacts.Clear();
            Contacts.AddRange(data.Contacts ?? new List<ContactState>());
            Sides.Clear();
            foreach (SideState side in data.Sides ?? new List<SideState>()) Sides[side.Side] = side;
            if (!Sides.ContainsKey(Side.Blue)) Sides[Side.Blue] = new SideState { Side = Side.Blue };
            if (!Sides.ContainsKey(Side.Red)) Sides[Side.Red] = new SideState { Side = Side.Red };
            if (data.Version >= 4 && data.EntropyDecks != null && data.EntropyDecks.Count > 0)
            {
                EntropyDecks.Clear();
                EntropyDecks.AddRange(data.EntropyDecks.Select(CloneDeck));
            }
            EnsureEntropyDeckCompleteness();
            PendingEntropyReveals.Clear();
            if (data.Version >= 4 && data.PendingEntropyReveals != null) PendingEntropyReveals.AddRange(data.PendingEntropyReveals.Select(notice => new EntropyDrawNotice { CardId = notice.CardId, FormationId = notice.FormationId }));
            CommandResponseDecks.Clear();
            if (data.Version >= 5 && data.CommandResponseDecks != null && data.CommandResponseDecks.Count > 0) CommandResponseDecks.AddRange(data.CommandResponseDecks.Select(CloneResponseDeck));
            else InitializeCommandResponseDecks(seed);
            Log.Clear();
            Log.AddRange(data.Log ?? new List<string>());
            Time = data.Time;
            AgeTwoTargetingPenalty = data.AgeTwoTargetingPenalty;
            lastActingSide = data.Version >= 3 && data.HasLastActingSide ? data.LastActingSide : (Side?)null;
            Active = Find(data.ActiveFormationId) ?? Rules.NextReady(Formations, lastActingSide);
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
            if (cardId != "F-01" && cardId != "F-02" && cardId != "F-07") { message = "This effect has no immediate printed Command response."; return false; }
            SideState side = Sides[formation.Side];
            if (side.CommandSlots < 1) { message = "No free Command Slot is available."; return false; }
            side.CommandSlots--;
            ResolveAttachedEffect(formation, cardId);
            AddLog($"T{Time:00}  COMMAND RESPONSE — {formation.Name} cancelled {cardId} {EntropyEffectCatalog.Find(cardId)?.Title}.");
            message = Log[0];
            return true;
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
            string targetDetail = card.Id == "C-04" ? $" {formation.Name} is now assigned to {mission}." : card.Id == "C-18" ? $" {formation.Name} prepared an Evade and two-hex withdrawal." : string.Empty;
            AddLog($"T{Time:00}  RESPONSE PLAYED — {side}: {card.Id} {card.Title}.{targetDetail} {card.Play}{(string.IsNullOrEmpty(card.Cost) ? string.Empty : " Cost: " + card.Cost)}");
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
            ApplyImmediateCardEffect(formation, card);
            AddLog($"T{Time:00}  CARD PULL â€” {formation.Name}: {card.Id} {card.Title}. {card.Effect}");
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
            int distance = HexCoord.Distance(formation.Position, destination);
            int allowed = Rules.MoveAllowance(formation, mode);
            if (formation != Active) { message = "Only the highlighted Ready formation may act."; return false; }
            if (mode == MoveMode.HighTempo && formation.HasEffect("F-11")) { message = "Checklist Churn prevents High Tempo until this Formation Holds or Recovers."; return false; }
            if (!Area.Contains(destination)) { message = "That hex is outside the active operational area."; return false; }
            OperationalTerrain terrain = Area.TerrainAt(destination);
            if (terrain == OperationalTerrain.Land && formation.Kind != FormationKind.AirGroup) { message = "Surface and submarine formations cannot end movement in a Land hex."; return false; }
            if (distance < 1 || distance > allowed) { message = $"{mode} Move allows {allowed} hex{(allowed == 1 ? string.Empty : "es")}."; return false; }
            formation.Position = destination;
            if (formation.HasEffect("F-07")) ResolveAttachedEffect(formation, "F-07");
            formation.Loud = mode == MoveMode.HighTempo;
            if (mode == MoveMode.HighTempo) MarkEntropy(formation, EntropySource.Friction);
            if (mode == MoveMode.HighTempo && formation.HasEffect("F-04")) DegradeEndurance(formation);
            int terrainTime = terrain == OperationalTerrain.Littoral && formation.Kind != FormationKind.AirGroup ? 1 : 0;
            CompleteAction(formation, ActionKind.Move, mode == MoveMode.HighTempo, $"{formation.Name} moved {distance} hexes ({mode}){(terrainTime > 0 ? " through littoral waters" : string.Empty)}.", terrainTime);
            message = Log[0];
            return true;
        }

        public bool Search(FormationState searcher, FormationState target, SearchMode mode, out string message)
        {
            if (target == null) { message = "Search needs a Contact or an area hex."; return false; }
            return SearchArea(searcher, target.Position, mode, out message);
        }

        public bool SearchArea(FormationState searcher, HexCoord center, SearchMode mode, out string message)
        {
            if (searcher != Active) { message = "Only the highlighted Ready formation may act."; return false; }
            if (!Area.Contains(center)) { message = "That Search area is outside the operational area."; return false; }
            int centerRange = HexCoord.Distance(searcher.Position, center);
            int maximumRange = Rules.SearchRange(mode);
            if (centerRange > maximumRange) { message = $"{mode} Search range is {maximumRange} hexes ({maximumRange * Area.NauticalMilesPerHex} nm)."; return false; }
            SideState side = Sides[searcher.Side];
            if (mode == SearchMode.Focused && searcher.HasEffect("D-08")) { message = "Jammed Circuits prevents Focused Search until Recover."; return false; }
            bool freeFocused = mode == SearchMode.Focused && searcher.FreeFocusedSearch;
            if (mode == SearchMode.Focused && !freeFocused && side.CommandSlots < 1) { message = "Focused Search needs one free Command Slot."; return false; }
            if (mode == SearchMode.Focused && !freeFocused) side.CommandSlots--;
            searcher.Loud = mode == SearchMode.Active;

            var detected = new List<string>();
            foreach (FormationState target in Formations.Where(candidate => candidate.Side != searcher.Side && !candidate.IsDestroyed && HexCoord.Distance(center, candidate.Position) <= Rules.SearchAreaRadius && HexCoord.Distance(searcher.Position, candidate.Position) <= maximumRange))
            {
                int range = HexCoord.Distance(searcher.Position, target.Position);
                int modeModifier = SearchModifierFor(searcher, mode);
                int finalValue = searcher.EffectiveSearch + modeModifier + target.EffectiveSignature - range;
                int required = Rules.SearchTarget(finalValue);
                int roll = Roll();
                if (roll < required) continue;
                ContactState contact = Contacts.FirstOrDefault(candidate => candidate.Owner == searcher.Side && candidate.TargetId == target.Id);
                if (contact == null)
                {
                    contact = new ContactState { Owner = searcher.Side, TargetId = target.Id, LastKnownPosition = target.Position, Location = LocationQuality.Low, Identity = IdentityQuality.Unknown };
                    Contacts.Add(contact);
                }
                else if (contact.Location < LocationQuality.High) contact.Location++;
                else if (contact.Identity < IdentityQuality.Identified) contact.Identity++;
                contact.LastKnownPosition = target.Position;
                contact.Age = 0;
                contact.IsFalse = false;
                contact.IsLost = false;
                string label = contact.Identity == IdentityQuality.Identified ? target.Name : "Contact at " + contact.LastKnownPosition;
                detected.Add($"{label}: {contact.Summary}");
            }

            if (mode == SearchMode.Focused && !freeFocused) side.CommandSlots++;
            string outcome = detected.Count == 0 ? "no detections" : string.Join("; ", detected);
            CompleteAction(searcher, ActionKind.Search, false, $"{searcher.Name} searched area {center} (radius {Rules.SearchAreaRadius}, {mode}) — {outcome}.");
            message = Log[0];
            return true;
        }

        public bool Strike(FormationState attacker, FormationState target, Salvo salvo, Reaction reaction, out CombatResult result, out string message)
            => Strike(attacker, target, salvo, reaction, null, out result, out message);

        public bool Strike(FormationState attacker, FormationState target, Salvo salvo, Reaction reaction, HexCoord? evadeDestination, out CombatResult result, out string message)
        {
            result = null;
            if (attacker != Active) { message = "Only the highlighted Ready formation may act."; return false; }
            ContactState contact = ContactFor(attacker.Side, target.Id);
            if (contact == null) { message = "A usable Contact is required to Strike."; return false; }
            int strikeRange = Rules.StrikeRange(attacker.Kind, salvo);
            if (HexCoord.Distance(attacker.Position, contact.LastKnownPosition) > strikeRange) { message = $"{attacker.Kind} {salvo} Strike range is {strikeRange} hexes ({strikeRange * Area.NauticalMilesPerHex} nm)."; return false; }
            if (salvo == Salvo.Heavy && !attacker.CanHeavySalvo)
            { message = "Heavy Salvo is unavailable to this formation."; return false; }
            IReadOnlyList<Reaction> legalReactions = AvailableReactions(attacker, target);
            Reaction resolvedReaction = reaction;
            if (target.OrderlyWithdrawalReady && reaction == Reaction.Defend && legalReactions.Contains(Reaction.Evade)) resolvedReaction = Reaction.Evade;
            if (!legalReactions.Contains(resolvedReaction)) { message = $"{resolvedReaction} is not a legal Reaction for {target.Name}."; return false; }

            int evadeAllowance = resolvedReaction == Reaction.Evade ? (target.OrderlyWithdrawalReady ? 2 : 1) : 0;
            List<HexCoord> legalEvadeDestinations = evadeAllowance > 0 ? LegalEvadeDestinations(attacker, target, evadeAllowance).ToList() : new List<HexCoord>();
            if (resolvedReaction == Reaction.Evade && evadeDestination.HasValue && !legalEvadeDestinations.Contains(evadeDestination.Value))
            { message = "Choose a legal Evade destination farther from the attacker."; return false; }
            int salvoModifier = salvo == Salvo.Standard && attacker.HasEffect("X-11") ? Rules.SalvoModifier(Salvo.Light) : Rules.SalvoModifier(salvo);
            int attack = attacker.EffectiveStrike + salvoModifier + Rules.TargetingModifier(contact, AgeTwoTargetingPenalty);
            int defense = target.EffectiveDefense + (resolvedReaction == Reaction.Defend || resolvedReaction == Reaction.Evade ? 1 : 0) - (target.Destruction ? 1 : 0);
            int difference = attack - defense;
            CombatBand band = Rules.BandFor(difference);
            int roll = Roll();
            DamageState damage = Rules.DamageFor(band, roll);
            ApplyDamage(target, damage);
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
                    }
                }
                if (target.OrderlyWithdrawalReady) target.OrderlyWithdrawalReady = false;
            }

            bool counterattacked = false;
            int counterattackRoll = 0;
            DamageState counterattackDamage = DamageState.None;
            string counterattack = string.Empty;
            if (resolvedReaction == Reaction.Counterattack && !target.IsDestroyed)
            {
                ContactState returnContact = ContactFor(target.Side, attacker.Id);
                int returnAttack = target.EffectiveStrike + Rules.SalvoModifier(Salvo.Light) + Rules.TargetingModifier(returnContact, AgeTwoTargetingPenalty);
                int returnDefense = attacker.EffectiveDefense - (attacker.Destruction ? 1 : 0);
                CombatBand returnBand = Rules.BandFor(returnAttack - returnDefense);
                counterattackRoll = Roll();
                counterattackDamage = Rules.DamageFor(returnBand, counterattackRoll);
                ApplyDamage(attacker, counterattackDamage);
                counterattacked = true;
                counterattack = $" Counterattack: {returnAttack} vs {returnDefense}, {returnBand}, rolled {counterattackRoll} — {counterattackDamage}.";
            }

            result = new CombatResult { Attack = attack, Defense = defense, Difference = difference, Band = band, Roll = roll, Damage = damage, Reaction = resolvedReaction, Withdrew = withdrew, WithdrawalDestination = withdrawalDestination, Counterattacked = counterattacked, CounterattackRoll = counterattackRoll, CounterattackDamage = counterattackDamage };
            string withdrawal = resolvedReaction == Reaction.Evade ? withdrew ? $" Evaded to {withdrawalDestination}." : " Evade had no legal safer destination." : string.Empty;
            CompleteAction(attacker, ActionKind.Strike, false, $"{attacker.Name} struck {target.Name}: {attack} vs {defense}, {band}, rolled {roll} — {damage}. Reaction: {resolvedReaction}.{withdrawal}{counterattack}");
            message = Log[0];
            return true;
        }

        public Reaction ReactionFor(FormationState target, Reaction fallback = Reaction.Defend)
            => target == null || target.HasReacted || target.Replenishing ? Reaction.None : target.OrderlyWithdrawalReady ? Reaction.Evade : fallback;

        public static ReactionControl ReactionController(OperationMode mode, Side humanSide, Side defenderSide)
            => mode == OperationMode.LocalHotseat ? ReactionControl.HumanHandoff : defenderSide == humanSide ? ReactionControl.HumanDirect : ReactionControl.Ai;

        public IReadOnlyList<Reaction> AvailableReactions(FormationState attacker, FormationState target)
        {
            if (attacker == null || target == null || target.IsDestroyed || target.HasReacted || target.Replenishing) return new[] { Reaction.None };
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
                .Where(candidate => target.Kind == FormationKind.AirGroup || Area.TerrainAt(candidate) != OperationalTerrain.Land)
                .Where(candidate => !Formations.Any(formation => formation != target && !formation.IsDestroyed && formation.Position.Equals(candidate)))
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
            formation.Loud = false;
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
            int additionalTime = (formation.HasEffect("F-06") && !IsLogisticsSupported(formation.Position) ? 1 : 0) + (formation.HasEffect("X-10") ? 1 : 0);
            string discarded = DiscardOneEntropyEffect(formation, source, cardId);
            CompleteAction(formation, ActionKind.Recover, false, $"{formation.Name} recovered one {source} effect{(string.IsNullOrEmpty(discarded) ? string.Empty : " (" + discarded + ")")}. {(IsEntropyMarked(formation, source) ? "Additional matching cards remain." : "The source is now clear.")}", additionalTime);
            message = Log[0];
            return true;
        }

        private void CompleteAction(FormationState formation, ActionKind action, bool generatedFriction, string entry, int additionalTime = 0)
        {
            bool acceptedRisk = formation.IgnoreEntropyNextAction;
            int cost = Rules.ActionTime(action) + additionalTime + formation.NextReadyTimeBonus;
            bool complex = action == ActionKind.Move || action == ActionKind.Search || action == ActionKind.Strike || action == ActionKind.Support;
            if (formation.Friction && complex && !acceptedRisk) cost++;
            if (formation.HasEffect("F-01")) cost++;
            if (action == ActionKind.Strike && formation.HasEffect("F-09")) cost++;
            if (formation.HasEffect("X-12")) cost++;
            formation.ReadyTime = Time + cost;
            formation.HasReacted = false;
            if (action == ActionKind.Move || action == ActionKind.Search || action == ActionKind.Strike)
            {
                formation.MajorActions++;
                if (formation.MajorActions >= 3)
                {
                    formation.MajorActions = 0;
                    if (formation.Endurance < Endurance.Critical) formation.Endurance++;
                }
            }
            if (generatedFriction && !formation.Friction) MarkEntropy(formation, EntropySource.Friction);
            formation.CommandBonus = 0;
            formation.MoveBonus = 0;
            formation.SignatureBonus = 0;
            formation.FreeFocusedSearch = false;
            formation.SuppressDestructionNextAction = false;
            formation.IgnoreEntropyNextAction = false;
            formation.NextReadyTimeBonus = 0;
            if (acceptedRisk) MarkEntropy(formation, EntropySource.Friction);
            AddLog($"T{Time:00}  {entry} Next Ready T{formation.ReadyTime:00}.");
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
                    contact.Age += delta;
                    if (contact.Age >= 3)
                    {
                        if (contact.Location == LocationQuality.Low) contact.IsLost = true;
                        else contact.Location--;
                    }
                }
            }
            Active = Rules.NextReady(Formations, lastActingSide);
        }

        private void ApplyDamage(FormationState target, DamageState damage)
        {
            if (damage == DamageState.None) return;
            if (damage == DamageState.Light) target.Damage = target.Damage < DamageState.Light ? DamageState.Light : target.Damage;
            else
            {
                target.Damage = damage > target.Damage ? damage : target.Damage;
                MarkEntropy(target, EntropySource.Destruction);
            }
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
                    Sides[side].CommandStrain++;
                    break;
                case "C-04":
                    if (formation == null || !mission.HasValue) { message = "Choose a friendly Formation and its new Mission."; return false; }
                    if (formation.Mission == mission.Value) { message = $"{formation.Name} is already assigned to {mission.Value}."; return false; }
                    if (formation.HasEffect("D-04")) { message = "Broken Link prevents this Formation from receiving a new Mission."; return false; }
                    formation.Mission = mission.Value;
                    formation.NextReadyTimeBonus++;
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
            ContactState contact = Contacts.Where(candidate => candidate.Owner == formation.Side && !candidate.IsLost && !candidate.IsFalse)
                .OrderByDescending(candidate => candidate.Location).ThenByDescending(candidate => candidate.Identity).ThenBy(candidate => candidate.TargetId).FirstOrDefault();
            if (card.Id == "D-01" && contact != null)
            {
                if (contact.Location > LocationQuality.Low) contact.Location--;
                else if (contact.Identity > IdentityQuality.Unknown) contact.Identity--;
                ResolveAttachedEffect(formation, card.Id);
            }
            else if (card.Id == "D-07" && contact != null)
            {
                HexCoord falseHex = NearbyValidHex(contact.LastKnownPosition, 2);
                Contacts.Add(new ContactState { Owner = formation.Side, TargetId = NextFalseContactId(), LastKnownPosition = falseHex, Location = LocationQuality.Low, Identity = IdentityQuality.Unknown, IsFalse = true });
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

        private bool IsLogisticsSupported(HexCoord hex) => Area.Locations.Any(location => location.Hex.Equals(hex) && (location.Kind == LocationKind.Port || location.Kind == LocationKind.Airfield));
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
        private void AddLog(string text) { Log.Insert(0, text); if (Log.Count > 8) Log.RemoveAt(Log.Count - 1); }

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
