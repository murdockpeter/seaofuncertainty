# Ballistic playtest in the current operations

Implemented content: 2026-10-15. Start a **New Operation** in Meridian Veil or Northern Gateway. Loading an existing save preserves that save's forces and equipment; it does not retrofit the new catalog loadouts. Both operations retain their existing executable control, transit, escort, denial and withdrawal scoring, duration, weather and scheduled events. Firing missiles does not create an additional scoring objective. Values below are provisional until human playtesting.

| Equipment | Meridian Veil | Northern Gateway |
|---|---|---|
| Blue submarine launcher | SSN Nightfall | SSN Wayfinder |
| Red submarine launcher | Hunter Submarine | Submarine Shade |
| Red land launcher | Battery Cinder, East Haven Airfield (10,8) | Battery Breakwater, Basco Airfield (11,10) |
| Blue escort defender | SAG Valiant | SAG Lantern |
| Red escort defender | Surface Action Two | SAG Monsoon |
| Blue carrier self-defense | CSG Resolute | CSG Horizon |
| Red carrier self-defense | CVG Tempest | CVG Typhoon |
| Launch range | 14 hexes | 24 hexes |
| Reload services | West Haven and East Haven logistics regions | Basco logistics region |

Coordinates above are zero-based authored `(Q,R)` values. The map uses its usual player-facing hex labels. Each launcher has two ballistic packages, attack modifier +1, and explicit Surface/Land targets. Air and submarine targets are excluded. Each surface escort has three interceptor packages, rating 4, radius four hexes, protecting surface formations including itself. Each carrier has one rating-2 self-defense package, representing its organic escort capability. Other formations have no ballistic equipment. Interception automatically selects one eligible provider; ratings do not stack and interception does not consume the ordinary Reaction.

The added battery participates in existing surviving-combat-capability and damage-burden tie-breaks. It does not control naval objectives, qualify as a naval escort, or earn transit/withdrawal points. The extra Red formation is a deliberate playtest balance change, not a symmetric baseline matchup.

Both sides receive a fresh High-location/General-identity carrier Contact as opening intelligence, alongside the other original reports. These are starting reports, not permanent tracking: targets can move and Contacts can age or be lost. Both carriers begin within friendly surface-escort coverage. The Red battery starts on legal land at a compatible airfield; Northern Gateway's single-hex island deliberately limits relocation. The two neighboring islands require a naval crossing and are not legal battery moves.

To try an opening launch:

1. Begin a new Solo operation as Blue and select the submarine when it is Ready. Other Ready formations may act first.
2. Open **Strike Orders > Ballistic**, select a salvo commitment, then the carrier Contact and its reported aim hex. Check owned ammunition and range in the inspector. If the report has degraded or moved, acquire a legal report before firing.
3. Resolve the normal target Reaction. One ballistic package is spent; an eligible enemy provider spends one interceptor package automatically. The attacker receives no hidden provider identity or location.
4. Hover a friendly marker to reveal its coverage and eligible protected formations. Use **Inspector > Show Defense Coverage** to pin it and **Hide Defense Coverage** to clear the pin. Green outlines indicate available defense; amber shows nominal unavailable range and a reason. Inspect friendly escort coverage and stocks on your own formations. To observe defense expenditure directly, play Hotseat and let Red fire at the Blue carrier; the defender's private receipt identifies its own selected provider.
5. Test depleted stocks, moving beyond escort coverage, and replenishment at a listed service site. A nearby fleet train provides ordinary logistics but does not itself supply ballistic/interceptor reload service outside those regions.

Land batteries can replenish at their airfield. Naval formations still need ordinary legal port/anchorage or fleet-train access inside the service region. The existing logistics-access rules apply to either side; these service regions do not introduce base ownership or capture scoring.

Automated content checks exercise launch legality and actual interception from every authored launcher, deployed escort coverage, compatible reload sites, land-only battery movement, save/load after expenditure, preserved objectives/events and legacy saved forces. Visible Solo/Hotseat acceptance and balance assessment remain open in [TODO](../TODO.md). The four dedicated expansion scenarios remain separate work.

Seeded Adaptive AI runs on 2026-10-15 completed without rejected decisions or stalls:

| Operation | Seed | Actions | Ballistic launches | Interceptions | Final time |
|---|---|---|---|---|---|
| Meridian Veil | 1978 | 79 | 6 | 5 | T16 |
| Meridian Veil | 4200 | 90 | 6 | 5 | T16 |
| Northern Gateway | 1978 | 132 | 5 | 2 | T24 |
| Northern Gateway | 4200 | 134 | 5 | 2 | T24 |

These are automated rules checks, not human balance approval. Logs: `SeaOfUncertainty/TestResults/ScenarioAuthoring.log`. Generated HTML formation/scenario aids and counters have been refreshed from the equipped catalog; full rule/PDF integration remains SCN-BM-004.
