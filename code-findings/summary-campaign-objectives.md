# Summary: campaign objectives

**Build:** see [00-build-provenance](00-build-provenance.md)
**Source:** covers [24-campaign-objectives](24-campaign-objectives.md)

The war's narrative arc: what ends the campaign, what drives the chapters, and
where the foreign-intervention trigger comes from. This was the last untouched
campaign subsystem.

---

## Architecture: the project model, not the policy model

findings/22 found two architectures for one feature. `CampaignObjective` sits
firmly on the data-driven side: **one prefab, instantiated once per record**,
every field filled from `Config/campaignobjectives.dat`.
`1 + 38 × 20 = 761` lines verified.

But the path is hardcoded, lowercase and **global** — there is no scenario
override, so per-scenario selection happens through the record's own
`ObjectiveScenario` field. And it is the **one loader in the file set that
omits `Encoding.Default`**, so `StreamReader`'s UTF-8 default mangles every
Windows-1252 character in the shipped newspaper prose. A one-argument fix, and
visible in the game.

## 38 objectives, two sets

**Ids 0–28** are the main campaign (`001`): strategic war goals, empty town
lists, no prestige, each hardcoded as its own `if (UniqueObjectiveID == N)`
block. **Ids 29–37** are scenario `002`: town/IIP lists, prestige 500–2,500
paid through `DLC_WL.AdjustPrestige`, and almost no code — the generic
"do you hold every town on the list" path handles them. Since this install has
no `Campaigns/002`, the second set is DLC content and unreachable here.

Two nice pieces of modelling in the first set: objectives 2 and 3 (**Capture
Montgomery** vs **Capture Richmond**) are made mutually exclusive by **policy
170**, so the Confederate capital move is expressed as a policy; and objectives
15/16 snap Kentucky's and Missouri's Confederate support **up to a floor of
0.75** rather than adding to it — a one-way ratchet written through the normal
clamped adder.

## Two elections, and one of them ends the game

- **4 November 1862, objective 9.** `alliance[0].averagesupport` must reach
  0.60. Failing it charges −0.01 Union national morale per week — but the
  objective's chapter list is `{1}` and chapter 2 begins on 6 November, so the
  charge runs for **two days** and totals about **0.003**. As shipped the 1862
  midterms have no mechanical consequence, against a config label describing an
  open-ended rate.
- **9 November 1864, objectives 26/27.** Union national morale must be ≥ 0.50.
  Both objectives are live in the same chapter and the sweep walks them in id
  order in one loop, so failing it charges **−0.20 twice**. From below 0.50,
  −0.40 puts the Union under `breakmoraletrigger = 0.25`, and objective 1
  "Break Union Morale" is evaluated in the same sweep — which runs every ten
  game minutes.

> **The 1864 election is a pass/fail test at 50 points, not a −20 setback.**
> Failing it ends the campaign that afternoon, not on inauguration day: the
> 4 March 1865 branch is unreachable in both directions, because a held
> election advances the chapter and deactivates both objectives, and a lost one
> has already ended the campaign through objective 1. Neither tooltip, each of
> which promises "−20", says any of this.
>
> **Corrects an earlier reading of this summary**, which had the 1862 penalty
> running for the whole campaign and the 1864 loss arriving at the
> inauguration. The two real loss conditions are national morale below 25 %,
> and — for the Union only — the 50 % reading on one specific day.

## The other half of the intervention script

findings/20 found the script needs **both** `GetInterventionProbability >= 1`
**and** `winmajorbattleintervention == <side>`. This is where the second half
lives: it is a static reset in `Awake`, written by the battle-result path,
saved with the objective state, and consumed by objectives 24 and 25.

Both deactivate outright if any European power is *already* at war — they are
strictly pre-intervention — and each needs its own patron close to committing:
**objective 24 (Union) watches Spain**, objective 25 (CSA) watches Britain or
France, each at probability ≥ 0.75. That matches findings/17 §4 exactly.

The payoff is **asymmetric by 2.5×**: a Union major victory moves intervention
levels −0.10, a Confederate one +0.25. A "major battle" means **each army ≥
10,000 strong and ≥ 2,000 total casualties**, and the winner's army commander
also takes +0.50 fame.

## Reason codes: the last gaps closed

National morale codes **4–12 and 14** and support codes **7, 8 and 41** are all
written here, with their objectives, prefs and shipped values tabulated in
[24](24-campaign-objectives.md) §6. Highlights:

- Code 8 "Elections" covers all three election mechanics (objectives 9, 26, 27).
- **Code 10 is shared by four unrelated mechanics** — invading the North,
  consecutive land battles, consecutive naval battles, and beating a European
  power — so the player's morale breakdown cannot tell them apart.
- Codes 5, 6 and objective 9's arm are **rates**, rescaled `× 365` or `× 52`
  from per-day and per-week prefs.
- Support code 8's three sites are objectives 14 → state 39, 15 → state 14,
  16 → state 22, confirming the inference in findings/22 §8.1.

## Objectives choose the map's state set — and the rename trap is live

`BattlefieldSetup.UpdateStateOccupation` picks the state set from the date,
then overrides it: **objective 14 ("Capture Western Virginia") forces set 4**
in the main campaign, and **objective 29 flips scenario 002 between sets 0 and
1.** Changing the set rewrites `nation[i].name` from `statesetname[]`.

findings/23 §4.1 called that trap latent. It is not — `Campaigns/001/A` fills
`statesetname` for **Kansas** ("Kansas Territory" in sets 1–2) and
**Minnesota** ("Minnesota Territory" in sets 0–1). Kansas is one of the five
states policy 30's volunteer bonus matches **by string**.

It does not bite in the shipped game, because the sets are chronological
(Minnesota admitted 1858, Kansas January 1861), so an 1861 campaign starts at
set 3 where Kansas is "Kansas" again — and a blank entry leaves the previous
name in place, so it stays correct through sets 4–5. **A scenario that starts
earlier, or fills more slots, silently disables the bonus.**

## Traps

- **`Awake()` resets all eleven global war-progress statics**, including
  `winmajorbattleintervention`. It runs per instance, which is harmless during
  initialization but means **instantiating the prefab at runtime wipes the
  war's progress**. Add objectives through the data file.
- **Unresolvable town names are silently dropped** at load, and a list that
  empties completely moves the objective onto a different evaluation path
  rather than failing. For objectives 29/30 specifically that is dangerous:
  their id-specific block sets `accomplished = true` unconditionally and relies
  on the town check to veto it, so an empty list hands West Virginia over the
  moment the objective becomes valid.
- **`CheckIfAccomplished` is not a switch** — it is a flat chain of ~38
  `if (UniqueObjectiveID == N)` blocks, all evaluated every call for every
  objective. There is no per-objective hook target, only the whole method.
- An `ObjectiveChapters` list that is **empty means "every chapter"**, not
  "no chapter".

## Mod seams

1. **A new objective with a town/IIP list needs no code at all** — the generic
   path handles it, which is how all nine scenario-`002` objectives work. The
   cheapest content addition in the game.
2. A new objective **without** a town list and without a block in
   `CheckIfAccomplished` is evaluated by nothing and can never complete.
3. `CheckIfAccomplished` postfix, keyed on `UniqueObjectiveID`, for scripted
   conditions — set `accomplished` and let the tail fire the newspaper and the
   prestige award.
4. `GetObjectiveAccomplishment`, `GetObjectiveFromID` and
   `LaunchNewspaperMessage` are all `public static`; the last is usable on its
   own for mod-authored newspaper events.

## What this leaves open

Who *writes* the remaining statics — `unioncapturedforts`,
`consecutivevictories[4]` (four slots for two sides and two battle kinds),
`unioncasualties`, `victoryagainstgb/fr` — almost certainly the battle-result
export and the fort-capture path. **Two are now located:**
`Town.UpdateOwnership` increments `csacapturedtowns` when a Union town in a
Union-loyal state falls after chapter 0, and sets `confederacyinvaded` on the
mirror case ([17](17-politics-and-support.md) §3a). The `Frontline2` state-set
cut-over dates are inspector data and need an asset dump.
