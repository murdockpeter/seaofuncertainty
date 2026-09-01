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
            public int Version = 2;
            public string ScenarioId;
            public string OperationalAreaId;
            public int Time;
            public string ActiveFormationId;
            public bool AgeTwoTargetingPenalty;
            public List<FormationState> Formations = new List<FormationState>();
            public List<ContactState> Contacts = new List<ContactState>();
            public List<SideState> Sides = new List<SideState>();
            public List<string> Log = new List<string>();
        }

        private readonly Random random;
        public readonly List<FormationState> Formations = new List<FormationState>();
        public readonly List<ContactState> Contacts = new List<ContactState>();
        public readonly Dictionary<Side, SideState> Sides = new Dictionary<Side, SideState>();
        public readonly List<string> Log = new List<string>();
        public ScenarioDefinition Scenario { get; }
        public OperationalAreaDefinition Area => Scenario.Area;
        public int Time { get; private set; }
        public FormationState Active { get; private set; }
        public bool AgeTwoTargetingPenalty = true;

        public PrototypeGame(int seed = 1978, ScenarioDefinition scenario = null)
        {
            random = new Random(seed);
            Scenario = OperationalDataMigration.Migrate(scenario ?? ScenarioCatalog.MeridianVeil());
            Sides[Side.Blue] = new SideState { Side = Side.Blue };
            Sides[Side.Red] = new SideState { Side = Side.Red };
            CreateScenario();
            AdvanceToNextFormation();
        }

        public FormationState Find(string id) => Formations.FirstOrDefault(f => f.Id == id);
        public ContactState ContactFor(Side owner, string targetId) => Contacts.FirstOrDefault(c => c.Owner == owner && c.TargetId == targetId && !c.IsLost);

        public SaveData CaptureState()
        {
            return new SaveData
            {
                Time = Time,
                ScenarioId = Scenario.Id,
                OperationalAreaId = Area.Id,
                ActiveFormationId = Active?.Id,
                AgeTwoTargetingPenalty = AgeTwoTargetingPenalty,
                Formations = new List<FormationState>(Formations),
                Contacts = new List<ContactState>(Contacts),
                Sides = new List<SideState>(Sides.Values),
                Log = new List<string>(Log)
            };
        }

        public void RestoreState(SaveData data)
        {
            if (data == null || data.Version < 1 || data.Version > 2) throw new ArgumentException("Unsupported or empty save data.");
            if (data.Version >= 2 && !string.IsNullOrEmpty(data.ScenarioId) && !string.Equals(data.ScenarioId, Scenario.Id, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException($"Save scenario {data.ScenarioId} does not match loaded scenario {Scenario.Id}.");
            Formations.Clear();
            Formations.AddRange(data.Formations ?? new List<FormationState>());
            Contacts.Clear();
            Contacts.AddRange(data.Contacts ?? new List<ContactState>());
            Sides.Clear();
            foreach (SideState side in data.Sides ?? new List<SideState>()) Sides[side.Side] = side;
            if (!Sides.ContainsKey(Side.Blue)) Sides[Side.Blue] = new SideState { Side = Side.Blue };
            if (!Sides.ContainsKey(Side.Red)) Sides[Side.Red] = new SideState { Side = Side.Red };
            Log.Clear();
            Log.AddRange(data.Log ?? new List<string>());
            Time = data.Time;
            AgeTwoTargetingPenalty = data.AgeTwoTargetingPenalty;
            Active = Find(data.ActiveFormationId) ?? Rules.NextReady(Formations);
        }

        public bool Move(FormationState formation, HexCoord destination, MoveMode mode, out string message)
        {
            int distance = HexCoord.Distance(formation.Position, destination);
            int allowed = Rules.MoveAllowance(formation, mode);
            if (formation != Active) { message = "Only the highlighted Ready formation may act."; return false; }
            if (!Area.Contains(destination)) { message = "That hex is outside the active operational area."; return false; }
            OperationalTerrain terrain = Area.TerrainAt(destination);
            if (terrain == OperationalTerrain.Land && formation.Kind != FormationKind.AirGroup) { message = "Surface and submarine formations cannot end movement in a Land hex."; return false; }
            if (distance < 1 || distance > allowed) { message = $"{mode} Move allows {allowed} hex{(allowed == 1 ? string.Empty : "es")}."; return false; }
            formation.Position = destination;
            formation.Loud = mode == MoveMode.HighTempo;
            if (mode == MoveMode.HighTempo) formation.Friction = true;
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
            if (mode == SearchMode.Focused && side.CommandSlots < 1) { message = "Focused Search needs one free Command Slot."; return false; }
            if (mode == SearchMode.Focused) side.CommandSlots--;
            searcher.Loud = mode == SearchMode.Active;

            var detected = new List<string>();
            foreach (FormationState target in Formations.Where(candidate => candidate.Side != searcher.Side && !candidate.IsDestroyed && HexCoord.Distance(center, candidate.Position) <= Rules.SearchAreaRadius && HexCoord.Distance(searcher.Position, candidate.Position) <= maximumRange))
            {
                int range = HexCoord.Distance(searcher.Position, target.Position);
                int finalValue = searcher.EffectiveSearch + Rules.SearchModifier(mode) + target.Ratings.Signature + (target.Loud ? 1 : 0) - range;
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

            if (mode == SearchMode.Focused) side.CommandSlots++;
            string outcome = detected.Count == 0 ? "no detections" : string.Join("; ", detected);
            CompleteAction(searcher, ActionKind.Search, false, $"{searcher.Name} searched area {center} (radius {Rules.SearchAreaRadius}, {mode}) — {outcome}.");
            message = Log[0];
            return true;
        }

        public bool Strike(FormationState attacker, FormationState target, Salvo salvo, Reaction reaction, out CombatResult result, out string message)
        {
            result = null;
            if (attacker != Active) { message = "Only the highlighted Ready formation may act."; return false; }
            ContactState contact = ContactFor(attacker.Side, target.Id);
            if (contact == null) { message = "A usable Contact is required to Strike."; return false; }
            int strikeRange = Rules.StrikeRange(attacker.Kind, salvo);
            if (HexCoord.Distance(attacker.Position, contact.LastKnownPosition) > strikeRange) { message = $"{attacker.Kind} {salvo} Strike range is {strikeRange} hexes ({strikeRange * Area.NauticalMilesPerHex} nm)."; return false; }
            if (salvo == Salvo.Heavy && (attacker.WeaponExpended || attacker.Endurance == Endurance.Critical || attacker.Damage == DamageState.Crippled))
            { message = "Heavy Salvo is unavailable to this formation."; return false; }
            int attack = attacker.EffectiveStrike + Rules.SalvoModifier(salvo) + Rules.TargetingModifier(contact, AgeTwoTargetingPenalty);
            int defense = target.Ratings.Defense + (reaction == Reaction.Defend || reaction == Reaction.Evade ? 1 : 0) - (target.Destruction ? 1 : 0);
            int difference = attack - defense;
            CombatBand band = Rules.BandFor(difference);
            int roll = Roll();
            DamageState damage = Rules.DamageFor(band, roll);
            ApplyDamage(target, damage);
            if (salvo == Salvo.Heavy) attacker.WeaponExpended = true;
            result = new CombatResult { Attack = attack, Defense = defense, Difference = difference, Band = band, Roll = roll, Damage = damage };
            CompleteAction(attacker, ActionKind.Strike, false, $"{attacker.Name} struck {target.Name}: {attack} vs {defense}, {band}, rolled {roll} — {damage}.");
            message = Log[0];
            return true;
        }

        public bool Hold(FormationState formation, out string message)
        {
            if (formation != Active) { message = "Only the highlighted Ready formation may act."; return false; }
            formation.Loud = false;
            CompleteAction(formation, ActionKind.Hold, false, $"{formation.Name} held position and went quiet.");
            message = Log[0];
            return true;
        }

        public bool Recover(FormationState formation, out string message)
        {
            if (formation != Active) { message = "Only the highlighted Ready formation may act."; return false; }
            if (!formation.Friction && !formation.Disruption) { message = "No recoverable Friction or Disruption is marked."; return false; }
            if (formation.Friction) formation.Friction = false; else formation.Disruption = false;
            CompleteAction(formation, ActionKind.Recover, false, $"{formation.Name} recovered one Entropy source.");
            message = Log[0];
            return true;
        }

        private void CompleteAction(FormationState formation, ActionKind action, bool generatedFriction, string entry, int additionalTime = 0)
        {
            int cost = Rules.ActionTime(action) + additionalTime;
            bool complex = action == ActionKind.Move || action == ActionKind.Search || action == ActionKind.Strike || action == ActionKind.Support;
            if (formation.Friction && complex) cost++;
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
            if (generatedFriction) formation.Friction = true;
            AddLog($"T{Time:00}  {entry} Next Ready T{formation.ReadyTime:00}.");
            AdvanceToNextFormation();
        }

        private void AdvanceToNextFormation()
        {
            FormationState next = Rules.NextReady(Formations);
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
                foreach (FormationState formation in Formations) formation.HasReacted = false;
            }
            Active = Rules.NextReady(Formations);
        }

        private void ApplyDamage(FormationState target, DamageState damage)
        {
            if (damage == DamageState.None) return;
            if (damage == DamageState.Light) target.Damage = target.Damage < DamageState.Light ? DamageState.Light : target.Damage;
            else
            {
                target.Damage = damage > target.Damage ? damage : target.Damage;
                target.Destruction = true;
            }
        }

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
