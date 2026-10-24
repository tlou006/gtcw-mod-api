# 17 — Politics: state support, national morale and intervention

**Build:** see [00-build-provenance](00-build-provenance.md)
**Source:** `Assembly-CSharp.dll` — `GameVars.Nation`, `GameVars.Alliance`,
`CampaignStrategy`, `CampaignObjective`, `Policies`, `Regiment`
**Summary:** [summary-politics](summary-politics.md)

Fourth Phase 2 subsystem, taken out of the planned order because
`nation[state].support[]` had already turned up three times from other
directions — capping battle morale, amplifying desertion, and credited by
hospitals.

> **It earned the promotion.** Politics is not a side system that reacts to the
> war; it is an input to recruitment, battle morale, desertion, surrender and
> the loss condition. Three separate political variables, each with its own
> accounting array.

---

## 1. `Nation` is a *state*, not a country

`GameVars.Nation` models a US state: `name`, `abbrev`, `statesetname`,
`townsinstate`, `slaveryallowed`, `occupiedby`, `population`, `census[]`,
`mobilizationrate[]`, `productionfactor[]`, `recruitable`, `contractlength`.

Countries are `GameVars.Alliance`. The array is `new Alliance[n]` sized from
`nations.dat`, and the shipped campaign declares **eight** — not the three or
five this file originally assumed:

| # | Name | Role |
| --- | --- | --- |
| 0 | Union | |
| 1 | Confederacy | |
| 2 | Great Britain | can intervene **for the Confederacy** (§4) |
| 3 | France | can intervene **for the Confederacy** (§4) |
| 4 | Spain | can intervene **for the Union** (§4) |
| 5 | Mexico | |
| 6 | Sea Trade | pseudo-alliance — a trade counterparty |
| 7 | Neutral States | pseudo-alliance — states that had not yet chosen a side |

Names come from config, so the identities are data rather than code. Ten of the
1,906 shipped commanders carry `alliance == 7`
([19](19-commanders-and-perks.md) §10.1), which is why the per-commander loops
that index `GameVars.alliance[]` do not fault.

---

## 2. State support

`Nation.support[]` is indexed by alliance and clamped to `[0, 1]`. **Both
sides have a support value in every state simultaneously** — they are not two
ends of one slider.

```csharp
public void AdjustSupport(int _alliance, float amount, int typ)
```

- Applies **only for `_alliance < 2`** — foreign powers have no state support.
- `amount` is **recomputed after clamping** as the actual achieved delta, so a
  saturated state records zero influence rather than a phantom gain. Good
  hygiene, and worth copying.
- Accumulates into `supportinfluence[alliance, typ]`, a `float[10, 50]`
  breakdown: 10 alliances × 50 reason slots.
- **Slot 0 is a computed total**, recalculated as the sum of slots 1–49 on
  every call. Reason codes are therefore 1-based.

### The 42 reason codes

Hardcoded in `GameVars.supportdescr` — **not config**, so a mod cannot add a
reason without patching:

| # | Reason | # | Reason |
| --- | --- | --- | --- |
| 0 | *Total cumulative* | 21 | Policy: Nationalize Trade |
| 1 | Battle casualties | 22 | Policy: Slavery Restriction |
| 2 | Commander appointments | 23 | Policy: CSCT |
| 3 | National morale | 24 | Policy: Emancipation Act |
| 4 | State occupation | 25 | Policy: The Old Dominion |
| 5 | Drafting | 26 | Policy: Kansas a Slave State |
| 6 | Raids | 27 | Policy: Slavery to the West |
| 7 | Objective: Invade the Confederacy | 28 | Policy: Apostles of Disunion |
| 8 | Objective: State Secession | 29 | Policy: Filibustering |
| 9 | Policy: Agriculture | 30 | Policy: King Cotton |
| 10 | Policy: Bank Act | 31 | Policy: Alliance with the Natives |
| 11 | Policy: Confiscation | 32 | Medical treatment of wounded |
| 12 | Policy: Revenue Act | 33 | Condition of enemy POW camps |
| 13 | Policy: Emancipation Act | 34 | Project: Propaganda |
| 14 | Policy: Militia Act | 35 | Project: Counter-propaganda |
| 15 | Policy: Militia Act not triggered | 36 | Project: Occupation-administration |
| 16 | Policy: Kansas a free State | 37 | Project: suppress population |
| 17 | Policy: Go West | 38 | Commander |
| 18 | Policy: Support Abolitionism | 39 | Incident: a just cause |
| 19 | Policy: Bread Basket | 40 | Incident: escalation |
| 20 | Policy: Impressment | 41 | Objective: West Virginia |

Note **23 of the 41 live codes are policies**, which is why `Policies.cs` holds
27 of the ~49 `AdjustSupport` call sites. This is the concrete mechanism behind
the hardcoded-policy-effects finding in
[09-policy-and-project-effects](09-policy-and-project-effects.md): most policy
effects *are* support adjustments.

`CampaignObjective` contributes 8 call sites, `BattleUnits` 6, `Projects` 4,
`Regiment` 3 (raiding), `GameVars` 2, `DLC_WL` 1.

### `supportinfluencelast` is broken

Alongside the cumulative array, `AdjustSupport` maintains a "recent change"
tracker:

```csharp
supportinfluencelast[_alliance, typ] = Mathf.Pow(amount, 0.5f);
if (supportinfluencelast[_alliance, typ] == float.NaN)
    supportinfluencelast[_alliance, typ] = 0f;
```

Two defects compound:

1. **`Mathf.Pow(negative, 0.5f)` is `NaN`.** Any support *loss* writes NaN.
2. **`x == float.NaN` is always false in C#** — NaN is not equal to itself, so
   the guard never fires. The correct test is `float.IsNaN(x)`.

The NaN then propagates: `BattleUnits` decays the tracker with
`supportinfluencelast[...] /= Mathf.Pow(1f + x, 30f)`, and NaN divided by
anything stays NaN. Nothing ever clears it.

**Consequence 1 — the stale-reset never fires.** `AdjustSupport` resets the
cumulative slot when the tracker is near zero
(`if (Mathf.Abs(supportinfluencelast[...]) < 0.0001f) supportinfluence[...] = 0f`).
`Mathf.Abs(NaN) < 0.0001f` is false, so **once a reason code has ever caused a
loss in a state, its cumulative figure stops being reset.**

**Consequence 2 — the "what is hurting us" UI can never populate.**
`CampaignStrategy.GetStateSupportInfo(alliance, plus, amount)` selects entries
with:

```csharp
(supportinfluencelast[alliance, j] * 100f >= 0.01f && plus)
| (supportinfluencelast[alliance, j] * 100f < -0.01f && !plus)
```

Both comparisons are false for NaN. So the `plus: false` call — the list of
support *losses* — **returns an empty string in every case**, because the only
way a slot becomes negative is the same path that makes it NaN.

> Reported as an observation with the mechanism rather than asserted as intent,
> but the `== float.NaN` test is unambiguously a mistake, and the sqrt of a
> signed delta looks like one too.
>
> **This is a good first target for the hook library.** A postfix on
> `AdjustSupport` that recomputes `supportinfluencelast` as
> `sign(amount) * sqrt(|amount|)` would restore the negative breakdown without
> touching the simulation — a visible fix with no balance impact.

### What state support does

| Consumer | Effect |
| --- | --- |
| `fightingspirit` | **Caps battle morale** in both combat models ([13-morale-and-cohesion](13-morale-and-cohesion.md) §5) |
| Desertion | `(desertionfactorstatesupport − 1) / (1 + support) + 1` multiplies desertions ([15-attrition-and-condition](15-attrition-and-condition.md) §2c) |
| Volunteer pool | Linear in the inner term, then `3 × raw^0.9` (§5) |
| Draft pool | Hard gate at `statesupportrecruiting` (§5) |
| Reenlistment | `0.10 + 0.50 × support`, ±20 %, when a contract expires (§5b) |
| Replacements | The same two pools, drawn automatically ([14](14-supply-and-logistics.md) §3b) |
| National morale | Reason code 33, "State Support" (§3) |
| `GetBalance` | Via the `LoyalStatesSlider` and `NationalSupportSlider` (§6) |
| Policy research | **Linear** multiplier on the rate, via `averagesupport` ([22](22-policy-and-project-catalogue.md) §3.1) |
| Movement speed | The term exists and is **inert** — see below |
| Readiness | Movement consumption lerps `{−8.5, −25.0}` per annum on the support of the state being crossed ([14](14-supply-and-logistics.md) §10b) |
| Loyalty | **Nothing.** Only Militia Act I's one-shot cascade and three scripted objectives ever change `Nation.alliance` (§5a) |

#### `averagesupport` is not an average over states

`Alliance.averagesupport` — the figure the Strategy panel labels *National
Support*, and the one that multiplies policy research speed — is accumulated in
`Economy.UpdateMacroEconomy` (~2068) as

```csharp
// per town, if town.state >= 0 && town.Owner <= 1
alliance[town.Owner].averagesupporttemp
    += town.representingpopulation * nation[town.state].support[town.Owner];
…
alliance[n].averagesupport = averagesupporttemp / max(1f, totalpopulationtemp);
```

Three properties worth knowing before reading anything into the number:

- it is **population-weighted**, so a single large city outweighs a quiet
  state;
- it is keyed on **town ownership**, not on `Nation.alliance`, so **every enemy
  town you capture enters your own average** at your (low) support in that
  enemy state. Conquest therefore *lowers* `averagesupport` and slows your own
  policy research;
- the denominator counts every owned town while the numerator skips towns with
  `state < 0`, so any stateless owned town dilutes the figure downward.

#### The movement-speed penalty is shipped off

```csharp
float num15 = 1f * GameVars.nation[num14].support[alliance]
            + GamePrefs.adjustmentmovingspeedsupport0
              * (1f - GameVars.nation[num14].support[alliance]);
num11 *= num15;
```

`adjustmentmovingspeedsupport0 = 1.0` and the config label declares `[<1]`, so
the blend is identically 1 and there is no penalty at any support level. The
`"Low state support (−x %)"` rolling text guarded by
`num15 < 1 − movementspeedrollingtextinfl` can never print. `Regiment` ~22128
is the only consumer.

---

## 3. National morale

`Alliance.nationalmorale`, also `[0, 1]`, with the same accounting pattern:
`nationalmoraleinfluence[50]`, slot 0 the computed total, and a
`nationalmoraledescr[35]` label table.

`AdjustNationalMorale(int type, float value)` is the clean twin of
`AdjustSupport` — same clamp-and-recompute-delta hygiene, **no NaN bug**
(it keeps no sqrt tracker).

The in-game description is explicit:

> *"Represents the citizens' willingness to fight. It is influenced by policies
> and objectives, and is reduced when losing battles or towns are occupied.
> Recapturing lost towns retrieves the morale loss. **If national morale drops
> too low the campaign is lost.**"*

### The 35 reason codes

| # | Reason | # | Reason |
| --- | --- | --- | --- |
| 1 | Battles | 20 | USCT (Policy) |
| 2 | Naval engagements | 21 | Emancipation (Policies) |
| 3 | Town occupation | 22 | Drafts (Policies) |
| 4 | Loss of capital | 23 | Abolition (Policies) |
| 5 | Militia act (Policies) | 24 | Blockade Runners (Policy) |
| 6 | Invasions (Objectives) | 25 | Mexico (Policy) |
| 7 | Win first major battle (Objectives) | 26 | The Old Dominion (Policy) |
| 8 | **Elections** (Objectives) | 27 | Northern Industrialization (Policy) |
| 9 | Capture forts & towns (Objectives) | 28 | Security Measures (Policy) |
| 10 | Battles (Objectives) | 29 | Indian Wars (Policy) |
| 11 | Seceded states (Objectives) | 30 | Southern Industrialization (Policy) |
| 12 | Bleed them out (Objective) | 31 | Arms Agents (Policy) |
| 14 | Cut Confederacy in Half (Objective) | 32 | Industrialization (Policy) |
| | | 33 | **State Support** |
| | | 34 | **Private Wealth** |

Slots 13 and 15–19 are empty strings — reserved or removed.

> **Slots 33 and 34 are the upward couplings**: state support and private
> wealth (economy) both feed national morale. So the chain runs
> **economy → state support + wealth → national morale → campaign loss.**

> **Elections are implemented as a campaign objective** (slot 8), not as a
> separate system. `CampaignObjective` also tests
> `GamePrefs.demoralizevotersunionmoralelevel` against Union national morale —
> so low morale affects the election outcome.

### Thresholds

| Pref | Use |
| --- | --- |
| `breakmoraletrigger` | Campaign loss — tested per alliance inside `CampaignObjective.CheckIfAccomplished`, so **"enemy morale broken" is itself an objective** |
| `minnationalmoralesurrender` | In `Regiment`: **units surrender in battle** when their alliance's national morale is at or below this |
| `demoralizevotersunionmoralelevel` | Election effect |

> `minnationalmoralesurrender` is a fourth politics→combat link, and the
> bluntest one: a politically collapsing nation's units stop fighting.

### The three continuous couplings

`BattleUnits.UpdateAllianceData` (~3860) runs three pairings every alliance
pass, for `alliancerunthrough <= 1` only:

```csharp
// 34 "Private Wealth" — a DERIVATIVE, not a level
value = (privateincome − lastmonthsprivateincome) × dt × convergencenatmoraletowardsprivatewealth;
if (lastmonthsprivateincome == 0f) value = 0f;
AdjustNationalMorale(34, value);

// 33 "State Support" — symmetric convergence
value = (averagesupport − nationalmorale) × dt × convergencenatmoraletowardsstatesupport;
AdjustNationalMorale(33, value);

// per state with nation.alliance == this side, reason 3 "National morale"
value = (nationalmorale − nation[i].support[side]) × dt × convergencestatesupporttowardsnatmorale;
nation[i].AdjustSupport(side, value, 3);

// per state, reason 4 "State occupation"
lostShare = (towns in state not owned by this side) / max(1, towns in state);
nation[i].AdjustSupport(side, statesupportlosttowns × dt × lostShare, 4);
```

| Pref | Value | Meaning |
| --- | --- | --- |
| `convergencenatmoraletowardsprivatewealth` | 10.0 | per annum, on the **month-over-month change** in average private income |
| `convergencenatmoraletowardsstatesupport` | 0.5 | per annum, toward `averagesupport` |
| `convergencestatesupporttowardsnatmorale` | 0.25 | per annum, toward national morale, loyal states only |
| `statesupportlosttowns` | −1.00 | per annum × the share of the state's towns in enemy hands |

Three things follow.

**Both convergences run in both directions.** High state support *raises*
national morale as surely as low support lowers it, and national morale pulls
every loyal state's support toward itself at half that rate. The chain is a
feedback loop, not a one-way penalty.

**Private wealth is a rate on a difference.** `lastmonthsprivateincome` is
re-snapshotted in `UpdateEndOfMonthFunctions`, so the term measures growth
*within the current month* and resets on the first. A nation with permanently
high but flat private wealth earns nothing from code 34; a poor nation whose
wealth is climbing earns it every tick. The `== 0f` guard suppresses the term
on the first month of a campaign.

**Losing towns is the heaviest single support term in the file.** At −1.00 per
annum scaled by the share of towns lost, a state whose cities are all taken
bleeds 100 points of support a year — against the +0.25/yr pull back toward
national morale. Occupation wins that race by four to one.

---

### 3a. Who writes codes 1, 2 and 3

findings/24 mapped codes 4–14, the objective block, and findings/22 mapped
5 and 20–31, the policy block. The three remaining live codes are the ones the
player generates by playing: **1 Battles, 2 Naval engagements, 3 Town
occupation.** None of them is written in this file's own subsystem.

#### The shipped starting values

`nationalmorale` is alliance record field 35, divided by 100 on read
([23](23-game-state.md) §3.1), so it is authored per campaign start:

| Start | Scenario name | Opening date | Union | CSA |
| --- | --- | --- | --- | --- |
| A | Spring 1861 | 23 Feb 1861 | **1.00** | **1.00** |
| B | Summer 1861 | 8 Jul 1861 | 0.95 | 0.95 |
| C | Spring 1862 | 21 Mar 1862 | 0.90 | 0.90 |
| D | Spring 1863 | 10 May 1863 | **0.82** | **0.85** |
| E | Spring 1864 | 1 May 1864 | 0.75 | 0.75 |
| F | Tutorial | 8 Jul 1861 | 0.95 | 0.95 |

Read from each `Campaigns/001/<letter>/nations.dat` at line
`1010 + 42 × alliance + 34`, against the 19-line state / 42-line alliance
record layout of [23](23-game-state.md) §3.1. D is the only asymmetric start.

> **A Spring 1861 campaign begins at the clamp**, and `AdjustNationalMorale`
> recomputes its delta *after* clamping, so every favourable term — the state
> support convergence, private wealth, captured forts — records **zero** until
> something has first pushed the number below 1.0.

#### Codes 1 and 2: battles, via the battle-result file

Neither side's morale is adjusted in the battle scene. Both sides' figures are
computed there, written to the battle-result file as a two-float array, and
applied when the campaign scene reads it back:

```csharp
// ImportExportUnitData ~3000, per side num26 ∈ {0,1}
if (monumentlist[n].landorsea == 0)
    alliance[monumentlist[n].alliance[num26]].AdjustNationalMorale(1, array8[num26]);
else if (!flag | !flag2)
    alliance[monumentlist[n].alliance[num26]].AdjustNationalMorale(2, array8[num26]);
```

**Both sides are charged, including the winner**, each for its own casualties.
There is no victory bonus in the national-morale path at all — the only
positive battle terms in the game are objectives 7, 8 and 18–23
([24](24-campaign-objectives.md) §6.1).

`flag` and `flag2` are set while reading the two unit lists if **any** fleet in
the battle had `fleetorders == 3` (Raid) or `== 1` (Blockade) respectively, so
`!flag | !flag2` suppresses the naval code whenever a raid met a blockade —
the engagement [26](26-naval-combat.md) §7.1 already treats as a skirmish
rather than a fleet action. The land code has no such exclusion.

**There are two implementations of the land formula**, and they differ.

```
// Autocalc.GetLossInfluenceOnNationalMorale(side, battleendtype)  ~3430
num  = (int)(totalstrength[side] + totallosses[side]);     // DEAD, debug log only
num2 = totallosses[side] ^ influenceonnationalmoralelossratefactor
       × influenceonnationalmoraleabsolutelossfactor
if this side retreated  (endtype 7 player / 2 enemy)  num2 *= …retreat
if this side was beaten (endtype 8 player / 0 enemy)  num2 *= …battlelost
return −num2
```

```
// BattleUnits.GetLossInfluenceOnNationalMorale(side)  ~6015
num  = sideinformation[side].totallosses ^ lossratefactor × absolutelossfactor
num2 = clamp(1 − usedcampaignbonusrunning × nationalmoralelossmodifierdifficulty,
             0.1, 1)
if VictoryBarBelowTrigger() == side:
        if side is not the player's   num *= num2        // AI discount
        num *= influenceonnationalmoralebattlelost
        if ltgenpolicy && c-in-c commands this side  num *= ltgenlossmorale
else if this side left the field:
        if side is not the player's   num *= num2        // AI discount
        num *= influenceonnationalmoraleretreat   (endtype 4/7 own, 2 enemy)
        num *= influenceonnationalmoralesurrender (endtype 5 own, 6 enemy)
return −num
```

| Pref | Value | Note |
| --- | --- | --- |
| `influenceonnationalmoralelossratefactor` | **0.3** | the exponent on absolute casualties |
| `influenceonnationalmoraleabsolutelossfactor` | 0.0004 | |
| `influenceonnationalmoralebattlelost` | 2.0 | driven from the field |
| `influenceonnationalmoraleretreat` | 1.5 | voluntary withdrawal |
| `influenceonnationalmoralesurrender` | **3.0** | **live version only** |
| `ltgenlossmorale` | 1.2 | **live version only** |
| `nationalmoralelossmodifierdifficulty` | 1.0 | **live version only**; × `usedcampaignbonusrunning` ∈ [0, 0.5] |
| `moraleinfluencenavaldefeats` | −0.00035 | per point of firepower destroyed |

Four findings follow.

**The exponent 0.3 makes the charge almost flat in casualties.** 250 losses
cost 0.21 points, 50,000 cost 1.03 — a 200× range in blood for 5× in morale.
Nothing is relative to army size: the one expression that computes starting
strength is consumed by the debug log.

**The auto-calculator is missing three of the live version's four
multipliers** — the surrender ×3, the Lieutenant General ×1.2 and the AI
difficulty discount. So auto-resolving and fighting the same battle produce
different political consequences, and in opposite directions depending on who
is losing: auto-resolve charges the AI at full price and charges the player
less for capitulating.

**The AI discount is sizeable where it applies.** At the maximum bonus of 0.5
the enemy's charge is halved, floored at ×0.1.

**Military experience is priced in destroyed national morale.** The same array
is reused immediately afterwards:
`militaryexperience += max(0, SumUp(array8) × milexpratio{victor,loser} × aiBonus)`
with `milexpratiovictor = −1.1` and `milexpratioloser = −0.7` — negative
coefficients on a negative array, so both sides gain in proportion to the
battle's **total** morale damage, the winner 1.57× the loser.

#### Code 3: town occupation

`Town.UpdateOwnership` (~287) is the only writer:

```csharp
if (((owner == playeralliance) | (Owner == playeralliance)) && owner != Owner) {
  if (frame >= 50 && owner >= 0) {
    if ((float)Owner != originalowner && originalowner >= 0f)
        alliance[owner].AdjustNationalMorale(3,
            influenceonnationalmoraletowns * GetImportance() / 10f);   // −0.125 × imp/10
    if ((float)Owner == originalowner)
        alliance[Owner].AdjustNationalMorale(3,
            −influenceonnationalmoraletowns * GetImportance() / 10f);
    if (originalowner < 0f) originalowner = Owner;
  }
}
```

```csharp
public int GetImportance()
  => clamp(ceil(privatewealth * representingpopulation
                / max(0.01f, topincome) * 10f), 0, 10);
```

The same method is also where two of [24](24-campaign-objectives.md) §12's
eleven unattributed war-progress statics are written, both guarded on
`Policy.CurrentChapter > 0` and on the town's state being loyal to the losing
side:

```csharp
if (owner == 0 && Owner == 1 && chapter > 0 && nation[state].alliance == 0)
    CampaignObjective.csacapturedtowns++;          // objective 12, "Capture 3 Cities"
if (owner == 1 && Owner == 0 && chapter > 0 && nation[state].alliance == 1)
    CampaignObjective.confederacyinvaded = true;   // objective 6, "Invade the Confederacy"
```

So **neither counter advances for a town in a border or occupied state**, and
neither is ever decremented on recapture — `csacapturedtowns` is a lifetime
count, the same shape as the recruiting pools
([23](23-game-state.md) §5.2).

Five properties, in rough order of how much they change play.

**Only the losing side is charged; conquest earns nothing.** The first branch
bills the previous owner. The second refunds the *original* owner on recapture.
A town that changes hands and comes back is morale-neutral for both sides over
the round trip, and the attacker's own morale is never touched in either
direction.

**`originalowner` is the campaign's opening ownership**, set by
`Town.UpdateInitialOwnerships` from `nation[state].alliance`, overwritten by
`cities.dat` special ownerships, and re-set whenever
`GameVars.SetNationToNewAlliance` hands a state over with `handovertowns`. So
the refund tracks the 1861 map, not the current front line.

**`topincome` is a high-water mark that never decays.** It is `topincometemp`,
which only ever takes a larger value and is never cleared between sweeps, so
importance is each town's income as a share of the richest town's *all-time
peak*. As the war impoverishes the country every town's importance ratchets
down, and `ceil` puts almost every town at 1 — a flat **−0.0125** — while only
the largest city in the game ever reaches 10, at **−0.125**.

**The whole block is gated on the player being party to the transfer.** A town
passing between two alliances that are both not the player — an intervention
force taking a Confederate port — moves no national morale at all. The gate
exists to decide whether to write a journal entry and a capture message; the
morale adjustment was placed inside it.

**A town whose `originalowner` is still −1 charges nobody** and then adopts its
*captor* as the original owner, so the defender's later recapture earns
nothing and its own subsequent loss is charged. No shipped town reaches
`UpdateOwnership` in that state, because initialization always sets the field;
it is a hazard for a mod that instantiates towns at runtime.

A second, unrelated defect sits ten lines below, in the DLC event hook:

```csharp
if (nation[state].slaveryallowed)      Events.CheckEvent(…EnemyStateSlavery);
else if (nation[state].slaveryallowed) Events.CheckEvent(…EnemyStateNonSlavery);
```

The `else if` repeats the `if`'s condition instead of negating it, in both the
capture and the recapture arm, so the two **non-slavery** capture events are
unreachable ([25](25-newspaper-and-events.md) covers the event catalogue).

#### What reads `nationalmorale`

Nine consumers, which is the whole set:

| Reader | Effect |
| --- | --- |
| `CampaignObjective` 0 / 1 | `< breakmoraletrigger` (**0.25**) → `SetEndOfCampaign`. Chapters 0–3, i.e. always live |
| `CampaignObjective` 26 / 27 | the 1864 election, `lincolnmaintainmoraleabove` / `demoralizevotersunionmoralelevel` = 0.50 ([24](24-campaign-objectives.md) §3.2) |
| `Regiment` ~13693 | routed units **evaluate surrender** — see below |
| `Regiment` ~6636 | desertions when a campaign formation disintegrates, interpolated `{0.5, 0.8}` at morale 0 → `{0.3, 0.5}` at 1 |
| `BattleUnits` ~3867 | → `averagesupport` convergence (code 33) and the reverse pull into each loyal state (support code 3) |
| `AICampaign.GetNationalMoraleFactor` | own ÷ enemy, **divides** the AI's threat estimate ([20](20-ai-campaign.md) §4.2) |
| `AICampaign` ~1911 | the AI only recruits in a state whose support ≥ **its own national morale** |
| `CampaignStrategy` ~469, ~551 | the two side figures, and the war bar |
| `CampaignTopPanel` ~1132 | the player's own figure, as an integer percentage |

The surrender rule is the bluntest politics→combat link in the game:

```csharp
// Regiment.Rout(), battle scene only (!calledfromcampaign)
if (nationalmorale <= minnationalmoralesurrender          // 0.4
 && sideinformation[side].averagemorale <= minavgarmymoralesurrender) {   // 0.5
    b = nationalmorale * sideinformation[side].averagemorale;
    b = 1f / Mathf.Pow(Mathf.Max(0.0001f, b), probsurrenderlowmoraleexp)  // 1.5
        * probsurrenderlowmoralelin;                                      // 0.01
    if (Random.Range(0f, 1f) < b) { Surrender(); return; }
}
```

So the probability is `0.01 / (nationalmorale × armymorale)^1.5`, evaluated once
per rout: **11 % at the gate (0.40 × 0.50), 37 % at 0.30 × 0.30, and certain
below a product of 0.046.** It is tested before the melee-outflanked and
surrender-counter paths, so a nation under 40 % morale loses routed brigades
outright rather than rallying them.

The war bar is worth one line of its own:

```csharp
NationalMoraleSlider.value = min(1, max(0, csa − 0.25)
                               / max(0.0001, (union − 0.25) + (csa − 0.25)));
```

— the Confederacy's distance above the loss threshold as a share of both
sides' distances above it, **not** a ratio of the two percentages. It is an
honest and more sensitive readout than the figures beside it.

#### The difficulty one-shot is discarded in campaign play

`GameVars.usedcampaignbonus` is the campaign-setup "bonus" slider, 0 to 0.5,
and [20](20-ai-campaign.md) §9.1 described it as adding flat national morale at
scenario load. **That is not where it is applied.** It is applied inside
`BattleUnits.import_battledata`, in the `!IsCampaign` branch — i.e. in the
battle scene:

```csharp
for (int j = 0; j < 2; j++) {
    …
    if (alliance[j] != GameVars.playeralliance) {
        sideinformation[j].militaryexp    *= 1f + GameVars.usedbonusbattles;
        sideinformation[j].fightingspirit *= 1f + GameVars.usedbonusbattles;
        GameVars.usedbonusbattles = 0f;
        GameVars.alliance[j].nationalmorale += GameVars.usedcampaignbonus;
        GameVars.alliance[j].volunteersadj  *= 1f + GameVars.usedcampaignbonus
                                                  / 100f * bonusvolunteersmodifier;
        GameVars.usedcampaignbonus = 0f;
    }
```

Three defects in six lines.

1. **In a campaign battle it is immediately overwritten.** `import_battledata`
   is called at `BattleUnits` ~1401, and ~1403 runs
   `ImportExportUnitData.OverwriteNationsData(…)` for operations 3, 4 and 8,
   which re-reads `nationalmorale`, `volunteersadj` **and
   `usedcampaignbonus`** from the save. So both effects are applied and
   discarded, every battle, for the whole campaign. The slider's live effects
   are the `…running` copy and `casualtiesmodifier`.
2. **The write is indexed by seat, not by alliance.** The guard reads
   `alliance[j]` (the side-to-alliance map) and the write reads
   `GameVars.alliance[j]` (the alliance array). `battledata.dat`'s side 0 is
   `callingunit.alliance`, the side that *initiated* the battle, so in any
   battle the AI started the bonus is written to the player's alliance.
3. **The volunteers line is scaled for a 0–50 slider.** The config label reads
   *"50 bonus and value of 0.5 here means 25% more volunteers"*, and
   `bonusvolunteersmodifier` ships at 1.0 — so the intent is +50 % at the
   maximum. With the shipped slider maximum of 0.5 the expression yields
   **+0.5 %**. Conversely `nationalmorale += 0.5` is scaled for a [0, 1] field
   and would be **+50 points**, unclamped, so the two uses of the same variable
   disagree by two orders of magnitude.

> The net effect is that the difficulty slider gives the AI no morale head
> start at all — which is the reason the +50 has never been noticed. A mod that
> moved the write into the campaign scene without rescaling it would pin one
> side at the ceiling for the whole war.

---

## 4. Foreign intervention

`Alliance.interventionlevels[]` — one accumulator per alliance **per foreign
power**, adjusted by `AdjustInterventionLevel(...)` and floored at 0.

```
GetInterventionProbability(ofalliance):
  if power 2 or 3:   p = alliance[1].level[power] − alliance[0].level[power]    // favours CSA
  if power 4:        p = alliance[0].level[power] − alliance[1].level[power]    // favours Union
  if power 2:        p /= max(1, higherinterventionlevels)                      // harder to draw in
```

Two hardcoded gates:

- **Power 3 requires a date and a policy**: zeroed before
  `Tools.Date(7, 6, 1863)` — 7 June 1863 — **and** unless policy **144** is
  active.
- **Power 4 requires policy 174.**

A DLC commander's nationality shifts the probability via
`DLC_WL.EffectMultiplier(34, …)`.

Finally the result is **chapter-gated**:

```
if (Policy.CurrentChapter == -1) return 0;
p = GamePrefs.relationsintervention[Policy.CurrentChapter] >= 100f
      ? 0
      : p / max(0.0001f, GamePrefs.relationsintervention[Policy.CurrentChapter]);
clamp [0, 1]
```

So `relationsintervention[]` is **per chapter**, and a value of `100` or more
makes intervention impossible in that chapter outright — the designers' switch
for "diplomacy is closed for now".

Intervention level is driven by a large family of policy prefs —
`kingcottonintervention`, `cottontradeintervention`, `abolitionintervention`,
`emancipationintervention`, `emancipationcsaintervention`,
`neutralityactintervention`, `legalblockadeintervention`,
`lettersmarqueintervention`, `freetradeintervention`,
`breadbasketintervention`, `feedeuropeintervention`, `burncottonintervention`,
`slaveryrestrictionintervention`, `agriculturalintervention`,
`mexicointervention`, `relationsintervention`,
`project_sendenvoysinterventionlevel` — plus battle victories
(`winbattleinterventionlevelunion` / `...csa`, applied from
`CampaignObjective`).

`pullbackinterventionarmies` and `pullbackinterventionfleets` govern
withdrawal; `AICampaign` checks the latter against
`fleetref.averagecondition`.

> **Diplomacy is almost entirely a policy game**, with a hardcoded historical
> date gate on one power. The `+100` policy-id offset convention
> ([09](09-policy-and-project-effects.md) §2) means policies 144 and 174 are
> in the second (Confederate) id block.

### 4a. Each accumulator is floored at zero

`AdjustInterventionLevel(foralliance, value, …)` clamps after every write:

```csharp
interventionlevels[foralliance] += value;
if (!clearvalues && policyid >= 0) alliance[foralliance].interventionpointspolicy[policyid] ±= value;
if (interventionlevels[foralliance] < 0f) interventionlevels[foralliance] = 0f;
```

The single-argument overload loops `for (int i = 2; i < alliance.Length; i++)`,
so a policy's relations effect applies to **every** foreign power at once, not
to a chosen one.

Two consequences:

- **Negative relations effects are silently discarded against the floor.** A
  side at zero loses nothing from `tariffactrelations` or
  `lettersmarqueintervention` (both −0.1), so the *order* in which acts are
  taken changes the outcome.
- **The per-policy breakdown is not clamped**, only `interventionlevels` is, so
  `interventionpointspolicy[]` can sum to more than the accumulator it
  explains — the same class of discrepancy as `supportinfluencelast` in §2,
  with a less visible symptom.

Starting values are per scenario, read from `nations.dat` as three-character
fields divided by 100 (`GameVars` ~7306). The shipped campaign:

| Scenario | CSA–GB | CSA–France | Union, all powers |
| --- | --- | --- | --- |
| A Spring 1861 | 0.10 | 0.20 | 0 |
| B Summer 1861 | 0.20 | 0.25 | 0 |
| C Spring 1862 | 0.25 | 0.30 | 0 |
| D Spring 1863 | 0.20 | 0.30 | 0 |
| E Spring 1864 | 0.20 | 0.30 | 0 |
| F Tutorial | 0.20 | 0.25 | 0 |

> **The Union's diplomacy is purely defensive.** It starts at zero with every
> power in every scenario, and its only lever on Britain and France is to raise
> its *own* accumulator, which subtracts from theirs. It can never push a power
> below neutral.

### 4b. There is no dice roll

`AICampaign.UpdateInterventions(alliance)` tests
`GetInterventionProbability(alliance) >= 1f && GameVars.interventions`, then
requires the matching `CampaignObjective.winmajorbattleintervention` and
`alliance[power].timesincewar <= 0f`, and declares war. **No `Random` call
appears in either function.** The name "probability" describes a normalised
difference, not a chance: when the displayed figure reaches 100, the next
qualifying major victory brings the power in deterministically.

`timesincewar` is set by `SetWarWithNation(n, isinwar: true)` and **never
cleared** by the `isinwar: false` path, so the `<= 0f` guard means each power
can intervene **exactly once per campaign**.

### 4c. Three sign and routing errors

- **The withdrawal penalty is charged to the wrong alliance.** Every pullback
  branch in `AICampaign` does
  `alliance[0].AdjustInterventionLevel(power, −100f)` for a power that was at
  war with alliance 0, and `alliance[1]...` for one at war with alliance 1 —
  that is, it wipes the accumulator of the power's *opponent*, which is the
  subtrahend in `GetInterventionProbability`. With the §4a floor the figure is
  normally already zero, so the penalty is inert; where it is not, it widens
  the difference. The patron's accumulator is never touched.
- **`winbattleinterventionlevelunion` has the wrong sign for its objective.**
  Objective 24 ("Discourage the Europeans") applies −0.10 to the **Union's
  own** accumulator, which lowers `P(Spain for Union)` and raises
  `P(Britain/France for CSA)`. Its mirror, objective 25, applies +0.25 to the
  Confederacy's own accumulator and does raise Confederate intervention. So
  both sides' major victories move Europe toward the Confederacy
  ([24](24-campaign-objectives.md) §4).
- **The gates run before the DLC adjustment.** `GetInterventionProbability`
  zeroes France for the date and policy-144 tests, then adds
  `DLC_WL.EffectMultiplier(34, 0)` for a matching chosen commander — so in a
  DLC scenario a commander's nationality can lift a gated-off power back above
  zero. *Untested:* the `DLC_WL` layer is unexamined
  ([01](01-assembly-partition.md)).

### 4d. What the player is shown

`CampaignStrategy.UpdateStats` fills the **European Relations** row from
`GetInterventionProbability`, not from the accumulators: the Union's figure is
`(4)` — Spain alone — and the Confederacy's is `max((2), (3))`. Both are
post-division and clamped to [0, 1], so in Chapter 0 (`relationsintervention[0]
= 100`) the row reads **0 for both sides whatever the accumulators hold**.

The monthly intelligence report (`BattleUnits` ~1890) has four defects in one
block:

1. the Confederate report's France line prints `interventionProbability`, the
   **Britain** value, instead of calling `GetInterventionProbability(3)`;
2. its France delta is `Britain_now − France_lastmonth`;
3. the Union report is headed with `alliance[4].name` (Spain) but computes
   `GetInterventionProbability(3)` (France);
4. `lastmonthinterventionprobability[4]` is written from
   `GetInterventionProbability(0)` (`BattleUnits` ~2030), which is identically
   0 for a belligerent, so the `> 0f` guard never passes and the Union's line
   never reports a monthly change.

---

## 5. Recruitment is proportional to state support

The largest practical consequence, in `Nation` (~line 4830):

```
// Drafts — a hard gate
if (support[i] >= statesupportrecruiting
    && (GetAmountOfTownsOccupied(i) > 0 || townsinstate.Count == 0))
    drafts[i] = floor(population × recruitratedraftsfullmobilization
                      × mobilizationrate[i] × draftspolicyadjrunning × dlc)
                − draftsrecruited[i];
else
    drafts[i] = 0;

// Volunteers — linear in support
s = support[i];
if (s < statesupportrecruiting && !usctException) s = 0;
if (Policy.CurrentChapter == 0 && nation.alliance != i) s = 0;   // pre-war: only your own states

f = population × recruitratevolunteersfullsupport × s × projectFactor × dlc;
if (f > 0) f *= adjvolpoolshighnumberslin / f ^ adjvolpoolshighnumbersexp;   // diminishing returns
volunteers[i] = floor(f) − volunteersrecruited[i];
```

`projectFactor` chains `recruitmentprojectfactor`, `volunteerspolicyadj`,
`volunteerspolicyadjrunning`, `volunteersadj`, and
`1 + GetProjectLevel(113) × project_farmmechanizationrecruitment`.

> **State support is the manpower tap.** Volunteers scale linearly with it and
> go to **zero** below `statesupportrecruiting`; drafts are gated outright. A
> politically hostile state contributes nothing, which is why the policy game
> matters mechanically and not just narratively.
>
> The diminishing-returns term means large pools are compressed — doubling
> population does not double volunteers.

**The constants, resolved** (see [23-game-state](23-game-state.md) §5.2):
`adjvolpoolshighnumberslin = 3`, `adjvolpoolshighnumbersexp = 0.1`, so the
pool is `3 × raw^0.9`. The two cross at `raw = 3¹⁰ = 59,049`: **below a raw
pool of ~59 k the "reduction" multiplies the pool up**, and only above it does
it compress. `recruitratevolunteersfullsupport = 0.016`,
`recruitratedraftsfullmobilization = 0.04`, `statesupportrecruiting = 0.3`.
So volunteers are linear in support only through the inner term; the pool as a
whole is a power law.

### Hardcoded state names

```csharp
if (((name == "Illinois") | (name == "Ohio") | (name == "Indiana")
   | (name == "Iowa") | (name == "Kansas")) && Policy.GetStatusByPolicyID(30))
    recruitmentprojectfactor *= GamePrefs.northernroutesvolunteersmodifier;
```

> **Policy 30 keys off five state *names* as strings.** Renaming a state in
> config silently disables the effect. Policy 33 (USCT) similarly inspects
> `slaveryallowed` and town ownership to grant a support-floor exception.
>
> A clear case for the ModApi to expose states by stable id, and a concrete
> example of the "hardcoded effects" risk being worse than it looks — this one
> is coupled to *localisable text*.

### The shipped starting data

All six authored campaign starts ship `mobilizationrate[] = {0, 0}` for
**every one of the 53 states**, so the draft ceiling is identically zero until
a policy raises it (§5c). The Spring 1863 and Spring 1864 starts pre-research
policy 39/139, which supplies the 0.5 on the first effects pass; the Union
never starts with policy 40.

Starting support, Spring 1861 (`Campaigns/001/A/nations.dat`, percent, Union /
Confederate):

| State | Sup | State | Sup | State | Sup |
| --- | --- | --- | --- | --- | --- |
| South Carolina | 0/100 | Virginia | 25/85 | Kansas | 60/35 |
| Georgia | 5/95 | Arkansas | 25/75 | Oregon | 70/5 |
| Florida | 5/95 | Dakota Terr. | 30/30 | West Virginia | 70/40 |
| Louisiana | 5/95 | Tennessee | 35/75 | Delaware | 75/25 |
| Texas | 4/96 | Arizona Terr. | 35/60 | Maryland | 75/25 |
| Alabama | 10/90 | Indian Terr. | 35/50 | Illinois | 80/20 |
| Mississippi | 15/90 | New Mexico | 40/60 | Ohio | 82/18 |
| North Carolina | 20/80 | Kentucky | 60/40 | Massachusetts | 99/1 |
| Utah Terr. | 20/20 | Missouri | 60/45 | D.C. | 100/0 |

Several pairs sum to more than 100 — Tennessee 110, Virginia 110, West
Virginia 110, Missouri 105 — which is legal because the two figures are
independent (§2), and which is what makes those states contested rather than
merely divided. The 1863 and 1864 starts ship materially lower Union figures in
the loyal states (New York 70 rather than 90).

`contractlength` is authored per state (3 everywhere) and **never read** — the
unit takes `Alliance.contractlength` at creation. `volunteersadj` is authored
per alliance and is 1 for all eight.

---

## 5b. Contracts, expiry and reenlistment

Every `unittyp <= 13` unit carries `contractdate` (its enlistment date) and
`contractlength` in months, assigned at creation from
`GameVars.alliance[a].contractlength`. `Regiment.CheckContractExpiries()`, on
the slow frame-paced cycle and **skipped while `inbattle`**, walks
`allattachedunits` and acts when `contractdate + contractlength` has passed:

```
r = nation[state].GetReenlistmentRatio(alliance)
  = reenlistmentscontractexpiredsupport1(0.60) × support
  + reenlistmentscontractexpiredsupport0(0.10) × (1 − support)
  , then × (1 ± reenlistmentscontractexpiredvariation(0.2)), clamped [0,1]

leaving      = floor(strength × (1−r)) + floor(losses[0] × (1−r))
strength     = floor(strength × r)
losses[0]    = floor(losses[0] × r)                  // the wounded pool shrinks too
weaponstock += the leavers' weapons (statsguns for artillery)
contractdate = today;  contractlength = alliance.contractlength
```

So the ratio runs **0.10 at zero support to 0.60 at full**, ±20 % relative.
Three consequences:

- **The leavers are not credited back to the recruiting pool.**
  `AdjustRecruitingPool` is not called here, so `volunteersrecruited` stays
  charged: expiry destroys manpower on both sides of the ledger
  ([23](23-game-state.md) §5.2). Their *weapons* do come back.
- **The wounded go with them.** `losses[0]` is scaled by the same ratio, so a
  contract expiry silently disposes of the unit's disabled men as well.
- **Contracts cannot expire mid-battle** (`inbattle` is tested), so the roll
  lands on the campaign map.

**`averagereenlistmentratioexp_tmp` is assigned, not accumulated** — the same
shape as `powratio`'s denominator ([23](23-game-state.md) §6.2). Whichever unit
most recently expired sets the figure, which `UpdateAllianceData` ~3718 then
feeds into a trailing average (seeded at 0.5 if zero) as
`Alliance.averagereenlistmentratio`. That is what `CampaignTopPanel` ~1170
prints as *"Estimated reenlistment ratio"* and what
`Regiment.GetContractExpiries(timeframe = 0.25f)` uses to forecast the men
whose contracts lapse in the next quarter — a forecast built on one unit's
roll.

The Draft Acts' effect on contracts is **asymmetric**:

| | Union (policies 39, 40) | Confederate (139, 140) |
| --- | --- | --- |
| Resets every unit's `contractdate` to today | ✔ | ✔ |
| Raises every unit's `contractlength` | — | ✔ to 36, then 99 months |
| Sets the alliance's default `contractlength` | — | ✔ |
| Pref actually read | — | `draftresetcontractlengthcsa` {36, 99} |

`draftresetcontractlength` — the Union's own {36, 36} — is **parsed and never
read**: both writes sit inside `if (alliance == 1)`. A Union Draft Act therefore
buys a restart of the current term and no extension, and the Confederacy's
second Act, at 99 months, is for the duration of the war. Each Act also costs
`draftnationalmorale` = **−0.02** national morale, once.

---

## 5c. The Act ladders, and what Militia Act I switches on

Militia Act I (policy 36 Union, 136 Confederate) is the start of the war. Its
one-shot block is guarded on `uniquePolicyID == 36` — **the Union's version
only** — so the cascade in §5a, the fort handover, `SetWarWithNation(1)` and
the recruitable unlocks all wait for the Union to pass it:

```csharp
GameVars.nation[2].recruitable = true;   // Arkansas
… 10, 13, 14, 22, 28, 34, 38, 39, 50    // Indian Terr., Kansas, Kentucky, Missouri,
                                         // N. Carolina, Tennessee, Virginia,
                                         // W. Virginia, New Mexico Terr.
```

`recruitable` is written **nowhere else in the assembly**, and never set false.
Seven states ship unrecruitable and stay that way for the whole game:
California, Oregon, Nevada, Utah Territory, Dakota Territory, Nebraska
Territory and the Unorganized Territory. The flag gates `RecruitingList` ~116
and `BattleUnits.GetBestRecruitingState`, not `UpdateRecruitData`, so an
unrecruitable state still *computes* pools — and a brigade's replacement draw
([14](14-supply-and-logistics.md) §3b) never consults it.

The two ladders, with the shipped values:

| Act | `militiaactcontract` | `volunteerspolicyadj` += | `volunteerspolicyadjrunning` += p.a. |
| --- | --- | --- | --- |
| Militia I, Union | **3 months** | 0.08 | 0.50 × contractFactor |
| Militia II, Union | 12 | 0.15 | 0.15 × contractFactor |
| Militia III, Union | 24 | 0.12 | 0.15 × contractFactor |
| Militia IV, Union | 36 | 0.10 | 0.15 × contractFactor |
| Militia I, CSA | 12 | 0.25 | 0.50 × contractFactor |
| Militia II, CSA | 24 | 0.20 | 0.15 × contractFactor |
| Militia III, CSA | 36 | 0.10 | 0.15 × contractFactor |

with `premilitiaactvolunteersfact` = **0.15** as the pre-war level that
`UpdatePolicyEffects` resets `volunteerspolicyadj` to on every pass, and

```csharp
contractFactor = Tools.Interpolate([3, 12, 24, 36], alliance.contractlength,
                                   recruitsgrowthreductioncontract);
// recruitsgrowthreductioncontract = {1.0, 0.9, 0.6, 0.3}
```

Two structural points. **`volunteerspolicyadj` is a level** (reset and rebuilt
each pass, so it tracks the Acts you hold), while
**`volunteerspolicyadjrunning` is an unreset integral** that starts at 1.0 and
grows for as long as the Acts are active — it is the only term in the
recruiting formulas that rewards simply having passed a policy early. And the
contract factor throttles that growth: a Union at 3-month contracts accrues
0.50/yr, the same Union with all four Acts and 36-month contracts accrues
`0.95 × 0.3 = 0.285`/yr. **Short contracts grow the pool fastest and churn
units hardest.**

The Draft Acts (39/40 Union, 139/140 Confederate) are the draft-side ladder:

| Term | Pref | Value |
| --- | --- | --- |
| Mobilization rate, added to **every** state | `draftmobilization` | +0.5, then +0.3 |
| Volunteer level | `draftsvolunteersfactor` | +0.05, +0.025 |
| Draft growth integral | `draftvolunteersrunning` → `draftspolicyadjrunning` | +0.15, +0.15 per annum |
| National morale | `draftnationalmorale` | −0.02 each, once |
| Replacement speed | `draftactreplacementspeed` | ×1.5, then ×2.0 (not cumulative) |
| Contract reset | §5b | asymmetric |

`draftspolicyadjrunning` starts at 1.0 and, like its volunteer twin, is never
reset — so four years with both Acts active puts it at 2.2 and a late-war draft
ceiling at roughly **227,000 men for New York alone**. The draft is not limited
by its ceiling; it is limited by `influenceonsupportifdrafted`.

Two more writers of `mobilizationrate` exist: Emancipation on the Confederate
side (`emancipationcsadrafts`, every state) and policy 144 Mexico, which raises
**Texas** alone by `mexicoavailablerecruits` and in the same breath charges
half of Texas's remaining volunteer pool:

```csharp
GameVars.nation[35].volunteersrecruited[alliance]
    += Mathf.FloorToInt(Mathf.Max(0, nation[35].volunteers[alliance]
                                   − nation[35].volunteersrecruited[alliance]) * 0.5f);
```

`volunteers[]` is *already* `ceiling − volunteersrecruited`, so the second
subtraction double-counts and the charge is half of
`remaining − recruited` rather than half of the remainder. It is also the only
place in the assembly that bills a pool without raising a unit.

### The recruitment subsidy does not buy recruits

`campaignprefs.txt` ships `additionalrecruitssubsidies` = **2** under the label
*"Additional recruits (volunteers) available for full recruitment subsidies
(1 = no change)"*, alongside `timetoreachrecruitstarget` = 2 campaign years.
`Alliance.UpdateSubsidyRecruitment(dt)` uses them like this:

```csharp
float step = GamePrefs.additionalrecruitssubsidies * timediff
           / GamePrefs.timetoreachrecruitstarget;
recruitmentprojectfactortarget = 1f + GetProjectLevel(109) * GamePrefs.project_recruitment;
// then recruitmentprojectfactor steps toward the target by ±step
```

The pref that the label makes the *target* is used as the **slew rate**, and
the target itself has no subsidy term — only project 109 Recruitment Offices at
+5 % per level. `GetSubsidyFactor(subsidy_military)` has no consumer at all
beyond cost accounting ([23](23-game-state.md) §7.3), so the Military subsidy
reaches recruitment only by funding that project.

---

## 5a. A state's loyalty is decided once, on the day the war starts

`Nation.alliance` is written outside loading by exactly three kinds of caller:

| Caller | What it does |
| --- | --- |
| `Policies.LaunchPolicyEffect` case 136 | the **one-shot secession cascade**, run on the first activation of Militia Act I |
| `Policies` cases 170–179 | pre-war policies placing Kansas, Arizona/New Mexico, Cuba and Indian Territory |
| `CampaignObjective` 14, 22, 39 | the scripted secessions of **Kentucky**, **Missouri** and **West Virginia** |

The cascade is a bare majority test, evaluated once:

```csharp
for (int i = 0; i < GameVars.nation.Length; i++)
    if ((nation[i].alliance == 0) | (nation[i].alliance == 1))
        GameVars.SetNationToNewAlliance(i, nation[i].support[0] > nation[i].support[1] ? 0 : 1);
```

No threshold, no neutral band, and `>` rather than `>=`, so **a tie goes to the
Confederacy**. It runs in the same call that applies Militia Act I's support
effects, so the Union's pre-Sumter `militiaact1unionbeforesumter = +0.10` to
every slave state lands *before* the comparison.

> **Nothing afterwards revisits it.** Support can fall to zero, every city in a
> state can be captured, and `Nation.alliance` does not move — which is exactly
> what the manual promises ("States with low support will not change loyalty
> even if they are completely controlled by the enemy"), and stronger: there is
> no mechanism at all, not merely a high bar. The three scripted objectives and
> the pre-war policies are the only exceptions
> ([24](24-campaign-objectives.md) §7).

---

## 6. The war balance is computed from UI sliders

`CampaignStrategy.GetBalance(bool updatestats = true)` finds
`GameObject.Find("UI/Strategy")`, optionally calls `UpdateStats()`, then reads
**ten `Slider.value` fields**:

`NationalSupportSlider`, `ArmyMoraleSlider`, `LoyalStatesSlider`,
`MenFieldedSlider`, `NavyTonnageSlider`, `MilitaryExperienceSlider`,
`BattlesWonSlider`, `TotalCasualtiesSlider`, `TradeWarfareSlider`,
`WeaponsIndustrySlider`.

```
mean = average of the ten slider values
w    = |nationalMoraleSlider − 0.5| × 2        // 0 when even, 1 at the extremes
balance = w × nationalMoraleSlider + (1 − w) × mean
```

A neat blend: near parity the balance is the mean of ten factors; as national
morale polarises, the balance increasingly *is* national morale.

> **But the strategic assessment is derived from UI widgets, not from the
> model.** It needs the `UI/Strategy` object to exist, and anything reading it
> headlessly — an AI, a mod, a test harness — gets whatever the sliders last
> displayed. This is the clearest instance so far of the
> simulation/UI entanglement noted in
> [02-type-inventory](02-type-inventory.md).

`GetBalanceDescription()` buckets the result into twelve bands from "total
dominance" to "Evenly matched". **The band `0.8333 ≤ balance < 0.9167` is not
covered** — the chain tests `>= 0.75 && < 5f/6f`, then jumps to `>= 11f/12f`,
so that range returns an empty string.

### What feeds two of those sliders

`UpdateStats` computes the ten displayed figures first, and two are worth
pinning down because the panel's labels mislead.

**Loyal States is a share, not a level** (`BattleUnits` ~3823):

```csharp
float b = Tools.SumUp(nation[i].support);                       // both sides' support
if (nation[i].support[side] / Mathf.Max(1E-05f, b) > GamePrefs.statesupportownstate)
    alliance[side].loyalstates++;
```

With `statesupportownstate = 0.65`, a state at 20/5 counts as loyal to the
first side (0.80) while a state at 70/40 counts for **neither** (0.636). The
counter is reset to 0 at the top of the pass, so it does not accumulate.

> **The same pref is read as an absolute support level elsewhere** — the
> recruiting list's "Own states"/"Enemy states"/"Neutral states" filters
> (`RecruitingList` 141–151), `AICampaign` 1817 and 3809, and `Policies` 1902
> all compare `support[side]` directly against `statesupportownstate` or
> `statesupportenemystate` (0.35). So a state can be *loyal* on the Strategy
> panel and *neutral* in the recruitment list at the same time, from one
> number.

**European Relations is the intervention probability**, not a relations
balance, and the Union's figure is Spain alone — see §4d.

---

## 7. Objectives drive both variables

**Now written up in full: [24-campaign-objectives](24-campaign-objectives.md).**
One correction to what this section said originally: `CampaignObjective` is
**not** prefab-authored like `Policy` — it is data-driven like a project, one
prefab instantiated once per record from `Config/campaignobjectives.dat`
(38 records, 20 lines each). The fields are `validfromdate`/`validtodate`,
`ObjectiveChapters`, `ObjectiveScenario`, `precampaignobjectives`
prerequisites, `influenceprestige`, and a `List<object> objectives` of towns
and IIPs resolved by name.

Progress is tracked in static fields on the class itself:
`confederacyinvaded`, `unionfirstvictory`, `csafirstvictory`,
`unioncapturedforts`, `csacapturedtowns`, `csavictoryenemysoil`,
`consecutivevictories[]`, `winmajorbattleintervention`, `victoryagainstgb`,
`victoryagainstfr`, `unioncasualties`.

> `victoryagainstgb` and `victoryagainstfr` name two of the foreign powers
> directly in code, which is the strongest available hint at the identities
> left open in §1 — but the field names are not the alliance names, so this
> file still does not assert the mapping.

`CheckAccomplishmentOfAllObjectives()` is driven from the campaign data tick
([10-simulation-tick](10-simulation-tick.md) §4), gated on
`GameVars.frame >= 50`.

---

## 8. `GamePrefs` keys

**Support:** `statesupportrecruiting`, `statesupportinfluenceraiding`,
`statesupportinfluenceraidingopposite`, `supportinfluenceenemysoil`,
`supportinfluenceprintingworkslin`, `supportinfluenceprintingworksexp`,
`inflcasualtiesonstatesupport`, `inflwoundedtreatedstatesupport`,
`desertionfactorstatesupport`, `statesupportownstate` (0.65),
`statesupportenemystate` (0.35), `statesupportlosttowns` (−1.00),
`convergencestatesupporttowardsnatmorale` (0.25),
`adjustmentmovingspeedsupport0` (**1.0, inert**), and the Militia Act family
`militiaact1unionbeforesumter`, `militiaact1unionbeforecsasupport`,
`militiaact1unionaftercsasupport`, `militiaact1csabeforeunionsupport`.

**National morale:** `breakmoraletrigger`, `minnationalmoralesurrender`,
`demoralizevotersunionmoralelevel`, `demoralizevotersunionmoraleinfluence`,
`lincolnmaintainmoraleabove`, `lincolninfluenceunionmorale`,
`republicanmajoritysupportlevel`, `republicanmajorityunionmorale`,
`convergencenatmoraletowardsstatesupport` (0.5),
`convergencenatmoraletowardsprivatewealth` (10.0),
`militiaact1csabeforeunionnationalmorale`,
`militiaact1unionaftercsanationalmorale`.

**Battles, towns and surrender (§3a):** `influenceonnationalmoraletowns`
(−0.125), `influenceonnationalmoralelossratefactor` (0.3),
`influenceonnationalmoraleabsolutelossfactor` (0.0004),
`influenceonnationalmoralebattlelost` (2.0),
`influenceonnationalmoraleretreat` (1.5),
`influenceonnationalmoralesurrender` (3.0, live path only),
`ltgenlossmorale` (1.2, live path only),
`nationalmoralelossmodifierdifficulty` (1.0, live path only),
`moraleinfluencenavaldefeats` (−0.00035), `minavgarmymoralesurrender` (0.5),
`probsurrenderlowmoralelin` (0.01), `probsurrenderlowmoraleexp` (1.5),
`desertionsdisintegrationnatmorale0` {0.5, 0.8} /
`desertionsdisintegrationnatmorale1` {0.3, 0.5}, `milexpratiovictor` (−1.1),
`milexpratioloser` (−0.7), `bonusvolunteersmodifier` (1.0),
`abolitionunionmorale` (+0.05) / `abolitioncsamorale` (**+0.10**),
`newspaperminornatmoraleloss` (−1.0) / `newspapermediocrenatmoraleloss`
(−2.0, both unreachable — see
[ui-vs-simulation](../mechanics-findings/ui-vs-simulation.md) §2.2), and
`industrialpolicynationalmorale`, shipped at **0** with no writer for
national-morale code 32.

**Intervention:** `higherinterventionlevels`, `interventionlevelincrease`,
`winbattleinterventionlevelunion`, `winbattleinterventionlevelcsa`,
`pullbackinterventionarmies`, `pullbackinterventionfleets`,
`project_sendenvoysinterventionlevel`, and the ~16 per-policy
`*intervention` prefs listed in §4. `interventionlevelincrease` (0.5) is
**parsed and never read** ([18](18-economy.md) §4), as are
`lettersmarquesubsidies` and `legalblockadetradewarsubsidies`.

**Recruitment:** `recruitratevolunteersfullsupport`,
`recruitratedraftsfullmobilization`, `adjvolpoolshighnumberslin`,
`adjvolpoolshighnumbersexp`, `northernroutesvolunteersmodifier`,
`usctmobilizationrateadj`, `project_farmmechanizationrecruitment`,
`influenceonsupportifdrafted` (−0.000016 per man),
`premilitiaactvolunteersfact` (0.15), `militiaactcontract` {3, 12, 24, 36},
`militiaactvolunteersfact{union,csa}`, `militiaactvolunteersfact{union,csa}running`,
`recruitsgrowthreductioncontract` {1.0, 0.9, 0.6, 0.3}, `draftmobilization`
{0.5, 0.3}, `draftsvolunteersfactor`, `draftvolunteersrunning`,
`draftnationalmorale` (−0.02), `draftresetcontractlengthcsa` {36, 99},
`draftresetcontractlength` (**parsed, never read**), `emancipationcsadrafts`,
`mexicoavailablerecruits`, `additionalrecruitssubsidies` (**mis-wired, §5c**),
`timetoreachrecruitstarget`, `project_recruitment`.

**Contracts:** `reenlistmentscontractexpiredsupport0` (0.10),
`reenlistmentscontractexpiredsupport1` (0.60),
`reenlistmentscontractexpiredvariation` (0.2).

**Replacements** are in [14](14-supply-and-logistics.md) §3b.

---

## 9. Open questions

### Answered since

1. ~~**The president system.**~~ **The president is cosmetic.**
   `UpdatePresident()` picks the last prefab-authored entry whose date has
   passed and writes a name label, a title label, an inauguration line and a
   portrait. Nothing mechanical reads `currentpresident`, and
   `LoadCampaignScenario`'s `chosenpresident` parameter is **accepted and never
   read** — so it does not seed the policy set either. Elections remain a
   campaign objective that does not change the president.
   ([19](19-commanders-and-perks.md) §8)
2. ~~**`supportthroughappointedcommanders[]` / reason 38.**~~ The reason code is
   **2, "Commander appointments"**; it fires only for `political` commanders
   holding a command, as
   `0.005 × fameMultiplier(1.0→3.5) × echelonMultiplier`. Code 38,
   "Commander", is **DLC-only**. ([19](19-commanders-and-perks.md) §9)
3. ~~**`printingworks[]` and `militaryschools[]`.**~~ Both exist at **two
   levels**, per-state and per-alliance, and all four accessors are power laws
   `value ^ exp × lin`:
   - `Nation.printingworks[alliance]` → draft support;
     `Alliance.printingworksvalue` → **commander fame from battles** and
     intelligence gathering.
   - `Nation.militaryschools[alliance]` → reduces the experience cost of
     promotion **and is the only source of leadership, administration and
     cunning growth in the game**; `Alliance.militaryschools` → national
     military experience, which caps commander `exp`.
     ([19](19-commanders-and-perks.md) §2.3, §4.1)
4. ~~**`Alliance.credibility`.**~~ `(ratingnotches.Length − currentrating) × 5`
   — an inverted credit-rating notch. ([18](18-economy.md))

### Still open

1. ~~**`AddTradeWarfare(int type, float value)`**~~ **Resolved:** a 10-slot
   accumulator with slot 0 recomputed as the total of 1–9, no clamping at all,
   and two orphaned statements (`Mathf.Abs(value);`, `_ = 1000000f;`) where a
   magnitude guard used to be. Same idiom as `AdjustSupport` and
   `AdjustNationalMorale`. ([23](23-game-state.md) §8)
2. **POW camps** — ~~support reason 33~~ **resolved, and the answer is
   negative:** nothing in the assembly ever writes reason code 33, so
   "Condition of enemy POW camps" is a breakdown label with no mechanic behind
   it ([22](22-policy-and-project-catalogue.md) §8.1). The camp's own condition
   value and its "overcrowded" message are still unexamined, and commanders
   captured in melee do become POWs ([19](19-commanders-and-perks.md) §5.1), so
   there is a mechanic there — it just does not touch state support.
   `Alliance.powratio` is now mapped: total prisoners over camp capacity,
   computed once per alliance pass — and its denominator **assigns** instead of
   accumulating, so only the last camp's capacity counts
   ([23](23-game-state.md) §6.2).
3. ~~**Which policies map to which support/national-morale reason codes.**~~
   **Done** — support codes 9–31 and national-morale codes 5 and 20–31 are
   mapped to their writing policies in
   [22](22-policy-and-project-catalogue.md) §8. Support code 9 and
   national-morale code 32 turn out to have no writer at all.
4. **Whether `adjustmentmovingspeedsupport0 = 1.0` is a disabled feature or a
   typo.** The config label declares the range `[<1]` and the manual documents
   the effect in prose (§2); the shipped value makes the blend an identity. A
   mod can switch it on by lowering the number, which makes this the cheapest
   restorable mechanic found so far.
5. **Whether `averagesupport`'s keying on town ownership rather than
   `Nation.alliance` is intended** (§2). It makes conquest slow your own policy
   research, which is defensible as design and indistinguishable from a
   loyalty-vs-ownership mix-up.
6. **Whether objective 24's `winbattleinterventionlevelunion = −0.10` is a sign
   error** (§4c). Under the shipped sign, both sides' major victories move
   Europe toward the Confederacy.
7. **Why the pullback penalty is charged to the intervening power's opponent**
   (§4c). The one-shot `timesincewar` guard hides it, so a fix would be
   invisible unless that guard is also changed.
8. **The `DLC_WL` intervention terms.** `EffectMultiplier(34, 0)` is added
   after the date and policy gates, and `DLC_WL` also calls
   `AdjustInterventionLevel` on all three powers directly (~3504–3512). The
   layer is unexamined.
9. **Whether the Union's missing contract extension is deliberate.** Both
   writes of `draftresetcontractlength` sit inside `if (alliance == 1)` and the
   Union array ships {36, 36}, so a Union Draft Act resets the clock without
   lengthening the term (§5b). The Confederate asymmetry is large enough —
   36 then 99 months — that it could be intended balance or a copy-paste that
   lost its Union branch.
10. **Whether `additionalrecruitssubsidies` was meant to be the target rather
    than the slew rate** (§5c). The label and the shipped 2 both point at a
    doubling of volunteers under full recruitment subsidies; the code uses it
    only to pace the project-driven factor. A one-line fix either way, and it
    would make the Military subsidy worth buying for its own sake.
11. **Whether policy 144's double subtraction on Texas's volunteer pool is a
    bug or a flat 50 % charge that happens to read oddly** (§5c). It is the
    only site that bills a recruiting pool without raising a unit.
12. **What a pardoned prisoner is supposed to do.** The release path charges
    the released men against their home states' *volunteer* pools, scaled by
    the reenlistment ratio, and no unit gains strength
    ([23](23-game-state.md) §5.2). Either the sign is inverted or the strength
    half of the feature was never written.
13. **Whether the two uses of `usedcampaignbonus` were written against
    different scales, and whether the campaign-path overwrite is intentional**
    (§3a). The volunteers line divides by 100 and the morale line does not; the
    config label is written for a 0–50 slider and the shipped maximum is 0.5.
    Either the slider range or both call sites are wrong, and as shipped both
    effects are discarded in campaign play, which hides the question.
14. **Whether taking a town should credit the captor** (§3a). Only the former
    owner is charged, so national morale has no conquest term at all outside
    the scripted objectives — defensible as design, but the config label
    ("influence on national morale when enemy towns are occupied") reads as
    though it should apply to the occupier.
15. **Whether the auto-calculator's three missing multipliers are deliberate**
    (§3a). Dropping the surrender ×3 and the Lieutenant General ×1.2 could be
    simplification; dropping the AI's difficulty discount changes the balance
    of a campaign depending on how the player resolves battles.
16. **Why `Town.topincometemp` is never reset** (§3a). As a high-water mark it
    makes every town's importance — and therefore the morale cost of losing
    it — fall monotonically over a campaign. A per-sweep reset is a one-line
    change and would make late-war cities matter as much as early-war ones.
