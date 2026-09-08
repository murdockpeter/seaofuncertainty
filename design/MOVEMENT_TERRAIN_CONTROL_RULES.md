# Movement, Terrain, Stacking, and Control Rulings

Status: approved prototype ruling, 2026-09-08.

These rules close the immediate movement decisions while keeping weather and sea-state effects deferred until paired playtests justify additional movement detail.

## Movement routes

- A Move uses a deterministic shortest legal route of no more hexes than the formation's effective allowance.
- When equal-length routes exist, the resolver prefers Deep Water, then Littoral, then Strait hexes, followed by stable coordinate order.
- Every entered hex is validated. Land, restricted areas, and Strait stopping cannot be bypassed by selecting a legal destination beyond them.
- Friendly or enemy occupancy does not block transit through a hex.
- The route is public only to the moving side. It must not disclose hidden enemy occupancy.

## Signature duration

- Cautious movement applies Signature -1 after the Move resolves.
- High Tempo movement makes the formation Loud after the Move resolves, equivalent to Signature +1.
- Either movement state persists while other formations act and through reactions. It expires when that formation completes its next Action.
- The completed Action may establish a new Signature state of its own: Active Search and Aggressive Screen are Loud; Cautious and High Tempo Move establish their respective movement states; other Actions leave the formation at its printed Signature.

This gives opponents a full opportunity window to exploit High Tempo and gives Cautious movement a full concealment window without requiring duration counters.

## Terrain and restricted areas

- Deep Water has no movement effect.
- Entering one or more Littoral hexes during a non-air Move adds +1 Ready-Time total, not +1 per hex.
- Non-air formations cannot enter Land except that a Port or Anchorage may be the final hex. A Land facility cannot be used as a transit hex.
- Restricted areas are impassable: formations cannot enter, cross, or end in them. This applies to air and naval formations unless a later scenario special rule explicitly overrides it.
- Air groups ignore Land, Littoral cost, and Strait stopping, but not restricted areas.

## Straits

- A non-air formation may enter a Strait normally, but entry ends its route for that Action.
- A formation beginning its Action in a Strait may move out normally.
- Straits do not add Ready-Time by themselves.

This makes a named Strait a meaningful one-Action chokepoint without adding facing, traffic capacity, or automatic detection rules.

## Stacking and occupancy

- Only one friendly formation may end an Action in a hex. Friendly formations may pass through one another.
- Opposing formations may occupy the same 20 nm hex. Enemy occupancy never blocks a Move or makes the destination appear illegal, because doing so would reveal hidden information.
- Co-occupancy does not create automatic Contact, combat, or interception. Those results still require Search, Strike, or Patrol/Screen rules.
- Air groups follow the same friendly end-of-Move stacking limit in this prototype.

## Objective control

- A side has control presence when at least one non-air, non-Crippled, non-Destroyed formation is within one hex of the designated objective.
- A side controls the objective only when it has control presence and the opponent does not. Otherwise the objective is neutral or contested.
- Air groups cannot establish or contest control. Crippled and Destroyed formations cannot establish or contest control.
- Final control is adjudicated from actual formation positions. During play, the map may show the viewing side's own presence but must not reveal hidden enemy presence through a control marker.

This remains the generic control rule. Scenario data now layers transit, escort, denial, and withdrawal scoring onto it at the configured horizon.

## Explicitly deferred

Weather and sea-state movement effects remain inactive. They should be added only after movement, route, chokepoint, and control playtests show that the extra modifier improves decisions enough to justify its complexity.
