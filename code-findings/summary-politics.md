# Summary — Politics

**Covers:** [17-politics-and-support](17-politics-and-support.md)
**Build:** see [00-build-provenance](00-build-provenance.md)

## In one paragraph

Three political variables, each with its own accounting array: **state
support** (per state × alliance, 42 reason codes), **national morale** (per
alliance, 35 reason codes, and the campaign-loss condition), and **foreign
intervention levels** (per alliance × foreign power, almost entirely a policy
game with one hardcoded date gate). Politics is not a system that reacts to the
war — it is an *input* to recruitment, battle morale, desertion and surrender.
State support is the manpower tap: volunteers scale with it inside a power law
and drop to zero below a threshold, drafts are gated outright, and contracts
renew at a rate read straight off it. And the support breakdown UI is **broken
by a NaN bug**, which makes it the hook library's best first target.

## Taken out of order, and it earned it

`nation[state].support[]` had already turned up three times from other
subsystems before this pass — capping battle morale via `fightingspirit`,
amplifying desertion, and credited by hospitals. It turns out to reach further
than that.

| Consumer | Effect |
| --- | --- |
| **Volunteer pool** | **Linearly proportional** to support; zero below `statesupportrecruiting` |
| **Draft pool** | Hard-gated on the same threshold *and* on town occupation |
| `fightingspirit` | Caps battle morale in **both** combat models |
| Desertion | `(desertionfactorstatesupport − 1)/(1 + support) + 1` multiplier |
| National morale | Reason code 33, "State Support" |

> **A politically hostile state contributes no manpower at all.** That is why
> the policy game matters mechanically and not just narratively.

And a fourth politics→combat link, the bluntest: `minnationalmoralesurrender`
makes **units surrender in battle** when their alliance's national morale is
low enough.

## `Nation` is a state, not a country

A `GameVars.Nation` is a US state — `townsinstate`, `slaveryallowed`,
`occupiedby`, `census[]`, `mobilizationrate[]`. Countries are
`GameVars.Alliance`: **0 Union, 1 Confederacy, 2–4 foreign powers**, where 2
and 3 can intervene for the Confederacy and 4 for the Union. Both sides hold a
support value in **every** state simultaneously — they are not two ends of one
slider.

## The chain

```
economy ──► private wealth ─────────────┐
        └─► prices, trade               ├──► national morale ──► campaign loss
policies ──┬─► state support ───────────┘                   └──► surrender in battle
           └─► intervention levels ──► foreign entry

state support ──┬──► volunteer pool (linear), draft pool (gated)
                ├──► fightingspirit ──► battle morale ceiling
                └──► desertion rate
```

**23 of the 41 live support reason codes are policies**, which is the concrete
mechanism behind the hardcoded-policy-effects finding in
[summary-effects](summary-effects.md): most policy effects *are* support
adjustments. `Policies.cs` holds 27 of the ~49 `AdjustSupport` call sites.

## The NaN bug — and why it's a good first mod

`AdjustSupport` maintains a "recent change" tracker alongside the cumulative
breakdown:

```csharp
supportinfluencelast[alliance, typ] = Mathf.Pow(amount, 0.5f);
if (supportinfluencelast[alliance, typ] == float.NaN)      // never true in C#
    supportinfluencelast[alliance, typ] = 0f;
```

Two defects compound. `Mathf.Pow(negative, 0.5f)` is **NaN**, so any support
*loss* writes NaN; and `x == float.NaN` is **always false** (NaN ≠ itself), so
the guard never fires. The decay in `BattleUnits` divides NaN by a number and
keeps NaN. Nothing clears it.

**Two visible consequences:**

1. The stale-reset (`if (Mathf.Abs(last) < 0.0001f) cumulative = 0f`) never
   fires for a reason code that has ever caused a loss, so those cumulative
   figures stop resetting.
2. `GetStateSupportInfo(alliance, plus: false, …)` — the "what is hurting our
   support" list — selects on `last * 100f < -0.01f`, which is false for NaN.
   **It returns an empty string in every case.**

> **A postfix on `AdjustSupport` that recomputes the tracker as
> `sign(amount) * sqrt(|amount|)` restores the negative breakdown without
> touching the simulation.** Visible fix, zero balance impact, one method —
> exactly the kind of thing the hook library should ship as a sample.

The twin `AdjustNationalMorale` is clean: same clamp-and-recompute-delta
hygiene, no sqrt tracker, no bug.

## Diplomacy is a policy game with one hardcoded gate

```
probability(power 2 or 3) = CSA.level[power] − Union.level[power]
probability(power 4)      = Union.level[power] − CSA.level[power]
power 2 is additionally divided by higherinterventionlevels
```

Roughly sixteen `*intervention` prefs attach intervention weight to individual
policies (King Cotton, Emancipation, Neutrality Act, Letters of Marque, Free
Trade, Bread Basket, …), plus battle victories via
`winbattleinterventionlevel{union,csa}`.

**Power 3 is gated on 7 June 1863 *and* policy 144; power 4 on policy 174** —
hardcoded historical conditions, with the policy ids in the second (Confederate)
`+100` id block.

## Two things that will bite a modder

**Policy 30 keys off five state *names* as strings:**

```csharp
if ((name == "Illinois" | name == "Ohio" | name == "Indiana"
   | name == "Iowa" | name == "Kansas") && Policy.GetStatusByPolicyID(30))
```

> Renaming a state in config **silently disables the effect.** This is the
> hardcoded-effects risk at its worst — coupled to localisable text. A strong
> case for the ModApi exposing states by stable id.

**The war balance is computed from UI sliders.**
`CampaignStrategy.GetBalance()` finds `GameObject.Find("UI/Strategy")` and
reads ten `Slider.value` fields, blending their mean with national morale by
how polarised morale is. Elegant formula, but **the strategic assessment is
derived from UI widgets rather than the model** — anything reading it
headlessly gets whatever the sliders last displayed. The clearest instance yet
of the simulation/UI entanglement flagged in
[summary-architecture](summary-architecture.md).

`GetBalanceDescription()` also leaves **`0.8333 ≤ balance < 0.9167`
uncovered**, returning an empty string for that band.

## The manpower tap, in numbers

Every state holds one recruiting pool per side, and both are **lifetime
quotas** — a ceiling minus every man ever raised there, with casualties never
credited back ([23-game-state](23-game-state.md) §5.2).

| | Volunteers | Drafts |
| --- | --- | --- |
| Ceiling | `3 × (pop × 0.016 × support × policyMods)^0.9` | `pop × 0.04 × mobilizationrate × growth` |
| Support gate | zero below 30 % | hard gate at 30 % |
| Territory gate | none | **must own a town in the state** |
| Shipped starting value | live from turn one | **zero — needs a Draft Act** |
| Support cost | none | **−1.6 points per 1,000 men** |

> **Every authored campaign ships a mobilization rate of zero in all 53
> states**, so the draft does not exist until a Draft Act is researched. And
> because drafting burns the support it is gated on, a state at 90 % support
> yields about **37,500 drafted men** before it closes itself off.

The power law is backwards from its label: `3 × raw^0.9` crosses `raw` at
59,049, and no shipped state reaches that, so the *"reduction of volunteers
pools for higher recruiting numbers"* multiplies every real pool **up**.

**The Militia Acts are the volunteer game.** They raise a level multiplier from
0.15 to 0.60 (Union, four Acts) or 0.70 (Confederate, three), and start a
second multiplier *growing* at +0.50/yr for the first Act and +0.15/yr for each
later one. The catch is in the contracts.

## Contracts: the ninety-day men

A unit holds an enlistment date and a term in months; at expiry it keeps
`0.10 + 0.50 × support` of its men (±20 %), and the rest are gone from the unit
**and** from the pool ([17](17-politics-and-support.md) §5b). Its wounded are
scaled down by the same ratio.

Both sides start on 12-month contracts. **The Union's Militia Act I cuts that
to three months** — the historical ninety-day men — and Acts II–IV take it to
12, 24 and 36. The Confederate ladder runs 12, 24, 36. Short contracts grow the
volunteer pool fastest (the growth multiplier is throttled ×1.0/0.9/0.6/0.3 by
contract length) and churn units hardest.

The Draft Acts are asymmetric: both sides' units get their contract clock
reset, but **only Confederate units get the term extended** — to 36 months and
then 99, which is the rest of the war. The Union's own extension values are
shipped and never read.

## Smaller findings

- **`recruitable` is written in exactly one place** — the Union's Militia Act I,
  which unlocks ten border states for *both* sides. Seven states (California,
  Oregon, Nevada, Utah, Dakota, Nebraska, Unorganized) ship unrecruitable and
  never change, so they contribute no men to the war at all.
- **The USCT support-floor exception leaks to the Confederacy**: its flag is
  declared outside the per-alliance loop, so in any slave state where the Union
  holds a town, the Confederacy also recruits below the 30 % floor
  ([23](23-game-state.md) §5.2).
- **Pardoning prisoners shrinks the recruiting pool** rather than refilling it,
  and adds strength to no unit.
- **The recruitment subsidy buys no recruits.** The config's *"additional
  recruits available for full recruitment subsidies"* = 2 is used as the slew
  *rate* of a factor whose target contains no subsidy term — only project 109
  at +5 % per level ([17](17-politics-and-support.md) §5c).
- **Policy 30's five-state list is matched by name**, and so is policy 144's
  Texas charge by index; the first is the rename trap, the second double-counts
  its own subtraction.
- **Elections are a campaign objective**, not a separate system (national
  morale reason code 8), and low Union morale affects the outcome via
  `demoralizevotersunionmoralelevel`.
- **"Enemy morale broken" is itself an objective** — `breakmoraletrigger` is
  tested inside `CampaignObjective.CheckIfAccomplished`.
- **National morale reason codes 33 and 34 are the upward couplings** from
  state support and private wealth, so the economy reaches the loss condition.
- `AdjustSupport` recomputes the delta **after** clamping, so a saturated state
  records zero influence rather than a phantom gain. Worth copying.
- The 42 support labels and 35 national-morale labels are **hardcoded in
  `GameVars`, not config** — a mod cannot add a reason without patching.
- `GameVars` also holds `unitstatustextcamp[28]`, which **answers the
  encamped/entrenched/winter-quarters question** left open by
  [summary-logistics](summary-logistics.md): they are campaign unit statuses
  12, 8 and 14. Now in [16-enumerations](16-enumerations.md) along with the
  battle status table and five ten-band condition descriptors.

## Answered since, by [summary-commanders](summary-commanders.md) and [summary-economy](summary-economy.md)

- **The president is cosmetic.** A name, a title, a date line and a portrait.
  `LoadCampaignScenario`'s `chosenpresident` parameter is accepted and never
  read, so he does not seed the policy set either.
- **Political-commander support is reason code 2**, "Commander appointments" —
  not 38. Code 38 exists but is DLC-only.
- **`printingworks` and `militaryschools` both exist per-state *and*
  per-alliance.** Military schools turn out to be the **only** source of
  leadership, administration and cunning growth in the game; printing works
  multiply commander fame from battles.
- **`Alliance.credibility`** = `(ratingnotches.Length − currentrating) × 5`.
- **There are eight alliances**, not five: Union, Confederacy, Great Britain,
  France, Spain, Mexico, Sea Trade, Neutral States.

## Still open

1. ~~**Which policy maps to which reason code.**~~ **Done** — see
   [22-policy-and-project-catalogue](22-policy-and-project-catalogue.md) §8.
   Support codes 9–31 and national-morale codes 5, 20–31 are the policy block;
   support 9, support 33 and national morale 32 have no writer anywhere.
2. **POW camps** — the camp condition value and its "overcrowded" message are
   unexamined, but **support reason 33 is dead**: nothing writes it, so POW
   camp conditions do not move state support.
3. **`AddTradeWarfare(...)`** surfaces in the strategy UI and belongs with the
   economy.
