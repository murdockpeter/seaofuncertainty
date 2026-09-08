using System;
using System.Collections.Generic;
using System.Linq;

namespace SeaOfUncertainty.Core
{
    public enum Side { Blue, Red }
    public enum OperationMode { LocalHotseat, SoloVsAi }
    public enum FormationKind { CarrierGroup, SurfaceGroup, Submarine, AirGroup }
    public enum ActionKind { Move, Search, Strike, Patrol, Support, Recover, Replenish, Hold }
    public enum MoveMode { Cautious, Normal, HighTempo }
    public enum SearchMode { Passive, Active, Focused }
    public enum SearchPriority { Location, Identity }
    public enum Salvo { Light, Standard, Heavy }
    public enum Reaction { None, Defend, Evade, Counterattack, Hold }
    public enum ReactionControl { None, HumanDirect, HumanHandoff, Ai }
    public enum PatrolPosture { Defensive, Balanced, Aggressive }
    public enum SupportKind { Strike, Search, Defense, AswSearch, Synchronization }
    public enum CommandSlotStatus { Free, Occupied, Strained }
    public enum MissionObjectiveKind { CurrentArea, OperationalObjective, FriendlyFormation, Contact, LogisticsFacility }
    public enum MissionPosture { Cautious, Balanced, Aggressive }
    public enum MissionTrigger { OnReady, ContactLocated, EntropyMarked, LogisticsRequired, ObjectiveReached }
    public enum LocationQuality { Low, Medium, High }
    public enum IdentityQuality { Unknown, General, Identified }
    public enum Endurance { Ready, Extended, Critical }
    public enum DamageState { None, Light, Heavy, Crippled, Destroyed }
    public enum CombatBand { Poor, Even, Favorable, Dominant }

    [Serializable]
    public struct HexCoord : IEquatable<HexCoord>
    {
        public int Q;
        public int R;

        public HexCoord(int q, int r) { Q = q; R = r; }
        public bool Equals(HexCoord other) => Q == other.Q && R == other.R;
        public override bool Equals(object obj) => obj is HexCoord other && Equals(other);
        public override int GetHashCode() => (Q * 397) ^ R;
        public override string ToString() => $"{(char)('A' + Q)}{R + 1}";

        public static int Distance(HexCoord a, HexCoord b)
        {
            int ax = a.Q;
            int az = a.R - (a.Q - (a.Q & 1)) / 2;
            int ay = -ax - az;
            int bx = b.Q;
            int bz = b.R - (b.Q - (b.Q & 1)) / 2;
            int by = -bx - bz;
            return (Math.Abs(ax - bx) + Math.Abs(ay - by) + Math.Abs(az - bz)) / 2;
        }
    }

    [Serializable]
    public sealed class Ratings
    {
        public int Move;
        public int Search;
        public int Signature;
        public int Strike;
        public int Defense;
        public int Asw;
        public int Command;
    }

    [Serializable]
    public sealed class FormationState
    {
        public string Id;
        public string Name;
        public Side Side;
        public FormationKind Kind;
        public HexCoord Position;
        public Ratings Ratings;
        public int ReadyTime;
        public bool Friction;
        public bool Disruption;
        public bool Destruction;
        public bool Loud;
        public bool HasReacted;
        public bool WeaponExpended;
        public int MajorActions;
        public Endurance Endurance;
        public DamageState Damage;
        public List<string> ActiveEffectCardIds = new List<string>();
        public List<string> ResolvedEffectCardIds = new List<string>();
        public int CommandBonus;
        public int MoveBonus;
        public int SignatureBonus;
        public int MovementSignatureModifier;
        public int ReactionDefenseBonus;
        public int NextReadyTimeBonus;
        public bool FreeFocusedSearch;
        public bool IgnoreEntropyNextAction;
        public bool SuppressDestructionNextAction;
        public ActionKind Mission = ActionKind.Hold;
        public bool OrderlyWithdrawalReady;
        public bool Replenishing;
        public bool PatrolActive;
        public HexCoord PatrolCenter;
        public string PatrolProtectedFormationId;
        public PatrolPosture PatrolPosture;
        public bool PatrolInterceptionAvailable;
        public bool SupportActive;
        public string SupportRecipientId;
        public SupportKind SupportKind;
        public bool SupportBlockedUntilRecover;
        public MissionObjectiveKind MissionObjective = MissionObjectiveKind.CurrentArea;
        public string MissionObjectiveId;
        public HexCoord MissionObjectiveHex;
        public MissionPosture MissionPosture = MissionPosture.Balanced;
        public MissionTrigger MissionTrigger = MissionTrigger.OnReady;
        public bool PendingMissionChange;
        public ActionKind PendingMissionTask;
        public MissionObjectiveKind PendingMissionObjective;
        public string PendingMissionObjectiveId;
        public HexCoord PendingMissionObjectiveHex;
        public MissionPosture PendingMissionPosture;
        public MissionTrigger PendingMissionTrigger;
        public int MissionDeliveryTime;
        public bool TriggerMissionCommandReady;
        public bool MissionChangeLockedUntilAction;
        public bool PushThroughReady;
        public bool LastActionFollowedMission;
        public int CompletedActions;
        public int LightDamageExpiresAfterAction;

        public int EntropySources => (Friction ? 1 : 0) + (Disruption ? 1 : 0) + (Destruction ? 1 : 0);
        public string Cohesion => EntropySources >= 3 ? "Disorganized" : EntropySources >= 2 ? "Disrupted" : "Cohesive";
        public bool IsDestroyed => Damage == DamageState.Destroyed;
        public int EffectiveMove => Math.Max(1, Ratings.Move - (Endurance == Endurance.Critical ? 1 : 0) - (Damage == DamageState.Crippled ? 1 : 0) - (HasEffect("X-03") ? 1 : 0));
        public int EffectiveStrike => Ratings.Strike - (Destruction && !IgnoreEntropyNextAction && !SuppressDestructionNextAction ? 1 : 0) - (HasEffect("X-01") && (Kind == FormationKind.CarrierGroup || Kind == FormationKind.AirGroup) ? 1 : 0);
        public int EffectiveSearch => Ratings.Search - (Disruption && !IgnoreEntropyNextAction ? 1 : 0) - (HasEffect("X-02") ? 1 : 0);
        public int EffectiveDefense => Math.Max(0, Ratings.Defense - (Damage == DamageState.Light ? 1 : 0) - (HasEffect("X-06") ? 1 : 0) - (HasEffect("X-07") ? 1 : 0) + ReactionDefenseBonus);
        public int EffectiveSignature => Ratings.Signature + (Loud ? 1 : 0) + SignatureBonus + MovementSignatureModifier;
        public int EffectiveCommand => Math.Max(0, Ratings.Command + CommandBonus - (HasEffect("X-05") ? 1 : 0) - (HasEffect("X-12") ? 1 : 0));
        public bool CanHeavySalvo => !WeaponExpended && Endurance != Endurance.Critical && Damage != DamageState.Crippled && !HasEffect("X-04") && !HasEffect("X-11");
        public bool HasEffect(string id)
        {
            if (ActiveEffectCardIds == null || !ActiveEffectCardIds.Contains(id) || ResolvedEffectCardIds != null && ResolvedEffectCardIds.Contains(id)) return false;
            if (IgnoreEntropyNextAction) return false;
            EntropyEffectDefinition effect = EntropyEffectCatalog.Find(id);
            return !SuppressDestructionNextAction || effect == null || effect.Source != EntropySource.Destruction;
        }
    }

    [Serializable]
    public sealed class ContactState
    {
        public string TargetId;
        public Side Owner;
        public HexCoord LastKnownPosition;
        public LocationQuality Location;
        public IdentityQuality Identity;
        public int Age;
        public int MovementUncertainty;
        public bool IsFalse;
        public bool IsLost;
        public bool HasContradictoryPosition;
        public HexCoord ContradictoryPosition;

        public string Summary => IsLost ? "LOST" : $"{Location} / {Identity} / AGE {Math.Min(Age, 3)}{(Age >= 3 ? "+" : string.Empty)} / AREA R{Rules.ContactUncertaintyRadius(this)}{(HasContradictoryPosition ? " / CONTRADICTORY" : string.Empty)}";
    }

    [Serializable]
    public sealed class CommandSlotState
    {
        public int Index;
        public CommandSlotStatus Status;
        public string Purpose;
        public string FormationId;
        public int ReleaseTime = -1;
    }

    [Serializable]
    public sealed class SideState
    {
        public Side Side;
        public int CommandSlots = 3;
        public int CommandStrain;
        public List<CommandSlotState> SlotStates = new List<CommandSlotState>();
    }

    [Serializable]
    public sealed class OperationalLogEntry
    {
        public string Text;
        public bool IsPrivate;
        public Side Audience;
    }

    [Serializable]
    public sealed class CombatResult
    {
        public int Attack;
        public int Defense;
        public int Difference;
        public CombatBand Band;
        public int Roll;
        public DamageState Damage;
        public DamageState ResultingDamage;
        public Reaction Reaction;
        public bool Withdrew;
        public HexCoord WithdrawalDestination;
        public bool Counterattacked;
        public int CounterattackRoll;
        public DamageState CounterattackDamage;
        public DamageState CounterattackResultingDamage;
    }

    public static class Rules
    {
        public static bool IsComplexAction(ActionKind action)
            => action == ActionKind.Move || action == ActionKind.Search || action == ActionKind.Strike || action == ActionKind.Patrol || action == ActionKind.Support;

        public static bool IsMajorAction(ActionKind action)
            => action == ActionKind.Move || action == ActionKind.Search || action == ActionKind.Strike;

        public static int MoveDistance(MoveMode mode) => mode == MoveMode.Cautious ? 1 : mode == MoveMode.Normal ? 2 : 3;
        public static int MoveDistance(FormationKind kind, MoveMode mode)
        {
            if (kind != FormationKind.AirGroup) return MoveDistance(mode);
            return mode == MoveMode.Cautious ? 4 : mode == MoveMode.Normal ? 6 : 8;
        }
        public static int MoveAllowance(FormationState formation, MoveMode mode)
        {
            int allowance = formation.Kind == FormationKind.AirGroup ? MoveDistance(formation.Kind, mode) : Math.Min(MoveDistance(formation.Kind, mode), formation.EffectiveMove);
            allowance += formation.MoveBonus;
            if (formation.HasEffect("F-07")) allowance--;
            return Math.Max(1, allowance);
        }
        public static int SearchModifier(SearchMode mode) => mode == SearchMode.Passive ? 0 : mode == SearchMode.Active ? 1 : 2;
        public static int SearchRange(SearchMode mode) => mode == SearchMode.Passive ? 8 : mode == SearchMode.Active ? 10 : 12;
        public const int SearchAreaRadius = 1;
        public static int ContactUncertaintyRadius(ContactState contact)
        {
            if (contact == null) return 0;
            int qualityRadius = contact.Location == LocationQuality.High ? 0 : contact.Location == LocationQuality.Medium ? 1 : 2;
            return qualityRadius + Math.Min(2, Math.Max(contact.Age, contact.MovementUncertainty));
        }
        public static int SalvoModifier(Salvo salvo) => salvo == Salvo.Light ? 0 : salvo == Salvo.Standard ? 1 : 2;
        public static int StrikeRange(FormationKind kind, Salvo salvo)
        {
            int light = kind == FormationKind.AirGroup ? 6 : kind == FormationKind.CarrierGroup ? 4 : 3;
            int standard = kind == FormationKind.AirGroup ? 10 : kind == FormationKind.CarrierGroup ? 8 : kind == FormationKind.Submarine ? 5 : 6;
            int heavy = kind == FormationKind.AirGroup ? 14 : kind == FormationKind.CarrierGroup ? 12 : kind == FormationKind.Submarine ? 7 : 9;
            return salvo == Salvo.Light ? light : salvo == Salvo.Standard ? standard : heavy;
        }
        public static int InterceptionRange => 1;
        public static int TargetingModifier(ContactState contact, bool agePenalty)
        {
            int value = contact.Location == LocationQuality.Low ? -2 : contact.Location == LocationQuality.Medium ? -1 : 0;
            if (contact.Identity == IdentityQuality.Identified) value += 1;
            if (agePenalty && contact.Age >= 2) value -= 1;
            return value;
        }

        public static int SearchTarget(int finalValue)
        {
            if (finalValue <= 0) return 5;
            if (finalValue <= 2) return 3;
            if (finalValue <= 4) return 2;
            return 1;
        }

        public static CombatBand BandFor(int difference)
        {
            if (difference <= -2) return CombatBand.Poor;
            if (difference <= 1) return CombatBand.Even;
            if (difference <= 3) return CombatBand.Favorable;
            return CombatBand.Dominant;
        }

        public static DamageState DamageFor(CombatBand band, int roll)
        {
            switch (band)
            {
                case CombatBand.Poor: return roll == 6 ? DamageState.Light : DamageState.None;
                case CombatBand.Even: return roll == 1 ? DamageState.None : roll == 6 ? DamageState.Heavy : DamageState.Light;
                case CombatBand.Favorable: return roll == 1 ? DamageState.Light : roll == 6 ? DamageState.Crippled : DamageState.Heavy;
                default: return roll == 1 ? DamageState.Heavy : roll == 6 ? DamageState.Destroyed : DamageState.Crippled;
            }
        }

        public static DamageState CombineDamage(DamageState current, DamageState incoming)
        {
            if (incoming == DamageState.None || incoming < current) return current;
            if (incoming > current) return incoming;
            return current >= DamageState.Destroyed ? DamageState.Destroyed : (DamageState)((int)current + 1);
        }

        public static FormationState NextReady(IEnumerable<FormationState> formations, Side? lastActingSide = null)
        {
            List<FormationState> available = formations.Where(f => !f.IsDestroyed).ToList();
            if (available.Count == 0) return null;
            int earliestTime = available.Min(f => f.ReadyTime);
            List<FormationState> tied = available.Where(f => f.ReadyTime == earliestTime).ToList();
            if (lastActingSide.HasValue && tied.Any(f => f.Side == lastActingSide.Value) && tied.Any(f => f.Side != lastActingSide.Value))
                tied = tied.Where(f => f.Side != lastActingSide.Value).ToList();
            return tied
                .OrderBy(f => f.EntropySources)
                .ThenByDescending(f => f.EffectiveCommand)
                .ThenBy(f => f.Id, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        public const int PatrolRadius = 1;
        public const int SupportRange = 2;
        public const int ControlRadius = 1;
        public static int ActionTime(ActionKind action) => action == ActionKind.Patrol || action == ActionKind.Support || action == ActionKind.Hold ? 1 : action == ActionKind.Replenish ? 3 : 2;
    }
}
