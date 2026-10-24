# Summary: the newspaper, historical events and dispatches

**Build:** see [00-build-provenance](00-build-provenance.md)
**Source:** covers [25-newspaper-and-events](25-newspaper-and-events.md) and
[27-dispatches-and-the-message-log](27-dispatches-and-the-message-log.md)

The game's one full-screen narrative channel, and the last open question shared
by [22](22-policy-and-project-catalogue.md) and
[24](24-campaign-objectives.md). One panel, one queue, **four producers**: the
dated event catalogue, the policy records, the objective records, and a
procedurally written battle report.

And the game's other one: the military dispatch, 46 code-written letter types
feeding a ten-day log. The two channels share a panel neighbourhood and a pair
of unreachable morale strings, and between them they are everything the game
ever tells the player in its own voice.

---

## The panel

`Show(...)` never displays anything — it **appends to a queue**.
`UpdateNewspaperQueue()`, called once per frame from `BattleUnits.Update()`,
promotes the head only when nothing is showing, the panel is closed, **20
frames** have passed since the last close, and the game is unpaused. Then
`UpdateShow` pauses the game.

That last condition has an exception: the battle report is the **only**
producer that passes `overridepause: true`, so it is the only paper that
appears through a pause. Everything else waits — which is why a queue can sit
unshown while the player reads something else.

`IsNewspaperOpened()` is a three-state static (showing / queued / nothing) and
its one consumer is nice: campaign objectives 0 and 1 **defer the end of the
campaign** until the queue drains, so the player reads the final paper before
the victory screen.

> The queue is **not saved**. A paper still queued when the player saves and
> quits is never seen, and `Dispatch.OnDisable` calls `newspaper.Close()`
> unconditionally, which dequeues a paper that is on screen.

`GameVars.debug_deactivatenewspapermessages` is **not a debug flag** — `Options`
binds it to a player-facing checkbox and persists it. It is checked inside
`Show`, so switching it on **drops** papers rather than deferring them.

## The catalogue: 119 records, 12 lines each

`1 + 119 × 12 = 1429` lines verified. `index == id` throughout.

Two things it does that `campaignobjectives.dat` does not: it **supports a
per-sublevel override** (`ImportHistoricalEvents(loadfolder)` falls back to
`Config`), and it **opens the reader with `Encoding.Default`** — which the
shipped data relies on for two CP1252 en dashes. That sharpens
[24](24-campaign-objectives.md) §1: the objectives file is the odd one out, not
the norm.

### The scan is an index cursor

`CheckHistoricalEvents` does not loop. It tests the **single** record at
`historicaleventsrunthrough`, increments, and resets only when the in-game date
changes. So the catalogue is swept **one record per unpaused campaign frame**,
and the sweep has to finish before the day rolls over.

It does **not**, at the top of the campaign speed ladder. The campaign sets
`battledaylengthinmins = 1.25` (the battle file's 120 is a different scene's
value — [10-simulation-tick](10-simulation-tick.md) §5), so one campaign game
day is `2.5 / gamespeed` real minutes: **3.0 real seconds at speed 50**, which
is `3 × fps` frames. The sweep needs about **40 fps** to reach index 118, and
clears 60 fps with only a 1.5× margin. Below 40 the tail is never tested, and
the tail is the late war — Atlanta, the 1864 election, Sherman's march,
Appomattox. **A live defect at the speed most players use for 1864–65**, and
one that bites hardest exactly when the late-war map is slowest.

The same arithmetic turned up a dead pref: **`GamePrefs.maxgamespeed` has no
consumer anywhere.** The real ceilings are the hardcoded array tails — 20 in
battle, 50 in the campaign.

### Two gates

**A chapter gate** suppresses the four-paper Fort Sumter chain once the war has
started — correct in a normal game, since chapter 1 begins on 15 April 1861 and
the chain runs 1 March to 13 April. But it is keyed on the **cursor index, not
the id**, so inserting one record before index 42 slides the window onto four
unrelated events. That is the trap for modders.

**A policy gate** (`policyid >= 0`) calls `Policy.GetStatusByPolicyID` with the
alliance argument defaulted, which reduces it to `researchprogress >= 1f` on
the shared policy object — so the gate is **alliance-agnostic**: "has anyone
finished this policy". Fifteen policies gate events, policy **78 Indian Wars**
alone gating the six-paper frontier arc from the Dakota War to Sand Creek. The
full map is in [25](25-newspaper-and-events.md) §2.7.

## Eleven dated events can never fire

The earliest campaign start that ships is **23 February 1861**. Ten records
dated December 1860 to 9 February 1861 form a complete, fully illustrated
secession-winter chain — South Carolina's convention, the first Fort Sumter
reports, Mississippi, Texas, the founding of the Confederacy — for a start date
that was cut. An eleventh, id 33, sits between two 1861 records and is dated
**29-03-1860**, which reads as a year typo rather than cut content.

| Sublevel | Start | Dated events reachable |
| --- | --- | --- |
| A Spring 1861 | 23-02-1861 | 94 / 105 |
| B Summer 1861 | 08-07-1861 | 77 |
| C Spring 1862 | 21-03-1862 | 65 |
| D Spring 1863 | 10-05-1863 | 47 |
| E Spring 1864 | 01-05-1864 | 30 |
| F Tutorial | 15-07-1861 | 76 |

Because the catalogue *is* per-sublevel overridable, the fix for a late start
is a trimmed copy of the file in that sublevel's folder — no code.

## The script-launched half

Fourteen records carry the sentinel date `01-01-1900` and can only arrive by
id:

- **0–3** the four chapter papers, fired by `Policy.CheckForChapterUpdate`
  alongside the chapter video.
- **4–6** a European power entering the war: France → CSA, Britain → CSA,
  **Spain → the Union**. These are the newspaper face of the script
  [20](20-ai-campaign.md) §10 and [24](24-campaign-objectives.md) §5 traced.
- **7–9** the same three powers withdrawing, each when **every** one of its
  expeditionary units has pulled back — France 3, Britain 4, Spain 2, not the
  uniform four findings/20 had — each followed by
  `AdjustInterventionLevel(-100f)`, a one-way exit. Spain's branch is gated to
  scenario `001`, so **in scenario `002` Spain can intervene and never leave**
  and event 9 can never fire.
- **115–118** the AI recruitment and credit emergencies of
  [22](22-policy-and-project-catalogue.md) §15, once per campaign, recorded in
  the save.

## The battle report, and a constant that should be a variable

The fourth producer is not authored at all: `WriteBattleResultToMonument` writes
a paper from `battleresult.dat`, with headlines and prose assembled by ten
`BattleMonument.GetNewspaper*` methods. Small naval actions (under
`minshipsfornewspapernavalengagement = 8`) get a dispatch but no paper — and so
does **a commerce raider running into a blockade, at any size**, which is the
same `fleetorders` pair that suppresses the naval national-morale adjustment
([17](17-politics-and-support.md) §3a). The same flag also gates the auto-open
of the battle-result dispatch, so that action does not interrupt the player at
all.

Pinning it down required two enumerations no earlier file had, now in
[16](16-enumerations.md): **`endbattle`** (14 values, four of them transient
prompt states, `9` never assigned) and **`battleresulttype`** (0 victory,
1 glorious victory, 2 defeat, 3 disaster, always player-relative, with three
`endbattle` overrides).

That mapping answered a worry. Six of the generators end in a **diagnostic
string that is printed into the paper**, not logged — `"no newspaper result,
type:8 result:0"`. Cross-checking the reachable `(endbattle, battleresulttype)`
pairs shows **none of them can surface in the shipped game**; autocalc's
`endbattle = 8` is always paired with result 3, which a result-only branch
catches. They remain a live hazard for any mod that adds an `endbattle` value.

The real defect is quieter:

> **The morale paragraph of every battle newspaper is a constant string.**
> `newspaperminornatmoraleloss` and `newspapermediocrenatmoraleloss` ship at
> **−1.0** and **−2.0**, but the value they are compared against —
> `−(losses^0.3 × 0.0004)` times a multiplier of at most 3 — is about **−0.026**
> for a 30,000-casualty battle, on a 0–1 morale scale. So headline 4 is always
> *"Support for the … cause wavers."* and the closing paragraph is always
> *"the public unmoved by the recent events."* Five authored variants,
> including the whole *"in shock after the licking"* register, are unreachable.

Those two prefs have **no other consumer in the assembly**, so this is a
two-number config fix. But it uncovers a second defect that is currently
masked: `GetNewspaperHeadline4` indexes the **loser**'s morale effect
(correct), while `GetNewspaperMorale` indexes the **player's** while narrating
*"Sources from the <enemy> side report…"*. Since the exporter zeroes the
winner's entry, fixing only the thresholds would make the enemy press report
itself unmoved after being routed and in shock after winning. **Fix the index
and the thresholds together or not at all.**

## The other channel: 46 dispatches, none of them authored

`Messages` (1,633 lines), `MessageLog` (254) and `Dispatch` (139) are the
military-dispatch channel — a blinking symbol over the map for 30 real seconds,
then a scrolling log, with the letter itself rendered on a period form. **All
46 texts are assembled in code**, which is the opposite of the newspaper's
data-driven catalogue, and `Messages.Add` is the single entry point for all of
them, called from 50 sites in eleven files.

Only your own units report, and a warning is signed by the **corps** commander
rather than the unit in trouble (`messagehighestunitlevel = 15`). The morale
and rout warnings exist at exactly two echelons — **Division and Corps** — so
an Army, an Army Group and a brigade are all silent.

Four findings carry beyond the channel itself.

> **The one fog-gated dispatch never waits for the sighting.** `useunitspotted`
> has exactly one caller — `msg_enemyreinforcementsarrive` — and it passes
> `timedelay: 0f`, so the elapsed-time release clause is satisfied on the next
> frame and the fog clause is never consulted. **Enemy reinforcements are
> announced by the commander's name, with formation type and approximate
> strength, through fog of war, in every battle.** The strength figures still
> pass the `approximationvalues` filter, so fog blurs the numbers and leaves
> the arrival public. One-argument fix.

> **The battle de-duplication window compares hour-of-day, not elapsed time.**
> `uniStormSystem.Hour − message.time <= 0.5` goes negative once a battle
> crosses midnight, and a negative difference satisfies the test — so a
> dispatch raised at 18:00 on day one is treated as half an hour old until
> 18:30 on day two. The campaign branch uses a cumulative clock and is correct.

> **The Quartermaster's Morning Report always says morale is "intact."** Its
> first sentence indexes `desertiontextinfo` with
> `desertionslastnight × 10` on the player's **top formation** — a field only
> `CheckDesertionsAndMorale` writes, and only for `unittyp <= 13`. So the index
> is always 0 and four of the five strings are unreachable. This is the
> dispatch channel's exact counterpart to the newspaper's constant morale
> paragraph, and unlike that one it is **not** a config fix.

> **The campaign supply warning tests two of the four supplies.**
> `msg_outofsupply` requires `groupsupplystate[0] < 0.25 ||
> groupsupplystate[3] < 0.25` — rifle ammunition or forage. Artillery
> ammunition and **provisions** are not tested, although the dispatch body
> reports all four. An army can starve of food with no warning raised.

**The monthly intelligence report is near-perfect information, free.** Queued
on the first frame of a new month alongside the economy report, never
de-duplicated, it names every policy the enemy finished and every one it is
still debating, its recruitment or credit crisis, every project completed,
every Corps and Army raised **with the state it is meant to operate in**, every
fleet raised, its armies' exact average morale and the month's change, the top
three high-ranking officers it lost, and `lastmonthaistrategy` in plain
English — which is a **high-water mark** of the most aggressive thing it did,
with value 4 raised while a sea-invasion force is still assembling. Only the
recruited-men figure passes the fog filter; everything else is exact.

**Retention is ten game days, hardcoded, and enforced only while the log panel
is open** — `holdmessagesfordays = 10` is a bare field initialiser with no
loader, and `MessageLog.CheckAutoRemoval` runs in the panel's `Update`. So
dispatches are destroyed the moment the player opens the log, and at campaign
speed 50 ten game days is **thirty seconds of real time**.

Three dispatch texts are complete and have **no writer**: ids 12, 13 and 14 —
the enemy proposing a cease-fire, warning of its own retreat, and accepting
terms of surrender in words close to Appomattox. All three are written as
requests for the player's orders, which describes a negotiated-battle-end layer
the game does not have.

## Traps

- **`ImageS` is captured once from the prefab and never reassigned**, so
  `Resources.UnloadAsset` targets the wrong sprite and every paper's image
  leaks. A campaign keeps ~100 newspaper sprites resident.
- **A bad `imageid` is silent** — `Resources.LoadAll` returning nothing leaves
  the *previous* paper's picture on screen.
- **Do not instantiate the `Newspaper` prefab at runtime.** `Awake()`
  re-creates `historicalnewspaperstriggered`, discarding the campaign's event
  log — the same hazard [24](24-campaign-objectives.md) found for
  `CampaignObjective`.
- `runningnumber` is not saved, so the issue number restarts at 1 after every
  load while the volume (from `startyear`) stays correct.
- Sprites **6 and 7 are dead art**, reachable only via `battleresulttype` 5 and
  6, which the game never produces. `ImgID_Victory` and `ImgID_Lost` have no
  readers at all — and if their names describe sprites 0 and 1, the land image
  fallback has them **swapped**, with every defeat showing the victory picture.
  That one needs an asset dump before it can be claimed.
- Five shipped typos reach the player: *"whipping the X thouroughly"*,
  *"ceering crowds"*, *"maretime might"*, *"dissatisfied citizen"*, and
  *"wanderïng freely"* in event 54.

## Mod seams

1. **Adding or retiming an event needs no code** — 12 lines in
   `historicalevents.dat`, or a whole-file override in a sublevel folder. Keep
   `id == index` below 42.
2. **`Newspaper.Show` and `LaunchHistoricalEventByID` are public**; the queue,
   the cooldown and the pause/resume are handled for you, and
   `checkifalreadylaunched: true` gives once-per-campaign semantics that
   persist through the save.
3. **Postfix the ten `BattleMonument.GetNewspaper*` methods** — pure functions
   of monument state (nine return strings, one an image id) with no side
   effects, and the cleanest text-rewriting surface in the game.
4. **Prefix `Newspaper.UpdateShow`** to intercept any paper from any producer,
   including its image id. Single chokepoint.
5. `CampaignObjective.LaunchNewspaperMessage` and
   `Policies.LaunchNewspaperMessage` are both `public static` and usable
   standalone.

## What this leaves open

An **asset dump** is now the binding constraint on five questions: which
`UI/Newspaper/<id>` sprites exist (the catalogue references 11–96 plus a
**506/507/513/522/526** block that may be DLC), the serialized
`ImgID_Victory` / `ImgID_Lost` values, `PanelsWhenOpenedNotResetSpeed`, which
message-symbol atlas slot each `type + symboloffset` lands in, and whether
`MessageLog` really sits on the panel object its retention behaviour depends on
([27](27-dispatches-and-the-message-log.md) §8.1).

Of the three narrative channels, two are now covered. The third, **`Diary`**,
reads `config/diary.dat`, which is **absent from this install** — it is DLC
content and belongs with the `DLC_WL` pass, along with
`DLC_WL.messageidstoignore`, the list of dispatch ids a career scenario
suppresses.
