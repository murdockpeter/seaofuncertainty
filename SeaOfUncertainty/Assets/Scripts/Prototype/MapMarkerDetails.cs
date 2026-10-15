using System.Linq;
using SeaOfUncertainty.Core;

namespace SeaOfUncertainty.Prototype
{
    // Marker text uses public geography, friendly state, or the viewer's Contact report only.
    public sealed class MapMarkerDetails
    {
        public string Category;
        public string Title;
        public string Metadata;
        public string Description;

        public static MapMarkerDetails Objective(PrototypeGame game)
        {
            if (game?.Active == null) return null;
            OperationalLocationDefinition location = game.Area.Locations.FirstOrDefault(item => item.Kind == LocationKind.Objective && item.Hex.Equals(game.Area.Objective));
            string scoring = string.Join("\n", game.Scenario.Objectives
                .Where(item => item.Kind == ScenarioObjectiveKind.Control || item.Kind == ScenarioObjectiveKind.Denial)
                .Select(item => $"{item.Points} VP · {item.Title}\n{ScenarioScoring.DescribeObjective(game.Scenario, item)}"));
            string presence = game.HasControlPresence(game.Active.Side, game.Area.Objective) ? "Friendly naval presence in the control area." : "No friendly naval presence in the control area.";
            return new MapMarkerDetails
            {
                Category = "OPERATIONAL OBJECTIVE",
                Title = location?.Name ?? game.Scenario.DisplayName,
                Metadata = $"HEX {game.Area.Objective} · {game.Area.NauticalMiles(game.Active.Position, game.Area.Objective)} NM FROM ACTIVE · T{game.Scenario.Horizon} SCORING",
                Description = (string.IsNullOrEmpty(scoring) ? "Named operational reference; this scenario defines no Control or Denial scoring here." : scoring) + "\n\n" + presence
            };
        }

        public static MapMarkerDetails Formation(PrototypeGame game, FormationState formation)
        {
            if (game?.Active == null || formation == null || formation.Side != game.Active.Side || formation.IsDestroyed || !game.Formations.Contains(formation)) return null;
            return new MapMarkerDetails
            {
                Category = formation == game.Active ? "ACTIVE FORMATION" : "FRIENDLY FORMATION",
                Title = formation.Name,
                Metadata = $"{formation.Kind} · HEX {formation.Position} · READY T{formation.ReadyTime:00}",
                Description = $"{formation.Cohesion} · {formation.Endurance} endurance\nMAGAZINES {formation.Weapons?.Summary ?? "LEGACY LOADOUT"}\n{(formation == game.Active ? "Right-click for orders · I for the full inspector" : "T opens the friendly Ready queue and inspector")}"
            };
        }

        public static MapMarkerDetails Contact(PrototypeGame game, ContactState contact, HexCoord reportHex)
        {
            if (game?.Active == null || contact == null || contact.Owner != game.Active.Side || contact.IsLost || !game.Contacts.Contains(contact)) return null;
            bool known = contact.Identity == IdentityQuality.Identified;
            string name = known ? game.Find(contact.TargetId)?.Name : null;
            return new MapMarkerDetails
            {
                Category = "CONTACT REPORT",
                Title = name ?? (contact.Identity == IdentityQuality.Unknown ? "Unresolved Contact" : "Contact · " + contact.Domain),
                Metadata = $"REPORTED HEX {reportHex} · {game.Area.NauticalMiles(game.Active.Position, reportHex)} NM FROM ACTIVE",
                Description = contact.Summary + "\nSearch can refine this report. A Strike commits to an estimated aim hex."
            };
        }

        public static MapMarkerDetails Location(PrototypeGame game, OperationalLocationDefinition location)
        {
            if (game?.Active == null || location == null) return null;
            return new MapMarkerDetails
            {
                Category = "THEATER LOCATION",
                Title = location.Name,
                Metadata = $"{location.Kind} · HEX {location.Hex} · {game.Area.NauticalMiles(game.Active.Position, location.Hex)} NM FROM ACTIVE",
                Description = location.Kind == LocationKind.Objective ? "Named map location; scoring follows the operation's objectives." : "Named theater location. Facility services depend on your formation and scenario."
            };
        }
    }
}
