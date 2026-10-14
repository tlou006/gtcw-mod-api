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
| Volunteer pool | **Linearly proportional** (§5) |
| Draft pool | Hard gate at `statesupportrecruiting` (§5) |
| National morale | Reason code 33, "State Support" (§3) |
| `GetBalance` | Via the `LoyalStatesSlider` and `NationalSupportSlider` (§6) |

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

---

## 7. Objectives drive both variables

`CampaignObjective` is prefab-authored like `Policy`
([09](09-policy-and-project-effects.md)), with `validfromdate`/`validtodate`,
`precampaignobjectives` prerequisites, `influenceprestige`, and a
`List<object> objectives` of conditions.

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
`desertionfactorstatesupport`.

**National morale:** `breakmoraletrigger`, `minnationalmoralesurrender`,
`demoralizevotersunionmoralelevel`.

**Intervention:** `higherinterventionlevels`, `interventionlevelincrease`,
`winbattleinterventionlevelunion`, `winbattleinterventionlevelcsa`,
`pullbackinterventionarmies`, `pullbackinterventionfleets`,
`project_sendenvoysinterventionlevel`, and the ~16 per-policy
`*intervention` prefs listed in §4.

**Recruitment:** `recruitratevolunteersfullsupport`,
`recruitratedraftsfullmobilization`, `adjvolpoolshighnumberslin`,
`adjvolpoolshighnumbersexp`, `northernroutesvolunteersmodifier`,
`usctmobilizationrateadj`, `project_farmmechanizationrecruitment`.

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

1. **`AddTradeWarfare(int type, float value)`** has its own accounting and
   feeds the `TradeWarfareSlider`. Belongs with the economy.
2. **POW camps** — ~~support reason 33~~ **resolved, and the answer is
   negative:** nothing in the assembly ever writes reason code 33, so
   "Condition of enemy POW camps" is a breakdown label with no mechanic behind
   it ([22](22-policy-and-project-catalogue.md) §8.1). The camp's own condition
   value and its "overcrowded" message are still unexamined, and commanders
   captured in melee do become POWs ([19](19-commanders-and-perks.md) §5.1), so
   there is a mechanic there — it just does not touch state support.
3. ~~**Which policies map to which support/national-morale reason codes.**~~
   **Done** — support codes 9–31 and national-morale codes 5 and 20–31 are
   mapped to their writing policies in
   [22](22-policy-and-project-catalogue.md) §8. Support code 9 and
   national-morale code 32 turn out to have no writer at all.
