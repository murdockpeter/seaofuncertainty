# Strike, Combat, and Damage Rules

These rulings close the prototype's individual Strike, reaction, and damage questions. They apply to the digital model and map directly to a Contact marker, possible-area hexes, damage track, and spent-reaction marker in print.

## Strike commitment

1. Choose one owned, usable Contact.
2. Choose Light, Standard, or Heavy and one aim hex inside that Contact's current possible area and the attacker's range.
3. Confirm Heavy expenditure when applicable.
4. Commit the Strike. Only now test whether the Contact represents a live target whose actual hidden position is the aim hex.
5. If the aim is empty, stale, or false, consume the Strike, committed Strike Support, Heavy capability, and applicable attacker effects. Report only **no confirmed effect**; do not reveal which failure caused it.
6. If occupied, the eligible defender chooses a Reaction, then combat resolves normally.

Pre-commitment UI may show the attacker's rating, salvo and targeting modifiers, range, and possible area. It must not show hidden occupancy, real/false status, target condition, Defense, defensive Support, available Reactions, combat band, or odds. A successful hit may disclose the resolved arithmetic, but an unidentified target remains labeled by Contact area rather than formation name.

## Reactions

- Defend: +1 Defense.
- Evade: +1 Defense, then move to a legal farther hex after combat if the formation survives.
- Counterattack: make the existing Light return Strike after the incoming result if legal.
- Hold: no modifier and no movement.
- Crippled: Defend or Hold only.
- Replenishing, destroyed, or already-reacted: no Reaction.

A used Reaction remains spent until that formation completes its own Action. Reaction use does not change Ready Time.

## Damage

Damage is ordered `None < Light < Heavy < Crippled < Destroyed`.

- Light applies −1 Defense. It persists through the target's next completed own Action and then clears. If Light is inflicted during that formation's currently resolving Action, it persists through that Action and the following completed Action.
- Heavy and worse remain until repaired through Replenishment under the existing logistics rules.
- Higher incoming damage replaces current damage.
- Equal nonzero damage escalates one step: Light + Light → Heavy, Heavy + Heavy → Crippled, Crippled + Crippled → Destroyed.
- Lower incoming damage does not change the track.
- Worsening to Heavy or worse creates a Destruction event and draws a Destruction card.
- Destroyed formations leave play; Crippled formations retain their existing movement, Heavy-salvo, control, and reaction limits.
