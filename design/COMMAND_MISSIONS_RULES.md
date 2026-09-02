# Command Attention and Standing Mission Rules

These rulings are authoritative for the current prototype slice.

## Vocabulary

- **Assign** writes a persistent Standing Mission or attaches a persistent relationship such as Support. Assignment itself is not consumption.
- **Occupy** changes a Command Slot from Free to Occupied until its printed or declared release condition. Occupied Slots return to Free automatically.
- **Spend** is reserved for a card, capability, or resource that leaves its current pool. The rules do not use “spend a Slot” when the Slot will later return.
- **Use** is informal interface language only and does not define lifecycle.
- **Strain** is accumulated command-system damage. Each full two Command Strain changes one of the three Slots to Strained and unavailable. Strain is capped at six.

## Individual Command Slots

Each side owns three individually serialized Slots with a status, purpose, associated formation, and release condition:

- **Free:** available to occupy.
- **Occupied:** committed to a Focused Search, immediate retask, Mission change, delayed order, or printed Command response.
- **Strained:** unavailable because of accumulated Command Strain.

Focused Search and an immediate out-of-Mission action release their Slots when that action resolves. An immediate Standing Mission change releases its attention when the ordered formation next acts. A Communications Latency order releases at delivery Time. Printed entropy responses occupy attention until the affected formation next acts. A newly reached Strain threshold converts a Free Slot when possible; if every Slot is occupied, the reduction is applied as soon as one releases.

## Standing Mission vocabulary

A Standing Mission contains four fields:

- **Task:** one of the eight playable actions.
- **Objective:** Current Area, Operational Objective, Friendly Formation, Contact, or Logistics Facility, with an associated hex or ID.
- **Posture:** Cautious, Balanced, or Aggressive guidance.
- **Trigger:** On Ready, Contact Located, Entropy Marked, Logistics Required, or Objective Reached.

Task is the authoritative test for whether an action follows the Mission. Objective and Posture are explicit commander intent stored for UI, AI, and later route/doctrine expansion; they do not silently automate movement.

## Attention and triggers

- An action whose kind matches Task follows the Standing Mission and occupies no Command Attention.
- A legal immediate action of another kind occupies one Slot until action resolution but does not overwrite the Standing Mission.
- A conditional Trigger may automatically change Task without attention: Contact Located authorizes Strike, Entropy Marked authorizes Recover, Logistics Required authorizes Replenish, and Objective Reached authorizes Patrol. The action must still pass its ordinary legality checks.
- Broken Link permits only the existing Task. C-03 Mission Command prepares one preplanned Trigger change through Broken Link; C-17 Flash Order sends an immediate chosen Task through Broken Link and marks one Command Strain.
- Communications Latency delivers a Mission change one Time later while the existing Mission continues. Confused Priorities occupies a second Slot. Hasty Retasking adds +1 Time to the formation’s next Ready schedule.
- C-07 Delegate Authority locks Mission editing until the formation completes its next action.

## Command recovery and forced action

A Disorganized formation must **Push Through** before a complex Move, Search, Strike, Patrol, or Support action. Push Through marks one Command Strain and authorizes one successfully committed complex action; a rejected action does not consume the authorization.

**HQ Recovery** is a two-Time Recover action available to an active Carrier Group at a compatible logistics facility. It removes up to two Command Strain, restoring one Strained Slot when a threshold is crossed.

Mission changes, Trigger changes, out-of-Mission orders, Slot occupation/releases, Strain, Push Through, and HQ Recovery produce dedicated playtest telemetry events in addition to the event feed.
