using System;
using System.Collections.Generic;
using System.Linq;

namespace SeaOfUncertainty.Core
{
    public enum ResponseTarget { None, Formation, Contact, Hex, SynchronizedStrike, Reaction }

    [Serializable]
    public sealed class CommandResponseDefinition
    {
        public string Id;
        public string Title;
        public string Play;
        public string Cost;
        public ResponseTarget Target;
        public bool MechanicallySupported;
    }

    [Serializable]
    public sealed class CommandResponseDeckState
    {
        public Side Side;
        public List<string> DrawPile = new List<string>();
        public List<string> Hand = new List<string>();
        public List<string> DiscardPile = new List<string>();
        public int MajorActionsTowardDraw;
    }

    public static class CommandResponseCatalog
    {
        private static readonly List<CommandResponseDefinition> cards = new List<CommandResponseDefinition>
        {
            Card(CommandResponseIds.CommandersPresence, "Commander's Presence", "Remove one Friction effect.", "Mark 1 Command Strain.", ResponseTarget.Formation, true),
            Card(CommandResponseIds.PriorPlanning, "Prior Planning", "When declaring a Synchronized Strike, ignore the first Friction generated.", "", ResponseTarget.SynchronizedStrike, true),
            Card(CommandResponseIds.MissionCommand, "Mission Command", "A Broken Link Formation may alter Mission by Trigger without Command Attention.", "", ResponseTarget.Formation, true),
            Card(CommandResponseIds.RapidReplan, "Rapid Replan", "Change one Formation's Mission without a Command Slot.", "Next Ready is +1 Time.", ResponseTarget.Formation, true),
            Card(CommandResponseIds.DirectedTelescope, "Directed Telescope", "Conduct a Focused Search without occupying a Command Slot.", "", ResponseTarget.Formation, true),
            Card(CommandResponseIds.DamageControlPriority, "Damage Control Priority", "Downgrade one Destruction card's effect for one Action.", "", ResponseTarget.Formation, true),
            Card(CommandResponseIds.DelegateAuthority, "Delegate Authority", "Choose one Formation: +1 Command until its next Action.", "That Formation may not change Mission.", ResponseTarget.Formation, true),
            Card(CommandResponseIds.AcceptTheRisk, "Accept the Risk", "Ignore one Entropy restriction for this Action.", "Afterward mark Friction.", ResponseTarget.Formation, true),
            Card(CommandResponseIds.ReserveStaff, "Reserve Staff", "Remove one Communications Latency or Staff Overload effect.", "", ResponseTarget.Formation, true),
            Card(CommandResponseIds.DeconflictionCell, "Deconfliction Cell", "One Synchronized Strike participant ignores a +1 Time Coordination Drift.", "", ResponseTarget.SynchronizedStrike, true),
            Card(CommandResponseIds.RepairFocus, "Repair Focus", "Treat Destruction as one step lighter until end of next Action.", "", ResponseTarget.Formation, true),
            Card(CommandResponseIds.UpdateThePlot, "Update the Plot", "Refresh one Contact's Age to 0, but do not improve certainty.", "", ResponseTarget.Contact, true),
            Card(CommandResponseIds.BattleRhythm, "Battle Rhythm", "Remove one Delayed Execution or Hasty Retasking effect.", "", ResponseTarget.Formation, true),
            Card(CommandResponseIds.PreparedAxis, "Prepared Axis", "One Formation may Move +1 hex this Action.", "+1 Signature until next Action.", ResponseTarget.Formation, true),
            Card(CommandResponseIds.CircuitBreaker, "Circuit Breaker", "Cancel one Jammed Circuits or Sensor Saturation effect.", "", ResponseTarget.Formation, true),
            Card(CommandResponseIds.CoveringFires, "Covering Fires", "One friendly Formation gains +1 Defense for one Reaction.", "", ResponseTarget.Reaction, true),
            Card(CommandResponseIds.FlashOrder, "Flash Order", "One Broken Link Formation receives one new Mission immediately.", "Mark 1 Command Strain.", ResponseTarget.Formation, true),
            Card(CommandResponseIds.OrderlyWithdrawal, "Orderly Withdrawal", "One Evade reaction moves +1 extra hex after combat.", "", ResponseTarget.Reaction, true),
            Card(CommandResponseIds.PriorityRefuel, "Priority Refuel", "Improve one Formation's Endurance by one step.", "", ResponseTarget.Formation, true),
            Card(CommandResponseIds.DamageControlSurge, "Damage Control Surge", "Remove one Friction or Disruption effect from a damaged Formation.", "Next Ready is +1 Time.", ResponseTarget.Formation, true),
            Card(CommandResponseIds.FalseWindow, "False Window", "Create one False Contact in a hex within 2 hexes of a friendly Formation.", "", ResponseTarget.Hex, true),
            Card(CommandResponseIds.LocalInitiative, "Local Initiative", "One Formation counts as +1 Command until its next Action.", "", ResponseTarget.Formation, true),
            Card(CommandResponseIds.TrailRecovery, "Trail Recovery", "Improve one Contact's Location by one level.", "", ResponseTarget.Contact, true),
            Card(CommandResponseIds.TaskGroupReshuffle, "Task Group Reshuffle", "Reassign one Screen or Support bonus to a different friendly Formation in the same area.", "", ResponseTarget.Formation, true)
        };

        public static IReadOnlyList<CommandResponseDefinition> All => cards;
        public static CommandResponseDefinition Find(string id) => cards.FirstOrDefault(card => string.Equals(card.Id, id, StringComparison.OrdinalIgnoreCase));

        private static CommandResponseDefinition Card(string id, string title, string play, string cost, ResponseTarget target, bool supported)
            => new CommandResponseDefinition { Id = id, Title = title, Play = play, Cost = cost, Target = target, MechanicallySupported = supported };
    }
}
