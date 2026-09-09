using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using SeaOfUncertainty.Core;
using UnityEditor;
using UnityEngine;

namespace SeaOfUncertainty.Editor
{
    public static class PrintAndPlayGenerator
    {
        private const string OutputDirectory = "../generated/print-and-play";

        [MenuItem("Sea of Uncertainty/Generate Print-and-Play Proofs")]
        public static void Generate()
        {
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, OutputDirectory));
            Directory.CreateDirectory(directory);
            IReadOnlyList<ScenarioDefinition> scenarios = ScenarioCatalog.All();
            Write(directory, "formation-cards.html", Page("Formation Cards", FormationCards(scenarios), "card-sheet"));
            Write(directory, "counters-and-contacts.html", Page("Formation Counters and Contact Markers", Counters(scenarios), "counter-sheet"));
            Write(directory, "tracks.html", Page("Operational Tracks", Tracks(), "track-sheet"));
            Write(directory, "entropy-deck.html", Page("Entropy Deck — 36 Cards", EntropyDeck(), "card-sheet"));
            Write(directory, "response-deck.html", Page("Command Response Deck — 24 Cards", ResponseDeck(), "card-sheet"));
            Write(directory, "scenario-aids.html", Page("Scenario Setup and Player Aids", ScenarioAids(scenarios), "aid-sheet"));
            Write(directory, "manifest.json", Manifest(scenarios));
            Debug.Log($"Generated print-and-play proof set at {directory}");
        }

        private static string FormationCards(IEnumerable<ScenarioDefinition> scenarios)
        {
            var html = new StringBuilder();
            foreach (ScenarioDefinition scenario in scenarios)
            {
                html.Append($"<h2>{E(scenario.DisplayName)}</h2>");
                foreach (FormationDefinition formation in scenario.Formations.OrderBy(item => item.Side).ThenBy(item => item.Id))
                {
                    WeaponInventoryState weapons = formation.Weapons ?? new WeaponInventoryState();
                    Ratings r = formation.Ratings ?? new Ratings();
                    html.Append($"<article class='card {formation.Side.ToString().ToLowerInvariant()}'><header>{E(formation.Name)} <small>{E(formation.Id)}</small></header>")
                        .Append($"<p>{formation.Side} · {formation.Kind}</p><div class='ratings'>MOVE {r.Move} · SEARCH {r.Search} · SIG {r.Signature}<br>STRIKE {r.Strike} · DEF {r.Defense} · ASW {r.Asw}<br>CMD {r.Command} · EW {r.ElectronicWarfare} · CYBER {r.Cyber}</div>")
                        .Append($"<footer>WEAPONS L {weapons.MaxLight} · S {weapons.MaxStandard} · H {weapons.MaxHeavy}<br>START {new HexCoord(formation.Q, formation.R)}</footer></article>");
                }
            }
            return html.ToString();
        }

        private static string Counters(IEnumerable<ScenarioDefinition> scenarios)
        {
            var html = new StringBuilder();
            foreach (FormationDefinition formation in scenarios.SelectMany(item => item.Formations).GroupBy(item => item.Id).Select(item => item.First()).OrderBy(item => item.Id))
                html.Append($"<div class='counter {formation.Side.ToString().ToLowerInvariant()}'><strong>{E(formation.Id)}</strong><span>{KindCode(formation.Kind)}</span></div>");
            foreach (Side side in Enum.GetValues(typeof(Side)))
                for (int quality = 0; quality < 6; quality++) html.Append($"<div class='counter contact'><strong>{side} CONTACT</strong><span>{(LocationQuality)Math.Min(quality / 2, 2)} / {(IdentityQuality)Math.Min(quality % 3, 2)}</span></div>");
            for (int index = 1; index <= 8; index++) html.Append($"<div class='counter contact'><strong>FALSE CONTACT</strong><span>FC-{index:00}</span></div>");
            return html.ToString();
        }

        private static string Tracks()
        {
            var html = new StringBuilder();
            html.Append(Track("READY TIME", Enumerable.Range(0, 31).Select(value => value.ToString(CultureInfo.InvariantCulture))));
            html.Append(Track("COMMAND", Enumerable.Range(0, 5).Select(value => value.ToString(CultureInfo.InvariantCulture))));
            html.Append(Track("ENDURANCE", new[] { "READY", "EXTENDED", "CRITICAL" }));
            html.Append(Track("WEAPONS", new[] { "LIGHT", "STANDARD", "HEAVY", "EXPENDED" }));
            html.Append(Track("ENTROPY", new[] { "FRICTION", "DISRUPTION", "DESTRUCTION" }));
            return html.ToString();
        }

        private static string EntropyDeck()
        {
            var html = new StringBuilder();
            foreach (EntropyEffectDefinition card in EntropyEffectCatalog.All)
                html.Append($"<article class='card entropy'><header>{E(card.Id)} · {E(card.Title)}</header><p>{card.Source}</p><div>{E(card.Effect)}</div><footer>{E(card.Response)}</footer></article>");
            return html.ToString();
        }

        private static string ResponseDeck()
        {
            var html = new StringBuilder();
            foreach (CommandResponseDefinition card in CommandResponseCatalog.All)
                html.Append($"<article class='card response'><header>{E(card.Id)} · {E(card.Title)}</header><p>{card.Target}</p><div>{E(card.Play)}</div><footer>{E(card.Cost)}</footer></article>");
            return html.ToString();
        }

        private static string ScenarioAids(IEnumerable<ScenarioDefinition> scenarios)
        {
            var html = new StringBuilder();
            foreach (ScenarioDefinition scenario in scenarios)
            {
                html.Append($"<section class='scenario'><h2>{E(scenario.DisplayName)}</h2><p><strong>{E(scenario.Summary)}</strong></p>")
                    .Append($"<p>Area: {E(scenario.Area.DisplayName)} · {scenario.Area.NauticalMilesPerHex} nm/hex · Horizon T{scenario.Horizon} · Weather: {E(scenario.Weather)} ({scenario.WeatherSeverity})</p>")
                    .Append($"<p>Blue: {scenario.BlueCommandArchitecture} · Red: {scenario.RedCommandArchitecture}</p><h3>Setup</h3><table><tr><th>ID</th><th>Side</th><th>Formation</th><th>Start</th></tr>");
                foreach (FormationDefinition formation in scenario.Formations.OrderBy(item => item.Side).ThenBy(item => item.Id))
                    html.Append($"<tr><td>{E(formation.Id)}</td><td>{formation.Side}</td><td>{E(formation.Name)}</td><td>{new HexCoord(formation.Q, formation.R)}</td></tr>");
                html.Append("</table><h3>Special Rules</h3><p>").Append(E(scenario.SpecialRules)).Append("</p><h3>Victory</h3><p>").Append(E(scenario.VictoryConditions)).Append("</p></section>");
            }
            html.Append("<section class='scenario'><h2>Action Aid</h2><p>Move · Search · Strike · Patrol/Screen · Support · Recover · Replenish · Hold</p><p>Resolve the lowest Ready Time first. On ties, alternate sides when possible, then prefer lower Entropy, higher Command, and stable formation ID.</p></section>");
            return html.ToString();
        }

        private static string Track(string name, IEnumerable<string> cells)
            => $"<section class='track'><h2>{E(name)}</h2><div>{string.Join(string.Empty, cells.Select(cell => $"<span>{E(cell)}</span>"))}</div></section>";

        private static string Manifest(IReadOnlyList<ScenarioDefinition> scenarios)
        {
            return JsonUtility.ToJson(new PrintManifest
            {
                Version = 1, ScenarioIds = scenarios.Select(item => item.Id).ToList(), FormationCardCount = scenarios.Sum(item => item.Formations.Count),
                EntropyCardCount = EntropyEffectCatalog.All.Count, ResponseCardCount = CommandResponseCatalog.All.Count,
                Files = new List<string> { "formation-cards.html", "counters-and-contacts.html", "tracks.html", "entropy-deck.html", "response-deck.html", "scenario-aids.html" }
            }, true);
        }

        private static string Page(string title, string body, string bodyClass)
        {
            return "<!doctype html><html><head><meta charset='utf-8'><title>" + E(title) + "</title><style>" +
                "@page{size:letter;margin:.35in}*{box-sizing:border-box}body{font-family:Arial,sans-serif;color:#102634;margin:0}h1{page-break-after:avoid}.card-sheet,.counter-sheet{display:grid;grid-template-columns:repeat(3,1fr);gap:.12in}.card{height:3.35in;border:2px solid #173d50;padding:.13in;break-inside:avoid;display:flex;flex-direction:column}.card header{font-weight:700;border-bottom:1px solid;padding-bottom:.08in}.card header small{float:right}.card footer{margin-top:auto;border-top:1px solid;padding-top:.08in;font-size:10pt}.blue{border-color:#176d9b}.red{border-color:#a43c35}.entropy{border-color:#8c5d20}.response{border-color:#436847}.counter-sheet{grid-template-columns:repeat(6,1fr)}.counter{aspect-ratio:1;border:2px dashed;padding:.08in;text-align:center;display:flex;flex-direction:column;justify-content:space-around;font-size:9pt}.contact{border-radius:50%}.track{break-inside:avoid;border:1px solid;margin:.15in 0;padding:.1in}.track div{display:flex;flex-wrap:wrap}.track span{min-width:.38in;min-height:.38in;border:1px solid;display:grid;place-items:center;padding:.03in}.scenario{break-after:page}table{border-collapse:collapse;width:100%}th,td{border:1px solid;padding:.06in;text-align:left}@media print{h1{margin-top:0}}" +
                "</style></head><body><h1>" + E(title) + "</h1><main class='" + bodyClass + "'>" + body + "</main></body></html>";
        }

        private static string KindCode(FormationKind kind) => kind == FormationKind.CarrierGroup ? "CV" : kind == FormationKind.SurfaceGroup ? "SAG" : kind == FormationKind.Submarine ? "SS" : kind == FormationKind.AirGroup ? "AIR" : "LOG";
        private static string E(object value) => System.Net.WebUtility.HtmlEncode(value?.ToString() ?? string.Empty);
        private static void Write(string directory, string file, string contents) => File.WriteAllText(Path.Combine(directory, file), contents, new UTF8Encoding(false));

        [Serializable]
        private sealed class PrintManifest
        {
            public int Version;
            public List<string> ScenarioIds = new List<string>();
            public int FormationCardCount;
            public int EntropyCardCount;
            public int ResponseCardCount;
            public List<string> Files = new List<string>();
        }
    }
}
