# 29 — The dead-pref audit

**Build:** see [00-build-provenance](00-build-provenance.md)
**Source:** `Assembly-CSharp.dll` → `GamePrefs` (2,224 `public static`
declarations), plus every file that reads one; `Config/battleprefs.txt`,
`Config/campaignprefs.txt`, `Config/unitprefs.txt`, `Config/ui.txt`,
`Config/ui_battle.txt`, `Config/debug.txt`
**Summary:** [summary-data-layer](summary-data-layer.md)

[07-config-data-model](07-config-data-model.md) ran a mechanical sweep of
`GamePrefs` and found "about **145** fields with no reference outside the
loader", of which eight were confirmed by hand and **the remaining ~137 were
left tagged Untested as a group**. This closes that out.

**Result:** **144 prefs are parsed from a config file and never read**, and a
further **4 are declared and never even loaded**. 90 of the 144 are
gameplay-facing rather than engine or presentation plumbing. More useful than
the count: chasing *why* a pref is dead turned up **five live defects** —
including one that makes your intelligence rating irrelevant to how often you
see an enemy formation, and one that makes winter four times less of a
deterrent to the AI than this repo has been claiming.

It also **retracts a published exception**: the `[deactivated]` tag in the
config files does not lie anywhere. The one case where it appeared to was a
pair of near-identically named prefs in two different files.

---

## 1. Method, and the exact counts

Four steps, the last two by hand:

1. Every `public static` declaration in `GamePrefs.cs` → **2,215 distinct
   field names** (2,224 declarations; a few names repeat across overloaded
   array shapes).
2. Every `GamePrefs.<name>` reference in the other **250** source files →
   2,074 names referenced. Set difference: **150 candidates** with no external
   reference. (There is no `using static` anywhere in the assembly, so an
   external consumer must be qualified.)
3. For each candidate, classify every reference *inside* `GamePrefs.cs` as the
   declaration, a loader assignment, or a read. Two candidates fall out:
   **`debugsloaded`** is an internal latch (`if (!debugsloaded)` /
   `debugsloaded = true`), and **`coverobjects`** is read to size four sibling
   arrays. Both are live. **148 remain.**
4. Split those 148 by whether a loader line assigns them at all.

| Class | Count | Meaning |
| --- | --- | --- |
| **Loaded, never read** | **144** | A labelled number in a config file that nothing consumes |
| **Declared, never loaded, never read** | **4** | Orphan fields |

For the mirror case — *read but never loaded*, where a formula consumes a field
that holds `0f` forever — [07](07-config-data-model.md) found five, of which
`fatigueinfluenceonreloading` is the only one with gameplay consequence. That
result stands; this audit did not change it.

### 1.1 The four orphans

| Field | Declared | Note |
| --- | --- | --- |
| `amountofcitylevels` | `int`, no initialiser | Already in [07](07-config-data-model.md) |
| `cloudupdatecycle` | `float`, no initialiser | Already in [07](07-config-data-model.md) |
| `debug_scenetoload` | `int`, no initialiser | Already in [07](07-config-data-model.md) |
| `biggersizeobjectivesonpapermap` | `float = 2.5f` | **New.** The only orphan carrying a value, and the only one whose name describes a visible effect — paper-map objective markers at 2.5× size |

### 1.2 Domain breakdown of the 144

| Domain | Count | Examples |
| --- | --- | --- |
| **Gameplay** | **90** | the subsidy block (§4), `forthitpoints`, `timefactorspottings`, `winteroffensiveoperationsmodifier`, the cohesion triad |
| Presentation | 44 | cloud formations, smoke, sprite facing, banners, heat-map resolutions, `ui_scale`, `wheelscrollsensitivity` |
| Scheduling | 7 | `frontlineupdaterythm`, `economyalliancedataupdates`, `toptoolbarupdate`, `importancevaluesupdates` |
| DLC | 3 | `dlcwl_incidentgrundelowprestigelevel`, `dlcwl_incidentradicalizationpolicycost`, `dlcwl_takeovercostbattles` |

The presentation and scheduling groups are mostly uninteresting — an abandoned
cloud system accounts for eight of them on its own, and nine carry an explicit
`[deactivated]` tag. The 90 gameplay entries are the subject of the rest of
this file.

---

## 2. The largest single cause: an abandoned subsidy system

Twenty-three of the dead prefs are one cut, and reading them together says what
was removed.

[22](22-policy-and-project-catalogue.md) §10 already documented ten dead
`float[4]` **policy-ladder** arrays — the effects a ladder level would have had
beyond raising one subsidy cap — and §10.1 two Act add-ons to the trade-war
cap. This audit found the other half: the **effects of the subsidy categories
themselves**, eleven more prefs in one contiguous block of `campaignprefs.txt`
(lines 2654–2681). Ten plus eleven plus two is the twenty-three.

| Pref | Config label | Shipped |
| --- | --- | --- |
| `improvedpricesdiplomacy` | Improved prices factor for full **diplomacy** subsidies at full level (1=no change) | 1.50 |
| `interventionlevelincrease` | Improvement of intervention level per year of full **diplomacy** subsidies | 0.5 |
| `improvedproductivitycompanies` | Improved productivity of industries for full **diplomacy** subsidies | 1.5 |
| `improvedproductivityfarms` | Improved productivity of farms for full **diplomacy** subsidies | 1.5 |
| `upkeeptransportsfactor` | Upkeep cost factor of transports for **transportation** subsidies at full level | 0.5 |
| `iiptransportcapacityfactor` | Increased IIP transport capacity for **transportation** subsidies | 1.5 |
| `railroadconstructionspeedfactor` | Increased railroad construction speed for **transportation** subsidies | 2.0 |
| `blockadingefficiencytradewar` | Increased blockading/blockade running efficiency for **trade war** subsidies | 2.0 |
| `supportcivilorder` | Decrease factor of support events for full **civil order** subsidies | 0.5 |
| `intelligencefactorcivilorder` | Increased intelligence gathering when **civil order** subsidies are full | 1.5 |
| `ratingmodifiers` | Rating modifiers for **funding policy** 1-3 (Union), in notches | −2, −2, −2 |

Two things give the cut away.

**The labels name subsidy categories that do not exist.** The shipped six are
*politics, economy, agriculture, industry, military, diplomacy* — the order
`basicsubsidiescost`'s own label gives, with **two trailing slots marked
`n/a`** in an eight-wide array. The labels above refer to **transportation**,
**trade war**, **civil order** and **recruitment**, none of which is one of the
six, and `ratingmodifiers` refers to a **funding** policy ladder that is not
one of the five. So the design once had roughly ten categories and shipped
with six, and the prefs describing what the four cut ones did were left in the
file with their numbers.

**The three vestigial `subsidy_*x` constants** that
[22](22-policy-and-project-catalogue.md) §10 noted line up with three of the
cut categories — transport, trade war and
civil order — which is the same conclusion from the other direction.

Only two prefs in the whole block survive, and neither does what its label
says: **`additionalrecruitssubsidies`** (*"additional volunteers at full
recruitment subsidies"*, 2) and **`timetoreachrecruitstarget`** (2) are used as
the numerator and denominator of a convergence rate for project 109's recruiting
target, never as the target itself
([28-project-effect-reachability](28-project-effect-reachability.md) §4.5).

> **For a modder this is the single largest trap in `campaignprefs.txt`.**
> Twenty-three labelled, plausibly-valued numbers across two contiguous
> stretches of the file, describing a whole economic subsystem, and **none of
> them is read.** A balance mod that tunes "civil order subsidies" or
> "transportation subsidies" is tuning nothing. The six surviving categories
> are controlled only by `basicsubsidiescost`, `subsidiesfactor` and the one
> ladder array per category that raises the cap
> ([22](22-policy-and-project-catalogue.md) §3.2a).

---

## 3. Twin prefs — the trap that has already produced one wrong reading

Four times in the 144, a dead pref sits beside a **live one with a
near-identical name, in a different config file**. Chasing a label to the
wrong field is exactly how a reader — or an earlier pass of this repo — gets
the wrong answer.

| Dead | File | Live twin | File | What the live one does |
| --- | --- | --- | --- | --- |
| `experienceinfluenceoncommanderattributes` | unitprefs | `factorcommanderexpattr` | campaignprefs | scales commander attributes by experience |
| `intelligenceinfluenceonreadiness` | campaignprefs | `influenceofintelligenceonreadiness` | campaignprefs | intelligence raises readiness |
| `draftresetcontractlength` | campaignprefs | `draftresetcontractlengthcsa` | campaignprefs | Confederate Draft Acts extend contracts |
| `timefactorspottings` | campaignprefs | `spottingfactor[]` | unitprefs | **the wrong array entirely — see §4.1** |

### 3.1 `[deactivated]` does not lie after all

[19-commanders-and-perks](19-commanders-and-perks.md) carried a note headed
*"`factorcommanderexpattr` is live, and the config says it is not"*, concluding
that it was **the only `[deactivated]` label found that lies in that
direction**. That conclusion is withdrawn, and the label is innocent.

The `[deactivated]`-tagged label is `unitprefs.txt:1583`:

> *Adjustment factor for experience to influence commander attributes (admin,
> initiative, cunning, leadership) [0-1, 1 means experience of 0 is totally
> reducing all effects to zero, 0.5 means half, 0 means exp has no influence)*
> **[deactivated]** — value **0.5**

and the loader at `GamePrefs` ~5503 reads it into
**`experienceinfluenceoncommanderattributes`**, which has exactly two
references in the assembly: that line and its declaration. **It is dead, and
the tag is correct.**

`factorcommanderexpattr` is a different field, read from
`campaignprefs.txt:427` — *"Weighting factor of commander starting attributes
due to lower exp (due to cap of mil exp in earlier scenarios) [0-1], the higher
the higher…"*, also **0.5**, with **no `[deactivated]` tag** — at `GamePrefs`
~7219, confirmed by its loader neighbours `strengthtriggertodisband` (0.05) and
`expgaincommanderservice` (0.2), which match `campaignprefs.txt:429` and `:431`
exactly.

> **The mechanic is unaffected**: `GetCommanderExperience` with
> `usedforstatsmultiplying: true` still multiplies every attribute by
> `0.5 + 0.5 × exp`, so *"a green commander operates at half his printed
> attributes"*
> ([commanders-worth-having](../mechanics-findings/commanders-worth-having.md)
> §2) remains correct. What changes is the provenance — and the fact that
> **`[deactivated]` is now a reliable signal with no known exceptions**, which
> is worth more than the one number.
>
> The two prefs ship the same value, 0.5, which is why the mix-up was easy and
> invisible.

### 3.2 A defamed commander's experience cap is not lowered

`increaseexpcapcommanderifveteran` (**0.25**) is live, read at `GameVars` ~2042,
and raises a veteran's experience ceiling above the national military
experience. Its sibling `decreaseexpcapcommanderifdefamed` (**0.2**), from the
adjacent config line *"Decrease experience cap due to national military
experience by this value if commander is defamed [0-1]"*, is **dead**.

So defaming is strictly cheaper than the config implies: it costs fame and the
things fame feeds ([19](19-commanders-and-perks.md) §3), but **not the
experience ceiling**. The asymmetry is one line of code.

---

## 4. Two wrong-pref reads the sweep exposed

A dead pref is often only half the story: something nearby is reading the
wrong field. Two cases, and both change a number this repo has published.

### 4.1 Spotted-enemy marks ignore your intelligence entirely

`campaignprefs.txt:656` is explicit about what should govern how often you get
a last-seen mark on an enemy formation:

> *Time factor for enemy spottings, based on intelligence, in game yrs; first
> value: time of average spotting with intelligence 0, second value: …* —
> **0.1** and **0.003**

The loader reads both into `timefactorspottings[0..1]` (`GamePrefs` ~7466), and
**nothing reads it.** What `SpottedEnemy.UpdateMarks` ~245 actually uses is:

```csharp
num3 = (intel * GamePrefs.spottingfactor[1] + (1f - intel) * GamePrefs.spottingfactor[0])
       * Random.Range(1f - spottingvariationfactor, 1f + spottingvariationfactor);
num2 = (GetCampaignFloatingDate() - lastmarktime) / Mathf.Max(0.001f, num3);
```

`spottingfactor` is a **different array from a different file**: a
`float[unittypes]` from `unitprefs.txt`, labelled *"Spotting Factor (used for
concealment, the lower, the more visible)"* and read correctly elsewhere as
`spottingfactor[unittype]` (`FogOfWar` ~532). Indices 0 and 1 are **Infantry
and Cavalry**, and they ship **−50** and **−60**.

So `num3` is always negative, `Mathf.Max(0.001f, num3)` clamps the denominator
to **0.001**, and `num2` — a probability — becomes `1000 ×` the game years
since the last mark. It is then capped at
`maximumcreationprobability` = **0.9** and rolled against `Random.Range(0, 1)`.

| | Intended | Shipped |
| --- | --- | --- |
| Denominator at intelligence 0 | 0.1 yr | 0.001 (clamped) |
| Denominator at intelligence 1 | 0.003 yr | 0.001 (clamped) |
| Time to reach the 0.9 ceiling at intelligence 0 | ~33 days | **~8 game hours** |
| Time to reach the 0.9 ceiling at intelligence 1 | ~1 day | **~8 game hours** |

> **Your intelligence rating has no effect on how often an enemy formation
> leaves you a mark.** Nor do the two perks built to shift it:
> `informationdelay` (Field Telegraph) multiplies `num2` by 1.0/0.75/0.5 and
> `informationdelaybureauofmilitaryinformation` divides it by the same values
> ([19](19-commanders-and-perks.md) §1.7), and at 1000× the intended magnitude
> `num2` is over the ceiling after eight game hours whichever way they push it.
>
> What is left governing mark creation is **distance**:
> `minimumdistanceforspotmark` = **40**, which is live. Intelligence still
> decides the mark's *content* — `AddMark(unitid, intelligenceOnPos)`, and
> showing the unit type needs intelligence ≥ 0.6.
>
> The fix is one identifier: read `timefactorspottings[1]` and `[0]`. Note that
> the clamp is doing real work and hiding the error, which is why no earlier
> pass caught it from the consumer side.

### 4.2 Winter does not suppress AI offensives by ×0.05

`winteroffensiveoperationsmodifier` is loaded (`GamePrefs` ~8042) and **read
nowhere**. `winterdefensiveoperationsmodifier` has two consumers, and one of
them is in the **offensive** path:

| Site | Method | Reads |
| --- | --- | --- |
| `AICampaign` ~4306 | the defensive-operation probability | `winterdefensiveoperationsmodifier` — correct |
| `AICampaign` ~4965 | **`CheckOffensiveMovements`** (~4851), into `num5`, applied to the aggression roll at ~4992 | `winterdefensiveoperationsmodifier` — **wrong pref** |

Both sites use the same month test
(`monthCounter <= winterquartersmonthuntil || >= winterquartersmonthfrom`).

> **In winter the AI's offensive-operation probability is multiplied by 0.2,
> not 0.05** — four times more aggressive than
> [20-ai-campaign](20-ai-campaign.md) §(aggression) and
> [fighting-the-ai](../mechanics-findings/fighting-the-ai.md) §6 have been
> saying. Those two files are corrected; the dedicated 0.05 constant has never
> been read.

---

## 5. The publisher tags 40 of them, and every tag is honest

A keyword search across `battleprefs.txt`, `campaignprefs.txt` and
`unitprefs.txt` returns **40 lines** carrying `deactivated` or `inactive`; at
least one is a false positive (a label about greyed-out *icons*), so call it
roughly **38 marked labels**. Every one checked in this audit and in earlier
passes describes a pref that really is dead — including the
`factorcommanderexpattr` case that looked like an exception (§3.1).

The markers are not uniform in spelling, which matters for anyone grepping:
`[deactivated]`, `(deactivated)`, `-> deactivated` and `-> inactive` all occur.
A sweep looking only for the bracketed form misses eleven of the forty.

Two examples from the `-> inactive` family, both `unitprefs.txt`, both relevant
to a published mechanic:

| Pref | Label | Value |
| --- | --- | --- |
| `minimumsupportlevelfightingspirit` | Minimum support value that is taken into account for fighting spirit caps (before the leverage!) **-> inactive** | 0.20 |
| `leveragefightingspiritsupportcap` | The leverage to the support value that caps the fighting spirit of the units **-> inactive** | 2.0 |

These were a richer, two-stage version of the morale ceiling. What ships is the
simpler one — `influencestatesupportonfightingspirit` **0.20** for volunteers
and `…drafts` **0.40**, `campaignprefs.txt:506` — which
[why-units-break](../mechanics-findings/why-units-break.md) §5 already
documents. No correction needed; the dead pair simply explains why the cap has
no floor and no exponent.

> **So the tag is a usable signal, but only in one direction**: tagged means
> dead, while the roughly **110 dead prefs that carry no marker at all** are
> where the real traps are. Nothing in the config distinguishes those from a
> live number, which is what makes the list in this file worth keeping.

---

## 6. Other individually notable dead prefs

Of the 90 gameplay entries, roughly 25 were already documented by earlier
passes — the ten subsidy-ladder arrays and two Act add-ons
([22](22-policy-and-project-catalogue.md) §10, §10.1), the cohesion triad
([13](13-morale-and-cohesion.md) §9a), `maxsupplyratioretreat`
([14](14-supply-and-logistics.md) §3a), `draftresetcontractlength`
([17](17-politics-and-support.md) §5b), `maxgamespeed`
([25](25-newspaper-and-events.md) §2.4), `necessaryrelationsforimport`
([28](28-project-effect-reachability.md) §10) and the eight
[07](07-config-data-model.md) confirmed by hand. The rest are confirmed dead
here for the first time. These are the ones worth naming:

| Pref | Config label | Shipped | What it would have done |
| --- | --- | --- | --- |
| `forthitpoints` | Hit points per for level (0-5) | 0, 500, 1000, 1300, 2500, **3000** | Given forts durability that scales with level — see below |
| `timetowithdrawgroupsifallunitsrouted` | The time a commander may take to rally his subordinates before group withdraws from battle | **0.75** game hr | A 45-minute rally window before a wholly routed formation leaves the field |
| `influencenationalmoraleonoffensives` | The influence of national morale on offensive decisions, the higher the value the more troops will … | **1.0** | Made the AI's willingness to attack depend on its national morale |
| `interventionlevelincrease` | Improvement of intervention level per year of full diplomacy subsidies | **0.5** | Made the diplomacy subsidy buy European intervention directly |
| `minimumunitsforhierarchylevel` | Commander may command this amount of subordinate units for each 0.2 exp step **[deactivated]** | 100, 100 | Capped span of command by commander experience |
| `weaponstandardizationexp` / `…lin` | Weapon standardization steepness 1 / 2 **-> deactivated** | 5.0 / 8.0 | Shaped how fast a new weapon reaches full productivity |
| `influencesupplyspeedtransportshipsloco` | Influence of supply and reinforcement speed if factor of transport ships and locomotives are 0 **[deactivated]** | 0.9 | A floor on supply speed when transport capacity is exhausted |
| `historicalbattlefieldrange` | Use historical battlefields only up to this range **[deactivated, use battlemarkers in inspector directly]** | 150 | Chosen historical maps by proximity rather than by marker |
| `powerbalanceinfominorvictory` | Power balance info about minor victory, vp relation above/equal | **0.7** | Three of the five tiers of the battle power-balance readout — see §6.2 |
| `powerbalanceinfomajorvictory` | …about major victory, vp relation above/equal | **0.8** | " |
| `powerbalanceinfominordefeat` | …about minor defeat, vp relation below/equal | **0.3** | " |

### 6.1 A fort's level does not make it harder to take

`forthitpoints` is a six-entry per-level table rising from 0 to 3,000, and
nothing reads it. What fort damage actually touches is
`CBuilding.Damage(float)` → `Condition -= byvalue`, the **generic 0-to-1
building condition every building type shares**, and the two callers carry no
level term at all:

- `Autocalc` ~3166 — siege assault:
  `Damage(Random(fortdamageattackersize[0..1]) × timediff × attacker strength)`,
  with the pair shipping **0.8** and **1.7**;
- `Regiment` ~4636 — raiding: `Damage(num2 × raiddamage)`, `raiddamage` =
  **0.075**.

`GamePrefs.fortcapacity` *is* live and does scale with level — 5,000 men at
level 0 up to **25,000** at level 5 — as does `setuptimesiege` (0.002 to
0.015 game years).

> **Upgrading a fort buys garrison capacity and a longer siege setup, not
> durability.** A level-5 fort and a level-1 fort absorb the same absolute
> damage before their condition reaches zero, because the per-level hit-point
> table the config ships is not read. That is the one finding in this section
> a player can act on: build forts for the garrison they hold and the delay
> they impose, not because a bigger one survives longer.

### 6.2 The power-balance readout has thresholds for two tiers of five

`battleprefs.txt:889–898` ships a five-tier band for the in-battle
power-balance indicator, and **three of the five prefs are dead**:

| Pref | Label | Shipped | Status |
| --- | --- | --- | --- |
| `powerbalanceinfominorvictory` | vp relation above/equal | 0.7 | **dead** |
| `powerbalanceinfomajorvictory` | vp relation above/equal | 0.8 | **dead** |
| `powerbalanceinfominordefeat` | vp relation below/equal | 0.3 | **dead** |
| `powerbalanceinfomajordefeat` | vp relation below/equal | **0.2** | live — `BattleUnits` ~5753 |
| `powerbalanceinfocriticalloss` | loss ratio above | **0.4** | live — `BattleUnits` ~6196 |

The two survivors are not used as a tier ladder either. `…majordefeat` gates
one branch of `victorytypeforplayer`: with `endbattle == 7` — the player's side
withdrawing or being annihilated ([25](25-newspaper-and-events.md) §4.1) — a VP
relation above 0.2 records the result as type **2** rather than worse.
`…criticalloss` adds a single sentence to the Major Defeat tooltip
(*"Surrendering, to prevent further bloodshed, could be lesser of two evils…"*)
once the player has lost 40 % or more.

The actual victory tiering comes from `GetBattleResultType`'s victory-point
comparison and the `losstriggersown` / `losstriggersenemy` casualty test
([25](25-newspaper-and-events.md) §4.2), not from these.

---

## 7. Corrections to earlier files

Three published readings change:

1. **[20-ai-campaign](20-ai-campaign.md)** — the winter modifier on AI
   offensive operations is `winterdefensiveoperationsmodifier` = **0.2**, not
   `winteroffensiveoperationsmodifier` = 0.05. The latter has no consumer
   anywhere (§4.2).
2. **[19-commanders-and-perks](19-commanders-and-perks.md)** — the
   *"`factorcommanderexpattr` is live, and the config says it is not"* note is
   withdrawn. The `[deactivated]` label belongs to a different field in a
   different file, which really is dead; `factorcommanderexpattr` has its own
   untagged label in `campaignprefs.txt`. The mechanic is unchanged (§3.1).
3. **[07-config-data-model](07-config-data-model.md)** — "about 145 fields,
   ~137 of them Untested" becomes **144 loaded-but-never-read and 4 orphans,
   all confirmed**, with `biggersizeobjectivesonpapermap` added to the orphan
   list (§1).

And one claim this audit newly supports rather than corrects: `SpottedEnemy`'s
intelligence term was never priced in
[20](20-ai-campaign.md) §4 or [19](19-commanders-and-perks.md) §1.7 beyond the
perk multipliers; §4.1 now shows why pricing it was impossible — it is not read.

---

## 8. Defect list

1. **`SpottedEnemy.UpdateMarks` reads `spottingfactor[0..1]`** — the
   per-unit-type concealment array from `unitprefs.txt`, shipped −50 and −60 —
   where it wants
   `timefactorspottings[0..1]` from `campaignprefs.txt`. The product is
   negative, `Mathf.Max(0.001f, …)` clamps it, and the mark-creation probability
   runs about 1,000× intended, so **intelligence and both intelligence perks
   have no effect on how often an enemy formation leaves a mark** (§4.1).
2. **`CheckOffensiveMovements` reads `winterdefensiveoperationsmodifier`**,
   leaving the dedicated offensive constant unread and winter four times
   weaker a deterrent than the config implies (§4.2).
3. **`forthitpoints` is unread**, so fort level buys garrison capacity and
   siege setup time but **not durability** (§6.1).
4. **`decreaseexpcapcommanderifdefamed` is unread** while its veteran sibling
   is live, so defaming never lowers a commander's experience ceiling (§3.2).
5. **Twenty-three prefs describe a subsidy system with four categories the
   game does not have** (§2).
6. **Three of the five `powerbalanceinfo*` tiers are unread** (§6.2).
7. **`biggersizeobjectivesonpapermap = 2.5f` is declared and never used**, the
   only orphan with a value (§1.1).
8. **Four pairs of near-identically named prefs sit in different config files**,
   one dead and one live in each pair, with identical shipped values in the
   worst case (§3).

---

## 9. Mod seams

1. **Treat this file's list as the authority on which `GamePrefs` labels are
   real.** 144 of them are not, and about 110 of those carry no marker.
2. **Four of the eight defects are one-identifier fixes**: swap
   `spottingfactor` for `timefactorspottings` in `SpottedEnemy`, swap
   `winterdefensiveoperationsmodifier` for `winteroffensiveoperationsmodifier`
   in `CheckOffensiveMovements`, read `forthitpoints[level]` in
   `CBuilding.Damage`'s callers, and subtract
   `decreaseexpcapcommanderifdefamed` beside the veteran term.
3. **Reviving the four cut subsidy categories is not a one-line job** —
   `basicsubsidiescost` has two spare slots marked `n/a` and the effect prefs
   all exist, but the UI, the ladder arrays and `GetSubsidyCost` would all need
   extending. The ten effect prefs in §2 are a complete specification of what
   those categories were meant to do, which is the useful part.
4. **A pref's label is not a safe key for finding its field.** Four names
   collide across files (§3); a tool that maps labels to fields must walk the
   loader in order, not pattern-match names.
5. **The sweep is reproducible.** Declarations in `GamePrefs.cs` minus
   qualified `GamePrefs.<name>` references in the other 250 files, then
   classify each candidate's internal references as declaration, loader
   assignment or read. Two false positives to expect — an internal latch and
   an array-sizing read.

---

## 10. Open questions

- **A label-to-field map for all 2,215 prefs.** Building one needs the loaders
  simulated in order — every `ReadLine()`, including those inside `for` bounds
  that consume *n* lines — rather than matched line by line. It would make
  every future pref question a lookup, and it is the natural next tool.
- **The 44 presentation and 7 scheduling dead prefs** were classified by name
  and spot-checked, not read individually. Nothing in them is likely to affect
  play, but the cloud block in particular (8 prefs) looks like a whole
  abandoned weather renderer and might be worth one pass if the `UniStorm`
  layer is ever documented.
- **Whether `timefactorspottings`' intended values are balanced.** 0.1 game
  years at intelligence 0 is **33 days** between marks, which is a very long
  time; fixing §4.1 without retuning might make the campaign map far darker
  than the developers last tested.
- **Three DLC prefs** (`dlcwl_incidentgrundelowprestigelevel`,
  `dlcwl_incidentradicalizationpolicycost`, `dlcwl_takeovercostbattles`) are
  dead outright: the sweep covers `DLC_WL.cs`, and all three have exactly two
  references — the declaration and the loader line in
  `GamePrefs.loaddlcwlconfig`. They are loaded from `dlcwl_config.dat`, which
  is absent from this install, so they hold `0f` as well as being unread. What
  the DLC *would* have done with them is a question for the `DLC_WL` pass.
