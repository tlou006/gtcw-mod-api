# 27 — Dispatches and the message log

**Build:** see [00-build-provenance](00-build-provenance.md)
**Source:** `Assembly-CSharp.dll` → `Messages`, `MessageLog`, `Dispatch`,
`BattleUnits`, `Regiment`, `ImportExportUnitData`, `Objectives`, `Pontoons`,
`CBuilding`, `Vessel`, `WeaponList`, `AICampaign`; `Config/battleprefs.txt`;
`Config/campaignprefs.txt`; `Config/debug.txt`
**Summary:** [summary-newspaper](summary-newspaper.md)

The newspaper is the full-screen narrative channel
([25](25-newspaper-and-events.md)). This is the other one: the **military
dispatch**, a letter from a subordinate that appears as a blinking symbol over
the map and goes into a scrolling log. It is the game's whole in-fiction
telemetry system — ammunition warnings, casualty reports, supply failures,
battle results, the two monthly cabinet reports — and unlike the newspaper none
of its text is authored in a data file. **Every word is assembled in code.**

Closes open question 2 of [25](25-newspaper-and-events.md) and the `Messages`
item the [README](README.md) carried as remaining work.

---

## 1. Three objects

| Type | Lines | Role |
| --- | --- | --- |
| `Messages` | 1,633 | The store, the on-screen symbols, and `GenerateMessageContent` — the switch that writes all 46 dispatch texts |
| `MessageLog` | 254 | The log panel: a fixed window of entries, mouse-wheel paging, retention |
| `Dispatch` | 139 | The letter itself — letterhead, addressee, body, signature, and a *move the camera there* button |

All three live under `UI`. `Messages` is found by
`GameObject.Find("UI").GetComponent<Messages>()` and the other two are reached
through serialized references from it. The panel ids are
`PanelHandler.panel_dispatch`, `panel_messagelog` and `panel_messagesshown`
(the HUD counter pair).

`GameVars.debug_deactivatemessaging` switches the whole channel off and **is**
a debug flag, read from `Config/debug.txt` by `GamePrefs.loaddebugs` (~4471).
It has no options checkbox — the contrast with the newspaper's
`debug_deactivatenewspapermessages`, which *is* player-facing
([25](25-newspaper-and-events.md) §1.5), is worth keeping straight. When set,
`Messages.Update`, `MessageLog.Start/Update`, `Add`, `OpenMessage` and
`GetMessageIDFromReferenceNumber` all early-return, and
`GetMessageIDFromReferenceNumber` returns `-1`.

---

## 2. `Messages.Message` — the record

Nineteen fields, all public: `type`, `regref`, `objref`, `targetdestination`,
`sender`, `day`, `month`, `year`, `time`, `number`, `read`, `content`,
`headline`, `tooltip`, `delayedtime`, `unitspotted`, `useunitspotted`,
`wasshownonscreen`, `manualsender`, `symboloffset`, `manualmessage`.

The constructor stamps the date and then calls `GenerateMessageContent()`
**once**, at creation. So `headline`, `content` and `tooltip` are frozen the
moment the dispatch is raised, not rendered when it is read: a report of a
unit's morale describes the morale it had when the courier left.

Three reference fields do different jobs and are easy to confuse:

| Field | Meaning |
| --- | --- |
| `sender` | The formation that *signs* the letter. Named in the letterhead, and its commander's name is the signature |
| `regref` | The unit the letter is *about*. Supplies the strength, morale and supply figures quoted in the body |
| `objref` | A map object the letter is about — an objective marker, a battle monument, a building, a pontoon |

`manualsender` overrides the signature with a literal string, which is how the
cabinet reports are signed *Secretary of the Treasury* and *Secretary of War*
with no regiment behind them. `manualmessage` overrides the body the same way,
used by the officer-loss report, the weapon-delivery and ship-completion
notices and the rehabilitation notice.

### 2.1 The symbol id is `type + symboloffset`

`ShowMessageOnScreen` and `UpdateScreenMessages` both call
`panelhandler.ShowSymbol(message.type + message.symboloffset, …)`.
`symboloffset` defaults to **5**, and every campaign-side call site passes
`GameVars.messagesymbolsstartcampaign`, a hardcoded **−45**.

So the atlas index is `type + 5` for battle dispatches (types 0–28 → 5–33) and
`type − 45` for campaign dispatches (types 50–91 → 5–46). The two ranges
overlap, which is harmless only because the two sets never coexist in one
scene. **A mod adding a new id must pick the offset to match**, and an id
outside the declared range will index past the atlas.

---

## 3. `Add()` — the one entry point

```
bool Add(int typ, Regiment sender, float timedelay = 0f, bool useunitspotted = false,
         bool checkifalreadyexist = true, Regiment regref = null, GameObject objref = null,
         Vector3 targetdestination = default, string manualsender = "",
         int symboloffset = 5, string manualmessage = "")
```

It returns `false` when the dispatch was **not** raised — suppressed by the
debug flag, rejected by the DLC filter, or judged a duplicate. One caller reads
that return value (§9).

Every dispatch in the game goes through it, from **50 call sites in eleven
files** — 22 of them in `Regiment` and 12 in `BattleUnits`, plus four in
`Tutorial` and one in `DebugInfo`.

### 3.1 The DLC filter

With `DLC_WL.dlc_scenarioactive`, two extra gates apply before anything else:

- if the dispatch has a `sender`, it is dropped unless that formation is
  `dlcw_isundercommander` or (in the campaign) `DLC_WL.IsPlayerPartOfUnit`;
- the id must not be in `DLC_WL.messageidstoignore`.

So in a DLC career scenario the channel is narrowed to the player's own chain
of command. In the base game neither gate runs.

### 3.2 The duplicate window compares clock time, not elapsed time

With `checkifalreadyexist: true` (the default), `Add` walks the **whole**
message list and rejects the new dispatch if an existing one matches on
`type`, `sender`, `delayedtime`, `useunitspotted`, `regref` **and** `objref`
and is inside a time window:

| Scene | Expression | `checktimeoflatestmessage` |
| --- | --- | --- |
| Battle | `uniStormSystem.Hour − message.time <= pref` | **0.5** game hours |
| Campaign | `GameVars.currenttimefromstart − message.time <= pref` | **0.005** game years ≈ **1.83 days** |

The campaign comparison is against a cumulative clock and behaves. **The battle
comparison is not**: `uniStormSystem.Hour` is the hour of the day, 0–24, and
battles in this game run across nights
([10-simulation-tick](10-simulation-tick.md)). After midnight, any dispatch
raised at a later clock hour yields a **negative** difference, which satisfies
`<= 0.5`, so it counts as recent for the whole of the following day.

> A brigade that reported low ammunition at 18:00 on day 1 of a battle cannot
> report it again at any time before 18:30 on day 2 — the window test says the
> older dispatch is half an hour old when it is fourteen hours old.

Two further asymmetries in the same method:

- the main-list comparison **omits `targetdestination`** while the delayed-list
  comparison includes it, so two dispatches identical except for where they
  point are duplicates in one list and distinct in the other;
- the delayed-list loop wraps **both** of its time branches in
  `if (!bunits.IsCampaign)`, so in the campaign `flag2` can never become true
  and **the delayed queue is never checked for duplicates at all**. Inert in
  the shipped game, because no campaign-side call site passes a delay (§3.3).

### 3.3 The delayed queue, and the fog release that fires immediately

A dispatch goes to `delayedmessage` instead of `message` when
`(timedelay > 0f) | useunitspotted`. `CheckDelayedMessages`, once per frame,
promotes it when **either**:

```
delayed.time + delayed.delayedtime <= uniStormSystem.Hour
|| (delayed.useunitspotted && !delayed.regref.iswithinfog)
```

Three call sites use it, and they split two ways.

**Fog as a delay — works as labelled.** `msg_enemygroupexistsbattlefield`
(`Regiment` ~8578) and `msg_pontoonbuildbyenemyforces` (`Pontoons` ~274) both
set `timedelay = 0f` when the enemy unit is visible and
`standarddelayforenemyunitexit` / `standarddelayforenemypontoonfinishedmessage`
(**1** game hour each) when it is not. Both pass `useunitspotted: false`. So a
hidden enemy's withdrawal or pontoon is reported an hour late rather than
withheld — which is what the config labels claim.

**Fog as a gate — does not work.** `msg_enemyreinforcementsarrive`
(`BattleUnits` ~9405) is the assembly's **only** `useunitspotted: true` call
site, and it passes `timedelay: 0f`. `time` was stamped as the current `Hour`,
so the first clause `time + 0 <= Hour` is satisfied on the very next frame and
the dispatch is promoted **before the fog clause is ever consulted**.

> **The one dispatch built to wait until the enemy is actually spotted never
> waits.** Enemy reinforcements entering the battlefield are announced by name
> on the next frame, with their formation type and approximate strength, fog of
> war or not. The strength figures are still rounded by
> `Tools.GetApproximateValue` to `approximatevalues{infantry 1000, cavalry 500,
> guns 5}`, so fog blurs the numbers while leaving the arrival itself public.
>
> The fix is one argument: pass a non-zero `timedelay`, or make the first
> clause conditional on `!useunitspotted`.

Two side effects of the same site:

- `runningnumber` is incremented in the constructor when `delayedtime <= 0f`
  and **again** by `CheckDelayedMessages` on promotion, so every sighted enemy
  arrival burns two issue numbers and leaves a **visible gap in the dispatch
  numbering** on the letterhead.
- choosing the signing formation spins `UpdateUnitRangeFast()` in a `while`
  loop up to **360 times in one frame** looking for an enemy in range.

---

## 4. The 46 ids

All declared as `public static int msg_*` on `Messages`, and all 46 have a case
in `GenerateMessageContent`. There is **no `default:` arm**, so an unknown type
produces a dispatch with an empty headline and body rather than a diagnostic
string — the opposite of the newspaper's behaviour
([25](25-newspaper-and-events.md) §5.4).

### 4.1 Battle dispatches — 0 to 28

| id | Name | Raised by |
| --- | --- | --- |
| 0 | `ownreinforcementsarrive` | `BattleUnits` ~9390 — scheduled arrival, own side |
| 1 | `enemyreinforcementsarrive` | `BattleUnits` ~9405 — scheduled arrival, enemy (§3.3) |
| 2 | `objectivetaken` | `Objectives` ~551 |
| 3 | `objectivelost` | `Objectives` ~558 |
| 4 | `pontoonbuildbyownforces` | `Pontoons` ~282 |
| 5 | `pontoonbuildbyenemyforces` | `Pontoons` ~282, 1 game hr late if hidden |
| 6 | `ownunitsurrenders` | `Regiment` ~13962 |
| 7 | `enemyunitsurrenders` | `Regiment` ~13971 |
| 8 | `commanderkia` | `Regiment` ~8510, signed by the Quartermaster of the parent |
| 9 | `commanderwia` | same site, `newstate == 3` |
| 10 | `owngroupexitsbattlefield` | `Regiment` ~8574 |
| 11 | `enemygroupexistsbattlefield` | `Regiment` ~8584, 1 game hr late if hidden |
| 12 | `enemyproposesceasefire` | **never raised** |
| 13 | `enemyarmyretreats` | **never raised** |
| 14 | `enemyarmysurrenders` | **never raised** |
| 15 | `groupaireachedfinalwaypoint` | `Regiment` ~22996; `Tutorial` ×2 |
| 16 | `groupaiincombatwithenemy` | `Regiment` ~8095 — same site as 28, chosen when `ai_stance > 0` |
| 20 | `lowammunition` | `Regiment` ~8139 |
| 21 | `criticalammunition` | `Regiment` ~8149 |
| 22 | `lowartilleryammunition` | `Regiment` ~8159 |
| 23 | `relativelyhighcasualties` | `Regiment` ~8171 |
| 24 | `veryhighcasualties` | `Regiment` ~8177 (and a dead twin at ~8183) |
| 25 | `lowmorale` | `Regiment` ~8117 |
| 26 | `grouprouted` | `Regiment` ~8126 |
| 27 | `statusoftroopseod` | `BattleUnits` ~8284 — the Quartermaster's Morning Report (§7) |
| 28 | `incombatwithenemy` | `Regiment` ~8095, `TriggerEnemyContactMessage`, `ai_stance <= 0` |

Ids **17, 18 and 19 are not declared at all** — a gap in the numbering between
the event dispatches and the warning dispatches, with no case and no symbol.

### 4.2 Campaign dispatches — 50 to 91

| id | Name | Raised by |
| --- | --- | --- |
| 50 | `combatresult` | `ImportExportUnitData` ~3197, once per monument per side (§9) |
| 51 | `navalcombatresult` | same site, `landorsea == 1` |
| 53 | `issuenewbonds` | `AICampaign` ~5943, signed *Secretary of the Treasury* |
| 54 | `rebuynewbonds` | `AICampaign` ~5952 |
| 55 | `outofsupply` | `Regiment` ~5394, `lowsupplymessagetrigger` = **0.25** (§5.1) |
| 56 | `reachedfinalwaypointcamp` | `Regiment` ~23001; `Tutorial` |
| 57 | `retreating` | `Regiment` ~6562; `BattleUnits` ×3 |
| 58 | `economyreport` | `BattleUnits` ~2014, monthly (§6) |
| 59 | `returnfromwia` | `BattleUnits` ~2249 |
| 60 | `returnfrompow` | `BattleUnits` ~2265 |
| 61 | `officerlossreport` | `ImportExportUnitData` ~3216, body from `GetOfficerLossReport` |
| 74 | `unitdisintegrates` | `Regiment` ~6579, ~6588 |
| 75 | `unitdisintegratesfleetsurvives` | `Regiment` ~6583, ~6592 |
| 76 | `unitsdisbanded` | `ImportExportUnitData` ~3673 |
| 79 | `siegestarted` | `BattleUnits` ~3090 |
| 87 | `rehabilitated` | `BattleUnits` ~2298, body from `manualmessage` |
| 88 | `intelligence` | `BattleUnits` ~2015, monthly (§6) |
| 89 | `constructionfinished` | `CBuilding` ~1649, signed *Secretary of the Treasury* |
| 90 | `weaponorderfinished` | `WeaponList` ~763, signed *Chief of Ordnance or Secretary of War* |
| 91 | `shipsready` | `Vessel` ~862, signed *Secretary of the Navy* |

Ids **52, 62–73, 77, 78 and 80–86** are not declared — twenty-two gaps,
several of them contiguous blocks, in a range the campaign otherwise uses
densely. Nothing reads them and nothing writes them.

### 4.3 Three dispatches are authored and never raised

Ids **12, 13 and 14** have complete headline, tooltip and body text in
`GenerateMessageContent` and **no writer anywhere in the assembly**:

| id | Headline | What it says |
| --- | --- | --- |
| 12 | *"X proposes cease-fire"* | The enemy asks for a halt for the rest of the day to tend the wounded, and will hold fire if the player's courier returns with agreement |
| 13 | *"X retreats"* | Scouts see the enemy moving his trains away; should we press the attack before they slip away? |
| 14 | *"X surrenders"* | A reply accepting the player's terms of surrender and designating officers to carry the stipulations into effect |

All three are written as *requests for orders* — the cease-fire one explicitly
awaits a reply. Together they describe a negotiation layer the battle scene
does not have: the player can offer a cease-fire or surrender through
`PanelHandler`'s prompts ([25](25-newspaper-and-events.md) §4.1 values 5 and
10), but the enemy has no path to ask. Id 14's body is a close paraphrase of
Lee's acceptance at Appomattox.

### 4.4 Who signs a dispatch, and who is quoted in it

`GamePrefs.messagehighestunitlevel` = **15** (Corps) and almost every battle
site passes it to `bunits.GetHighestParentUnit(unit, 15)`. So a warning about a
brigade is **signed by its corps commander** where one exists, with the brigade
named as `regref` in the body.

Two exceptions, both from the same block:

- `msg_grouprouted` is signed by the routed formation itself;
- `msg_relativelyhighcasualties` and `msg_veryhighcasualties` are signed by the
  **regiment**, because the live branch assigns `highestParentUnit = this`.
  The branch that assigns the real parent (`Regiment` ~8183) is a duplicate of
  the live one and its `!messagetriggered[4]` guard is already false by the
  time control reaches it, so **it is dead code** — the casualty dispatches are
  signed one or two echelons below everything else in the panel.

`msg_commanderkia` / `wia` is the only one with a hand-written sender string:
*"Quartermaster of the <parent formation>"*.

---

## 5. The warning triggers, with their shipped thresholds

`Regiment.CheckMessageTriggers` (~8104) is the whole in-battle warning system.
It returns immediately unless `alliance == GameVars.playeralliance`, so **only
your own units ever report**, and it splits by echelon:

| Branch | Echelon | Dispatches |
| --- | --- | --- |
| `unittyp > 13 && <= GamePrefs.messagehighestunitlevel` | **Division and Corps only** | low morale, rout |
| `unittyp <= 13` | fighting units and low echelons | ammunition ×3, casualties ×2 |

Because the upper branch is bounded by `messagehighestunitlevel` = 15, an
**Army or Army Group never reports anything**, and neither does a brigade — the
morale warning exists at exactly two levels of the hierarchy
([16-enumerations](16-enumerations.md) *unittyp*).

| Trigger | Pref | Shipped | Rearms at | Scope |
| --- | --- | --- | --- | --- |
| Low morale | `messagelowmoraletrigger` | **< 0.40** | **> 0.48** (`× 1.2`) | Division, Corps |
| Rout | — | `grouproutedratio >= 1` | `< 1` | Division, Corps |
| Low ammunition | `messagelowammotrigger` | **< 0.30** | `>= 0.30` | non-artillery, `unittyp <= 13` |
| Critical ammunition | `messagecriticalammotrigger` | **< 0.15** | `>= 0.15` | non-artillery |
| Low artillery ammunition | `messagelowariammotrigger` | **< 0.25** of the mean of `ammo[]` | `>= 0.25` | `unittyp == 2` only |
| High casualties | `messagehighcasualtiestrigger` | **> 0.40** | never | `unittyp <= 13` |
| Very high casualties | `messageveryhighcasualtiestrigger` | **> 0.60** | never | `unittyp <= 13` |

The morale trigger is the only one with hysteresis — it rearms at 1.2× the
threshold, so a unit hovering at 40 % morale reports once rather than
repeatedly. **The three ammunition triggers rearm at exactly the firing
threshold**, so a unit oscillating across 30 % would spam the log were it not
for the duplicate window of §3.2 — which is the same window that goes wrong
after midnight. The two casualty triggers never rearm, correctly, since
casualties only accumulate.

The casualty ratio is `losses / (strength + losses)`, i.e. a share of the
unit's **original** strength, not of what is left.

### 5.1 The campaign supply warning tests two of the four supplies

`msg_outofsupply` fires from `Regiment.UpdateUpkeep` (~5392) only when **all**
of: `supplystate` is below `lowsupplymessagetrigger` = **0.25**, the unit
belongs to the player, the method was called from the campaign, `supplystate`
fell during this very update — `num14` is its value captured at the top of the
method — **and**

```
groupsupplystate[0] < 0.25 || groupsupplystate[3] < 0.25
```

Those two indices are **rifle ammunition and forage**. Indices 1 and 2 —
artillery ammunition and **provisions** — are not tested, although the
dispatch body reports all four by name.

> **An army can starve without being reported.** Drop provisions to zero while
> rifle ammunition and forage stay above a quarter and no dispatch is raised,
> even though the overall supply figure is below the trigger and falling. The
> same army loses morale and men to it overnight
> ([14-supply-and-logistics](14-supply-and-logistics.md),
> [15-attrition-and-condition](15-attrition-and-condition.md)).

---

## 6. The two monthly cabinet reports

Both are queued from `BattleUnits.UpdateEndOfMonthFunctions` on the **first
frame of a new month** ([10-simulation-tick](10-simulation-tick.md) §4, the
`lastdayupdate > dayCounter` monotonicity break), both with `sender: null`,
both with **`checkifalreadyexist: false`** — so neither is ever deduplicated —
and both followed by `SetButtonsBlinkStatus(…, red, 10f)` on HUD buttons 362
and 363.

The order matters: the reports are queued **before** the last-month snapshot is
overwritten, so the figures a player reads describe the month that just ended.

### 6.1 The intelligence report is near-perfect information, free, every month

`BattleUnits.GetIntelligenceReport(alliance)` returns `""` for `alliance >= 2`
and otherwise reports on `num`, the *other* belligerent. In one page it
discloses:

| Paragraph | Discloses | Approximated? |
| --- | --- | --- |
| Policies completed | Every policy the enemy finished last month, **by name** | no |
| Policies in discussion | Every act and policy the enemy currently has `researchprogress < 1`, by tooltip headline | no |
| Crisis | Whether the enemy AI's personality is `emergencyaipolicyrecruiting` or `emergencyaipolicycredit` — its recruitment or credit emergency ([22](22-policy-and-project-catalogue.md) §15) | no |
| Projects | Every project the enemy completed last month, by name | no |
| European relations | One paragraph, and it is broken — [17](17-politics-and-support.md) §4d | — |
| Recruitment | Men recruited and the number of brigades | **men only** |
| Formations | Every Corps and Army formed last month, **by name, with the state it is supposed to operate in** | no |
| Fleets | Every fleet raised, by name, with the state it is operating near | no |
| Shipyards | Ships laid down last month | no |
| Morale | The enemy's `averagearmymorale` as a **percentage, plus the month-on-month change** | no |
| Officers | How many high-ranking officers the enemy lost, naming the **top three by `campaignranking`** | no |
| Doctrine | `lastmonthaistrategy` as plain English: consolidating / raiding / defending its land / carrying out offensive operations / **planning a naval invasion** | no |

Only one figure in the whole report passes through the fog filter.
`TransformNumber` (~2426) rounds to `GamePrefs.approximationvalues` —
**50 / 100 / 500 / 1000 / 2000** for magnitudes below 1,000 / 5,000 / 10,000 /
50,000 / above — and only when `GameVars.usefow` is on and the subject is not
the player's own alliance. Everything else in the table is printed exactly.

> The last row is the one that matters, and it needs reading precisely.
> `lastmonthaistrategy` is **a high-water mark, not a doctrine**: five sites in
> `AICampaign` raise it with `if (x < n) x = n` as the AI does things — **1**
> when a raid force is formed (~254), **2** when units are set to defend
> (~1370), **3** when an offensive operation is launched (~4669, ~4736), **4**
> when a sea-invasion force exists (~697) — and nothing ever lowers it until
> `UpdateEndOfMonthFunctions` resets it to 0, after the report has been queued.
>
> So the sentence reports **the most aggressive thing the AI did last month**,
> with raiding masked by anything else and consolidating meaning only that
> nothing happened. The one forward-looking value is 4: a sea-invasion force
> exists while it is still assembling, so *"planning a naval invasion"* is a
> genuine warning. All of it arrives with **no intelligence investment, no
> commander attribute and no project** — the report is unconditional on
> everything except the month rolling over.

Two shipped text defects: the completed-policies list separates entries with
`" ,"` (space before the comma), and the officer sentence is appended directly
to the morale sentence with no separator, so it reads *"… recently. The enemy
has lost 4 high ranking officers"* only when the change line fired and runs the
two together otherwise.

### 6.2 The economy report

`Economy.GetEconomyReport(playeralliance)` describes the **player's own**
nation, so nothing in it is approximated. Two thresholds from
`campaignprefs.txt` decide what it calls out: `minimumstocklevelshortage` =
**0.20** marks a product as a shortage and `maximumstockleveloverproduction` =
**0.80** marks one as overproduced. Neither pref has any other consumer in
the assembly, so both are purely descriptive — nothing in the simulation acts
on them.
The rest of it is the rating, the treasury, debt, tax revenue and private
income against the stored last-month figures ([18-economy](18-economy.md)).

---

## 7. The Quartermaster's Morning Report opens with a constant

`msg_statusoftroopseod` (id 27) is the battle's end-of-day summary, raised from
`BattleUnits` ~8284 once the deployment phase closes, signed *Quartermaster*,
with `sender` and `regref` both set to
`GetHighestGroupOfAlliance(playeralliance)` — the player's **top formation**,
an Army or Army Group.

Most of it is sound. The desertion and wounded totals come from
`sideinformation[side]`, which aggregates `groupdesertionslastnight` across the
side (`BattleUnits` ~6646); the supply paragraph divides each
`lastsupplyinstock[i]` by consumption and prints *sufficient* at ≥ 7 days, a
day count from 1 to 6, and *nearly exhausted* below 1, for rifle ammunition,
artillery ammunition, provisions and forage in turn.

The first sentence is not sound:

```
"The reports received suggest morale in the army is "
  + GameVars.desertiontextinfo[Tools.GetSteps(regref.desertionslastnight * 10f, 0, 4)]
```

Two things are wrong with it at once.

**It reads a desertion figure and calls it morale.** `desertiontextinfo` is
`{intact, all right, slightly reduced, getting low, wavering}` and the index is
`clamp(ceil(desertionslastnight × 50) − 1, 0, 4)` — five bands over a desertion
*fraction*, saturating at **8 %** of the unit per night.

**The field it reads is never written for that unit.** `desertionslastnight` is
assigned in exactly one place, `BattleUnits.CheckDesertionsAndMorale` ~8140,
inside a loop guarded by `unittyp <= 13`. The report's `regref` is an Army or
Army Group — `unittyp` 16 or 18 — so its own `desertionslastnight` is never
touched by that loop, and the aggregate that *is* maintained for formations,
`groupdesertionslastnight`, is a different field.

> **The Quartermaster's Morning Report always says morale is "intact".** The
> field is 0 in a fresh battle and stays 0, `GetSteps(0, 0, 4)` clamps to index
> 0, and the other four strings are unreachable. This is the dispatch channel's
> counterpart to the newspaper's constant morale paragraph
> ([25](25-newspaper-and-events.md) §5.5), and unlike that one it is **not** a
> config fix: the line has to read `groupdesertionslastnight`, or better the
> formation's `groupmorale`, which is what the sentence claims to describe.

---

## 8. The log, the screen symbols and the unread count

### 8.1 Retention: ten game days, enforced only while the log is open

`MessageLog.CheckAutoRemoval` deletes every dispatch older than
`GameVars.holdmessagesfordays / 365f` in `Tools.Date.yearfraction` terms.
`holdmessagesfordays` is **10**, a bare field initialiser in `GameVars` ~5822
with **no loader and no other writer** — it is in no config file and no options
screen.

`yearfraction` is absolute (`year + month + day` fractions), so there is no
new-year wraparound; the divisor is 365 rather than 365.25, which shortens the
window by about two and a half hours a year.

The important part is *where* the method runs: `MessageLog.Update`, and
`PanelHandler.HidePanel` ends by calling `go.SetActive(false)` on the panel
object, so a disabled panel's `Update` does not run.

> **Dispatches are purged the moment the player opens the log, not as they
> age.** Everything older than ten game days is destroyed on the first frame
> the panel is active, so the log can only ever show the last ten days — and at
> campaign speed 50, where a game day is 3.0 real seconds
> ([10-simulation-tick](10-simulation-tick.md) §5), ten game days is **thirty
> seconds of real time**. **Untested** in play: the conclusion rests on
> `MessageLog` sitting on the `panel_messagelog` object, which its
> `OnEnable`/`OnDisable` camera handling implies but which an asset dump would
> settle.

`CheckAutoRemoval` also mis-steps its cursor: after `RemoveMessage(i)` it does
`i--` and then clamps `if (i < 0) i = 0`, so when index 0 expires the loop's
`i++` carries it to 1 and the new index 0 is skipped for that pass. Harmless —
the next frame catches it — but it means two expiries at the head of the list
take two frames. `RemoveReadMessages`, which the panel's *clear* button calls,
does the same decrement without the clamp and is correct.

### 8.2 Paging

`CheckScrolls` reads `Input.GetAxis("Mouse ScrollWheel")` every frame and pages
by a whole screen: `startlogentry ±= logentry.Length`, clamped to
`[0, messages.Count − logentry.Length]`. There is no line-by-line scroll and no
filter, sort or search. `ResetEntries` jumps to the bottom. Entries render as
`number + "\t" + headline`, in **red** (0.7, 0, 0) when unread and dark grey
otherwise.

### 8.3 The unread count lags by a full sweep

`messagesunread` is not computed on demand. `UpdateScreenMessages` walks
`GamePrefs.numberofmessagesupdatespercycle` records per frame — **100** in
battle, **50** in the campaign — accumulating into `messagesunreadtemp`, and
only publishes it to `messagesunread` when the cursor wraps past the end of the
list. So the HUD's unread figure is always one complete sweep out of date, and
the *total* figure beside it (`message.Count`) is current. With fewer than 50
dispatches the sweep completes every frame and the lag is invisible.

### 8.4 A screen symbol needs a sender or it vanishes

`ShowMessageOnScreen` gives every new dispatch a blinking symbol anchored at
`StartMessagePositionObjectLanding` and drifting at most
`messagemaximummoveinpx` = **6** px per cycle, spaced
`messagedistanceonscreeninpx` = **100** px apart, for
`messagetimeonscreen` = **30** real seconds. It blinks during its first 5
seconds and its last quarter.

`UpdateScreenMessages` removes a symbol when its 30 s expire **or** when
`sender == null && manualsender == ""`. Every senderless dispatch in the
shipped game carries a cabinet title, so none is caught by that, but it is the
reason `MessageLog.OpenMessage` writes `manualsender = " "` into any dispatch
that has neither — and that single space is a trap, not a fix (§12).

The symbol also only re-renders while `regref != null || manualsender != ""`,
so a dispatch about no particular unit, signed by nobody, would be drawn once
and never again.

---

## 9. The battle-result dispatch, and what `flag3` really gates

`ImportExportUnitData.WriteBattleResultToMonument` raises the campaign
battle-result dispatch — `msg_combatresult` (50) for land and
`msg_navalcombatresult` (51) for sea — once per monument per side, at ~3197,
signed by the commanding officer's `currentcommand` or, if he has none, by a
hand-built `"<rank> <name>"` string. It then **auto-opens** it:

```
bool flag5 = messages.Add(typ, currentcommand, …);
if (flag3 & flag5) messages.OpenMessageOutright(-1, movetoposition: false);
```

This corrects and completes [25](25-newspaper-and-events.md) §5.1. `flag3` is
not only the newspaper gate at ~3401 — it gates the **auto-open of the
dispatch** too, and `flag5` is `Add`'s return value, so a dispatch suppressed
as a duplicate (§3.2) is not popped open either.

`flag3` is cleared at ~3183 when the battle was naval and either:

- the participant count was below
  `minshipsfornewspapernavalengagement` = **8**; or
- `flag2 && flag` — some fleet in the battle had `fleetorders == 1`
  (**Blockade**) *and* some fleet had `fleetorders == 3` (**Raid**).

So the second condition is **a commerce raider running into a blockade**, at
any size. That is the same pair of flags that suppresses the naval
national-morale adjustment ([17](17-politics-and-support.md) §3a): a
raider-versus-blockader action costs neither side national morale, prints no
newspaper, and does not interrupt the player with its dispatch. It still
appears in the log, and the officer-loss report that follows at ~3216 is raised
unconditionally whenever either side lost officers.

---

## 10. Save / load

`Messages.Save`/`Load` write **23 lines per dispatch** plus a count header and
a trailing `runningnumber`:

| Lines | Fields |
| --- | --- |
| 1 | `type` |
| 2–4 | `targetdestination` x, y, z |
| 5–10 | `day`, `month`, `year`, `time`, `number`, `read` |
| 11–13 | `content`, `headline`, `tooltip` — each through `Tools.TransformFromLineBreaks` |
| 14–17 | `delayedtime`, `unitspotted`, `useunitspotted`, `wasshownonscreen` |
| 18–19 | `manualsender`, `symboloffset` |
| 20–22 | `regref`, `sender`, `objref` as identity strings, or `"null"` |
| 23 | `manualmessage` — only read when `versionid >= 0.8707`, so a pre-0.8707 save holds 22 |

Unlike the newspaper, **`runningnumber` is saved** (since
`versionid >= 0.8204`), so dispatch numbering survives a reload while the
newspaper's issue number restarts at 1 ([25](25-newspaper-and-events.md) §1.2).

Three things do **not** survive:

- `delayedmessage` is never written, so a dispatch waiting on a delay or a
  sighting is lost on save;
- the on-screen `screenmessage` list is not written, and `wasshownonscreen`
  round-trips as `true`, so a symbol on screen at save time never comes back;
- the frozen text does survive, but the objects it refers to are re-bound by
  **string identity**: `regref` and `sender` by
  `name + commander + unitid + abrvname`, `objref` by
  `name + parent.name + childCount`. A renamed or restructured unit silently
  loses its back-reference, which costs the dispatch its *move the camera
  there* button and its on-screen symbol.

`Load` has a latent indexing error in all three lookups: it iterates
`j < array.Length` over the `Regiment[]` while testing `array2[j] != null` on
the `GameObject[]`. `array2` is `FindObjectsOfType<GameObject>()` and so is
always the longer of the two, which is why this has never thrown — but the
guard checks an unrelated object, and the `objref` loop correctly iterates
`array2` while the two regiment loops do not.

---

## 11. Defects and dead content

1. **The one fog-gated dispatch never waits for the sighting.**
   `msg_enemyreinforcementsarrive` is the assembly's only `useunitspotted: true`
   caller and passes `timedelay: 0f`, so the elapsed-time clause releases it on
   the next frame and enemy reinforcements are announced through fog of war
   (§3.3).
2. **The battle duplicate window compares hour-of-day, not elapsed time**, so
   after midnight it reads negative and suppresses legitimate repeats for the
   rest of the following day (§3.2).
3. **The Quartermaster's Morning Report always reports morale as "intact"** —
   it reads `desertionslastnight` on a formation, which nothing writes, through
   a desertion string array (§7).
4. **The campaign supply warning ignores provisions and artillery ammunition**,
   testing only rifle ammunition and forage, while reporting all four (§5.1).
5. **The very-high-casualties dispatch has a dead twin.** The second block at
   `Regiment` ~8183 differs only in signing the parent formation instead of the
   regiment, and its `!messagetriggered[4]` guard is already false — so the
   casualty dispatches are signed an echelon or two below every other warning
   (§4.4).
6. **Three fully authored dispatches have no writer** — ids 12, 13 and 14, the
   enemy's cease-fire proposal, retreat warning and surrender acceptance
   (§4.3).
7. **Delayed dispatches are never deduplicated in the campaign**, because both
   time branches of the delayed-list loop sit inside `if (!bunits.IsCampaign)`.
   Inert today; live for any mod that adds a delayed campaign dispatch (§3.2).
8. **The main duplicate test omits `targetdestination`** while the delayed test
   includes it, so the same dispatch pointing at two places is a duplicate in
   one list and not the other (§3.2).
9. **A sighted enemy arrival burns two issue numbers**, leaving a visible gap
   in the dispatch numbering, because the constructor increments
   `runningnumber` for a zero `delayedtime` and the promotion increments it
   again (§3.3).
10. **Dispatches are destroyed on log open, not on ageing**, so the ten-day
    window is enforced at an arbitrary moment and the log cannot show anything
    older (§8.1).
11. **`CheckAutoRemoval` skips the new head** after removing index 0, because
    it clamps `i` to 0 after decrementing it (§8.1).
12. **The unread counter lags a full sweep** of 50–100 records per frame while
    the total beside it is current (§8.3).
13. **`MessageLog.OpenMessage` writes a single space into `manualsender`** for
    any dispatch with no sender, which defeats `Dispatch.UpdateContent`'s
    `Length > 1` test and sends it into `GameVars.commander[sender.commander]`
    with `sender == null`. No shipped dispatch reaches it; a mod's will (§12).
14. **`Dispatch` prints the year as `year - 1860`**, so the letterhead's
    pre-printed *186—* only works for this war. A scenario outside the 1860s
    prints a nonsense digit.
15. **Id 87 (*Officer rehabilitated*) assigns no tooltip**, so its symbol has
    no hover text, and it is the only id of the 46 that assigns none.
16. **`Load` guards a `Regiment[]` walk with a `GameObject[]` null test** in
    both unit lookups (§10).
17. **Twenty-five undeclared ids inside the two ranges** — 17–19 in the battle
    range and 52, 62–73, 77, 78 and 80–86 in the campaign range. No case, no
    symbol, no writer (§4.1, §4.2).
18. **`GameVars.holdmessagesfordays` has no loader** — a hardcoded 10 that no
    config file or option can reach (§8.1).

---

## 12. Mod seams

1. **`Messages.Add` is public** and is the only entry point, reachable through
   `GameObject.Find("UI").GetComponent<Messages>()`. Eleven arguments cover
   every shipped case; the ones that matter are `typ`, `sender`,
   `manualsender`, `manualmessage` and `symboloffset`.
2. **Always pass a `manualsender` longer than one character** when `sender` is
   null. `Dispatch.UpdateContent` falls through to `sender.commander` for any
   shorter string, and `MessageLog.OpenMessage` will helpfully write a single
   space into an empty one — so an empty `manualsender` with no sender is a
   `NullReferenceException` the first time the player opens that dispatch from
   the log (defect 13).
3. **A new id needs three things**: a `case` in
   `Messages.Message.GenerateMessageContent` (postfix it; there is no
   `default:` arm, so an unhandled id silently produces an empty letter), a
   `symboloffset` that maps it into a real atlas slot (§2.1), and a
   `GamePrefs` entry if it needs a threshold. Ids **17–19** and the
   twenty-one campaign gaps are free.
4. **Reviving ids 12–14 needs only writers.** The text, tooltips and symbol
   slots already exist; what is missing is an `Add` call when the enemy AI
   decides to propose a cease-fire, pull out, or surrender — and for 12, a
   reply path.
5. **`Messages.OpenMessageOutright`** (`public`, defaults to the newest
   dispatch) and **`OpenLastMessageByHeadline(string)`** both open a dispatch
   unprompted; `CheckIfHeadlineExists(string)` tests for one by headline. That
   trio is the cheapest way for a mod to drive the player's attention without
   touching the newspaper's pause.
6. **`GameVars.holdmessagesfordays` is a plain public static.** Setting it once
   at campaign start changes retention with no config edit — the only way to
   change it at all.
7. **`MessageLog.MarkAllAsRead` and `RemoveReadMessages` are public**, and
   `Messages.RemoveMessage(int)` removes one by index.
8. The seven **`GamePrefs.message*trigger`** values and
   `lowsupplymessagetrigger` are ordinary prefs in `battleprefs.txt` and
   `campaignprefs.txt` — retuning when the player is warned needs no code
   ([06-config-file-format](06-config-file-format.md) for the positional
   parsing rule).

---

## 13. Open questions

- **Which atlas sprite each id maps to.** The formula is `type + symboloffset`
  (§2.1) but the atlas itself is prefab data; an asset dump would close it,
  along with the twelve `LogEntry` slots' real count and whether `MessageLog`
  sits on `panel_messagelog` (§8.1).
- **`Messages.CheckIfHeadlineExists` / `OpenLastMessageByHeadline` have no
  callers** in the base assembly. Whether the DLC uses them is a question for
  the `DLC_WL` pass.
- **`Newspaper.message`** — the newspaper holds a `Messages` reference used
  only by `OnDisable` to call `CloseMessage()`, which hides the *dispatch*
  panel. Carried over from [25](25-newspaper-and-events.md) §9; the pairing in
  `Dispatch.OnDisable`, which closes the newspaper, suggests the two panels
  were meant to be mutually exclusive.
- **`DLC_WL.messageidstoignore`** — which ids a career scenario suppresses
  (§3.1). Belongs with the DLC pass.
- **Whether `objref` identity strings collide.** `name + parent.name +
  childCount` is not unique for sibling objective markers or battle monuments
  on the same parent; two dispatches about two objectives in one battle could
  re-bind to the same marker on load (§10). The test is a save taken mid-battle
  with two objectives reported.
