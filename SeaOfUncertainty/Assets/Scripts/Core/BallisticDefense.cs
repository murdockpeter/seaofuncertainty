using System;
using System.Collections.Generic;
using System.Linq;

namespace SeaOfUncertainty.Core
{
    [Serializable]
    public sealed class BallisticDefenseProfile
    {
        public int InterceptionRating;
        public int Interceptors;
        public int Capacity;
        public int CoverageRadius;
        public List<ContactDomain> ProtectedDomains = new List<ContactDomain>();
        public bool CanProtectSelf = true;
        public bool SelfDefenseOnly;
        public bool RequireReady;
        public bool AllowShallowSubmarines;
        public bool AllowDeepSubmarines;
        public bool CanInterceptWhileDeep;

        public bool IsValid => InterceptionRating >= 0 && InterceptionRating <= 6 && Interceptors >= 0 && Capacity >= Interceptors && CoverageRadius >= 0
            && ProtectedDomains != null && ProtectedDomains.All(domain => domain != ContactDomain.Unknown && Enum.IsDefined(typeof(ContactDomain), domain))
            && ProtectedDomains.Distinct().Count() == ProtectedDomains.Count
            && (Capacity == 0 || InterceptionRating > 0 && ProtectedDomains.Count > 0 && (!SelfDefenseOnly || CanProtectSelf))
            && (!(AllowShallowSubmarines || AllowDeepSubmarines) || ProtectedDomains.Contains(ContactDomain.Subsurface))
            && (!ProtectedDomains.Contains(ContactDomain.Subsurface) || AllowShallowSubmarines || AllowDeepSubmarines);

        public BallisticDefenseProfile Clone() => new BallisticDefenseProfile
        {
            InterceptionRating = InterceptionRating, Interceptors = Interceptors, Capacity = Capacity, CoverageRadius = CoverageRadius,
            ProtectedDomains = ProtectedDomains == null ? null : new List<ContactDomain>(ProtectedDomains),
            CanProtectSelf = CanProtectSelf, SelfDefenseOnly = SelfDefenseOnly, RequireReady = RequireReady,
            AllowShallowSubmarines = AllowShallowSubmarines, AllowDeepSubmarines = AllowDeepSubmarines, CanInterceptWhileDeep = CanInterceptWhileDeep
        };

        public int EffectiveRating(FormationState provider) => Math.Max(0, InterceptionRating - (provider.Damage == DamageState.Heavy ? 2 : provider.Damage == DamageState.Light ? 1 : 0));
        public string Summary => $"Ballistic defense {Interceptors}/{Capacity}; rating {InterceptionRating}; radius {CoverageRadius} hexes";
    }

    public sealed partial class PrototypeGame
    {
        // Public presentation access must be explicitly scoped to the target's owner.
        public IReadOnlyList<FormationState> FriendlyBallisticCoverage(Side viewer, FormationState target)
            => target == null || target.Side != viewer || !Formations.Contains(target) ? new List<FormationState>() : EligibleBallisticDefenders(target);

        public string FriendlyBallisticDefenseStatus(Side viewer, FormationState provider, FormationState target = null)
        {
            if (provider == null || provider.Side != viewer || !Formations.Contains(provider)
                || target != null && (target.Side != viewer || !Formations.Contains(target))) return string.Empty;
            string reason = BallisticDefenseUnavailableReason(provider, target);
            return string.IsNullOrEmpty(reason) ? "Automatic interception ready; no stance or ordinary Reaction required." : reason;
        }

        public IReadOnlyList<FormationState> FriendlyBallisticProtectedFormations(Side viewer, FormationState provider)
            => provider == null || provider.Side != viewer || !Formations.Contains(provider) ? new List<FormationState>()
                : Formations.Where(target => target.Side == viewer && CanProvideBallisticDefense(provider, target)).OrderBy(target => target.Id, StringComparer.Ordinal).ToList();

        public bool FriendlyBallisticDefenseAvailable(Side viewer, FormationState provider)
            => provider != null && provider.Side == viewer && Formations.Contains(provider) && string.IsNullOrEmpty(BallisticDefenseUnavailableReason(provider, null));

        public IReadOnlyList<HexCoord> FriendlyBallisticCoverageHexes(Side viewer, FormationState provider)
        {
            var hexes = new List<HexCoord>();
            if (provider == null || provider.Side != viewer || !Formations.Contains(provider) || provider.IsDestroyed
                || provider.BallisticDefense == null || !provider.BallisticDefense.IsValid || provider.BallisticDefense.Capacity == 0 || !Area.Contains(provider.Position)) return hexes;
            for (int q = 0; q < Area.Width; q++) for (int r = 0; r < Area.Height; r++)
            {
                var hex = new HexCoord(q, r);
                if (HexCoord.Distance(provider.Position, hex) <= (provider.BallisticDefense.SelfDefenseOnly ? 0 : provider.BallisticDefense.CoverageRadius)) hexes.Add(hex);
            }
            return hexes;
        }

        private List<FormationState> EligibleBallisticDefenders(FormationState target)
            => Formations.Where(provider => CanProvideBallisticDefense(provider, target))
                .OrderByDescending(provider => provider.BallisticDefense.EffectiveRating(provider))
                .ThenBy(provider => HexCoord.Distance(provider.Position, target.Position))
                .ThenBy(provider => provider.Id, StringComparer.Ordinal).ToList();

        private bool CanProvideBallisticDefense(FormationState provider, FormationState target)
            => target != null && string.IsNullOrEmpty(BallisticDefenseUnavailableReason(provider, target));

        private string BallisticDefenseUnavailableReason(FormationState provider, FormationState target)
        {
            BallisticDefenseProfile profile = provider.BallisticDefense;
            if (profile == null || !profile.IsValid) return "Invalid ballistic defense profile.";
            if (profile.Capacity == 0) return "No ballistic interception equipment.";
            if (provider.IsDestroyed) return "Defender destroyed.";
            if (provider.Damage >= DamageState.Crippled) return "Defender crippled; cannot intercept.";
            if (!Area.Contains(provider.Position)) return "Defender outside the operational area.";
            if (provider.Replenishing) return "Defender replenishing; interception unavailable until Ready.";
            if (profile.Interceptors <= 0) return "Interceptor stocks depleted; replenish at a compatible service site.";
            if (profile.EffectiveRating(provider) <= 0) return "Damage has reduced interception rating to zero.";
            if (profile.RequireReady && provider.ReadyTime > Time) return "This defense profile requires the defender to be Ready.";
            if (provider.HasEffect(EntropyCardIds.LauncherDamage)) return "Launcher Damage blocks interception.";
            if (provider.HasEffect(EntropyCardIds.MagazineDamage)) return "Magazine Damage blocks interception.";
            if (provider.Kind == FormationKind.Submarine && provider.SubmarineDepth == SubmarineDepthState.Deep && !profile.CanInterceptWhileDeep) return "This defender cannot intercept while deep.";
            if (target == null) return string.Empty;
            if (provider.Side != target.Side) return "Only friendly formations can be protected.";
            if (target.IsDestroyed || !Area.Contains(target.Position)) return "Protected formation unavailable.";
            if (provider == target && !profile.CanProtectSelf) return "This profile excludes self-defense.";
            if (provider != target && profile.SelfDefenseOnly) return "This profile provides self-defense only.";
            if (HexCoord.Distance(provider.Position, target.Position) > profile.CoverageRadius) return "Outside this defender's coverage radius.";
            if (!profile.ProtectedDomains.Contains(DomainFor(target.Kind))) return "This defender does not protect that target domain.";
            if (target.Kind == FormationKind.Submarine && !(target.SubmarineDepth == SubmarineDepthState.Deep ? profile.AllowDeepSubmarines : profile.AllowShallowSubmarines)) return "This defender does not protect submarines at that depth.";
            return string.Empty;
        }

        // Called only after a legal ballistic attack commits against an eligible real
        // target. Its return value has no provider identity or location.
        private int CommitBallisticInterception(FormationState target)
        {
            FormationState provider = EligibleBallisticDefenders(target).FirstOrDefault();
            if (provider == null) return 0;
            int modifier = provider.BallisticDefense.EffectiveRating(provider);
            provider.BallisticDefense.Interceptors--;
            string receipt = $"{provider.Id} {provider.Name} protected {target.Id} {target.Name}; ballistic interception +{modifier}; interceptors {provider.BallisticDefense.Interceptors}/{provider.BallisticDefense.Capacity}. Ordinary Reaction unchanged.";
            AddLog($"T{Time:00}  {receipt}", target.Side);
            CommandEvent?.Invoke(provider, "BallisticInterception", receipt);
            return modifier;
        }

        public bool HasInterceptorReloadAccess(FormationState formation) => formation != null && HasLogisticsAccess(formation)
            && Scenario.LogisticsRegions.Any(region => region.SupportsInterceptorReload && region.Hexes.Contains(formation.Position));
    }
}
