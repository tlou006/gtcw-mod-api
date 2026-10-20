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

## 6. Open questions

1. **The rest of the manual.** This pass covered the economy chapters. The
   combat, morale, supply, commander and politics chapters are unchecked, and
   the two appendices — **policies** and **projects** — are directly
   comparable against [22](../code-findings/22-policy-and-project-catalogue.md),
   which is the obvious next cross-check and likely the richest.
2. **Whether the doubled Locomotive and Transport Ship costs were a deliberate
   rebalance.** The 2× pattern is too clean to be coincidence, but the manual
   is at version 1.13 and the build is unversioned
   ([04](../code-findings/04-version-identification.md)), so the order of
   events is not recoverable from here. Worth asking the developers.
3. **`maxpricebonusresource`** gates the bonus multipliers in §2 and its
   shipped value was not read.
