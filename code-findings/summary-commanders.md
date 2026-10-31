# Summary — Commanders and perks

**Covers:** [19-commanders-and-perks](19-commanders-and-perks.md), [34-perks](34-perks.md)
**Build:** see [00-build-provenance](00-build-provenance.md)

## In one paragraph

Commanders are the last cross-cutting system, and the one every earlier
subsystem kept reaching into. `GameVars.Commander` is a flat list of 1,906
researched historical officers whose five attributes feed supply, desertion,
morale, fatigue, intelligence, stance, naval combat and the AI. The attributes
are never read raw — a shared pipeline scales them by experience, halves them
when the man is out of duty, and skews them toward his superior's. **Three of
the five never change in the base game** except through military schools.
Rank is data-driven from authored promotion dates and **can only ever go up**.
And the system hiding in here is **feuds**: a subordinate whose initiative
differs sharply from his superior's, and who is the more famous of the two,
goes off and fights his own battle.

## The perk catalogue — the Phase 2 blocker is gone

**Three namespaces, not one**: 19 battle perks, 18 army-campaign perks, 11
fleet perks, each with its own tooltip file and its own id space. The id
collisions recorded in [16](16-enumerations.md) were cross-namespace and are
unambiguous at runtime everywhere but one site — see below.

The namespace is decided by a single line:

```csharp
int num = ((component.unittyp <= 13) ? 1 : 4);   // perk slots
```

> **Fighting units get one battle perk. Formations get four campaign (or
> fleet) perks.** The namespace is derivable from `unittyp` alone, which is
> exactly what a ModApi perk surface needs.

Battle perks are additionally partitioned by branch — infantry 0–7, cavalry
8–13, artillery 14–18 — and that partition is **hardcoded in C#**, in
`PerkRow`'s static arrays, not in config.

Two things worth knowing before building on it:

| | |
| --- | --- |
| **Slots unlock in order** | A perk slot earns experience only if it is slot 0 or its predecessor is already filled. Formations must commit to a first perk before the second starts progressing. |
| **Inheritance is one level, Corps only** | `HasPerkParent` early-returns unless `unittyp == 15`. An Army's perk reaches its Corps and stops; a Division never inherits from its Corps. Where both are tested, the bonuses stack multiplicatively — except for Engineers & Mechanics, which assigns (see below). |

And the tooltip lies: **`PerkRow.IsPerkUnlocked()` returns `true` and is never
called.** The in-game text promising that available perks are "limited for each
nation, depending on its policies and technologies" describes a feature that
does not exist.

## Perk magnitudes, and four data defects

Every perk array is `float[3]` indexed by the 0-based level, so "level I" is
index 0. The six campaign perks the first pass of
[19](19-commanders-and-perks.md) could not find prefs for all have real
effects, reached through `HasPerkParent` sites in `Autocalc` and `CBuilding`:
Siege Train (siege weapon strength ×1.25/1.50/2.00), Engineers & Mechanics
(construction speed, same), Sappers and Miners (siege assault, same), Partisan
Brigades (raiding casualties, same), Limelights (night combat, same) and
Bushwhackers. Full tables in §1.7.

Four of the shipped arrays are wrong in a way that changes play.

| What | Problem |
| --- | --- |
| **Balloon Corps** | `orderdelaysballooncorps` ships **1.25 / 1.10 / 1.10** against a *multiplier on the order delay*, where the adjacent `fieldtelegraphorderdelays` ships 0.9 / 0.8 / 0.7. **The perk lengthens its formation's order delays** at every level |
| **Bushwhackers** | `skirmishingbushwhackers` ships **1.25 / 1.20 / 1.10**, so the raiding-casualty multiplier **falls** as the perk levels up. Its support-drop sibling rises correctly |
| **Field Telegraph vs. Bureau of Military Information** | Both read a 1.0 / 0.75 / 0.5 array against the same spot-mark probability in `SpottedEnemy`, one with `*=` and one with `/=`. The probability is the chance **the player** is shown a last-seen mark for an enemy formation, and the perk read is that formation's **own** — so Field Telegraph conceals its holder and the intelligence perk makes its holder **twice as easy to track** |
| **Engineers & Mechanics** | The own-perk and parent-perk branches both **assign** `constructiontimer` rather than multiplying, so this is the one paired `HasPerkParent` site that does not stack |

And one genuine cross-namespace ambiguity, qualifying the claim above that
there are none: `Regiment` ~4700's raiding-target filter tests `HasPerk(6)`
with **no `unittyp == 17` guard**, so campaign perk 6 (**Foot Cavalry**)
satisfies the fleet-only Sea Raiders clause. The `IIP` copy of the same test is
guarded, and no shipped trade node is reachable by land, so it is latent.

Two perks make their commander famous, which is worth ×1.4 on his fame
everywhere: **Blockade Busters** on selection, and **Sea Raiders** on reaching
level III while blockading a trade node.

## The attribute pipeline

```
value = clamp01( raw
              × (status == 0 ? 1 : 0.5)      // reductionfactorcommanderoutofduty
              × (0.5 + 0.5 × exp)            // factorcommanderexpattr = 0.5
              × (1 + (superior − self) × 0.33)
              + dlcStaffBonus )
```

> **The experience multiplier spans [0.5, 1.0].** A green commander operates at
> half his nominal attributes and experience only ever brings him *up to* the
> number on his card, never past it. This is the single most load-bearing
> constant in the commander system.

The superior term is the parent formation's commander when the unit is within
command radius, otherwise the commander-in-chief. It has a sharp edge:

> **If the parent unit has `commander < 0`, the factor is `0f`** — and because
> it is a multiplier, leadership, initiative, administration and cunning all
> collapse to zero. A mod that builds a formation without assigning a
> commander silently guts its subordinates.

## Three attributes are effectively frozen

| Attribute | Changes? |
| --- | --- |
| `exp`, `fame`, `initiative` | Yes — service, battles, promotions, wounds |
| `leadership`, `administration`, `cunning` | **Only through military schools** |

`IncreaseAttributes` has exactly one caller, scaled by
`Nation.GetInfluenceOfExperienceLossThroughMilitarySchools()` for the
commander's **home state**.

> That closes the `militaryschools[]` question from
> [summary-politics](summary-politics.md), and makes military schools far more
> consequential than their panel suggests: they are the *only* source of
> leadership growth in the game. The parallel `printingworks` finding is in
> §4.1 — `Alliance.printingworksvalue` multiplies commander fame from battles.

It draws **one** random value and adds the same amount to all four attributes,
and it bypasses the breakdown trackers — so the gains are invisible in the
tooltip that is supposed to explain them.

## Rank only goes up

`UpdateRankToFit(unit)` compares the commander's rank to the unit's rank window
from `ranks.txt` and promotes him if he is below it. If he is *above* it, he
gains experience and loses fame and the ceiling is remembered — but
**`rank` itself is never decremented, by any code path, including the manual
one.** There is no demotion in this game.

Rank data has three traps:

1. **Rows 0–9 of `ranks.txt` are dead.** Fighting types are remapped to the
   Brigade row (13) and artillery to the Battalion row (11) *before* lookup.
   Editing `#Unit type 0` does nothing.
2. **Only `ranks0.txt` ships**, so the Union has its own table and the other
   seven alliances share the generic one. They differ in exactly one value.
3. `if ((max >= 8 && alliance == 0) & checkforpromotion) max = 7;` —
   **the Union cannot be promoted past Major General**, in code, not data.

Ranks themselves come from the data: every shipped commander has `rank = -1`,
and `UpdateCommanderRanks` plus `CheckActivation` walk the nine authored
`dateofpromotion[]` entries to raise the officer corps on its historical
schedule. **`dateofpromotion[]` is the most load-bearing field in
`commanders.txt`** — spot-checked against Irvin McDowell's real commission and
two real promotion dates.

## Feuds

The find of this pass. Every game hour, per formation:

```
p = |initiative(self) − initiative(superior)| + (fame(self) − fame(superior)) − 0.3
```

and on a hit the formation is handed to **`aibattle[2]`** — a third AI
controller, neither player nor enemy — with a random stance, for
`5 × p` hours. While feuding it may march at the nearest enemy within 250 units
with probability `initiative² × 1.0`, doubled for a **political** commander and
halved for a **volunteer**.

> **A famous, high-initiative political general is the most insubordinate thing
> on the field**: twice as likely to charge off as a West Pointer, four times
> as likely as a volunteer. The absolute difference means the *timid*
> subordinate quarrels as readily as the rash one.

## Three negative results worth having

| Claim | Reality |
| --- | --- |
| The president matters | **Cosmetic.** `currentpresident` sets a name, a title, a date line and a portrait. `LoadCampaignScenario` takes a `chosenpresident` parameter and never reads it. Elections are a campaign objective that does not change the president. |
| Engineer is a branch of arms | **No mechanical effect anywhere.** `branchtextsub[3]` promises siege and obstacle bonuses; only the avatar panel, a list filter and the DLC read branch 3. 61 shipped commanders have it. |
| Perks are gated by policies and technology | **Not implemented.** See `IsPerkUnlocked()` above. |

And `Commander.veteran` — 456 commanders carry it — has **no mechanical
consumer**: it is displayed, filtered on, and read by a DLC item requirement,
but nothing in the base simulation branches on it.

## Corrections to earlier findings

| File | Correction |
| --- | --- |
| [16-enumerations](16-enumerations.md) | Perk ids resolve into three namespaces keyed by `unittyp`, not an unresolved collision set. `Commander.historicalbranch` has **five** values (adds 3 engineer, 4 navy) and is a *different* enumeration from `Regiment.historicalbranch`. `Commander.status`: 0 active, 2 POW, 3 WIA, 4 KIA, 5 DLC; **1 is never used**. The branch bonuses require `westpoint` in `Regiment` but **not** in `Autocalc`. |
| [17-politics-and-support](17-politics-and-support.md) | Political-commander support is reason code **2** ("Commander appointments"), not 38; code 38 is DLC-only. There are **eight** alliances, not five: Union, Confederacy, Great Britain, France, Spain, Mexico, Sea Trade, Neutral States. The president does nothing. `printingworks[]` and `militaryschools[]` are decoded. |
| [summary-combat](summary-combat.md) | Two more manual-versus-auto divergences: the commander-casualty roll fires **twice** in autocalc and superior commanders are at risk **only** in autocalc; and the branch bonus needs `westpoint` only on the manual path. |

## Defects found, ranked by how visible they are

1. **Rehabilitation uses `Random(0,1) × elapsed < threshold`** where every
   sibling check uses `Random(0,1) < prob × elapsed`. Because `elapsed` is a
   tiny fraction of a year, defamed commanders are rehabilitated almost
   immediately after `commanderehabilitationmin` (0.4 yr) and
   `commanderehabilitationmax` (1.5 yr) is nearly inert.
2. **`HasPerk(8) && GetPerkLevel(9) >= 2`** — tests one perk, reads another's
   level. A fighting unit has one perk slot, so the Expert Scouts III morale
   branch is **unreachable**. One-character fix.
3. **The last commander in the list never contributes state support** — the
   round-robin commit fires one index early.
4. **KIA and WIA rolls overwrite rather than compete**, so the effective death
   rate is lower than the configured one.
5. **`flag2` in the commander-in-chief election** is declared outside the
   per-alliance loop and never reset.
6. `UnitPerk` carries stale hardcoded tooltips calling perk 2 "Sharpshooters"
   (it is Zouave) and applies them *after* the correct id-based lookup.
7. The best-of-battle fame guard compares a commander **id** against a fame
   **score**, so it never does anything.
8. `commanderehabilitationmin/max` are **misspelled in the game** —
   "ehabilitation" — joining `choesionbyotherunitspassing`.

## For the hook library

The sample-mod shortlist from [summary-politics](summary-politics.md) now has
company. All three are one-method Harmony postfixes with no balance risk:

| Target | Fix |
| --- | --- |
| `Nation.AdjustSupport` | recompute `supportinfluencelast` as `sign(amount) × sqrt(|amount|)` — restores the empty negative-support breakdown |
| `Regiment.UpdateMorale` | the perk 8/9 index slip — but it is mid-method, so this one needs a transpiler, not a postfix |
| `BattleUnits.UpdateCommanderHistories` | the rehabilitation probability — also mid-method |

The cleanest **new** seams this pass found:

- **`Commander.GetCommander{Leadership,Initiative,Administration,Cunning,Fame}`** —
  five getters that every consumer in the game funnels through. A postfix on
  these is the single highest-leverage hook in the commander system, and the
  one place a ModApi can expose "effective attribute" without reimplementing
  the pipeline.
- **`Regiment.HasPerk` / `GetPerkLevel`** — likewise the only two readers of
  the perk list, so a ModApi perk layer needs to wrap exactly two methods.
- **`GameVars.GetSuitableCommander`** — the one appointment decision point.
- **`Regiment.CalculateFeud`** — the whole insubordination system behind one
  method.

Two ModApi design constraints fall out:

1. **Perks must be keyed on *(namespace, id)***, with the namespace derived
   from `unittyp` (`<= 13` battle, `== 17` fleet, otherwise campaign).
2. **`GameVars.currenttimefromstart` is hours in a battle and years in the
   campaign.** Anything the ModApi exposes as "time" has to pick one and say
   which.

## Still open

1. **`changeofexp[7]`** is serialized both ways and **never written** — another
   unwired feature alongside `cohesionbyfire`.
2. **`bonusmalusexp`** parks promotion experience overflow and is never read
   back; the config says it decays over time and no decay exists.
3. **Army Groups** bypass the whole appointment path and were not followed —
   though the unlock is now known: `AllowsArmyGroups()` needs the
   `allowarmygroups` flag **plus project 88 or 89**
   ([22](22-policy-and-project-catalogue.md) §12.4).
4. **`campaignranking` has two producers with inverted semantics** — a packed
   6-digit key (higher is better) and a 1-based bucket ordinal (1 is best). The
   second is reachable only from the `Statistics` panel, and whether it can
   actually fire in a campaign scene needs a runtime check.
5. **`PerksShownGroup` / `PerksShownFleet` are prefab arrays**, so the actual
   campaign and fleet availability lists need an asset dump to confirm.
6. **`AvatarManagement`** (1,792 lines) contains what looks like a commander
   *editor* writing attributes directly — possibly a scenario-authoring tool.
7. ~~**`fametrigger[]` / `defamedtrigger[]`** are indexed by a battle-result
   enumeration that has not been catalogued.~~ **Resolved** —
   [minor victory, major victory, minor defeat, major defeat], see
   [21-ai-battle](21-ai-battle.md) §14.2.
