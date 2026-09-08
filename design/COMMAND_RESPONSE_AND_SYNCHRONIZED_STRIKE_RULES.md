# Command Responses and Synchronized Strikes

Status: implemented prototype ruling, 2026-09-08.

## Command Response deck

- Each side has one copy of each C-01 through C-24, shuffled from the scenario seed. Each side begins with three cards.
- The hand limit is five. Every third completed Major Action by that side (Move, Search, or Strike) earns one draw. A full hand defers the earned draw; playing a card immediately awards any deferred draw that now fits.
- When the draw pile is empty, the discard is returned by a deterministic reverse-cut. This gives stable save/replay results without persisting additional random-generator state.
- Ordinary Responses are played during the owning side's planning window, while one of its formations is active. C-16 Covering Fires and C-18 Orderly Withdrawal also have a Reaction-preparation window before a committed attack resolves, and persist until the target's next eligible Reaction.
- C-02 Prior Planning and C-10 Deconfliction Cell have an atomic declaration window: they can be committed only after the Synchronized Strike's Contact, aim, and participants have been selected. A failed declaration does not discard either card.
- All successful plays discard the card. Response deck, hand, discard, and pending draw progress are saved.

## Synchronized Strike declaration

- The active formation is the leader. Select two to four friendly participants including the leader, one owned usable Contact, one aim inside its possible area, and one Salvo type used by every participant.
- Every participant must be alive, unreserved, able to participate in synchronization, able to use the selected Salvo, and in range of the aim. A Disorganized participant must already have Push Through prepared.
- Strike Time is the later of T+1 and the latest participant Ready Time. Participants are immediately reserved at that time and may take no intervening actions. The leader alone represents the event in the Ready queue.
- One Command Slot is occupied at declaration and held until resolution or abort. A side may have one pending Synchronized Strike.
- C-02 ignores the first Friction that the event would generate. C-10 names one participant that ignores its +1 Time Coordination Drift.

## Resolution, reaction, and uncertainty

- Participants are validated again at Strike Time. A missing, destroyed, unreserved, or out-of-range participant prevents resolution and requires abort or a legal retask.
- The defender receives one Reaction for the entire event. Its defensive modifier and any Screen/Defense Support apply to the first attack only. Evade and Counterattack occur after the volley.
- Attacks resolve in stable participant order. The target suffers cumulative Defense −1 on the second attack, −2 on the third, and −3 on the fourth. Attacks cease to damage a target once it is Destroyed, but every participant still completes and schedules its committed Strike action.
- A degraded but usable Contact uses its current quality at resolution. If it is lost, the owner must explicitly abort, retask, or continue blind against the declared aim. Continuing blind uses the declaration snapshot for targeting and reveals only “no confirmed effect” on an empty, stale, or false aim.
- Retask requires a new usable owned Contact and an aim in range of all participants. It adds +1 Time to every participant's post-strike schedule and marks leader Friction.
- Abort is available at Strike Time. No Salvo is fired; all participants schedule at T+1, leader Friction is marked, reservations clear, and Command releases.
- Destruction of any reserved participant before Strike Time forces an immediate abort so a destroyed leader cannot strand the Ready queue; surviving participants retain at least T+1 Ready, Command releases, and the surviving leader marks Friction.
- Every participant receives +1 Time Coordination Drift after resolution unless named by C-10 or covered by stored Synchronization Support; that Support is consumed at declaration. With three or four independent participants, each participant marks Friction; C-02 suppresses the first such mark in stable order.

## Presentation, AI, persistence, and tests

- Pending events appear above the formation Ready queue with Strike Time, participant count, aim, and held-Command status for their owning side.
- The OPFOR declares only from owned high-location, identified Contacts, chooses eligible participants without consulting hidden enemy state, resolves the same one-Reaction sequence, and evaluates ordinary Responses only from its hand and owned/public state.
- Save format 10 persists pending events, reservations, Response draw progress, and both synchronized-card commitments.
- `SynchronizedStrikeFeatureTests.Run` deterministically covers declaration, C-02/C-10 consumption, Command occupation/release, save/load, scheduling, sequential Defense, Friction, replacement draw, lost-Contact abort, and AI Response play.
