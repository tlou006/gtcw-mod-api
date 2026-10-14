# Summary — policies and projects

**Build:** see [00-build-provenance](00-build-provenance.md)
**Covers:** [22-policy-and-project-catalogue](22-policy-and-project-catalogue.md),
and completes [09-policy-and-project-effects](09-policy-and-project-effects.md)

---

## They are one feature in the UI and two systems in the code

| | Policies | Projects |
| --- | --- | --- |
| Definition | a **`Policy` MonoBehaviour per id**, authored in the Unity inspector | a record in `Config/projects.dat` |
| What the config file does | **overrides** existing prefabs, matched by id | **is** the definition |
| Add one by editing config? | **No** | **Yes**, and 56 ids are reserved for it |
| Paid in | research time (game-years) | subsidy money, per level |
| Effects | one 1,200-line `switch`, **replayed every tick** | three unrelated mechanisms |
| Count | **117** | **125** (69 real, 56 `PLACEHOLDER`) |

That asymmetry is the practical headline. `Config/projects.dat` is the whole
truth about projects — `Projects.LoadedProjects.Import` reads it and there is no
prefab anywhere — and ids **20–29 and 42–87** ship as `PLACEHOLDER`, a 56-slot
reserved range. `Config/policies.dat` (23 lines per record, verified
`1 + 117 × 23 = 2,692`) can only reshape policies the scene already contains;
an unmatched id is read and discarded.

For the Phase 3 gating question left open in
[09](09-policy-and-project-effects.md), the answer is now concrete rather than
hopeful. **Projects: yes, trivially.** **Policies: not from config, but yes at
runtime** — `Policy.InitializeStatics()` is `public static` and rebuilds
`allpolicies` from `GameObject.Find("UI").GetComponentsInChildren<Policy>(includeInactive: true)`,
so cloning a policy GameObject under `UI`, rewriting `UniquePolicyID` and
calling that method should register it. Three things still need a live build to
confirm; they are listed in
[22](22-policy-and-project-catalogue.md) §16.

---

## The contract for hooking an effect

[09](09-policy-and-project-effects.md) established that `LaunchPolicyEffect`
has 117 case labels and no `default:`, so new ids are inert and a postfix can
supply behaviour. What it did not say is **how often the postfix runs**.

`Policies.UpdatePolicyEffects` executes every campaign tick and does this:

1. resets thirteen fields — `ratingmodifier`, `treasuryadjustment`,
   `publicwealthfactor`, `supplyenemysoilfactor`, `transportcapacityports`,
   `civilianships`, `volunteerspolicyadj`, and the five subsidy factors to
   their index-0 baselines;
2. calls `LaunchPolicyEffect` for **every completed policy and act**;
3. calls `DeactivatePolicyEffect` for acts no longer held.

So a policy's effect is **recomputed from scratch** once per **12 game hours
per alliance** — the cadence of `UpdateAllianceData`'s round-robin
([23-game-state](23-game-state.md) §6.1). The game's own
cases handle this in one of two ways: guard with
`if (!alreadyactivatedacts.Contains(policy))` to fire once, or multiply by
`timediff` to apply a rate. A mod must do the same, and anything it accumulates
into that is not one of those thirteen fields will grow without bound.

`DeactivatePolicyEffect` implements exactly **one** policy — 131, Restrict
Cotton Trade — and it restores European cotton demand by *dividing* by the
factor rather than restoring the prior value, so a second cancellation
compounds. Everything else is either recovered by the per-tick reset or is
permanent: created units, state reassignments, building levels, forts,
railroads.

---

## The five policy ladders each do exactly one thing

Government Funding, Bread Basket / King Cotton, Industrialization, Military and
Diplomacy are the backbone of the policy tree — four or five levels each, the
longest research times, and the prerequisites for most acts. Every level does
one thing: `AdjustSubsidyFactor` on a single subsidy category, from
`{0.1, 0.25, 0.5, 1, 2}`.

The config describes more. **Ten `float[4]` arrays are parsed from
`campaignprefs.txt` and referenced nowhere in the assembly:**

- Bread Basket / King Cotton ships `agriculturalintervention`
  (0, 0.05, 0.05, 0.05 — European relations) and `agriculturalsupportloyal`
  (0, 0, 0, 0.05 — loyal-state support). Neither exists.
- Industrialization ships a transportation-subsidy cap and a national-morale
  bonus. Neither exists.
- **Military ships six**: recruitment, civil-order and trade-war subsidy caps,
  army railroad speed, transport upkeep, and a military-experience bonus. None
  exists.

Two pieces of corroboration make this one abandoned design rather than ten
oversights. `GameVars` carries three vestigial subsidy constants —
`subsidy_transportx = 4` (duplicating military), `subsidy_tradewarx = 5`
(duplicating diplomacy) and `subsidy_civilorderx = 7` — matching three of the
dead arrays; and the `basicsubsidiescost[8]` config label names the eight
categories as *"politics, economy, agriculture, industry, military, diplomacy,
**n/a, n/a**"*. An eight-category subsidy system was cut to six, and the policy
ladders' secondary effects went with it. Every AI personality ships
`subsidyfocus[6] = subsidyfocus[7] = 0`, confirming it.

The scar is visible in the source: case 3/103 contains
`if (alreadyactivatedacts.Contains(policy)) { }` — an empty body where the
agricultural support call used to be. **Support reason code 9, "Policy:
Agriculture", is a live label with no producer.**

---

## Militia Act I starts the war

Policy 36/136 is not a recruiting policy. Its case body hands over every fort,
reassigns **every contested state to whichever side it supports more**,
declares war, and flips ten border states to recruitable — Arkansas, Indian
Territory, Kansas, Kentucky, Missouri, North Carolina, Tennessee, Virginia,
West Virginia and New Mexico Territory. It is also the only case in the switch
that uses `goto`.

Chapters hang off it. `Policy.CurrentChapter` is derived state, recomputed from
zero on every call:

| Chapter | Begins |
| --- | --- |
| 0 | campaign start |
| 1 | Union policy 36 completed, **or** automatically on 15 April 1861 |
| 2 | 5 November 1862 |
| 3 | 9 November 1864, with campaign objective 26 done and 27 not |

Scenario `002` starts at chapter 1. Before the war starts, **Fort Sumter is
handed over on 14 April 1861** by a lookup on *name and world coordinate* —
`GetBuildingByNameAndPosition("FtSumter", (1547, 489, −1304), id_fort, 10f)` —
the most brittle lookup found anywhere in the assembly so far.

`IsDeactivated` is the entire exclusion system and has two entries: policy 36
is suppressed before 4 March 1861 (Lincoln's inauguration), and policy 170
(The Old Dominion) while Virginia is still Union.

---

## Parallel research is a trap, and the AI never falls into it

Research rate per policy is

```
averagesupport / N^1.1 × (1 + 0.05 × projectLevel(95)) / ResearchDuration
```

where N is the count in research. Total throughput across N is
`N × N^-1.1 = N^-0.1`, which **decreases** with N: at the cap of 12 you get
0.79× the aggregate rate of researching one at a time. The slot cap itself
(7 to 12) is the *only* thing the politics subsidy does.

`Policies.CheckAIPolicyChange` walks the personality's authored policy queue in
order and researches the **first eligible** entry, and returns immediately if
anything is already in research. **The AI holds at most one policy at a time**
and never pays the penalty. Acts, meanwhile, bypass the slot cap but still
count toward N — so queueing acts is a way to slow your own research past a
limit that was supposed to stop you.

The AI's three pre-war "grand" policies are the **first three** entries of its
personality's `prewarpolicies` list, so **17 of the 20 authored slots per
personality are never read**. And of the twenty personalities, only the
*Historical* doctrine has side-specific economics: ids 1–7 carry identical tax,
subsidy and project-spending numbers for Union and Confederacy, differing only
in their policy and project id lists and in slot 5's name
(Agricultural / King Cotton).

---

## Projects: three mechanisms, and the data file does not say which

- **Push, continuous** — `UpdateProjectEffects()` re-runs for every held id.
  Projects 0–19 unlock weapons (`SetAsImported` for foreign purchase,
  `SetResearched` for domestic production), 30–41 unlock ship types, and
  exactly two modifier projects (101 Military Railroad, 102 Weapon Production)
  are pushed here.
- **Push, once** — `UpdateOneTimeProjectsEffects(id, alliance)`: 12 ids,
  including the four propaganda/occupation projects (**support reason codes
  34–37**) and the two reforms that rewrite unit `overridesymbol`.
- **Pull** — everything else is read where it is used, as
  `1 + GetProjectLevel(id) × pref`. `GetProjectLevel` **counts occurrences of
  the id**, so a repeating project's level is its multiplicity.

Two findings worth carrying elsewhere:

**The "reforms" that other findings files call "post-reform" are projects.**
`Alliance.HasOrganizationReform()` returns `projects.Contains(90)` when
`GamePrefs.commandhierarchyshift == 0` and `projects.Contains(89)` when it is
−1 — which is why there are two projects both named "Organization Reform". The
shipped shift is 0, so **project 90 is live and 89 is unreachable scaffolding
for the other configuration**. Project 90 is what raises the `campaignranking`
clamp from 15 to 16 ([19](19-commanders-and-perks.md)) and applies the 0.9
supply-consumption factor. Similarly `IsHorseArtilleryAvailable()` keys on
project **123 in scenario `002`** and **111 everywhere else**, and
`AllowsArmyGroups()` needs the alliance flag **plus project 88 or 89** — which
closes the Army Group question left open in
[19](19-commanders-and-perks.md).

**Without Cavalry Reform (110) or Artillery Reform (112 / 124), those branches
are crippled by design** — maximum brigade size is multiplied by 0.2 and 0.375
respectively, and both arms sit a level lower in the command hierarchy.

---

## Reason codes, finished

Mapping `supportdescr[42]` and `nationalmoraledescr[35]` to their writers
completes the open question in
[17-politics-and-support](17-politics-and-support.md):
**support codes 9–31 are the policy block** and 34–37 are projects 91–94;
**national-morale codes 20–31 are the policy block**, plus code 5 for Militia
Act I. The full id → code table is in
[22](22-policy-and-project-catalogue.md) §8.

Three codes are live labels with no producer anywhere in the assembly:
**support 9** ("Policy: Agriculture"), **support 33** ("Condition of enemy POW
camps" — which answers another findings/17 question: there is no mechanic, only
a breakdown line) and **national morale 32** ("Industrialization (Policy)").

---

## Hardcoded indices, decoded

`nations.dat` gives the state table — 53 entries in scenario 001, 19 lines per
record, and an alliance block whose count line is **8**, confirming the eight
alliances of [17](17-politics-and-support.md). Every bare index in the policy
switch now reads plainly: *Go West!* makes **California and Oregon**
recruitable, *Slavery to the West* takes **New Mexico and Arizona Territory**
into the Confederacy and upgrades the **Texas** harbours, *Filibustering*
annexes **Cuba and Nicaragua**, *Alliance with the Natives* takes **Indian
Territory**, *Support Mexican Intervention* mobilises **Texas** and declares war
on Mexico, and *The Old Dominion* moves the capital to **Richmond** by name.

Two concrete errors fall out:

**Policy 30 (Northern Routes) has two effect sites with two different state
lists** — seven states by name in `Policies` for crop production, five in
`GameVars.Nation` for volunteers, overlapping in only three. findings/17
documented the second; the first was missed.

**The Confederate brigade-origin lists contain state 20, Minnesota.** USCT
(policy 33) and CSCT (133) both draw Confederate brigades from
`{0, 7, 8, 20, 33, 35, 38}` while the Regulars Act (48/147) uses
`{0, 7, 8, 21, 33, 35, 38}`. 21 is Mississippi and is obviously right; 20 is a
Union state. All three also draw with `Random.Range(0, list.Count - 1)`, so
**Virginia, the last entry, is never chosen** — the same max-exclusive slip
catalogued in [20](20-ai-campaign.md), and it recurs in the AI's weighted
project lottery.

---

## Name-keyed couplings, a third set

`Policies.UpdateTradeNodesEurope()` matches IIPs by exact GameObject name:
`Trade_GB`, `Trade_FR`, `Trade_Cuba`, `Trade_MiddleAmerica`,
`Trade_MiddleAmerica2`, `Trade_Pacific`. This is a **different set** from the
transport-capacity nodes in [18-economy](18-economy.md) — only `Trade_Pacific`
appears in both. Railroads are reached by NavMesh area id instead, and
`Policy.GetAllianceFromPolicy` reads a policy's alliance from **the last
character of its parent GameObject's name**, while grand-policy status comes
from the **grandparent** being named `"GrandPolicies"`.

Together with the economy's trade nodes, the campaign AI's expeditionary units
and the battle AI's objective names, that is now four independent
name-as-identifier schemes. A mod that renames or reparents scene objects
breaks things in four unrelated subsystems.

---

## Smaller things

Eleven projects — 88, 89, 90, 110, 111, 112, 115, 120, 122, 123, 124 — have
real effects and **never appear in the player's "active effects" panel**, and
project 97's line formats a **credit-rating notch count as a percentage**
("+100 %" per level). A **UI sprite array (`levelgraphics.Length`) caps the
maximum level of every repeating project**. Subsidy cost is
`level^0.5 × firstLevelCost`, a flattening curve. `GetNextProject`, the
deterministic sibling of the AI's project lottery, has no callers. And
`ImportPolicyData` contains a discarded `_ = 179;`.

---

## Where to hook

`Policies.LaunchPolicyEffect` (postfix, respecting the replay contract above)
and `Alliance.GetProjectLevel(int)` (one postfix moves every modifier project's
consumer at once) are the two highest-leverage seams. `Policies.IsDeactivated`
is two `if`s, so a postfix buys arbitrary mutual exclusion and date gating.
`Projects.LoadedProjects.Import` needs no prefab, so a postfix can append whole
new project records. `Policy.GetStatusByPolicyID` is the cheapest way to test
an effect without researching anything, and `Policy.ImportPolicyData` is the
only way to change an existing policy's duration, chapter or prerequisites.

The sample mod this pass suggests: **implement the ten dead policy pref
arrays.** They are already parsed, already documented in the config with
shipped values, and a single postfix on `LaunchPolicyEffect` can apply all ten
— turning three of the five policy ladders from one-line effects into what the
game's own config says they should be. It touches no data format and needs no
new content.
