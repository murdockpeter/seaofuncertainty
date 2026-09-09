# Production Art Direction and Approval Workflow

Status: approved baseline, 2026-09-08.

## Visual thesis

The operation is a sober maritime command table: deep blue-green water, restrained atmospheric depth, warm command amber, and compact military geometry. Readability outranks spectacle. The map feels materially credible at close zoom and diagrammatically decisive at distant zoom.

## Formation language

- Carrier groups: long angled deck, offset island, two escorts, longitudinal recognition stripe, broad paired wake, rectangular distant symbol.
- Surface groups: narrow tapered combatant hull, bridge, mast, launcher silhouette, two escorts, narrow recognition stripe and wake, slender rectangular distant symbol.
- Submarines: low hydrodynamic hull, sail, planes, rudder, and shrouded propulsor; no surface wake; compressed oval distant symbol.
- Air groups: swept wing, tail, and canopy silhouette, transverse recognition stripe, paired contrails, diamond-like distant symbol.
- Blue/red affiliation is redundant with shape, labels, formation codes, and side markings. Color is never the only discriminator.

## Contacts and state

Contacts are diamonds, never platform silhouettes until Identity permits it. Their labels expose only authorized type, Location quality, and Age. Uncertainty geometry, contradictory fixes, staleness, eligibility, active state, damage, cohesion, entropy, and Loud status each require a distinct border, shape, glyph, or text treatment.

## Environment and effects

Weather, wakes, foam, relief, haze, cloud shadows, and action effects remain subordinate to command overlays. Effects may acknowledge an authoritative event but may not imply extra range, location precision, damage, or timing. Reduced Motion freezes or suppresses nonessential animation.

## UI icon system

Action glyphs are paired with numbered text labels: arrow/Move, ring/Search, star/Strike, return/Recover, square/Hold, bullseye/Screen, plus/Support, cycle/Replenish, and double-star/Synchronized Strike. Rating and status abbreviations remain visible beside shape and color cues. Glyphs never replace labels.

## Technical budgets

The full theater remains within 100k visible triangles, 100 materials, 180 draw calls, 512 MB texture memory, and 64 simultaneous transient effects. Runtime-generated assets are deterministic and disposable without changing game state. Distant symbols replace model detail at the established command-camera threshold.

## Approval workflow

1. Record purpose, owning system, source, author, license, target platforms, information-security risk, and budget estimate.
2. Review silhouette at grayscale and simulated color-vision variants, close/distant legibility, hidden-information discipline, reduced motion, and minimum-resolution fit.
3. Verify pooling, material sharing, deterministic behavior, performance budget, and absence of rule coupling.
4. Add a file-level entry to `3D_ASSET_LEDGER.md`; reject undocumented binaries and ambiguous generated assets.
5. Record reviewer and date. Replacements retain stable logical IDs and a rollback path.

Current approval: code-owned formation models, type-specific symbols, recognition markings, persistent wakes and contrails, procedural environment, Contact/status visuals, and labeled UI glyphs are approved for the public prototype. Human art-direction review may refine them without reopening rules authority.
