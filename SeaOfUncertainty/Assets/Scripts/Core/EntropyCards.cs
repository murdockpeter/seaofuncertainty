using System;
using System.Collections.Generic;
using System.Linq;

namespace SeaOfUncertainty.Core
{
    public enum EntropySource { Friction, Disruption, Destruction }

    [Serializable]
    public sealed class EntropyEffectDefinition
    {
        public string Id;
        public EntropySource Source;
        public string Title;
        public string Effect;
        public string Response;
    }

    [Serializable]
    public sealed class EntropyDeckState
    {
        public EntropySource Source;
        public List<string> DrawPile = new List<string>();
        public List<string> DiscardPile = new List<string>();
    }

    [Serializable]
    public sealed class EntropyDrawNotice
    {
        public string CardId;
        public string FormationId;
    }

    public static class EntropyEffectCatalog
    {
        private static readonly HashSet<string> inactiveSystemCards = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "F-02", "F-03", "F-05", "F-08", "F-10", "F-12",
            "D-02", "D-03", "D-04", "D-06", "D-11", "D-12",
            "X-08", "X-10"
        };

        private static readonly List<EntropyEffectDefinition> cards = new List<EntropyEffectDefinition>
        {
            Card("F-01", EntropySource.Friction, "Delayed Execution", "+1 additional Time before next Action.", "Spend 1 Command Slot to cancel."),
            Card("F-02", EntropySource.Friction, "Staff Overload", "Cannot participate in Synchronization until completing another Action.", "Spend 1 Command Slot to restore immediately."),
            Card("F-03", EntropySource.Friction, "Confused Priorities", "Current Mission remains in force, but changing it costs +1 Command Slot."),
            Card("F-04", EntropySource.Friction, "Maintenance Backlog", "High Tempo Move or Strike degrades Endurance one step."),
            Card("F-05", EntropySource.Friction, "Coordination Drift", "If part of a Synchronized Strike, this Formation shifts +1 Time."),
            Card("F-06", EntropySource.Friction, "Overextended Watchbill", "Recover takes +1 Time unless in a logistics-supporting hex."),
            Card("F-07", EntropySource.Friction, "Navigation Drift", "The first Move this Formation makes is reduced by 1 hex.", "Spend 1 Command Slot to restore full movement."),
            Card("F-08", EntropySource.Friction, "Fuel Priority Conflict", "After a High Tempo Move, this Formation may not Support until Recover."),
            Card("F-09", EntropySource.Friction, "Sortie Turnaround Lag", "Strike actions cost +1 Time until Recover."),
            Card("F-10", EntropySource.Friction, "Formation Spread", "The first Screen or Support bonus this Formation receives is ignored."),
            Card("F-11", EntropySource.Friction, "Checklist Churn", "This Formation may not choose High Tempo until after Hold or Recover."),
            Card("F-12", EntropySource.Friction, "Hasty Retasking", "Immediate Mission change sets next Ready +1 Time."),
            Card("D-01", EntropySource.Disruption, "Corrupted Track", "Reduce either Location or Identity one level."),
            Card("D-02", EntropySource.Disruption, "Communications Latency", "Mission changes arrive 1 Time later. Existing Mission continues."),
            Card("D-03", EntropySource.Disruption, "Contradictory Reports", "Place a second possible-location marker adjacent to one Contact."),
            Card("D-04", EntropySource.Disruption, "Broken Link", "This Formation cannot receive new orders until restored."),
            Card("D-05", EntropySource.Disruption, "Sensor Saturation", "Active Search provides no bonus until Recover."),
            Card("D-06", EntropySource.Disruption, "EW Shadow", "The first Support bonus received is ignored."),
            Card("D-07", EntropySource.Disruption, "False Emission", "Place one False Contact within 2 hexes of one real Contact."),
            Card("D-08", EntropySource.Disruption, "Jammed Circuits", "Focused Search may not be declared until Recover."),
            Card("D-09", EntropySource.Disruption, "Data Gap", "One Contact of your choice immediately ages +1."),
            Card("D-10", EntropySource.Disruption, "Identification Doubt", "One Identified Contact becomes General Identity."),
            Card("D-11", EntropySource.Disruption, "Misrouted Orders", "This Formation's next Support action is lost."),
            Card("D-12", EntropySource.Disruption, "Compromised Plot", "This Formation may not participate in Synchronization until Recover."),
            Card("X-01", EntropySource.Destruction, "Flight Ops Degraded", "Carrier or air group: -1 Strike until repaired."),
            Card("X-02", EntropySource.Destruction, "Sensor Damage", "-1 Search until repaired."),
            Card("X-03", EntropySource.Destruction, "Propulsion Damage", "Maximum movement is reduced by 1 hex."),
            Card("X-04", EntropySource.Destruction, "Magazine Damage", "Heavy Salvo is unavailable."),
            Card("X-05", EntropySource.Destruction, "Command Spaces Hit", "-1 local Command until repaired."),
            Card("X-06", EntropySource.Destruction, "Escort Lost", "-1 Defense until reorganized."),
            Card("X-07", EntropySource.Destruction, "Air Defense Hit", "-1 Defense against Strike until repaired."),
            Card("X-08", EntropySource.Destruction, "Hangar Damage", "Air Support from this Formation is unavailable."),
            Card("X-09", EntropySource.Destruction, "Fire Control Hit", "This Formation may not Counterattack."),
            Card("X-10", EntropySource.Destruction, "Hull Breach", "Recover and Replenish each take +1 Time."),
            Card("X-11", EntropySource.Destruction, "Launcher Damage", "Standard Salvo counts as Light; Heavy Salvo is unavailable."),
            Card("X-12", EntropySource.Destruction, "Bridge Casualties", "-1 local Command and next Ready is +1 Time.")
        };

        public static IReadOnlyList<EntropyEffectDefinition> All => cards;
        public static EntropyEffectDefinition Find(string id) => cards.FirstOrDefault(card => string.Equals(card.Id, id, StringComparison.OrdinalIgnoreCase));
        public static IEnumerable<EntropyEffectDefinition> For(EntropySource source) => cards.Where(card => card.Source == source);
        public static bool IsFullyMechanicallySupported(EntropyEffectDefinition card) => card != null && !inactiveSystemCards.Contains(card.Id);
        public static bool AppliesTo(EntropyEffectDefinition card, FormationKind kind) => card != null &&
            ((card.Id != "X-01" && card.Id != "X-08") || kind == FormationKind.CarrierGroup || kind == FormationKind.AirGroup);

        private static EntropyEffectDefinition Card(string id, EntropySource source, string title, string effect, string response = "")
            => new EntropyEffectDefinition { Id = id, Source = source, Title = title, Effect = effect, Response = response };
    }
}
