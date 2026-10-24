# Summary — The campaign AI

**Covers:** [20-ai-campaign](20-ai-campaign.md)
**Build:** see [00-build-provenance](00-build-provenance.md)

> Scope: `AICampaign` only. The battlefield AI (`AIBattle`, 5,458 lines, plus
> `Config/aistrategies.dat`) is a separate subsystem and still to do.

## In one paragraph

The campaign AI is a **36-step job machine per faction**, one step per call,
cycling through seven AI alliances — the same staggering idiom as the economy
and the commander round-robin, scaled up. It reasons over three spatial
representations: strategic zones **read from a texture asset**, a 20×20 blurred
influence grid, and raw positions. Its value map is **purely economic** —
transport capacity, corporate turnover, population, plus a flat capital bonus.
It decides to attack by assembling the minimum force that beats a
morale-weighted threat estimate, with a **cubic anti-stall term** that
eventually forces action. Its economic brain is literally the player's
auto-manage autopilot, and its whole agenda swaps to a crisis doctrine under
fiscal or manpower stress. Difficulty is two sliders plus **ten hardcoded
cheat constants** in the assembly, two of which are dead reads and one of which
has an inverted guard.

## The three facts that matter most

**1. AI throughput scales with the square root of game speed.**

```csharp
int num = Mathf.Max(1, Mathf.FloorToInt(Mathf.Pow(GameVars.gamespeed, 0.5f)));
for (int k = 0; k < num; k++) UpdateUnitAI();
```

> At 16× the AI gets four passes per frame, not sixteen. **Fast-forwarding
> gives the AI proportionally less thinking time per game-day**, and with
> ~250 calls to a full sweep that is a large effect. It is the most plausible
> mechanical explanation for a fast-forwarded campaign feeling passive.

**2. The AI plans against where you are going, not where you are.**

Every position it evaluates comes from
`GetLastTransmittedPathPos(ignoreorderdelay: true)` — the endpoint of the
unit's last ordered path, with the order-transmission delay explicitly
bypassed. It reads this for *enemy* units too.

So the AI intercepts a march before the march has even been transmitted. That
is structural, not a tuning multiplier, and it is its largest single advantage.

**3. Fog of war is one-directional.**

The shared detection guard reads
`(!regiment.iswithinfog | (regiment.alliance == GameVars.playeralliance) | !GameVars.usefow)`.
A player unit in fog is still detected; an AI unit in fog is not.

> **Concealment works against the player and not against the AI** — one clause,
> in battle and on the campaign map alike. And the nearest-enemy reference the
> defensive AI actually consults sits *outside* that guard, so it ignores fog
> for both sides.

Beyond that, `SeparateUnits` is fed `BattleUnits.completeunitlist` and filters
only on echelon and active state, so **the strategic AI has full information**
about every top-level formation. Note that
`GetUnitVisibilityStateOnCampaignMap` — which looks like a fog check — is not
one; it is an echelon/rendering classification.

## The strategic map is a bitmap

`InitializeAIAreas` samples `Frontline2.TextureAIAreas` texel by texel and
creates one `AIArea` per distinct colour.

> **Repaint the texture and you have repartitioned the AI's continent.** Change
> its colour quantisation by accident and you have done the same. Lookups are
> `GetAIArea(GetColorOnPos(pos))` — a texture sample plus a linear scan
> comparing `Color` equality, called inside per-unit loops. That is the
> subsystem's hot path.

`GameVars.debug_showcampaignaimapvalues` draws the importance, troop, distance
and supply numbers over every zone. For anyone working on this, it is worth
more than the source.

## The value map is economic, and the weights are lopsided

```
importance += IIP.GetTransportCapacity()         × 1.0
importance += CBuilding.volumesold.GetTtmValue() × 0.00001
importance += Town.representingpopulation        × 0.0001
importance += 750                                (capital)
× 0.35 if the owner is not at war with this faction
```

> No terrain, no defensibility, no history. And **transport capacity carries
> weight 1.0 against population's 0.0001** — a 100,000-person town scores 10.
> Worth measuring against real `GetTransportCapacity()` magnitudes
> ([18-economy](18-economy.md)) before tuning anything here.

Military weight is separate and **signed**: `aiimportanceownunits = −0.01`,
`enemy = +0.01`, `neutral = +0.0005`. So `points[faction] < 0` means "this zone
is mine", and **a neutral soldier counts as 5% of an enemy** — the Border State
posture, in a single constant. Threat also decays with distance as `d^−0.25`,
which is shallow enough that distant armies still register.

## How it decides to attack

```
threat   = target.enemyStrengthClose × morale × 0.01 × weaponFactor / nationalMoraleRatio
required = threat × 0.8 × (1 + (1 − initiative) × 0.25)
```

> **The required force ratio runs 0.8 to 1.0** — `aidominanceforoffensiveoperations`
> is 0.8, so **a bold general attacks at a numerical disadvantage** and a timid
> one waits for parity. Commander initiative is the dial
> ([19](19-commanders-and-perks.md)).

Then a probability roll:

```
p = aiagressiveness[chapter] × timediff × 10 × factionAggression × difficulty × winterMod
p += clamp(yearsStationary³, 0, 1)
```

| | |
| --- | --- |
| `aiagressiveness[0..4]` = **0, 0.75, 1.5, 2.0, 2.5** | **the AI launches no offensives at all in chapter 0.** The phoney war is data, not code |
| `+ clamp(t³)` | **the longer a force sits still the more likely it attacks, cubically** — certainty after about a year. This is why an AI army eventually moves even when the arithmetic says it should not |

Finally a composite gate with a **hardcoded `< 10f`** threshold on zone points,
and one last touch worth copying: **an unopposed advance moves only the lead
formation**; a contested one moves the whole assembled force.

Defence has its own nice line — threats are prioritised by
`min(distance to its nearest enemy, distance to our capital ÷ 3)`, so an enemy
heading for the capital looks three times closer than it is.

## AI personalities, and the player's autopilot

Eight named doctrines per side, from a per-campaign `aipersonalities.dat`:
Historical, Industrial, Expansionist, Diplomatic, Militaristic,
**Agricultural** (Union) / **King Cotton** (Confederacy), Economic,
Abolitionist — plus ids 100 **Recruit Emergency** and 101 **Credit Emergency**.
`SetRandomAIPersonality` rolls uniformly among `id < 100`.

Each doctrine carries tax focus, subsidy caps, a 20-slot pre-war policy list,
an 80-slot policy list and a **100-entry weighted project lottery**.

> `GetAIPersonality` is not a getter — it is a decision, re-evaluated on every
> call. **Under fiscal or manpower stress the AI swaps its entire agenda** for a
> crisis doctrine and swaps back when the pressure lifts; credit distress takes
> precedence over manpower distress. The newspaper announces it.

And the economic routines are shared:

> **Ticking "auto-manage finances" hands the player's treasury to the AI
> personality**, `taxfocus` and `subsidyfocus` included. Same for
> "auto-manage projects". A mod that improves the economic AI improves the
> player's autopilot at the same time — the cleanest dual-purpose seam found so
> far.

Separately, bond issue and redemption run for **both** sides regardless of any
toggle: treasury below zero borrows, treasury above a trigger repays. That
closes the loop with the debt → rating → interest spiral in
[18-economy](18-economy.md) — everyone keeps borrowing until the rating floors.

`aipersonalities.dat` uses **fixed-width padding with `-1` sentinels** rather
than the count-prefixed convention in
[06-config-file-format](06-config-file-format.md): 317 lines per record,
`1 + 20 × 317 = 6,341` lines, verified exactly. **A mod cannot add a 21st
pre-war policy or a 101st project** — the slots must be filled in place.

## Difficulty: two sliders, ten cheats

| Slider | Range | Drives |
| --- | --- | --- |
| `usedcampaignagressiveness` | 0.5 – 1.75 | offensive probability, recruiting target, and it *divides* the economy's debt burden |
| `usedcampaignbonus(running)` | 0 – 0.5 | the handicap multiplier below, and **`casualtiesmodifier`** |

> That answers the `casualtiesmodifier` provenance question left open in
> [11-combat-resolution](11-combat-resolution.md): it is the bonus slider ×
> `casualtiesmodifierbonus`, set once in `MainMenu`.

> **The non-running copy of the bonus does nothing in a campaign.** Its
> one-shot — flat national morale and a volunteers multiplier for the AI — is
> applied in the battle scene's `battledata` import and overwritten by
> `OverwriteNationsData` in the very next statement, every battle, for the
> whole campaign. It is also indexed by battle seat rather than alliance id
> ([17](17-politics-and-support.md) §3a).

The ten cheats are **literal constants on `GameVars`, not config**, and all are
disabled in AI-vs-AI. The live ones: zero order delay for AI units in contact,
no morale loss while regrouping, retreat morale loss at **0.35×** (two
multipliers compounding), 1.75× fallback speed, 0.55× march fatigue, and two
morale-recovery boosts.

> **Two of the ten are discarded reads** — `_ = GameVars.debug_aicheat_weaponpurchases;`
> and `_ = GameVars.aicheat_lowerhighmomentarycasualties;` — so the AI's
> weapon-purchase bonus and the player's momentary-casualty relief do nothing.
>
> **And one has an inverted guard.** The two shifted-morale cheats are
> structurally identical code five thousand lines apart; one disables itself in
> AI-vs-AI like every sibling, the other disables itself *outside* AI-vs-AI. So
> `aicheat_shiftedbonusmoralelosstrigger` applies only when no human is
> playing. Given the uniformity of the rest, that reads as a sign error.

**What the difficulty slider actually buys**, with `b` the slider value:

| Term | Formula | At Very Hard (b = 0.5) |
| --- | --- | --- |
| **Casualties** | `amount /= 1 + 0.5b` in `Regiment.SufferCasualties` ~16249, for every non-player alliance | **−20 % from every source** — combat, disease, desertion, raiding |
| National morale from battle losses | `× clamp(1 − b, 0.1, 1)`, live path only | half the loss |
| State support from own casualties | `÷ (1 + 0.3b)` | −13 % |
| Morale recovery / unit exp / military exp / readiness / weapon production / all spending | `× (1 + 0.3b)` each | +15 % each |

> **The casualty division is the whole package's centre of gravity**, because
> it sits in the single entry point every loss in the game passes through
> rather than in a combat formula.

The sliders reach the player as five-step labels, `ceil(x × 5) − 1` over the
normalised position: **Very Easy / Easy / Mediocre / Hard / Very Hard** in
0.1 bands of `bonus`, and **Calm / Low / Mediocre / Elevated / Very High** in
0.25 bands of `aimode`. The battle start menu shows the same `aimode` field as
a binary "AI: historical" versus "AI: dynamic" instead.

And the support-hit half of the handicap leaks between the two sides:
`num67` in `ImportExportUnitData` ~3641–3648 is not reset between side 0 and
side 1, so when the AI is the side evaluated second its support hit inherits
the first side's **Civil Order project** multiplier as well as its own
([20](20-ai-campaign.md) §9.2).

## Foreign intervention is a script, not a system

Refines [17-politics-and-support](17-politics-and-support.md): the
policy-driven probability is **necessary but not sufficient** — a major battle
victory must also have been registered for the side being joined
(`CampaignObjective.winmajorbattleintervention`).

And the implementation is the worst data coupling in the assembly: gated on
`leveltoload == "001" || "002"`, with alliances 2/3/4 and their sides
hardcoded, and the expeditionary forces activated as pre-placed inactive
GameObjects **found by exact English display name** — "Army of Canada",
"French Expeditionary Corps", "North American Squadron", and six more.

> Worse than the trade-node names in [18](18-economy.md) and the state-name
> strings in [17](17-politics-and-support.md). **A mod cannot add a foreign
> power, add intervention to a new scenario, or rename any of those nine
> units.**

## Two determinism problems

**The campaign is not reproducible.** `UnityEngine.Random.InitState(DateTime.Now.Millisecond)`
appears **three times inside `AICampaign`** (raid raising, sea-invasion
assembly, zone recruiting), plus once each in `LoadingScreen` and
`SoundEngine`. Unity's `Random` is one global stream, so reseeding it from the
system clock inside the AI tick makes every subsequent draw in the game —
casualties, sickness, feuds — depend on real time. Two loads of the same save
diverge.

> Anything built on the hook library that wants deterministic replay or
> regression testing has to neutralise those five call sites first. (The
> `InitState(i)` calls inside `Regiment`'s sprite loops are a different,
> legitimate use.)

**A repo-wide off-by-one.** `Random.Range(int, int)` is max-exclusive, and the
codebase has **39** instances of `Random.Range(0, list.Count - 1)` against
**36** of the correct form, mixed freely.

> **The last candidate in a list is never chosen.** In `AICampaign` that hits
> ship-type selection, fleet and group grabbing, new-formation creation, raid
> placement and the weighted project lottery. Usually a mild bias; for a
> weight-1 project that lands last, an unreachable option. Same slip as the
> dummy-commander names in [19](19-commanders-and-perks.md).

## One more save/load bug

`AICampaign.Load` contains `defensiveoperations.Count < defensiveoperations.Count`
— always false — so the saved `initialstrength` of each defensive operation is
**never applied**. The field has already been re-accumulated from the units'
*current* strength, so it is not zero, just wrong.

> **Reloading mid-campaign resets every defensive operation's cancellation
> baseline**, so the `current < initial × ratio` check restarts and the
> operation presses on longer than it should. Reloading quietly makes the AI
> more stubborn.

Also: `Load` bails out entirely for save versions ≤ 0.9102, discarding all AI
state.

## For the hook library

The campaign AI is unusually hook-friendly — almost everything is a `public
static` method taking a faction index, with no hidden per-unit state:

| Seam | Why |
| --- | --- |
| `CheckOffensiveMovements(faction, unit, timediff)` | the entire attack decision, one method |
| `GetUnitPoints(faction, unit)` / `UpdateImportanceValues` | the whole value map, two methods |
| `GetAIPersonality(alliance)` | a postfix here redirects the AI's entire agenda |
| `GetStrengthRatio(alliance)` / `GetNationalMoraleFactor(faction)` | the global posture inputs |
| `AIArea.GetAIArea(Color)` | the hot path; also where a caching mod would go |
| `UpdateFinancialAI` / `UpdateProjects` | improves the AI and the player's autopilot together |

And the cheats are plain static fields on `GameVars`, so a "no AI handicaps"
mod is ten assignments and needs no patching at all.

## Still open

1. **`UpdateCampaignTheaters` / `IsWithinOperationsTheater`** — a per-unit
   `theaterposition` constrains every operation and is probably the AI's answer
   to "don't send the Army of the Potomac to Missouri". Where it is assigned was
   not traced.
2. **`CheckTransferOfUnits`** — uses the heatmap's deficit/surplus extremes, but
   the transfer rule itself was not followed.
3. **Job 17 is skipped** — job 16 sets the index straight to 18. A removed step,
   or an off-by-one.
4. **The doctrines' actual agendas** — 20 pre-war policies, 80 policies and 100
   weighted projects per personality. That is the policy/project catalogue's
   job.
5. **What sets `iswithinfog` on the campaign map**, which determines how much
   the AI's fog exemption is actually worth.
6. **`AIBattle`** — the whole battlefield AI, plus `aistrategies.dat`
   (176 records, 7,393 lines, keyed by named map locations like
   "SudleySprings", "MatthewsHill", "HenryHill").
