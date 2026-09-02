# Replenishment and Logistics Rules

These rulings are authoritative for the current prototype slice.

## Access

- Replenish is legal only while the active formation occupies a compatible facility whose hex is included in one of the scenario's logistics regions.
- Carrier groups, surface groups, and submarines use Ports or Anchorages. Air groups use Airfields.
- A data-defined Port or Anchorage is a legal naval movement endpoint even when its underlying terrain is Land.
- Facilities have no side ownership in the present scenario schema. Either side may use an accessible facility it occupies; ownership and contested-service rules remain a future scenario-layer concern.

## Service package

Replenish has a base cost of three Time. One action performs every applicable item below:

- improve Endurance by one step, from Critical to Extended or Extended to Ready;
- reload the expended Heavy Salvo capability;
- repair Damage by one step, from Crippled to Heavy, Heavy to Light, or Light to None;
- reset progress on the three-major-action Endurance track;
- repair and discard one player-selected attached Destruction card.

The action is unavailable when none of those items would change state. Additional Replenish actions may be used to continue restoring multi-step damage, Endurance, or additional Destruction cards.

## Timing and effects

- The restoration is applied when the action is committed, then the formation schedules Ready at base Time +3.
- The formation is marked Replenishing and cannot React during the service interval. That state clears when the formation next becomes Ready.
- X-10 Hull Breach adds +1 Time to Replenish. If X-10 is selected for repair, its added Time still applies to that action because costs are established before repair.
- Replenish is quiet and ends any active Patrol or Support assignment like any other subsequent action.
- Replenishment state, repaired capability state, and Ready timing are serialized normally.

Meridian Veil now defines western and eastern Port/Airfield logistics regions. Northern Gateway continues to use the combined Basco Port and Airfield logistics hex.
