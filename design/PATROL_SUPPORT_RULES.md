# Patrol / Screen and Support Rules

These rulings are authoritative for the current prototype slice.

## Patrol / Screen

- Patrol is a one-Time action. The acting formation selects its own hex or an adjacent hex as the center of a radius-one Screen area.
- The assignment may name a friendly formation in the area, the objective, or the area itself. A future plotted-route system may allow a route segment to become the named subject without changing the radius rule.
- A Screen persists until the screening formation is destroyed or completes a non-Patrol action. Reassigning Patrol replaces its old Screen and refreshes its interception.
- The first enemy formation that completes movement inside the area triggers the Screen's one extra interception. This is separate from the screener's ordinary Reaction and does not change Ready Time.
- Interception resolves immediately after the destination is committed: the screener makes a Light-strength attack, creates or refreshes a Contact on the mover, and spends the Screen interception whether or not damage is caused.
- Defensive posture grants friendly formations in the area +1 Defense against a Strike. Balanced posture has no additional modifier. Aggressive posture grants +1 interception Attack and makes the screener Loud while the Screen persists.
- F-10 ignores the first Defensive Screen or Support bonus received by its formation.

## Support

- Support is a one-Time action with range two. The supporter selects another friendly formation and one effect: Strike, Search, Defense, ASW Search, or Synchronization.
- The +1 persists until the matching action/defense uses it, either participant leaves range, the supporter is destroyed, or the supporter completes another action. A new assignment by that supporter replaces its old assignment.
- Support bonuses of the same kind do not stack on one recipient; a new assignment replaces the existing one. Search and ASW Search are capped at a combined +1 against submarines.
- Strike adds +1 Attack to the recipient's next Strike. Search adds +1 Search to its next Search. Defense adds +1 Defense against its next Strike. ASW Search adds +1 only against submarines in its next Search.
- Synchronization stores a +1 coordination modifier through the same persistent assignment API. Its consumption is intentionally deferred until the synchronized-action parent system exists.
- F-08 blocks Support after that formation makes a High Tempo Move until it Recovers. F-10 and D-06 ignore the first applicable received bonus. D-11 loses the formation's next Support action. X-08 prevents carrier/air-group air Support. C-24 reassigns an eligible active Screen or Support within the destination formation's area.

Patrol and Support assignment fields are serialized with formation state, rendered on the operational map, included in dossiers/timeline rows, and recorded in action telemetry.
