# Entropy and Recovery Rules

These rulings close the prototype's Entropy and recovery questions for the current 36-card deck.

## Action classifications

- **Complex Actions:** Move, Search, Strike, Patrol, and Support. A Disorganized formation must Push Through before one of these Actions. Universal Friction adds +1 Time to each unless explicitly ignored.
- **Major Actions:** Move, Search, and Strike. These alone advance the three-action Endurance track.
- **Other Actions:** Recover, Replenish, and Hold are neither complex nor major.

## Sources and stacking

A formation may hold multiple cards from the same source. Every new Entropy event draws and attaches another physical card even when that source is already marked. The source remains marked while at least one matching card remains attached.

Universal source penalties and individual card effects stack. For example, Friction adds +1 Time to a Synchronization Support and F-05 Coordination Drift adds another +1 Time. An explicit cancel, suppression, or Accept the Risk effect is the only exception.

Light damage does not mark Destruction. Worsening to Heavy, Crippled, or Destroyed does. Recover removes one selected Friction or Disruption card; Replenishment repairs damage and one selected Destruction card under the logistics rules.

## Printed Entropy responses

F-01, F-02, and F-07 are the only Entropy cards with a printed Command response in the current deck.

- The response opens when the card is revealed.
- It remains available until the affected formation completes its next own Action.
- It costs one free Command Slot.
- That Slot remains occupied until the affected formation completes its next own Action.
- Playing the response resolves the card's mechanical effect but leaves the physical card attached until Recover; the source therefore remains marked when appropriate.
- Response-window state is authoritative and survives save/load.

## Previously deferred cards

- **F-02 Staff Overload:** blocks Synchronization Support. It resolves after the formation completes another own Action, or immediately through its printed one-Slot response.
- **F-05 Coordination Drift:** adds +1 Time when the formation assigns Synchronization Support. This stacks with universal Friction.
- **D-03 Contradictory Reports:** adds a second adjacent possible-position center to one owned Contact. The possible area is the union around both centers; a successful Search or observed displacement restores a single plot.
- **D-12 Compromised Plot:** blocks Synchronization Support until the card is removed through Recover or another legal effect-removal mechanism.

Synchronization Support is the active bridge for these coordination effects until the larger synchronized-strike event system is implemented. Those future procedures must call the same participation and timing rules rather than introduce a second interpretation.
