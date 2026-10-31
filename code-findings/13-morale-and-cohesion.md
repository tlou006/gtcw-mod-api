# 13 — Morale and cohesion

**Build:** see [00-build-provenance](00-build-provenance.md)
**Source:** `Assembly-CSharp.dll` — `Regiment.UpdateMorale`,
`Regiment.UpdateMoraleCampaign`, `Regiment.CalculateCohesion`, `Autocalc`,
`BattleUnits`, `GamePrefs`; pref values quoted from the shipped
`Config/unitprefs.txt`
**Summary:** [summary-morale](summary-morale.md)

Second Phase 2 subsystem. Morale is the decisive mechanic in this genre, and
it is also the best-documented code in the assembly — the game maintains its
own effect breakdown with player-facing text, so the semantics come free.

> **The documentation shortcut `plan.md` predicted works here better than
> anywhere.** `Regiment` keeps `moraleeffects[18]` alongside
> `moraleeffecttext[18]`, so every contributing factor is enumerated in one
> method *with its own explanation attached*. No inference needed.

---

## 1. Two morale systems, one value

| | Battle | Campaign |
| --- | --- | --- |
| Method | `UpdateMorale()` | `UpdateMoraleCampaign(bool _inbattle, Regiment parentcampaignunit)` |
| Breakdown array | `moraleeffects[18]` + `moraleeffecttext[18]` | `moraleeffectscampaign[14]` (12 for `unittyp == 17`) |
| UI | `BattleUI` | `CampaignArmyPanel`, via `moraleeffectsaverage[]` |

Both write the same `Regiment.morale` field, in `[0, 1]`. Both skip
`unittyp > 13` and `unittyp == 5`.

Every effect is a **rate**: each is multiplied by elapsed game time and added
immediately, inline, rather than summed at the end —

```
morale += moraleeffects[i] * num [* num4];
```

where `num = GameVars.currenttimefromstart - lastmoraleupdate` and `num4` is a
DLC incident multiplier. The pref values are per game hour (the config labels
say so explicitly, e.g. *"per hour"*).

> `num4` is applied to the negative-leaning slots (2, 3, 4, 6, 7, 8, 9, 10, 12,
> 13, 14, 15, 16) but **not** to slots 1, 5 and 11 — supportive units, own
> cover and friendly cavalry nearby. So the DLC incident multipliers scale
> fear but not confidence. Whether deliberate is unclear.

## 2. Base recovery: the rally value

Before any effect is applied, morale regenerates:

```
morale += num × rallyValue × branchBonus × slowFactor
```

| Term | Source | Shipped |
| --- | --- | --- |
| `rallyValue` | `rallyvalue`, then `×= 1 - (1 - Experience()) × expinfluenceonbasemoraleincrease`, then AI bonus (`aibonus_restoringmorale`, non-player side only) and DLC effect 16 | `0.30`/hr × `[0.34, 1.0]` |
| `branchBonus` | `increaseofmoralecapforcorrectbranch` when the commander is West Point *and* his `historicalbranch` matches the unit type | `1.25`, else 1 |
| `slowFactor` | `slowmoraleimprovementfactor` **when `morale > battlestartmorale`** | `0.15`, else 1 |

`battlestartmorale` is latched on the first update as
`max(morale, abovemoraleslowimprovement)` — shipped `abovemoraleslowimprovement`
is **0.5**, so the threshold is *whatever the unit entered the battle at, but
never below 50 %*.

> **Recovery above where the unit started the battle is deliberately slowed.**
> A unit can rally back to its starting morale at full rate, then crawls above
> it at 15 %.

**The experience term is the single largest multiplier on recovery.** With
`expinfluenceonbasemoraleincrease` = 0.66, the factor is
`1 − (1 − Experience()) × 0.66`, so a green unit recovers at
`0.30 × 0.34 = 0.102`/hr and a fully experienced one at `0.30`/hr — **2.94×**.
With the matching West Point commander that becomes 0.128 and 0.375.

> So the time to climb out of a rout (morale is set to **0** by `Rout()`, §6)
> to the 0.25 rally threshold is **2 h 27 m of game time for a green brigade
> and 50 minutes for a veteran** — and in a battle whose fighting day is 9 to
> 16 game hours, that is a large fraction of the day either way.

### `rallyvalue` is a constant per unit, from a pref that exists twice

Closing §11's third open question. `rallyvalue` is a `Regiment` field assigned
**once, at unit creation** (`Regiment` ~3912):

```csharp
if (unittyp > 13)  rallyvalue = GamePrefs.rallyvalueperhour;    // commander objects
if (unittyp <= 13) rallyvalue = GamePrefs.basemoraleincrease;   // fighting units
```

and never written again outside save/load. Nothing a commander does raises it;
the commander's rally *order* is a separate additive path (§6).

> **`basemoraleincrease` is parsed from two different config files with two
> different units**, exactly like `battledaylengthinmins`
> ([10-simulation-tick](10-simulation-tick.md) §5):
> `unitprefs.txt` *"Base rally value increase per hour (without commander)"* =
> **0.3**, and `campaignprefs.txt` *"Base rally value increase per day
> campaign"* = **0.02**. `loadcampaigncontrols()` runs last in the campaign
> scene and `BattleController.Start` reloads `unitprefs` for a battle, so each
> scene gets the value meant for it — but the collision is real, and the
> campaign path multiplies by `365f` to convert its own per-day value against a
> delta measured in game years. **A modder editing "the rally value" will
> change one scene and not the other.**

### `historicalbranch` decoded

From the three branch/unittyp pairings in the bonus check:

| `historicalbranch` | Branch | Matching `unittyp` |
| --- | --- | --- |
| 0 | Infantry | 0, 3 |
| 1 | Cavalry | 1, 4 |
| 2 | Artillery | 2 |

This also explains `increaseofrofwhencorrectbranch` and
`increaseofaccuracywhencorrectbranch` from
[11-combat-resolution](11-combat-resolution.md), both of which test
`historicalbranch == 2` for artillery.

Shipped rally values: base rally without a commander `0.3`/hr, rally morale
increase per hour `0.20`, experience multiplier on rally `0.66`, rally ceiling
`0.9`.

## 3. The 18 battle morale slots

Taken from the method together with its own `moraleeffecttext`, which is the
authoritative statement of intent. The last column is the player-facing label
from `GameVars.debugmoraleeffecttexts[18]`, which the unit panel prints.

| # | Factor | Rate/hr | `GamePrefs` key | Panel label |
| --- | --- | --- | --- | --- |
| 0 | **First battle — a cap, not an addend** (§5) | cap | `firsttimeinbattlecap` 0.6, `effectofdrillonfirsttimemalus` 0.5, `effectofcommanderleadershiponfirsttimemalus` 0.5 | "First battle" |
| 1 | Friendly units protecting flanks / rear, per secured side | **+0.05** | `moraleeffectsupportiveunits × n` | "Supported" |
| 2 | Enemy **positioned** on a flank / in the rear | **−0.008** / **−0.25** | `moraleeffectenemyflanking`, `moraleeffectenemyinrear` | "Flanked" |
| 3 | Enemy **firing** from a flank / the rear | **−0.25** / **−0.50** | `moraleeffectenemyflankingfire`, `moraleeffectenemyinrearfire` | "Flanking fire" |
| 4 | Commanders nearby, within `50` | +0.025 base, +0.125×leadership, +0.075×fame | `basemoraleincreasecommandernearby`, `moralemultipliercommanderleadership_*` | "Nearby commanders" |
| 4 | …the same commander wounded / killed | −0.03/−0.015, −0.05/−0.02 | leadership / fame multipliers | — |
| 5 | Own unit behind cover | **+0.15** × cover | `moralefactorbehindcoverown` | "In cover" |
| 6 | Enemy in front behind cover | **−0.25** × cover | `moralefactorbehindcoverenemy` | "Enemy in cover" |
| 7 | Low / out of ammunition, for 0.5 h | −0.10 / −0.20 | `moraleeffectlowammo`, `moraleeffectoutofammo` | "Low on ammo" |
| 8 | Under artillery fire, **per battery** | −0.01, ÷ (distance × 0.0025) | `moraleeffectofeachincomingarifire` | "Under artillery fire" |
| 9 | Recent severe casualties (>4 % in 0.15 h); plus army-wide casualty ratio | **−0.60** | `moraleeffectonhighmomentarycasualties`, `armylosstriggerinfluenceonmorale` | "Casualties" |
| 10 | Falling back / retreating under fire | −0.075 / −0.15 | `moraleeffectfallingback`, `moraleeffectretreating` | "Falling back under fire" |
| 11 | Friendly cavalry within `75` | +0.05 | `moraleeffectfriendlycavalrynearby` | "Own cavalry close" |
| 12 | Enemy cavalry hitting the formation | **−0.50** | `moraleeffectenemycavalryhits × n` | "Enemy cavalry close" |
| 13 | Charged by a foe with better cohesion or numbers | **−1.00** × max(0, 1 − resolve) | `moraleeffectchargecohesionlower` | "Charged by enemy" |
| 14 | Own cohesion below `cohesionalertlevel` (0.3) | −0.10 | `moraleeffectcohesionbelowalertlevel` | "Low cohesion" |
| 15 | Outside the commander's influence | −0.10 | `moraleeffectofbeingcutoff` | "Cut off from commander" |
| 16 | `fatigue > fatiguealertlevel` (0.7), only while `morale > 0.3`, suppressed by perk 2 | −0.10 | `moraleeffectonhighfatigue` | "High fatigue" |
| 17 | Routed units within `60` — enemy +0.05 each, own −0.075 each, one-time, decaying at 0.3/hr | ± | `routedunitrecovery` | "Routed units" |

Four things fall out of the numbers:

- **Being outflanked is nearly free until they shoot.** An enemy *standing* on
  a flank costs −0.008/hr; the same enemy *firing* into it costs −0.25/hr, a
  **31× jump**. In the rear the ratio is only 2× (−0.25 against −0.50), so
  slot 2's flank value is an order of magnitude out of family with its three
  neighbours. Whether `−0.008` is a missing digit is not decidable from code,
  but the shape is suspicious: `−0.125` would make the flank/rear ratio 2× on
  both halves.
- **The enemy's cover hurts more than your own helps** — `−0.25` against
  `+0.15` on the same cover value, a 1.67× asymmetry.
- **The artillery term's cover discount is inverted.** Slot 8's full
  expression is

  ```csharp
  moraleeffects[8]  = moraleeffectofeachincomingarifire * batteries;
  moraleeffects[8] /= Mathf.Max(10f, distance) * moraleleveragedistancearifire;
  moraleeffects[8] -= moraleeffects[8] * moralereductionfactorofariwhenincover * (1f - covervalue);
  ```

  so closer guns demoralise harder (−0.16/hr per battery at 25 yd against
  −0.01/hr at 400), which is right. But the third line subtracts
  `0.75 × (1 − covervalue)`, and `covervalue` rises with cover — the same field
  slot 5 uses to *add* morale for being in cover. At `covervalue == 1` the
  subtraction is **zero** and the unit takes the full penalty; in the open it
  is 0.75 and the unit keeps only a quarter of it. The pref is labelled
  *"Morale effect reduction factor of incoming artillery fire when behind full
  cover"*, which is the opposite of what the line does. **A unit in the open
  is three-quarters immune to artillery morale pressure and a unit behind a
  stone wall is not** — live, reachable, and visible in the panel as the
  *"Under artillery fire"* row.
- **The two largest terms in the whole table are both about being charged**:
  slot 13 at −1.00/hr and slot 12 at −0.50/hr. Before amplification (§4) a
  disordered unit under a successful charge is losing a full morale bar in one
  game hour; after it, in 12 to 20 game minutes.

### Slot 13 — the "determined foe" resolve

The charge-doubt term reuses the melee `resolve` value computed over all melee
counterparts ([12-melee](12-melee.md) §4):

```
resolve = (1 + cohesion·weightingcohesionchargedeterminedfoe)
        / max(0.01, 1 + Σ(enemy.cohesion·enemy.strength)·weighting / Σ enemy.strength)
        × strength / max(1, Σ enemy.strength) / 3
        × (1 + GetTraining())
        × (1 + Experience())
```

Zeroed for `unittyp >= 2`, forced to 1 with no counterparts, floored at 1 by
perk 7 level ≥ 2. Shipped weighting `0.4`, morale effect `−1`/hr.

### Slot 17 — routed-nearby accumulators

`enemyroutednear` decays toward 0 and `friendlyroutednear` rises toward 0
(it is negative), both at `routedunitrecovery × num`, each clamped to its own
half-range. Perk 7 level ≥ 1 zeroes the friendly-rout penalty entirely.

Both are gated on `morale > minimumlevelforfatigueinfluenceonmorale` — the
same gate as slot 16, despite the name mentioning only fatigue.

## 4. The experience / leadership amplifier

After every slot has been applied:

```
num22 = morale - moraleAtEntry;          // net change this tick
if (num22 < 0) {
    morale += num22 × (2 - Experience())                  × overallmoraleinfluenceexp;
    morale += num22 × (2 - commanderLeadership)           × overallmoraleinfluenceleadership;
}
```

> **This fires on losses only** — gains are never amplified. And it is
> *additional* loss on top of what was already applied.

Both prefs ship at **1.0**, so collecting the terms gives the whole thing in
one expression:

```
net loss applied = net loss computed × (5 − Experience() − commanderLeadership)
```

| Unit | Multiplier on every net morale loss |
| --- | --- |
| Green brigade, poor commander | **×5.0** |
| Both at half | ×4.0 |
| Elite brigade, best commander | **×3.0** |

> **Every per-hour number in §3 understates the real loss by a factor of three
> to five.** This is the most consequential single fact in the subsystem and it
> is nowhere in the UI: `UnitPanel` sums the same slot values the simulation
> uses, prints them as *"Overall change per hour"*, and **never applies the
> amplifier** — so a panel reading −0.4/hr on a green brigade is describing a
> unit actually shedding 2.0/hr.
>
> Experience and leadership narrow the band from 5× to 3×; they never close it.
> That is the main reason veteran units feel steadier than their raw stats
> suggest, and it compounds with the 2.94× recovery advantage in §2: a veteran
> loses morale at 3/5 the rate and regains it at 2.94× the rate, for an
> effective **4.9× swing** in staying power over a green unit with the same
> morale bar.

Since every slot is a rate multiplied by elapsed game time, `num22` is
proportional to the tick length and the amplification is therefore
rate-correct — it does not change with game speed or frame rate
([10-simulation-tick](10-simulation-tick.md) §10).

`Autocalc.SufferMoraleHit` applies the identical pair of lines under the same
`moraleeffect < 0` guard, so auto-resolve and manual battles agree here. The
**campaign** morale method does not have it at all: campaign morale losses are
unamplified.

## 5. Three caps, in order

1. **`fightingspirit`** — if `>= 0.01`, a hard ceiling; also sets
   `maxmoralevalue`. `Autocalc` applies the identical cap, so it holds in both
   combat models.
2. **`[0, 1]`** clamp.
3. **First battle** — when `firstbattle`:

   ```
   cap = min(1, firsttimeinbattlecap
              + (1 - firsttimeinbattlecap) × effectofdrillonfirsttimemalus × GetTraining()
              + (1 - firsttimeinbattlecap) × effectofcommanderleadershiponfirsttimemalus × leadership)
   ```

   Shipped `firsttimeinbattlecap` = **0.6**, and drill and leadership buy back
   part of the remaining 0.4. Stored into `moraleeffects[0]` so the UI can
   explain it.

### `fightingspirit` is the campaign→battle link

It is **not** a battle quantity. Earlier revisions of this note quoted

```
regiment.fightingspirit = GameVars.nation[regiment.state].support[alliance]   // BattleUnits ~3408
```

as the rule. **It is only the seed**: that line sits behind
`initfirsttimefightingspirit`, is called from exactly one place
(`BattleUnits` ~1330) and is superseded within a frame or two by
`Regiment.CalculateFightingSpirit()`, which runs on the slow per-unit cycle.
The live rule is four steps.

**Step 1 — state support becomes a cap, through a lerp, not directly**
(`TransformStateSupportToFightingSpirit`):

```
cap = 1 − (1 − nation[state].support[alliance]) × influence  +  bountiesfightingspirit[level]
```

returning 1 for a European state (`alliance >= 2`) and clamped to ≤ 1. The
influence depends on how the unit was raised:

| `recruitingtype` | Pref | Value | Cap at 0 % support | …at 50 % | …at 100 % |
| --- | --- | --- | --- | --- | --- |
| volunteers | `influencestatesupportonfightingspirit` | **0.20** | 0.80 | 0.90 | 1.00 |
| drafts (`== 1`) | `influencestatesupportonfightingspiritdrafts` | **0.40** | **0.60** | 0.80 | 1.00 |

and the Bounties policy **subtracts** `bountiesfightingspirit[bountieslevel]`,
labelled *"Bounties 1-2, absolute reduction of fighting spirit cap of volunteer
units"* and shipped **−0.05, −0.10**. In practice only Bounties II subtracts
anything (below). The term also applies to drafted units, whatever the label
says. So the worst reachable cap is a drafted brigade from a zero-support state
under Bounties II: **0.50** — exactly the melee crack level (§6).

> **Bounties I is free.** Policy 45/145 sets `bountieslevel = 0` and 46/146
> sets 1 (the field defaults to −1), so the arrays are indexed by level from 0,
> as their "Bounties 1-2" labels say. But both consumers guard with
> `bountieslevel > 0`: `TransformStateSupportToFightingSpirit` (`Regiment`
> ~7385) and the recruit bounty in `Economy.GetUnitCost` (~2349). So under
> Bounties I neither the −0.05 cap nor the **$300 per recruit**
> (`bountiescost[0]`) ever applies; the policy grants only its volunteer bonus.
> Bounties II reads index 1 (−0.10, $600) as intended. An earlier version of
> this section gave −0.05 for level 1 and −0.10 for level 2. Registered as
> GTCW-022.

> This is much gentler than "support is the cap", and it matters: at 50 %
> support a volunteer brigade is capped at **90 %**, not 50 %. The reason is
> two `GamePrefs` values that are **parsed and never read**, both marked
> `-> inactive` in `unitprefs.txt`: `minimumsupportlevelfightingspirit` (0.20)
> and `leveragefightingspiritsupportcap` (2.0). The intended formula clearly
> had a floor and a ×2 leverage on the support term; neither is wired, and the
> config is honest about it.

**Step 2 — the formation average caps the brigade.** `CalculateFightingSpirit`
takes `GrabNextParentUnit(gameObject)` — the next formation up — and uses
*its* `CalculateCampaignFightingSpirit()`, which for a formation is the
**unweighted mean** of step 1 over all its attached brigades. The brigade's own
cap is then applied as a second `Mathf.Min`:

```
num4 = min( parentFormationMeanCap , ownStateCap )
```

> **A brigade is dragged down to its formation's average and never lifted
> above it.** Attaching a brigade recruited in a 100 %-support state to a corps
> whose brigades average 70 % caps it at the corps average. Homogeneous
> formations by recruiting state are strictly better than mixed ones, and the
> game never says so.

**Step 3 — three commander terms, as differentials against your own army.**
Not absolute values:

```
fightingspirit  = num4 × (1 + (Experience()  − sideAverageExperience)        × w[lvl, 0])
                       × (1 + (fame          − sideAverageCommanderFame)     × w[lvl, 1])
                       × (1 + (leadership    − sideAverageCommanderLeadership) × w[lvl, 2])
```

with `lvl = max(unittyp − 13, 0)` and `fame`/`leadership` multiplied by
`commanderattributesreductionifnonactive` (**0.5**) when the commander is
wounded or killed. `w` is `influenceofattributesonfightingspirit[4, 3]`:

| Level | Experience | Fame | Leadership |
| --- | --- | --- | --- |
| brigade (`unittyp ≤ 13`) | 0.33 | 0.05 | **0.20** |
| first above brigade (14) | 0.33 | 0.10 | 0.15 |
| second above (15) | 0.33 | **0.15** | 0.10 |
| third above (16) | 0.33 | **0.20** | 0.05 |
| fleets (`unittyp == 17`) | — returns `fightingspirit = 1` and exits | | |

> **Experience is weighted identically at every level, while fame rises with
> hierarchy and leadership falls.** Leadership is worth 4× fame at brigade
> level and ¼ of it at army level — so a famous general belongs at the top of
> the chain and a good leader at the bottom. This corrects the earlier reading
> of "weighted 0.33 each".
>
> And because all three are **differentials against the side's own average**,
> a veteran brigade in a veteran army collects nothing. Fighting spirit
> measures *relative* quality inside your own force, the same idiom melee uses
> ([12-melee](12-melee.md) §2).

**Step 4 — enemy soil, then the floor.** When the battle's state is one the
unit's alliance is at war with, and it is not the unit's own home state,
`fightingspiritenemysoil` is **added**:
`Interpolate(militaryexperience, −0.30, −0.10)` — so **−0.30 at national
military experience 0, easing to −0.10 at 1**. Finally clamped to
`[0.01, 1.0]`.

> **Political support in a unit's home state caps how well it can fight.**
> That remains the single most important cross-layer coupling in the assembly —
> any mod touching state support is a combat mod whether it means to be or
> not — but the magnitude is 0.60–1.00 for drafts and 0.80–1.00 for
> volunteers, plus −0.10 from Bounties II (Bounties I's −0.05 never applies)
> and −0.30 for invading.
>
> `CampaignArmyPanel` surfaces it as *"Morale is capped by the fighting
> spirit"*, and `UnitPanel` prints it under *"Maximum morale is capped by"*.

**In a standalone historical battle all of that is discarded.** Two lines later:

```csharp
if (!calledfromcampaign && SceneManagement.currentoperation != 3)
    fightingspirit = Mathf.Min(bunits.sideinformation[GetSideFromAlliance(alliance)].fightingspirit);
```

`Mathf.Min` is called with **one argument**, so it is an assignment, not a cap:
every unit on a side takes the side's scenario-file value and steps 1–4 are
thrown away. `currentoperation == 3` is a battle launched from the campaign, so
the campaign path is unaffected — but in a historical scenario the state-support
cap, the formation average, the commander differentials and the enemy-soil
penalty all have no effect. The single-argument `Mathf.Min` reads like a dropped
`fightingspirit,` first parameter; reported as an observation, not as intent.

`sideinformation[].fightingspirit` and the per-group value are read from the
battle setup files **divided by 100**, and scaled by `GameVars.usedbonusbattles`.
## 6. Rout and rally

**Two crack thresholds**, selected by melee state:

```
if ((morale < moralecracklevel      && inmelee <= 0)
 || (morale < moralecracklevelmelee && inmelee >  0))
    → Rout(...)
```

Shipped, these are **0.25** (*"Unit cracks below morale of"*) and **0.50**
(*"…while in melee"*), with `inmelee` the fraction of blocks engaged
([12-melee](12-melee.md) §1).

> **Contact doubles the break threshold.** A unit at 40 % morale is perfectly
> steady under fire and routs the instant a single one of its blocks is
> touched. `moralealertlevel`, the level at which the morale icon starts to
> blink, is also **0.5** — so the blinking icon is exactly the warning *"this
> unit will break if anything reaches it"*, which is a far more specific
> statement than it looks.

**Rout zeroes morale.** `Rout()` sets `morale = 0f` unless called with
`skipstatsupdate`, so the climb back starts from nothing, at the §2 rate. It
also early-returns on `calledfromcampaign` — **campaign units never rout**;
routing is a battle-only state. Two more behaviours in it: a dismounted
cavalry unit is **remounted first** (`MountUnits`, via a `markedforrout`
handshake that defers the rout until the horses are up), and a unit in square
(`formation == 4`) is reformed into line.

**Rally**, at the end of `UpdateMorale`:

```
if ((morale > moralecracklevel || (routoutofmelee && no enemy within minretreatdistanceoutofmelee))
    && isrouted)
    → isrouted = false; StopRegiment(); play rally bugle;
      RotateRegiment(anglebeforemelee + 180°);
      if (routoutofmelee) morale = moralecracklevel + 0.01;
```

The unit turns to face back the way it came — `anglebeforemelee` was latched
on melee entry, which is what that field is for.

> **A melee rout rallies on distance; a fire rout rallies on morale.**
> `routoutofmelee` is latched in `Rout()` when `inmelee > 0`, and that branch
> clears the rout as soon as no enemy is within
> `minretreatdistanceoutofmelee` (**40**), *whatever the morale is*, then sets
> morale to **0.26**. A unit that broke under fire has no such escape: it must
> climb from 0 past 0.25 unaided, which is **2 h 27 m of game time green and
> 50 minutes veteran** (§2). The practical consequence is counter-intuitive —
> **units broken in hand-to-hand come back quickly and units shelled out of
> position do not.**

**The commander's rally order is a separate, additive channel.**
`UpdateRallyZone` runs on the *commander* object (it returns on
`unittyp <= 13`) and adds `elapsed × rallyvalue` straight to one subordinate's
morale, with `rallyvalue` on a commander being `rallyvalueperhour` = **0.20**
— on top of that subordinate's own 0.102–0.30/hr base recovery, so roughly a
**+67 % to +200 % boost** depending on the unit's experience. Three rules:

- It is capped at `min(rallytomaxmorale, maxmoralevalue)` — shipped
  `rallytomaxmorale` = **0.9**.
- **It is cancelled the moment the target is given a move order**
  (`if (component.GetLastTransmittedPath() >= 0) rallycommand = null`).
- When the target reaches the ceiling the order **walks itself down the
  formation**: `SetRallyVariable(null)`, then
  `GetSubordinateWithLowestMorale(…, excludeskirmishers: true, …,
  onlynonmovingunits: true)` picks the next unit. So one rally order
  eventually treats a whole command, worst first, skipping skirmishers and
  anything moving.

The contribution is reported separately as `moraleincreaseeffects[3]`, which
`UnitPanel` prints as *"Commander rallying"* — **closing §11's sixth open
question**; the other three elements are the base rally value, the branch
bonus and the slow factor, printed as *"Basic rally value"*,
*"Commander attributes"* and *"Unit above start morale"* / *"Unit started
broken"*.

**`startedwithlowmorale` is a campaign-battle import rule.** Set in
`BattleUnits` ~10433 when a unit arrives with morale below `moralecracklevel`
**and `SceneManagement.currentoperation == 3`** — i.e. only for a battle
launched from the campaign, not a standalone scenario — it raises the unit to
`moralecracklevel + 0.05` and caps it there for the whole battle:

> **A brigade brought to battle under 25 % campaign morale is pinned at 30 %
> all day.** It can rally out of a rout, since 0.30 is above the 0.25
> threshold, but 0.30 is **below the 0.50 melee crack level**, so it will break
> the moment anything contacts it, every time, with no path back. The earlier
> reading — "can never recover past the break point" — was imprecise in both
> directions; this is the exact shape.

A DLC incident (2) floors morale at `moralecracklevel + 0.01`, preventing rout
outright.

The unit symbol texture tracks three bands — normal above `moralealertlevel`
(0.5), `fragmentedsymbol` between the alert and crack levels,
`brokensymbol` at or below `moralecracklevel` (0.25; skipped for `unittyp` 3
and 4). Shipped `cohesionalertlevel` = 0.3.

There is also a diagnostic: a drop to `morale <= 0` from above `0.4` in one
tick logs every one of the 18 effects — **useful as a debugging hook, and it
means the developers hit the same confusion.**

### A third threshold: 0.40 refuses the battle outright

`minimummoraleforunitstoparticipateinbattles` (**0.40**, `campaignprefs.txt`
*"Minimum morale of units to participate in battles"*) is tested against
`groupmorale` — the formation's **average** brigade morale, normalised at
`Regiment` ~11011 — at four sites:

| Site | Effect below 0.40 |
| --- | --- |
| `BattleUnits` ~2778 (`EstablishCampaignBattle`, caller) | **no battle**; a `msg_retreating` message to the player |
| `BattleUnits` ~2787 (same, defender) | the defender **`SetRetreatToDepot`** and no battle |
| `BattleUnits` ~2629 / ~2679 | individual brigades are excluded from a battle that does start |
| `Regiment` ~6473 (`CheckIfJoiningOwnUnit`) | the formation will not join a neighbour's ongoing battle |

Both battle-establishment tests are skipped for a unit with a
`garrisonreference`, and for a unit already `inbattle`.

> **Below 40 % average morale a formation will not accept a field battle.**
> That sits between the two morale caps of this section: 0.25 is where a unit
> arrives crippled (capped at 0.30 all battle), 0.40 is where it declines to
> fight at all, and 0.50 is where it breaks on contact. The band 0.25–0.40 is
> therefore *"too shaken to fight in the open but not yet capped"*, and a
> demoralised enemy in open country **cannot be brought to battle** — it
> withdraws to a depot. Garrisons are the exception.

### The other way a unit breaks: unbearable losses
Morale is not the only rout path. `Regiment.LossesAreUnbearable()` tests the
unit's cumulative loss fraction against `GetUnbearableLossesTrigger()`
(`Regiment.cs:4293`), and `Autocalc` applies the same test at two sites. This
is the stat the unit panel labels **Loss Resilience** — the word "resilience"
appears nowhere in the code or the config.

```
fragility = Interpolate(allianceMilitaryExperience,
                        unbearablelossesmoraletrigger0,    // 0.80
                        unbearablelossesmoraletrigger100)  // 0.55
          × (1 − Experience()            × (1 − influenceexpunbearablelossestrigger))        // 0.91
          × (1 − GetTraining()           × (1 − influencetrainingunbearablelossestrigger))   // 0.90
          × (1 − GetCommanderLeadership()× (1 − influenceleadershipunbearablelossestrigger)) // 0.75

trigger = min(1, (1 − fragility)
                 × unbearablelossestriggerskirmishers      // 0.5, unittyp 3 and 4 only
                 × dayFactor)
```

Every multiplier *reduces* fragility, so experience, drill and a good
commander all raise the trigger. The spread is wide:

| Unit | Trigger |
| --- | --- |
| Green, undrilled, poor commander, alliance military experience 0 | **0.20** |
| All four at half | **0.46** |
| Elite, fully drilled, best commander, military experience 1 | **0.66** |
| Skirmisher detachment, same inputs | half of the above |

So a raw regiment breaks irrecoverably at **one man in five** lost, and a
veteran one at **two in three** — a 3.3× range on the single hardest cliff in
the battle model.

`dayFactor = (Interpolate(Experience(),
unbearablelosstriggerincreaseperdayfactorexp0 = 1.1,
...exp100 = 1.3) − 1) × bunits.battlepasseddays + 1`, so **units toughen as a
multi-day battle goes on** — 10 % per elapsed day for green troops, 30 % for
veterans, added linearly. Hard-coded defaults rather than config values, in
this one case.

For a non-player alliance the trigger is also multiplied by
`1 + (aicheat_shiftedbonusmoralelosstrigger + usedcampaignbonusrunning) ×
aibonus_restoringmorale`; the cheat half is zeroed unless `ai_vs_ai`
([20-ai-campaign](20-ai-campaign.md) §9).

> **Two mismatches between the test and the bar that displays it.**
> `LossesAreUnbearable()` sums `losses` **minus `losses[3]` (prisoners)**,
> while `UnitPanel.cs:277-291` fills the bar from the full `Tools.SumUp(losses)`
> — so a unit that has lost men as prisoners reads closer to breaking than it
> is. And the bar is normalised against
> `GetUnbearableLossesTrigger(0, simulatemaxvalues: true)`, the trigger a
> *perfect* unit of that type would have, so neither fill level can be read as
> a percentage of the unit's own strength.

Two more properties of the trigger, both UI-relevant:

- **For a formation it is the maximum over its brigades.** The `unittyp > 13`
  branch loops `allattachedunits` and returns the **largest** brigade trigger,
  not the mean or the weakest. So a corps's displayed Loss Resilience is its
  toughest brigade's, and the corps will visibly come apart well before the
  bar says it should.
- **`simulatemaxvalues` does not override drill.** It substitutes 1.0 for
  national military experience, unit experience and commander leadership, but
  leaves `GetTraining()` as the unit's own. The "perfect unit" the bar is
  normalised against is therefore a perfect unit *with this unit's drill*,
  which makes the denominator move as the unit trains.

## 7. The 14 campaign morale slots

Campaign rates are **per game year**, and the config labels say so. Values are
in morale points on the 0–100 scale the UI shows, so divide by 100 for the
`[0, 1]` field.

| # | Factor | Rate/yr | Trigger | `GamePrefs` key |
| --- | --- | --- | --- | --- |
| 0 | Commander within camp radius | **+5.0** | | `moraleimprovementcommanderradiuscamp` |
| 1 | Telegraph connection | **+2.0** | | `moraleimprovementtelegraphconnection` |
| 2 | — **never assigned** | | | |
| 3 | Low on provisions | **−10.0** | below 0.25 | `moralelossoutoffood` |
| 4 | Low on ammunition | −5.0 | below 0.25 | `moralelossoutofammo` |
| 5 | Low condition — men | **−8.0** | below 0.25 | `moralelosslowconditionmen` |
| 6 | Low condition — horses | −3.0 | below 0.25 | `moralelosslowconditionhorses` |
| 7 | Low condition — guns | −2.0 | below 0.25 | `moralelosslowconditionguns` |
| 8 | High attrition | −9.0 | above 0.4 | `moralelosshighattrition` — **computed but never applied, see §9** |
| 9 | Commander lost (`status >= 2`) | **−10.0** | | `moralelosscommanderlost` |
| 10 | Bad weather (rain, snow, storms) | −2.5 | | `moralelosswithinbadweather` |
| 11 | — **never assigned** | | | |
| 12 | Unhappy with commander | −2.0 | see below | `moralelossunhappywithcommander` |
| 13 | Hospital in range | **+1.0** × treatment value | | `moralehospitalrange` |

Fleets (`unittyp == 17`) swap four of them for their own values: provisions
**−12**, bad weather **−10**, low ammo −6.0 (trigger 0.20), plus
*"+5.0 if the fleet contains at least one ironclad"* and
*"+0.1 per unit of nearby friendly fleet combat strength"* within 100.

**Recovery is a separate, much larger term.** `morale += elapsed × rallyvalue ×
num3 × num4 × 365f`, with `rallyvalue` = `basemoraleincrease` = **0.02/day**
from `campaignprefs.txt` (§2) — so **+7.3/yr** at base, against the −2 to −10
penalties above. The two multipliers are:

| | Source | Shipped |
| --- | --- | --- |
| `num3` | matching West Point commander; **× `moraleincreasemultiplierenemysoil` on enemy soil**; DLC effect 16 and camp modifier 7; `GetArmyGroupBonus(1)` | 1.25 / **0.75** |
| `num4` | encamped, or entrenched (linear in entrenchment level, floored by the camp value) | **1.25** / up to **2.00** |

> **Entrenching doubles campaign morale recovery and camping raises it by a
> quarter**, while standing on enemy soil cuts it by a quarter. Combined, a
> fully entrenched unit under a matching West Point general recovers at
> `7.3 × 1.25 × 2.0 = 18.3`/yr, and the same unit marching through enemy
> territory under a mismatched general at `7.3 × 0.75 = 5.5`/yr — a 3.3× range
> that the player controls almost entirely through stance and posture.
>
> Note what is *not* in the campaign recovery term: the **experience factor**
> that triples battle recovery (§2), and the **loss amplifier** of §4.
> Campaign morale is the simple, linear half of the system.

`moraleimprovement` is exported for display as
`rallyvalue × num3 × num4 × 365 / 52` — the same figure **per week**.

### Slot 12 — the unhappiness test

```
if (fame/2 + leadership×2 < Experience()×expleverageunhappy
                          + expleverageunhappyseniorofficers × famedSeniorOfficers
    || commander.IsDefamed())
```

**Veteran units resent mediocre commanders.** Experience is on the *demand*
side of the inequality, so the better the troops, the more fame and leadership
they require. A `unhappytooltip` string is built explaining which cause
applied — low fame/leadership, defamed, or senior officers.

### UI labels are addressed by numeric id

`CampaignArmyPanel` renders the breakdown with
`GetTooltipHeadline(panel, num3 + i)` — the label for campaign morale slot `i`
is fetched by **index offset into the panel's tooltip table**, not from a
string in code. Effects are only shown when their magnitude exceeds
`triggertoshowcampaignmoraleeffects`.

> This is supporting evidence for the long-standing question about numeric
> header cells being localization ids
> ([07-config-data-model](07-config-data-model.md)): at least in this panel,
> UI text is unambiguously addressed by number. **A mod adding a morale effect
> would need a tooltip id, not just a string** — worth confirming before the
> ModApi offers custom morale effects.

---

## 8. Cohesion

A clean, self-contained system: `CalculateCohesion()`, gated on
`GameVars.frame >= 50` (so it does not run during load) and skipping
`unittyp == 5` and `unittyp > 13`.

**Exponential approach to a target**, not an accumulator:

```
disruption = cohesionbyfire + terrainPenalty + movingPenalty

target = 1 - disruption × (1 - GetTraining() × traininginfluenceoncohesionfactor)
           - cohesioncausedbyotherunitspassing
target = min(target, 1 - terrainPenalty)        // terrain is a hard ceiling
target = clamp(target, 0, 1)

rate = cohesionrecoveryperhour × perk_irondisciplinecohesionrecovery[level]   // perk 7

cohesion += (target - cohesion) × rate × elapsed
```

Four things worth knowing:

- **Terrain only penalizes a moving unit.** `terrainPenalty =
  terraininfluenceoncohesion[currentterrain]` is zeroed in the `else` branch
  when the unit has no transmitted path. A stationary unit in rough ground
  takes no terrain disruption at all.
- **Terrain is also a ceiling**, applied twice: inside the disruption sum and
  again as `min(target, 1 - terrainPenalty)` — which training cannot buy back,
  since the second application bypasses the training factor.
- **Training reduces disruption, not recovery.** Shipped
  `traininginfluenceoncohesionfactor` = 0.4, and the config label states the
  intent plainly: *"1 means 100% drill would lead to no cohesion any more"*.
- **`movementmode == 5` sets `cohesion = 0` outright**, bypassing the approach
  entirely.

Recovery is fast: shipped `cohesionrecoveryperhour` = 2, and the config notes
*"values > 1 mean recovery takes less than 1 hour"*.

### `currentterrain` decoded, with its cohesion cost

Closing §11's second open question. `battleprefs.txt`'s *TERRAIN SPECIFICS*
block declares **14 terrain meshes** and names each one, in the order
`terraininfluenceoncohesion[]` is indexed:

| `currentterrain` | Terrain | `terraininfluenceoncohesion` | Cohesion ceiling while moving through it |
| --- | --- | --- | --- |
| 0 | Grassland | 0 | 1.00 |
| 1, 2 | *not used* | 0 | 1.00 |
| 3 | Creek | 0.45 | 0.55 |
| 4 | **River** | **1.00** | **0.00** |
| 5 | River Crossing (ford) | 0.60 | 0.40 |
| 6 | Bridge | 0.10 | 0.90 |
| 7 | Road | 0 | 1.00 |
| 8 | Fences / Walls | 0.40 | 0.60 |
| 9–13 | Pontoon 0–4 | 0.10 | 0.90 |

The ceiling column is the second, training-proof application
(`min(target, 1 − terrainPenalty)`).

> **Moving through a River mesh forces cohesion to exactly zero**, with no
> training, perk or commander able to lift it, and a unit at zero cohesion is
> below `cohesionalertlevel` (0.3) — so it is also bleeding morale (slot 14)
> and losing the charge-doubt comparison (slot 13) badly. A ford caps it at
> 0.40, which is still above the alert level; a bridge or pontoon costs almost
> nothing.
>
> Since the penalty applies **only while moving**, stopping inside the obstacle
> starts recovery immediately at 2/hr. The tactical reading is
> straightforward: **cross at a bridge, ford or pontoon, not through the water
> — and if you must take the water, halt and re-form before contact rather
> than arriving disordered.**

Note that [11-combat-resolution](11-combat-resolution.md) §4b reads terrain
values 3/4/5/8 as meaning "embarked". Those are not the same enumeration as
this one — 8 here is *Fences / Walls* — so one of the two readings is keyed on
a different field; worth reconciling before either is exposed through the
ModApi.

### The other disruption terms, with values

| Term | Pref | Shipped | Notes |
| --- | --- | --- | --- |
| Moving at all | `cohesionbymoving` | **0.15** | any transmitted path |
| Another unit passing through | `choesionbyotherunitspassing` *(sic)* | **0.25** absolute | decays at `cohesionbyotherunitsreductionperhour` = **5**/hr |
| Enemy fire | `cohesionbyfire` | **always 0** | see §9a |
| Drill offset | `traininginfluenceoncohesionfactor` | 0.40 | reduces the *sum*, not the terrain ceiling |
| Recovery | `cohesionrecoveryperhour` | **2**/hr | ×1.25 / ×1.50 / ×2.00 at perk 7 levels 1–3 |

So the worst a fully drilled unit can reach on open ground is
`1 − 0.15 × (1 − 0.40) = 0.91`, and an undrilled one `0.85` — **marching in
the open is almost free**. The expensive things are water obstacles and having
your own troops walk through you.

### Cohesion is visible

When `|cohesion - lastcohesion| >= cohesionspriteupdatetrigger`, sprite
positions are re-laid out (`SetRegimentSpritePos`), spreading up to
`cohesionmaximumspritespread` (shipped `2`), scaled by `(1 - cohesion)`.
**Disorder is literally rendered as the formation loosening.**

### A naming inversion to watch

In code, `cohesion` is **order**: 1 = perfect formation. In the config labels,
"cohesion" means **disorder** — *"Cohesion increase by enemy artillery fire"*
describes a penalty. The disruption variables are named for the damage, then
subtracted from 1.

> Reading `unitprefs.txt` with the code's sense of the word will invert every
> conclusion.

### Cohesion's consumers

| Consumer | Use |
| --- | --- |
| Morale slot 14 | Below `cohesionalertlevel` → morale penalty |
| Morale slot 13 | Compared against the enemy's, as charge doubt |
| `Autocalc.FightUnit` | `cohesioninfluenceonenemycasautocalc`, shipped **0.8** (`campaignprefs.txt` 1169): casualties dealt ×0.8 at zero cohesion. An earlier version said 0.3 |
| `CalculateMeleeCasualties` | `meleeinfluence_cohesion`, as a differential |
| `CalculateMeleeStrength` | `(1 + cohesion)` multiplier — display only |
| Sprite layout | Formation spread |

## 9. Four unwired or mis-wired features

The first two were verified by exhaustive reference search across all 211 game
types. Each is a config knob the player can set that does nothing.

### 9a. Fire does not disrupt cohesion

`unitprefs.txt` ships three values under *Cohesion Preferences*:

| Config label | Value | Field | Status |
| --- | --- | --- | --- |
| Cohesion increase by enemy infantry fire | 0.001 | `cohesioninfantryfire` | **parsed, never read** |
| Cohesion increase by enemy artillery fire | 0.01 | `cohesionartilleryfire` | **parsed, never read** |
| Cohesion increase by enemy cavalry shock | 0.15 | `cohesioncavalryshock` | **parsed, never read** |
| Cohesion due to fire: decrease per hour | −3 | `firecohesiondecreasebyhour` | used |

And `cohesionbyfire` — the accumulator those three would feed — has **no
increment path anywhere in the assembly.** Its complete reference set is:
declaration, zeroing at init, the decay `cohesionbyfire += -3 × elapsed`
(clamped at 0), the read in the target formula, and save/load.

> **So `cohesionbyfire` is permanently 0, and fire has no effect on cohesion in
> this build.** Only the decay half of the feature is wired. The config labels
> show clear design intent — infantry fire, artillery fire and cavalry shock
> were each meant to disrupt formations — and the three prefs are even parsed
> consecutively, immediately before the decay value that *is* used.
>
> Cavalry shock does still hit **morale** (slot 12) and melee; it just never
> touches cohesion.
>
> **A modder tuning these three numbers would see no effect and reasonably
> conclude they had the wrong file.** This is exactly the class of finding that
> justifies the project.

### 9b. Campaign high-attrition morale loss is computed but not applied

`moraleeffectscampaign[8]` is assigned `moralelosshighattrition` (or 0), and
then — unlike slots 0, 1, 3–7, 9, 10, 12 and 13 — there is **no
`morale += moraleeffectscampaign[8] * num;`** line. Each slot is applied
individually inline; there is no bulk loop that would pick it up.

The value still reaches `moraleeffectsaverage[]` and therefore the campaign UI,
so **the player can be shown an attrition morale penalty that is not being
applied.**

Campaign slots 2 and 11 are never assigned at all — dead slots in a
fixed-size array, probably removed effects.

### 9c. The fighting-spirit floor and leverage are not wired

`unitprefs.txt` ships two values immediately after the enemy-soil pair:

| Config label | Value | Field | Status |
| --- | --- | --- | --- |
| Minimum support value that is taken into account for fighting spirit caps (before the leverage!) **-> inactive** | 0.20 | `minimumsupportlevelfightingspirit` | **parsed, never read** |
| The leverage to the support value that caps the fighting spirit of the units **-> inactive** | 2.0 | `leveragefightingspiritsupportcap` | **parsed, never read** |

Unlike §9a and §9b, the config is **honest about these** — both labels carry
`-> inactive`, the same style of signal as the `[deactivated]` markers in
[07-config-data-model](07-config-data-model.md). Their absence is what makes
the shipped state-support cap a mild `1 − (1 − support) × 0.20` lerp rather
than the leveraged, floored curve the labels describe (§5).

### 9d. `GetTraining()` writes to the field it reads

```csharp
public float GetTraining()
{
    _ = training;
    if (DLC_WL.dlc_scenarioactive)
    {
        training += DLC_WL.EffectMultiplier(20, 0, dlcw_isundercommander);   // effect_training
        if (training > 1f) training = 1f;
        if (training < 0f) training = 0f;
    }
    return training;
}
```

`EffectMultiplier(id, 0, …)` returns `cumulativeeffectsonce[id]` — the array
name says the bonus is meant to be added **once** — and 0 unless the DLC
scenario is active *and* the unit is a player unit. But `GetTraining()` is a
getter called many times per unit per cycle: by `CalculateCohesion`, the
unbearable-losses trigger, the first-battle cap, the momentary-casualty
reduction, melee and the resolve term. Each call adds the bonus again.

> **In a DLC scenario with the training effect active, a player unit's
> `training` ratchets to its 1.0 clamp within a handful of frames** — fully
> drilled, permanently, and saved that way. `Experience()` next door does the
> same job correctly, reading into a local `num` and never touching the field.
> Reported as an observation: the asymmetry between the two methods and the
> `once` in the array name are the evidence, and the effect is base-game-safe
> because `EffectMultiplier` returns 0 outside a DLC scenario.

---

## 10. `GamePrefs` keys

**Battle morale:** `moraleeffectsupportiveunits`, `moraleeffectenemyflanking`,
`moraleeffectenemyinrear`, `moraleeffectenemyflankingfire`,
`moraleeffectenemyinrearfire`, `basemoraleincreasecommandernearby`,
`moralemultipliercommanderleadership_w`,
`moralemultipliercommanderleadership_k`, `moralefactorbehindcoverown`,
`moralefactorbehindcoverenemy`, `moraleeffectlowammo`,
`moraleeffectoutofammo`, `moraleeffectofeachincomingarifire`,
`moraleeffectonhighmomentarycasualties`, `armylosstriggerinfluenceonmorale`,
`moraleeffectfallingback`, `moraleeffectretreating`,
`moraleeffectfriendlycavalrynearby`, `moraleeffectenemycavalryhits`,
`moraleeffectchargecohesionlower`, `moraleeffectcohesionbelowalertlevel`,
`moraleeffectofbeingcutoff`, `moraleeffectonhighfatigue`,
`routedunitrecovery`, `weightingcohesionchargedeterminedfoe`.

**Thresholds and caps:** `moralecracklevel`, `moralecracklevelmelee`,
`moralealertlevel`, `firsttimeinbattlecap`, `effectofdrillonfirsttimemalus`,
`effectofcommanderleadershiponfirsttimemalus`, `abovemoraleslowimprovement`,
`slowmoraleimprovementfactor`, `minimumlevelforfatigueinfluenceonmorale`,
`minretreatdistanceoutofmelee`.

**Recovery:** `rallyvalue` (field), `expinfluenceonbasemoraleincrease`,
`increaseofmoralecapforcorrectbranch`, `aibonus_restoringmorale`,
`overallmoraleinfluenceexp`, `overallmoraleinfluenceleadership`.

**Campaign morale:** `moraleimprovementcommanderradiuscamp`,
`moraleimprovementtelegraphconnection`, `moralelossoutoffood`,
`moralelossoutofammo`, `moralelosslowconditionmen`,
`moralelosslowconditionhorses`, `moralelosslowconditionguns`,
`moralelosshighattrition`, `moralelosscommanderlost`,
`moralelosswithinbadweather`, `moralelossunhappywithcommander`,
`moralehospitalrange`, `expleverageunhappy`,
`expleverageunhappyseniorofficers`, `triggertoshowcampaignmoraleeffects`.

**Cohesion:** `cohesionrecoveryperhour`, `cohesionbymoving`,
`terraininfluenceoncohesion[]`, `traininginfluenceoncohesionfactor`,
`choesionbyotherunitspassing` *(sic — the game misspells it)*,
`cohesionbyotherunitsreductionperhour`, `cohesionmaximumspritespread`,
`cohesionspriteupdatetrigger`, `cohesionalertlevel`,
`firecohesiondecreasebyhour`, `perk_irondisciplinecohesionrecovery[]`, and the
three dead keys in §9a.

**DLC multipliers:** `dlcwl_incidentsholycommunionmoralemultiplier`,
`dlcwl_incidentsfearofdeathmoralemultiplier`,
`dlcwl_incidentsnellyblymultiplier`,
`dlcwl_incidentsourgutshisglorybaserallyvalue`.

> Note the typo `choesionbyotherunitspassing` in `GamePrefs`. Any ModApi
> binding must match the misspelling, and it is a trap for anyone
> pattern-matching on `cohesion*`.

## 11. Open questions

1. **`movementmode` enumeration.** Mode 5 zeroes cohesion outright and mode 6
   is set by `AssignChargeTarget` ([12-melee](12-melee.md) §1); the full set is
   unknown and clearly load-bearing.
2. **Fatigue** is an input to morale (slot 16), accuracy, autocalc casualties
   and melee, but its own update rule has not been traced. `unitprefs.txt`
   exposes *"Fatigue improvement if no movement and melee per hour"* and an
   experience-scaled reduction factor, so the shape is visible but the
   consumers and the movement cost are not. It is the obvious companion to
   this subsystem and arguably belongs with supply.
3. **Where `training` comes from.** `Experience()` is now traced: it is a
   `Regiment` field raised by `IncreaseExperience(inflictingUnit, casualties)`
   at `experiencegainpercasualty` = **0.0002** per enemy casualty inflicted
   (×`artilleryexpmodifier` for guns, ×`aibonus_expgainunits` for the AI),
   clamped at 1 — so **a brigade must inflict 5,000 casualties to go from
   green to elite**, which is why the 2.94× recovery and 3-to-5× loss bands of
   §2 and §4 move so slowly. `training` has no equivalent accumulator in this
   file's scope; the drill mechanic that feeds it lives with
   [15-attrition-and-condition](15-attrition-and-condition.md) and has not
   been traced.
4. **Whether 9a, 9b and 9d are bugs or deliberate removals** cannot be settled
   from code. 9c is explicitly marked `-> inactive` in the config, so that one
   is deliberate. Checking whether the three cohesion prefs changed value
   across patches would be suggestive for 9a.
5. **The two terrain readings genuinely conflict.** §8 decodes
   `terraininfluenceoncohesion[]`'s index from the 14 mesh names in
   `battleprefs.txt` — index 8 is *Fences Walls*. But
   `BattleUnits.EstablishCampaignBattle` ~2794 selects
   `terrainrestrictions_watermovementcombat` on
   `currentterrain == 3 | 4 | 8`, reading **the same field**, and 3 and 4 are
   Creek and River. Either the scene's mesh 8 is not what the config label
   says, or the water set is wrong. This needs settling before either reading
   is exposed through the ModApi, and an asset dump of the terrain meshes would
   do it.
6. **Whether `fightingspirit` can be 0 when `Autocalc` caps against it.**
   `Regiment.UpdateMorale` guards the cap with `if (fightingspirit >= 0.01f)`;
   `Autocalc.SufferMoraleHit` applies `if (morale > fightingspirit) morale =
   fightingspirit` **unguarded**, so a unit whose `CalculateFightingSpirit`
   had never run would be pinned at 0 morale and rout on the first
   auto-resolved tick. The method floors at 0.01 and runs on the slow cycle
   (~1 real second), so this is latent rather than live — but a newly formed
   unit auto-resolving immediately is the case to test.
