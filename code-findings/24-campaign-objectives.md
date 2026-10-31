# Campaign objectives

**Build:** see [00-build-provenance](00-build-provenance.md)
**Source:** `decompiled/Assembly-CSharp/CampaignObjective.cs` (1,158 lines),
`BattlefieldSetup.cs`, `Frontline2.cs`; `game/Config/campaignobjectives.dat`,
`game/Config/campaignprefs.txt`
**Summary:** [summary-campaign-objectives](summary-campaign-objectives.md)

The last untouched campaign subsystem. `CampaignObjective` is where the war's
narrative arc is encoded: it decides when the campaign ends, drives the
chapter machine, writes nine of the national-morale reason codes and three of
the support codes, and holds the trigger for foreign intervention.

---

## 1. Architecture: data-driven, unlike `Policy`

findings/22 found two architectures for one feature — a `Policy` is an
inspector-authored prefab that config only *overrides*, while a project is
defined entirely by its data file. **`CampaignObjective` is the project
model**: one prefab, instantiated once per record, with every field filled
from the file.

```csharp
// InitializeObjectives(GameObject prefab, GameObject parentfolder)
StreamReader streamReader = new StreamReader("config/campaignobjectives.dat");
int num = int.Parse(streamReader.ReadLine());
for (int i = 0; i < num; i++) { … Object.Instantiate(prefab) … }
```

Three things about that line:

- **The path is hardcoded, lowercase, and global.** No scenario folder is
  consulted, so the whole-file override scheme of findings/05 does not apply —
  `Config/campaignobjectives.dat` is shared by every scenario, and per-scenario
  selection happens through the record's own `ObjectiveScenario` field instead.
- **It is the one loader in the file set that omits `Encoding.Default`.**
  Every other importer passes it explicitly; `StreamReader(string)` defaults to
  UTF-8, so the Windows-1252 bytes in the shipped prose decode as U+FFFD. It
  is visible in the shipped text — objective 0's newspaper body contains
  "the Union<?>s victorious forces".
- Objective names resolve with `Town.GetTownFromGameObjectName` and then
  `IIP.GetIIPFromObjectName`, **by string**, and a name that resolves to
  neither is **silently dropped** — the same failure mode as
  `Tools.GetIntPartitions` (findings/06). An objective whose whole town list
  fails to resolve becomes `objectives.Count == 0`, which changes its
  evaluation path entirely (§5.3).

### 1.1 Record layout — 20 lines

| # | Field | Note |
| --- | --- | --- |
| 1 | `UniqueObjectiveID` | |
| 2 | `ObjectiveName` | |
| 3 | `ObjectiveTooltipHeadline` | |
| 4 | `ObjectiveTooltip` | |
| 5 | `ObjectiveChapters` | 4-char groups, `Substring(j*4, 3)`; **empty = all chapters** |
| 6 | `ObjectiveAlliance` | 0 Union, 1 Confederacy |
| 7 | `ObjectiveScenario` | `Tools.GetStringPartitions`, matched against `GamePrefs.leveltoload` |
| 8 | `validfromdate` | `Tools.Date.ConvertString` |
| 9 | `validtodate` | |
| 10 | town/IIP list | `Tools.GetStringSeparation`, resolved by name |
| 11 | prerequisite ids | `Tools.GetIntPartitions`, resolved in a **second pass** |
| 12 | `influenceprestige` | DLC prestige award |
| 13–17 | `Headlines[5]` | newspaper variants |
| 18 | `PictureID` | |
| 19 | `HeadlinePicture` | |
| 20 | `Content` | newspaper body, `\n`/`\t` expanded by `Tools.TransformToLineBreaks` |

Verified: `1 + 38 × 20 = 761` lines in `Config/campaignobjectives.dat`. ✔

Records whose `ObjectiveScenario` does not contain the current
`GamePrefs.leveltoload` are `Destroy`ed and removed from `allcampaignobjectives`
at the end of initialization — so the live list is scenario-filtered, while
`IsDeactivated` *also* re-tests the same condition at runtime.

### 1.2 `Awake()` resets the global war progress

Every instance's `Awake` clears the eleven statics that track war progress:

```
confederacyinvaded, unionfirstvictory, csafirstvictory, unioncapturedforts,
csacapturedtowns, csavictoryenemysoil, consecutivevictories[4],
winmajorbattleintervention = -1, victoryagainstgb, victoryagainstfr,
unioncasualties
```

During `InitializeObjectives` that happens 38 times in a row, which is
harmless because nothing has accumulated yet. It is a hazard for a mod:
**instantiating a `CampaignObjective` prefab at runtime wipes the whole war's
progress**, including the intervention trigger. Add objectives through the data
file, or clone and restore the statics around the call.

`Start()` and `Update()` are both empty — evaluation is driven externally by
`CheckAccomplishmentOfAllObjectives()`.

---

## 2. The 38 objectives

Two distinct sets, split cleanly by scenario.

### 2.1 Main campaign (`001`) — ids 0–28

Strategic war goals. **All have an empty town list and `influenceprestige = 0`**,
and every one is hardcoded as its own `if (UniqueObjectiveID == N)` block in
`CheckIfAccomplished`.

| id | Name | Side | Chapters | Condition |
| --- | --- | --- | --- | --- |
| 0 | Break Confederate Morale | 0 | 0–3 | `alliance[1].nationalmorale < breakmoraletrigger (0.25)` → **campaign ends** |
| 1 | Break Union Morale | 1 | 0–3 | mirror, on `alliance[0]` |
| 2 | Capture Montgomery, AL | 0 | 0–3 | CSA capital; live **only while policy 170 is not researched** |
| 3 | Capture Richmond, VA | 0 | 0–3 | live **only once policy 170 is researched** and Virginia is not Union |
| 4 | Capture Washington, D.C. | 1 | 0–3 | |
| 5 | Pre-emptive Action | 0 | 0 | gated to **on or after 14 Apr 1861** |
| 6 | Invade the Confederacy | 0 | 1 | |
| 7 | Win a Major Battle | 0 | 1 | deactivated once `csafirstvictory` |
| 8 | Win a Major Battle | 1 | 1 | deactivated once `unionfirstvictory` |
| 9 | Maintain Support Above 60 | 0 | 1 | **the 1862 midterms**, 4 Nov: `averagesupport >= republicanmajoritysupportlevel (0.60)`, §3.1 |
| 10 | Union Support Below 60 | 1 | 1 | mirror |
| 11 | Capture 10 Forts | 0 | 1 | `unioncapturedforts >= 10` |
| 12 | Capture 3 Cities | 1 | 1–2 | `csacapturedtowns >= 3` |
| 13 | Invade the North | 1 | 1 | |
| 14 | Capture Western Virginia | 0 | 1 | off if Virginia is Union or sublevel `E`; **selects state set 4** |
| 15 | Capture Kentucky | 1 | 1–3 | off if state 14 is already CSA **or** its CSA support < 0.45 |
| 16 | Capture Missouri | 1 | 1–3 | same for state 22 |
| 17 | Cut Confederacy in Half | 0 | 1–2 | off in sublevel `E` |
| 18 / 19 | Win 5 Consecutive Land Battles | 0 / 1 | 1–3 | `consecutivelandbattlesamount = 5` |
| 20 / 21 | Win 5 Consecutive Naval Battles | 0 / 1 | 1–3 | `consecutiveseabattlesamount = 5` |
| 22 | Defeat the British | 0 | 1–3 | requires GB at war with the Union |
| 23 | Defeat the French | 0 | 1–3 | requires France at war with the Union |
| 24 | Gain a Major Victory | 0 | 1–3 | **intervention trigger**, §4 |
| 25 | Gain a Major Victory | 1 | 1–3 | **intervention trigger**, §4 |
| 26 | Maintain Morale Above 50 | 0 | 2 | **the 1864 election**, §3.2 |
| 27 | Union Morale below 50 | 1 | 2 | mirror |
| 28 | Inflict 50,000 casualties | 1 | 3 | `unioncasualties >= bleedthemoutcasualties (50,000)` |

Objectives 2 and 3 are the **Confederate capital move modelled as a policy
switch**: policy 170 ("The Old Dominion", the one findings/22 found gated on
Virginia still being Union) flips which city the Union must take.

**Objectives 0 and 1 are the game's only loss condition.** Their chapter list
is 0–3, so they are live from the first day of a campaign to the last, and each
tests nothing but the other alliance's `nationalmorale` against
`breakmoraletrigger = 0.25` before calling `SetEndOfCampaign`. Nothing
anywhere ends a campaign on a calendar date: the chapter machine has three
transitions and no fourth chapter (findings/22 §4), and the one date-driven
end-of-campaign call in the file — objective 27's 4 March 1865 branch — is
unreachable (§3.2). **A war that neither side is losing runs indefinitely.**
Both objectives hold the end of the campaign until the newspaper queue drains,
which is the only consumer of `Newspaper.IsNewspaperOpened()`
([25](25-newspaper-and-events.md)).

### 2.2 Scenario `002` — ids 29–37

A different shape entirely: **town/IIP lists, no id-specific logic (except
29/30), and prestige awards of 500–2,500.** Prestige is paid through
`DLC_WL.AdjustPrestige`, gated on `DLC_WL.dlc_scenarioactive` and on the
objective's alliance matching the chosen commander's — so these are the
DLC theatre campaign's objectives.

| id | Name | Side | Valid from | Pre | Prestige | Objectives |
| --- | --- | --- | --- | --- | --- | --- |
| 29 | West Virginia | 0 | 1850 | — | 500 | Grafton, Charleston, Wheeling, Parkersburg, Lewisburg, Beverly, Romney |
| 30 | West Virginia | 1 | 1850 | — | 1000 | Charleston, Grafton, Lewisburg, Romney |
| 31 | Shenandoah Valley | 0 | 1850 | — | 1000 | Winchester, Martinsburg, Strasburg, New Market |
| 32 | Baltimore & Ohio Lines | 1 | 1850 | — | 1000 | Cumberland, Hagerstown, Berkeley Springs, Martinsburg, Harpers Ferry |
| 33 | Maryland Campaign | 1 | 1862 | 32 | 1000 | Hagerstown, Frederick |
| 34 | Pennsylvania Campaign | 1 | **1863** | 32 | **2500** | Hagerstown, Harrisburg, Chambersburg, Carlisle, Gettysburg, York |
| 35 | Coastal Operations | 0 | 1862 | — | 1000 | New Bern, Plymouth |
| 36 | Salt Wars | 0 | 1862 | 29 | 500 | Saltville |
| 37 | Invasion of Norfolk | 0 | 1862 | — | 1000 | Norfolk, Portsmouth, Suffolk |

Objectives 29 and 30 are the only pair that is **mutually exclusive and
self-deactivating**: each is switched off by the other's accomplishment *and by
its own*. On completion they call

```csharp
GameVars.SetNationToNewAlliance(39, 0 /* or 1 */, handovertowns: false,
                                handoverbuildings: false, handoveriips: false);
```

— **West Virginia (state 39) changes sides without any property changing
hands**, plus a symmetric ±`dlcwl_westvirginiasupportchange` on both of state
39's support slots under reason code **41** ("Objective: West Virginia", the
last entry in `supportdescr`).

---

## 3. Two elections

### 3.1 The 1862 midterms — objective 9, and its one-day window

Objective 9 "Maintain Support Above 60" keys off **4 November 1862**, the real
midterm date, and tests `GameVars.alliance[0].averagesupport` — the
population-weighted figure from [17](17-politics-and-support.md) §2 — against
`republicanmajoritysupportlevel = 0.60`. Before the date it can only be marked
*failed*; on and after it, meeting the threshold completes it, and failing it
charges `republicanmajorityunionmorale` (−0.01) per week under national-morale
code 8:

```csharp
if (!accomplished && date >= Date(4, 11, 1862))
    alliance[0].AdjustNationalMorale(8, republicanmajorityunionmorale * num * 52f);
    // num = date − lastupdate, in campaign years
```

**The rate is −0.52 per annum, and it runs for about a day.** The objective's
`ObjectiveChapters` is `{1}`, and `CheckForChapterUpdate` raises the chapter to
2 once the date is strictly past `Date(5, 11, 1862)`, after which the generic
chapter gate (§5.1) deactivates the objective and the charge stops.

The two thresholds do not land at midnight, because the two date systems
measure a day differently. `Tools.Date.yearfraction` is `year + (days in the
months before) / 365 + day / daysInMonth / 12`; `GetCampaignFloatingDate()` is
`year + (month − 1) / 12 + (day − 1) / 365.25 + hour / 8766`. For 1862 that
puts *(calculated)*:

| Threshold | Code | Falls on the campaign clock at |
| --- | --- | --- |
| Objective 9 starts charging | `date >= Date(4, 11, 1862)` | about **21:24 on 4 November** |
| Chapter 2 begins | `date > Date(5, 11, 1862)` | about **21:45 on 5 November** |

So the charge runs for about **24 hours**, and the reachable total is
`0.52 × 24.4 / 8766` ≈ **0.0015**, about a sixth of a point of national
morale. An earlier version of this section said two days and 0.003, reading
both dates as midnights.

> **Corrects an earlier reading of this file**, which said the penalty runs
> "for the rest of the campaign" — that is what the config label describes and
> what the formula would do, but the chapter list prevents it. Either the
> chapter list or the label is wrong; as shipped, **the 1862 midterms have no
> mechanical consequence.**

Its Confederate mirror, objective 10 "Union Support Below 60 / Union
Elections", has no effect term at all: it latches `accomplished` from the same
threshold before the date and is scored only through `influenceprestige`.

### 3.2 The 1864 election is the Union's hard loss condition

Objectives 26 and 27 are not scored like the others. Both key off
**9 November 1864**, plus one day (`0.002739726f` = 1/365):

```
before election day + 1
    26: accomplished  = (alliance[0].nationalmorale >= lincolnmaintainmoraleabove 0.50)
    27: accomplished  = false
    (and flag2 = false before the election date itself, which suppresses the
     newspaper message for an objective that is merely "currently true")

on or after election day + 1, while not accomplished
    one-shot, guarded by lastupdate < electionDate + 1:
        26: alliance[0].AdjustNationalMorale(8, lincolninfluenceunionmorale −0.20)
        27: alliance[0].AdjustNationalMorale(8, demoralizevotersunionmoraleinfluence −0.20)
    27 additionally sets accomplished when nationalmorale < demoralizevotersunionmoralelevel 0.50

    if the date passes 4 March 1865:
        bunits.SetEndOfCampaign(playeralliance == 0 ? 1 : 0)   // the Union loses
```

`SetEndOfCampaign`'s argument is **player-relative** — 0 prints "Victory!", 1
prints "Defeat!" — so `playeralliance == 0 ? 1 : 0` is a Confederate win
whichever side the player is on.

So: **hold Union national morale at or above 0.50 on 9 November 1864 or Lincoln
loses re-election.** Three things follow that an earlier reading of this file
got wrong.

**The penalty is −0.40, not −0.20.** Both objectives are live in the same
chapter, both are in their `!accomplished` branch on the first pass after the
election when morale is below 0.50, and `CheckAccomplishmentOfAllObjectives`
walks them in id order in a single loop — so objective 26 charges −0.20 and
objective 27 charges another −0.20 immediately after. Both tooltips read
*"Union national morale -20"*.

**It is therefore an immediate loss, not a four-month countdown.** Lincoln
loses only when morale is already under 0.50, so after −0.40 it is under 0.10,
well below `breakmoraletrigger = 0.25`. Objective 1 "Break Union Morale" is
evaluated in the same sweep, which runs every `updatetimeforcampaigndata =
0.00002` campaign years — about **ten game minutes**
([10](10-simulation-tick.md) §4). The campaign ends in a Confederate victory on
the next pass, not on inauguration day.

**And the 4 March 1865 branch is unreachable.** If Lincoln *held*,
`Policy.CheckForChapterUpdate` — called in the statement immediately before the
objective sweep (`BattleUnits` ~1549) — raises the chapter to 3 as soon as the
date passes 9 November 1864, because its condition is exactly
`GetObjectiveAccomplishment(26) && !GetObjectiveAccomplishment(27)`. Both
election objectives have `ObjectiveChapters = {2}`, so they deactivate before
their own post-election branch ever runs: neither −0.20 fires and no
end-of-campaign is set. If Lincoln *lost*, objective 1 has already ended the
campaign. Objective 27's copy of the `SetEndOfCampaign` call is dead either
way — it fires only while 27 is *not* accomplished, i.e. only while Union
morale is *above* 0.50, which is the branch the chapter advance switches off.

> **For a player:** 9 November 1864 is a cliff at 50 national morale, not a
> −20 event to absorb. For a modder: the two prefs are
> `lincolninfluenceunionmorale` and `demoralizevotersunionmoraleinfluence`,
> both −0.20, and zeroing either restores the −20 the tooltips promise.

This is the only loss condition in the game that is a *date* rather than a
threshold, and it is invisible in the objective's own tooltip text.

---

## 4. Foreign intervention: the other half of the script

findings/20 found that the intervention script requires **both**
`GetInterventionProbability >= 1` **and**
`CampaignObjective.winmajorbattleintervention == <side>`. This is where the
second condition is produced and consumed.

`winmajorbattleintervention` is a `static int`, reset to −1 in `Awake`, written
by the battle-result path, and **saved and loaded** with the objective state
(`LoadAll`/`SaveAll`). Objectives 24 and 25 read it:

```csharp
if (UniqueObjectiveID == 24 && !deactivated && !alreadyAccomplished) {
    if (winmajorbattleintervention == 0) {
        accomplished = true;
        GameVars.alliance[0].AdjustInterventionLevel(winbattleinterventionlevelunion −0.10);
    }
}
// 25: winmajorbattleintervention == 1 → alliance[1], winbattleinterventionlevelcsa +0.25
```

Both are deactivated outright if **any** of Great Britain, France or Spain is
already at war with either belligerent — they are strictly *pre*-intervention
objectives — and each needs its own patron close to committing:

| id | Side | Extra gate |
| --- | --- | --- |
| 24 | Union | `GetInterventionProbability(4) >= 0.75` — **Spain** |
| 25 | Confederacy | `GetInterventionProbability(2) >= 0.75` **or** `(3) >= 0.75` — Great Britain or France |

That matches [17](17-politics-and-support.md) §4: Spain intervenes for the
Union, Britain and France for the Confederacy.

> **But the two payoffs are not mirror images, and the Union's has the wrong
> sign.** Each applies to its *own* alliance's accumulator, and the probability
> is a difference: `P(GB) = CSAlevel − Unionlevel`. Objective 25's `+0.25` on
> the Confederate accumulator raises British and French intervention, as
> "Encourage the Europeans" intends. Objective 24's `−0.10` on the **Union's**
> accumulator *lowers* `P(Spain for Union)` — the very figure its own
> `>= 0.75` gate waits for — and *raises* British and French intervention
> against the Union. So **both sides' major victories push Europe toward the
> Confederacy**, the Confederacy's 2.5× harder than the Union's, and with the
> floor at zero ([17](17-politics-and-support.md) §4a) the Union's term is
> often discarded entirely. Either `winbattleinterventionlevelunion` should be
> `+0.10` or the objective's subtitle "Discourage the Europeans" is wrong; the
> shipped combination is self-defeating under both readings.

A "major battle" is defined in `campaignprefs.txt` as **each army at least
10,000 strong and at least 2,000 total casualties**
(`minsizemajorbattle`, `mincasualtiesmajorbattle`), and the winning army's
commander also gains **+0.50 fame** on a first major victory.

---

## 5. Evaluation

### 5.1 `IsDeactivated()` — the generic gates

Recomputed at the top of every `CheckIfAccomplished`:

1. `ObjectiveChapters` does not contain `Policy.CurrentChapter` — **unless the
   list is empty**, in which case the objective is valid in every chapter
   (`list.Count > 0` is part of the test).
2. `ObjectiveScenario` does not contain `GamePrefs.leveltoload`.
3. The campaign date is outside `validfromdate … validtodate`.
4. Any id-specific rule from §2.
5. **Any prerequisite in `precampaignobjectives` is not accomplished.**

Everything else in the class treats `deactivated` as "do not evaluate", so a
deactivated objective keeps whatever `accomplished` value it last had.

### 5.2 Chapter coupling

findings/22 §3 has chapter 3 arriving on 9 November 1864 "with objective 26
done and 27 not". Both of those are the election objectives of §3.2, so the
chapter machine and the Union's loss condition read the same two flags on the
same date. `GetObjectiveAccomplishment(uniqueid, bool usechaptercheck = true)`
is the accessor; the `usechaptercheck: false` form is what callers outside the
chapter machine use, notably `BattlefieldSetup`'s state-set selection (§6).

### 5.3 The generic town-list path

For the nine scenario-`002` objectives, accomplishment is uniform:

```csharp
if (objectives.Count > 0 && !deactivated) {
    bool all = every Town.Owner == ObjectiveAlliance
            && every IIP.allianceowner == ObjectiveAlliance;
    if (!all)        accomplished = false;
    else if (!flag3) accomplished = true;     // flag3 = "an id-specific block ran"
}
```

So the town list can only ever **veto** an objective that an id-specific block
has already decided, and it can only *grant* one that has no id-specific block.
Objectives 0–28 have empty lists, so the two paths never overlap — except for
29 and 30, whose id-specific block sets `accomplished = true` unconditionally
and is then immediately corrected by the town check. That block is therefore
**redundant in the shipped data**, and actively dangerous if someone empties
29 or 30's town list: with `objectives.Count == 0` the veto never runs and the
objective completes the moment it becomes valid, handing West Virginia over.

### 5.4 Dispatch

`CheckIfAccomplished` is **not** a `switch`. It is a flat chain of about
thirty-eight `if (UniqueObjectiveID == N && !deactivated)` blocks, every one
of which is evaluated on every call, for every objective. With 38 objectives
that is ~1,400 integer comparisons per sweep — cheap, but it means a hook
cannot intercept "the block for objective N"; there is only the whole method.
It is the same no-dispatch shape as findings/09's `default`-less switch in
`Policies.LaunchPolicyEffect`.

---

## 6. Reason codes written here

This closes the last gaps in findings/17's and findings/22's tables. The three
codes this file does *not* write — **1 Battles, 2 Naval engagements, 3 Town
occupation**, the ones the player generates by playing — are in
[17](17-politics-and-support.md) §3a.

### 6.1 National morale (`AdjustNationalMorale(code, value)`)

| Code | Label | Written by | Pref | Value |
| --- | --- | --- | --- | --- |
| 4 | Loss of capital | obj 2, 3 (→ CSA), 4 (→ Union) | `capturecapitalnationalmorale` | −0.20 |
| 5 | Militia Act not triggered | obj 5 | `militiaactmoraledrop` | −0.01 **per day** |
| 6 | Enemy on own soil | obj 6 | `invadeconfederacyunionmoraledrop` | −0.005 **per week** |
| 7 | First major battle | obj 7 / 8 | `winfirstmajorbattleinfluenceunion` / `csawin…` | +0.025 / −0.05 |
| 8 | Elections | obj 9 (rate, two days only), 26, 27 (**both** fire) | `republicanmajorityunionmorale`, `lincolninfluenceunionmorale`, `demoralizevotersunionmoraleinfluence` | −0.01/wk, −0.20, −0.20 |
| 9 | Captured forts / cities | obj 11 / 12 | `capturefortsnationalmorale` / `capturecitiesnationalmorale` | +0.05 each |
| 10 | Battles won | obj 13, 18–23 | `invadenorthunionmorale`, `consecutivelandbattlesmorale`, `consecutiveseabattlesmorale`, `winbattleagainsteurope` | −0.025, +0.05, +0.025, +0.025 |
| 11 | States seceding | obj 14, 15, 16 | `westvirginiacsanationalmorale`, `secedeunionmorale` | −0.025, −0.05 |
| 12 | Casualties | obj 28 | `bleedthemoutinfluencenationalmorale` | −0.10 |
| 14 | Confederacy cut in half | obj 17 | `confederacyhalfunionmorale` | +0.05 |

Codes 5, 6 and 9 (objective 9's arm) are **rates**, multiplied by the elapsed
campaign time since `lastupdate` and rescaled — `× 365` for the per-day pref
and `× 52` for the per-week ones. The rest are one-shots, guarded by the
`!alreadyAccomplished` test.

Note **objective 13 ("Invade the North") and the consecutive-battle and
anti-European objectives all share code 10**, so the player's morale breakdown
collapses four different mechanics into one line.

### 6.2 State support (`AdjustSupport(alliance, value, code)`)

| Code | Label | Written by |
| --- | --- | --- |
| 7 | Enemy on own soil | obj 6, `invadeconfederacycsasupport` +0.01/week for alliance 1 **in all slave states** while the Confederacy is invaded |
| 8 | State secession | obj 14 → state 39 for the Union; obj 15 → state 14; obj 16 → state 22 |
| 41 | Objective: West Virginia | obj 29 / 30, DLC, symmetric ± on state 39 |

Code 8's form is a **snap to a floor, not an increment**:

```csharp
GameVars.nation[14].AdjustSupport(1, Mathf.Max(0f, secedecsasupport − nation[14].support[1]), 8);
```

with `secedecsasupport = 0.75`. So Kentucky's and Missouri's Confederate
support jumps to 0.75 if it is below that and is left alone if above — a
one-way ratchet expressed through the normal clamped adder. This is the
mechanism findings/22 §8.1 inferred for "code 8 *is* written by
`CampaignObjective` for states 39/14/22"; here are the three sites and the
shape.

---

## 7. State indices confirmed

Spot-checked against `game/Campaigns/001/A/nations.dat` and the table in
findings/22 §11.1: **14 Kentucky, 22 Missouri, 38 Virginia, 39 West Virginia.**
Objectives 14/15/16 and 29/30 index them as bare integers, and objective 3's
deactivation reads `GameVars.nation[38].alliance == 0`.

---

## 8. The state-set rename, live

findings/23 §4.1 flagged `Nation.statesetname[6]` as a latent trap:
`BattlefieldSetup` rewrites `nation[i].name` whenever the active state set
changes, breaking everything that matches a state by string. The shipped data
makes it **real, not hypothetical** — `Campaigns/001/A/nations.dat` fills it
for two states:

| State | set 0 | set 1 | set 2 | set 3 | sets 4–5 |
| --- | --- | --- | --- | --- | --- |
| Kansas | *(blank)* | Kansas Territory | Kansas Territory | Kansas | *(blank)* |
| Minnesota | Minnesota Territory | Minnesota Territory | Minnesota | *(blank)* | *(blank)* |

The set is chosen in `BattlefieldSetup.UpdateStateOccupation`:

```csharp
int num = frontline2script.GetStateSetIDFromDate(GetCampaignFloatingDate());
if (leveltoload == "001") {
    if (CampaignObjective.GetObjectiveAccomplishment(14, usechaptercheck: false)) num = 4;
    else if (num > 3 && subleveltoload != "D" && subleveltoload != "E")          num = 3;
}
if (leveltoload == "002")
    num = GetObjectiveAccomplishment(29, usechaptercheck: false) ? 1 : 0;
```

so **campaign objectives choose the map's state set**: objective 14 ("Capture
Western Virginia") forces set 4 in the main campaign, and objective 29 flips
scenario 002 between sets 0 and 1.

`GetStateSetIDFromDate` walks `Frontline2.FrontlineTextureDateToBeUsedFrom`, an
inspector-serialized string array that is **not in the assembly** — reading the
actual cut-over dates needs an asset dump. The names are consistent with
chronological sets (Minnesota was admitted in 1858, Kansas in January 1861), so
sets 0–2 are antebellum and the 1861 campaign starts at set 3.

**That is why the trap has not bitten in the shipped game.** Policy 30's
volunteer bonus (findings/17, findings/23 §5.2) matches the literal string
`"Kansas"`, and at set 3 and above the name *is* "Kansas". A blank
`statesetname` entry leaves the previous name in place rather than restoring
the authored one, so once set 3 has been reached Kansas stays "Kansas" through
sets 4 and 5 as well. A scenario that starts earlier, or that fills more
`statesetname` slots, would silently disable the bonus.

---

## 9. Persistence

`SaveAll(folder)` / `LoadAll(folder)` round-trip per objective `accomplished`,
`deactivated` and `lastupdate`, plus the eleven global statics including
`winmajorbattleintervention`. Because `Awake` zeroes those statics and
`InitializeObjectives` runs before the load, the ordering is load-after-init
and therefore correct — but it is the reason the §1.2 hazard matters.

---

## 10. Mod seams

1. **`Config/campaignobjectives.dat` is additive for the *data* half.** New
   records get a prefab, chapters, dates, scenario filter, prerequisites,
   newspaper text and prestige for free. What they do **not** get is a win
   condition: an id with no block in `CheckIfAccomplished` and an empty town
   list is evaluated by nothing and can never complete.
2. **A new objective with a town/IIP list works with no code at all** — the
   generic path of §5.3 handles it. This is the cheapest way to add content,
   and it is how all nine scenario-`002` objectives work.
3. **`CheckIfAccomplished` is the hook for a new scripted condition**, as a
   postfix keyed on `UniqueObjectiveID`. Set `accomplished` and let the base
   method's tail fire the newspaper and prestige.
4. **`GetObjectiveAccomplishment(int, bool)` and `GetObjectiveFromID(int)`**
   are `public static` and the natural read API for the ModApi to re-export.
5. **`LaunchNewspaperMessage(int)` is `public static`** — usable on its own
   for mod-authored events, independently of objectives.
6. Avoid instantiating the prefab (§1.2).

---

## 11. Defects and oddities

1. **The loader omits `Encoding.Default`** (§1), corrupting every non-ASCII
   character in the shipped newspaper prose. A one-argument fix.
2. **`Awake` resets global war progress per instance** (§1.2).
3. **Objectives 29/30's id-specific block is redundant** and becomes a
   give-away-West-Virginia bug if their town lists are emptied (§5.3).
4. **Four unrelated mechanics share national morale code 10** (§6.1), so the
   player's breakdown cannot distinguish an invasion of the North from a naval
   winning streak.
5. **Unresolvable town names are silently dropped** at load (§1), and a list
   that empties completely flips the objective onto a different evaluation
   path rather than failing loudly.
6. **The intervention payoff is asymmetric by 2.5× and the Union's half has
   the wrong sign** (§4): objective 24's −0.10 on the Union's own accumulator
   lowers the Spanish probability its own gate waits for, and raises the
   British and French ones.
7. **`CheckIfAccomplished` evaluates all 38 id tests per objective** (§5.4).
8. **The 1864 election charges its −0.20 twice** (§3.2), from two objectives
   whose tooltips each promise −20, which turns the Union's election loss into
   an immediate campaign loss rather than a setback.
9. **The 1862 midterm penalty is confined to a window of about a day** by its
   chapter list, against a config label describing an open-ended weekly rate
   (§3.1).
10. **Objective 27's `SetEndOfCampaign` call is unreachable** (§3.2), and
    objective 26's fires only in a branch the chapter advance has already
    switched off.

---

## 12. Open questions

- **Who writes the eleven statics.** `unioncapturedforts`,
  `consecutivevictories[4]`, `winmajorbattleintervention`, `unioncasualties`,
  `csavictoryenemysoil`, `victoryagainstgb/fr` are all read here and set
  elsewhere — almost certainly
  `ImportExportUnitData.ExportBattleResultData` and the fort capture path. The
  `consecutivevictories` array has **four** slots for two sides and two battle
  kinds; which index is which is unconfirmed. **Two are now located:**
  `Town.UpdateOwnership` increments `csacapturedtowns` and sets
  `confederacyinvaded`, each guarded on `CurrentChapter > 0` and on the town's
  state being loyal to the side losing it — so neither advances for a town in
  a border or already-occupied state, and neither is undone on recapture
  ([17](17-politics-and-support.md) §3a).
- **`Frontline2.FrontlineTextureDateToBeUsedFrom`** — the state-set cut-over
  dates are inspector data; an asset dump would close §8 completely.
- ~~**`Newspaper` and `historicalevents.dat`.** `LaunchNewspaperMessage` and the
  `Headlines[5]`/`PictureID`/`HeadlinePicture` fields are one half of the
  newspaper system; findings/22's open question about `historicalevents.dat`
  and its `policyid` is the other. They should be written up together.~~
  **Done** — [25-newspaper-and-events](25-newspaper-and-events.md). It confirms
  this file's `Encoding.Default` finding by contrast: the event loader *does*
  pass `Encoding.Default` and its data relies on it, so `campaignobjectives.dat`
  is the exception. It also notes that the objective path has **no enemy
  filter** (unlike the policy path's `ShowNewspaperMessageEnemy`) and always
  datelines from the player's own capital, and that objectives 0 and 1 are the
  only consumers of `Newspaper.IsNewspaperOpened()`, which they use to hold the
  end of the campaign until the queue drains.
- **`influenceprestige` and `DLC_WL.AdjustPrestige`** — the DLC prestige
  economy (also open in findings/22 as `CalculatePrestigeCost*`).
- **`IsPositionWithinRangeOfTownObjective` and `IsPartOfObjective`** are public
  helpers used elsewhere (campaign AI target weighting?); their call sites were
  not traced.
- **`GetAvailableObjectives(allianceid, includeaccomplished, mintownobjectives)`**
  — the `mintownobjectives` parameter suggests the UI or the AI asks for
  objectives with a minimum number of towns; unexamined.
- Scenario `002` has **no `Campaigns/002` folder in this install**, consistent
  with it being DLC content; objectives 29–37 are therefore unreachable here.
