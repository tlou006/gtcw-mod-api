# 25 — The newspaper and the historical-event system

**Build:** see [00-build-provenance](00-build-provenance.md)
**Source:** `Assembly-CSharp.dll` → `Newspaper`, `BattleMonument`,
`ImportExportUnitData`, `Policies`, `CampaignObjective`, `BattleUnits`,
`Autocalc`; `Config/historicalevents.dat`; `Config/campaignprefs.txt`;
`Config/battleprefs.txt`; `Campaigns/001/*/ScenarioDescr.txt`
**Summary:** [summary-newspaper](summary-newspaper.md)

The newspaper is the game's one full-screen narrative channel. It is a single
panel fed by a queue, with **four independent producers**: the dated
historical-event catalogue, the policy records, the campaign-objective records,
and a procedurally written battle report. This file documents the panel, the
catalogue, and the battle report's text generator.

Closes open question 8 of
[22-policy-and-project-catalogue](22-policy-and-project-catalogue.md) and the
`Headlines[5]` / `PictureID` / `HeadlinePicture` half of
[24-campaign-objectives](24-campaign-objectives.md).

---

## 1. `Newspaper` — the panel

`Newspaper` is a `MonoBehaviour` on `UI/Newspaper`, **392 lines**, found by
three different idioms depending on the caller:

| Caller | Lookup |
| --- | --- |
| `PanelHandler` ~786, `ImportExportUnitData` ~127 | `GameObject.Find("UI/Newspaper").GetComponent<Newspaper>()` |
| `Policies` ~352, `CampaignObjective` ~1148 | `GameObject.Find("UI").transform.Find("Newspaper")…` |
| `AICampaign` ~6312 | `GameObject.Find("UI").GetComponentsInChildren<Newspaper>(includeInactive: true)[0]` |
| `BattleUnits` | a serialized `newspaper` field |
| `ImportExportUnitData` ~2734 | `GetComponentInChildren<Newspaper>(includeInactive: true)` |

The panel id is `PanelHandler.cpanel_newspaper = 9`.

### 1.1 Queue, not immediate display

`Show(...)` never displays anything. It appends a `NewspaperQueue` record and
returns:

```
Show(headlines, prosa, imgid, imageheadline, location, datefrom,
     dontcontinuespeed, overridepause)
```

`UpdateNewspaperQueue()` — called once per frame from `BattleUnits.Update()`
(~1586) — promotes `newspaperqueue[0]` to `UpdateShow(...)` only when **all** of:

- `!newspapershown`
- the queue is non-empty
- `panelhandler.panel[cpanel_newspaper].state <= 0`
- `GameVars.frame >= lastframeofclosing + 20` — a **20-frame cooldown** between
  papers
- `!GameVars.gamepaused`, **unless** the record set `overridepause`

`UpdateShow` calls `ShowPanel(..., 100000000f, ...)` (effectively no timeout),
then `GameVars.SetGameSpeed(0f)` — **every newspaper pauses the game**.
`Close()` restores speed via `GameVars.SetGameSpeed()` unless
`dontcontinuespeed`, any panel in `PanelsWhenOpenedNotResetSpeed` is open, or
`GameVars.remainspausedpanelclosed` is set, and dequeues `newspaperqueue[0]`.

Only one producer passes `overridepause: true`: the battle report (§5). Every
other paper waits for an unpaused frame, which is why a queue can sit unshown
while the player is reading something else.

`IsNewspaperOpened()` is a **three-state** public static — `2` showing, `1`
queued but not yet shown, `0` nothing — and the only consumer is
`CampaignObjective.CheckIfAccomplished` for objectives 0 and 1, which defer
`SetEndOfCampaign` until it returns `<= 0` so the player reads the final paper
before the end-of-campaign screen ([24](24-campaign-objectives.md) §2.1).

### 1.2 Masthead

`UpdateCommon()` writes the dateline from `uniStormSystem.dayCounter/
monthCounter` and `bunits.year`, then:

- `"( No <runningnumber>.-Vol <roman>. )"` where the volume is
  `Tools.ConvertToRoman(bunits.year - GameVars.startyear + 1)`;
  `GameVars.startyear` comes from `ScenarioDescr.txt` (`BattlefieldSetup`
  ~1085), so an 1861 campaign prints Vol. I–V.
- `runningnumber` starts at 1 in `Awake()` and is **not saved**. Reloading a
  campaign restarts the issue numbering at 1 while the volume stays correct.
- The dateline is always `"<weekday> MORNING, <long date>"`.

The body's byline is `location + ", " + Tools.GetDateString(day, month)`, and
when the producer passes no location `UpdateShow` substitutes
**the player's own capital** — including for news about the enemy.

### 1.3 The image path

```
if (ImageS != null) Resources.UnloadAsset(ImageS);
Sprite[] array = Resources.LoadAll<Sprite>("UI/Newspaper/" + imgid.ToString("0"));
if (array.Length != 0) img.sprite = array[0];
```

Three consequences:

- **A bad `imageid` is silent.** `LoadAll` returning nothing leaves
  `img.sprite` at whatever the previous paper set, so a typo'd id shows the
  *last* paper's picture rather than blanking or logging.
- **The loaded sprites are never unloaded.** `ImageS` is assigned exactly once,
  in `ReferenceComponents()`, from the prefab's initial sprite; it is never
  reassigned. So every `UpdateShow` unloads the same original prefab sprite
  (a no-op after the first) and leaks the sprite it just loaded. A campaign
  that shows ~100 papers keeps ~100 newspaper images resident. The intended
  line is almost certainly `ImageS = img.sprite;` after the assignment.
- The sprites live in a Unity `Resources` folder, not on disk — there is no
  `UI/Newspaper` directory in the install — so **enumerating which ids exist
  needs an asset dump**.

### 1.4 `ImgID_Victory` / `ImgID_Lost` are dead

Two public inspector fields (`ImgID_Victory`, `ImgID_Lost = 1`) with **no
reader anywhere in the assembly**. Their declared defaults are the only hint at
what sprites 0 and 1 are; see §5.6 for why that hint matters.

### 1.5 The player-facing off switch

`GameVars.debug_deactivatenewspapermessages` reads like a debug flag and is
not one: `Options` ~287/~435 binds it to a checkbox (`NewspaperCB`) and ~614/
~862 round-trips it through the options file. Both `Show` and `UpdateShow`
check it, so turning it on **drops papers rather than queueing them** — a
newspaper suppressed this way is gone, not deferred.

### 1.6 A dispatch panel closing dismisses the paper

`Dispatch.OnDisable` (~59) calls `newspaper.Close()` whenever
`GameVars.frame > 50`. `Close()` dequeues `newspaperqueue[0]` if
`newspapershown`. So closing a military dispatch while a paper is on screen
discards that paper unread. (Whether both panels can be open at once is a
layout question for an asset dump; the code path is unconditional.)

---

## 2. `historicalevents.dat`

### 2.1 Loading

`Newspaper.ImportHistoricalEvents(string folder)` is `public static`, called
once from `CampaignController` ~215 at **`GameVars.frame == 2`**, in the same
frame as `CampaignObjective.InitializeObjectives`
([10](10-simulation-tick.md) frame table).

`folder` is `loadfolder` = `"Campaigns/<level>/<sublevel>"`, and the loader
falls back to `"Config"` when the file is absent — the whole-file override of
[05](05-config-paths-and-overrides.md). **So the event catalogue *is*
per-sublevel overridable**, unlike `campaignobjectives.dat`, whose path is
hardcoded ([24](24-campaign-objectives.md) §1). No sublevel ships an override;
only `Config/historicalevents.dat` exists.

The reader is opened with `Encoding.Default` — again unlike
`campaignobjectives.dat`. The shipped file relies on it: three lines carry
non-ASCII bytes, two of them `0x96` (CP1252 en dash) in
`"Colorado–Blanco"` (record 93) and `"Hickok–Tutt"` (record 108). The third,
`0xEF` in `"wanderïng freely"` (record 54), is a plain authoring typo that no
encoding fixes.

### 2.2 Record shape — 12 lines

**`1 + 119 × 12 = 1429` lines**, verified against the shipped file.

| Line | Field | Notes |
| --- | --- | --- |
| 1 | `id` | `index == id` for all 119 records |
| 2 | `date` | fixed offsets `dd-mm-yyyy`, parsed `(0,2)(3,2)(6,4)` |
| 3–7 | `headline[0..4]` | added only when `Length > 1`, so a blank or 1-char line is skipped |
| 8 | `imagetitle` | caption printed under the picture |
| 9 | `imageid` | `UI/Newspaper/<id>` |
| 10 | `prosatext` | run through `Tools.TransformToLineBreaks` (literal `\n` → newline) |
| 11 | `location` | may be empty → the player's capital is substituted |
| 12 | `policyid` | `-1` = ungated |

The file is **not sorted by date** and does not need to be (§3.1). Record 33 is
dated 1860 while records 31–32 are 1861.

### 2.3 Two dispatch paths

**Date-driven.** `CheckHistoricalEvents(day, month, year)`, called once per
frame from `BattleUnits.Update()` ~1574 while `!GameVars.gamepaused`.

**Script-driven.** `LaunchHistoricalEventByID(int id, bool
checkifalreadylaunched = false)` scans the whole list and shows **every**
record whose `id` matches — so a duplicated id in a modded file fires twice. It
records the id in `GameVars.historicalnewspaperstriggered` regardless of the
flag, making that list a complete log of script-launched events. The list is
created in `Awake()`, re-created on load (`ImportExportUnitData` ~1578) and
written back at ~1077–1080, so it **round-trips through the save**.

### 2.4 The scan is an index cursor, not a loop

`CheckHistoricalEvents` does not iterate. It tests the **single** record at
`historicaleventsrunthrough`, then increments it. The counter resets to 0 only
when the in-game date differs from `lasthistoricaleventsupdate`:

```
if (!date.IsMatching(lasthistoricaleventsupdate)) { runthrough = 0; lastupdate = date; }
if (runthrough < count && events[runthrough].date.IsMatching(date)) { …gates…; Show(…); }
runthrough++;
```

So the catalogue is swept **one record per unpaused campaign frame**, and the
sweep must reach index 118 before the date rolls over or the tail of the file
goes unchecked for that day.

It comfortably does. `GameVars.SetGameSpeed` sets
`uniStormSystem.dayLength = nightLength = GamePrefs.battledaylengthinmins /
gamespeed`, with `battledaylengthinmins = 120`, so one game day is
`240 / gamespeed` real minutes. The campaign speed ladder is
`gamespeedcampaigns = {1, 5, 20, 50}`, so the shortest game day is
**4.8 real minutes = 288 s**. Sweeping 119 records inside that needs only
**0.41 fps**. The cursor design is safe for any plausible frame rate, and it
stays safe up to roughly 17,000 records at 60 fps.

Two side notes from the same arithmetic:

- `GamePrefs.maxgamespeed` (`battleprefs.txt`: `Maximum Gamespeed 10`) has
  **no consumer anywhere in the assembly**. The real ceilings are the hardcoded
  array tails — 20 in battle, 50 in the campaign.
- There is **one** day length shared by battle and campaign; no campaign
  override exists.

`lasthistoricaleventsupdate` is a static that `Awake()` does *not* reset, but
`Tools.Date.IsMatching(null)` returns `false`, so the first frame of a campaign
resets the cursor correctly either way.

### 2.5 Two gates on the date path

```
if (runthrough >= 42 && runthrough <= 45 && Policy.CurrentChapter != 0) flag = false;
if (events[runthrough].policyid >= 0
    && !Policy.GetStatusByPolicyID(events[runthrough].policyid)) flag = false;
```

**The chapter gate is keyed on the cursor index, not on the id.** In the
shipped file index == id, so it suppresses records 42–45 — the Fort Sumter
chain (1 Mar, 6 Apr, 12 Apr, 13 Apr 1861) — once the war has begun. That is
correct behaviour: chapter 1 starts on Union policy 36 or automatically on
15 April 1861 ([22](22-policy-and-project-catalogue.md) §4), so the chain plays
in full in a normal game and is skipped only if the Union rushed Militia Act I.

**For a modder the index keying is a trap**: inserting a single record before
index 42 slides the window onto four unrelated events.

**The policy gate** calls `Policy.GetStatusByPolicyID(id)` with its
`useactivatedvariablealso` defaulted to `-1`, which short-circuits the
per-alliance membership test and returns `researchprogress >= 1f` from the
shared `allpolicies` entry. The gate is therefore **alliance-agnostic**: it
asks "has anyone finished researching this policy", so a Union player does see
the Confederate-flavoured events when the Confederate AI adopts the matching
policy. Since the two sides' catalogues are mirrored at `N` / `N+100`, that is
effectively "has the owning side adopted it".

### 2.6 The 119 records

Dates are `dd-mm-yyyy` as authored. `img` is the sprite id, `pol` the gating
policy (`-1` = none).

**Sentinel date `01-01-1900`** marks the fourteen script-only records. No
campaign reaches 1900, so these can only arrive through
`LaunchHistoricalEventByID`.

| id | date | img | pol | location | image caption | launched by |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | *sentinel* | 21 | — | — | The Election of 1860. | chapter 0 |
| 1 | *sentinel* | 18 | — | — | The Demon of War has Awoken! | chapter 1 |
| 2 | *sentinel* | 18 | — | — | A War so Terrible! | chapter 2 |
| 3 | *sentinel* | 18 | — | — | Civil Wars leave nothing but tombs. | chapter 3 |
| 4 | *sentinel* | 13 | — | Mexico | Ironclad Normandie of the French Fleet. | **France joins the CSA** |
| 5 | *sentinel* | 16 | — | London | British Intervention Force Mobilized. | **Britain joins the CSA** |
| 6 | *sentinel* | 17 | — | Madrid | General Juan Prim of Spain. | **Spain joins the Union** |
| 7 | *sentinel* | 11 | — | — | Napoleon III Skedaddles. | **France withdraws** |
| 8 | *sentinel* | 16 | — | — | Royal Navy in Retreat. | **Britain withdraws** |
| 9 | *sentinel* | 17 | — | — | General Prim's Army has been defeated. | **Spain withdraws** |
| 115 | *sentinel* | 65 | — | — | Union Recruitment in Crisis. | AI emergency |
| 116 | *sentinel* | 51 | — | — | Union Economy in Crisis. | AI emergency |
| 117 | *sentinel* | 526 | — | — | C.S.A. Recruitment in Crisis. | AI emergency |
| 118 | *sentinel* | 26 | — | — | C.S.A. Economy in Crisis. | AI emergency |

The 105 dated records:

| id | date | img | pol | location | image caption |
| --- | --- | --- | --- | --- | --- |
| 10 | 01-11-1861 | 11 | — | London | French Emperor Napoleon III. |
| 11 | 06-03-1862 | 12 | — | Mexico | French Invasion of Mexico underway. |
| 12 | 10-04-1862 | 11 | — | Mexico | French Emperor Napoleon III. |
| 13 | 09-05-1862 | 14 | — | Mexico | The French Defeated in the Battle of Puebla! |
| 14 | 13-12-1862 | 14 | — | Mexico | French Emperor Napoleon III. |
| 15 | 16-06-1863 | 14 | — | Mexico | Second Battle of Puebla a French Victory. |
| 16 | 17-06-1863 | 13 | **144** | Mexico | The French Ironclad Normandie. |
| 17 | 01-06-1864 | 11 | — | Mexico | French Emperor Napoleon III. |
| 18 | 16-11-1861 | 15 | — | — | Union Ship stopping the Trent. |
| 19 | 29-11-1861 | 15 | — | London | Union Ship stopping the Trent. |
| 20 | 14-05-1861 | 16 | — | London | Royal Navy is observing the Civil War. |
| 21 | 24-12-1860 | 26 | — | Charleston | Secession Convention held in South Carolina. |
| 22 | 27-12-1860 | 22 | — | Charleston | Fort Sumter in Charleston Harbor, SC. |
| 23 | 09-01-1861 | 22 | — | Charleston | Fort Sumter in Charleston Harbor, SC. |
| 24 | 10-01-1861 | 28 | — | — | Mississippi State House in Jackson. |
| 25 | 11-01-1861 | 26 | — | Montgomery | Delegates from all Slave states meet in Montgomery. |
| 26 | 15-01-1861 | 29 | — | Pensacola | Fort Pickens Holds In Florida. |
| 27 | 19-01-1861 | 26 | — | Milledgeville | Georgian Convention held at Milledgeville. |
| 28 | 26-01-1861 | 26 | — | Baton Rouge | The Convention voted 113 to 17 to adopt Ordinance of Secession. |
| 29 | 01-02-1861 | 26 | — | Texas | Texas Became the Seventh State to Secede. |
| 30 | 09-02-1861 | 26 | — | Montgomery | A Convention in Montgomery, AL, established the Confederacy. |
| 31 | 24-02-1861 | 27 | — | Montgomery | Honorable Mr. Jefferson Davis. |
| 32 | 04-03-1861 | 25 | — | Washington | Inauguration of President Abraham Lincoln. |
| 33 | **29-03-1860** | 27 | — | Montgomery | President of the Confederate States, Jefferson Davis. |
| 34 | 05-03-1861 | 35 | **170** | — | Could We See C.S. Capital in Virginia? |
| 35 | 02-06-1862 | 36 | — | — | Abe Lincoln's Last Card; Or, Rouge-et-Noir. |
| 36 | 08-06-1864 | 38 | — | — | National Union Party Ticket, 1864. |
| 37 | 30-08-1864 | 37 | — | — | The True Issue or "That's whats the matter". |
| 38 | 15-04-1865 | 39 | — | — | "Lincoln Belongs to the Ages". |
| 39 | 27-04-1865 | 39 | — | — | "Useless. Useless." -The Assassin's Last Words. |
| 40 | 19-05-1863 | 40 | — | Ohio | Vallandigham's Arrest. |
| 41 | 02-03-1861 | 41 | — | — | Organizing the West: Kansas Polls. |
| 42 | 01-03-1861 | 22 | — | Charleston | Fort Sumter in Charleston Harbor, SC. |
| 43 | 06-04-1861 | 29 | — | Charleston | Fort Sumter Under Siege. |
| 44 | 12-04-1861 | 23 | — | Charleston | Fort Sumter Bombarded! |
| 45 | 13-04-1861 | 24 | — | Charleston | Lowering of the U.S. Flag At Fort Sumter. |
| 46 | 19-06-1862 | 507 | — | — | End of Slavery in the U.S. Territories. |
| 47 | 02-07-1862 | 42 | **8** | — | Modern Engineering Feats in the U.S. |
| 48 | 18-08-1862 | 43 | **78** | — | Dakota Attacks in Northern Minnesota, |
| 49 | 26-12-1862 | 44 | **78** | — | Mass hanging in Mankato. |
| 50 | 15-05-1863 | 45 | **78** | — | Fort Snelling, Minnesota. |
| 51 | 07-03-1861 | 522 | — | — | An Act to Establish Regular Army Passed. |
| 52 | 01-04-1861 | 513 | **174** | Havana | Preparations to Support Confederacy in Cuba. |
| 53 | 15-05-1864 | 46 | **33** | — | Confederates Massacring Colored Troops. |
| 54 | 15-04-1864 | 48 | **78** | — | New Tensions on the Western Frontier. |
| 55 | 14-06-1864 | 43 | **78** | — | Nathan Hungate's Family Murdered. |
| 56 | 30-11-1864 | 47 | **78** | — | Indians Massacred at Sand Creek. |
| 57 | 22-03-1861 | 49 | — | Savannah | Vice President Stephens in the Middle. |
| 58 | 13-07-1861 | 50 | **178** | — | Choctaws and Chickasaws Ally with the C.S.A. |
| 59 | 26-07-1861 | 51 | — | — | War Aims Resolution Passed by Congress. |
| 60 | 20-04-1861 | 52 | **76** | Boston | The American River Ganges. |
| 61 | 26-10-1861 | 53 | — | St. Joseph | Pony Express Stamp from Last Year. |
| 62 | 06-11-1861 | 27 | — | — | Jefferson Davis is Elected President. |
| 63 | 21-12-1861 | 54 | — | — | The Medal of Honor. |
| 64 | 23-01-1862 | 55 | — | London | The Dead are Brought Up. |
| 65 | 01-02-1862 | 56 | — | — | The Lyrics were Published in the Atlantic Monthly. |
| 66 | 13-03-1862 | 51 | **25** | — | Union Forces Barred from Returning Anyone to Slavery. |
| 67 | 05-06-1862 | 57 | — | Paris | French Troops Storming Saigon, 1859. |
| 68 | 16-06-1862 | 41 | **71** | Kansas | Casting Votes in Kansas, |
| 69 | 14-08-1862 | 58 | — | — | President Abraham Lincoln. |
| 70 | 30-09-1862 | 59 | — | Berlin | Otto von Bismarck, Minister President of Prussia. |
| 71 | 17-12-1862 | 506 | **175** | — | Cotton is Smuggled from the South. |
| 72 | 09-10-1862 | 60 | — | South Africa | The Boer Fighting Way. |
| 73 | 10-01-1863 | 61 | — | London | The London Underground Railroad. |
| 74 | 03-01-1863 | 62 | — | — | Santa Distributes Gifts in Nast's Cartoon. |
| 75 | 02-02-1863 | 63 | — | St. Petersburg | Czar Alexander II of Russian Empire. |
| 76 | 03-03-1863 | 64 | — | — | Seal of the Idaho Territory. |
| 77 | 15-07-1863 | 65 | **39** | — | Armed Rioters Clashing with Union Army Soldiers. |
| 78 | 17-07-1863 | 66 | — | Japan | U.S.S. Wyoming Battling Choshu Vessels… |
| 79 | 13-07-1863 | 67 | — | Auckland | The British are again Attacking Maori Tribes… |
| 80 | 17-08-1863 | 68 | — | Japan | Map of the Bombardment of Kagoshima. |
| 81 | 03-10-1863 | 69 | — | — | Thanksgiving in Camp. |
| 82 | 29-10-1863 | 70 | — | Geneva | Battle of Solferino, Italy, 1859. |
| 83 | 01-10-1863 | 71 | — | — | Warships of the Russian Navy in America. |
| 84 | 01-02-1864 | 59 | — | Berlin | Otto von Bismarck… |
| 85 | 06-02-1864 | 73 | — | Berlin | Prussian Hussars Attacking Danish Infantry. |
| 86 | 18-04-1864 | 72 | — | Berlin | The Fortress of Dybbol Captured. |
| 87 | 22-04-1864 | 51 | — | — | The United States Congress passes the Coinage Act. |
| 88 | 12-05-1864 | 74 | — | Berlin | The Battle of Heligoland on May 9, 1864. |
| 89 | 21-05-1864 | 75 | **39** | — | Scene in the Gold Room… |
| 90 | 26-05-1864 | 76 | — | — | Map of the Territory of Montana. |
| 91 | 01-07-1864 | 73 | — | Berlin | A Prussian Victory at Als. |
| 92 | 20-07-1864 | 77 | — | China | The Taiping Rebellion is Over! |
| 93 | 10-08-1864 | 78 | — | Montevideo | Brazilian-Colorado Troops Advanced… |
| 94 | 22-08-1864 | 79 | — | Geneva | A Convention Governing the Sick and Wounded was Signed. |
| 95 | 08-09-1864 | 80 | — | Japan | The British Storm the Stockade at Shimonoseki. |
| 96 | 06-10-1864 | 81 | — | Calcutta | Effects of the Cyclone at Calcutta… |
| 97 | 24-10-1864 | 82 | — | Quebec | The Quebec Conference in assembly. |
| 98 | 30-10-1864 | 72 | — | Vienna | The Capture of Dybbol in April, 1864. |
| 99 | 31-10-1864 | 58 | — | — | …Nevada Became the 36th U.S. State. |
| 100 | 12-11-1864 | 83 | — | Asuncion | Brazilian Reinforcements Arriving. |
| 101 | 08-12-1864 | 84 | — | Europe | Pope Pius XI… |
| 102 | 20-12-1864 | 85 | — | Minnesota | Nordic Immigrants Settling in North U.S.… |
| 103 | 03-03-1865 | 86 | **34** | — | A Bureau agent stands between armed groups… |
| 104 | 27-04-1865 | 87 | — | — | The Steamboat Sultana on Fire before Sinking. |
| 105 | 05-05-1865 | 88 | — | North Bend | The First Train Robbery in the U.S.… |
| 106 | 17-05-1865 | 89 | — | Paris | Atlantic Telegraph Cable Laying. |
| 107 | 11-06-1865 | 90 | — | Asuncion | The Battle of the Riachuelo, June 11, 1865. |
| 108 | 24-07-1865 | 91 | — | Springfield | Wild Bill Hickok Defeated Davis Tutt in a duel. |
| 109 | 17-12-1863 | 92 | **143** | Halifax | Lieut. Braine, the Pirate Who Seized the Chesapeake. |
| 110 | 27-02-1861 | 93 | — | St. Louis | Unrest in St.Louis as Militias are Formed. |
| 111 | 28-02-1861 | 94 | — | Frankfort | "Gov. Magoffin's Neutrality…" |
| 112 | 13-04-1861 | 26 | **36** | — | Uproar During Recent Secession Conventions. |
| 113 | 20-10-1864 | 95 | — | St. Albans | Bank Tellers Forced to Pledge Allegiance to the C.S.A. |
| 114 | 03-05-1861 | 96 | **178** | — | Fort Arbuckle in Indian Territory. |

### 2.7 The policy gates, named

Cross-referenced against [22](22-policy-and-project-catalogue.md). Union ids
below 100, Confederate at `+100`.

| Policy | Name | Gates events |
| --- | --- | --- |
| 8 | Industrialization II | 47 |
| 25 | Confiscation Act | 66 |
| 33 | United States Colored Troops | 53 (Fort Pillow) |
| 34 | Emancipation Proclamation | 103 (Freedmen's Bureau) |
| 36 | **Militia Act I** | 112 |
| 39 | Enrollment Act I | 77 (NY draft riots), 89 (Gold Room) |
| 71 | Kansas a Free State | 68 |
| 76 | Industrialization | 60 |
| 78 | **Indian Wars** | 48, 49, 50, 54, 55, 56 — the whole frontier arc |
| 143 | Letters of Marque (CSA) | 109 |
| 144 | Support Mexican Intervention (CSA) | 16 |
| 170 | The Old Dominion (CSA) | 34 |
| 174 | Filibustering (CSA) | 52 |
| 175 | King Cotton (CSA) | 71 |
| 178 | Alliance with the Natives (CSA) | 58, 114 |

Policy **170** is the same switch that makes campaign objectives 2 and 3
mutually exclusive ([24](24-campaign-objectives.md) §2.1) and that
`Policies.IsDeactivated` suppresses while Virginia is Union
([22](22-policy-and-project-catalogue.md) §4) — so event 34 ("Could We See C.S.
Capital in Virginia?") is the newspaper face of the capital move.

### 2.8 Where the script-launched ids come from

| ids | Writer | Condition |
| --- | --- | --- |
| 0–3 | `Policy.CheckForChapterUpdate` ~550 | `LaunchHistoricalEventByID(CurrentChapter)` on any chapter change, alongside the `CHAPTER_<n+1>_COLOR_MASTER` video |
| 4 | `AICampaign.UpdateInterventions` ~6334 | alliance **3 France** declares war on the Union and allies with the CSA |
| 5 | `…` ~6327 | alliance **2 Britain** declares war on the Union and allies with the CSA |
| 6 | `…` ~6320 | alliance **4 Spain** declares war on the **CSA** and allies with the Union |
| 7 | `…` ~6504 / ~6522 | every French expeditionary unit pulled back → France leaves the war, `AdjustInterventionLevel(-100f)` |
| 8 | `…` ~6400 / ~6422 | same for Britain |
| 9 | `…` ~6581 | same for Spain |
| 115–118 | `Policies` ~1842 | AI emergency, `checkifalreadylaunched: true` |

The declaration branches (4–6) carry the four-part entry condition
[20](20-ai-campaign.md) §10 already records — `GetInterventionProbability >= 1f`,
`GameVars.interventions`, `CampaignObjective.winmajorbattleintervention` equal
to the side being helped, not already at war, and `timesincewar <= 0f`.

The withdrawal branches check `CheckPullback` over **every** unit of that
power, and the counts differ: France 3, Britain 4, Spain 2 in scenario `001`,
with shorter `leveltoload == "002"` variants for France and Britain. Spain has
no `002` variant, so this is the newspaper view of a dead end
[20](20-ai-campaign.md) §10 now records: **in scenario `002` Spain can
intervene and never withdraw**, so event 9 can never fire there.

The emergency ids map by alliance and personality
([22](22-policy-and-project-catalogue.md) §15): Union (alliance 0) →
**116** for `emergencyaipolicycredit` (101), **115** for
`emergencyaipolicyrecruiting` (100); Confederacy (alliance 1) → **118** and
**117**. Alliances 2–7 leave `num = -1` and launch nothing. The surrounding
method then calls
`GetActivatedPolicies(includeactsinresearch: true, includegrandpolicies: false,
researchedonly: true)`, which [23](23-game-state.md) §7.6 found ignores both
flags and counts policies still *in* research — so the emergency path's
"has this alliance any policy running" test is the buggy one.

### 2.9 Eleven dated events can never fire

The earliest campaign start in the install is **23 February 1861**:

| Sublevel | Name | Start | Dated events reachable | Skipped |
| --- | --- | --- | --- | --- |
| A | Spring 1861 | 23-02-1861 | 94 / 105 | 11 |
| B | Summer 1861 | 08-07-1861 | 77 | 28 |
| C | Spring 1862 | 21-03-1862 | 65 | 40 |
| D | Spring 1863 | 10-05-1863 | 47 | 58 |
| E | Spring 1864 | 01-05-1864 | 30 | 75 |
| F | Tutorial | 15-07-1861 | 76 | 29 |

Eleven records are therefore **unreachable in every shipped start**:

| id | date | caption |
| --- | --- | --- |
| 21 | 24-12-1860 | Secession Convention held in South Carolina. |
| 22 | 27-12-1860 | Fort Sumter in Charleston Harbor, SC. |
| 23 | 09-01-1861 | Fort Sumter in Charleston Harbor, SC. |
| 24 | 10-01-1861 | Mississippi State House in Jackson. |
| 25 | 11-01-1861 | Delegates from all Slave states meet in Montgomery. |
| 26 | 15-01-1861 | Fort Pickens Holds In Florida. |
| 27 | 19-01-1861 | Georgian Convention held at Milledgeville. |
| 28 | 26-01-1861 | The Convention voted 113 to 17… |
| 29 | 01-02-1861 | Texas Became the Seventh State to Secede. |
| 30 | 09-02-1861 | A Convention in Montgomery, AL, established the Confederacy. |
| 33 | **29-03-1860** | President of the Confederate States, Jefferson Davis. |

Ids 21–30 are a coherent secession-winter chain, authored with art and prose,
for a start date earlier than any that shipped. Id 33 sits between two 1861
records and is dated **1860**; 29 March 1861 would make it reachable, so it
reads as a year typo rather than cut content.

Because the catalogue *is* per-sublevel overridable (§2.1), the clean fix for a
late start is a trimmed `Campaigns/001/<sublevel>/historicalevents.dat`, not a
code change.

### 2.10 Date collisions

Only two dated collisions exist, and both are deliberate:

- **13-04-1861** — id 45 (Fort Sumter flag lowering, suppressed outside chapter
  0) and id 112 (secession-convention uproar, gated on policy 36, which *is*
  what starts chapter 1). The two are exactly mutually exclusive: at peace you
  get Sumter, at war you get the conventions.
- **27-04-1865** — ids 39 (Booth's death) and 104 (the *Sultana*). Both fire;
  the queue shows them in index order, one after the other.

---

## 3. The other authored producers

### 3.1 Policies

`Policies.LaunchNewspaperMessage(int alliance, Policy policy)` is reached only
from `Policy.ResearchPolicy` (~359) and only on the **additive** path crossing
`researchprogress >= 1f`, so scripted completions (grand policies, save
loading) are silent — as [22](22-policy-and-project-catalogue.md) §3.3 noted.

It filters on `policy.ShowNewspaperMessageEnemy || alliance == playeralliance`,
so an enemy adoption only makes the news if the record's line-14 flag is set.
It lazily calls `policy.Initialize()` first, and takes the dateline from the
player's capital. Content comes from the policy record's lines 16–20
(`Headlines[5]`), 21 (`PictureID`), 22 (`HeadlinePicture`) and 23 (`Content`).

### 3.2 Campaign objectives

`CampaignObjective.LaunchNewspaperMessage(int uniqueobjectiveid)` is
`public static`, resolves the objective by id and calls
`newspaper.Show(Headlines, content, PictureID, HeadlinePicture, location)`.

Two differences from the policy path: there is **no enemy filter** — every
objective's paper shows to both sides — and the location is unconditionally the
**player's** capital. Content comes from the objective record's lines 14–18,
19 and 20 ([24](24-campaign-objectives.md) §1.1). All five call sites are
inside `CheckIfAccomplished`, each guarded by `!flag` (the pre-call
`accomplished` value), so a paper fires once per objective.

### 3.3 The channels that are *not* the newspaper

- **`Messages`** (1,633 lines) is the small on-screen dispatch channel, with
  ~45 typed ids declared as `public static int msg_*` (0–28 battle, 50–91
  campaign; `msg_combatresult = 50` and `msg_navalcombatresult = 51` fire
  alongside the battle paper, and `msg_officerlossreport = 61` carries the
  casualty list). Its text is assembled in code, not authored. A full write-up
  is separate work.
- **`Diary`** (779 lines) reads `DLC_WL.ConfigFileNameDiary = "config/diary.dat"`,
  which **is not present in this install**, so the diary is DLC content and
  inert here. `BattleUnits.Update` still drives
  `Diary.CheckDiaryEventFireUp()` every 6 frames and `Diary.UpdateEventQueue()`
  every 17, and `DiaryReference.Initialize()` runs at frame 21
  ([10](10-simulation-tick.md)). It belongs in the DLC write-up.

---

## 4. `endbattle` and `battleresulttype`

The battle paper is driven by two small enumerations that no earlier file
pinned down. Both are also recorded in
[16-enumerations](16-enumerations.md).

### 4.1 `BattleUnits.endbattle` / `BattleMonument.battleendtype`

`SetEndOfBattle(int action)` is the only writer. It refuses to overwrite a
value unless the current one is `< 0` or one of the transient states
`{3, 4, 5, 8, 10, 12}`, and `action == 11` is special-cased to set
`BattleController.quitbattlescene` **without touching `endbattle`**.

| Value | Meaning | Set by |
| --- | --- | --- |
| −1 | none / prompt dismissed | `BattleUnits` ~1422, `PanelHandler` |
| 0 | enemy retreat timer expired while the enemy was below the victory trigger — **enemy routed** | `BattleUnits` ~6006, `Autocalc`, DLC |
| 1 | DLC career-panel outcome `LEFTFIELD0` | `DLC_WL` ~7285 |
| 2 | **enemy** side withdraws or is annihilated | `BattleUnits` ~5499/~5973, `Autocalc` |
| 3 | player pressed **Retreat** — prompt stage | `PanelHandler` ~2040 |
| 4 | player retreat confirmed during the end-of-day cycle | `PanelHandler` ~2051 |
| 5 | player pressed **Surrender** — prompt stage, and the value that survives to export | `PanelHandler` ~2061 |
| 6 | **AI surrenders** automatically (casualty rate and morale below `casualtiesratef orsurrender` / `maxmoraleforsurrender`) | `BattleUnits.CheckAISurrender` ~5560 |
| 7 | **player** side withdraws or is annihilated | `BattleUnits` ~5495/~5968/~5999, `Autocalc`, DLC |
| 8 | the player's side fell below the victory trigger — retreat timer starts | `BattleUnits` ~5956, `AIBattle`, `Autocalc` |
| 9 | **never assigned** | — |
| 10 | player pressed **Cease Fire** — prompt stage | `PanelHandler` ~2026 |
| 11 | quit the battle scene (does not change `endbattle`) | `PanelHandler` ×7 |
| 12 | a non-player AI side's retreat timer starts | `AIBattle` ~3231/~3255 |
| 13 | enemy retreat timer expired while the enemy was **not** below the trigger — **enemy withdraws in order** | `BattleUnits` ~6010 |

3, 8, 10 and 12 are transient: `BattleUnits.Update` ~1595 excludes 3, 8 and 12
from the end-of-battle pause, 8 and 3 resolve into 7 (or 4), 12 resolves into
0 or 13, and 10 resolves to −1 plus `triggerendofday`. The values that actually
reach `ExportBattleResultData` are **0, 1, 2, 4, 5, 6, 7, 13** from a played
battle and **0, 2, 7, 8** from a land autocalc.

### 4.2 `battleresulttype` — player-relative

`BattleUnits.GetBattleResultType()` returns **0 victory, 1 glorious victory,
2 defeat, 3 disaster**, always from the player's point of view. It compares
victory-point relations, promotes to 1 or 3 on a `casualtyratio` vs
`losstriggersown` / `losstriggersenemy` test, then applies three overrides:
`endbattle ∈ {2, 13}` forces a win, `endbattle ∈ {4, 7}` forces a loss, and
`endbattle == 6` forces **1**. The last is correct rather than suspicious:
`CheckAISurrender` is gated on `GameVars.playeralliance != foralliance`, so
only the AI can reach 6. (Its second clause,
`(foralliance != playeralliance || GameVars.ai_vs_ai)`, is redundant — the
first clause already implies it — so `ai_vs_ai` never reaches the surrender
check for the alliance that happens to equal `playeralliance`.)

`Autocalc.FinishLandBattle` ignores that method and derives the type straight
from its own `endbattle`: **2 → 0, 0 → 1, 7 → 2, 8 → 3**. Naval autocalc
(`Autocalc` ~3540) uses only `endbattle ∈ {0, 8}`.

---

## 5. The battle report

`ImportExportUnitData.WriteBattleResultToMonument(List<BattleMonument>, …)`
builds one paper per monument from `battlefields/999/A/battleresult.dat` — the
file `ExportBattleResultData` wrote at ~2397 on the way out of the battle
scene (`SceneManagement.ExitCampaignBattleAndRebuildCampaign` →
`BattleUnits.SaveBattleResult`). It is the **only** producer that passes
`overridepause: true`, so the battle paper appears even while the game is
paused.

### 5.1 Suppression for small naval actions

`flag3` gates the `Show` call. It is cleared when `landorsea == 1` and the
participant count is below `GamePrefs.minshipsfornewspapernavalengagement`
(**8**), or when both of two other flags are set. The same pass still adds the
`msg_navalcombatresult` dispatch, so a small engagement is reported in the
message log but gets no paper.

### 5.2 Headline assembly

Headlines are appended in a fixed order, each omitted when it returns `""`:

| Slot | Source | Content |
| --- | --- | --- |
| 0 | `GetNewspaperHeadline0()` | `"Victory at X"` / `"Glorious victory at X"` / `"Defeat at X"` / `"Disaster at X"` from `battleresulttype`, with `"Battle of "` stripped from `battlename` |
| 1 | commander or unit name **+** `GetNewspaperHeadline1()` | land: `"in retreat"` / `"surrenders"` / `"fleeing in panic"` / `"withdraws"`; sea: always `"in retreat"` |
| — | `GetNewspaperHeadline5()` | naval only: ships sunk, captured, or `"All enemy ships escaped!"` / `"Our fleet managed to escape!"` |
| — | `GetNewspaperHeadline2()` | the **player** side's commander became a national hero or lost face |
| — | `GetNewspaperHeadline3()` | the **enemy** commander lost face |
| — | `GetNewspaperHeadline4()` | national-morale colour (§5.5) |

`GetSides()` returns `[player side, enemy side, losing side, winning side]`,
resolving a battle between two third parties by `IsInAllianceWith`.

### 5.3 Body

One long concatenation, land and sea variants. The land version names the
battle, the duration in days, both forces' strength and guns, the hours fought,
total casualties, the winning commander and `GetNewspaperResult()` (`"forcing
the X to withdraw"` / `"forcing the X to surrender"` / `"whipping the X
thouroughly"` — the typo is shipped), then captured weapons bucketed by
`newspaperweaponsamount{small, substantial, large}` = **1000 / 2500 / 5000**,
then prisoners, fame and defame lines, up to
`GamePrefs.numberofkiacommandersinnews` (**5**) KIA commanders per side, and
finally `GetNewspaperMorale()`.

The naval version lists the top participating units by `gameObject.name` on
both sides, ship counts, hours, ships lost and prizes taken.

### 5.4 Every diagnostic fall-through is unreachable — today

`BattleMonument` has **ten** `GetNewspaper*` methods. Four of them end in a
diagnostic string rather than `""`, and these are **printed into the paper**,
not logged:

| Method | Fall-through |
| --- | --- |
| `GetNewspaperResult` (~963) | `"no newspaper result, type:X result:Y"` |
| `GetNewspaperHeadline0` (~985) | `"no newspaper headline 0, type:X"` |
| `GetNewspaperHeadline1` (~1018) | `"no newspaper headline 1, type:X result:Y"` |
| `GetNewspaperImageHeadline` (~1175) | `"No image headline, result:X, endt:Y"` |

A fifth lives in `GetBattleEndTypeText` (~816), which is not a newspaper
method — it feeds `Messages` ~589/~643/~647, the combat-result dispatch — but
has the same branch structure and therefore the same coverage.

Checking them against §4's reachable sets:

- `GetNewspaperHeadline0` covers `battleresulttype` 0–3, which is the whole
  range.
- `GetNewspaperResult` and `GetNewspaperHeadline1` cover
  `endbattle ∈ {0,1,2,4,5,6,7,13}` directly, and anything with
  `battleresulttype ∈ {1,3}` through a result-only branch. The only exported
  value not in the first set is autocalc's **8**, which `FinishLandBattle`
  pairs with `battleresulttype = 3` — so it lands in the second.
- `GetNewspaperImageHeadline`'s **sea** branch handles only `endbattle` 0 and
  8, which is exactly what naval autocalc produces.

So no diagnostic string can surface in the shipped game. They are a real hazard
for a mod that introduces a new `endbattle` value or a fifth
`battleresulttype`: the paper will print the debug text verbatim.

### 5.5 The morale colour is a constant

`GetNewspaperHeadline4` and `GetNewspaperMorale` branch on
`nationalmoraleeffect[…]` against two prefs whose **only** consumers they are:

| Pref | Shipped value |
| --- | --- |
| `newspaperminornatmoraleloss` | **−1.0** |
| `newspapermediocrenatmoraleloss` | **−2.0** |

`nationalmoraleeffect[side]` is `BattleUnits.GetLossInfluenceOnNationalMorale`,
which returns `−(losses^0.3 × 0.0004)` scaled by at most
`influenceonnationalmoralebattlelost = 2.0`, `…retreat = 1.5` or
`…surrender = 3.0` (and `ltgenlossmorale`), with the **winner's entry zeroed**
by the exporter. For 30,000 losses that is about **−0.026**; national morale
itself runs 0–1 ([23](23-game-state.md) §6).

The thresholds are therefore two orders of magnitude out of range, and:

- `GetNewspaperHeadline4` always takes its first branch — **"Support for the …
  cause wavers."** The *"Unity among the … supporters cracking."* and
  *"A significant increase in … war-weariness."* variants are unreachable.
- `GetNewspaperMorale` always takes its second branch — **"the public unmoved
  by the recent events."** The *"cheering crowds"* branch needs
  `> 0.01f` and the function can only return `≤ 0`, so it is unreachable even
  with corrected thresholds; the *"dissatisfied citizen"* and *"in shock after
  the licking"* branches need the thresholds fixed.

Both files are data, so **this is a one-line-each config fix** — set them to
roughly `-0.01` and `-0.02` — with no code change and no other consumer to
break.

There is a second defect underneath, currently masked by the first.
`GetNewspaperHeadline4` indexes `nationalmoraleeffect[sides[2]]` — the
**loser**, correct. `GetNewspaperMorale` indexes `nationalmoraleeffect[sides[0]]`
— the **player** — while narrating `"Sources from the <enemy> side report …"`
and closing with `"…whether they can resist the military might of the
<playeralliance>"`. Since the winner's entry is zeroed, fixing only the
thresholds would make the enemy press report itself "unmoved" after being
routed, and "in shock" after winning. **A modder who fixes the thresholds must
also change `sides[0]` to `sides[1]`.**

Two typos in the same two methods reach the player: **"ceering crowds"** and
**"maretime might"**.

### 5.6 Image selection, and a probable swap

`GetNewspaperImageID()`:

| Branch | Returns |
| --- | --- |
| land, `battleresulttype == 5` | 6 |
| land, `battleresulttype == 6` | 7 |
| land, `endbattle == 0` and player is the CSA | 3 |
| land, `endbattle == 1` and player is the CSA | 4 |
| land, `endbattle == 0` | 1 |
| land, anything else | **0** (after two orphaned statements, `_ = battleendtype; _ = 1;`) |
| sea, `(0, Union)` or `(8, CSA)` | 8 |
| sea, `(0, CSA)` or `(8, Union)` | 9 |
| sea, anything else | 10 |

`battleresulttype` only ever takes 0–3 (§4.2), so **sprites 6 and 7 are dead
art**. `endbattle == 1` is DLC-only, so base-game land papers use ids **0, 1
and 3** and naval papers **8, 9 and 10**.

The land fallback is worth a second look. Id 1 is returned for
`endbattle == 0`, which is *the enemy being routed* — a player victory — while
id **0** is the catch-all that every defeat falls into. The two unread
inspector fields on `Newspaper` are declared `ImgID_Victory` (default 0) and
`ImgID_Lost = 1`. If those names describe sprites 0 and 1, the land branch has
them **swapped**: a routing victory shows the defeat picture and every defeat
shows the victory picture. Both fields are prefab-serialized and the sprites
live in `Resources`, so **confirming this needs an asset dump** of
`UI/Newspaper/0` and `UI/Newspaper/1` and of the prefab's serialized values.
Flagged, not asserted.

---

## 6. Save / load

| What | Where |
| --- | --- |
| `GameVars.historicalnewspaperstriggered` | written `ImportExportUnitData` ~1077–1080 (count + ids), read ~1578–1582 |
| `CampaignObjective` per-objective `accomplished` / `deactivated` / `lastupdate` and the eleven war-progress statics | [24](24-campaign-objectives.md) §9 |
| `Policy.researchprogress` | drives §2.5's gate; [22](22-policy-and-project-catalogue.md) §3 |
| `runningnumber`, `newspaperqueue`, `historicaleventsrunthrough`, `lasthistoricaleventsupdate` | **not saved** — the queue is lost on save/exit and the issue number restarts at 1 |

A paper still sitting in the queue when the player saves and quits is
therefore never seen.

---

## 7. Defects and dead content

1. **The morale colour of every battle paper is a constant string** —
   thresholds −1.0 / −2.0 against values bounded by about −0.03 (§5.5). Data
   fix.
2. **`GetNewspaperMorale` reads the player's morale effect while narrating the
   enemy's home front** — `sides[0]` where `sides[1]` is meant. Inert until
   defect 1 is fixed, then inverting (§5.5).
3. **Eleven dated events are unreachable** in every shipped start, ten of them a
   complete secession-winter chain with art; one (id 33) is dated 1860 where
   1861 reads as intended (§2.9).
4. **The loaded newspaper sprite is never unloaded.** `ImageS` is captured once
   from the prefab and never reassigned, so `UnloadAsset` targets the wrong
   sprite and each paper's image leaks (§1.3).
5. **A missing `imageid` silently reuses the previous paper's picture** rather
   than blanking or logging (§1.3).
6. **`runningnumber` is not saved** — the issue number restarts at 1 after every
   load while the volume stays correct (§1.2).
7. **A queued paper is lost on save/quit** and on `Dispatch.OnDisable`
   (§1.6, §6).
8. **The chapter gate is keyed on the cursor index, not the id** — inserting a
   record before index 42 moves the Fort Sumter suppression window onto four
   unrelated events (§2.5).
9. **Sprites 6 and 7 are dead art**, selected only by `battleresulttype` 5 and
   6, which the game never produces (§5.6).
10. **`ImgID_Victory` / `ImgID_Lost` have no readers**, and the land image
    fallback looks inverted relative to their names (§1.4, §5.6).
11. **`GamePrefs.maxgamespeed` has no consumer** — the ceilings are the
    hardcoded `gamespeedcampaigns` / `gamespeedbattles` tails (§2.4).
12. **Four newspaper generators print diagnostic strings into the paper** on an unhandled
    `(endbattle, battleresulttype)` pair. Unreachable today; a hazard for mods
    (§5.4).
13. **Shipped typos in player-visible text**: `"whipping the X thouroughly"`,
    `"ceering crowds"`, `"maretime might"`, `"dissatisfied citizen"`, and
    `"wanderïng freely"` in event 54's prose.
14. **`CheckAISurrender`'s guard is redundant** — its `|| GameVars.ai_vs_ai`
    arm is unreachable because the first clause already excludes the player's
    alliance (§4.2).
15. **`AddTradeWarfare`-style orphans** survive in
    `GetNewspaperImageID` (`_ = battleendtype; _ = 1;`), `GetNewspaperResult`
    and `GetNewspaperHeadline1` (`_ = currentcommand…`) — the same
    dead-assignment pattern seen throughout
    ([23](23-game-state.md) §7.5).

---

## 8. Mod seams

1. **Adding or retiming a historical event needs no code.** Edit
   `Config/historicalevents.dat`, keep the 12-line shape, keep `id == index`
   below 42 if you want the Fort Sumter window intact, and give a script-only
   event the `01-01-1900` sentinel date. A per-sublevel
   `Campaigns/001/<sublevel>/historicalevents.dat` overrides the whole file —
   the right place to retime the secession chain for a late start.
2. **`Newspaper.LaunchHistoricalEventByID` is public and non-static** on a
   component reachable by `GameObject.Find("UI/Newspaper")`, so a mod can fire
   any catalogue entry from its own trigger. Pass
   `checkifalreadylaunched: true` for once-per-campaign semantics and it will
   be recorded in the save.
3. **`Newspaper.Show` is public** — five arguments is enough for a fully
   mod-authored paper, and `overridepause: true` makes it appear through a
   pause. The queue, the 20-frame cooldown and the pause/resume are all handled
   for you.
4. **`CampaignObjective.LaunchNewspaperMessage(id)` is `public static`** and
   usable on its own, without the objective being accomplished.
5. **`Policies.LaunchNewspaperMessage(alliance, policy)` is `public static`**
   and the only path that respects `ShowNewspaperMessageEnemy`.
6. **Postfix the ten `BattleMonument.GetNewspaper*` methods** for the battle
   paper. They are pure functions of monument state — nine return strings, one
   an image id — with no side effects, and the cleanest text-rewriting surface
   in the game. The two morale methods are the ones worth fixing (§5.5).
7. **Prefix `Newspaper.UpdateShow`** to intercept or rewrite any paper
   regardless of producer, including its image id; it is the single chokepoint.
8. **Do not instantiate the `Newspaper` prefab at runtime** — `Awake()`
   re-creates `historicalnewspaperstriggered` and `newspaperqueue` and resets
   `runningnumber`, discarding the campaign's event log.

---

## 9. Open questions

- **Which `UI/Newspaper/<id>` sprites exist.** Needs an asset dump. The dated
  events reference 11–96 plus **506, 507, 513, 522, 526**; the battle paper
  references 0, 1, 3, 4, 8, 9, 10 and the dead 6, 7. The >500 block may be a
  DLC set. Also needed: the prefab's serialized `ImgID_Victory` / `ImgID_Lost`
  and whether sprites 0 and 1 are swapped (§5.6).
- **`Messages`** — the ~45 typed dispatch ids, their text assembly, the
  `checkifalreadyexist` de-duplication window
  (`Time to last message to check for equal messages = 0.005` campaign yr) and
  `OpenMessageOutright`. Its own write-up.
- **`Diary`** — `config/diary.dat` is absent from this install; belongs with
  the `DLC_WL` pass.
- **`Newspaper.message`** (a `Messages` reference) is only used by `OnDisable`
  to call `CloseMessage()`. Why the newspaper owns a message reference at all
  is unclear.
- **The `PanelsWhenOpenedNotResetSpeed` array** is prefab data; which panels
  are in it decides when closing a paper does *not* resume the game.
- **`BattleMonument`'s remaining surface.** This file covers only its ten
  newspaper methods; the monument is also the campaign-map battle marker with
  a balance bar, and `GetBattleResultText` / `CampaignStrategy.JournalEntry`
  feed a separate journal channel.
- Whether the two `0x96` en dashes and `Encoding.Default` behave identically on
  a non-Western Windows codepage. `Encoding.Default` on Unity's Mono profile is
  the OS ANSI codepage, so a CJK locale would decode those two bytes
  differently — a portability question that also applies to every other
  `Encoding.Default` loader in [06](06-config-file-format.md).
