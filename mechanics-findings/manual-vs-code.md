# The manual against the code

**Build:** see [00-build-provenance](../code-findings/00-build-provenance.md)
**Source:** `game-manual.md` — the official manual, version **1.13**, checked
against `Config/resources.dat`, `Config/buildingtypes.dat`,
`Config/campaignprefs.txt` and the decompiled assembly
**Translates:** [18](../code-findings/18-economy.md),
[23](../code-findings/23-game-state.md)

The manual is the developers' own account of the mechanics, so it is the right
yardstick for this folder: where it and the code agree, a finding is confirmed
from both ends; where they disagree, one of them is misleading players.

This file records the **economy** cross-check. It is a first pass — the manual
is 9,000 lines and this covers its economy chapters only.

> **The manual is accurate far more often than not.** Of 37 production recipes
> and 38 producer assignments, 33 and 37 match the shipped data exactly,
> including every connector in the four-term boolean recipes. What follows is
> the exceptions, which is not a fair picture of the document as a whole.

---

## 1. What the manual confirms

These were derived from code in
[the-war-economy](the-war-economy.md) and
[production-and-workforce](production-and-workforce.md) and are now confirmed
as **intended design**, not inference:

| Finding | The manual's words |
| --- | --- |
| Debt is automatic; you never choose to borrow | *"Debt is issued automatically, if the government runs out of money."* |
| "Available workforce" is slack, i.e. unemployment | *"Unemployment increases if the private companies cannot employ enough citizens."* |
| Recruitment drains the labour pool | *"Notice, that recruitment from the state this city is in, reduces available workforce."* |
| The slave exemption scales with Confederate state support | *"Number of available slave workforce depends on CSA support in the state."* |
| Slavery demand is highest in extraction, lowest in industry | *"Plantations, farms and mines will have the highest use of slave workforce, while heavy industry the lowest."* |
| Specialising a building raises output | *"If the industry concentrates on the production of only a few products, the efficiency may increase."* |
| Agriculture and Industry subsidies are the construction budget | *"Industries can be upgraded by paying subsidies. Depending on industry type, agriculture or industry subsidies are required."* |
| Only the treasury balance can be spent, not the projected surplus | *"If you have $2 million in treasury, and projected surplus of 5 million … you will only have the $2 million to use."* |
| Migration runs toward jobs and is slow | *"a region with many available jobs will attract workforce from regions with higher rate of unemployment … This migration is slow."* |

The last one closes
[production-and-workforce](production-and-workforce.md) §11 question 4 as to
*intent*: the population feedback found in `UpdateWorkforceFluctuation` —
growth where labour is scarce, decline where it is abundant — is the migration
the manual describes.

**The eight supply goods match exactly.** The manual marks Artillery,
Artillery Ammunition, Forage, Horses, Provisions, Small Arms Ammunition,
Uniforms and Weapons with *"Stored in Depots"*, and those are precisely the
eight records with `issupplydepotgood = true`.

---

## 2. Four recipes and one producer that do not match the data

| Good | The manual says | The data says |
| --- | --- | --- |
| **Flour** | 4× Crops | **3× Crops** |
| **Locomotives** | 4× Iron **and** Steel **and** Machine Parts | **8× Iron, 2× Steel, 2× Machine Parts** — exactly double |
| **Transport Ships** | 80× Wood **and** 2× Machine Parts | **160× Wood, 4× Machine Parts** — exactly double |
| **Bronze** | produced by **Mines** | produced by **Foundries** |

Locomotives and Transport Ships are both **exactly 2× the manual's figures**,
which reads as a rebalance the manual never caught up with rather than four
separate slips. Flour is a plain discrepancy, 3 against 4.

> **What to do with this:** a transport ship costs **160 wood**, not 80. If you
> are planning amphibious or riverine movement from the manual's numbers, you
> are budgeting half the timber you need, and wood also feeds weapons,
> artillery, construction and iron works. Locomotives likewise cost double what
> the manual states, which matters because they are what railroad
> infrastructure consumes.
>
> Bronze is smelted in **Foundries** alongside Iron, not in Mines. Mines
> produce the copper ore it is made from.

### The bonus resources, with the numbers the manual omits

The manual marks four inputs with *"Increases the speed of production"* but
gives no figures. They are production **multipliers**, and they are large:

| Good | Bonus input | Multiplier |
| --- | --- | --- |
| Bronze | Coal | **×2.0** |
| Gunpowder | Coal | **×2.0** |
| Iron | Coal | **×2.0** |
| Food | Salt | **×1.5** |

> **Coal doubles the output of bronze, gunpowder and iron.** That is not a
> marginal bonus, and it makes coal the single most leveraged raw good in the
> chain — it is also consumed directly by foundries, iron works, trains and
> steamships. Salt raising food output by half is the same shape of effect, and
> salt is separately a required input to Leather and Provisions.
>
> The bonus only applies where the local price of the bonus good is below
> `maxpricebonusresource`; an expensive coal market loses you the multiplier.

---

## 3. Two things the manual warns about that the code does not do

### 3.1 Sales tax does not raise prices or slow trade

The manual, on the sales tax slider:

> *"High sales tax makes the movement of goods more expensive, pushing up
> prices."*

**It does neither.** `tax_excise` appears exactly once in the whole trade
engine, and it is the revenue line: the tax is accrued on each completed sale
and nothing else. It is absent from the attractivity term that decides how much
of a good moves, absent from the price calculation, and absent from the
transport-capacity maths. Its only other appearances in the assembly are two
**display** sites that net it off a node's shown profit.

The term the manual is describing does exist — but it is the **tariff**, not the
sales tax, and it applies only to trade across an alliance boundary
([the-war-economy](the-war-economy.md) §2).

> **What to do with this:** sales tax is free revenue. Put the slider at
> maximum. The downside the manual describes is not implemented, and the only
> real cost of having the slider above zero is the 5 % administration surcharge
> that every enabled tax type carries
> ([the-war-economy](the-war-economy.md) §5).

### 3.2 Tax *rates* carry no support penalty

The manual says income tax *"will hurt support"* and that corporate tax
*"will hurt support of the Government"*.

**No support adjustment anywhere in the assembly references a tax rate.** Of
roughly 49 `AdjustSupport` call sites, not one reads `tax[]` or `taxcaps[]`.

The penalty is real, but it attaches to the **Revenue Acts** rather than to the
sliders: enacting one calls `AdjustAllSupport(alliance, revenueactsupport[n],
12)` — reason code 12 — **once, on activation**. Likewise the manual's
*"lower private wealth due to … rising inflation"* is `publicwealthfactor`,
which only **Print Notes I–II**, **War Bonds** and **Restrict Cotton Trade**
move. Dragging a slider changes neither.

> **What to do with this:** the political cost of taxation is paid once, when
> you pass the Act. After that, **the rate is free.** There is no reason to run
> income or corporate tax below its cap once the enabling Act is through — you
> have already paid for it. This is the opposite of how the manual reads.

---

## 4. One place the manual is more pessimistic than the code

On the credit rating:

> *"Whenever the credit rating drops below B, the Congress will not allow
> further construction or recruitment."*

The code's test is `notch / 26 > 1 − 0.45`, which fails at **notch 15 = CCC+**.
The ladder runs … B+ (12), B (13), B− (14), CCC+ (15) … so **B− still works**
and you lose construction, recruitment, ship construction and weapon orders
one notch later than the manual says. The in-game tooltips compute the
threshold from the same pref and render it as *"below B−"*, which is correct.

> **What to do with this:** nothing, except not to panic a notch early. The
> manual errs on the safe side, and the margin is one rung.

Worth adding, because the manual does not: **all four of those systems switch
off together**, at the same threshold, from the same pref — see
[production-and-workforce](production-and-workforce.md) §7. The manual
mentions construction and recruitment; weapon orders and ship construction are
on the same switch.

---

## 5. What the manual does not mention at all

Not contradictions — gaps, where the code does something the manual leaves out
and a player would want to know:

- **Casualties hold the labour pool down permanently.** The manual says
  recruitment reduces available workforce, and that casualties reduce
  *support*. It does not say that the recruited count is never reduced by
  deaths, so every man killed costs his home state five workers for the rest
  of the campaign ([production-and-workforce](production-and-workforce.md) §4).
- **Fertility is applied squared**, so 0.8 land yields 64 %
  ([production-and-workforce](production-and-workforce.md) §5).
- **Mines and ports never grow by themselves** — the manual describes industry
  expanding where production is profitable without noting the two exceptions
  ([production-and-workforce](production-and-workforce.md) §7).
- **The boom/bust cycle is an authored curve**, identical in every scenario and
  shared by both sides ([the-war-economy](the-war-economy.md) §7). The manual
  presents the economy as fluctuating in response to the war.
- **The price curve's shape.** The manual explains supply and demand
  correctly but never says that price barely moves above 70 % stock and
  quadruples between 60 % and 30 % ([the-war-economy](the-war-economy.md) §1).

---

## 6. The policy appendix is exact

Appendix A gives numbers for the pre-war policies, and **every one of them
matches the shipped data.** Checked against `campaignprefs.txt`:

| Policy | The manual says | The data holds |
| --- | --- | --- |
| Underground Railroad | slave workforce −10 %, then −5 % a year | `-0.10`, `-0.05` |
| Kansas a Free State | Kansas +25, all free states +5 | `0.25`, `0.05` |
| Go West! | support +25, population +20 %, trail capacity **doubled** | `0.25`, `1.20`, `2.0` |
| Support Abolitionism | Southern support in Union states −10 | `-0.10` |
| Security Measures | CSA ships and weapons −50 %, CSA morale −5 | `0.5`, `0.5`, `-0.05` |
| Industrialization | population +20 %, morale −5 | `1.2`, `-0.05` |
| Indian Wars | military experience Union and CSA +5, Union morale +5 | `0.05`, `0.05`, `0.05` (CSA morale `0.0`) |
| Union Pacific Railroad | credit rating **+2**, railroad capacity and construction speed +25 % | `-2` notches, `1.25`, `1.25` |

Ten numeric claims, ten matches. The support values are quoted out of 100 and
stored as [0,1] fractions, which is the only translation needed.

The Union Pacific entry incidentally confirms the sign convention: the manual's
*"credit rating + 2"* is the data's `-2`, and the config label spells it out —
*"notches; minus = improvement"*. A **higher notch number is worse**
([the-war-economy](the-war-economy.md) §6).

**One nuance the manual leaves out.** It describes the Underground Railroad as
reducing *"Initial slave workforce for CSA"*. The code applies both reductions
to **both alliances** (`for (int i = 0; i < 2; i++)`). That is mostly inert for
the Union, whose states largely have `slaveryallowed = false` — but it is not
inert in the **border states and in captured Confederate territory**, where a
Union-owned farm or mine does draw on slave labour. So a Union player's own
Underground Railroad quietly erodes the slave labour of the ground they take.
It reads as deliberate rather than as a slip, but it is not what the appendix
says.

## 7. Project 98 Market Reform does nothing at all

Appendix B is written qualitatively — *"Each level of this project will further
increase …"* — so it cannot be checked number against number. Two entries can
be checked as statements, and one of them fails.

**Improve Credit Rating** — *"will further increase the national credit rating
with one step"* — is exactly right: `project_creditrating` is **−1**, applied
as `FloorToInt(level × −1)` notches.

**Market Reform** is dead. In the market-capacity paint, the project level and
its pref are both read and **thrown away**:

```csharp
if (BuildingType == CBuilding.id_market && constructiontimer <= 0f)
{
    _ = 1f;
    GameVars.alliance[…].GetProjectLevel(98);     // result discarded
    _ = GamePrefs.project_marketeffects;          // discarded
    marketcapacity.Paint(pos, GamePrefs.marketinfluenceoninfastructure[level], …);
    transportbottlenecks.Paint(pos, 0f - GamePrefs.marketinfluenceoninfastructure[level], …);
}
```

The paint uses only the market building's own level. `GetProjectLevel(98)` has
**exactly one occurrence in the whole assembly** — that discarded one — and
`project_marketeffects` has two: the discard, and the project panel's own
description generator. So **the project is advertised and has no effect.**

> **What to do with this:** do not research Market Reform. Build Markets
> instead — `marketinfluenceoninfastructure[level]` is real, and raises market
> capacity while reducing transport bottlenecks. The project that claims to
> make Markets better is the part that does nothing.

That is the **third** project effect found inert, after **114 Plantation
Mechanization** (no effect at all) and the production half of **113 Farm
Mechanization** — see
[production-and-workforce](production-and-workforce.md) §3.

**A sweep bounds the problem.** Checking every `project_*` pref for a consumer
outside `GamePrefs` and the description generator: `project_marketeffects` is
the **only** one whose sole remaining use is a discarded statement. Every other
project pref reaches live code. The 113/114 failure is a different shape — a
pref used inside a condition that can never be true — and that kind cannot be
swept mechanically, so a project-by-project audit of the *reachability* of each
effect is still outstanding.

## 8. One word wrong about banks

Appendix B on **Subsidize Banks**: *"National banks can further increase the
available capital in the region, increasing private wealth and lowering
construction costs of companies."*

Available capital does raise private wealth
([production-and-workforce](production-and-workforce.md) §8) and it does lower
a building's costs — but the function it enters is `GetMaintainanceCosts`,
operating on `fixedcosts[]`, the **running** cost per level.
`GetConstructionCost` does not take capital as an argument at all.

> **What to do with this:** banks make your industry cheaper to *run*, not
> cheaper to *build*. Building a bank to cheapen a construction project will
> not work. (Capital does raise the probability that a company is founded
> nearby, by up to ×1.5 — so the manual's instinct is half right, by a
> different route.)

---

## 9. Open questions

1. **The rest of the manual.** This pass covered the economy chapters and both
   appendices. The **combat, morale, supply, commander and politics** chapters
   are unchecked, and on the evidence so far they are worth checking: the
   policy numbers were exact, the production recipes were not.
2. **A reachability audit of every project effect.** §7's sweep rules out any
   further *discarded* prefs, but not the 113/114 shape — a live-looking use
   inside a condition that cannot be true. Three of roughly 125 project
   effects have now been found inert by hand; the remainder are unaudited.
3. **Whether the doubled Locomotive and Transport Ship costs were a deliberate
   rebalance.** The 2× pattern is too clean to be coincidence, but the manual
   is at version 1.13 and the build is unversioned
   ([04](../code-findings/04-version-identification.md)), so the order of
   events is not recoverable from here. Worth asking the developers.
4. **`maxpricebonusresource`** gates the bonus multipliers in §2 and its
   shipped value was not read.
