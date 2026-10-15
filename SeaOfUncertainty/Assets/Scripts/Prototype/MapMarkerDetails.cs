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

        public static string BallisticDefense(PrototypeGame game, Side viewer, FormationState formation)
        {
            if (formation == null || formation.Side != viewer || !game.Formations.Contains(formation)) return string.Empty;
            string equipment = formation.BallisticDefense?.Capacity > 0
                ? formation.BallisticDefense.Summary + "\n" + game.FriendlyBallisticDefenseStatus(viewer, formation) + "\n"
                    + (formation.BallisticDefense.SelfDefenseOnly ? "SELF-DEFENSE ONLY" : "DOMAINS: " + string.Join(", ", formation.BallisticDefense.ProtectedDomains)) + "\n" : string.Empty;
            var coverage = game.FriendlyBallisticCoverage(viewer, formation);
            if (coverage.Count > 0) return equipment + "AUTOMATIC PROTECTION: " + string.Join(", ", coverage.Select(item => item.Name))
                + "\nFirst eligible provider spends one package; no ordinary Reaction cost.";
            var candidates = game.Formations.Where(item => item.Side == viewer && !item.IsDestroyed && item.BallisticDefense?.Capacity > 0)
                .OrderBy(item => HexCoord.Distance(item.Position, formation.Position)).ThenBy(item => item.Id, System.StringComparer.Ordinal).Take(2);
            return equipment + "NO ELIGIBLE BALLISTIC PROTECTION\n" + string.Join("\n", candidates.Select(item => item.Name + ": " + game.FriendlyBallisticDefenseStatus(viewer, item, formation)));
        }

        public static string LatestFriendlyInterception(PrototypeGame game, Side viewer)
            => game.LogEntries.FirstOrDefault(entry => entry.IsPrivate && entry.Audience == viewer && entry.Text.Contains("; ballistic interception +"))?.Text;

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
                Description = $"{formation.Cohesion} · {formation.Endurance} endurance\nMAGAZINES {formation.Weapons?.Summary ?? "LEGACY LOADOUT"}{(formation.BallisticWeapons?.Capacity > 0 ? "\n" + formation.BallisticWeapons.Summary : string.Empty)}\n{BallisticDefense(game, game.Active.Side, formation)}\n{(formation == game.Active ? "Right-click for orders · I for the full inspector" : "T opens the friendly Ready queue and inspector")}"
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
