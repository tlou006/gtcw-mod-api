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
State support is the manpower tap: volunteers scale **linearly** with it and
drop to zero below a threshold. And the support breakdown UI is **broken by a
NaN bug**, which makes it the hook library's best first target.

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

## Smaller findings

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

1. **Which policy maps to which reason code.** The labels name them, so this is
   mechanical and belongs in the policy catalogue.
2. **POW camps** — support reason 33, "Condition of enemy POW camps", with an
   "overcrowded" message. Commanders captured in melee become POWs, so the two
   systems meet.
3. **`AddTradeWarfare(...)`** surfaces in the strategy UI and belongs with the
   economy.
