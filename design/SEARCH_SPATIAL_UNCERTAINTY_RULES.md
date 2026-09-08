# Search and Spatial Uncertainty Rulings

Status: approved prototype ruling, 2026-09-08.

## Location geometry

At Age 0, Location quality defines a discrete possible area centered on the Contact's last known position:

- High: radius 0, one exact hex.
- Medium: radius 1, normally seven possible hexes.
- Low: radius 2, normally nineteen possible hexes.

Areas are clipped at operational-area boundaries. Land remains in an unidentified Contact's possible area because excluding it could reveal whether the Contact is an air group. The interface draws every possible hex rather than using a decorative ring with no rules meaning.

Age and observed movement add uncertainty without revealing a hidden route:

- Each elapsed Ready-Time expands the possible area by one ring, to a maximum of two additional rings.
- When a tracked formation Moves, opposing Contacts for it immediately gain one anonymous movement-expansion ring, to a maximum of two.
- Age and movement expansion do not add together; use the larger of the two. This prevents double-counting the same passage of operational time.
- Movement expansion reveals neither direction nor distance.
- A successful Search recenters the Contact on the actual hex and resets Age and movement expansion to zero.

## Search improvement

Before commitment, the searching player declares Location or Identity as the improvement priority.

- A successful detection creates a new Contact at Low Location and Unknown Identity, or improves the declared axis by one step.
- If the declared axis is already at its maximum, the success improves the other axis instead.
- A Search grants at most one step per detected Contact. Excess die-roll margin never grants additional steps.
- The declaration is made before hidden Signature, exact position, and the die roll are resolved.

## Contact aging and degradation

- Contact Age increases by the exact amount whenever the operational clock advances.
- Same-Time actions do not age Contacts.
- On reaching Age 3, Location degrades one step.
- Each further elapsed Time degrades Location one additional step.
- A degradation from Low makes the Contact Lost.
- Search resets Age to 0. Effects that only refresh Age do not secretly recenter a Contact or erase movement uncertainty.

## Sensor envelopes

Maximum Search range is data on each scenario, keyed by formation sensor role and Search mode. The prototype profiles are:

| Formation | Passive | Active | Focused |
| --- | ---: | ---: | ---: |
| Carrier group | 8 hex / 160 nm | 10 / 200 nm | 12 / 240 nm |
| Surface group | 7 / 140 nm | 9 / 180 nm | 11 / 220 nm |
| Submarine | 6 / 120 nm | 8 / 160 nm | 10 / 200 nm |
| Air group | 10 / 200 nm | 12 / 240 nm | 14 / 280 nm |

Scenarios may override these profiles. Data validation requires exactly one positive, ordered profile for every formation kind.

## False Contacts

- An undisproved False Contact uses exactly the same marker, summary, possible-area geometry, Search eligibility, mission-trigger test, and Strike eligibility as a real Contact of the same public quality.
- A Search attempts to verify every False Contact whose center lies in the selected radius-one footprint and inside the searcher's sensor envelope.
- Verification uses Search rating, mode, Support, and range, but no hidden target Signature.
- Success marks the Contact Lost and privately reports that it was disproved. Failure reports no detection and leaves it unchanged.
- A Strike selects one aim hex within the Contact's possible area. False, stale, and real Contacts remain behaviorally identical until hidden occupancy is tested after commitment.

## Information-discipline audit

| Path | Allowed information | Protected information | Enforcement |
| --- | --- | --- | --- |
| Map and dossier | Owned formations and owned Contact state | Enemy exact position, identity beyond quality, real/false status | Side-owned filtering; shared false/real marker; geometry derived only from public Contact fields |
| Search preview | Area, role-specific range, rating, mode, declared priority | Hidden Signature, exact target position, target existence in a blind area | Preview uses only searcher and owned/public data |
| Search resolution | Successful Contact improvement or private disproof | Failed target identity or exact position | Failures collapse to `no detections`; results are side-private |
| Strike preview | Attack, targeting, possible area, selected salvo | Hidden occupancy, target Defense and condition, defensive Support, reactions, and whether the Contact is real | Every Contact uses the same unresolved preview; exact odds are withheld until resolution |
| Movement | Anonymous one-ring expansion of an existing opposing Contact | Direction, route, distance, exact destination | Movement uncertainty stores only a capped ring count; action logs are side-private |
| Command Responses | Owner's hand, target, and result | Opponent hand, response target, False Window placement | Response and card logs are side-private; hands were already side-owned |
| Entropy cards | Owning formation's draw and attached effect | Opponent private draw and information effects | Draw notices and operational logs are side-private |
| AI | Its formations, owned Contacts, public terrain/objective | Opposing formation state behind Contacts | Candidate selection uses owned Contacts; no real/false branch |
| Save/load | Complete authoritative state | Cross-side display after restore | Save v8 preserves uncertainty, Light-damage duration, and side-scoped log audiences |

Automated regression coverage exercises blank-area failure text, selected improvement priority, excess-success cap, exact possible-hex counts, movement and Age expansion, degradation/loss timing, role-specific ranges, False Contact disproof and presentation, side-private logs, save/load, and AI completion.
