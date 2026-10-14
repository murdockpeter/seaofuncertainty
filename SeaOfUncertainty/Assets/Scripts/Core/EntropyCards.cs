using System;
using System.Collections.Generic;
using System.Linq;

namespace SeaOfUncertainty.Core
{
    public enum EntropySource { Friction, Disruption, Destruction }
    public enum EntropyResponseWindow { None, BeforeNextOwnAction }

    [Serializable]
    public sealed class EntropyEffectDefinition
    {
        public string Id;
        public EntropySource Source;
        public string Title;
        public string Effect;
        public string Response;
        public int ResponseCommandCost;
        public EntropyResponseWindow ResponseWindow;
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

    [Serializable]
    public sealed class EntropyResponseWindowState
    {
        public string FormationId;
        public string CardId;
        public int ExpiresAfterCompletedActions;
    }

    public static class EntropyEffectCatalog
    {
        private static readonly HashSet<string> inactiveSystemCards = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static readonly List<EntropyEffectDefinition> cards = new List<EntropyEffectDefinition>
        {
            Card(EntropyCardIds.DelayedExecution, EntropySource.Friction, "Delayed Execution", "+1 additional Time before next Action.", "Spend 1 Command Slot to cancel.", 1, EntropyResponseWindow.BeforeNextOwnAction),
            Card(EntropyCardIds.StaffOverload, EntropySource.Friction, "Staff Overload", "Cannot participate in Synchronization until completing another Action.", "Spend 1 Command Slot to restore immediately.", 1, EntropyResponseWindow.BeforeNextOwnAction),
            Card(EntropyCardIds.ConfusedPriorities, EntropySource.Friction, "Confused Priorities", "Current Mission remains in force, but changing it costs +1 Command Slot."),
            Card(EntropyCardIds.MaintenanceBacklog, EntropySource.Friction, "Maintenance Backlog", "High Tempo Move or Strike degrades Endurance one step."),
            Card(EntropyCardIds.CoordinationDrift, EntropySource.Friction, "Coordination Drift", "If part of a Synchronized Strike, this Formation shifts +1 Time."),
            Card(EntropyCardIds.OverextendedWatchbill, EntropySource.Friction, "Overextended Watchbill", "Recover takes +1 Time unless in a logistics-supporting hex."),
            Card(EntropyCardIds.NavigationDrift, EntropySource.Friction, "Navigation Drift", "The first Move this Formation makes is reduced by 1 hex.", "Spend 1 Command Slot to restore full movement.", 1, EntropyResponseWindow.BeforeNextOwnAction),
            Card(EntropyCardIds.FuelPriorityConflict, EntropySource.Friction, "Fuel Priority Conflict", "After a High Tempo Move, this Formation may not Support until Recover."),
            Card(EntropyCardIds.SortieTurnaroundLag, EntropySource.Friction, "Sortie Turnaround Lag", "Strike actions cost +1 Time until Recover."),
            Card(EntropyCardIds.FormationSpread, EntropySource.Friction, "Formation Spread", "The first Screen or Support bonus this Formation receives is ignored."),
            Card(EntropyCardIds.ChecklistChurn, EntropySource.Friction, "Checklist Churn", "This Formation may not choose High Tempo until after Hold or Recover."),
            Card(EntropyCardIds.HastyRetasking, EntropySource.Friction, "Hasty Retasking", "Immediate Mission change sets next Ready +1 Time."),
            Card(EntropyCardIds.CorruptedTrack, EntropySource.Disruption, "Corrupted Track", "Reduce either Location or Identity one level."),
            Card(EntropyCardIds.CommunicationsLatency, EntropySource.Disruption, "Communications Latency", "Mission changes arrive 1 Time later. Existing Mission continues."),
            Card(EntropyCardIds.ContradictoryReports, EntropySource.Disruption, "Contradictory Reports", "Place a second possible-location marker adjacent to one Contact."),
            Card(EntropyCardIds.BrokenLink, EntropySource.Disruption, "Broken Link", "This Formation cannot receive new orders until restored."),
            Card(EntropyCardIds.SensorSaturation, EntropySource.Disruption, "Sensor Saturation", "Active Search provides no bonus until Recover."),
            Card(EntropyCardIds.EWShadow, EntropySource.Disruption, "EW Shadow", "The first Support bonus received is ignored."),
            Card(EntropyCardIds.FalseEmission, EntropySource.Disruption, "False Emission", "Place one False Contact within 2 hexes of one real Contact."),
            Card(EntropyCardIds.JammedCircuits, EntropySource.Disruption, "Jammed Circuits", "Focused Search may not be declared until Recover."),
            Card(EntropyCardIds.DataGap, EntropySource.Disruption, "Data Gap", "One Contact of your choice immediately ages +1."),
            Card(EntropyCardIds.IdentificationDoubt, EntropySource.Disruption, "Identification Doubt", "One Identified Contact becomes General Identity."),
            Card(EntropyCardIds.MisroutedOrders, EntropySource.Disruption, "Misrouted Orders", "This Formation's next Support action is lost."),
            Card(EntropyCardIds.CompromisedPlot, EntropySource.Disruption, "Compromised Plot", "This Formation may not participate in Synchronization until Recover."),
            Card(EntropyCardIds.FlightOpsDegraded, EntropySource.Destruction, "Flight Ops Degraded", "Carrier or air group: -1 Strike until repaired."),
            Card(EntropyCardIds.SensorDamage, EntropySource.Destruction, "Sensor Damage", "-1 Search until repaired."),
            Card(EntropyCardIds.PropulsionDamage, EntropySource.Destruction, "Propulsion Damage", "Maximum movement is reduced by 1 hex."),
            Card(EntropyCardIds.MagazineDamage, EntropySource.Destruction, "Magazine Damage", "Heavy Salvo is unavailable."),
            Card(EntropyCardIds.CommandSpacesHit, EntropySource.Destruction, "Command Spaces Hit", "-1 local Command until repaired."),
            Card(EntropyCardIds.EscortLost, EntropySource.Destruction, "Escort Lost", "-1 Defense until reorganized."),
            Card(EntropyCardIds.AirDefenseHit, EntropySource.Destruction, "Air Defense Hit", "-1 Defense against Strike until repaired."),
            Card(EntropyCardIds.HangarDamage, EntropySource.Destruction, "Hangar Damage", "Air Support from this Formation is unavailable."),
            Card(EntropyCardIds.FireControlHit, EntropySource.Destruction, "Fire Control Hit", "This Formation may not Counterattack."),
            Card(EntropyCardIds.HullBreach, EntropySource.Destruction, "Hull Breach", "Recover and Replenish each take +1 Time."),
            Card(EntropyCardIds.LauncherDamage, EntropySource.Destruction, "Launcher Damage", "Standard Salvo counts as Light; Heavy Salvo is unavailable."),
            Card(EntropyCardIds.BridgeCasualties, EntropySource.Destruction, "Bridge Casualties", "-1 local Command and next Ready is +1 Time.")
        };

        public static IReadOnlyList<EntropyEffectDefinition> All => cards;
        public static EntropyEffectDefinition Find(string id) => cards.FirstOrDefault(card => string.Equals(card.Id, id, StringComparison.OrdinalIgnoreCase));
        public static IEnumerable<EntropyEffectDefinition> For(EntropySource source) => cards.Where(card => card.Source == source);
        public static bool IsFullyMechanicallySupported(EntropyEffectDefinition card) => card != null && !inactiveSystemCards.Contains(card.Id);
        public static bool AppliesTo(EntropyEffectDefinition card, FormationKind kind) => card != null &&
            ((card.Id != EntropyCardIds.FlightOpsDegraded && card.Id != EntropyCardIds.HangarDamage) || kind == FormationKind.CarrierGroup || kind == FormationKind.AirGroup);

        private static EntropyEffectDefinition Card(string id, EntropySource source, string title, string effect, string response = "", int responseCommandCost = 0, EntropyResponseWindow responseWindow = EntropyResponseWindow.None)
            => new EntropyEffectDefinition { Id = id, Source = source, Title = title, Effect = effect, Response = response, ResponseCommandCost = responseCommandCost, ResponseWindow = responseWindow };
    }
}
