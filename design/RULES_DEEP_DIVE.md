# Rules Deep Dive — v0.5

## The game in one sentence

Sea of Uncertainty is not primarily a naval combat game; it is a tempo-and-information game in which naval combat is the irreversible way players cash in positional and informational advantage.

That distinction should govern both rules development and the digital interface. The central question is not “what can this unit attack?” but “what do I know, what can I make true before the enemy acts, and what future tempo am I sacrificing?”

## The core decision engine

Every meaningful decision changes one or more of six shared resources:

1. **Time** — when the formation acts again.
2. **Position** — geometry, control, screens, and access.
3. **Information** — Contact Location, Identity, and Age.
4. **Command** — scarce exceptions to routine mission execution.
5. **Entropy** — temporarily unavailable capability caused by friction, disruption, or destruction.
6. **Physical capability** — weapons, endurance, damage, and formation survival.

The best part of the design is that these resources are coupled. High Tempo gains Position but worsens Information security and Time. Focused Search gains Information but consumes Command and Time. Synchronization trades Time and information freshness for reduced enemy Defense. Recover restores capability but yields initiative. This is the correct foundation to protect.

## Primary loop

`Ready → Choose → Commit → React → Resolve → Apply Entropy → Schedule`

The published ten-step sequence is a teaching expansion of this loop. In digital form, advancing time, aging information, checking cohesion, and scheduling should be automated. The UI should keep the decisions visible while hiding maintenance.

## Highest-value hypotheses

### 1. Ready Time creates legible tempo

This is the chassis. It must make players ask “what can the enemy do before T08?” without producing timeline administration. A sorted formation timeline and explicit “Next Ready T##” preview are mandatory interface elements.

Primary risk: consecutive activations can feel like accidental double turns unless the UI explains why ordering occurred.

### 2. Contacts create repeated uncertainty

Location, Identity, and Age form a compact information model. It succeeds only if each axis changes different decisions:

- Location must affect targeting and geographic confidence.
- Identity must affect threat assessment and target value.
- Age must make waiting and synchronization risky.

Primary risk: the current Search success result asks the player to improve Location, improve Identity, or create a Contact, but gives no selection rule or tradeoff after success. If one improvement is routinely optimal, the choice is procedural rather than meaningful.

### 3. Combat validates preparation

Attack-minus-Defense bands are excellent for bounded luck: players can understand the quality of the decision before the die lands. The digital UI should show the full arithmetic and result probabilities before commitment.

Primary risk: the 2–5 middle range gives a 66.7% single outcome in every band. This is stable and readable, but may make repeated strikes feel mechanically flat. Test before adding more dice or sub-tables.

### 4. Entropy explains degradation

The three-source model is conceptually strong because it answers *why* capability is unavailable. The effect cards add texture, but they also create 18 special rules on top of the base status effects.

Primary risk: a newly marked entropy source currently applies both a universal penalty and a randomly selected effect. This may be double punishment, especially when High Tempo already paid Signature and Endurance costs.

### 5. Command Slots prioritize exceptions

Command works best as permission to break routine: retask now, focus sensors, synchronize, or push through. Slots should not become mana refreshed by an arbitrary round because the game has no rounds.

Primary risk: the rules do not define precisely when occupied Slots become free, how each side refreshes them, or whether “spend,” “use,” “assign,” and “occupy” represent different lifetimes.

## Rules completeness audit

### Operational clock

Defined: next Ready equals Action Time plus applicable Friction; advance directly to the next occupied time; same-time priority uses lower Entropy, higher Command, then 1d6, followed by alternation.

Needs definition:

- Whether “alternate Ready Formations” alternates sides or formations after the first tie winner.
- What happens when one side has multiple Ready formations and the other has none.
- Whether reactions alter Ready Time.
- When a formation regains its one reaction.
- Whether scheduled synchronized attacks are separate timeline entries.

Prototype assumption: sort globally by Ready Time, Entropy, Command, and stable ID. Reactions reset when operational time advances. This deterministic final tie-break is for reproducible testing; a future version should expose the intended 1d6 tie.

### Standing missions

Defined: Task, Objective, Posture, Trigger; following a mission is free; immediate retasking normally uses one Slot.

Needs definition:

- Formal mission vocabulary and legal combinations.
- What a Trigger authorizes automatically.
- Who decides whether an action “continues” a mission.
- Whether a Slot is spent, occupied until execution, or immediately released.

Recommendation: treat missions as soft constraints in the first prototype, then instrument every out-of-mission action. Do not automate movement until players have demonstrated that mission execution is genuinely rote.

### Movement and control

Defined: one/two/three hex commitments; Cautious changes Signature; High Tempo raises Signature and marks Friction; screens may react to entry.

Needs definition:

- Terrain, strait, stacking, occupancy, route, and control rules are now resolved in `MOVEMENT_TERRAIN_CONTROL_RULES.md`.
- Move rating caps mode distance.
- Cautious Signature -1 and High Tempo Loud persist until the formation completes its next Action.
- Patrol/Screen area size, intercept procedure, and “+1 interception Reaction” resolution.
- Evade destination legality.

Implemented ruling: movement validates deterministic routes; Land and restricted areas block transit; Littoral adds +1 Ready-Time per route; entering a Strait stops non-air movement; friendly formations cannot end stacked; hidden enemies do not block movement; combat-capable non-air formations control objectives within one hex unless contested. Weather and sea-state movement effects remain deferred.

### Search and contacts

Defined: calculation, success thresholds, contact axes, age descriptions, and degradation at Age 3+.

Previously unresolved questions (resolved in `SEARCH_SPATIAL_UNCERTAINTY_RULES.md`):

- Search selects a radius-one area centered on any legal in-range hex.
- Sensor maximum ranges are scenario data keyed by formation role and Search mode.
- The searching player declares Location or Identity priority before resolution.
- Success improves one step per Contact; excess margin grants no additional step.
- When Contacts age: every integer Time, only when time advances, or at the owning side’s activations.
- Whether movement updates a Contact’s possible location area.
- Successful Search verification privately disproves a False Contact.
- At Age 0, High, Medium, and Low are radius-zero, radius-one, and radius-two possible areas.

Implemented ruling: Contacts age by elapsed operational Time, degrade on reaching Age 3 and each further Time, and expand anonymously after tracked movement. Search resets successful Contacts to Age 0. See `SEARCH_SPATIAL_UNCERTAINTY_RULES.md`.

The map now renders the discrete possible-hex area rather than a decorative uncertainty label.

### Strike, reaction, and damage

Implemented: formation/salvo ranges, expenditure, targeting modifiers, player-selected reactions, attack/defense arithmetic, bounded results, damage, Screen and Support modifiers, and uncertain-area commitment. See `STRIKE_COMBAT_DAMAGE_RULES.md`.

Light damage applies −1 Defense and clears after the formation completes its next own Action. A higher incoming result replaces current damage, an equal nonzero result escalates one step, and a lower result is ignored. Crippled formations may Defend or Hold but cannot Evade or Counterattack.

A Strike chooses an owned Contact and then one hex in its possible area. Hidden occupancy is tested only after commitment. An empty, stale, or False Contact aim consumes the action and any committed Heavy weapon or Strike Support, then reports only “no confirmed effect.”

### Synchronization

Defined: one Slot, participants and Strike Time declared, readiness requirement, sequential Defense erosion, friction at three participants, and lost-contact choices.

Needs definition:

- Whether participants become reserved and may act before Strike Time.
- How Strike Time is chosen and whether it can equal current Time.
- Whether all attacks consume independent Strike actions and Ready scheduling.
- Number and timing of defender reactions.
- Meaning and numeric effect of “+1 synchronization” Support.
- Abort and retask costs.

Recommendation: implement only after individual Strike and Reaction are stable. Synchronization magnifies every ambiguity in those systems.

### Entropy and effect cards

Implemented: three universal sources, all 36 physical effect cards, stacking draws, source-aware recovery, private reveals, response windows, and save/load. See `ENTROPY_RECOVERY_RULES.md`.

Complex Actions are Move, Search, Strike, Patrol, and Support. Major Actions are Move, Search, and Strike. Universal source penalties stack with individual card effects unless an explicit response suppresses or ignores one.

F-02 and D-12 gate Synchronization Support, F-05 shifts it +1 Time, and D-03 adds a second adjacent center to the Contact's possible-area geometry. The three printed Entropy responses each cost one Command Slot and remain playable from reveal until the affected formation completes its next own Action.

### Command Attention and responses

Defined: three baseline Slots, six common uses, two Command Strain threshold, and 12 response cards.

Needs definition:

- Slot refresh/release timing.
- Hand limit, deck construction, discard, reshuffle, and draw triggers.
- Exact play windows for each response.
- HQ Recovery action, timing, and location.
- Whether Command Strain can exceed two.

Recommendation: model each Slot as `Free`, `Occupied until Time`, or `Strained`, rather than a single integer, once synchronization enters the build.

### Endurance and logistics

Implemented: Move, Search, and Strike are major Actions. Every third major Action degrades Endurance one step and resets the visible counter. Ready has no state penalty. At Extended, Recover takes +1 Time and High Tempo marks Friction. At Critical, Move is reduced by one, Heavy Salvo is prohibited, and every complex Action marks Friction. Scenario data defines typed logistics access, and Replenishment restores one Endurance step, reloads Heavy, repairs damage one step, resets the major-action track, and repairs one selected Destruction card.

### Victory and scenario

Implemented: each scenario owns scored Control, Transit, Escort, Denial, and Withdrawal objectives. Meridian Veil resolves them at T16. Operational points decide the result; combat-capable formations, lower total damage burden, and closest surviving naval formation break ties in that order. One-sided operational elimination is an immediate loss, while mutual elimination continues through the ordinary objective and tie-break sequence. Damage and destruction award no points directly.

## Scope order

### Slice 1 — implemented

Ready Time, eight formations, fogged contacts by side, contact aging, Move commitments, Search commitments, Strike salvos, automatic Defend, bounded combat, damage, basic entropy, endurance, command display, and action history.

### Slice 2 — next

Player-selected reactions, Patrol/Screen geometry, Support assignment, visible combat odds, effect-card draws, a real Command Slot lifecycle, and structured playtest telemetry.

### Slice 3

Synchronized strikes, Standing Mission editing and triggers, deception/false Contacts, replenishment/logistics access, and scenario scoring.

### Later, only if earned

Distinct air/surface/submarine procedures, layered missile defense, weapon inventories, electronic warfare detail, AI, asynchronous saves, and multiplayer authority.

## Digital/print parity

The core rules should remain a deterministic, presentation-independent model. Unity automates state transitions; the print-and-play version exposes the same transitions through cards, tracks, and markers. Every digital-only convenience should map to a physical procedure, and every physical component should have a clear state representation in the model.

This is also the correct multiplayer foundation: networking should synchronize declared commands and authoritative state transitions later, not UI objects now.
