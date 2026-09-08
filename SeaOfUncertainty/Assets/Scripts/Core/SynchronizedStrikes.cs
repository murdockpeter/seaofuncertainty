using System;
using System.Collections.Generic;

namespace SeaOfUncertainty.Core
{
    [Serializable]
    public sealed class SynchronizedStrikeParticipant
    {
        public string FormationId;
        public Salvo Salvo = Salvo.Standard;
    }

    [Serializable]
    public sealed class SynchronizedStrikeState
    {
        public string Id;
        public Side Side;
        public string LeaderId;
        public string ContactTargetId;
        public HexCoord Aim;
        public int DeclaredTime;
        public int StrikeTime;
        public LocationQuality DeclaredLocation;
        public IdentityQuality DeclaredIdentity;
        public int DeclaredAge;
        public List<SynchronizedStrikeParticipant> Participants = new List<SynchronizedStrikeParticipant>();
        public bool PriorPlanning;
        public string DeconflictedFormationId;
        public List<string> CoordinationSupportedFormationIds = new List<string>();
    }

    [Serializable]
    public sealed class SynchronizedStrikeResult
    {
        public string StrikeId;
        public bool Hit;
        public bool ContinuedBlind;
        public bool Aborted;
        public Reaction Reaction;
        public List<CombatResult> Attacks = new List<CombatResult>();
    }
}
