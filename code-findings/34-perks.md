# 34 — Perks: a fresh audit of all 48, manual against code

**Build:** see [00-build-provenance](00-build-provenance.md)
**Source:** `Assembly-CSharp.dll` — `Regiment`, `Autocalc`, `BattleUnits`,
`FogOfWar`, `SpottedEnemy`, `IIP`, `CBuilding`, `Vessel`, `Projectile`,
`ObstacleLine`, `Pontoons`, `PerkRow`, `PanelHandler`, `AIBattle`,
`AICampaign`, `GamePrefs`; `Config/unitprefs.txt`, `Config/campaignprefs.txt`,
`Config/Perk*Tooltips*.txt`, the seven `Battlefields/*/*/unitprefs.txt`
copies; `game-manual.md` Appendix C
**Summary:** [summary-commanders](summary-commanders.md)

---

Every perk in the manual's Appendix C, traced from scratch through every
`HasPerk` / `GetPerkLevel` / `HasPerkParent` / `IncreasePerkExp` call site
(about 330 calls in about 60 methods). This was done as an independent pass,
not as an edit of [19](19-commanders-and-perks.md) §1, and §9 lists where it
agrees and where it doesn't. The short version: **of 48 perks, 19 do what the
manual says, 9 work but differently, and 20 have a headline effect that is
dead, inverted or lands on the wrong unit** (§8).

**How to read a perk entry.** *Manual* is Appendix C, levels I / II / III.
*Code* is what the source does, with shipped values I / II / III and a
roughly-here line reference. *Levels up* is the experience rule. Verdicts:

| Mark | Meaning |
| --- | --- |
| ✅ | Works as the manual says |
| ⚠️ | Works, but not as the manual says: wrong scope, magnitude or condition |
| ❌ | Headline effect is dead, inverted, or lands on another unit |
| ➕ | An effect the manual does not mention |

**Levels are 0-based in code.** `GetPerkLevel` returns 0 for level I, so a
gate written `>= 1` opens at level II and `>= 2` at level III. It also
returns 0 for a perk the unit does not hold, which matters in §3.8.

---

## 1. Where the numbers come from

Every magnitude below was read by **simulating the loader**, not by trusting
the label above it. `GamePrefs` parses positionally (`ReadLine` for the label,
`Parse(ReadLine())` for the value), so a scratch script walked the two loader
segments statement by statement, expanding the `for` loops over each
`new float[3]`, and printed which file line each variable lands on:

| Segment | Loader | File lines | Result |
| --- | --- | --- | --- |
| Battle perks | `GamePrefs` ~5593–5836 | `unitprefs.txt` 1676–1918 | every label aligned |
| Campaign and fleet perks | `GamePrefs` ~8790–9116 | `campaignprefs.txt` 2315–2640 | every label aligned |
| Auto-resolve perk prefs | `GamePrefs` ~7625–7864 | `campaignprefs.txt` 1161–1376 | aligned |

So **no perk value is misread through a label/position slip** — the defects
below are all in the code that consumes them. One perk pref is never read at
all: `perk_sharpshooters_rangefrom` (§3.2).

### 1.1 Seven battlefields ship older battle-perk numbers

`GamePrefs.loadunitprefs(folder)` takes `{folder}/unitprefs.txt` if it exists.
`BattleController` passes `Battlefields/{level}/{sublevel}` for standalone
battles and `Campaigns/{scenario}` for campaign battles (no campaign ships the
file). Seven battlefields do: **000/C, 009/A, 014/A, 020/A, 021/A, 022/A,
027/A**. They declare 18 unit types instead of 19 (no Army Group), which shifts
the perk block from line 1676 to 1632 but keeps it aligned. Their perk values
differ from `Config/`:

| Pref | `Config/` | 000/C and five others | 020/A |
| --- | --- | --- | --- |
| `perkexplvl0mp[2]` (artillery slot unlock) | 0.4 | 0.5 | 0.4 |
| `perk_engineers_engineeringpoints` | **2 / 3 / 4** | **1.25 / 1.5 / 2** | 1.25 / 1.5 / 2 |
| `perk_closesupportperkexp` | 0.0025 | 0.015 | 0.015 |
| `perk_skilledlayingperkexp` | 0.005 | 0.015 | 0.005 |
| `perk_grandbatteryperkexp` | 0.005 | 0.015 | 0.005 |
| `perk_sappergunnersperkexp` | 0.005 | 0.01 | 0.005 |

> **The manual was written against the older numbers.** Its Engineering
> "+25 % / +50 % / doubled" is exactly 1.25 / 1.5 / 2; the in-game tooltip's
> "x2 / x3 / x4" matches `Config/`. A standalone battle on one of those seven
> fields plays the manual's Engineering and levels artillery perks two to
> three times faster; the same field fought from a campaign does not.

---

## 2. Mechanics every perk shares

### 2.1 One list, three meanings

`Regiment.perks` is a `List<Perk>` of `(perk, perklevel, perkexp)` with
`perk = -1` meaning empty. `create_regiment` gives fighting units
(`unittyp <= 13`) **1 slot** and everything above **4 slots**
(`BattleUnits` ~11421). The id means a battle perk on a fighting unit, an army
perk on a formation, and a fleet perk on a fleet (`unittyp == 17`). Nothing in
`HasPerk` knows which; each call site is supposed to guard itself. §7 lists the
sites that don't.

**Corps inherit from their parent and nothing else does.** `HasPerkParent` and
`GetPerkLevelParent` (`Regiment` ~21572, ~21624) return the parent's perk only
when `calledfromcampaign && unittyp == 15`. Most campaign sites test
`HasPerk(n)` and then `HasPerkParent(n)` and multiply both, so a Corps holding
a perk under an Army holding it too gets it squared. The inherited value is the
**full** factor — `perkexpincreasegroup` (0.25), whose label says it is "also
used for perk effect on subordinates", is used only for experience.

### 2.2 Experience and levels

`IncreasePerkExp(amount, id)` (`Regiment` ~13363) walks the slots:

- An **assigned** slot gains only if `id` matches it, or `id == -2` (all).
- An **empty** slot gains from *every* call, whatever `id` is, but only if it
  is slot 0 or the slot before it is assigned. It caps at 1.0, which is what
  lets `ChoosePerk` fill it.
- Empty-slot gains are scaled. In battle: `perkexplvl0mp` = **0.15 infantry
  (and skirmishers), 0.25 cavalry (and scouts), 0.4 artillery** (and anything
  `unittyp >= 5`). On the campaign map: `perkexplvl0mpcamp` = **1.0 for
  everything that isn't a fleet, 2.0 for fleets**.
- Artillery (`unittyp == 2`) multiplies **all** perk experience by 2
  (`perkexpincreasemultiplierartillery`).
- Past 1.0 an assigned slot levels up and keeps the remainder; at level III it
  caps at 1.0.
- A Corps on the campaign map also passes `amount × 0.25` to its parent.

Two blanket grants bypass the per-perk rules entirely:

- **Auto-resolve.** `Autocalc.FightUnit` (~3121) gives both units in every
  pairing `aiperkexpincreaseautocalc × timediff` = **30 × timediff** to all
  slots (`id = -2`). For line fighting `timediff` is elapsed campaign years ×
  `landbattleintensity` (0.7) × 10, so roughly 0.58 experience per day per
  pairing (derived, not measured). That is more than half a level a day of
  auto-resolved fighting **whatever perk the brigade holds** — far faster than
  any perk's own rule in §3.
- **US Regulars** (`Policies` ~1011) start with `usregularsinitialperkvalue`
  = 1 in all slots, i.e. a perk ready to pick on day one.

**Detaching** a skirmisher or scout company (`unittyp` 3 or 4) copies the
parent's perk, level and experience (`BattleUnits` ~11905). **Reattaching**
merges experience as a strength-weighted average and keeps the parent's level
(`BattleUnits` ~12042), so experience earned by a small detachment is diluted
by its share of the men, and a level it gained while detached is lost.

### 2.3 Choosing a perk

`ChoosePerk(id)` (`Regiment` ~13462) fills the first empty slot that has
reached 1.0, and refuses an id the unit already holds. Side effects: battle
perk 0 sets `hasengineers`; **fleet perk 8 calls `SetFamous()` on the fleet's
commander** (§5.9).

| Who | How | Notes |
| --- | --- | --- |
| Player, battle units | `PerkRow` slot buttons offer `PerksShownInfantrySingle {0–7}`, `…CavalrySingle {8–13}`, `…ArtillerySingle {14–18}` (hardcoded, `PerkRow` ~9–13) | Matches the manual's Infantry / Cavalry / Artillery tables |
| Player, formations and fleets | `PerkRow` rows; `PanelHandler` defines **three** row panels for each of army, multi-panel and fleet (`cpanel_perkrowslots*`, ~461/495/533) | The 4th data slot is never shown. How many rows the prefab actually enables (the manual says 3 for armies, 2 for corps and fleets) is an asset question |
| AI, battle (`AIBattle` ~2391) | A random perk from the unit's branch list, only while it holds none | Correct namespace |
| AI, campaign (`AICampaign` ~2856) | `Random.Range(0, count − 1)` on the **int** overload, whose upper bound is exclusive, for every formation and fleet every pass | **Never picks the last id: AI armies never get River Expedition (17), AI fleets never get Port Defender (10).** No branch filter, and nothing stops it filling the **4th slot** the player can't reach |

> `PanelHandler` ~3751–3765 still wires three legacy buttons by name:
> `156_Engineers → ChoosePerk(0)`, **`157_Zouaves → ChoosePerk(1)`,
> `158_Sharpshooters → ChoosePerk(2)`**. In this build 1 is Sharpshooters and
> 2 is Zouave. That is an older id order, and it's the same order the
> auto-resolve code still uses (§3.2, §3.3).

---

## 3. Battle perks (19)

All of these are read in the battle scene only, except where §6 says
auto-resolve.

### 3.1 Engineering — battle 0, infantry

**Manual:** pontoons allowed; engineering points +25 % / +50 % / doubled.
**Code:**
- Holding it sets `hasengineers` (`Regiment` ~4114). **Pontoons require it**
  (`BattleUI` ~4707). Obstacles test `hasengineers2` instead, which defaults
  to `true` and is never cleared, so any unit can build obstacles. ✅
- Deployment engineering budget: every infantry unit adds
  `availableconstructiontimeperengineerunit` = 0.5 h; a holder adds 0.5 ×
  **2 / 3 / 4** (`BattleUnits` ~9190). In auto-resolve only holders count, at
  the same rate (`Autocalc` ~4545).
- Obstacle building speed ×**2 / 3 / 4** (`ObstacleLine` ~189). The
  multiplier is set inside the loop but initialised outside it, so once a
  holder is processed, every later non-holder on the same line builds at the
  holder's speed.
- Pontoon building speed ×**1 / 1.25 / 2** (`Pontoons` ~325) — no gain at
  level I, and the same leak across the two landing zones.

**Levels up:** 1.0 per hour of obstacle work and 1.5 per hour of pontoon work
on the working unit, plus a deployment-phase lump through
`IncreasePerksAllUnits` that is split between infantry holding perk 0 (or,
with none, between perkless infantry). ✅
**Verdict:** ⚠️ — double to quadruple per unit rather than the manual's
+25 % / +50 % / ×2 (§1.1).

### 3.2 Sharpshooters — battle 1, infantry

**Manual:** accuracy +10 / +25 / +50 %; range +25 % at III; levels at long range.
**Code:**
- Accuracy ×**1.1 / 1.25 / 1.5**, but only for `unittyp == 0`
  (`Regiment.FireBullet` ~9640). A detached skirmisher company keeps the perk
  and loses the bonus.
- Range ×**1 / 1 / 1.25** for any holder (`GetFireRange` ~8886). ✅
- Auto-resolve reads id 1 as Zouave (§6).

**Levels up:** 0.001 per casualty inflicted, on a 50 % roll, **at any range**
(`IncreaseCasualtyPerks` ~16660). The range gate `perk_sharpshooters_rangefrom`
(0.75, "experience increased at range from …") is loaded and **never read**.
**Verdict:** ⚠️

### 3.3 Zouave — battle 2, infantry

**Manual:** allows running; fatigue −25 % / −50 % at II / III; levels fighting prone.
**Code:**
- Running from level I (`CheckAbilityToRun` ~4205; also re-set in
  `IncreasePerkExp` ~13444). ✅
- Fatigue gain ×**1 / 0.75 / 0.5** (`UpdateFatigue` ~21541). ✅
- ➕ **Immune at every level to the "men are exhausted" morale term** —
  `UpdateMorale` skips slot 16 entirely for a holder (~18375).

**Levels up:** 0.001 per casualty inflicted while lying down. ✅
**Verdict:** ✅ in battle. ❌ in auto-resolve, where all three of its effects
key on id 1 (§6).

### 3.4 Expert skirmishers — battle 3, infantry

**Manual:** concealment +10 / +25 / +50 %; skirmisher reload +5 / +10 % at II / III.
**Code:**
- Concealment: subtracts **10 / 25 / 50** from the fog cell's spotting value
  in `FogOfWar.UnitIsHidden` (~536). Below zero the unit is hidden, so this is
  the right direction. These are points, not percent.
- Reload time ÷**1.0 / 1.05 / 1.1** (`UpdateFiringAndReloading` ~10020) on all
  of the unit's fire, not just in skirmish order. ⚠️
- ➕ Level III: exempt from the "formation is disrupted" morale term when
  cohesion is below 0.3 (`UpdateMorale` ~18352).

**Levels up:** 0.001 per casualty inflicted by infantry or skirmishers, 50 %
roll, in any formation; a detached skirmisher company also credits its parent.
**Verdict:** ✅ (plus ➕). §7 lists two other namespaces that also get this
concealment.

### 3.5 Spirited charge — battle 4, infantry

**Manual:** melee strength +10 / +20 / +50 %.
**Code:**
- `meleestrength` ×1.1 / 1.2 / 1.5 (`CalculateMeleeStrength` ~16128). **Only
  the unit panel reads `meleestrength`** (`BattleUI` ~1845); melee casualties
  never use it ([12](12-melee.md) §3). ❌
- ➕ Level III, the only effect that changes a fight: in a melee, an opponent
  that doesn't hold Spirited charge at II+ counts this unit's men at +1.0
  cohesion in its "doubt their chances" comparison, deepening that opponent's
  morale term 13 (`UpdateMorale` ~18311).

**Levels up:** 0.003 per melee casualty inflicted, 50 % roll — and the
unit-type test is on the **victim**, so only melee against line infantry
counts (`SufferCasualties` ~16498).
**Verdict:** ❌ — levels I and II do nothing in combat.

### 3.6 Deadly volley — battle 5, infantry

**Manual:** +50 % resistance to high momentary casualties at every level;
volley strength +5 / +10 / +25 %.
**Code:**
- Casualties the holder inflicts with fire ×**1.05 / 1.10 / 1.25**
  (`SufferCasualties` ~16242), at any range. ✅
- Resistance to momentary casualties **only at level III, and inverted.** The
  term is `moraleeffectonhighmomentarycasualties` (−0.6) minus a drill
  reduction of `term × training × 0.7`. The perk multiplies the
  **reduction** by 0.5 rather than the term (`UpdateMorale` ~18221). At full
  drill the hit goes from −0.18 to **−0.39** per hour while the trigger holds;
  at no drill nothing changes.
  ❌
- ➕ Level II+: exempt from the "enemy is in cover" morale term (~18132).

**Levels up:** 0.002 per casualty inflicted within half fire range, 50 %. ✅
**Verdict:** ❌ for the morale half; the damage half is ✅.

### 3.7 Ace of Spade — battle 6, infantry

**Manual:** cover +10 / +20 / +50 %; morale behind cover doubled at II / III;
reload behind cover +25 % at III; levels fighting from cover.
**Code:** cover value ×**1.1 / 1.2 / 1.5** (`GetCoverValueFromID` ~26515);
morale behind cover ×**1 / 2 / 2** (~18114); reload in cover ÷**1 / 1 /
1.25** (~10024). ✅
**Levels up:** 0.001 per casualty inflicted, 50 % — **with no cover test**
(~16680), unlike Lightning brigade and Sapper gunners, which check it.
**Verdict:** ✅ (levels ⚠️)

### 3.8 Iron discipline — battle 7, infantry

**Manual:** cohesion recovers 25 / 50 / 100 % faster.
**Code:**
- Cohesion approach rate ×**1.25 / 1.5 / 2** (`CalculateCohesion` ~19554).
  The rate is symmetric: cohesion moves toward a target that falls under fire
  or on the move, so the perk also **speeds the fall** by the same factor. ⚠️
- **Level II+ freezes the friendly-rout penalty instead of ignoring it**
  (`UpdateMorale` ~17953). A friendly rout within 60 adds −0.075 to
  `friendlyroutednear`, which is applied to morale per hour and normally
  decays at 0.3 per hour, so it's gone in 15 minutes. From level II the
  perk sets the **decay** factor to zero. Each rout then costs −0.075 an hour
  for the rest of the battle, down to −1. The manual says this level holds
  "even if other units nearby falter". ❌
- ➕ Level III: the charge "doubt their chances" factor is floored at 1, so
  morale term 13 never applies (~18329).

**Levels up:** 0.002 per casualty the unit **suffers** while infantry or
skirmishers, 50 %. ✅
**Verdict:** ❌ from level II.

### 3.9 Expert scouts — battle 8, cavalry

**Manual:** concealment +10 / +25 / +50 %; visibility +25 / +50 % at II / III.
**Code:** concealment **−10 / 25 / 50** (`FogOfWar` ~540) and line of sight
×**1 / 1.25 / 1.5** (~624). ✅ A third branch, `HasPerk(8) && GetPerkLevel(9)
>= 2` (`UpdateMorale` ~18333), reads perk 9's level. A fighting unit has one
slot, so it can't hold both; the branch is dead.
**Levels up:** 0.1 per enemy unit first revealed, but **only when the
revealer is `unittyp == 4`**, a detached scout company (`CheckIfEnemyIsRevealed`
~13048). The cavalry regiment never earns it itself, and reattaching averages
the detachment's experience back in by strength share and drops any level it
gained (§2.2).
**Verdict:** ✅ effects; ⚠️ levels very slowly.

### 3.10 Cold steel — battle 9, cavalry

**Manual:** melee +10 / +20 / +50 %; morale hit on the charge target doubled at II / III.
**Code:**
- `meleestrength` ×1.1 / 1.2 / 1.5 — display only, as in §3.5. ❌
- Target's "engaged by enemy cavalry" morale term ×**1 / 2 / 2** when the
  holder is mounted and the target isn't in square (formation 4)
  (`UpdateMorale` ~18305). ✅

**Levels up:** 0.004 per melee casualty inflicted, with the type test on the
**victim** (`SufferCasualties` ~16502): it only levels against cavalry or
scouts. A regiment that charges infantry earns nothing.
**Verdict:** ❌ for melee; ✅ for morale at II / III.

### 3.11 Mounted rifles — battle 10, cavalry

**Manual:** reload slowdown while mounted −25 % / −50 % / none.
**Code:** both mounted penalties, accuracy (×0.6) and reload time (×1.25),
become `(1 − base) × f + base` with f = **0.75 / 0.5 / 0.2**
(`FireBullet` ~9620, `UpdateFiringAndReloading` ~10004). That recovers a
share *f* of the penalty, so the authored "remaining malus" array runs
backwards:

| | No perk | I | II | III |
| --- | --- | --- | --- | --- |
| Mounted accuracy | 0.60 | 0.90 | 0.80 | **0.68** |
| Mounted reload time | 1.25 | 1.06 | 1.13 | **1.20** |

**Levels up:** 0.002 per casualty inflicted while mounted. The test is
`unittyp == 1 || == 3`, so detached cavalry scouts (4) never earn it; infantry
skirmishers (3) qualify but are never mounted.
**Verdict:** ❌ — always better than nothing, and **weaker at every level**.
It also covers accuracy, which the manual doesn't mention.

### 3.12 Stormers — battle 11, cavalry

**Manual:** allows running; loose-order accuracy +10 / +25 % at II / III.
**Code:** running (`CheckAbilityToRun` ~4209) ✅; accuracy ×**1 / 1.1 /
1.25** in formation 1 (~9644) ✅; ➕ level III exempt from the "enemy is in
cover" morale term (~18136).
**Levels up:** 0.001 per casualty inflicted in loose formation (same
`1 || 3` test as §3.11).
**Verdict:** ✅

### 3.13 Vanguard — battle 12, cavalry

**Manual:** I: no morale hit if detached. II: also when flanked or falling
back. III: also when retreating.
**Code:**
- **At any level**, zeroes the outflanked term (slot 2, up to −0.27/h when
  surrounded) and the flanking-fire term (slot 3, up to −1.0/h under fire
  from all sides) (`UpdateMorale` ~18034, ~18070). That's the manual's level
  II effect, a level early.
- II+: no falling-back-under-fire term; III: no retreating-under-fire term
  (~18252, ~18269). ✅
- **The detached penalty is never touched.** "Outside the influence of its
  commander" (slot 15, ~18371) has no perk test. ❌

**Levels up:** 0.002 per casualty inflicted while detached. ✅
**Verdict:** ❌ — level I's headline is missing, and its level II benefit
arrives at level I.

### 3.14 Lightning brigade — battle 13, cavalry

**Manual:** same as Ace of Spade.
**Code:** cover ×**1.1 / 1.2 / 1.5** per cover object (~26509); morale
behind cover ×**1 / 2 / 2** (~18118); reload in cover ÷**1 / 1 / 1.25**
(~10008). ✅
**Levels up:** 0.0015 per casualty inflicted **from cover**. ✅
**Verdict:** ✅

### 3.15 Close support — battle 14, artillery

**Manual:** canister doubled; canister reload +10 / +20 % at II / III; melee
+25 / +50 % at II / III.
**Code:** canister casualties ×**2.0** at every level (`Projectile` ~174) ✅;
reload ÷**1 / 1.1 / 1.2** on **every** reload, not only canister (~10016)
⚠️; melee ×1 / 1.25 / 1.5, display only ❌.
**Levels up:** 0.0025 × canister casualty points, only once the perk is
already held.
**Verdict:** ⚠️

### 3.16 Skilled laying — battle 15, artillery

**Manual:** counter-battery accuracy +10 / +20 / +50 %; morale hit from
artillery −50 % / none at II / III.
**Code:** accuracy ×**1.1 / 1.2 / 1.5** in counter-battery
(`combatbehavior == 8`, ~9626); incoming-artillery morale term ×**1 / 0.5 /
0** (~18192). ✅
**Levels up:** 0.005 per counter-battery shot.
**Verdict:** ✅

### 3.17 Grand battery — battle 16, artillery

**Manual:** bombardment accuracy +10 / +20 / +30 %; enemy morale hit +50 %
at II, doubled at III.
**Code:** accuracy ×**1.1 / 1.2 / 1.3** in bombardment (`combatbehavior ==
9`, ~9634). ✅ The morale side is **amplified past its label.** For each
incoming artillery source the target's term counts the source and adds its
distance, then divides by the average distance. Grand battery adds
`malus − 1` (0 / 0.5 / 1.0) to the **count without adding distance**
(~18170), so the average distance shrinks by the same factor. For a lone
holder firing from 20 or more the term grows **×2.25 at II and ×4 at III**,
not ×1.5 / ×2. Machine guns, which also count double, add their distance a
second time and stay at ×2.
**Levels up:** 0.005 per bombardment shot.
**Verdict:** ⚠️ — stronger than advertised.

### 3.18 Flying battery — battle 17, artillery

**Manual:** speed +15 / +30 / +50 %; no falling-back hit at II, no retreat hit at III.
**Code:** speed ×**1.15 / 1.3 / 1.5** (`RegimentMove` ~22048); morale gates
at II and III (~18256, ~18273). ✅
**Levels up:** 5.0 per hour while falling back or retreating, **per**
non-artillery enemy whose fire range covers the battery
(`UpdatePerkFlyingBattery` ~26781). ✅
**Verdict:** ✅

### 3.19 Sapper gunners — battle 18, artillery

**Manual:** as Ace of Spade.
**Code:** cover ×**1.1 / 1.2 / 1.5** (~26510); morale behind cover ×**1 / 2
/ 2** (~18122); reload ÷**1 / 1 / 1.25** — **with no cover test**
(~10012), so level III reloads 25 % faster everywhere.
**Levels up:** 0.005 per casualty inflicted from cover. ✅
**Verdict:** ⚠️

---

## 4. Army perks (18)

Held by formations (`unittyp` 14–16, 18). Read on the campaign map and in
auto-resolve. Experience rates are per campaign year unless stated otherwise.

### 4.1 Siege Train — army 0

**Manual:** siege firepower +25 / +50 % / doubled.
**Code:** `Autocalc.EnvolvedUnits.UpdateSiegeStrength` (~407) computes
`siegestrength = 1 + Σ(factor − 1) / topunits.Count` with factor **1.25 /
1.5 / 2**, own and parent, **then adds 1**. `FightUnit` multiplies siege
weapon strength by it (~3133). Without the perk that baseline is **2.0**, so
the perk's relative gain is **halved** (+12.5 % / +25 % / +50 % for a lone
holder) and then **averaged** over every top unit on its side. The defending
side computes its own and benefits the same way.
**Levels up:** 5.0 for **both sides** of any siege, attacker or defender
(`UpdateSiegeCombat` ~2151).
**Verdict:** ⚠️ — half the stated effect.

### 4.2 Field Telegraph — army 1

**Manual:** order delays −10 / −20 / −30 %; information delays −25 / −50 % at II / III.
**Code:**
- Campaign order delay ×**0.9 / 0.8 / 0.7**, own and parent
  (`AddOrderCourierline` ~14954). ✅
- "Information delay" ×**1.0 / 0.75 / 0.5** is applied in
  `SpottedEnemy.UpdateMarks` (~249) to **the holder's own** fog marks: the
  chance that the *player* gets a "last seen here" mark for this formation.
  It's counter-intelligence, and it only touches AI formations, because
  player units are never in fog. In practice even that is nil: the base term
  saturates at its clamp within about eight game hours whatever multiplies it
  ([29](29-dead-prefs-audit.md) §4.1). ❌

**Levels up:** 0.025 per movement order executed, plus 0.025 × 0.25 to the
ordered unit when that's a different unit (`ProcessOrders` ~15253).
**Verdict:** ✅ order delays; ❌ information delays.

### 4.3 Balloon Corps — army 2

**Manual:** order delays +25 / +10 / +10 % (a cost); intelligence +25 / +25
/ +50 %; siege effectiveness +5 % at III.
**Code:**
- Order delays ×**1.25 / 1.10 / 1.10** (~14962). The manual lists this as the
  perk's cost. ✅
- Intelligence ×**1.25 / 1.25 / 1.5**, **only while the formation is
  stationary** (`regimentpaths <= 0`, `UpdateGroupStrength` ~10935). The
  config label says "when stationary"; the manual doesn't. ✅
- Siege strength ×**1.05 / 1.10 / 1.15** at **every** level, through the
  same halving and averaging as §4.1 (`Autocalc` ~419). ⚠️

**Levels up:** as Field Telegraph.
**Verdict:** ✅ (siege ⚠️)

### 4.4 Bureau of Military Information — army 3

**Manual:** intelligence +10 / +25 / +50 %; information delay −25 / −50 % at II / III.
**Code:**
- Intelligence ×**1.1 / 1.25 / 1.5**, stationary only (~10943). ✅
- Information delay: `SpottedEnemy` ~261 **divides** the holder's own mark
  chance by 1.0 / 0.75 / 0.5, **once per enemy unit inside its command
  range**. That makes an AI formation holding the Bureau *easier* for the
  player to track — the opposite of Field Telegraph's operator on the same
  values. It saturates the same way. ❌
- ➕ Gets Expert skirmishers' concealment on the map (§7).

**Levels up:** 1.0 per brigade while the formation scouts
(`cavalryorders == 1`, `UpdateCampaignUnitStats` ~6255).
**Verdict:** ✅ intelligence; ❌ information delay.

### 4.5 Skilled Cartographers — army 4

**Manual:** intelligence and movement speed +5 / +10 / +20 %.
**Code:** intelligence ×**1.05 / 1.10 / 1.20**, stationary only (~10951);
speed ×**1.05 / 1.10 / 1.20** (`RegimentMove` ~22058). ✅
**Levels up:** as the Bureau.
**Verdict:** ✅

### 4.6 Flying Column — army 5

**Manual:** speed +10 / +10 / +25 %; maximum supplies +50 / +100 / +150 %.
**Code:**
- Speed ×**1.1 / 1.1 / 1.25** (~22066). ✅
- "Maximum supplies" is really **consumption divided by 1.5 / 2.0 / 2.5**
  (`UpdateIIPSupplyLink` ~5061 sets the consumption factor to
  `1 / suppliescarriedflyingcolumn`). The stock cap is unchanged, so resupply
  still stops at the normal cap. The column lasts as long as the manual
  promises when cut off, and also draws less from its depots. ⚠️

**Levels up:** 1.0 for any campaign movement (`RegimentMove` ~22803). ✅
**Verdict:** ⚠️ — a different mechanism with a similar result.

### 4.7 Foot Cavalry — army 6

**Manual:** forced-march speed +10 / +20 / +30 %; forced-march attrition −10 / −20 / −30 %.
**Code:**
- Speed ×**1.1 / 1.2 / 1.3** inside `if (calledfromcampaign)` (~22207),
  with **no forced-march test and no `unittyp` test**. It applies to every
  campaign move, and **a fleet holding fleet perk 6, Sea Raiders, gets it
  too.** ⚠️
- Attrition ×**0.9 / 0.8 / 0.7** on the forced-march sickness multiplier
  (`UpdateCampaignUnitStats` ~6040). That code runs on the **brigades**
  (`unittyp <= 13`; the method returns early above 13), so `HasPerk(6)` reads
  a brigade's **battle** perk 6, Ace of Spade. `HasPerkParent` is false for
  brigades. The formation's Foot Cavalry never reaches it. ❌

**Levels up:** 3.0 while moving at double-quick or running (~22806). ✅
**Verdict:** ❌ attrition; ⚠️ speed (broader than stated).

### 4.8 Engineers & Mechanics — army 7

**Manual:** construction speed +25 / +50 % / doubled.
**Code:** on placement, `constructiontimer = 1 / 1.25, 1.5, 2` (own, *or*
parent's — assigned, not stacked; `CBuilding` ~1413). Construction moves
`Condition` up and the timer down by the same step. The building therefore
**"finishes" at Condition 0.8 / 0.67 / 0.5**: the completion message fires
and a fort gets its garrison. But in the same update cycle,
`UpdateRaidSmoke` (~419) sees Condition below 1 more than
`timetostopsmokeanimation` (0.003 yr) after placement and **re-arms the timer
to `1 − Condition`**. The building goes back under construction and finishes
the rest at the normal rate. **Total time to a full-condition building is
unchanged.** What the perk buys is an early completion event (and, for a fort,
an early garrison dummy). ❌
**Levels up:** a fixed grant on placement: fort 0.25, depot 0.15, telegraph
station 0.075. ✅
**Verdict:** ❌

### 4.9 Ambulance Corps — army 8

**Manual:** speed loss from wounded −25 / −50 / −75 %; mortality of wounded −10 / −25 / −50 %.
**Code:**
- The wounded share in the speed penalty ×**0.75 / 0.5 / 0.25**
  (`RegimentMove` ~22107). ✅
- Mortality ×**0.9 / 0.75 / 0.5** (`UpdateCampaignUnitStats` ~5998), but
  this runs on **brigades**, like §4.7. `HasPerk(8)` there is **battle**
  perk 8, Expert scouts, so **cavalry brigades with Expert scouts get the
  mortality cut and the Ambulance Corps formation's wounded do not.** ❌
- ➕ Gets Expert scouts' concealment *and* line of sight on the map (§7).

**Levels up:** 3.5 × (wounded share) per brigade. ✅
**Verdict:** ❌ mortality; ✅ speed.

### 4.10 Pontoon Train — army 9

**Manual:** movement speed across small rivers +25 / +50 / +75 %; supply efficiency +10 / +20 / +30 %.
**Code:**
- "Movement cost crossing creeks" ×**0.75 / 0.5 / 0.25** multiplies the
  **NavMesh area cost** of `Small Rivers` (base 50 vs 4 for open ground) in
  `AddPath` (~19876). That's a pathfinding cost: the route planner detours
  less around creeks. Nothing in `RegimentMove` reads area cost, so the unit
  doesn't move faster in a creek. ⚠️
- "Supply speed" ×**1.1 / 1.2 / 1.3** multiplies the **elapsed time** of the
  whole supply step (`UpdateIIPSupplyLink` ~5044). Consumption, depot
  draw-down, resupply **and the upkeep it pays** all scale together. The unit
  runs its supply clock 10–30 % fast. It isn't more efficient; it spends more
  per day. ❌

**Levels up:** 50 on creek or river-crossing terrain (`RegimentMove` ~22614).
**Verdict:** ❌ supply; ⚠️ movement.

### 4.11 Sappers and Miners — army 10

**Manual:** siege assault efficiency +25 / +50 % / doubled.
**Code:** `FightUnit` (~3082) multiplies `num5` by **1.25 / 1.5 / 2** when
`type == 2` (siege) and the stance is assault. A few lines later, still for
`type == 2`, `num5` is **overwritten** with `GetFortWeaponStrength(unit0)`
(~3125). That function returns the raw weapon firepower for a non-garrison
unit. **The multiplier never survives.** ❌
➕ Instead it gets River Expedition's embarked bonus: the `HasPerk(10)` test at
~3137 picks `fightingstrengthembarkedriverexp` (0.50, labelled *"…has perk
'river expedition'"*) over `fightingstrengthembarked` (0.25) on creek, river,
crossing or sea terrain.
**Levels up:** 40 while attacking in a siege (~2154).
**Verdict:** ❌

### 4.12 Land Torpedoes — army 11

**Manual:** skirmishing efficiency +5 / +10 / +20 %; enemy movement within range −10 / −20 / −30 %.
**Code:**
- Skirmish ×1.05 / 1.10 / 1.20 is gated `type == 2` (siege) with defend stance
  (~3101), and is overwritten exactly as in §4.11. Real skirmishes (`type ==
  6`, `UpdateSkirmishing`) read no perks at all. ❌
- Enemy formations inside the holder's command range move at ×**0.9 / 0.8 /
  0.7** (`RegimentMove` ~22079). ✅

**Levels up:** 25 per brigade while an enemy is inside command range, **only
once the perk is held** (`groupunit.HasPerk(11)`, ~6259).
**Verdict:** ❌ skirmish; ✅ movement.

### 4.13 Partisan Brigades — army 12

**Manual:** skirmishing efficiency +25 / +50 % / doubled; raiding morale hit on enemies +10 / +25 / +50 %.
**Code:** `Autocalc.UpdateRaiding` has two blocks. The first damages the
raided enemy and reads **no perks** (~2368). The second inflicts **return
casualties and a morale hit on one of the raider's own brigades**, picked from
`raidingunits[0]` — and that is where both multipliers sit (~2403): own
casualties ×**1.25 / 1.5 / 2**, own morale hit ×**1.10 / 1.25 / 1.50**. ❌
**Levels up:** 2.5 while raiding. ✅
**Verdict:** ❌ — **inverted**: it costs the raider and does nothing to the
enemy.

### 4.14 Bushwhackers — army 13

**Manual:** skirmishing efficiency +25 / +20 / +10 % (declining, as authored);
raiding support hit +10 / +25 / +50 %.
**Code:**
- Skirmish ×**1.25 / 1.20 / 1.10** in the same own-casualties block as §4.13.
  ❌ (The declining array matches the manual. Since it's a penalty, it
  happens to shrink as the perk levels.)
- Support: `Regiment.UpdateRaids` (~4653) scales both state-support writes of
  a raid in an enemy state by **1.10 / 1.25 / 1.50**. In the shipped data
  those writes are **−0.00025** to the **raider's own** support and
  **+0.0001** to the **owner's**. The perk amplifies a backlash, not damage
  to the enemy. ⚠️

**Levels up:** 2.5 while raiding.
**Verdict:** ❌

### 4.15 Embedded Reporters — army 14

**Manual:** fame development +25 / +50 % / doubled.
**Code:** `BattleUnits.CalculateCommanderFame` (~5912) applies the factor to
units of `unittyp >= 15` when `CheckForGroupPerk(unit, 14)` finds a holder.
That walk **starts at the unit's parent and never checks the unit itself**.
The factor is then indexed by **`unit.GetPerkLevel(14)`, the unit's own
level**, which is 0 when it doesn't hold the perk. So:
- the holder's own commander gets nothing, unless the holder's parent also
  holds it;
- every subordinate Corps gets **×1.25, the level I factor, whatever level
  the holder has reached**.

It scales defame as well as fame (`num5` can be negative). ❌
**Levels up:** 0.000002 × `groupstrength` per battle fought
(`ImportExportUnitData` ~3473), about 20 battles per level at 25,000 men. ✅
**Verdict:** ❌

### 4.16 Limelights — army 15

**Manual:** siege and skirmishing efficiency at night +25 / +50 % / doubled.
**Code:** ×**1.25 / 1.5 / 2** at night in `FightUnit` (~3112) for `type ∈
{0, 1, 2}`. `FightUnit` is only ever called for type 0 (auto-resolved land
battle) and type 2 (siege), and **type 2's value is overwritten** (§4.11).
Type 1 (raid) and type 6 (skirmish) never reach it. So **it works only in
auto-resolved field battles at night — the one case the manual doesn't
list.** ❌
**Levels up:** 5.0 while attacking in a siege (~2155).
**Verdict:** ❌

### 4.17 Expert Scouts — army 16

**Manual:** intelligence +25 / +50 % / doubled.
**Code:** ×**1.25 / 1.5 / 2.0**, stationary only (~10959). ✅
**Levels up:** as the Bureau. ✅
**Verdict:** ✅

### 4.18 River Expedition — army 17

**Manual:** river movement speed +25 / +50 % / doubled; siege assault efficiency +10 / +25 / +50 %.
**Code:**
- Speed ×**1.25 / 1.5 / 2.0** on campaign terrain 3, 4 or 5 (Creek, Rivers,
  River Crossings) (~22173). ✅
- Siege assault ×1.1 / 1.25 / 1.5 is overwritten like §4.11. ❌
- The embarked-strength override meant for this perk goes to perk 10 (§4.11). ❌
- The campaign AI never picks it (§2.3).

**Levels up:** 40 on creek, river or crossing terrain (~22610). ✅
**Verdict:** ✅ speed; ❌ the rest.

---

## 5. Fleet perks (11)

Held by fleets (`unittyp == 17`). The perk is read from the fleet regiment:
`Autocalc`'s `singleshipregref[i]` is the fleet each vessel belongs to.

### 5.1 Torpedoes — fleet 0

**Code:** ramming damage dealt ×**1.25 / 1.5 / 2** (`UpdateNavalBattle`
~1725). ✅ **Levels up:** 0.025 × ram probability per ram. ✅ **Verdict:** ✅

### 5.2 Mortar Boats — fleet 1

**Code:** fort-bombardment strength ×**1.05 / 1.10 / 1.25**
(`UpdateFortNavyEngagement` ~1301). ✅ **Levels up:** 10 while bombarding. ✅
**Verdict:** ✅

### 5.3 Supply Colliers — fleet 2

**Manual:** speed −30 / −20 / −10 %; supply consumption −15 / −30 / −50 %.
**Code:** campaign speed ×**0.7 / 0.8 / 0.9** (`RegimentMove` ~22094);
provisions consumption ×**0.85 / 0.70 / 0.50** (`Vessel.UpdateConsumation`
~211; provisions only, not coal or ammunition). ✅
**Levels up:** 10 while bombarding and 2.0 while blockading. ✅
**Verdict:** ✅

### 5.4 Balloon Barge — fleet 3

**Manual:** intelligence +25 / +35 / +50 %; fort bombardment +5 / +10 / +15 %.
**Code:** intelligence ×**1.25 / 1.35 / 1.50** (`UpdateGroupStrength`
~10972) ✅; bombardment ×**1.00 / 1.00 / 1.05** (~1307) — the shipped array
gives nothing at I and II, and a third of the stated bonus at III. ⚠️
➕ Gets Expert skirmishers' concealment on the map (§7).
**Levels up:** 10 while bombarding.
**Verdict:** ⚠️ (data)

### 5.5 Limelights — fleet 4

**Code:** ×**1.25 / 1.5 / 2** at night on fort-bombardment strength (~1312)
and on each vessel's firing strength in naval battle (~1626). It uses its own
array, `combatefficiencynighttime`, separate from army perk 15's. ✅
**Levels up:** 10 while bombarding. ✅
**Verdict:** ✅

### 5.6 Blockading Squadron — fleet 5

**Code:** this fleet's contribution to a harbor or trade-node blockade ×**1.1 /
1.25 / 1.5** (`IIP.UpdateBlockades` ~660). ✅ **Levels up:** 2.0 while
blockading. ✅ **Verdict:** ✅

### 5.7 Sea Raiders — fleet 6

**Manual:** allows blockading trade nodes; import prices +10 / +25 / +50 %.
**Code:**
- The unlock works: a blockading holder counts at a trade node
  (`IIP.UpdateBlockades` ~646). ✅ If several Sea Raiders share a node, the
  last one in `unitsinrange` sets the price factor, not the highest.
- **Import prices are display only.** The factor is stored in
  `IIP.priceadjustment`, and the only reader is the map tooltip
  (`CBuildingIcon` ~429, "Sea raiders affect import prices by …"). No price
  code reads it. ❌
- ➕ Level III calls `SetFamous()` on the commander on every blockade update.
- ➕ The fleet also gets Foot Cavalry's +10 / +20 / +30 % speed (§4.7, §7).

**Levels up:** 0.5 while blockading a trade node (`Regiment.UpdateBlockades`
~4731).
**Verdict:** ❌ prices; ✅ unlock.

### 5.8 Amphibious Attack — fleet 7

**Code:** required for any capture roll. On a 10 % gate per cycle, the
capture chance is `(own firepower share)^10 × elapsed × 1.00 / 1.10 / 1.50 ×
1750` (`UpdateFortNavyEngagement` ~1438). ✅
**Levels up:** 10 while bombarding. ✅
**Verdict:** ✅

### 5.9 Blockade Busters — fleet 8

**Manual:** fighting efficiency against blockaders +10 / +20 / +30 %;
blockade efficiency within the fleet's radius −10 / −20 / −30 %; levels by
attacking blockaders.
**Code:**
- Firing strength ×**1.1 / 1.2 / 1.3** against a vessel whose fleet is
  blockading (`UpdateNavalBattle` ~1635). ✅
- Own-port blockade ×**0.9 / 0.8 / 0.7** (`IIP.UpdateBlockades` ~671), but
  it's applied **inside** the per-unit loop, so it only discounts the
  blockade summed from units processed before the holder. Blockaders later in
  `unitsinrange` are untouched. ⚠️
- **Levels up when the holder is the one blockading.** The experience test is
  the holder's own `fleetorders == 1` (~1643), while the effect tests the
  target's. Attacking blockaders from port earns nothing. ❌
- ➕ **Choosing the perk makes the commander famous** (`ChoosePerk`).
- ➕ Gets Expert scouts' concealment and line of sight on the map (§7).

**Verdict:** ⚠️

### 5.10 Battery Runner — fleet 9

**Code:** firing strength against forts ×**1.25 / 1.5 / 2** (`Vessel` ~695);
➕ exempt from the speed penalty of being engaged by a fort it's bombarding
(`RegimentMove` ~22754; the main manual text describes this).
**Levels up:** 1.5 while moving and 10 while bombarding, **if the fleet
contains an ironclad**. ✅
**Verdict:** ✅

### 5.11 Port Defender — fleet 10

**Code:** firing strength ×**1.25 / 1.5 / 2** when the fleet has a friendly
harbor in range (`closestiipforsupply != null`, ~1647). ✅
**Levels up:** 5.0 in any naval battle. ✅ The campaign AI never picks it (§2.3).
**Verdict:** ✅

---

## 6. Auto-resolve swaps Sharpshooters and Zouave

All three auto-resolve sites use the old numbering from §2.3:

| Site | Test | Effect | Should be |
| --- | --- | --- | --- |
| `Autocalc.FightUnit` ~3062 | `HasPerk(2)` | `1 + sharpshooterperkinfluenceautocalc[stance] × level` on casualties dealt — 0.1 / 0.07 / 0.03 / −0.03 per level by stance, so **level I adds nothing** and retreat stance is a penalty | Sharpshooters |
| `Autocalc.FightUnit` ~3229 | `!HasPerk(1)` | exemption from the fatigue morale hit | Zouave (battle: ~18375) |
| `Autocalc.UpdateFatigue` ~3349 | `HasPerk(1)` | `perk_zouaves_fatiguefactor[GetPerkLevel(1)]` | Zouave |

In auto-resolve, **Sharpshooters brigades march like Zouaves, and Zouave
brigades shoot like sharpshooters.**

---

## 7. Cross-namespace collisions

Sites where an unguarded `HasPerk(n)` reads a perk from the wrong table.
Where the last column says **instead**, the intended perk loses the effect;
where it says **also**, another perk gets it free.

| Site | Test | Meant for | Also, or instead, fires for |
| --- | --- | --- | --- |
| `UpdateCampaignUnitStats` ~5998 | `HasPerk(8)` on a brigade | Ambulance Corps (army 8) | **Instead:** cavalry brigades with Expert scouts (battle 8) |
| `UpdateCampaignUnitStats` ~6040 | `HasPerk(6)` on a brigade | Foot Cavalry (army 6) | **Instead:** infantry brigades with Ace of Spade (battle 6) |
| `RegimentMove` ~22207 | `HasPerk(6)`, campaign, no `unittyp` | Foot Cavalry | **Also:** fleets with Sea Raiders (fleet 6) |
| `FogOfWar.UnitIsHidden` ~536 | `HasPerk(3)`, no scene or type guard | Expert skirmishers | **Also:** Bureau formations (army 3), Balloon Barge fleets (fleet 3) |
| `FogOfWar` ~540, ~622 | `HasPerk(8)` | Expert scouts | **Also:** Ambulance Corps formations (army 8), Blockade Busters fleets (fleet 8) — concealment **and** line of sight |
| `CheckAbilityToRun` ~4205, ~4209 | `HasPerk(2)`, `HasPerk(11)`, battle, no `unittyp` | Zouave, Stormers | **Also:** battle-scene HQs carrying Balloon Corps (army 2) or Land Torpedoes (army 11) can run |
| `Regiment.UpdateBlockades` ~4700 | `HasPerk(6)` | Sea Raiders | **Also:** Foot Cavalry formations; [19](19-commanders-and-perks.md) §1.7 notes no shipped trade node is reachable by land |
| `Autocalc.FightUnit` ~3137 | `HasPerk(10)` | River Expedition (17), per the pref label | **Instead:** Sappers and Miners (army 10) |

Formations do carry their perk list into battle (`BattleUnits.import_units`
~10259 reads four slots per group), which is why the `FogOfWar` and
`CheckAbilityToRun` rows apply to battle-scene HQs as well as the campaign map.

---

## 8. Scorecard

| Verdict | Battle (19) | Army (18) | Fleet (11) |
| --- | --- | --- | --- |
| ✅ works as written | Ace of Spade, Expert scouts, Expert skirmishers, Flying battery, Lightning brigade, Skilled laying, Stormers, Zouave¹ | Balloon Corps, Expert Scouts, Skilled Cartographers | Amphibious Attack, Battery Runner, Blockading Squadron, Limelights, Mortar Boats, Port Defender, Supply Colliers, Torpedoes |
| ⚠️ works differently | Close support, Engineering, Grand battery, Sapper gunners, Sharpshooters | Flying Column, Siege Train | Balloon Barge, Blockade Busters |
| ❌ headline dead, inverted or misdirected | Cold steel, Deadly volley, Iron discipline, Mounted rifles, Spirited charge, Vanguard | Ambulance Corps, Bushwhackers, Embedded Reporters, Engineers & Mechanics, Field Telegraph², Foot Cavalry, Land Torpedoes, Limelights, Partisan Brigades, Pontoon Train, River Expedition², Sappers and Miners, Bureau of Military Information² | Sea Raiders² |

¹ In battle; broken in auto-resolve (§6).
² Half works: Field Telegraph's order delays, River Expedition's speed, the
Bureau's intelligence and Sea Raiders' trade-node unlock are fine.

The ❌ defects come in six kinds:

| Kind | Perks |
| --- | --- |
| Multiplies a value nothing reads | Spirited charge, Cold steel and Close support (`meleestrength`); Sea Raiders (`priceadjustment`); Sappers and Miners, Land Torpedoes, River Expedition and Limelights (overwritten `num5`) |
| Arithmetic points the wrong way | Mounted rifles (recovery share authored as remaining malus); Deadly volley III (halves the drill reduction); Iron discipline II+ (freezes the decay) |
| Lands on the wrong unit | Partisan Brigades and Bushwhackers (raider's own brigade); Ambulance Corps and Foot Cavalry (brigade's battle perk); Embedded Reporters (parent walk skips self, level from wrong unit) |
| Undone later in the frame | Engineers & Mechanics (`UpdateRaidSmoke` re-arms the timer) |
| Wrong quantity scaled | Pontoon Train (whole supply clock; path cost, not speed) |
| Gate missing | Vanguard (detached term never tested); Field Telegraph and the Bureau (operate on the player's marks of AI units, and saturate anyway) |

---

## 9. Against findings/19

This audit was done blind and then compared. **Agreements**: the namespace
model, slot counts and inheritance; the experience rules; Mounted rifles,
Deadly volley III and Iron discipline II+ as inverted; the dead Expert scouts
III branch; `Autocalc` ~3349; the three display-only melee factors; Sapper
gunners' missing cover test; the `SpottedEnemy` operators; Engineers &
Mechanics assigning rather than stacking. **Corrections and additions:**

| findings/19 says | This audit finds |
| --- | --- |
| §1.4, and the summary: namespaces are "unambiguous at runtime everywhere but one site" | At least eight collision sites (§7), two of which strip Ambulance Corps and Foot Cavalry of their attrition effects |
| §1.7: the six "blank" campaign perks "all have real effects" | Sappers and Miners, Land Torpedoes (skirmish), River Expedition (assault) and Limelights (siege) are overwritten in `FightUnit`; Partisan Brigades and Bushwhackers act on the raider's own brigade; Engineers & Mechanics doesn't shorten total build time |
| §1.6, §1.7: Balloon Corps order delays "point the wrong way" | The manual lists +25 / +10 / +10 % as the perk's **cost**; it is as designed |
| §1.6, §1.7: Bushwhackers' declining array is a defect in inflicted casualties | The declining 25 / 20 / 10 is in the manual; the real defect is that it scales the raider's own casualties |
| §1.7: Sea Raiders raises prices 10 / 25 / 50 % at a trade node | `priceadjustment` is read only by the tooltip |
| §1.6: `Autocalc` ~3229 "may be the same slip; not traced" | Traced: same swap, and there's a third site at ~3062. The legacy `157_Zouaves → 1` / `158_Sharpshooters → 2` buttons show the old order |
| §1.2: Embedded Reporters is a fame multiplier for `unittyp >= 15` | It skips the holder and always applies the level I factor |
| §1.2: Pontoon Train is "movement cost" | NavMesh path cost, plus a supply-clock speed-up that raises consumption and upkeep |
| [15](15-attrition-and-condition.md) §2b: sickness "reduced by `attritionfootcavalry`[perk 6], self + parent" | Reads the brigade's battle perk 6; the parent test is always false for brigades |
| Not covered | Siege Train halved by the +1 baseline; Grand battery morale squared; Vanguard's missing detached gate; Sharpshooters' unread range gate; Cold steel and Spirited charge levelling on the victim's type; Expert scouts levelling only on detachments; the campaign AI never picking the last perk and filling a 4th slot; the seven battlefield `unitprefs.txt` copies |

---

## Open questions

- **How many perk rows the prefabs enable** for armies, corps and fleets. The
  code allows three and the data four; the manual says three and two.
  Settling it needs an asset dump, the same one [19](19-commanders-and-perks.md)
  wants for `PerksShownGroup`.
- **Whether an attached brigade's `doublequick` is ever true on the campaign
  map.** The only campaign writer found is `RegimentMove` on the unit that
  moves. If a brigade's flag stays false, the forced-march sickness multiplier
  — and with it §4.7's misdirected perk test — never fires at all.
- **Whether `UpdateRaidSmoke` re-arms every Engineers & Mechanics build.** The
  reading in §4.8 assumes `lasttimeconditionwasreduced` stays at the
  placement date (set in `Start`) and that construction lasts more than 0.003
  years. Watching one fort in a running game would settle it.
- **The real rate of the blanket auto-resolve grant** (§2.2). The 0.58 per day
  per pairing is derived from `landbattleintensity` and the campaign time
  unit; how many `FightUnit` pairings a brigade sits in per cycle wasn't
  counted.
- **Whether the campaign AI's `ownunits` can include a top-level brigade.** If
  it can, `ChoosePerk` would put an army-perk id into a one-slot unit, where
  battle code would read it as a battle perk.
- **Which battle modes load the seven battlefield `unitprefs.txt` copies.**
  `BattleController` uses them for any operation other than 3 and 8; whether
  every standalone historical battle on those fields is one of them is a scene
  question.
