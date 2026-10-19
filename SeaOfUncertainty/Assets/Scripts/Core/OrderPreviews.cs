using System.Linq;

namespace SeaOfUncertainty.Core
{
    public sealed partial class PrototypeGame
    {
        public bool CanAuthorizeAction(FormationState formation, ActionKind action, out string message)
        {
            if (formation == null || !Formations.Contains(formation) || formation.IsDestroyed)
            { message = "Choose an available friendly formation."; return false; }
            return AuthorizeAction(formation, action, out message, false);
        }

        public bool CanPlanAction(FormationState actor, ActionKind action, out string message)
        {
            if (actor == null || actor != Active) { message = "Only the highlighted Ready formation may act."; return false; }
            if (!CanAuthorizeAction(actor, action, out message)) return false;
            switch (action)
            {
                case ActionKind.Recover:
                    if (!actor.Friction && !actor.Disruption) { message = "No Friction or Disruption to recover. Use Hold to wait quietly; use Replenish for damage or Destruction."; return false; }
                    break;
                case ActionKind.Replenish:
                    if (!HasLogisticsAccess(actor)) { message = "No compatible logistics access here. Inspect facilities, then Move to a compatible port, anchorage, airfield or fleet train."; return false; }
                    if (!NeedsReplenishment(actor)) { message = "Nothing can be restored. Use Hold to wait quietly."; return false; }
                    break;
                case ActionKind.Support:
                    if (actor.SupportBlockedUntilRecover || actor.HasEffect(EntropyCardIds.HangarDamage) && (actor.Kind == FormationKind.AirGroup || actor.Kind == FormationKind.CarrierGroup))
                    { message = "Support is blocked by an attached effect. Inspect its card and Recover or repair it."; return false; }
                    if (!Formations.Any(item => item.Side == actor.Side && item != actor && !item.IsDestroyed && HexCoord.Distance(actor.Position, item.Position) <= Rules.SupportRange))
                    { message = $"No other friendly formation within Support range {Rules.SupportRange}. Move closer or choose another order."; return false; }
                    break;
                case ActionKind.Move:
                    if (LegalMoveDestinations(actor, MoveMode.Normal).Count == 0 && LegalMoveDestinations(actor, MoveMode.Cautious).Count == 0 && LegalMoveDestinations(actor, MoveMode.HighTempo).Count == 0)
                    { message = "No legal movement destination. Inspect terrain and friendly occupancy, or choose another order."; return false; }
                    break;
            }
            message = OrderCost(actor, action);
            return true;
        }

        public bool CanPlanStrike(FormationState actor, Salvo commitment, WeaponDelivery delivery, out string message)
        {
            if (!CanPlanAction(actor, ActionKind.Strike, out message)) return false;
            if (delivery == WeaponDelivery.Ballistic ? actor.BallisticWeapons == null || !actor.BallisticWeapons.CanLaunch(actor) : !actor.CanFire(commitment))
            { message = "Weapon unavailable: check ammunition, damage, servicing and attached effects. Replenish at compatible logistics or choose another weapon/order."; return false; }
            if (commitment == Salvo.Heavy && actor.Endurance == Endurance.Critical)
            { message = "Critical Endurance prevents Heavy commitment. Choose Light/Standard or Replenish."; return false; }
            message = OrderCost(actor, ActionKind.Strike) + (delivery == WeaponDelivery.Ballistic ? " One ballistic package, including a miss; launch increases signature." : $" {commitment} magazine expenditure.");
            return true;
        }

        public string SynchronizedParticipantReason(FormationState formation, ContactState contact, HexCoord aim, Salvo commitment, WeaponDelivery delivery)
        {
            if (!string.IsNullOrEmpty(formation.SynchronizedStrikeId)) return "Already reserved; resolve or abort the existing strike.";
            if (!CanParticipateInSynchronization(formation)) return "Coordination blocked by damage or attached effects; inspect and Recover/repair.";
            if (formation.EntropySources >= 3 && !formation.PushThroughReady) return "Disorganized: prepare Push Through before reservation.";
            if (!CanStrikeContact(formation, contact, aim, commitment, delivery, out string reason)) return reason + " Choose another weapon, report, aim or participant.";
            return "Eligible; reserved until the volley resolves or is aborted.";
        }

        public string OrderCost(FormationState actor, ActionKind action)
        {
            int command = ActionFollowsMission(actor, action) || TriggeredMissionFor(actor) == action ? 0 : 1;
            return $"Base Time {Rules.ActionTime(action)}; terrain, commitment and attached effects can add Time. Command {command} for this order, released at action resolution. Inspect the final destination/commitment preview.";
        }
    }
}
