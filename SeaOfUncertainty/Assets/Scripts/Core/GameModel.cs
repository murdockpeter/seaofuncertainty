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
    public enum Salvo { Light, Standard, Heavy }
    public enum Reaction { Defend, Evade, Counterattack, Hold }
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

        public int EntropySources => (Friction ? 1 : 0) + (Disruption ? 1 : 0) + (Destruction ? 1 : 0);
        public string Cohesion => EntropySources >= 3 ? "Disorganized" : EntropySources >= 2 ? "Disrupted" : "Cohesive";
        public bool IsDestroyed => Damage == DamageState.Destroyed;
        public int EffectiveMove => Math.Max(1, Ratings.Move - (Endurance == Endurance.Critical ? 1 : 0) - (Damage == DamageState.Crippled ? 1 : 0));
        public int EffectiveStrike => Ratings.Strike - (Destruction ? 1 : 0);
        public int EffectiveSearch => Ratings.Search - (Disruption ? 1 : 0);
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
        public bool IsFalse;
        public bool IsLost;

        public string Summary => IsLost ? "LOST" : $"{Location} / {Identity} / AGE {Math.Min(Age, 3)}{(Age >= 3 ? "+" : string.Empty)}";
    }

    [Serializable]
    public sealed class SideState
    {
        public Side Side;
        public int CommandSlots = 3;
        public int CommandStrain;
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
    }

    public static class Rules
    {
        public static int MoveDistance(MoveMode mode) => mode == MoveMode.Cautious ? 1 : mode == MoveMode.Normal ? 2 : 3;
        public static int MoveDistance(FormationKind kind, MoveMode mode)
        {
            if (kind != FormationKind.AirGroup) return MoveDistance(mode);
            return mode == MoveMode.Cautious ? 4 : mode == MoveMode.Normal ? 6 : 8;
        }
        public static int MoveAllowance(FormationState formation, MoveMode mode) => formation.Kind == FormationKind.AirGroup ? MoveDistance(formation.Kind, mode) : Math.Min(MoveDistance(formation.Kind, mode), formation.EffectiveMove);
        public static int SearchModifier(SearchMode mode) => mode == SearchMode.Passive ? 0 : mode == SearchMode.Active ? 1 : 2;
        public static int SearchRange(SearchMode mode) => mode == SearchMode.Passive ? 8 : mode == SearchMode.Active ? 10 : 12;
        public const int SearchAreaRadius = 1;
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
                .ThenByDescending(f => f.Ratings.Command)
                .ThenBy(f => f.Id, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        public static int ActionTime(ActionKind action) => action == ActionKind.Patrol || action == ActionKind.Hold ? 1 : action == ActionKind.Replenish ? 3 : 2;
    }
}
