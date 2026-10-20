# Summary: game state

**Build:** see [00-build-provenance](00-build-provenance.md)
**Source:** covers [23-game-state](23-game-state.md)

The ModApi's core data surface: the two arrays every other finding in this
folder eventually points at, the clock that refreshes them, and the three
idioms that decide whether a write to them survives.

---

## The shape

`GameVars` is a `MonoBehaviour` whose every field is `static` — 341 at the top
level plus sixteen nested types. **Scene changes do not clear it**; both scene
controllers reset `currenttimefromstart` by hand. A hook must re-read
`GameVars.alliance` / `GameVars.nation` through the static rather than cache
the array.

`Alliance` is a country (183 fields); `Nation` is a US state (38). Both arrays
are *created* by `GameVars.import_nations`, so the lengths come from data:
8 alliances everywhere, but **52, 53 or 54 states depending on the scenario**.
Never hardcode 53.

## One filename, two formats

`nations.dat` has an authored layout (19 lines per state, **42 per alliance**,
`1 + 53×19 + 1 + 8×42 = 1345` verified) and a completely different, length-
prefixed save layout (**1099 lines per state**, of which 1000 are the
`supportinfluence` pair; `1 + 53×1099 = 58248` verified against the shipped
developer save). Loading a campaign runs both: `import_nations` on the
scenario folder, then `OverwriteNationsData` on the save folder.

> **The modding rule:** a field present only in the authored format — names,
> abbreviations, census figures, colours, standard weapons — can be edited in
> the scenario file and takes effect on an **existing** save. Everything
> mechanical is in the save format, so the save wins.

Three shipped `nations.dat` files are still in a **legacy 40-line** alliance
format, and `import_nations` has no version gate for the two fields they lack.
Two are in unreachable sublevels; the third is
`Battlefields/DebugArmySetup/nations.dat`, which means **`debug_loaddebugarmysetup`
throws `FormatException` on load in this build.**

## The master campaign tick

Two composed gates. The campaign heartbeat fires every
`updatetimeforcampaigndata = 0.00002` campaign years (**≈ 10.5 game minutes**);
`UpdateAllianceData` processes **one alliance per beat, round-robin**, and
refuses to advance until `updaterhythmalliancedata = 0.50` **days** have passed
for that alliance. The composition is a burst of eight beats every twelve game
hours, so:

> **Every alliance's derived aggregates, policy effects, policy research,
> economic AI, weapon purchases and intervention checks refresh once per 12
> game hours**, with `dt ≈ 0.5/365` campaign years handed to every rate
> integration in the pass.

That pins findings/22's "replayed every tick" to a number. The pass also
drives `Policies.UpdatePolicyEffects`, `CheckAIPolicyChange`,
`AICampaign.UpdateProjects/UpdateFinancialAI/UpdateInterventions/
UpdateRailroadConstruction/UpdateTransportCapacity/CheckPurchaseWeapons`,
`Weapon.UpdateStandardization` for all 87 weapons, and the whole `DLC_WL`
block. It is the single highest-leverage hook point in the campaign layer.

## Three idioms, two of which make a direct write futile

1. **Double-buffered sweep.** A cursor walks one entity per frame into
   `<field>temp`; on wrap the published field is overwritten and `temp` zeroed.
   Writing either one is undone. `averagesupport`, `totalpopulation`,
   `privateincome`, `corporateprofits`, `volumeimports`, `tradevolume` and a
   dozen more. To change these, change the source or postfix the publish step.
2. **Reset-then-accumulate.** `UpdateAllianceData` zeroes ~18 fields and
   refills them from `unitlist`; `UpdatePolicyEffects` does the same for
   thirteen. A write survives at most 12 game hours.
3. **Trailing average** via `Tools.AddToAverage(…, 0.25, dt)`. These accept a
   write; it simply decays.

## Money

- **`AdjustTreasury` carries an AI handicap** findings/20 did not list: every
  negative adjustment for a non-player alliance is discounted by
  `usedcampaignbonusrunning × influenceaibonusonfinances (0.3)` — **15 % cheaper
  expenses at maximum difficulty**, for Britain and France too, not just the
  enemy. Subsidy funding carries a second one at
  `influenceaibonusonsubsidies = 1` → **+50 % accrual**.
- **Tax caps are entirely policy-driven.** `tax[]` is a 0–1 slider,
  `taxcaps[]` the ceiling. Income and corporate tax ship at cap **0** and are
  unusable until the Revenue Acts; corporate until Revenue Act III
  specifically. Tariff Act and Free Trade are deliberately commutative — each
  checks the other and multiplies — which is the right pattern for any hook
  writing a shared field under the replay contract.
- **The subsidy trio decoded.** `subsidies[]` is the slider,
  `subsidiesfactor[]` the policy-unlocked ceiling, `subsidyfunding[]` the money
  accrued. `GetSubsidyFactor` is their product; `GetSubsidyCost` clamps the
  *cost* at `min(1, subsidies[i])` while `AdjustSubsidyFactor` clamps the
  *slider* at **10** — so a slider above 1.0 buys up to 10× the effect at 1×
  the price. `maxpolicies = 7 + round(5 × GetSubsidyFactor(politics))` can
  reach **17** against a config comment that says 12.
- Both belligerents ship nonzero funding in the two "N/A" subsidy slots. Those
  cost nothing and do nothing — but they count toward `GetAdminCost`'s
  per-category surcharge, so **both sides pay an admin penalty for dead
  categories from turn one.** Corroborates findings/22's abandoned-ladder
  story from the data side.

## National morale ↔ state support are coupled both ways

Private income → national morale (rate 10.0, code **34**); `averagesupport` →
national morale (0.5, code **33**); national morale → each own state's support
(0.25, reason **3**); towns lost → support (−1.00 p.a., reason **4**, weighted
by the *fraction* of towns in foreign hands despite the "State occupation"
label). findings/17 had the labels; these are the writers.
`averagesupport` is population-weighted over towns held by a belligerent, and
it is the same figure that drives the policy research rate and admin cost.

`AdjustNationalMorale` and `AddTradeWarfare` both use `AdjustSupport`'s
clamp-then-recompute-delta idiom with slot 0 as a recomputed total.
`AddTradeWarfare` opens with two orphaned statements where a magnitude guard
used to be — closing findings/18's question about it: no guard, no clamp.

## Corrections to earlier findings

- **findings/17's recruitment section is sharpened, not corrected.** It already
  showed the diminishing-returns term; the constants are
  `adjvolpoolshighnumberslin = 3` and `adjvolpoolshighnumbersexp = 0.1`, so the
  pool is `3 × raw^0.9`. The two cross at `3¹⁰ = 59,049`, which means the
  "reduction" actually multiplies **small** pools up and only compresses large
  ones. Volunteers are linear in support only through the inner term.
- **findings/18's `AddTradeWarfare` question is closed** (above), as is the
  taxation model.
- **findings/19's `currenttimefromstart` ambiguity is pinned** to its two
  writers: `Hour + battlepasseddays × 24` in battle, `GetCampaignFloatingDate()
  − 1860 − debugmonthadjustment` in the campaign.
- **findings/10 gains a milestone:** `GameVars.ResetPopulationValues()` runs at
  `frame == 13` and only when `SceneManagement.currentoperation == 0` — new
  campaign, not a load.
- **findings/22's "every tick"** is every 12 game hours per alliance.

## Traps

- **`occupiedby` is a latch.** Full occupation sets it; nothing ever clears it.
  So once you have held every town in a state, your units there are
  permanently exempt from the enemy-soil `fightingspirit` penalty and
  `supplyenemysoilfactor`, and projects 93/94 keep paying out — after you lose
  every town back.
- **`nation[].name` is mutated at runtime** by `statesetname` when the active
  state-set changes. Everything that matches a state by string then silently
  stops matching, including `SearchNationByName` and policy 30's five-state
  volunteer bonus. **Shipped data fills it** for Kansas and Minnesota, and
  campaign objectives are what change the set
  ([24-campaign-objectives](24-campaign-objectives.md) §8); it escapes being a
  live bug only because the territorial names belong to sets that precede the
  campaign start date.
- **`menfielded` is the player's men from that state**, not the state's total,
  and recomputing it walks the whole unit hierarchy once per state.
- **`new Alliance()` depends on `GameVars.weapon` already being loaded** and on
  the static `alliance` array already being sized. `interventionpointspolicy`
  fixes a hard ceiling of **200** on policy ids.
- `GetRecruitAmounts` returns a flat **3000** and `HasEnoughRecruits` returns
  `true` unconditionally for alliances above 1 — **intervention forces have
  unlimited recruits.**

## Defects worth fixing

Beyond `occupiedby`: `transportcapacitypeak` is updated **three times per
pass** by a triple-nested loop with an unused outer index, cutting its trailing
window to a third; `powratio`'s denominator **assigns** instead of accumulating
POW camp capacity, so only the last camp counts; the save loader's −100 M
treasury floor is **dead** because the value is written twice and the second
read is unclamped; `GetCurrentTaxValuePa(-1)` **omits
`taxrevenuemodifier[corporate]`** that the per-type branch includes;
`battlecasualtiesavg` averages a cumulative counter; `weaponindustrylast`'s
warm-up gate uses the **rewinding** frame counter;
`NavyRankChangeTo{B,C}DatePerAlliance` are authored with the real 1862/1864
Navy reform dates and have **no consumers at all**; and
`GetActivatedPolicies(researchedonly: true)` counts policies **in** research,
ignoring the two flags four call sites bother to pass.

## Mod seams, ranked

1. `Nation.AdjustSupport` / `Alliance.AdjustNationalMorale` / `AddTradeWarfare`
   — single chokepoints, already clamp-and-recompute.
2. `Alliance.AdjustTreasury` — the only intended treasury writer; one prefix
   line removes the AI finance handicap.
3. `Alliance.GetSubsidyFactor` / `GetSubsidyCost` / `GetCurrentTaxValuePa` /
   `GetAdminCost` / `GetBalance` — pure functions of state, and the natural
   place to implement findings/22's ten dead pref arrays without touching the
   1,200-line policy switch. (`Alliance.GetBalance()` is the financial
   balance; `CampaignStrategy.GetBalance(bool)` from findings/17 is the *war*
   balance — two unrelated methods with the same name.)
4. `BattleUnits.UpdateAllianceData` — the right postfix for anything the pass
   itself resets, with `dt` already computed.
5. `GameVars.import_nations` — sees the arrays before any save overwrite; the
   hook for adding states or alliances.

Avoid direct writes to any double-buffered or reset-then-accumulate field, and
to `nation[].name`.
