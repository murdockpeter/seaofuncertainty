# Reaction Rules - Prototype Ruling

Approved for the current digital prototype on **2026-09-02**.

## Timing and refresh

1. The attacker selects a target and commits a Salvo.
2. Before combat is rolled, the defender chooses one legal Reaction.
3. Resolve the incoming Strike.
4. Resolve post-combat Evade movement or a Counterattack, when applicable.
5. Mark the defender's Reaction spent.

A Reaction does not change Ready Time. A formation may react once between its own Actions. Its Reaction refreshes immediately after it completes its own Action; advancing operational Time alone does not refresh it.

## Reaction effects

- **Defend:** +1 Defense for the incoming Strike. No movement or return attack.
- **Evade:** +1 Defense, then move one valid hex after combat if the formation survives. The destination must be in the operational area, legal for the formation, unoccupied, and farther from the attacker's last-known position when the defender has such a Contact. If the origin is unknown, any otherwise legal adjacent destination may be selected.
- **Counterattack:** No Defense bonus. If the defender survives, it resolves one Light return Strike using a usable defender-owned Contact on the attacker. It does not schedule a separate Action or alter Ready Time.
- **Hold:** No modifier. Preserve position and spend the Reaction.
- **None:** Used automatically when the Reaction is already spent or unavailable; it is not a player choice.

C-18 Orderly Withdrawal is prepared before combat and extends one chosen Evade from one hex to up to two hexes. It is consumed when that Evade resolves, including when no safer legal destination remains.

## Restrictions

- A formation whose Reaction is spent cannot react again until completing its own Action.
- Disrupted or Disorganized formations cannot Counterattack.
- Crippled formations may only Defend or Hold; they cannot Evade or Counterattack.
- Fire Control Hit (X-09) prevents Counterattack.
- A formation without a usable owned Contact on the attacker cannot Counterattack.
- A replenishing formation cannot react.
- A Destroyed formation cannot perform post-combat movement or Counterattack.

## Control and privacy

- In **Solo vs AI**, the human chooses for human-controlled defenders and the deterministic AI chooses for AI defenders.
- In **Local Hotseat**, the committed attack is followed by a full-screen secure handoff before the defender sees the Reaction choices.
- The defender sees an attacker name only through an Identified defender-owned Contact. Otherwise the UI shows a generic hostile Contact or unknown strike origin.
- Evade legality uses the defender's last-known attacker position, never hidden authoritative attacker coordinates. An unknown origin does not disclose direction through destination filtering.

These rules are deterministic except for the normal bounded combat rolls and remain independent of the 3D presentation.
