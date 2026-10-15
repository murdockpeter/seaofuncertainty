using System;
using System.Collections.Generic;
using System.Linq;

namespace SeaOfUncertainty.Core
{
    [Serializable]
    public sealed class BallisticWeaponProfile
    {
        public int Range;
        public int AttackModifier;
        public int Ammunition;
        public int Capacity;
        public LocationQuality MinimumLocation = LocationQuality.Medium;
        public IdentityQuality MinimumIdentity = IdentityQuality.General;
        public List<ContactDomain> TargetDomains = new List<ContactDomain>();
        public bool AllowShallowSubmarines;
        public bool AllowDeepSubmarines;

        public bool IsValid => Range >= 0 && AttackModifier >= -6 && AttackModifier <= 6 && Capacity >= 0 && Ammunition >= 0 && Ammunition <= Capacity
            && Enum.IsDefined(typeof(LocationQuality), MinimumLocation) && Enum.IsDefined(typeof(IdentityQuality), MinimumIdentity)
            && TargetDomains != null && TargetDomains.All(domain => Enum.IsDefined(typeof(ContactDomain), domain) && domain != ContactDomain.Unknown)
            && TargetDomains.Distinct().Count() == TargetDomains.Count
            && (Capacity == 0 || Range > 0 && TargetDomains.Count > 0 && MinimumIdentity >= IdentityQuality.General)
            && (!(AllowDeepSubmarines || AllowShallowSubmarines) || TargetDomains.Contains(ContactDomain.Subsurface))
            && (!TargetDomains.Contains(ContactDomain.Subsurface) || AllowDeepSubmarines || AllowShallowSubmarines);

        public BallisticWeaponProfile Clone() => new BallisticWeaponProfile
        {
            Range = Range, AttackModifier = AttackModifier, Ammunition = Ammunition, Capacity = Capacity,
            MinimumLocation = MinimumLocation, MinimumIdentity = MinimumIdentity,
            TargetDomains = TargetDomains == null ? null : new List<ContactDomain>(TargetDomains),
            AllowShallowSubmarines = AllowShallowSubmarines, AllowDeepSubmarines = AllowDeepSubmarines
        };

        public bool CanLaunch(FormationState formation) => IsValid && Range > 0 && Capacity > 0 && Ammunition > 0
            && formation != null && !formation.IsDestroyed && !formation.Replenishing && formation.Damage < DamageState.Crippled
            && !formation.HasEffect(EntropyCardIds.LauncherDamage) && !formation.HasEffect(EntropyCardIds.MagazineDamage);

        public string Summary => $"Ballistic {Ammunition}/{Capacity}; range {Range} hexes; attack {AttackModifier:+0;-0;0}";
    }

    public sealed partial class PrototypeGame
    {
        // This preview uses the owned report only. Actual domain/depth/occupancy is
        // checked after commitment and never changes the reason shown to the attacker.
        public bool CanStrikeContact(FormationState attacker, ContactState contact, HexCoord aim, Salvo salvo, WeaponDelivery delivery, out string message)
        {
            if (attacker == null || !Formations.Contains(attacker) || !Enum.IsDefined(typeof(Salvo), salvo) || !Enum.IsDefined(typeof(WeaponDelivery), delivery))
            { message = "Choose a valid owned formation, weapon, and salvo."; return false; }
            if (contact == null || contact.Owner != attacker.Side || contact.IsLost || !Contacts.Contains(contact))
            { message = "Choose one of your usable Contacts."; return false; }
            if (!ContactPossibleHexes(contact).Contains(aim)) { message = "Choose an aim hex inside the Contact's possible area."; return false; }
            int range = StrikeRangeFor(attacker, salvo, delivery);
            if (HexCoord.Distance(attacker.Position, aim) > range) { message = $"{delivery} Strike range is {range} hexes ({range * Area.NauticalMilesPerHex} nm)."; return false; }
            if (delivery == WeaponDelivery.Ballistic)
            {
                BallisticWeaponProfile profile = attacker.BallisticWeapons;
                if (profile == null || !profile.CanLaunch(attacker)) { message = "Ballistic launch capability is unavailable, damaged, or out of ammunition."; return false; }
                if (salvo == Salvo.Heavy && (attacker.Endurance == Endurance.Critical || attacker.Damage == DamageState.Crippled))
                { message = "Heavy commitment is unavailable at Critical Endurance or Crippled damage."; return false; }
                if (profile.TargetDomains == null || !profile.TargetDomains.Contains(contact.Domain) || contact.Domain == ContactDomain.Unknown
                    || contact.Location < profile.MinimumLocation || contact.Identity < profile.MinimumIdentity)
                { message = "The Contact needs a permitted domain and the ballistic profile's minimum location and identity quality."; return false; }
            }
            else
            {
                if (!attacker.CanFire(salvo)) { message = $"{salvo} weapon inventory is exhausted or unavailable to this formation."; return false; }
                if (contact.Domain == ContactDomain.Subsurface && salvo != Salvo.Light && contact.Identity != IdentityQuality.Identified)
                { message = "Standard and Heavy attacks against a submarine require an Identified ASW datum."; return false; }
            }
            message = "Legal Contact-area strike; hidden occupancy is resolved after commitment.";
            return true;
        }

        public bool IsBallisticTargetEligible(FormationState attacker, FormationState target)
        {
            BallisticWeaponProfile profile = attacker?.BallisticWeapons;
            if (attacker == null || target == null || target.IsDestroyed || target.Side == attacker.Side || profile?.TargetDomains == null || !profile.TargetDomains.Contains(DomainFor(target.Kind))) return false;
            return target.Kind != FormationKind.Submarine || (target.SubmarineDepth == SubmarineDepthState.Deep ? profile.AllowDeepSubmarines : profile.AllowShallowSubmarines);
        }

        public bool HasBallisticReloadAccess(FormationState formation) => formation != null && HasLogisticsAccess(formation)
            && Scenario.LogisticsRegions.Any(region => region.SupportsBallisticReload && region.Hexes.Contains(formation.Position));
    }
}
