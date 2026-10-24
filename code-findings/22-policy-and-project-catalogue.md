# 22 — The policy and project catalogue

**Build:** see [00-build-provenance](00-build-provenance.md)
**Source:** `Assembly-CSharp.dll` → `Policies` (1,926 lines), `Policy` (662),
`Projects` (1,042), `GameVars.Alliance`, `GameVars.Nation`, `GamePrefs`;
`Config/policies.dat`, `Config/projects.dat`, `Config/campaignprefs.txt`,
`Campaigns/<id>/<set>/nations.dat`, `Campaigns/<id>/<set>/aipersonalities.dat`
**Summary:** [summary-policies-and-projects](summary-policies-and-projects.md)

This is the id → effect catalogue that [09-policy-and-project-effects](09-policy-and-project-effects.md)
promised. That file described the *dispatch sites*; this one enumerates what
they dispatch to, and corrects one assumption made there (§3.1).

---

## 1. Two systems, two architectures

They look like one feature in the UI and are built completely differently.

| | Policies | Projects |
| --- | --- | --- |
| Definition | a **`Policy` MonoBehaviour per id**, authored in the Unity inspector | a record in `Config/projects.dat` |
| Config file | `policies.dat` **overrides** prefab fields, matched by id | `projects.dat` **is** the definition |
| Can a mod add one by editing config? | **No** — unmatched ids are skipped | **Yes** — and 56 `PLACEHOLDER` slots are reserved |
| Cost | research time, paid in game-years | subsidy money, paid per level |
| Repeatable | no (an act fires once; a ladder policy has fixed levels) | `repeating` flag; level = occurrence count |
| Effects | one 1,200-line `switch`, **replayed every tick** | three separate mechanisms (§8) |
| Count | **117** | **125** (69 real, 56 placeholders) |

### 1.1 `policies.dat` cannot introduce a policy

`Policy.ImportPolicyData()` (~579) reads a count, then per record an id, then
`GetPolicyFromID(id)`:

```csharp
Policy policyFromID = GetPolicyFromID(id);
if (policyFromID != null) { /* overwrite every field */ }
else { /* read and discard the same 22 lines */ }
```

So the file is a **data override for prefabs that already exist**. A new id is
parsed and thrown away. `ImportPolicyData` is called from
`Policy.InitializeStatics`, which first collects every `Policy` component under
the `UI` GameObject — so **the set of policies is whatever is in the scene**,
and the file only reshapes it. Projects have no such constraint. §15 works out
what that means for the Phase 3 gating question in
[09](09-policy-and-project-effects.md).

`ImportPolicyData` also contains a stray `_ = 179;` immediately after reading
the id — a discarded literal that happens to be the last Confederate policy id.

**Record shape — 23 lines**, verified `1 + 117 × 23 = 2,692` = the shipped
line count:

| Line | Field |
| --- | --- |
| 1 | `UniquePolicyID` |
| 2 | `validscenarios`, width 3 + `;` (`001;002;`) |
| 3 | pre-policy count |
| 4–8 | **five** pre-policy id slots; unused slots are read and discarded |
| 9 | `ResearchDuration`, years |
| 10 | `IsAct` |
| 11 | `ReferedChapter` (−1 = always available) |
| 12 | `TooltipHeadline` — the policy's display name |
| 13 | `Tooltip` — the description |
| 14 | `ShowNewspaperMessageEnemy` |
| 15 | headline count |
| 16–20 | **five** newspaper headline slots |
| 21 | `PictureID` |
| 22 | `HeadlinePicture` |
| 23 | `Content` — newspaper body |

The five-slot padding means prerequisites and headlines are capped at 5 each
regardless of the count field.

### 1.2 `Policy` is a singleton with one global `researchprogress`

`researchprogress` is a field on the shared component, not per alliance.
Per-alliance ownership lives in `GameVars.alliance[].activatedpolicies` /
`.activatedacts` / `.alreadyactivatedacts`. The two sides therefore **cannot
both research the same id** — which is why the catalogue is split into a
0–99 Union range and a 100+ Confederate range.

`Policy.GetStatusByPolicyID(id)` with its default argument only tests
`researchprogress >= 1f`, i.e. "has this id been completed by whoever owns
it". The two-argument form additionally requires the named alliance to hold it.

`Policy.GetAllianceFromPolicy(policy)` reads **the last character of the
parent GameObject's name**, falling back to the `>= 100` rule only when the
parent is named `"Choose"`. Another scene-name coupling.

---

## 2. The policy catalogue

Names are `TooltipHeadline` from `policies.dat`. **Ch** = `ReferedChapter`,
**Dur** = `ResearchDuration` in game-years, **Act** = `IsAct`,
**Pre** = prerequisite ids. All 117 ship with `validscenarios = 001;002;`.

### 2.1 Union — the five ladders (ids 0–19)

| id | Name | Ch | Dur | Pre |
| --- | --- | --- | --- | --- |
| 0 | Government Funding I | 0 | 0.068 | — |
| 1 | Government Funding II | 1 | 0.123 | 0 |
| 2 | Government Funding III | 2 | 0.164 | 1 |
| 3 | Bread Basket I | 0 | 0.068 | — |
| 4 | Bread Basket II | 0 | 0.082 | 3 |
| 5 | Bread Basket III | 1 | 0.123 | 4, 75 |
| 6 | Bread Basket IV | 2 | 0.164 | 5 |
| 7 | Industrialization I | 0 | 0.068 | — |
| 8 | Industrialization II | 0 | 0.082 | 7 |
| 9 | Industrialization III | 1 | 0.123 | 8, 76 |
| 10 | Industrialization IV | 2 | 0.164 | 9 |
| 11 | Military I | 0 | 0.068 | — |
| 12 | Military II | 1 | 0.123 | 11 |
| 13 | Military III | 2 | 0.164 | 12 |
| 14 | Military IV | 3 | 0.205 | 13 |
| 15 | Diplomacy I | 0 | 0.068 | — |
| 16 | Diplomacy II | 0 | 0.082 | 15 |
| 17 | Diplomacy III | 0 | 0.096 | 16 |
| 18 | Diplomacy IV | 1 | 0.123 | 17, 74 |
| 19 | Diplomacy V | 2 | 0.164 | 18 |

Three ladders take a **grand policy** as a mid-ladder prerequisite (5←75,
9←76, 18←74), so a pre-war choice gates a wartime ladder.

### 2.2 Union — acts (ids 20–48)

All `IsAct = true`.

| id | Name | Ch | Dur | Pre |
| --- | --- | --- | --- | --- |
| 20 | Print Notes I | 0 | 0.055 | 0 |
| 21 | Print Notes II | 0 | 0.068 | 0, 20 |
| 22 | War Bonds | 1 | 0.082 | 1 |
| 23 | Bank Act I | 2 | 0.110 | 2 |
| 24 | Tariff Act | 0 | 0.055 | 0 |
| 25 | Confiscation Act | 1 | 0.068 | 0 |
| 26 | Revenue Act I | 1 | 0.082 | 1 |
| 27 | Revenue Act II | 2 | 0.123 | 1, 26 |
| 28 | Revenue Act III | 2 | 0.137 | 2, 27 |
| 29 | Bank Act II | 3 | 0.110 | 2, 23 |
| 30 | Northern Routes | 0 | 0.055 | 4, 8 |
| 31 | Feed Europe I | 1 | 0.082 | 5 |
| 32 | Feed Europe II | 2 | 0.123 | 6, 31 |
| 33 | United States Colored Troops | 1 | 0.123 | 12, 34 |
| 34 | Emancipation Proclamation | 1 | 0.082 | 12 |
| 35 | Arming of Civilian Ships | 0 | 0.055 | 7, 11 |
| 36 | **Militia Act I** | 0 | 0.027 | — |
| 37 | Militia Act II | 0 | 0.055 | 36 |
| 38 | Militia Act III | 1 | 0.082 | 37 |
| 39 | Enrollment Act I | 1 | 0.082 | 12 |
| 40 | Enrollment Act II | 2 | 0.123 | 12, 39 |
| 41 | Legal Blockade | 0 | 0.055 | 11 |
| 42 | Lieutenant General Act | 2 | 0.110 | 13 |
| 43 | Letters of Marque | 0 | 0.055 | 11 |
| 44 | Abolition of Slavery | 2 | 0.123 | 12, 34 |
| 45 | Recruitment Bounties I | 1 | 0.082 | 12 |
| 46 | Recruitment Bounties II | 2 | 0.123 | 12, 45 |
| 47 | Militia Act IV | 1 | 0.096 | 38 |
| 48 | Regulars Act | 0 | 0.055 | 11 |

### 2.3 Confederate — ladders and acts (ids 100–147)

Structurally mirrored but **not uniformly offset by 100**. The ladders are
(100–102 Government Funding I–III, 103–106 **King Cotton** I–IV, 107–110
Industrialization I–IV, 111–114 Military I–IV, 115–119 Diplomacy I–V), with
the same chapters and durations as their Union twins, and 105←175, 109←176
replacing 5←75, 9←76. The Confederate Diplomacy IV (118) has **no** grand-policy
prerequisite where the Union's 18 requires 74.

| id | Name | Ch | Dur | Pre | Union twin |
| --- | --- | --- | --- | --- | --- |
| 120 | Print Notes I | 0 | 0.055 | 100 | 20 |
| 121 | Print Notes II | 0 | 0.068 | 100, 120 | 21 |
| 122 | War Bonds | 1 | 0.082 | 101 | 22 |
| 123 | Bank Act | 2 | 0.110 | 102 | 23 |
| 124 | Tariff Act | 0 | 0.055 | 100 | 24 |
| 125 | **Impressment Act** | 1 | 0.068 | 100 | 25 Confiscation |
| 126 | Revenue Act I | 1 | 0.082 | 101 | 26 |
| 127 | Revenue Act II | 2 | 0.123 | 101, 126 | 27 |
| 128 | Revenue Act III | 2 | 0.137 | 102, 127 | 28 |
| 129 | **Nationalized Trade Act** | 3 | 0.137 | 102 | 29 Bank Act II |
| 130 | **Burn Cotton** | 2 | 0.110 | 106 | 30 Northern Routes |
| 131 | **Restrict Cotton Trade** | 1 | 0.055 | 104 | 31 Feed Europe I |
| 132 | **Slavery Restriction Act** | 2 | 0.082 | 109 | 32 Feed Europe II |
| 133 | Confederate States Colored Troops | 2 | 0.123 | 109, 132 | 33 |
| 134 | Emancipation Proclamation | 3 | 0.151 | 110, 132 | 34 |
| 135 | Arming of Civilian Ships | 0 | 0.055 | 107, 111 | 35 |
| 136 | **Militia Act I** | 0 | 0.027 | — | 36 |
| 137 | Militia Act II | 1 | 0.068 | 136 | 37 |
| 138 | Militia Act III | 1 | 0.096 | 137 | 38 |
| 139 | Conscription Act I | 1 | 0.082 | 112 | 39 |
| 140 | Conscription Act II | 2 | 0.123 | 112, 139 | 40 |
| 141 | **Free Trade Act** | 0 | 0.055 | 116 | 41 Legal Blockade |
| 142 | **Organized Blockade Running** | 1 | 0.082 | 112, 118 | 42 Lt. General Act |
| 143 | Letters of Marque | 0 | 0.055 | 111 | 43 |
| 144 | **Support Mexican Intervention** | 1 | 0.082 | 112, 118 | 44 Abolition |
| 145 | Recruitment Bounties I | 1 | 0.082 | 112 | 45 |
| 146 | Recruitment Bounties II | 2 | 0.123 | 112, 145 | 46 |
| 147 | **Regulars Act** | 0 | 0.055 | 111 | 47 Militia Act IV |

The Confederacy has **no Militia Act IV** and **no Lieutenant General Act**;
the Union has no equivalent of Nationalized Trade, Burn Cotton, Restrict Cotton
Trade, Slavery Restriction, Free Trade or Organized Blockade Running. The
Confederate Militia Acts are also chapter-gated one step later (137 and 138
both at chapter 1, against the Union's 0 and 1).

> **Do not assume `id + 100` is the counterpart.** It holds for 0–28, 33–40,
> 43 and 45–46 and fails for 29–32, 41–42, 44, 47–48. The `switch` in
> `LaunchPolicyEffect` encodes this exactly: shared behaviour is written
> `case N: case N+100:`, and the divergent ids get their own arms.

### 2.4 Grand (pre-war) policies — ids 70–79 and 170–179

All ten per side: `ReferedChapter = 0`, `ResearchDuration = 0`, `IsAct = true`,
no prerequisites. They are not researched; they are **chosen before the
campaign starts**.

| Union | Name | Confederate | Name |
| --- | --- | --- | --- |
| 70 | Underground Railroad | 170 | The Old Dominion |
| 71 | Kansas a Free State | 171 | Kansas a Slave State |
| 72 | Go West! | 172 | Slavery to the West |
| 73 | Support Abolitionism | 173 | Apostles of Disunion |
| 74 | Enforce Neutrality Act | 174 | Filibustering |
| 75 | Bread Basket | 175 | King Cotton |
| 76 | Industrialization | 176 | Southern Industrialization |
| 77 | Security Measures | 177 | Arms Agents |
| 78 | Indian Wars | 178 | Alliance with the Natives |
| 79 | Union Pacific Railroad | 179 | Southern Pacific |

`GamePrefs.chooseablegrandpolicies = 3`. `Policies.UpdateGrandStrategies`
(~216) runs only in the main menu (`currentoperation == 0`): the player's three
come from the menu; **the AI's three are the first three entries of its
personality's `prewarpolicies` list** (§13.1). Both sets are then
`ResearchPolicy`'d to completion and the staging lists cleared.

---

## 3. Research mechanics

### 3.1 The rate

`Policies.UpdatePolicyResearch(alliance, timediff)` (~318), per policy in
research:

```
N    = GetActivatedPolicies(includeactsinresearch: true,
                            includegrandpolicies: false, researchedonly: true)
rate = averagesupport / N^policyspeedfactor
       × (1 + project_administrationreformpolicyspeed × projectLevel(95))
       / ResearchDuration
```

`policyspeedfactor = 1.1`; the config comment spells out the intent
("1 means speed is halved, 1.1 means speed for policy #2 is at 47%").

**The panel shows two different day counts for the same policy, and only one
is right.** `UpdateAllTooltips` (~301) computes

```csharp
policy.ResearchDuration * (1f - policy.researchprogress) * 365.25f
    / num2 / num3 / num        // support, 1/N^1.1, admin-reform factor
```

— correct, and it even itemises the three penalties in prose ("−25 % research
speed due to lower state support", "… as multiple policies are being
researched"). But `UpdateActivations` (~131), which writes the policy's own
`DurationText`, computes

```csharp
(policy.ResearchDuration * 365.25f * (1f - policy.researchprogress)) + " days"
```

with **no divisors at all**. So the figure printed on a policy is its nominal
duration and the figure in its tooltip is the real one; at 75 % average
support with three policies in research the printed number is **4.5× too
optimistic**.

> **Corrects an earlier reading of this file**, which took the tooltip's
> agreement with the simulation to cover the panel as a whole.

> **Parallel research is a net loss.** Total throughput across N policies is
> `N × N^-1.1 = N^-0.1`, which *decreases* with N: at the cap of 12 the
> aggregate rate is 0.79× that of researching one at a time. Parallelism only
> buys you several policies finishing together. **The AI never parallelises**
> (§13), so this is a one-sided handicap the player opts into.

`averagesupport` is the other driver, which is the link back to
[17-politics-and-support](17-politics-and-support.md): state support sets the
research speed of everything.

This corrects [09](09-policy-and-project-effects.md), which named
`Policies.UpdateAllTooltips` as a "rosetta stone" for id → effect. It is not —
it only appends research-speed text. The per-policy description lives in the
prefab/`policies.dat` `Tooltip` field, and the **mechanical** effect exists
only in `LaunchPolicyEffect`. The project-side rosetta stone
(`Projects.GetProjectEffect`) *is* real — see §9.

### 3.2 The slot cap — a lifetime budget, not a queue width

```
maxpolicies = minimumpolicies + Mathf.RoundToInt(
                  (maximumpolicies − minimumpolicies)
                  × GetSubsidyFactor(subsidy_politics))
```

`minimumpolicies = 7`, `maximumpolicies = 12`, and
`GetSubsidyFactor(politics) = subsidies[0] × subsidiesfactor[0]` where the
politics factor is **never written** (there is no Politics ladder) and stays
`1f`. So the cap is `7 + round(5 × the politics slider)` — and because
`RoundToInt` is half-to-even, sliders of 0.4 and 0.5 both give 9, and 0.8 and
0.9 both give 11.

**What the cap counts is the thing that matters.** `CanBeActivated` calls
`GetActivatedPolicies()` with **all three parameters defaulted**, which counts
every entry of `activatedpolicies` that is not an act and not a grand policy —
*regardless of progress*. And `ResearchPolicy` retires only **acts** to
`activatedacts` on completion; a finished non-act stays in `activatedpolicies`
forever, because §6's reset-then-replay architecture replays its effect from
that list every tick.

> **So the 7–12 counter is not the research queue width; it is the number of
> ladder levels a nation may ever hold.** There are **20 non-act policies per
> side** (ids 0–19 and 100–119, verified against `policies.dat`), and a ladder
> top needs every level beneath it — 3 for Government Funding, 4 each for
> Agriculture, Industrialization and Military, **5 for Diplomacy**. Twelve
> slots therefore buy the top of **three branches**; seven buy two.

`CanBeActivated` refuses a non-act when the queue is full; **acts bypass the
cap entirely**. But acts in research still count toward N in the rate divisor,
so queueing acts past the cap slows everything down without being blocked.

`UpdateSubsidyEffect` — called once per alliance pass from
`BattleUnits.UpdateAllianceData` — enforces the cap retroactively: while over
it, it repeatedly cancels the **non-act with the greatest `ResearchDuration +
(1 − progress)`** — i.e. the one with most work left — up to 50 removals per
call. Two consequences the formula hides:

- **In-research policies always go first.** Their `1 − progress` term reaches
  1.0 while the largest `ResearchDuration` in the file is 0.205, so nothing
  completed is touched until everything in research is gone.
- **Then it starts cancelling completed ladder levels**, and
  `RemoveResearch(alliance, policy)` zeroes not only that policy's
  `researchprogress` but **every policy whose transitive prerequisite closure
  contains it** — `GetListOfPrePolicies` is a fixpoint walk, so the whole
  subtree above the cancelled policy is wiped. Completed **acts** are spared by
  the `(!IsAct | researchprogress < 1f)` guard; completed non-acts are not.

> **Lowering the politics subsidy therefore destroys policies.** Dropping the
> slider from 1.0 to 0.6 removes two ladder levels and everything downstream of
> them: cancelling Government Funding I, for instance, zeroes II and III and
> every incomplete act in the economic branch. There is no warning and no undo.

### 3.2a What a policy ladder actually buys: the subsidy cap

Each ladder's only shared effect is to overwrite one slot of
`Alliance.subsidiesfactor[]`, which multiplies that category's cost — and
therefore its cap — in `GetSubsidyCost`:

```csharp
cost(id) = Mathf.Min(1f, subsidies[id]) * GamePrefs.basicsubsidiescost[id]
                                        * subsidiesfactor[id];
```

`basicsubsidiescost = {10e6, 50e6, 50e6, 50e6, 50e6, 50e6, 0, 0}` for
*(politics, economy, agriculture, industry, military, diplomacy, n/a, n/a)*,
and `subsidiesfactor` initialises to all `1f`. `UpdatePolicyEffects` resets the
five live slots to each array's `[0]` entry every tick, then each completed
ladder policy writes its own index:

| Branch | Levels | Array | Values | Cap at the top |
| --- | --- | --- | --- | --- |
| Politics | — | *(never written)* | stays 1 | **$10 M** |
| Funding / Economy | I–III | `economysubsidies[5]` | 0.1, 0.25, 0.5, 1.0, **2.00 unused** | **$50 M** |
| Agriculture | I–IV | `agriculturalsubsidies[5]` | 0.1, 0.25, 0.5, 1, 2 | $100 M |
| Industry | I–IV | `industrialpolicyadj[5]` | 0.1, 0.25, 0.5, 1, 2 | $100 M |
| Military | I–IV | `militarysubsidies[5]` | 0.1, 0.25, 0.5, 1.0, 2.0 | $100 M |
| Diplomacy | I–V | `diplomacysubsidyadj[6]` | 0.1, 0.25, 0.5, 1, 2, **3** | **$150 M** |

Three things to note.

- **With no policy in a branch the factor is 0.1**, so the cap starts at $5 M
  (or $1 M for politics) and doubles roughly per level.
- **The Funding array's fifth entry is dead**, and the config says so —
  *"last one not used"*. There is no Government Funding IV.
- **Diplomacy is the only six-entry array** and the only branch whose top cap
  exceeds $100 M. It is also the only ladder with five levels.

`GetSubsidyFactor(id) = subsidies[id] × subsidiesfactor[id]` has exactly one
consumer, the `maxpolicies` line above, where the factor is always 1 — so the
fact that it can return up to 3 for diplomacy never leaks into anything that
expects a [0, 1] fraction.

#### 3.2b The factor is also the project-funding accrual rate

`Alliance.GetSubsidyCost(id)` returns `−(min(1, subsidies[id]) ×
basicsubsidiescost[id] × subsidiesfactor[id])` — negative, because it is an
expense line. `BattleUnits.UpdateAllianceData` (~3812) then runs, once per
alliance pass, for every one of the eight slots:

```csharp
AdjustSubsidyFunding(type, timediff * (0f - GetSubsidyCost(type)) * aiBonus);
// aiBonus = 1 + usedcampaignbonusrunning × influenceaibonusonsubsidies, AI only
```

The double negation makes it **positive**, so:

```
project budget accrued per annum  = slider × basicsubsidiescost × ladder factor
treasury expense per annum        = exactly the same figure
```

This **closes the accrual half of §16's open question 4**: `subsidies[]` is the
player's slider, `subsidiesfactor[]` the ladder multiplier,
`subsidyfunding[]` the accrued per-category budget, and money moves from the
treasury into that budget 1:1 at the category's annual rate.
`AdjustSubsidyFunding` floors the balance at 0 and never overdraws;
`AppointProject` is the only debit.

| Category | Base | Factor with no policy | Accrual at a full slider |
| --- | --- | --- | --- |
| Politics | $10 M | **1.0, always** | **$10 M/yr from day one** |
| Economy | $50 M | 0.1 | $5 M → $50 M at Funding III |
| Agriculture / Industry / Military | $50 M | 0.1 | $5 M → $100 M at level IV |
| Diplomacy | $50 M | 0.1 | $5 M → **$150 M** at Diplomacy V |

Two consequences.

**Politics is the only category that is fully funded in 1861**, because it is
the only one with no ladder to multiply it. Everything else accrues at a tenth
of its headline rate until the first ladder level lands — which makes the first
level of a branch worth 2.5× its own throughput, and makes **Administration
Reform ($1 M, politics, +5 % research speed per level) the one project a 1861
campaign can actually afford.**

**`AdjustSubsidyFactor(id, tovalue)` is an assignment**, and its
`skipadjustmentoldvalue` parameter — which would rescale the slider to hold
absolute spend constant — defaults to `true`, so the policy path never uses it.
Completing a ladder level therefore **raises the category's bill immediately**
at an unchanged slider position: Military III at a 60 % slider takes military
spending from $15 M to $30 M a year with no player action. Since the
prerequisite chain forces levels to complete in order, the last write always
carries the highest level, so no ordering bug follows from the assignment.

### 3.3 `ResearchPolicy` and the three lists

`Policy.ResearchPolicy(id, alliance, progress = 1f, additive = false)` clamps
`researchprogress` to [0, 1], adds the policy to `activatedpolicies` while
progress > 0, removes it at 0, and on completion **moves an act out of
`activatedpolicies` into `activatedacts`**. A newspaper message fires only on
the additive path crossing 1.0, so a scripted `ResearchPolicy(..., 1f)` (grand
policies, save loading) is silent.

`alreadyactivatedacts` is the third list and the one the effect bodies test;
see §7.

---

## 4. Chapters

`Policy.CurrentChapter` is **derived state, recomputed from scratch** on every
`CheckForChapterUpdate()` call (~469) — it starts at 0 and is raised. Nothing
persists it.

Scenario `001`:

| Chapter | Begins |
| --- | --- |
| 0 | campaign start (pre-war) |
| 1 | Union policy **36** completed, **or** automatically on **15 April 1861** |
| 2 | **5 November 1862** |
| 3 | **9 November 1864**, and campaign objective 26 accomplished while 27 is not |

Scenario `002` starts at chapter 1 and uses the same two dates.

Two hardcoded events sit in this method:

- on **14 April 1861**, if the war has not started,
  `GetBuildingByNameAndPosition("FtSumter", (1547, 489, −1304), id_fort, 10f)`
  is handed to alliance 1 — **a name *and* a world coordinate**, the most
  brittle lookup found so far;
- crossing into chapter 1 calls `AICampaign.UpdatePreWarArmies()` and stamps
  `BattleUnits.warstartdate`.

A chapter change fires `newspaper.LaunchHistoricalEventByID(CurrentChapter)`
and plays `CHAPTER_<n+1>_COLOR_MASTER` (resolved by name).

`Policies.IsDeactivated(policy)` is the only exclusion mechanism and has
exactly two entries: policy **36** is suppressed before **4 March 1861**
(Lincoln's inauguration), and policy **170** (The Old Dominion) while Virginia
is still Union. There is no general mutual-exclusion system.

---

## 5. Militia Act I starts the war

Policy 36/136 is not a recruiting policy with a side effect; it is the war
trigger, and it carries the largest case body in the file. For `uniquePolicyID
== 36`, chapter ≤ 1, first activation:

1. `HandoverForts()` — every fort not owned by alliance 0 is handed over;
2. every state with `alliance == 0 || 1` is reassigned to **whichever side it
   supports more** (`SetNationToNewAlliance`);
3. `GameVars.alliance[0].SetWarWithNation(1)`;
4. ten states are flipped to `recruitable = true`: **2 Arkansas, 10 Indian
   Territory, 13 Kansas, 14 Kentucky, 22 Missouri, 28 North Carolina,
   34 Tennessee, 38 Virginia, 39 West Virginia, 50 New Mexico Territory**.

Before that, while only one side has passed it, the other side's states bleed
support each tick (`militiaact1csabeforeunionsupport`,
`militiaact1unionaftercsasupport`, reason codes 14 and 15) and national morale
moves on code 5. The case also uses **`goto case 136;`** — the only `goto` in
the switch — so the Union arm falls into the shared arm deliberately.

### 5.1 Who moves first, and what it costs

The "before" and "after" branches are not symmetric, and the gap is large. All
rates are per annum, applied as `pref × timediff`, so about 1/730 of the
figure lands per alliance pass:

| Situation | Effect | Pref | Value |
| --- | --- | --- | --- |
| Union passes 36 **before 14 Apr 1861** | CSA support in every slave state, **once** | `militiaact1unionbeforesumter` | **+0.10** |
| Union holds 36, CSA lacks 136 | CSA support in every slave state | `militiaact1unionbeforecsasupport` | **+2.00**/yr |
| Union holds 36, CSA lacks 136 | CSA national morale, code 5 | `militiaact1csabeforeunionnationalmorale` | +0.40/yr |
| CSA holds 136, Union lacks 36 | Union support in every **free** state | `militiaact1csabeforeunionsupport` | +0.75/yr |
| CSA holds 136, Union lacks 36 | CSA support in every **slave** state | `militiaact1unionaftercsasupport` | +0.75/yr |
| CSA holds 136, Union lacks 36 | Union national morale, code 5 | `militiaact1unionaftercsanationalmorale` | +0.40/yr |

> **Going first is 2.7× worse for the Union.** Calling out the militia hands
> the Confederacy +2.00/yr of slave-state support; letting the Confederacy
> move first costs the Union only the mirrored +0.75/yr and pays the Union
> +0.75/yr in the free states plus +0.40/yr of national morale. And the
> one-shot +0.10 lands in the **same call** as the secession cascade, before
> the `support[0] > support[1]` comparison that fixes every state's allegiance
> for the campaign ([17](17-politics-and-support.md) §5a).
>
> Note that the two field names are the wrong way round relative to their
> config labels — `militiaact1csabeforeunionnationalmorale` is applied on the
> **Union**-first path, and `militiaact1unionaftercsasupport` on the
> CSA-first path. The labels are right; the names describe the same situations
> from the other side.

The window is bounded by the chapter gate (`CurrentChapter <= 1`) and by the
other side passing its own Militia Act I. The Confederate act is chapter 0 with
`ResearchDuration = 0.027` years, so the AI closes the window in about ten
days — worth roughly +5 points of slave-state support on top of the one-shot
+10.

Every Militia / Conscription act then sets `contractlength` from
`militiaactcontract[]` and adds to `volunteerspolicyadj` /
`volunteerspolicyadjrunning`, with a contract-length penalty
`Tools.Interpolate({3, 12, 24, 36}, contractlength, recruitsgrowthreductioncontract)`
— **longer enlistments reduce the volunteer flow**, which is the trade-off the
ladder is built around.

---

## 6. The effect architecture: reset, then replay

`Policies.UpdatePolicyEffects(alliance, timediff, startsetup)` (~479) runs
every campaign tick and does this, in order:

```csharp
ratingmodifier = 0;  treasuryadjustment = 0f;  publicwealthfactor = 1f;
supplyenemysoilfactor = 1f;  transportcapacityports = 1f;  civilianships = false;
volunteerspolicyadj = GamePrefs.premilitiaactvolunteersfact;
AdjustSubsidyFactor(military,    militarysubsidies[0]);
AdjustSubsidyFactor(agriculture, agriculturalsubsidies[0]);
AdjustSubsidyFactor(industry,    industrialpolicyadj[0]);
AdjustSubsidyFactor(diplomacy,   diplomacysubsidyadj[0]);
AdjustSubsidyFactor(economy,     economysubsidies[0]);
// then LaunchPolicyEffect for every completed activatedpolicy and activatedact
// then DeactivatePolicyEffect for every alreadyactivatedact no longer held
```

So **`LaunchPolicyEffect` is replayed from scratch every tick**, and that list
of eight scalars plus five subsidy factors is the complete set of
**policy-owned accumulators**. Everything else a case does is either

- guarded by `if (!alreadyactivatedacts.Contains(policy))` — fire once; or
- multiplied by `timediff` — a per-tick rate.

### 6.1 The contract for a hook

This is the rule [09](09-policy-and-project-effects.md) left implicit, and it
matters more than the dispatch site itself:

1. a postfix on `LaunchPolicyEffect` runs **every tick**, not once;
   The cadence is now pinned: `UpdatePolicyEffects` is called from
   `BattleUnits.UpdateAllianceData`, which processes **one alliance per
   campaign heartbeat, round-robin**, and refuses to advance until
   `GamePrefs.updaterhythmalliancedata = 0.50` **days** have passed for that
   alliance — so "every tick" means **once per 12 game hours per alliance**,
   with `timediff` ≈ `0.5/365` campaign years
   ([23](23-game-state.md) §6.1);
2. anything it accumulates into must be reset somewhere, or it will grow
   without bound — the thirteen fields above are reset for you, nothing else
   is;
3. to fire once, test `GameVars.alliance[alliance].alreadyactivatedacts.Contains(policy)`
   exactly as the game's own cases do;
4. to apply a rate, multiply by `timediff`.

### 6.2 Deactivation is essentially unimplemented

`DeactivatePolicyEffect` has **one** case: policy 131 (Restrict Cotton Trade),
which restores European cotton demand by multiplying by
`1 / cottontradecottondemand` rather than restoring the prior value — so a
second cancellation would compound. Everything else is either recovered
implicitly by the per-tick reset or is **permanent**: created units, state
reassignments, building levels, forts, railroads.

---

## 7. Policy effects by id

Derived from `LaunchPolicyEffect` (~544–1740): 117 case labels, no `default:`.
"once" = guarded by `alreadyactivatedacts`; "rate" = scaled by `timediff`.

### 7.1 The five ladders

| ids | Effect |
| --- | --- |
| 0–2 / 100–102 Government Funding I–III | `AdjustSubsidyFactor(subsidy_economy, economysubsidies[1..3])` |
| 3–6 / 103–106 Bread Basket / King Cotton I–IV | `AdjustSubsidyFactor(subsidy_agriculture, agriculturalsubsidies[1..4])` |
| 7–10 / 107–110 Industrialization I–IV | `AdjustSubsidyFactor(subsidy_industry, industrialpolicyadj[1..4])` |
| 11–14 / 111–114 Military I–IV | `AdjustSubsidyFactor(subsidy_military, militarysubsidies[1..4])` |
| 15–19 / 115–119 Diplomacy I–V | `AdjustSubsidyFactor(subsidy_diplomacy, diplomacysubsidyadj[1..5])` |

**That is all they do.** Shipped caps:
`agriculturalsubsidies = economysubsidies = industrialpolicyadj =
militarysubsidies = {0.1, 0.25, 0.5, 1, 2}` (index 0 = no policy) and
`diplomacysubsidyadj = {0.1, 0.25, 0.5, 1, 2, 3}`. See §10 for the six further
effects the config describes and the code does not implement.

Case 3/103 contains `if (alreadyactivatedacts.Contains(policy)) { }` — **an
empty body**, the scar where the agricultural support bonus used to be (§10).

### 7.2 Union acts

| id | Effect |
| --- | --- |
| 20 / 21 | `treasuryadjustment += printnotestreasury[0/1]` (**+50 M/yr each**, cumulative); `publicwealthfactor *= printnoteswealthfactor[0/1]` (**×0.9 each**) |
| 22 | `publicwealthfactor *= warbondspublicwealthfactor` (0.9); `ratingmodifier += warbondscreditrating` (**−2 notches**) |
| 23 / 29 | `ratingmodifier += bankactcreditrating[0/1]` (−2 each); `treasuryadjustment += bankacttreasury[0/1]`; once: `AdjustAllSupport(bankactsupport[0/1], 10)` — note **23 uses reason 10, 29 uses reason 12** |
| 24 | `taxcaps[tax_tarifs] = tariffactsubsidies × (Confederate policy 141 active && alliance == 1 ? freetradetariffs : 1)`; once: `AdjustInterventionLevel(tariffactrelations)` |
| 25 | `supplyenemysoilfactor *= confiscationsupplyflow`; **rate**: every Confederate state gains CSA support (reason 11) |
| 26–28 | once: `AdjustAllSupport(revenueactsupport[0..2], 12)`; `taxcaps[tax_income] = revenueactincometaxcap[0..2]`; `taxcaps[tax_corporate] = revenueactcorporatetaxcap[0..2]` |
| 30 | `SetProductionFactor(resid_crops, northernroutesfactor)` in seven states **matched by name** (§11.2) |
| 31 / 32 | once: European trade nodes get `SetDemandFocus(resid_crops, feedeuropedemand[0/1])` + `AdjustInterventionLevel(feedeuropeintervention[0/1])`; always: `ratingmodifier += feedeuropecreditratingunion[0/1]`, `taxcaps[tax_landsales] = feedeuropelandsalestax[0/1]` |
| 33 | **rate**: `volunteerspolicyadjrunning += usctvolunteersrunning × timediff`. Once: CSA national morale 20; then **creates a Corps + Division + `usctbrigadestobeadded` brigades** at the capital, each from a random state in a hardcoded list (§11.3) |
| 34 | once: CSA national morale 21; `AdjustAllSupport(emancipationunionsupport, 13)`; `AdjustInterventionLevel(emancipationintervention)` |
| 35 / 135 | `transportcapacityports = civilianshipstransportcap`; `civilianships = true` |
| 36–38, 47 / 136–138 | contract length + volunteer factors (§5) |
| 39 / 40 | once: every state's `mobilizationrate[alliance] += draftmobilization[0/1]`; national morale 22; **all fighting units' `contractdate` is reset to now** (and for the CSA, `contractlength` raised to `draftresetcontractlengthcsa`). Always: `volunteerspolicyadj += draftsvolunteersfactor`; **rate**: `draftspolicyadjrunning` |
| 41 | once: `legalblockade = true`; `AdjustInterventionLevel(legalblockadeintervention)`; **every resource** at the European nodes gets `SetDemandFocus(…, legalblockadetrade)` |
| 42 | once: `ltgenpolicy = true`; C-in-C gains `ltgenfame` (reason 4) and **`rank = 8`** written directly. See [19](19-commanders-and-perks.md) §6 for what `ltgenpolicy` then does to `campaignranking` |
| 43 / 143 | once: `lettersmarque = true`; `AdjustInterventionLevel(lettersmarqueintervention)` |
| 44 | once: `AdjustInterventionLevel(abolitionintervention)`; **both** alliances' national morale on code 23 |
| 45 / 46 | once: `bountieslevel = 0` / `1`; always `volunteerspolicyadj += bountiesvolunteers[0/1]`; **rate**: `volunteerspolicyadjrunning` |
| 48 / 147 | once: **creates a Corps + Division + 7 named regular regiments** — "U.S. Regulars", "1st U.S. Infantry"…"2nd U.S. Artillery" (or "C.S." equivalents), `contractlength = 99`, training and experience seeded from `militaryexperience + usregularstraining/exp`, `IncreasePerkExp(usregularsinitialperkvalue, -2)`; then `militaryexperience += usregularsmilexp` |

### 7.3 Confederate-only acts

| id | Effect |
| --- | --- |
| 125 Impressment | once: own states gain support (reason 20); `impressment = true`. Always: `volunteerspolicyadj += impressmentrecruits` |
| 129 Nationalized Trade | once: European nodes `SetDemandFocus(resid_cotton, nationalizetradecottondemand)`; slave states gain CSA support (reason 21) |
| 130 Burn Cotton | once: **every owned IIP's cotton stock is multiplied by `burncottonstock`**; `AdjustInterventionLevel(burncottonintervention)` |
| 131 Restrict Cotton Trade | once: European nodes `SetDemandFocus(resid_cotton, cottontradecottondemand)`; `AdjustInterventionLevel`. Always: `publicwealthfactor *= cottontradepublicwealth`. The **only** policy with a `DeactivatePolicyEffect` arm |
| 132 Slavery Restriction | once: `maxslaveryfactor += slaveryrestrictionfactor`; `AdjustInterventionLevel`; own slave states' support (reason 22) |
| 133 CSCT | once: own slave states' support (reason 23); creates **Army + Corps + Division + `csctbrigades` brigades** at the capital |
| 134 Emancipation | once: own states' support (reason 24), **`slaveryallowed = false`**, `mobilizationrate += emancipationcsadrafts`; `AdjustInterventionLevel`; Union national morale 21 |
| 141 Free Trade | `taxcaps[tax_tarifs] = freetradetariffs × (Union policy 124 active ? tariffactsubsidies : 1)`; once: `AdjustInterventionLevel`. **Mirrors 24 — each side's tariff act reads the other's** |
| 142 Organized Blockade Running | once: Union national morale 24; `orgrunning = true` |
| 144 Support Mexican Intervention | once: Union national morale 25; **Texas** (state 35) gains `mexicoavailablerecruits` and **half its unrecruited volunteers are immediately marked recruited**; `SetWarWithNation(5)` — war with Mexico. Also the gate for European intervention power 3 ([17](17-politics-and-support.md)) |

### 7.4 Grand policies

| id | Effect |
| --- | --- |
| 70 Underground Railroad | once: **both** alliances' `maxslaveryfactor += undergroundrailroadslavery`; CSA `volunteerspolicyadj += undergroundrailroadrecadj`. **Rate**: both alliances' `maxslaveryfactor` grows per year |
| 71 Kansas a Free State | once: Kansas (13) Union support (reason 16); every non-slave state a smaller amount |
| 72 Go West! | once: **California (3) and Oregon (30)** gain support (17), become `recruitable`, and have `population *= gowestpopulation`. Guarded by `GameVars.nation.Length > 50` |
| 73 Support Abolitionism | once: every Union state's **CSA** support is adjusted by `abolitionismcsasupport` (reason 18), which ships at **−0.10** — so it *lowers* Confederate support in Union states by 10 points rather than raising it |
| 74 Enforce Neutrality Act | once: `AdjustInterventionLevel(neutralityactintervention)` |
| 75 Bread Basket | once: every **neutral** state gains support (19); `AdjustInterventionLevel`; **every owned farm gains a level** |
| 76 Industrialization | once: every owned factory/foundry/manufactory gains a level; `northernindustrializationrailroadids` are **constructed immediately**; every Union state's `population *=`; national morale 27 |
| 77 Security Measures | once (and only after frame 34): **every ship in `Battlemap/Units/ShipPools/1` is sunk with probability `securitymeasuresships`**; Confederate IIP artillery and rifle stocks and the whole CSA `weaponstock` are multiplied by `securitymeasuresweapons`; CSA national morale 28 |
| 78 Indian Wars | once: both sides' national morale 29 and `militaryexperience += indianwarsmilexp[0/1]` |
| 79 Union Pacific Railroad | once: `unionpacificrailroadids` constructed; always `ratingmodifier += unionpacificrailroadratingmodifier` |
| 170 The Old Dominion | once: Virginia (38) CSA support (25); Union national morale 26; **capital set to Richmond, Virginia by name** |
| 171 Kansas a Slave State | once: Kansas (13) CSA support (26) and `SetSlaveryAllowed()`; every slave state a smaller amount |
| 172 Slavery to the West | once: **New Mexico Territory (50) and Arizona Territory (1)** gain CSA support (27), are reassigned to alliance 1, become recruitable, `population *=`; every harbour IIP in **Texas** (state 35) gains a building level |
| 173 Apostles of Disunion | once: slave states gain CSA support, free states gain Union support (both reason 28) |
| 174 Filibustering | once: **Cuba (48) and Nicaragua (49)** join alliance 1, gain support (29), become recruitable; `militaryexperience += filibusteringmilexp` (clamped to 1). Always: the Cuba/Middle-America trade nodes' `allianceowner` is set to this alliance. Also gates European intervention power 4 ([17](17-politics-and-support.md)) |
| 175 King Cotton | once: slave states gain CSA support (30); `AdjustInterventionLevel`; **every owned plantation gains a level** |
| 176 Southern Industrialization | once: every CSA state's `population *=`; `southernindustrializationrailroadids` constructed; every owned manufactory/ironworks/foundry gains a level; national morale 30 |
| 177 Arms Agents | once (after frame 34): Confederate IIP weapon stocks and the CSA `weaponstock` are multiplied by `armsagentsweapons`; Union national morale 31 |
| 178 Alliance with the Natives | once: **Indian Territory (10)** joins this alliance, `slaveryallowed = true`, recruitable, CSA support (31), `population *=` |
| 179 Southern Pacific | once: `southernpacificrailroadids` constructed; always: `Trade_Pacific`'s `allianceowner` set; `ratingmodifier += southernpacificrailroadratingmodifier` |

Note the symmetry break in 77/177: both multiply **Confederate** stocks and
both adjust the **enemy's** national morale, but 77 additionally sinks ships
from pool `1` — so Union "Security Measures" is strictly the stronger of the
two.

---

## 8. Reason codes

The label tables are `GameVars.supportdescr[42]` and
`GameVars.nationalmoraledescr[35]` (also catalogued in
[16-enumerations](16-enumerations.md)). Mapping them to writers completes
[17-politics-and-support](17-politics-and-support.md)'s open question.

### 8.1 Support — codes 9–31 are the policy block

| Code | Label | Written by |
| --- | --- | --- |
| 9 | Policy: Agriculture | **nobody** (§10) |
| 10 | Policy: Bank Act | 23 / 123 |
| 11 | Policy: Confiscation | 25 (rate) |
| 12 | Policy: Revenue Act | 26–28 / 126–128, **and 29** |
| 13 | Policy: Emancipation Act | 34 |
| 14 | Policy: Militia Act | 36 / 136 |
| 15 | Policy: Militia Act not triggered | 36 / 136 (rate, both directions) |
| 16 | Policy: Kansas a free State | 71 |
| 17 | Policy: Go West | 72 |
| 18 | Policy: Support Abolitionism | 73 |
| 19 | Policy: Bread Basket | 75 |
| 20 | Policy: Impressment | 125 |
| 21 | Policy: Nationalize Trade | 129 |
| 22 | Policy: Slavery Restriction | 132 |
| 23 | Policy: CSCT | 133 |
| 24 | Policy: Emancipation Act | 134 |
| 25 | Policy: The Old Dominion | 170 |
| 26 | Policy: Kansas a Slave State | 171 |
| 27 | Policy: Slavery to the West | 172 |
| 28 | Policy: Apostles of Disunion | 173 |
| 29 | Policy: Filibustering | 174 |
| 30 | Policy: King Cotton | 175 |
| 31 | Policy: Alliance with the Natives | 178 |

Non-policy codes, for completeness: 0 computed total; 1–7 battle casualties,
commander appointments, national morale, state occupation, drafting, raids,
Objective: Invade the Confederacy; **8** Objective: State Secession, written by
`CampaignObjective` for West Virginia (39), Kentucky (14) and Missouri (22);
32 medical treatment; **33 "Condition of enemy POW camps" — written by
nobody**; 34–37 projects 91–94; 38 "Commander" (DLC); 39–40 DLC incidents;
41 Objective: West Virginia (DLC).

> Both unwritten codes, **9 and 33**, are live labels with no producer. 33 was
> an open question in [17](17-politics-and-support.md) — the answer is that
> enemy POW camp conditions have a UI breakdown line and no mechanic.

### 8.2 National morale — codes 20–31 are the policy block

| Code | Label | Written by |
| --- | --- | --- |
| 5 | Militia act (Policies) | 36 / 136, both sides |
| 20 | USCT (Policy) | 33 → CSA |
| 21 | Emancipation (Policies) | 34 → CSA, 134 → Union |
| 22 | Drafts (Policies) | 39 / 40 |
| 23 | Abolition (Policies) | 44, both sides |
| 24 | Blockade Runners (Policy) | 142 → Union |
| 25 | Mexico (Policy) | 144 → Union |
| 26 | The Old Dominion (Policy) | 170 → Union |
| 27 | Northern Industrialization (Policy) | 76 |
| 28 | Security Measures (Policy) | 77 → CSA |
| 29 | Indian Wars (Policy) | 78, both sides |
| 30 | Southern Industrialization (Policy) | 176 |
| 31 | Arms Agents (Policy) | 177 → Union |
| 32 | **Industrialization (Policy)** | **nobody** |

Slots 13 and 15–19 have empty labels and no writers. Slot **32 has a label and
no writer** — the third dead reason code. 33 State Support and 34 Private
Wealth are the computed entries from [17](17-politics-and-support.md).

---

## 9. Projects

`projects.dat`'s 18-line record schema is already documented in
[06-config-file-format](06-config-file-format.md) §"Worked example". What
matters here is that `Projects.LoadedProjects.Import` is the **whole**
definition — there is no prefab — and that 56 of the 125 records are named
`PLACEHOLDER`:

| Range | Contents |
| --- | --- |
| 0–19 | small arms and artillery (20 projects) |
| **20–29** | **PLACEHOLDER** |
| 30–41 | ships (12 projects) |
| **42–87** | **PLACEHOLDER** (46 slots) |
| 88–124 | reforms, subsidies and standing programmes (37 projects) |

`repeating` is serialized inconsistently across the file (`false`, `False`,
`true`, `True`); `bool.Parse` is case-insensitive so it works, but it marks the
file as spreadsheet-generated.

`usedgroup` is an index into the UI's `ContentFoldersLeftRight` — **which
column the project appears in**, nothing more. Group 0 is military/industrial,
group 1 the economic and political programmes.

### 9.1 `subsidytype`

`GameVars`: `subsidy_politics = 0`, `subsidy_economy = 1`,
`subsidy_agriculture = 2`, `subsidy_industry = 3`, `subsidy_military = 4`,
`subsidy_diplomacy = 5`. The `campaignprefs.txt` label for
`basicsubsidiescost[8]` confirms it verbatim: *"(politics, economy,
agriculture, industry, military, diplomacy, n/a, n/a)"*, with costs
10 M / 50 M / 50 M / 50 M / 50 M / 50 M / 0 / 0 at 100 % usage.

Three further constants are **dead aliases**: `subsidy_transportx = 4`
(= military), `subsidy_tradewarx = 5` (= diplomacy), `subsidy_civilorderx = 7`
(an unused slot). `subsidyidrailroads = 3`. Every AI personality ships
`subsidyfocus[6] = subsidyfocus[7] = 0`. The picture is of a planned
eight-category subsidy system cut back to six — and §10 shows where the
amputated policy effects went.

### 9.2 Cost and appointment

```
cost(nextLevel) = nextLevel ^ subsidylevelcostincreaseexp × subsidycostfirstlevel
```

with `subsidylevelcostincreaseexp = 0.5`, so **each level costs √level times
the first** — a flattening curve, not a steepening one.

`IsAppointable` requires: level < `levelgraphics.Length` (**a UI sprite array
caps the simulation's maximum project level**), non-repeating projects not
already held, `HasPolicy(alliance)` for `requiredpolicyids`, the scenario in
`applicablescenarios`, `FulfillsDLC()`, and enough
`subsidyfunding[subsidytype]`. `AppointProject` then debits the funding, appends
the id to `alliance.projects`, and calls `UpdateOneTimeProjectsEffects` and
`UpdateProjectEffects`.

`GetProjectLevel(int id)` **counts occurrences of the id in `projects`** —
level *is* multiplicity, which is how repeating projects stack.

### 9.3 `datefrom` gates nothing — it grants

Each record carries `datefrom[2]`, a per-alliance availability date, and 34 of
the 125 records fill at least one of them — 24 for the Union and 21 for the
Confederacy (Military Railroad 1 Jan 1864, Sharps Rifles 1 Jan 1863,
Supply Reform 1 Jan 1862 Union / 1 Jan 1863 Confederate, and so on).
**Nothing in the availability path reads it.**
`IsAppointable` checks the sprite-array cap, repeatability, `HasPolicy`,
scenario, DLC and funding; `Projects.UpdateProjects`, which builds the panel's
list, checks `alliances`, `applicablescenarios`, `FulfillsDLC` and the category
filters. Neither looks at the date.

Its **only** reader in the assembly is `Projects.CheckProjectUnlocks(alliance)`
(~1025), and that method does the opposite of gating:

```csharp
if (datefrom[alliance] != null && applicablescenarios.Contains(leveltoload)
    && FulfillsDLC() && datefrom[alliance].yearfraction <= now
    && !alliance.projects.Contains(id) && IsAppointable(project, alliance)) {
    alliance.projects.Add(id);
    UpdateOneTimeProjectsEffects(id, alliance);
}
```

It has **exactly one call site**: `CampaignController` ~422, inside
`if (GameVars.frame == 32 && newcampaign)`, for every alliance. So it is a
one-shot backfill at the start of a **new** campaign, and:

- **no funding is debited.** `IsAppointable`'s balance check is made against a
  balance that `CheckProjectUnlocks` never touches, so every qualifying project
  is granted free and the comparison is re-made against the same untouched
  figure for the next one;
- **the one-time effects fire**, so Hall's Carbines' weapon stock, the two
  reform projects' `overridesymbol` rewrites and the four support steps all
  apply;
- **the dates are otherwise cosmetic.** A 1861 campaign can buy Military
  Railroad in March 1861.

Opening funding comes from each scenario's `subsidies.dat`
(`GameVars.ImportSubsidies`, which writes `subsidyfunding[alliance][type]`
directly). Spring 1861 ships **all zeros**; Spring 1864 ships 6 / 8 / 3 / 16 /
12 / 4.8 $M for the Union and 5 / 6 / 10 / 4 / 11 / 12 for the Confederacy. So
**a late-starting scenario is handed most of the dated project list for
nothing, and Spring 1861 is handed none of it** — a scenario-balance
difference that no description mentions.
---

## 10. The ten dead policy pref arrays

Ten `float[4]` arrays are parsed from `campaignprefs.txt` into `GamePrefs` and
**referenced nowhere else in the assembly**. Each has a descriptive config
label and non-trivial shipped values.

| Pref | Config label | Shipped |
| --- | --- | --- |
| `agriculturalintervention` | Agricultural policy 1-4, influence on intervention levels, cumulative | 0, 0.05, 0.05, 0.05 |
| `agriculturalsupportloyal` | Agricultural policy 1-4, support in loyal states, cumulative | 0, 0, 0, 0.05 |
| `transportpolicyadj` | Industrial policy 1-4, max **transportation** subsidies factor | 0.25, 0.5, 1, 2 |
| `industrialpolicynationalmorale` | Industrial policy 1-4, effect on national morale, cumulative | 0, 0, 0, 0.05 |
| `militiarecruitmentadj` | Military policy 1-4, max **recruitment** subsidies factor | 0.1, 0.25, 1, 2 |
| `militiacivilorderadj` | Military policy 1-4, max **civil order** subsidies factor | 0.25, 0.5, 1, 2 |
| `militiatradewaradj` | Military policy 1-4, max **trade war** subsidies factor | 0.25, 0.5, 1, 2 |
| `militiarailroadspeedadj` | Military policy 1-4, railroad movement speed of armies, cumulative | 0.8, 1.0, 1.33, 1.0 |
| `militiatransportupkeepadj` | Military policy 1-4, transport upkeep cost, cumulative | 1.2, 1.0, 0.5, 1.0 |
| `militaryeffectnationalmorale` | Military policy 1-4, effect on **military experience**, cumulative | 0, 0, 0, 0.05 |

Consequences, stated plainly:

- **Bread Basket / King Cotton I–IV raise one subsidy cap and nothing else.**
  The advertised European-intervention and loyal-state-support effects do not
  exist, and support reason code 9 ("Policy: Agriculture") has no producer. The
  empty `if (alreadyactivatedacts.Contains(policy)) { }` in case 3/103 is where
  the support call was removed.
- **Industrialization I–IV** does not touch transportation subsidies or
  national morale.
- **Military I–IV** does not touch recruitment, civil-order or trade-war
  subsidies, army railroad speed, transport upkeep, or military experience.
  Six arrays, one implemented effect.
- The last array's **field name and label disagree**
  (`militaryeffectnationalmorale` vs *"effect on military experience"*), so it
  is unclear which was intended.

The three vestigial `subsidy_*x` constants line up with three of these arrays
(transport, trade war, civil order), which is the strongest evidence that this
is one abandoned design rather than ten separate oversights.

### 10.1 And three Acts whose trade-war clause died with it

The same cut removed the Trade War subsidy category, and three Acts still
advertise raising its cap. Two of their prefs are **parsed and never read**,
with exactly two references each — the declaration and the loader line:

| Pref | Config label | Value | Status |
| --- | --- | --- | --- |
| `lettersmarquesubsidies` | LettersMarque, add on to max trade war subsidies | 1.2 | **dead** |
| `legalblockadetradewarsubsidies` | LegalBlockade, add on to max trade war subsidies | 1.2 | **dead** |
| `orgrunningtradewar` | OrgRunning, multiplier to blockade ratios if policy was chosen | 0.75 | **live, repurposed** — `IIP.cs:677` multiplies the blockade ratio of the owner's own harbours |

So Letters of Marque (43/143) reduces to setting `lettersmarque = true`, which
floors every enemy harbour's blockade ratio at `lettersmarquefloorblockade =
0.015`, plus its intervention term; Legal Blockade (41) keeps its intervention
and `legalblockadetrade = 0.75` European-demand terms; and Organized Blockade
Running (142) gives +0.02 Union national morale and a real 25 % reduction in
the blockade of Confederate ports that its description never mentions
([manual-vs-code](../mechanics-findings/manual-vs-code.md) §28).

---

## 11. Hardcoded indices and name lookups

### 11.1 The state table

`GameVars.import_nations(folder)` reads `<campaign>/<set>/nations.dat`:
**19 lines per record**, 53 states in scenario 001, then the alliance block
(whose count line is **8**, confirming the eight alliances of
[17](17-politics-and-support.md)).

| | | | | | | |
| --- | --- | --- | --- | --- | --- | --- |
| 0 Alabama | 1 Arizona Terr. | 2 Arkansas | 3 California | 4 Connecticut | 5 Dakota Terr. | 6 Delaware |
| 7 Florida | 8 Georgia | 9 Illinois | 10 Indian Terr. | 11 Indiana | 12 Iowa | 13 Kansas |
| 14 Kentucky | 15 Louisiana | 16 Maine | 17 Maryland | 18 Massachusetts | 19 Michigan | 20 Minnesota |
| 21 Mississippi | 22 Missouri | 23 Nebraska Terr. | 24 Nevada | 25 New Hampshire | 26 New Jersey | 27 New York |
| 28 North Carolina | 29 Ohio | 30 Oregon | 31 Pennsylvania | 32 Rhode Island | 33 South Carolina | 34 Tennessee |
| 35 Texas | 36 Unorganized Terr. | 37 Vermont | 38 Virginia | 39 West Virginia | 40 Wisconsin | 41 Canada |
| 42 France | 43 Spain | 44 Mexico | 45 The Bahamas | 46 District of Columbia | 47 Colorado Terr. | 48 Cuba |
| 49 Nicaragua | 50 New Mexico Terr. | 51 Utah Terr. | 52 Great Britain | | | |

Every bare index in the policy switch decodes against this table (§7). Note
`statesetname[6]`: a state can be **renamed per state-set**, and
`BattlefieldSetup` swaps `nation[i].name` when the set changes — so code that
matches a state **by name** can stop matching after a set change.

### 11.2 Policy 30 has two effect sites with two different state lists

| Site | States | Effect |
| --- | --- | --- |
| `Policies.LaunchPolicyEffect` case 30 | Illinois, **Michigan**, **Wisconsin**, Indiana, Ohio, **New York**, **Pennsylvania** | `SetProductionFactor(resid_crops, northernroutesfactor)` |
| `GameVars.Nation` recruit update (~4855) | Illinois, Ohio, Indiana, **Iowa**, **Kansas** | `recruitmentprojectfactor *= northernroutesvolunteersmodifier` |

Both match **by name**, both are keyed on policy 30, and only Illinois, Ohio
and Indiana appear in both. [17-politics-and-support](17-politics-and-support.md)
documented the second list; the first was missed. They are almost certainly
meant to be the same set.

### 11.3 The brigade-origin state lists

Policies 33 (USCT), 48/147 (Regulars) and 133 (CSCT) create brigades from a
random state in a hardcoded list:

| Policy | Union list | Confederate list |
| --- | --- | --- |
| 33 | 18, 29, 27, 31, 32, 11, 4 | 0, 7, 8, **20**, 33, 35, 38 |
| 133 | 18, 29, 27, 31, 32, 11, 4 | 0, 7, 8, **20**, 33, 35, 38 |
| 48/147 | 18, 29, 27, 31, 32, 11, 4 | 0, 7, 8, **21**, 33, 35, 38 |

The Union list is Massachusetts, Ohio, New York, Pennsylvania, Rhode Island,
Indiana, Connecticut — coherent. The Confederate list is Alabama, Florida,
Georgia, **Minnesota**, South Carolina, Texas, Virginia in two of three cases,
and **Mississippi** (21) in the third. **Minnesota is a Union state**; 21 is
almost certainly correct and 20 a typo, so USCT and CSCT brigades can be
raised from Minnesota.

All three draw with `list[Random.Range(0, list.Count - 1)]`, the max-exclusive
slip catalogued in [20](20-ai-campaign.md) §11 — so **Virginia (38), the last
entry, is never chosen**.

### 11.4 Name-keyed scene objects

`Policies.UpdateTradeNodesEurope()` scans `Battlemap/3DMap/IIPs` for exact
names: **`Trade_GB`**, **`Trade_FR`** (the "Europe" group), **`Trade_Cuba`**,
**`Trade_MiddleAmerica`**, **`Trade_MiddleAmerica2`** (the "Cuba" group) and
**`Trade_Pacific`**. This is a *different* set from the transport-capacity
nodes in [18-economy](18-economy.md) (`Trade_SantaFe`, `Trade_OregonTrail`,
`Trade_Butterfield`, `Trade_Arizona`, `Trade_Pacific`); only `Trade_Pacific`
appears in both.

Railroads are reached by **NavMesh area id** instead:
`GameObject.Find("Battlemap/3DMap/Railroads/CONSTRUCTION")` is scanned for
`ConstructionSymbol.NavMeshArea` matching
`northernindustrializationrailroadids`, `unionpacificrailroadids`,
`southernindustrializationrailroadids` or `southernpacificrailroadids`, then
`Construct(..., immediate: true, ignoredateofconstruction: true)`. Note policy
76 looks up the symbols **without** `includeInactive`, while 176 passes
`includeInactive: true` — so the Southern variant can construct inactive
symbols and the Northern one cannot.

Also name-keyed: `Town.GetTownFromName("Richmond", "Virginia")` (policy 170),
`GetBuildingByNameAndPosition("FtSumter", (1547, 489, −1304), …)` (§4), and
`Battlemap/Units/ShipPools/1` (policy 77).

---

## 12. Project effects by id

Three mechanisms, and which one a project uses is not visible from the data
file.

### 12.1 Push, continuous — `UpdateProjectEffects()` (~495)

Re-run for every alliance and every held project id whenever a project is
appointed. Projects **0–19** unlock weapons and **30–41** unlock ship types:

- `Weapon.SetAsImported(alliance)` — foreign purchase: 0 (weapons 17, 83),
  1 (15, 16, 23), 2 (7, 8, 11, 13), 3 (46, 1), 4 (47, 84);
  ships: 38 (type 17), 39 (18), 40 (30), 41 (36).
- `Weapon.SetResearched(alliance, w)` — domestic production: 6 (21, 22, 85),
  7 (1, 5), 8 (4, 9), 9 (3, 6, 39), 10 (37, 38), 11 (2, 12), 12 (81),
  13 (18, 45), 14 (26, 27, 32, 33), 15 (28, 30, 31), 16 (24, 25),
  17 (34, 35, 44), **18 (14)**, **19 (14)**.
- `shiptypes[t].techavailableforalliance.Add(alliance)`: 30 (19, 27),
  31 (25, 26), 32 (8, 13), 33 (14), 34 (28), 35 (15, 16), 36 (24, 38),
  37 (22, 23).

Projects 18 and 19 are both named "Springfield Rifles" and both unlock weapon
14; 18 is Confederate and requires policy **177** (Arms Agents), 19 is Union
and available from **1 June 1861**. The duplication is deliberate: one weapon,
two unlock routes.

Only two modifier projects are pushed here:

| id | Effect |
| --- | --- |
| 101 Military Railroad | `transportupkeepadj = 1 + level × project_militaryrailroadupkeep`; `railroadspeedadj = 1 + level × project_militaryrailroadtransportspeed` |
| 102 Weapon Production | `weaponproductionadjustment = 1 + level × project_weaponsproduction` |

### 12.2 Push, once — `UpdateOneTimeProjectsEffects(id, alliance)` (~392)

| id | Effect |
| --- | --- |
| 5 Hall's Carbines | `weaponstock[36] += 3000`, `weaponstock[86] += 2500` — **hardcoded, not prefs** |
| 10 Machineguns | `weaponstock[37] += 32`, `weaponstock[38] += 32` (Gatling, Coffee Mill) |
| 11 Confederate Guns | `weaponstock[12] += 48` (Williams gun) |
| 91 Propaganda | own states' support, **reason 34** |
| 92 Counter-propaganda | enemy states' support, **reason 35** |
| 93 Occupation Administration | own support in states this alliance occupies, **reason 36** |
| 94 Suppress Population | enemy support in states this alliance occupies, **reason 37** |
| 103 Send Envoys | `AdjustInterventionLevel(project_sendenvoysinterventionlevel)` |
| 110 Cavalry Reform | every own cavalry unit with `overridesymbol == 12` → **13**, then `CheckCommanderPromotions()` |
| 112 Artillery Reform | every own artillery unit with `overridesymbol == 10` → **11**, then `CheckCommanderPromotions()` |
| 116 Cotton is King | `AdjustInterventionLevel(project_cottonisking)` |
| 117 Corn is King | `AdjustInterventionLevel(project_cornisking)` |

`machinegunprojectstock = {32, 32, 48}`; the config names them gatling, coffee
mill, williams gun, so weapon ids 37, 38 and 12 are those three.

Because 91–94 are **repeating** and fire once per appointment, their support
effect is a step per level, not a rate.

### 12.3 Pull — `GetProjectLevel(id)` read at the point of use

This is where most of the interesting projects live. All take the shape
`1 + level × pref` unless noted.

| id | Name | Pref | Value | Read in |
| --- | --- | --- | --- | --- |
| 95 | Administration Reform | `project_administrationreformpolicyspeed` | 0.05 | `Policies` research rate (§3.1) |
| 96 | Subsidize Banks | `project_subsidizebanks` | 0.05 | `Alliance.GetFundingImprovementThroughBanks`, `Economy` workforce |
| 97 | Improve Credit Rating | `project_creditrating` | **−1** | `Economy` — `FloorToInt(level × −1)` **rating notches**, not a factor |
| 98 | Market Reform | `project_marketeffects` | 0.05 | **no live consumer** — see below |
| 99 | Infrastructure Reform | `project_infrastructure` | 0.05 | `IIP` transport capacity |
| 100 | Logistics Reforms | `project_logisticsreform` | **−0.05** | `CBuilding`, `Regiment`, `GameVars` — supply and transport upkeep |
| 104 | Subsidize Industry | `project_subsidizeindustry` | 0.05 | `CBuilding` productivity |
| 105 | Subsidize Agriculture | `project_subsidizeagriculture` | 0.05 | `CBuilding` productivity |
| 106 | Trade Warfare | `project_blockadeefficiency` / `…runnerefficiency` | 0.05 / 0.05 | `IIP` — blockade and blockade-running |
| 107 | Civil Order | `project_civilordercasualties` / `…raids` | −0.05 / −0.05 | `ImportExportUnitData`, `Regiment` — support loss from casualties and raids |
| 108 | Recruit Agents | `project_recruitagents` | 0.05 | `Regiment.groupintelligencegathering` |
| 109 | Recruitment Offices | `project_recruitment` | 0.05 | `Nation.recruitmentprojectfactortarget` |
| 113 | Farm Mechanization | `project_farmmechanizationcropsproduction` / `…recruitment` | 0.1 / 0.1 | `CBuilding` crops, `Nation` volunteers |
| 114 | Plantation Mechanization | `project_cottonmachinery` | 0.10 | `CBuilding` cotton |
| 118 | Training Manuals | `project_trainingmanuals` | 0.05 | `Regiment` training speed |
| 119 | Railroad Construction | `project_railroadconstruction` | 0.05 | `BattleUnits` construction speed |
| 120 | Improvised Shipyards | — | — | `GameVars` ~7862: enables building type **28** when level > 0 |
| 121 | Trade Deals | `project_sendenvoystradevolume` | 0.05 | `IIP` trade volume |
| 122 | Military Education | — | — | **a count, not a factor**: `militaryschools >= GetProjectLevel(122)` in `AICampaign` and `BuildingList` |

Shipped one-time pref values: `project_propagandainfl` 0.01,
`project_counterpropaganda` −0.01, `project_occupationadministration` 0.01,
`project_suppresspopulation` −0.01, `project_sendenvoysinterventionlevel` 0.05,
`project_cottonisking` 0.025, `project_cornisking` 0.025.

> **Project 98 Market Reform has no effect.** In `Economy.UpdateFilterMaps`
> (~1610) the market-capacity paint reads the project level and its pref and
> **discards both**:
>
> ```csharp
> if (BuildingType == CBuilding.id_market && constructiontimer <= 0f) {
>     _ = 1f;
>     GameVars.alliance[…].GetProjectLevel(98);   // bare expression, discarded
>     _ = GamePrefs.project_marketeffects;        // discarded
>     marketcapacity.Paint(pos, GamePrefs.marketinfluenceoninfastructure[level], …);
>     transportbottlenecks.Paint(pos, 0f - GamePrefs.marketinfluenceoninfastructure[level], …);
> }
> ```
>
> `GetProjectLevel(98)` has **exactly one occurrence in the assembly** — that
> one — and `project_marketeffects` has two: the discard and
> `Projects.GetProjectEffect` (~745), which generates the panel description. So
> the project is advertised and inert. The Market *building* works; only the
> project that claims to improve it does not.
>
> **A sweep over every `project_*` pref** for a consumer outside `GamePrefs`
> and the description generator finds `project_marketeffects` is the **only**
> one whose sole remaining use is a discarded statement. The other inert
> effects found so far — **114** entirely and **113**'s production half
> ([18](18-economy.md) §3) — fail differently, inside a condition that cannot
> be true, which no mechanical sweep catches. A reachability audit of the
> remaining ~120 project effects is still outstanding.

### 12.4 Pull — boolean, via `projects.Contains(id)`

| ids | Meaning |
| --- | --- |
| 110 Cavalry Reform | without it, a cavalry brigade's maximum size is multiplied by `project_nocavalryreformsizefactor` (**0.2**), and `Regiment` uses a lower echelon in the command hierarchy |
| 112 Artillery Reform / 124 6-gun Batteries | **either** suffices; without both, artillery max size × `project_noartilleryreformsizefactor` (**0.375**) and a lower echelon |
| 115 Supply Reform | checked in `CBuilding` ~3070 for the player's supply depots |
| 88 Command Reform **or** 89 Organization Reform | `Alliance.AllowsArmyGroups()` — with the alliance's `allowarmygroups` flag set, **either unlocks Army Groups** |
| 89 / 90 Organization Reform | `Alliance.HasOrganizationReform()` returns `projects.Contains(90)` when `GamePrefs.commandhierarchyshift == 0` and `projects.Contains(89)` when it is **−1**; any other value returns false |
| 111 Cavalry Reform II / 123 Horse Artillery | `Alliance.IsHorseArtilleryAvailable()` — **123 in scenario `002`, 111 everywhere else** |

Two things fall out of this.

**Why there are two "Organization Reform" projects.** `commandhierarchyshift`
ships **0** ([19](19-commanders-and-perks.md) §4), so **project 90 is the live
one and 89 is the variant for the −1 configuration**. They are the same reform
authored twice, one per hierarchy setting, and only one is ever reachable in a
given build. `HasOrganizationReform()` is what raises the `campaignranking`
clamp from 15 to 16 (`BattleUnits` ~6425) and applies
`project_organisationreformlowerconsumption` (**0.9**) to supply consumption —
so the "post-reform" caps in findings/19 and findings/20 are **project 90**.

**Army Groups are project-gated**, which closes the
[19](19-commanders-and-perks.md) open question about `allowarmygroups`: the
pref is necessary but not sufficient; Command Reform or Organization Reform
must also be held.

### 12.5 Projects absent from the player's effect summary

`Projects.UpdateAllProjectEffects()` builds the "active effects" text by
calling `GetProjectEffect(id, pref[, pref2])` for ids 91–109, 113, 114, 116–119
and 121. Missing: **88, 89, 90, 110, 111, 112, 115, 120, 122, 123, 124** — all
real, all with effects, none of which ever appear in that panel.

`GetProjectEffect` also formats project 97 as
`GetAbrevStrValue(−project_creditrating × level × 100, …) + "%"`, i.e. "+100 %"
per level, when the underlying quantity is a **rating notch count**. The
number shown is not a percentage of anything.

---

## 13. How the AI uses both systems

Both are part of the player's autopilot as well:
`GameVars.automanagepolicies` and `automanageprojects` route the player's side
through the same code ([20](20-ai-campaign.md) §8).

### 13.1 Personality agendas

`aipersonalities.dat` ([20](20-ai-campaign.md) §7) gives each personality
`prewarpolicies[20]`, `policies[80]` and `projects[100]` with a parallel
`projectsprob[100]`, all `-1`-padded. Record layout within the 317 lines:
alliance, id, name, description, `taxfocus[4]`, `subsidyfocus[8]`,
prewarpolicies (16–35), policies (36–115), project/weight pairs (116–315),
`projectspendingratio` (316).

The shipped roster, with the **first three** pre-war picks (the only ones
`chooseablegrandpolicies` reads) and the head of each policy queue:

| Side | id | Name | Pre-war 3 | Policy queue head |
| --- | --- | --- | --- | --- |
| Union | 0 | Historical | 76, 71, 78 | 35, 37, 38, 24, 47, 25, 33, 34 |
| Union | 1 | Industrial | 76, 79, 71 | 35, 37, 38, 24, 25, 26, 47, 41 |
| Union | 2 | Expansionist | 72, 71, 78 | 35, 37, 42, 48, 25, 22, 30, 38 |
| Union | 3 | Diplomatic | 74, 75, 70 | 32, 31, 30, 44, 25, 23, 29, 26 |
| Union | 4 | Militaristic | 71, 77, 78 | 48, 42, 36, 35, 43, 37, 38, 22 |
| Union | 5 | Agricultural | 75, 72, 71 | 32, 31, 30, 25, 37, 38, 35, 47 |
| Union | 6 | Economic | 76, 75, 79 | 26, 27, 28, 23, 29, 37, 38, 39 |
| Union | 7 | Abolitionist | 73, 71, 70 | 44, 34, 33, 36, 25, 37, 38, 47 |
| CSA | 0 | Historical | 175, 170, 178 | 136, 125, 143, 142, 137, 120, 131, 138 |
| CSA | 1 | Industrial | 176, 179, 170 | 134, 135, 133, 132, 129, 141, 136, 137 |
| CSA | 2 | Expansionist | 174, 171, 172 | 136, 125, 147, 137, 138, 135, 143, 142 |
| CSA | 3 | Diplomatic | 175, 178, 170 | 130, 131, 132, 136, 137, 141, 144, 142 |
| CSA | 4 | Militaristic | 173, 177, 178 | 136, 137, 138, 135, 122, 125, 147, 144 |
| CSA | 5 | King Cotton | 175, 171, 172 | 147, 143, 142, 131, 130, 129, 125, 136 |
| CSA | 6 | Economic | 175, 176, 179 | 141, 122, 123, 125, 126, 127, 128, 129 |
| CSA | 7 | Abolitionist | 176, 170, 177 | 134, 133, 132, 123, 141, 136, 137, 138 |
| both | 100 | Recruit Emergency | — (all `-1`) | militia/draft/bounty chain |
| both | 101 | Credit Emergency | — (all `-1`) | the finance act chain in order |

**17 of the 20 authored `prewarpolicies` slots per personality are never
read**, since only `chooseablegrandpolicies = 3` are taken.

Tax and subsidy focus, with `projectspendingratio`:

| Side | id | `taxfocus[4]` | `subsidyfocus[0..5]` | proj. spend |
| --- | --- | --- | --- | --- |
| Union | 0 Historical | 1 / .4 / .5 / 0 | .8 .7 .3 .8 .8 .2 | 50 % |
| CSA | 0 Historical | .4 / .8 / .5 / .2 | .8 .4 .7 .2 .7 .7 | 60 % |
| both | 1 Industrial | 1 / .5 / .6 / .4 | .8 .5 .2 1 .6 .4 | 35 % / 30 % |
| both | 2 Expansionist | 1 / .25 / .75 / .75 | 1 .5 .4 .5 .6 .3 | 45 % |
| both | 3 Diplomatic | .25 / .75 / 1 / 1 | .6 .5 .75 .75 .5 1 | 65 % |
| both | 4 Militaristic | 1 / 1 / 1 / 1 | .8 .75 .35 .6 1 .4 | 70 % |
| both | 5 Agricultural / King Cotton | .5 / .5 / 1 / .25 | .6 .5 1 .3 .75 .7 | 40 % |
| both | 6 Economic | .3 / .5 / .75 / 0 | .8 1 .3 .8 .5 .6 | 60 % |
| both | 7 Abolitionist | .5 / .75 / 1 / 1 | 1 .75 .3 .75 .9 .5 | 50 % |
| both | 100 Recruit Emerg. | −1 ×4 | 1 −1 −1 −1 1 −1 | 100 % |
| both | 101 Credit Emerg. | 1 / 1 / 1 / 1 | 1 1 0 0 0 0 | 100 % |

> **Only the Historical doctrine's economics differ between the two sides.**
> Ids 1–7 carry identical tax, subsidy and spending numbers for Union and
> Confederacy; the sides differ solely in their policy and project id lists
> (and in slot 5's name). `subsidyfocus[6]` and `[7]` are 0 in all twenty
> records, matching the two "n/a" categories of §9.1. The Recruit Emergency's
> four `-1` tax values are a leave-alone sentinel; `taxfocus[4]` is allocated
> as `float[5]` and only four are read ([20](20-ai-campaign.md) §7).

### 13.2 Policy selection

`Policies.CheckAIPolicyChange(alliance)` (~1801), skipped for the player
unless `automanagepolicies` or `ai_vs_ai`:

- if the personality is 100 or 101, launch the matching newspaper historical
  event — **115** (Union recruiting), **116** (Union credit), **117** (CSA
  recruiting), **118** (CSA credit) — and **cancel any policy in research that
  is not on the emergency list**;
- otherwise return immediately if anything is already in research;
- then walk `aIPersonality.policies` **in order** and research the **first
  eligible** one: not already held, `ReferedChapter <= CurrentChapter`,
  `HasPrePolicies()`, not deactivated, not blocked, available in the scenario.

So the AI's "doctrine" is literally an ordered queue with chapter gating doing
the reordering, and **the AI holds at most one policy in research at a time**
— never paying the `N^1.1` parallelism penalty the player can (§3.1).

Emergency triggers: `emergencyaipolicyrecruiting = 100`,
`emergencyaipolicycredit = 101`, `emergencyairecruitingtrigger = 4` (*"number
of small brigades, below"* — closing the [20](20-ai-campaign.md) open
question), `emergencyaicredittrigger = 0.4` rating.

### 13.3 Project selection

`AIPersonality.GetNextProjectRandom(alliance, subsidytype)` builds a weighted
ticket list — one entry per weight point, for every eligible project of that
subsidy type — and draws

```csharp
return list[UnityEngine.Random.Range(0, list.Count - 1)];
```

the max-exclusive slip again, so **one ticket at the end of the list is
unreachable**. `AICampaign.UpdateProjects` then appoints the drawn project once
`subsidyfunding[subsidytype]` covers its next-level cost.

A deterministic sibling, `AIPersonality.GetNextProject`, scores by
`weight − (held count / total held)` and **has no callers**.

---

## 14. Defects and quirks

1. **Ten policy pref arrays are dead** (§10): the Bread Basket, Industrialization
   and Military ladders each advertise effects they do not have, and the
   Military ladder has six of them.
2. **Support reason codes 9 and 33, and national-morale code 32, have labels
   and no producers** (§8). Code 9's call site survives as an empty `if` block.
3. **The Confederate CSCT/USCT brigade lists use state 20 (Minnesota) where
   the Regulars list uses 21 (Mississippi)** (§11.3) — a Union state in a
   Confederate recruiting list.
4. **`Random.Range(0, list.Count - 1)`** in the three brigade-creation policies
   and in `GetNextProjectRandom`, so the last candidate is never drawn.
5. **Policy 30 has two state lists that disagree** (§11.2).
6. **`DeactivatePolicyEffect` implements exactly one policy** (131), and does
   so by dividing rather than restoring, so repeated cancellation compounds
   (§6.2).
7. **Eleven projects never appear in the player's effect summary** (§12.5), and
   project 97's line formats a rating-notch count as a percentage.
8. **`GetNextProject` is dead** (§13.3).
9. **17 of 20 `prewarpolicies` slots per AI personality are never read** (§13.1).
10. `ImportPolicyData` contains a discarded `_ = 179;`.
11. Policy 76 scans `ConstructionSymbol`s without `includeInactive` while its
    Confederate twin 176 passes `includeInactive: true` (§11.4).
12. Policies 23 and 29 are the two halves of the same Bank Act line but report
    under **different support reason codes** (10 and 12).
13. The Confederacy has no Militia Act IV and no Lieutenant General Act, and
    its Militia Acts II and III are both gated to chapter 1 against the
    Union's 0 and 1, so its recruiting ladder is both shorter and later with
    no compensating entry (§2.3).
14. `repeating` is serialized with four different capitalisations in
    `projects.dat` (§9).
15. **A UI sprite array caps a simulation value**: `levelgraphics.Length`
    bounds the maximum level of every repeating project (§9.2).
16. **Acts bypass the policy-slot cap but still divide the research rate**
    (§3.2) — queueing acts is a way to slow your own research past the cap.
17. **The policy-slot counter is a lifetime budget presented as a queue
    width** (§3.2). `CanBeActivated` counts completed non-acts, and only acts
    are retired from `activatedpolicies`, so a nation may hold at most 12 of
    its 20 ladder levels — the top of three of five branches — and 7 if the
    politics subsidy is unfunded.
18. **Lowering the politics subsidy cancels completed policies and zeroes
    their whole prerequisite subtree** (§3.2). `RemoveResearch` walks the
    transitive closure from `GetListOfPrePolicies`; completed acts are spared,
    completed ladder levels are not. No warning, no undo.
19. **A policy's own `DurationText` omits every divisor its tooltip applies**
    (§3.1) — support, the `N^1.1` parallelism penalty and Administration
    Reform — so the panel shows two different day counts for the same policy
    and the larger, correct one is only on hover.
20. **`datefrom` gates nothing** (§9.3). Its only reader grants dated projects
    **free** at frame 32 of a new campaign, checking the category balance
    without debiting it, so a late-starting scenario is handed most of the
    dated project list and a 1861 scenario can buy anything it can afford
    whenever it likes.
21. **Completing a ladder level silently raises that category's spending**
    (§3.2b): `AdjustSubsidyFactor` defaults to not rescaling the slider, so the
    treasury bill scales with the new factor at an unchanged slider position.
    Defensible as design, invisible in the moment.

---

## 15. Hook seams

| Seam | Why |
| --- | --- |
| `Policies.LaunchPolicyEffect` | the push side for all 117 ids, no `default:` arm. **Postfix**, and respect the replay contract in §6.1 |
| `Policies.UpdatePolicyEffects` | the reset list. A mod that adds its own accumulator should reset it in a prefix here |
| `Policy.GetStatusByPolicyID` | the pull side. A postfix can make an unresearched id read as active, which is the cheapest way to test an effect |
| `Policy.ImportPolicyData` | prefix to redirect or synthesise policy metadata; the only way to change `ResearchDuration`, `ReferedChapter` or prerequisites for an id that already exists |
| `Policies.IsDeactivated` | the whole exclusion system is two `if`s. A postfix gives arbitrary mutual exclusion and chapter/date gating for free |
| `Policy.CheckForChapterUpdate` | chapter boundaries, Fort Sumter, and the war-start hook |
| `Projects.LoadedProjects.Import` | **no prefab needed** — a postfix can append entirely new project records, and the 56 `PLACEHOLDER` ids are free |
| `Projects.UpdateOneTimeProjectsEffects` / `UpdateProjectEffects` | the two push dispatchers; neither has a `default:` |
| `Alliance.GetProjectLevel(int)` | the pull side for every modifier project. One postfix changes every consumer at once |
| `Projects.IsAppointable` / `AppointProject` | cost and eligibility |
| `Policies.CheckAIPolicyChange` / `AIPersonality.GetNextProjectRandom` | the AI's agenda. Both are small and data-driven |
| `Tooltip.RegisterNewTooltip` | `public static`, appends to the live tooltip table ([21](21-ai-battle.md) §13) — how a new policy or project gets described |

**The honest answer to the Phase 3 gating question** ([09](09-policy-and-project-effects.md)):

- **Projects: yes.** `Import` is the whole definition, ids 20–29 and 42–87 are
  free, and all three effect dispatchers are patchable. A mod can ship a new
  project with no assets at all.
- **Policies: not by config, but yes at runtime.** `policies.dat` only
  overrides, so a new id cannot come from the file. But
  `Policy.InitializeStatics(bool skiplevelcheck = false)` is `public static`
  and rebuilds both lists from scratch:

  ```csharp
  Policy[] found = GameObject.Find("UI").GetComponentsInChildren<Policy>(includeInactive: true);
  allpolicies = new List<Policy>();  grandpolicies = new List<Policy>();
  // a policy whose GRANDPARENT GameObject is named "GrandPolicies" is also a grand policy
  staticsinitialized = true;
  ImportPolicyData();
  ```

  So the registration path is: clone an existing policy GameObject under the
  `UI` hierarchy, rewrite `UniquePolicyID` and the inspector fields, then call
  `Policy.InitializeStatics()`. Three caveats to verify on a live build: the
  clone is discovered only if it sits under `UI` (or the
  `UICampaign/Menu/CampaignPanel/Background/GrandPolicies` fallback);
  `InitializeStatics` immediately runs `ImportPolicyData`, which will
  **overwrite the clone's fields** if `policies.dat` carries that id; and
  grand-policy status is decided by the **grandparent object's name**, not by
  a field. Behaviour then comes from a `LaunchPolicyEffect` postfix and text
  from `Tooltip.RegisterNewTooltip`.

---

## 16. Open questions

1. **Whether a runtime-cloned `Policy` survives.** §15 gives the registration
   path from reading `InitializeStatics`, but three things need a live build to
   confirm: that a cloned component under `UI` is found, that
   `markedasgrandpolicy` and the `ButtonTooltipRef` wiring survive cloning, and
   what the policy panel does with a button it has no layout slot for.
2. `Policy.transform.parent.gameObject.name` carries the alliance
   (`GetAllianceFromPolicy`). The scene hierarchy under the policy panel is
   undocumented; an asset dump would settle what parents exist besides
   `"Choose"`.
3. `taxcaps[5]` — which index is which tax. `tax_tarifs`, `tax_income`,
   `tax_corporate`, `tax_landsales` are named; the fifth is not, and the
   shipped defaults are `{0.3, 0.05, 0, 0, 0.2}`. Belongs with the taxation
   question still open in [18-economy](18-economy.md).
4. ~~`subsidyfunding[]` vs `subsidies[]` vs `subsidiesfactor[]` — how funding
   accrues per category, and what `AdjustSubsidyFunding` does on overdraft.~~
   **Done** — §3.2b. `subsidies[]` is the player's slider, `subsidiesfactor[]`
   the ladder multiplier, and `subsidyfunding[]` accrues at
   `slider × basicsubsidiescost × factor` per annum from the alliance pass,
   which is also exactly what the treasury pays. `AdjustSubsidyFunding` floors
   at zero rather than overdrawing, and `AppointProject` is the only debit —
   `CheckProjectUnlocks` is the one path that adds a project without one
   (§9.3). What is still untraced is the **length of `levelgraphics`**, which
   caps every repeating project's level (§9.2) and is inspector data.
5. `Alliance.ratingmodifier` is reset to `0` as an `int` but the credit-rating
   consumers in [18-economy](18-economy.md) treat notches as floats in places.
   Worth checking for truncation.
6. `GameVars.alliance[].maxslaveryfactor` (policies 70, 132) has no documented
   consumer yet.
7. ~~`CampaignObjective` writes support codes 7 and 8 and national-morale codes
   6–12 and 14.~~ **Done** — [24-campaign-objectives](24-campaign-objectives.md)
   maps all 38 objectives and every reason code they write (national morale
   4–12 and 14, support 7, 8 and 41), including the three sites for support
   code 8 that §8.1 here inferred: objective 14 → state 39, 15 → state 14,
   16 → state 22, each a **snap up to a 0.75 floor** rather than an increment.
8. ~~`historicalevents.dat` records carry a `policyid`
   (`Newspaper.cs:196`), so the newspaper is policy-driven. The event catalogue
   — including ids 115–118 used by the AI emergencies and the chapter events
   0–3 — is unmapped.~~ **Done** —
   [25-newspaper-and-events](25-newspaper-and-events.md) maps all 119 records,
   the fifteen gating policies (8, 25, 33, 34, 36, 39, 71, 76, 78, 143, 144,
   170, 174, 175, 178) and every script-launched id. Two notes for this file:
   the gate calls `Policy.GetStatusByPolicyID` with its alliance argument
   **defaulted**, so it reduces to `researchprogress >= 1f` on the shared
   policy object and is alliance-agnostic; and the emergency path at
   `Policies` ~1842 is one of the four call sites that pass
   `includeactsinresearch` / `includegrandpolicies` to
   `GetActivatedPolicies`, which [23](23-game-state.md) §7.6 found ignores
   both.
9. `DLC_WL.CalculatePrestigeCostPolicy` / `…Project` — the career-mode
   currency replaces subsidies and research entirely. DLC-only, so low
   priority.
10. Whether `UpdateOneTimeProjectsEffects` is re-run on save load for already
    held projects. If it is, the support steps from 91–94 would reapply; if it
    is not, projects 110/112's `overridesymbol` rewrite must be persisted. The
    save path in [08-save-load-surface](08-save-load-surface.md) does not say.
