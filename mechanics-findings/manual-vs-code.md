# The manual against the code

**Build:** see [00-build-provenance](../code-findings/00-build-provenance.md)
**Source:** `game-manual.md` — the official manual, version **1.13**, checked
against `Config/resources.dat`, `Config/buildingtypes.dat`,
`Config/campaignprefs.txt`, `Config/weapons.txt`, `Config/unitprefs.txt`,
`Config/battleprefs.txt`, `Config/campaignobjectives.dat`,
`Campaigns/001/*/nations.dat`, `Config/shiptypes.dat` and the decompiled
assembly
**Translates:** [18](../code-findings/18-economy.md),
[23](../code-findings/23-game-state.md),
[11](../code-findings/11-combat-resolution.md),
[12](../code-findings/12-melee.md),
[13](../code-findings/13-morale-and-cohesion.md),
[14](../code-findings/14-supply-and-logistics.md),
[19](../code-findings/19-commanders-and-perks.md),
[17](../code-findings/17-politics-and-support.md),
[22](../code-findings/22-policy-and-project-catalogue.md),
[24](../code-findings/24-campaign-objectives.md)

The manual is the developers' own account of the mechanics, so it is the right
yardstick for this folder: where it and the code agree, a finding is confirmed
from both ends; where they disagree, one of them is misleading players.

Six passes are recorded here. **Part I (§1–8)** covers the economy chapters
and both policy appendices; **Part II (§9–14)** the combat chapters and the
three weapons tables; **Part III (§15–20)** supply, depots, raiding and
readiness; **Part IV (§21–25)** the commander chapters and the attribute
matrix; **Part V (§26–33)** politics, national morale, the policy panel and
diplomacy with the rest of Appendix A; **Part VI (§34–42)** the naval chapters
and Appendices E and F. **Every chapter of the manual has now been checked**;
what is left is listed in §43.

> **The manual is accurate far more often than not**, in all six passes. Of
> 37 production recipes and 38 producer assignments, 33 and 37 match the
> shipped data exactly; of some 180 numbers in the weapons tables, four are
> wrong; the supply chapters' readiness ladder matches the code gate for gate;
> and most of the commander attribute matrix is implemented, several cells
> understating what the code does; and of thirty-five entries in Appendix A
> bearing on support, morale, relations or the treasury, thirty-one match
> exactly; and of some 270 numbers in Appendix E's ship tables, six are wrong.
> What follows is the exceptions, which is not a fair picture of the document
> as a whole.

---

# Part I — The economy chapters

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

# Part II — The combat chapters

Lesson VI (combat, morale, ranged and close combat, casualties, routs,
withdrawal) and **Appendix D**, the three weapons tables, checked against
`Config/weapons.txt`, `Config/unitprefs.txt`, `Config/battleprefs.txt` and the
decompiled assembly. Translates [11](../code-findings/11-combat-resolution.md),
[12](../code-findings/12-melee.md),
[13](../code-findings/13-morale-and-cohesion.md).

> **The pattern from Part I repeats exactly.** The manual's prose is careful
> and its numbers are nearly all right; where it goes wrong is a data table,
> and where the *game* goes wrong is a handful of wiring faults the manual
> could not have known about. Of 52 weapon entries checked on range, rate of
> fire and firepower — about 180 numbers — **four are wrong**.

---

## 9. The weapons tables, checked number by number

Appendix D lists 52 land weapons; the shipped file holds 53 of them among its
87 records. Every matching entry was compared on range, both rates of fire and
all three firepower bands.

| Weapon | Field | Manual | Data |
| --- | --- | --- | --- |
| 6-pounder field gun | shell / canister firepower | 7.6 / 9.2 | **9.10 / 10.07** |
| 12-pounder field gun | range | 1,523 yd | **1,600 yd** |
| 12-pounder iron Napoleon | maximum rate of fire | 3 /min | **2.5 /min** |
| 12-pounder howitzer | shell firepower | 11.2 | 11.125 (rounding) |

Three of the four matter:

- **The 6-pounder field gun is the piece every newly raised battery starts
  with.** Its shell firepower is **20 % higher** than the manual states and its
  canister **9 % higher**. The arithmetic suggests which end moved: 49 of the
  51 artillery records that carry all three bands have canister set to exactly
  `1.2 ×` shell, and the manual's pair (7.6, 9.2) obeys that rule while the
  data's (9.10, 10.07) does not. It reads like the shell value was raised and
  the canister value left to drift — but that is an inference from the
  pattern, not something the code states.
- **The 12-pounder field gun reaches 1,600 yards, not 1,523.** The manual gives
  it the 6-pounder's figure, which looks like a copied cell. 5 % more reach on
  an obsolete gun is small but free.
- **The iron Napoleon is slower than the manual says** — 2.5 rounds a minute at
  point blank against the bronze gun's 3, a **17 % penalty** the appendix does
  not show. It is otherwise identical to the bronze Napoleon in range,
  firepower and horse-artillery fitness, and cheaper, so the rate of fire is
  the real price of casting it in iron.

**One weapon is missing from the manual entirely.** The **Spencer Carbine**
(range 300 yd, 16 rounds/min, firepower 0.7, accuracy 1.0, cavalry-restricted)
is in `weapons.txt` and absent from Table II. The Spencer *Repeating Rifle* is
listed; the carbine is a separate record with different numbers.

**And one gun breaks the canister rule in the other direction.** The
**6-pounder Krupp rifle** has canister firepower *below* its shell — 15.41
against 17.11 — the only gun in the game where closing to canister range
lowers the per-round figure. The manual reproduces this faithfully (*"7,7 /
17,1 / 15,4"*), so it is a data oddity rather than a documentation error, but
it means the Krupp should be kept at range.

Four entries also print `True` instead of `Yes` in the Horse Artillery column
(Krupp, Williams, Gatling, Coffee Mill) — a raw boolean that reached the page.
---

## 10. The accuracy column grades on a scale the game no longer has

Every weapon's accuracy is one number in the data — 0.8, 0.9, 1.0, 1.1 or 1.2
— and both the manual and the game turn it into a word. They disagree, and the
reason is structural.

| Data | What the game's panel shows | What the manual says | Land weapons |
| --- | --- | --- | --- |
| 0.8 | Poor | Very Poor | 8 |
| 0.9 | Poor | Poor | 4 |
| 1.0 | Mediocre | Mediocre | 22 |
| 1.1 | **Very Good** | **Good** | 13 |
| 1.2 | **Excellent** | **Very Good** | 6 |

The manual's words are a perfect six-grade ladder — *Very Poor, Poor,
Mediocre, Good, Very Good, Excellent* — mapped straight onto 0.8 through 1.2.
The game's label array has **five** entries and starts at *Poor*
([11](../code-findings/11-combat-resolution.md) §2). So:

- **"Very Poor" is not in the game's vocabulary at all.** The eight weapons the
  manual calls very poor are shown as *Poor*.
- **0.8 and 0.9 share a label.** A float truncation in the index arithmetic
  pulls 0.9 down a rung, which is why those two rows agree with the manual by
  accident while the rows above do not.
- **"Good" is never displayed by any shipped weapon.** It is index 2, and
  nothing lands there: 1.0 shows *Mediocre* and 1.1 shows *Very Good*.
- For a 1.1 or 1.2 weapon the manual is **one grade pessimistic** against the
  panel. Nineteen of the 52 entries are in that band, so this is a
  ordinary case, not an edge.

The manual is also inconsistent with itself in **7 of 52 entries**: the Hall
Carbine, Lorenz Rifle, Richmond Carbine and Williams Gun are called *Good* at
accuracy 1.0 where its own ladder says *Mediocre*; the Gatling and Coffee Mill
guns are called *Poor* at 0.8 where it says *Very Poor*; and the 3-inch
Ordnance Rifle is called *Excellent* at 1.2 where it says *Very Good*. The
Ordnance Rifle is the one entry in the whole table that matches what the game
actually prints.

> **For modders:** raising any weapon's accuracy to **1.4 or above crashes the
> weapon panel.** The index is clamped to 5 against a five-element array, which
> would have been correct for the six-grade ladder the manual describes. 1.3 is
> safe (it still shows *Excellent*); 1.4 throws.

---

## 11. What the accuracy rating buys a player: for most weapons, nothing

The manual says *"Accuracy: The higher the accuracy, the more likely the unit
will be to inflict casualties"* and *"Untrained and inexperienced troops' fire
is very inaccurate with little casualties inflicted."* In a manually fought
battle the first half of the second claim does not hold, and the first claim
holds only for the best weapons in the best hands.

Fire is resolved by simulating each projectile, and the only thing the accuracy
figure does is set how far the aim point is allowed to wander
([11](../code-findings/11-combat-resolution.md) §3e):

```
scatter = range to target × 3 / max(computed accuracy, 1)
```

**That `max` is the whole story.** The game blends the weapon's accuracy with
the unit's experience and morale, then applies gun condition, artillery
zeroing-in, fatigue, the mounted penalty and four perks — and then, if the
result is anything at or below 1.0, throws all of it away and uses the same
scatter it would have used for the worst unit in the army.

How high can the blend go? Its ceiling is **1.105**, reached only by a 1.2
weapon in the hands of veterans with high morale. Working backwards:

| Weapon accuracy | Can it ever clear 1.0? |
| --- | --- |
| 1.2 (6 land records) | Yes — veterans at almost any morale |
| 1.1 (13) | Only at near-maximum experience **and** high morale |
| 1.0 (22) | **Never** — the arithmetic maximum is exactly 0.91 |
| 0.9 (4) | Never |
| 0.8 (8) | Never |

**Thirty-four of the 53 land weapons are rated 1.0 or lower, including every
standard rifle-musket.** A green regiment and a crack regiment both carrying
Springfields put their shots in exactly the same places. What the veterans
actually get is **rate of fire** — inexperienced troops take 40 % longer to
reload — plus everything morale and experience do outside the firing model.

What to do with that:

- **Veterans are worth having for their rate of fire, their resilience and
  their morale, not their marksmanship.** Do not pay a premium for accuracy on
  a 1.0 weapon.
- **The weapons where the accuracy stat is real are the 1.1 and 1.2 group** —
  Whitworth and Mississippi rifles, Sharps, Hall, Enfield musketoons, and the
  rifled artillery — and only once the unit is experienced. Giving Whitworths
  to green troops wastes the one stat that distinguishes them.
- **Skirmishers are the exception that works.** Their ×1.1 multiplier is
  applied after the blend, so a decent skirmisher detachment can cross 1.0 with
  an ordinary weapon and genuinely shoot tighter. This is the mechanical
  content behind the manual's claim that *"individual soldiers will fire more
  accurately, as they are not cramped shoulder to shoulder."*
- **Artillery zeroing-in is mostly inert for the same reason.** The first three
  rounds at a new target divide the blend by 5, 3 and 1.5, and only from the
  fourth does a battery fire at its own quality — but for a smoothbore rated
  0.8 to 1.0 the blend was already below the floor, so switching targets costs
  it nothing in accuracy. It is only the rifled batteries good enough to clear
  1.0 that pay three rounds for changing their mind.
---

## 12. The Bayonet column describes a field the simulation never reads

Appendix D gives every small arm a **Bayonet: Yes / No** line, and the close
combat chapter says *"Used weapon and the melee weapon will have a great
impact on melee efficiency… Long rifles with bayonets are much more effective
than short carbines and knives."*

The data has **two** separate fields, and the one the column describes is dead:

- `weapon.bayonet` (−1, 0 or 1) is parsed by the loader and **never read
  again** anywhere in the assembly.
- `weapon.melee` (0.7, 0.85 or 1.0) is what the melee formula uses.
- `weapon.infantrymeleeweapon` is what the hand-weapon icon and its caption
  come from.

The three mostly agree, so the manual's sentence is broadly honest — but where
they disagree it is the dead field the manual quoted:

> **Rebored Muskets are listed as bayonet-equipped and are not.** The data
> gives them `bayonet = 0`, knives for a hand weapon, and `melee = 0.7` — the
> carbine value, 30 % below every other long arm. The Rebore Muskets project
> is the cheapest early-war way to put rifles in the field, and the manual
> recommends it for exactly that; **the men carrying them fight at carbine
> strength in a melee**, which it does not say.

Two smaller discrepancies in the same column: the **Fayetteville
Rifle-Musket** has a *sword* bayonet in the data, better than the plain "Yes"
the manual gives it; and an **infantry** regiment carrying **Henry rifles or
Spencer repeating rifles** is captioned *"Armed with sabres and revolvers",*
because both records are flagged for cavalry as well and the lookup prefers
the cavalry default.

And "much more effective" is too strong. In the melee formula the weapon enters
as a **difference**, weighted 0.5, so the whole span from knives (0.7) to
bayonet (1.0) is a casualty multiplier of **0.85 to 1.15**
([12](../code-findings/12-melee.md) §2). Formation is worth far more:

| Situation | Casualty multiplier on the defender |
| --- | --- |
| Bayonets against knives | 1.15 |
| Line attacking a unit in skirmish order | 2.3 |
| Line attacking a unit in march column | 2.3 |
| Attack column attacking a line | 1.5 |
| Any cavalry attacking a line | 2.0 |
| Any cavalry attacking a square | 1.5 |

> **Catching a unit out of formation is worth about ten times what its weapon
> is.** And note the last two rows: square is the *least bad* formation against
> cavalry, not a safe one — every formation takes at least 50 % extra. The
> cavalry multiplier also does not check whether the horsemen are mounted, so
> **dismounted cavalry charging a line still collects the full ×2.**

---

## 13. Rate of fire: two of the manual's three factors

Both weapons-table legends say *"Experience, Training and Fatigue affect the
actual Rate of Fire."* In the reload calculation
([11](../code-findings/11-combat-resolution.md) §3f):

- **Experience: yes.** `experienceinfluenceonreloading` = 1.4 — green troops
  take 40 % longer than veterans. This is the single largest thing experience
  does in a firefight.
- **Fatigue: no.** The line that would apply it multiplies by
  `fatigueinfluenceonreloading`, a pref that is **read once and never loaded
  from any config file**, so it is permanently zero. `unitprefs.txt` even ships
  the value it wants — *"how much longer do exhausted troops need to reload?"*
  = 1.25 — but the loader parses that line into a different field, which a
  later line then overwrites. **Exhausted troops reload exactly as fast as
  fresh ones.**
- **Training: no**, at least not here. The reload path reads `Experience()`
  only; drill shows up in cohesion and in loss resilience instead.

A sweep of all **1,972** `GamePrefs` constants found
`fatigueinfluenceonreloading` to be the **only** gameplay value in the game
that is used in a formula and never loaded
([07](../code-findings/07-config-data-model.md)) — so this is a narrow fault,
not a symptom of a broken config layer.

The artillery legend's other claim needs a correction of emphasis rather than
fact. It says the two rates are *"Standard / Theoretical"* and that *"the
maximum ROF is only used in case of bombardment or self-defense."* In the code
the rate is **interpolated continuously by range**: the maximum at
muzzle-to-muzzle distance, the standard figure at the gun's maximum range, and
a straight line between. Bombardment and counter-battery orders pin it to the
maximum. So a battery firing at half range is already half way between the two
numbers — about 1.6 rounds a minute for a 6-pounder, not 0.2.

---

## 14. Where the combat chapters are exactly right

Worth recording, because it is the larger part of the pass.

**Loss Resilience is implemented from all three stated inputs.** The manual
says it *"depends on experience, training and commander's leadership"* and that
*"once too many losses are suffered, the unit will instantly break even if
morale otherwise seems intact"* — both true
([13](../code-findings/13-morale-and-cohesion.md) §6). The numbers are worth
knowing:

| Unit | Breaks at |
| --- | --- |
| Green, undrilled, poor commander, no national military experience | **20 % losses** |
| All four inputs at half | **46 %** |
| Elite, fully drilled, best commander, maximum military experience | **66 %** |
| A skirmisher detachment | **half of its parent's figure** |

The skirmisher halving is the manual's *"Skirmishers are not resilient to many
losses and will break sooner than full units"*, as an exact ×0.5. One thing the
manual omits: **units toughen as a multi-day battle goes on**, by 10 % of the
trigger per elapsed day for green troops and 30 % for veterans.

**Canister range is a quarter of fire range.** Shipped `canisterrange` = 0.23
against the manual's *"approximately ¼ of this range"*.

**Cover and prone fire reduce casualties by the stated kind of margin.**
`lossfactorlayndown` = 0.4 and `lossfactorskirmishing` = 0.66 — a prone unit
takes 60 % fewer casualties from small arms, a skirmishing one a third fewer,
which is the mechanical content of *"The Skirmishers suffer less casualties due
to being spread out."*

**The charge check exists, and a manual order really does bypass it.** The
manual says a unit given the Charge order *"must first pass a morale check. If
it fails, the unit will continue exchanging fire and will not charge"*, and
that an ALT + right-click charge ignores it. Both are true of the code
([12](../code-findings/12-melee.md) §2) — with one caveat worth more than the
confirmation: **it is not a morale check.** It is a weighted comparison of
strength, experience, morale and fatigue against the target's, and the
threshold is **3**. Between units of equal quality the quality terms cancel and
the test becomes *"do I have three times his numbers?"* — which is why units
under the Charge order so often stand and shoot. Mounted cavalry doubles its
own side of the ratio, so it needs only about 1.5×.

**Commanders can be hit, and HQ commanders much less often** — but only in
auto-resolved battles. The superior-commander rolls walk up to ten levels of
hierarchy, firing only for an HQ within 75 battlefield units of a known enemy,
with the probability divided by the cube of the hierarchy distance. That whole
loop sits inside the auto-resolve branch, so **in a battle the player fights
himself an army or corps commander cannot become a casualty at all**, and the
manual's *"except when rallying in the front line"* exception has no
implementation. The two config constants named for HQ commander losses are
parsed and never read. In auto-resolve the opposite applies: a unit's own
commander is rolled twice per casualty event, so **auto-resolving roughly
doubles the risk to your brigade commanders**.

**Only the first two ranks fire**, which the formation chapter implies and the
code enforces as a hard gate on rank index.

---

# Part III — Supply, logistics and readiness

The campaign chapters on Readiness, Supplies and Depots, plus the Raiding and
Guarding section, checked against `Config/campaignprefs.txt`,
`Config/unitprefs.txt` and the decompiled assembly. Translates
[14](../code-findings/14-supply-and-logistics.md) and
[18](../code-findings/18-economy.md).

> **This is the best the manual has looked.** The supply and readiness chapters
> are almost entirely prose rather than tables, and the prose is accurate: the
> readiness restriction ladder matches the code gate for gate, and six of the
> eight things the manual lists as slowing readiness recovery are exactly the
> code's multipliers. Three statements are back to front and a fourth is one
> notch out, and the chapters leave out several effects large enough to change
> how you play.

---

## 15. What the supply chapters get right, with the numbers

**Depots bypass the infrastructure, exactly as claimed.** The manual says
supply from depots *"arrive[s] much faster, without restrictions of the IIP
transport capacity"*, and no capacity term appears in the depot draw at all
([14](../code-findings/14-supply-and-logistics.md) §3a). Days to refill a unit
from empty, at good prices and full condition:

| Source | Food and forage | Ammunition |
| --- | --- | --- |
| Depot, level 3 | **0.9 days** | 3.0 days |
| Depot, level 1 | 1.8 days | 6.1 days |
| Foraging from an adjacent node | 3.2 days | 10.8 days |
| Foraging from a node 2,000 map units away | 24.3 days | 81 days |
| Foraging from an **enemy-owned** node | ×10 slower | ×10 slower |

A level-3 depot in range is worth about **four adjacent economic nodes**, or
twenty-seven distant ones.

**"It takes some time to get the captured infrastructure working"** is the
node's `condition` dividing the local supply rate. A railhead captured at
condition 0.3 supplies at a third of its eventual rate, and separately carries
only 30 % of its goods — raiding a node hurts twice.

**The projects do what their entries say.**

| Project | Manual | Code |
| --- | --- | --- |
| 99 Infrastructure Reform | *"increase the transport capacity of IIPs"* | ×(1 + 0.05 per level) on every node you own |
| 100 Logistics Reform | *"reduces costs of troop movement using sea, river and rail transportation, as well as costs of supplies"* | −5 % per level, applied to transport upkeep **and** to both supply purchase paths. Exactly as worded — it is a cost effect, not a speed one |
| 101 Military Railroad | *"troop movement speed along railroads is greatly increased"* | +50 % rail speed per level — **and a 50 % cut in transport upkeep per level, which the manual does not mention** |
| 115 Supply Reform | *"allows upgrading existing supply depots to levels 2-3, greatly increasing the efficiency"* | it is the gate on the upgrade, and the levels are worth **×3 capacity and ×2 throughput** |

**Raiding and guarding work as described.** Raiders take goods from enemy
nodes, convert them into their own supply, and damage nearby buildings; guards
reduce the effect. The arithmetic is a plain subtraction — your raiding
efficiency minus the guard efficiency standing at the node — floored at 0.1, so
**guards can cut a raid to a tenth but never stop it.** Cavalry counts 2.0 at
raiding against infantry's 1.0, and 1.7 at guarding against 0.7, with the
commander's cunning worth up to +50 % raiding and +40 % guarding.

**Readiness consumption depends on exactly what the manual says it does.**
State support of the state being crossed, and the commanding officer's
initiative:

| Factor | Effect |
| --- | --- |
| Marching through a **loyal** state | −8.5 readiness per year |
| Marching through a **hostile** state | **−25 per year** — about 15 days from full to empty |
| An aggressive commander (initiative 100 %) | ×0.35 |
| A war-experienced nation (military experience 100 %) | ×0.50 |
| Winter, snow only | ×4.0 |
| After a battle | −2.5 per 100 % casualties, ×1.2 if you lost |
| A newly appointed commander | −0.10 flat |

**And the restriction ladder is gate for gate correct.** The bands are
hardcoded quartiles — 25 %, 50 %, 75 % — and `CheckReadinessActions` enforces:

| Manual | Code |
| --- | --- |
| *"Yellow: Unit cannot use forced march"* | at ≤ 75 %, `doublequick = false` |
| *"Orange: … cannot be ordered to carry out offensives in enemy territory. Army/fleet orders are unavailable."* | at ≤ 50 %, movement onto enemy soil is refused and raid / guard / patrol / fleet orders are cleared |
| *"Red: … cannot use offensive stance or construct anything."* | at ≤ 25 %, `campaignstance` is forced to defensive and the stance buttons are disabled |

Six of the eight conditions the manual lists as slowing **recovery** are the
code's multipliers, with values: poor supply ×0.5, a brigade still arriving
×0.33, low morale ×0.4, winter quarters ×0.75, poor intelligence (×1 + 0.005
per point) and low training (up to ×1.5). Smaller units recovering faster is
also exact — the divisor is the **number of attached brigades or ships**, as
the manual says.

---

## 16. The factor of ten is a factor of eleven

The supplies chapter's one hard number: *"where a normal road has transport
capacity of one, a railroad will increase this by a factor of ten!"*

In `IIP.GetTransportCapacity` the multiplier starts at `1f` and the pref is
**added** to it:

```
num2 = 1f
if (any connected railroad is complete) num2 += railroadtransportcapacityfactor   // 10.0
capacity *= num2
```

So a rail-connected node carries **eleven times** a plain one, not ten. The
config label — *"Railroad transport capacity leverage factor"*, 10.0 — reads
naturally as the manual does; the `1 +` is what makes the difference.

A small thing on its own, but it changes §17's arithmetic.

---

## 17. The army does not attach to the nearest node

The manual says local supply *"[is] collected from the nearest IIPs to which
the unit will automatically attach."* It is not the nearest. Each candidate
node in range is scored

```
score = transport capacity / max(1, distance) / max(0.01, average price of supply goods)
```

and the **highest score** wins. All three terms trade off linearly, so with a
railroad worth ×11:

> **A railhead outranks a plain road junction up to eleven times further away**,
> and a node with cheap food outranks a nearer one with expensive food by the
> same ratio. An army can and does reach past the village next door for a
> railway station across the state.

This also explains the chapter's own advice — *"moving them near better
infrastructure and depots will help"* — more precisely than the chapter does:
you are not shortening the supply line, you are changing which node wins the
auction.

One consequence the manual states and the code does **not** support as a
mechanism: *"This rate of supply is dependent on the transport capacity of the
IIPs."* Capacity appears in the node **selection** and in how fast the economy
restocks that node — but not in the rate at which the army draws from it. The
army feels capacity through the node's prices, which throttle foraging above a
relative price of 0.7, down to a floor of 25 %. True as an outcome; not true as
a mechanism.

---

## 18. Three things the supply chapters have backwards

### 18.1 Rail and sea transport cost *less* readiness, not more

*"Transporting the unit using railroads or ships will cost additional
readiness."* The pref is `readinessconsumptionfactorifembarked` = **0.75**, and
it multiplies a per-year consumption rate. An hour on a train costs three
quarters of an hour's marching — and covers far more ground. **Rail movement is
the cheapest way to move an army in readiness terms**, which is close to the
opposite of the advice.

### 18.2 The Confiscation Act makes foraging on enemy soil worse

Appendix A's Union Acts table: *"Supply efficiency of armies operating in
enemy states + 15 %."* The config agrees — *"Confiscation, supply flow
improvement if unit is on enemy soil, factor [>1]"* = 1.15. The code sets
`supplyenemysoilfactor = 1.15` and then multiplies it into `num15`, which is
the **time** to resupply:

```
local rate ∝ elapsed / num15
num15 *= supplyenemysoilfactor      // when standing in a state at war with you
```

So the act makes local foraging on enemy soil **13 % slower**. Three sources
agree on the intent and the code has the sign inverted. The act's other half —
contraband slaves, +3 Confederate support per year in slave states — is
unaffected, so it is still worth taking for that; just not for the supply.

### 18.3 Bad weather and sieges are in the wrong column

The readiness chapter lists eight things that *"influence the speed of recovery
negatively"*. Two of them are not recovery effects:

- **Bad weather** does not appear in the recovery chain at all. Weather affects
  readiness **consumption**, and only as winter: `×4.0`, snow only.
- **Siege combat** is a flat **−5 readiness per year consumption**, not a
  recovery malus. The distinction matters: a besieging army loses readiness
  steadily rather than merely failing to regain it, which is why the chapter's
  own remark that readiness *"will happen very slowly during sieges"* is
  understated — it goes **down**.

Weather does throttle one thing in supply that the manual does not mention:
`GetWeatherAdjustment()[3]` multiplies the **depot** draw and not the local
one, so bad weather shifts an army's supply mix towards living off the land.

---

## 19. What the supply chapters leave out, and it is a lot

Each of these is large enough to change a decision.

**Forced march stops resupply dead.** A unit moving at forced march or running
speed outside battle passes an eligibility gate that returns **before either
supply source runs** — after consumption has already been applied. Its
`supplystate` reads 0, reinforcements stop, and it lives purely on what it
carries. The config even ships the intended version of this — *"Max supply
ratio of units retreating or on forced march [0-1]"* = 0.25 — and the pref is
**never read**, so the shipped behaviour is a full cut-off rather than a 25 %
cap. Forced march also needs readiness above 75 % to begin with, so the
sequence is: arrive at full readiness, burn it marching hard, and arrive out of
supply as well.

**Ammunition is always the slow one.** `supplyspeedammo` = 0.3 applies to both
sources, so ammunition refills **3.3× slower than food** — 3 days from a
level-3 depot against 0.9, and 10.8 days of foraging against 3.2. A unit that
has just fought is short of exactly the thing that takes longest to replace,
which is the real argument for depots near the front rather than food supply.

**A raid or guard order costs 50× in reinforcement delay.**
`reinforcementdelaycavalryorders` = 50 is added to the delay of any brigade
reinforcing a formation that holds one. Raiding cavalry does not get rebuilt.

**Withdrawing at low readiness is expensive.** The share of units taking
skirmish casualties while withdrawing runs from **20 % at full readiness to
100 % at zero**. The readiness chapter describes what low readiness stops you
doing; it does not mention what it costs you on the way out.

**Deploying a garrison needs more than 50 % readiness**, a gate the ladder in
the chapter does not list.

**The enemy-soil movement block has an exemption.** Movement into enemy
territory is refused below 50 % readiness *unless the target is within
regrouping range* — and occupied states count as friendly. So a low-readiness
army can still shuffle inside the ground it holds.

**Splitting an army lets it rest faster.** Recovery is divided by
`N ^ 0.43` where N is the number of attached brigades: a 4-brigade corps
recovers at 1.65× the base rate and a 12-brigade army at 1.03×. **Breaking a
large army into corps to rest is worth about 60 % more recovery per day**, and
the chapter says only that "smaller units will also recover faster".

**A burning depot empties 300× faster than it fills.** A successful raid does
not degrade a depot's stock, it erases it.

**Depots shed stock when no army is near.** A depot that has not supplied
anyone for a while is drawn down to 10 % of capacity, so a forward depot
stocked in advance of an offensive does not stay stocked.

---

## 20. For modders: the Transportation subsidy is gone

`campaignprefs.txt` still ships three constants describing what transportation
subsidies do — transport upkeep ×0.5, IIP transport capacity ×1.5, railroad
construction speed ×2.0 — and **all three are parsed and never read.** So are
the Trade War and Civil Order subsidy effects, and the Diplomacy slider's price
and intervention bonuses, and the industry and farm productivity bonuses: ten
constants in one consecutive block, none of them live
([18](../code-findings/18-economy.md)).

The reason is in the code rather than the manual. `GameVars` names the subsidy
ids `subsidy_transportx`, `subsidy_tradewarx` and `subsidy_civilorderx` — with
an `x` — and reuses ids 4 and 5 for Military and Diplomacy. The cost array's
own config label names the eight slots *"(politics, economy, agriculture,
industry, military, diplomacy, n/a, n/a)"*. **Three subsidy categories were
removed and their effect constants were left in the file.**

> **The manual is not at fault here.** It lists exactly the six categories that
> exist, and it correctly says the Industry subsidy funds *"construction of new
> industries and railroads"* — `subsidyidrailroads` really is the industry id.
> What a subsidy slider does now is buy **funding** that pays for projects and
> construction, plus two direct effects: politics raises the policy cap, and
> military feeds the recruitment target. The ten dead constants belong to an
> earlier design in which the sliders applied multipliers directly.
>
> For anyone editing config: the live supply and transport knobs are
> `railroadtransportcapacityfactor`, `project_infrastructure`,
> `marketinfluenceoninfastructure`, the `supplyspeed*` family,
> `supplyefficiencyfactor` and the depot level arrays. The subsidy-effect block
> is inert.

---

# Part IV — The commander chapters

The Officers tab, the attribute descriptions and **the 6 × 3 attribute-effects
matrix** — about forty individual claims — checked against the full consumer
map of the five attribute getters
([19](../code-findings/19-commanders-and-perks.md) §2.6). Translates
[19](../code-findings/19-commanders-and-perks.md),
[13](../code-findings/13-morale-and-cohesion.md).

> **The matrix is the densest data table in the manual and it holds up well.**
> Of its cells, most are implemented and several understate what the code does.
> Four are wrong — two attribute mix-ups and two mechanisms that do not exist —
> and the prose adds one more. The attribute *descriptions* are accurate
> throughout.

---

## 21. The attribute matrix, cell by cell

The manual's table is reproduced here with a verdict against each cell. ✓ means
a consumer was found; **✗** means none was.

### Experience

| Manual | Verdict |
| --- | --- |
| Campaign / Battle / Fleets: *"Modifies maximum effect of personality"* | ✓ **exactly.** Every attribute getter multiplies by `exp × 0.5 + 0.5`, so a green commander operates at **half** his nominal attributes and experience can never raise him above them |

Two effects the row omits: commander experience also raises **every vessel's
combat strength** by up to +40 % (`commanderexpinfluenceonvesselcombatstrength`),
and it is the only attribute in the **morale recovery rate** — the thing the
manual attributes to leadership and fame (§23.1).

### Fame

| Manual | Verdict |
| --- | --- |
| Campaign: *"Officer fame in high positions affects national morale"* | ✓ via the alliance fame average |
| Campaign / Battle: *"Chance of feuds"* | ✓ the feud test is `feudtriggerlevel − (ownFame − otherFame)` against `abs(ownInitiative − otherInitiative)` |
| Battle: *"Enemy will be more cautious vs. a very famous commander"* | **✗ not implemented.** `forcebalance` is `forceRatio × (1 + Δmorale) × (1 + Δexperience)` and nothing else, and **fame has no consumer in `AIBattle`, `AICampaign` or `Autocalc` at all.** The per-side fame average that exists is consumed only by your *own* troops' fighting spirit |
| Fleets: *"Officer fame in high positions affects national morale"* | ✓ naval battles feed the same national-morale path |

Three fame effects the row omits, all real: fame **reduces overnight
desertions** (`commandersfameinfluenceondesertions` = 0.2, the same weight as
administration), fame feeds `fightingspirit` against the side average at the
same 0.33 weight as leadership, and fame sets part of the **unhappiness test**
(§22).

### Initiative

| Manual | Verdict |
| --- | --- |
| Campaign: *"Readiness consumption during operations"* | ✓ ×0.35 at full initiative — and **capped by the commander-in-chief's initiative**, which the manual does not mention |
| Campaign: *"Auto-resolved battles"* | ✓ reserve commitment and stance |
| Campaign: *"Chance of feuds"* | ✓ |
| Campaign / Battle: *"Information accuracy about enemy forces (over-estimation)"* | ✓ it is in the formula — but it is the **enemy** commander's initiative, not yours (§24) |
| Battle: *"Order delays"* | **✗ not implemented.** No commander attribute appears on any line in the assembly mentioning delay, processing, courier, bugle or recognition. Order delay is courier geometry × `readinessorderdelay` × a feud factor of 3.0. Initiative reaches it only by making feuds likelier |
| Battle: *"Power balance calculation"* | ✓ **but not the way the prose says.** `forcebalance` contains no commander term; initiative **divides the thresholds** the AI compares it against — `forcebalancetriggerdefend`, `…attack`, `…assaultflanking`. The prose's *"the force balance calculation uses many values, and commander initiative is one of those"* is wrong; initiative shifts the AI's willingness, not the number |
| Fleets: *"Fleet combat range"* | ✓ ×1.3 at full initiative — and it widens the **command and combat range of every campaign formation**, not just fleets (`influenceinitiativeoncommanderrange` = 0.5, `…oncombatrange` = 0.33) |
| Fleets: *"Ramming during close combat"* | ✓ `commanderinitiativeinfluenceonramprob` = 1.5 |

### Leadership

| Manual | Verdict |
| --- | --- |
| Campaign: *"Fighting spirit of troops (reduces desertion)"* | ✓ fighting spirit directly, desertion only through morale — the **direct** desertion terms are fame and administration |
| Battle: *"Morale of troops"* | ✓ three ways: the commander-nearby bonus, the first-battle cap, and damping an amplified morale drop as `(2 − leadership)` |
| Battle: *"Rallying"* | **✗ not as a recovery rate.** `rallyvalue` is a flat pref by echelon and its multiplier chain contains experience, the AI handicap and DLC — no leadership. What a rallying commander actually does is arrive: morale slot 4 pays `basemoraleincreasecommandernearby` + `leadership × 0.125` + `fame × 0.075` while he is within range. Honest in effect, wrong in mechanism |
| Battle: *"Desertion"* | ✓ closest match is **retreat losses**: missing and captured scale by `1 + (1 − leadership) × 0.6` |
| Fleets: *"Morale"* | ✓ — and the row omits the larger one: **leadership raises naval firepower by up to 30 %**, fleet-vs-fleet and fleet-vs-fort alike |

### Administration

| Manual | Verdict |
| --- | --- |
| Campaign: *"Readiness recovery speed"* | ✓ ×1.33 at full administration |
| Campaign: *"Managing supplies, effectiveness"* | **✗ in the campaign.** The only supply consumer is `supplyadminweightingbf`, in `CalculateSupplyLine` — the **battlefield** model. `UpdateIIPSupplyLink`, the campaign model, contains no administration term ([14](../code-findings/14-supply-and-logistics.md) §3a) |
| Campaign: *"Drilling effect"* | **✗ not implemented.** The drill rate is `drillincrease{base,encamped,winterquarters,entrenched}` × project 118 × elapsed × condition. No commander attribute |
| Campaign: *"Reduces desertion"* | ✓ `commandersadmininfluenceondesertions` = 0.2 |
| Battle: *"Resupplying between days, effectiveness"* | ✓ this is the battlefield supply term above |
| Fleets: *"Number of ships he can command"* | ✓ and **administration exactly doubles it at every naval rank** — Lieutenant 1→2, Commander 5→10, Captain 10→20, Flag Officer 25→50 |
| Fleets: *"Resupplying speed"* / *"Repairing speed"* | ✓ one pref covers both plus movement: `commanderadmininfluenceonmovementspeed` = 0.35 |

One omission: administration **damps the fatigue penalty from poor condition**,
for men and horses alike (`conditionofmenreductionadmin` = 0.25).

### Cunning

| Manual | Verdict |
| --- | --- |
| Campaign: *"Intelligence gathering"* | ✓ ×2.0 at full cunning, the largest single cunning effect |
| Campaign: *"Skirmishing and rear-guard action effectiveness"* | ✓ skirmishing casualties fall to 0.25 at full cunning |
| Campaign: *"Raiding effectiveness"* | ✓ +50 % raiding, +40 % guarding |
| Campaign: *"Siege effectiveness"* | ✓ ×1.5 at full cunning |
| Campaign: *"Auto-resolved battles"* | ✓ own casualties when cavalry, and the garrison slip-away trigger |
| Battle: *"Flanking tactics used vs. direct attacks"* | **✗ wrong attribute.** The assault-flanking stance threshold is `forcebalancetriggerassaultflanking` divided by **initiative**. Cunning has **no consumer in `AIBattle` at all** |
| Battle: *"Unit concealment"* | ✓ passed straight into `FogOfWar.UnitIsHidden`, weight 0.5 |
| Fleets: *"Ability to avoid being spotted"* / *"Moving close to enemy fleet before enemy can engage"* | ✓ in effect, **but symmetric** — see §23.2 |

---

## 22. The prose the matrix leaves out, and it is all true

Three passages in the attribute chapters describe mechanics the matrix does
not list. All three are implemented, and two of them are among the sharpest
commander effects in the game.

**Veterans resent unknown commanders.** *"Veteran soldiers especially will not
be content with unknown commanders. If they also have low leadership, the
veterans will be unhappy and will lose morale."* This is campaign morale slot
12, and the test is explicit:

```
if ( fame/2 + leadership×2  <  Experience() × expleverageunhappy
                               + expleverageunhappyseniorofficers × famedSeniorOfficers
   || commander is defamed )
        → moraleeffectscampaign[12] = moralelossunhappywithcommander
```

> **Leadership is worth four times fame in that test** — `leadership × 2`
> against `fame / 2`. The right-hand side rises with the unit's own
> experience, so the better your troops the better a commander they demand,
> exactly as the manual says. A **defamed** commander fails the test outright
> regardless of his attributes, and the panel's tooltip says which of the two
> reasons fired.

**Losing a loved commander hurts more than losing a hated one.** *"The loss of
a well-liked commander is felt more heavily among the rank and file than the
loss of one who is hated"*, and *"Loss of a very famous commander in battle
will be more devastating for the troops."* Both are the same morale slot, with
separate multipliers per commander status:

| Commander status | Leadership multiplier | Fame multiplier |
| --- | --- | --- |
| Active and within range | **+0.125** | +0.075 |
| Wounded or captured | −0.03 | −0.015 |
| Killed | −0.05 | −0.02 |

So a beloved general's death costs 2.5× what a disliked one's does, and the
same ordering holds for fame. The positive term requires him to be **within
`maxrangecommandermoralebonus`** — presence, not mere existence.

**Recruits are fragile and good leadership steadies them.** *"If the men are in
their first battle ever, a commander with good leadership skills is able to
better keep them in line."* ✓ `firsttimeinbattlecap` is raised toward 1 by
`effectofcommanderleadershiponfirsttimemalus` = 0.5 × leadership, in both the
manual and auto-resolve paths.

And the Officers-tab descriptions check out as a group: West Point branch
bonuses are real and branch-specific, political officers do raise home-state
support and do feud more, volunteers are restricted to their home state, and
the defamed status is a real flag with a real rehabilitation path
([19](../code-findings/19-commanders-and-perks.md) §4, §9, §11).

---

## 23. Two places the chapters describe a one-sided advantage that is shared

### 23.1 Rallying is presence, not skill

Covered in §21 — the morale **recovery rate** has no leadership or fame term.
Worth separating out because the manual says it twice, once under Fame
(*"This influences his ability to rally his troops"*) and once under Leadership
(*"High leadership makes rallying more effective"*), and a player will
reasonably conclude that appointing a high-leadership general to a broken
formation speeds its recovery. It does not. What speeds recovery is the unit's
own **experience**. What a high-leadership general does is raise the morale
*floor* while he is standing next to the unit.

The practical difference matters: a rallying commander must stay in range to
keep paying out, and moving him away removes the bonus immediately.

### 23.2 The more cunning of the two admirals sets the range for both

*"Ability to avoid being spotted"*, *"Moving close to enemy fleet before enemy
can engage"*. The engagement distance is computed once, for the battle, from
**the maximum of the two sides' attributes**:

```csharp
num  = Mathf.Max(initiative[side0], initiative[side1]);
num2 = Mathf.Max(cunning[side0],    cunning[side1]);
fleetdistance *= 1f + num  × (commanderinitiativeinfluenceonnavalrange − 1f);   // 1.3
fleetdistance *= 1f + num2 × (commandercunninginfluenceonnavalrange   − 1f);    // 0.7
```

> **Both fleets always engage at the same distance**, and that distance is set
> by whichever admiral is bolder and whichever is more cunning, on either side.
> A cunning Confederate commodore shortens the range for the Union squadron
> fighting him as much as for his own. Cunning spans up to −30 % and initiative
> up to +30 %, so the two cancel when both admirals are maxed.
>
> There is no per-side spotting advantage in the naval model at all, which is
> what the manual's wording implies.

---

## 24. Your estimate of the enemy is computed from the enemy's own generals

The matrix's *"Information accuracy about enemy forces (over-estimation)"* cell
is implemented — and in a way worth setting out, because it decides every
enemy number the game shows you. One multiplier,
`BattleUnits.CalculateApproximation()`, is applied by `TransformNumber` to all
of them: 104 call sites in the statistics screen alone, plus the battle panel,
the result summary, the campaign top bar and the unit panels.

The multiplier is built from four things:

| Input | Shipped span | Direction |
| --- | --- | --- |
| Share of enemy units you can actually see | 0.90 (none) → 0.10 (all) | the real signal, and it works |
| **The enemy** commander's initiative | ×1.0 → ×0.2 | a bolder enemy general makes your estimate **5× more accurate** |
| **The enemy's** average intelligence gathering | ×0.3 → ×0.0 | the enemy scouting *you* makes your estimate of *him* better |
| **The enemy** commander's cunning | bias +0.5 → +0.0 | a *less* cunning enemy makes you over-estimate him more |

Every attribute in that list belongs to the side you are looking at, not to
you. The variable is `GetCommandingOfficerFromSide(GetSideFromAlliance(
GetOppositeAlliance(GameVars.playeralliance)))`, and the intelligence figure is
`sideinformation[thatSide].averageintel`, which is accumulated from **that
side's** units' `intel`.

> **So you cannot improve your picture of the enemy by appointing a cunning or
> energetic general of your own.** The only lever you hold is spotting — getting
> eyes on his units, which moves the first row. Scouting *is* the answer the
> manual gives elsewhere, so the practical advice survives; the attribute
> explanation does not.

The config labels show what was meant: *"Factor for variation of accuracy when
**having** full initiative"*, *"variation is multiplied with factor 0.3 if **we**
have full intelligence"*. Read that way the three attribute rows should be
yours, and the directions make sense — a decisive general and good scouts
sharpen your reading, and nothing about the enemy's staff helps you.

A second defect sits in the same line. The intelligence term is
`X − intel × X` with `X` = 0.3, so it spans `[0.3, 0.0]`; the label describes
`1 − intel × (1 − X)`, which spans `[1.0, 0.3]`. As shipped the estimate is up
to **3.3× tighter than designed** at zero intelligence and **exactly
correct** at full intelligence, which is why enemy figures in a well-scouted
battle look suspiciously precise.

---

## 25. For modders: one `[deactivated]` label that lies

Three passes have now found `[deactivated]` in a config label to be a reliable
signal — the three readiness targets, the intelligence-based readiness factor,
the subordinate command limit and the depot price transfer are all genuinely
unread. The commander block contains the one counter-example:

> `unitprefs.txt`: *"Adjustment factor for experience to influence commander
> attributes (admin, initiative, cunning, leadership) [0-1, 1 means experience
> of 0 is totally reducing all effects to zero, 0.5 means half, 0 means exp has
> no influence) **[deactivated]**"* = 0.5

It is **live**. `GetCommanderExperience(usedforstatsmultiplying: true)` returns
`exp × 0.5 + 0.5` and every attribute getter multiplies by it, so a green
commander really does operate at half his nominal leadership, initiative,
administration and cunning. Editing that number changes the game; believing
the label does not.

Two smaller traps in the same area:

- **`GetMaxShips` reads the raw `administration` field**, bypassing the
  experience multiplier, the out-of-duty reduction and superior influence. A
  green admiral commands as many ships as a veteran of the same nominal
  administration — the only attribute consumer in the game that skips the
  getter chain for a gameplay limit.
- **`GetMaxAmountOfSubordinateUnits()` has no consumers**, and here the config
  is honest about it.

---

# Part V — The politics chapters

## 26. What the politics chapters get right, with the numbers

The prose chapters on national morale, support, policies and diplomacy hold up
as well as every other prose chapter has. The checkable claims, and the figures
the manual leaves out:

| The manual says | The data holds |
| --- | --- |
| *"If national morale falls below 25, the campaign will be lost"* | `breakmoraletrigger = 0.25`, and the config label is `[<]` — strictly below |
| *"The lower the average support, the slower the policy making"* | the research rate is **linear** in `averagesupport`: at 30 % support everything takes 3.3× as long ([22](../code-findings/22-policy-and-project-catalogue.md) §3.1) |
| *"Politics: allows more active policies (up to 12 in total)"* | `minimumpolicies = 7`, `maximumpolicies = 12`, and widening that queue is the politics subsidy's **only** effect |
| *"up to a hundred million dollars per year (except for politics subsidy type)"* | base cost $50 M × a ladder of {0.1, 0.25, 0.5, 1, 2}; politics is $10 M × 1 — see §33 for the two branches that are not $100 M |
| *"deactivating Industrialization II will also deactivate Industrialization III and IV"* | `RemoveResearch` walks a **transitive** prerequisite closure, so every descendant is zeroed, not only the direct child |
| *"Acts can be cancelled before they are activated. Once activated, they will remain so"* | `RemoveResearch` returns immediately for `IsAct && researchprogress >= 1` |
| *"Acts will remain in effect even if the required policies are deactivated"* | the cascade skips completed acts explicitly |
| *"Militia Act I … once Abraham Lincoln is inaugurated on March 4, 1861"* | policy 36 is suppressed before `Date(4, 3, 1861)` — one of exactly two entries in `IsDeactivated` |
| *"a three-month contract"* | `militiaactcontract = {3, 12, 24, 36}` months |
| *"States with low support will not change loyalty even if they are completely controlled by the enemy"* | stronger than stated: **nothing** changes a state's loyalty from support or conquest at all ([17](../code-findings/17-politics-and-support.md) §5a) |
| *"a major victory is required to encourage an invasion"* | `GetInterventionProbability >= 1` **and** `winmajorbattleintervention == <side>` ([24](../code-findings/24-campaign-objectives.md) §4) |
| *"If interventions are allowed in the Game Options"* | `GameVars.interventions` gates the trigger outright |
| *"they are forced to sue for peace and will withdraw"* | armies pull back below 50 % strength, fleets below 25 % condition — though the relations penalty that follows lands on the wrong alliance (§31) |
| *"Whenever the credit rating drops below B … no further construction or recruitment"* | the gate is one notch lower, **below B−**, and a third system goes with it (§33) |

**The Militia Act claim is the best-confirmed thing in the chapter.** The
manual says that calling out the militia before Fort Sumter *"will be
considered an act of war by the CSA"*, that *"southern support in all slave
states"* rises, and that *"this could lead to further states seceding."* All
three are exact, and the mechanism is sharper than the sentence:

- a **one-off +10 points** of Confederate support in every slave state,
  applied only if the date is before 14 April 1861;
- **+200 points per year** of Confederate support in every slave state, and
  **+40 points per year** of Confederate national morale, for as long as the
  Union holds Militia Act I and the Confederacy does not;
- then, in the same call, the secession cascade: every state currently owned by
  either side is reassigned to **whichever side it supports more**, by a bare
  `support[0] > support[1]` — a tie goes to the Confederacy.

So the +10 lands *before* the comparison that fixes every state's allegiance
for the rest of the campaign. Going first is also expensive in itself: the
Union moving first gives the Confederacy +200 points of slave-state support a
year, where the **Confederacy** moving first gives itself only +75 and gives
the Union +75 in the free states. Calling out the militia first is **2.7×
worse** than letting the other side do it
([22](../code-findings/22-policy-and-project-catalogue.md) §5).

> **One date is wrong.** The manual says the Confederacy attacks Fort Sumter
> *"on the historical date on April 12, 1861."* The code hands the fort over on
> **14 April** and starts the war on **15 April**; the pre-Sumter support bonus
> uses 14 April as its cut-off. The manual is quoting the bombardment, the code
> is using the surrender.

## 27. Appendix A's political numbers, checked

Part I checked the ten pre-war numbers in Appendix A's first table (§6). The
other four tables — the policy ladders and both sides' Acts — carry many
more. **Thirty-five** of them bear on support, national morale, European
relations or the treasury. **Thirty-one match exactly**, checked against
`campaignprefs.txt`:

| Act or policy | The manual says | The data holds |
| --- | --- | --- |
| Enforce Neutrality Act | chance of intervention against the Union −10 | `0.10` |
| Bread Basket (pre-war) | relations with Europe +10 | `0.10` |
| Tariff Act (both sides) | relations with Europeans −10, tariffs to 50 % | `-0.1`, `0.5` |
| Confiscation Act | CSA support in slave states +3 annually | `0.03`/yr |
| Revenue Act I / II / III | support −1 / −2 / −3 | `-0.01`, `-0.02`, `-0.03` |
| Bank Act I / II | support −3 each, +$120 M / +$150 M, credit +2 each | `-0.03`, `-0.03`, `120e6`, `150e6`, `-2`, `-2` |
| Northern Routes | capacity +50 %, crops +25 %, Midwest volunteers −10 % | `1.5`, `1.25`, `0.9` |
| Feed Europe I / II | relations +10 / +20, trade +25 %, land sales −50 %, credit −1 | `0.1`, `0.2`, `1.25`, `0.5`, `+1` notch |
| Enrollment / Conscription I–II | national morale −2 each | `-0.02`, `-0.02` |
| Emancipation Proclamation (Union) | CSA morale +5, Union support +5, relations +20 | `0.05`, `0.05`, `0.20` |
| Abolition of Slavery | relations +10, Union morale +5, CSA morale +10 | `0.1`, `0.05`, `0.10` |
| Legal Blockade | intervention −20, trade with Europe −25 % | `0.2`, `0.75` |
| Letters of Marque | relations with Europe −10 | `-0.1` |
| The Old Dominion | CSA support in Virginia +10, Union morale −5 | `0.1`, `-0.05` |
| Kansas a Slave State | Kansas +25, all slave states +5 | `0.25`, `0.05` |
| Slavery to the West | +25, population +25 %, Texas trade doubled | `0.25`, `1.25`, `2.0` |
| Apostles of Disunion | slave states +20, free states +10 | `0.20`, `0.10` |
| Filibustering | CSA military experience +5 | `0.05` |
| King Cotton (pre-war) | slave-state support +5, intervention +20 | `0.05`, `0.20` |
| Arms Agents | weapons +50 %, Union morale −5 | `1.5`, `-0.05` |
| Alliance with the Natives | Indian Territory +25 | `0.25` |
| Southern Pacific Railroad | credit +2, capacity and speed +25 % | `-2`, `1.25`, `1.25` |
| Impressment Act | recruits +10 %, supply cost −20 %, support −3 | `0.1`, `0.8`, `-0.03` |
| Nationalize Trade Act | cotton demand +35 %, support −3 | `1.35`, `-0.03` |
| Restrict Cotton Trade | cotton demand +10 %, relations +10, wealth −5 % | `0.1`, `0.1`, `0.95` |
| Burn Cotton | cotton stock −50 %, relations +15 | `0.5`, `0.15` |
| Slavery Restriction Act | slaves −25 %, relations +20, slave-state support −5 | `-0.25`, `0.2`, `-0.05` |
| CSCT | three brigades, slave-state support −2 | `3`, `-0.02` |
| Support Mexican Intervention | Texas recruits −50 %, relations +15, Union morale +3 | `-0.5`, `0.15`, `0.03` |
| Free Trade Act | relations +10, maximum tariff rate 15 % | `0.1`, `0.15` |
| Print Notes I / II, War Bonds | +$50 M each, wealth −10 % each; credit +2, wealth −10 % | `50e6`, `0.9`; `-2`, `0.9` |

The other **four are wrong**, and all four
are in the same shape as Part II's weapons-table errors — a single number, not
a mechanism:

| Act | The manual says | The data holds |
| --- | --- | --- |
| Southern Industrialization | population **+25 %** | `1.2` — **+20 %**, the same value as the Union's Northern Industrialization, whose text correctly says +20 % |
| Alliance with the Natives | number of volunteers **doubled** | `3.0` — **tripled** |
| Emancipation Proclamation (CSA) | Union national morale **−5** | `-0.1` — **−10**, twice as much |
| Letters of Marque | enemy sea trade **−2.5 %** | `0.015` — a **1.5 %** *floor* on the blockade ratio of every enemy harbour, which is a different quantity as well as a different number |

**Two more are not single numbers and should not be read as such.** The USCT
Act's *"number of available volunteer recruits + 10 %"* is two separate 5 %
terms — `usctmobilizationrateadj` multiplies the volunteer pool at once, and
`usctvolunteersrunning` adds 0.05 a year to the running adjustment — so the
manual's 10 % is the sum of two labels rather than a figure the code uses. The
Recruitment Bounties' *"number of volunteers + 25 percent"* is an absolute
`+0.12` added to a pool factor whose baseline depends on which Militia Acts are
in force, so it is worth about +20 % after the full Militia ladder and more
before it.

**And the Free Trade Act contradicts itself in one sentence.** *"An Act that
halves the maximum tariffs"* and *"maximum tariff rate 15percent"* cannot both
be true when the Tariff Act's cap is 50 %. The data holds the second: the cap
is set to `freetradetariffs = 0.15`.

## 28. Three Acts still promise a subsidy category that was removed

Appendix A sells three Acts partly on raising a spending cap:

| Act | The manual says |
| --- | --- |
| Letters of Marque (both sides) | *"maximum trade war subsidies + 20 %"* |
| Legal Blockade (Union) | *"20 % of additional Trade War subsidies are allowed"* |
| Organized Blockade Running (CSA) | *"maximum trade war subsidies + 40 percent"* |

**There is no Trade War subsidy.** The category was cut: `GameVars` still
declares `subsidy_tradewarx = 5` with its vestigial `x`, and id 5 is now
`subsidy_diplomacy`; the cost array's own config label names the eight slots
*"(politics, economy, agriculture, industry, military, diplomacy, n/a, n/a)"*
and prices the last two at zero
([18](../code-findings/18-economy.md) §4, [the-war-economy](the-war-economy.md)).

The two prefs that would have raised the cap are **shipped and never read** —
`lettersmarquesubsidies` and `legalblockadetradewarsubsidies`, both `1.2`, each
with exactly two references in the assembly, its declaration and its loader
line. The third, `orgrunningtradewar`, kept its name and was **repurposed**:
it is now a multiplier on the blockade ratio of the Confederacy's own harbours
(`IIP.cs:677`), shipped at `0.75`.

So each of the three Acts does something, and in two cases the real effect is
not the advertised one:

| Act | What it actually does |
| --- | --- |
| Letters of Marque | sets `lettersmarque = true`, which **floors** the blockade ratio of every enemy harbour at 1.5 %, and moves European relations by −10 |
| Legal Blockade | intervention +20 and European trade demand ×0.75 — both as advertised; the subsidy clause alone is dead |
| Organized Blockade Running | Union national morale +2, and **your own ports are blockaded 25 % less effectively** — a real and useful effect the manual never mentions |

> **What to do with this:** Organized Blockade Running is worth more than its
> description. It does not fund the trade war; it directly blunts the Union
> blockade at every Confederate harbour, which is the thing the subsidy was
> supposed to pay for. Letters of Marque is worth *less* than its description
> against an opponent who already blockades properly — a floor of 1.5 % adds
> nothing to a port already interdicted above that.

## 29. European relations cannot go negative

`AdjustInterventionLevel` floors each accumulator at zero after every write:

```csharp
interventionlevels[foralliance] += value;
…
if (interventionlevels[foralliance] < 0f) interventionlevels[foralliance] = 0f;
```

Each side keeps its own non-negative accumulator per foreign power, and the
probability is the **difference** between them. Nobody can be worse than
neutral. Two consequences the chapter does not prepare a player for.

**A relations-damaging Act is free when your relations are at zero.** The Union
starts every scenario at **0 with all three powers**, so the Tariff Act's −10
and Letters of Marque's −10 are discarded outright, and taking them costs the
Union nothing diplomatically. Taking them *after* banking relations does cost
the full amount — so the order in which a Union player passes Acts changes the
outcome.

The Confederacy does not get this for free, because it starts with relations
already on the books. These are shipped per scenario in `nations.dat` and the
manual never mentions them:

| Scenario | CSA–Britain | CSA–France | Union, all powers |
| --- | --- | --- | --- |
| A, Spring 1861 | 10 | 20 | 0 |
| B, Summer 1861 | 20 | 25 | 0 |
| C, Spring 1862 | 25 | 30 | 0 |
| D, Spring 1863 | 20 | 30 | 0 |
| E, Spring 1864 | 20 | 30 | 0 |
| F, Tutorial | 20 | 25 | 0 |

**And the worked example in the Diplomacy chapter is right for the wrong
reason.** The manual works one through:

> *"CSA King Cotton pre-war Policy increases relations with Europe by 20
> points. During the game CSA activates Restrict Cotton Trade -act, which
> increases relations by a further 10 points. The Union player activates the
> Tariff Act, which decreases Union relations by 10 points. This results in
> relations balance of 40 in favor of the Confederacy."*

20 + 10 + 10 = 40 is the arithmetic of a signed two-way slider, which is not
what the code has. What actually happens in scenario A is that the Confederacy
reaches 10 + 20 + 10 = **40** on its own accumulator while the Union's Tariff
Act is clamped away at zero. The answer is 40 because the Confederacy's
undocumented starting +10 with Britain supplies exactly the ten points the
Union's Act fails to contribute. **Two errors cancel.** Start from scenario C,
where Britain begins at 25, and the same three decisions give 55, not 40.

## 30. "European Relations" on the Strategy panel is not a relations balance

The manual describes the row as *"the balance of who the European nations
currently favor"* and says that *"if either nation reaches a relations level of
100, there is a chance the Europeans will intervene."* The figure displayed is
`GetInterventionProbability(…) × 100`, which is the raw difference **after**
three transformations the description does not mention:

```
num = CSAlevel[power] − Unionlevel[power]        // powers 2 and 3, Britain and France
num = Unionlevel[4]   − CSAlevel[4]              // power 4, Spain
if power == 2:  num /= higherinterventionlevels          // 1.35
num /= relationsintervention[CurrentChapter]             // 100, 0.75, 1.00, 1.5
clamp [0, 1]
```

Four things follow.

**Britain is 35 % harder to draw in than France.** `higherinterventionlevels =
1.35` divides the British figure alone. The manual says only that *"the British
can intervene in any case"*, which is true of the policy gates and silent about
the threshold.

**The requirement moves by chapter, and the number 100 is only right in
Chapter III.** `relationsintervention = {100, 0.75, 1.00, 1.5}`, and a value of
100 or more makes intervention impossible outright:

| Chapter | Dates | Requirement | 40 points of British lead shows as |
| --- | --- | --- | --- |
| I | to 14 Apr 1861 | **impossible** | 0 |
| II | 15 Apr 1861 – 5 Nov 1862 | 75 | 40 |
| III | 6 Nov 1862 – 9 Nov 1864 | 100 | 30 |
| IV | from 10 Nov 1864 | 150 | 20 |

The manual's *"The Europeans will be more willing to intervene during the early
Chapters"* is therefore half right: among the chapters where intervention is
possible, Chapter II is the cheapest and Chapter IV the dearest — but **Chapter
I is a hard zero**, so all the pre-war diplomacy a Confederate player buys with
King Cotton and Filibustering displays as **0 for both sides** until the war
starts. (The Chapter II column is why the manual's worked example happens to
print 40: 0.40 ÷ 1.35 ÷ 0.75 = 0.395.)

**The Union's figure is Spain, and only Spain.** `UpdateStats` builds the two
numbers as `GetInterventionProbability(4)` for the Union and
`max(GetInterventionProbability(2), GetInterventionProbability(3))` for the
Confederacy. A Union player watching this row learns nothing about Britain or
France — the two powers that can actually intervene against them. Their
figures appear only in the row's tooltip.

**And at 100 there is no chance about it.** `AICampaign.UpdateInterventions`
tests `GetInterventionProbability(alliance) >= 1f`, and the function contains
no `Random` call anywhere. The displayed "relations" is already the
normalised probability, so when it reads 100 the next qualifying major victory
brings the power in, deterministically. *"May intervene"* and *"there is a
chance"* describe a roll that does not exist.

> **The practical consequence for France.** Power 3 is zeroed before
> `Date(7, 6, 1863)` and unless Confederate policy **144**, Support Mexican
> Intervention, is active — and Chapter IV starts on 9 November 1864 and raises
> the requirement to 150. So France's realistic window is **7 June 1863 to
> 9 November 1864**, seventeen months at a requirement of 100. The date gate
> is in neither the manual nor the in-game tooltip.

## 31. Three places the intervention script points the wrong way

**31.1 The Union's own major victory makes British intervention more likely.**
Objective 24 is the Union's *"Gain a Major Victory / Discourage the
Europeans."* It is deactivated unless `GetInterventionProbability(4) >= 0.75` —
that is, unless **Spain** is three-quarters of the way to intervening *for the
Union* — and when it fires it applies `winbattleinterventionlevelunion = −0.10`
to the **Union's own** accumulator. Since the probability is a difference, a
reduction on the Union's side lowers the chance of Spanish help and *raises* the
chance of British or French intervention against them. Its Confederate mirror,
objective 25, applies `+0.25` to the Confederacy's own accumulator, which
raises British and French intervention as intended. **So both sides' major
victories push Europe toward the Confederacy**, the Confederacy's 2.5× harder
than the Union's. Either the sign or the subtitle is wrong; under both readings
the shipped behaviour is self-defeating
([24](../code-findings/24-campaign-objectives.md) §4).

**31.2 The withdrawal penalty is charged to the wrong alliance.** When a
European expeditionary force is ground down and sues for peace, the pullback
branch does
`GameVars.alliance[0].AdjustInterventionLevel(power, −100f)` for a power that
was fighting the Union — that is, it wipes the **Union's** accumulator, which is
the *subtrahend* in that power's probability. The floor of §29 means the figure
was almost certainly zero already, so the penalty is inert; where it is not, it
*increases* the difference and makes re-entry easier. Nothing is ever charged to
the patron whose accumulated relations brought the power in.

That it does not matter is an accident of a different guard:
`Alliance.timesincewar` is set when a power enters a war and **never reset when
it leaves**, while the intervention trigger requires `timesincewar <= 0f`. So
each European power can intervene **exactly once per campaign**, and the
mis-sided −100 never gets a chance to be wrong in practice.

**31.3 The monthly intelligence report names the wrong powers.** Four defects
in one block of `BattleUnits`:

- the Confederate report prints Britain's probability under **France's** name,
  because the France line re-uses `interventionProbability` instead of calling
  `GetInterventionProbability(3)`;
- its month-over-month delta for France subtracts France's stored figure from
  Britain's current one;
- the Union report is headed *"relations to Spain"* and computes
  `GetInterventionProbability(3)` — **France's** pro-Confederate figure;
- the baseline it compares against, `lastmonthinterventionprobability[4]`, is
  written from `GetInterventionProbability(0)`, which is identically zero for a
  belligerent, so the guard `> 0f` never passes and the Union's line **never
  shows a monthly change**.

The manual says of this report only that *"you can find the most important
information about your enemy's political and military actions."* Its
intervention paragraph is not usable.

## 32. The elections the manual never mentions

**Search the 9,000-line manual for "election" and the only hits are the word
"selection".** There are two elections in the campaign, implemented as
campaign objectives, and one of them is the Union's hardest loss condition.

The manual's one statement about objectives covers them both and is false for
them: *"Completing these objectives is not mandatory, but they will offer you
bonuses during the campaign."*

**32.1 The 1862 midterms are almost entirely inert.** Objective 9, *"Maintain
Support Above 60 / Republican Majority"*, keys off 4 November 1862 and tests
the Union's `averagesupport` against `republicanmajoritysupportlevel = 0.60`.
Failing it charges `republicanmajorityunionmorale = −0.01` **per week** of
Union national morale — a punishing −52 points a year if it ran for the rest
of the war, which is how its own config label reads.

It does not run. The objective's chapter list is `{1}`, and chapter 2 begins on
6 November 1862, so the penalty's window is **4 and 5 November 1862 — two
days**, worth −0.3 points of national morale. Reaching 60 % support in those
two days latches the objective complete; missing it costs essentially nothing.
Whether that is a deliberate token or a chapter-boundary accident, the midterms
do not matter.

**32.2 The 1864 election is not a −20 setback. It is an instant loss.** Both
election objectives — 26, the Union's *"Maintain Morale Above 50 / Lincoln's
Second Term"*, and 27, the Confederacy's *"Union Morale below 50 / Demoralize
Union Voters"* — carry the same tooltip, *"Union national morale -20"*, and
each applies its own −0.20 on the first campaign tick after 10 November 1864.
In the branch where Lincoln loses, **both fire in the same pass**, so the Union
takes **−40**, not −20.

That is fatal by arithmetic. Lincoln loses when Union national morale is below
0.50 on election day, so after the double hit it is below 0.10 — far under
`breakmoraletrigger = 0.25`. Objective 1, *"Break Union Morale"*, is checked
on every campaign-data pass, which runs every `0.00002` campaign years, about
**ten game minutes**. The campaign ends in a Confederate victory within one
tick of the election.

Two further consequences, both corrections to what
[24](../code-findings/24-campaign-objectives.md) §3.2 said before this pass:

- **There is no four-month grace period.** The 4 March 1865 inauguration branch
  reads as a chance to recover, but objective 26 only re-evaluates its
  `accomplished` flag *before* election day + 1, so morale recovered afterwards
  can never satisfy it. In practice the branch is unreachable anyway: if
  Lincoln held, `Policy.CheckForChapterUpdate` advances the campaign to Chapter
  IV **in the statement immediately before** the objective sweep, which
  deactivates both election objectives; if Lincoln lost, the −40 ends the
  campaign long before March.
- **Objective 27's own end-of-campaign call is dead code.** It fires only while
  the objective is *not* accomplished — that is, only while Union morale is
  *above* 50 — which is exactly the branch the chapter advance has already
  switched off.

> **What to do with this, as the Union:** treat 9 November 1864 as a hard gate
> at 50 national morale, not as a −20 event to absorb. One point under and the
> campaign is over inside ten game minutes. As the Confederacy, the same date
> is a win condition that needs no battle: hold the Union's national morale
> under 50 on that one day.

## 33. What the politics chapters leave out

**A documented mechanic is switched off.** The Support map-filter section says
*"your armies will move more slowly in states with low support, as the
population is considered hostile and armies will need to take measures to cover
their movement."* The term exists, in the campaign speed chain:

```csharp
float num15 = 1f * nation[state].support[alliance]
            + GamePrefs.adjustmentmovingspeedsupport0
              * (1f - nation[state].support[alliance]);
```

`adjustmentmovingspeedsupport0` ships as **1.0**, and the config label itself
declares the intended range as `[<1]`. The blend collapses to exactly 1 for
every value of support, so there is no penalty at any support level, and the
rolling-text branch that would print *"Low state support (−x %)"* on the map
can never fire. State support does still bite movement indirectly, through
readiness: `readinessconsumptionmovement = {−8.5, −25.0}` per annum at support
1 and 0, so a hostile state drains readiness **2.9× faster**
([14](../code-findings/14-supply-and-logistics.md) §10b). That is the whole of
the effect, and it is not what the sentence describes.

**"National Support" is not the average of your loyal states.** The manual
defines it as *"the average support of all the loyal states under the control
of each side."* `Alliance.averagesupport` is a **population-weighted average
over every town you own**, of your support in that town's state
(`Economy.cs:2068`). Two differences matter. Big cities dominate, so one
captured metropolis moves the figure more than a quiet state. And it is keyed
on **ownership, not loyalty**: every enemy town you capture enters the average
at your support level in an enemy state, which is low. Because this same figure
is the linear multiplier on policy research speed
([22](../code-findings/22-policy-and-project-catalogue.md) §3.1), **conquest
slows your own legislature**.

**"Loyal States" is a share, not a level.** The count tests
`support[side] / (support[0] + support[1]) > statesupportownstate (0.65)`. A
state at 20/5 counts as loyal; a state at 70/40 counts for neither side. The
same pref is used as an **absolute** support level in the recruiting list's
"Own states" filter and in the AI's zone checks, so a state can be loyal on the
Strategy panel and neutral in the recruitment list at the same time.

**Private wealth moves national morale by its change, not its level.** The
Economy chapter says *"High private wealth will help improving national morale,
while poverty will lower it."* Reason code 34 is
`(privateincome − lastmonthsprivateincome) × elapsed × 10.0`, and
`lastmonthsprivateincome` is re-snapshotted at the end of every month. So a
nation with permanently high but flat private wealth earns **nothing**, and a
poor nation whose wealth is rising earns morale every tick. The lever is
growth, and it resets on the first of each month.

**The credit-rating gate is one notch lower than the manual says, and catches
a third system.** `IsRatingOkForRecruitment`, `IsRatingOkForConstruction` and
`IsRatingOkForWeaponPurchases` share one test with all three thresholds at
0.45, so recruitment, infrastructure construction **and weapon orders** stop
together at notch 15, CCC+ — one notch below B−, where the manual says
"below B" ([18](../code-findings/18-economy.md) §4). Weapon orders appear in
neither the Finances chapter nor the top-panel description.

**Two subsidy branches are not $100 M.** The ladder is per branch, and two of
the five do not reach the figure the manual quotes:

| Branch | Levels | Top factor | Annual cap |
| --- | --- | --- | --- |
| Politics | — | 1 | **$10 M** |
| Funding (Economy) | I–III | 1.0 | **$50 M** |
| Agriculture | I–IV | 2 | $100 M |
| Industry | I–IV | 2 | $100 M |
| Military | I–IV | 2.0 | $100 M |
| Diplomacy | I–V | 3 | **$150 M** |

The Funding array ships a fifth entry of `2.00` that the config labels *"last
one not used"* — there is no Government Funding IV to read it. Diplomacy is the
only branch with a fifth level, and the only one that beats the manual's
number.

**Military experience does nothing for training.** The Strategy panel's
Military Experience row claims the figure makes armies *"more effective in
entrenching, training, and keeping their morale high in battles."* Entrenching
is right and understated — national experience is a **hard ceiling** on
`entrenchmentlevel`, so at 50 % an army can never dig past the midpoint.
"Keeping morale high" is loosely right by a different route: experience raises
`GetUnbearableLossesTrigger`, the losses a unit will bear before it stops
rallying ([13](../code-findings/13-morale-and-cohesion.md)). **Training is
wrong.** The drill rate is the camp-state constant × project 118 × condition,
with no national-experience term — the same shape as the administration/drill
cell in §21.

**Parallel research is a trap the chapter does not mention.** The Policies
panel says only that activating a policy *"will reserve one available policy
slot."* Throughput across N policies in research is `N × N^−1.1 = N^−0.1`,
which **falls** as N rises: at the cap of twelve the aggregate rate is 0.79×
that of researching one at a time, and the AI never parallelises
([22](../code-findings/22-policy-and-project-catalogue.md) §3.1). Acts bypass
the slot cap but still count in the divisor, so queueing acts past the cap
slows everything without being blocked.

---

# Part VI — The naval chapters, Appendix E and Appendix F

Appendix E was the last unchecked data table in the manual and, on five
passes' evidence, the likeliest place to find errors. It holds up better than
that: **the ship data is internally perfect and the appendix is almost
exactly right.**

## 34. Appendix E against `shiptypes.dat`

`Config/shiptypes.dat` declares **41 ship types**. Appendix E gives a stat
block for **34** of them (§35 covers the other seven). Each block lists
movement, propulsion, speed, length, beam, displacement, armour, complement
and required port level, plus an armament line, so the table carries roughly
270 checkable numbers.

**The shipped data is self-consistent in every record.** For all 41 types the
ship-level `guns` figure equals the sum of its weapon systems' gun counts, and
the ship-level `turrets` figure equals the sum of their `withinturret` values —
41 for 41, with no rounding slack. That is better than the weapons tables
managed in Part II.

**Every armament line in the appendix matches.** All 34 blocks agree with the
data on the total gun count, the per-type counts, the turret placements and the
Ram tag. The only blemishes are a typo (*"100-punder Rifle"* for the
Double-Ender) and one block whose digits the PDF conversion scrambled.

**Twenty-nine of the 34 stat blocks are correct in every field.** Six numbers
across the other five are wrong:

| Ship | Field | The manual says | The data holds |
| --- | --- | --- | --- |
| 4th Rate Steamer | length | 27 m | **37 m** |
| Broadside Ironclad | length | 61 m | **64 m** |
| Broadside Ironclad | beam | 10 m | **11 m** |
| Gunboat | port level required | 2 | **1** |
| Ironclad Monitor | port level required | 4 | **3** |
| Ship-of-the-Line | propulsion | Steam and Sail | **Sail** — `engines = 0` and `coalcapacity = 0` |

The Broadside Ironclad's 61 × 10 is the Sloop-of-War's hull, two entries
earlier in the appendix, which reads like a copy-paste rather than a
measurement. The two port-level errors both cost the player: the Gunboat is
buildable at a **level-1** port, not level 2, and the Ironclad Monitor at
**level 3**, not level 4 — so two ship classes are available earlier than the
appendix implies.

> **A caveat on the source.** The manual is read here from a Markdown
> conversion of the PDF, which reflows the two-column stat blocks. Pairing was
> therefore done by matching each block's displacement **and** complement
> against the data — unique across all 41 types — rather than by page order,
> and the five flagged blocks each agree with their ship on every other field.
> A digit substitution is not a failure mode of the conversion, but the 4th
> Rate Steamer's 27-for-37 is the one finding that a clean PDF would settle.

## 35. Seven ship types the appendix does not list — and you can capture them

Availability is not in `requiredtechids`, which is **empty for all 41 types**.
It is in the record's last line, a list of the alliances for which the type is
*not* yet available; everyone else gets it at once. Three cases occur:

| Last line | Meaning | Count |
| --- | --- | --- |
| *(empty)* | available to both sides from the start | 15 types |
| `0;1;` | neither the Union nor the Confederacy has it | 25 types |
| `1;` | the Union has it, the Confederacy needs a project | 1 type (Gunboat) |

The appendix's "Required Project" column is exactly this list decoded. Twelve
projects grant ship types, and every one matches the appendix:

| Project | Grants | Project | Grants |
| --- | --- | --- | --- |
| 30 Ironclad Monitors | 19 Ironclad Monitor, 27 River Monitor | 35 Modern Warships | 15 Ocean Casemate Ironclad, 16 Ironclad Warship |
| 31 Ironclad Gunboats | 25 Ironclad River Gunboat, 26 Ironclad Gunboat | 36 Confederate Gunboats | 24 Cottonclad Ram, 38 Gunboat |
| 32 Rebuilt Ironclads | 8 Broadside Ironclad, 13 Ironclad Frigate | 37 Armored Gunboats | 22 Timberclad, 23 Tinclad |
| 33 Rebuilt Ironclads (CSA) | 14 Casemate Ironclad Ram | 38 British Warships | 17 Ironclad Turret Ship *(import)* |
| 34 Ironclad Gunboats (CSA) | 28 Ironclad Gunboat Ram | 39 French Warships | 18 Ocean Ironclad Ram *(import)* |
| | | 40 Gloire Class (CSA) | 30 Ironclad Ship-of-the-Line *(import)* |
| | | 41 Warrior Class | 36 Warrior Class *(import)* |

That leaves **eight types that no project ever grants to either side**. One of
them, the **Ship-of-the-Line**, is in the appendix and honestly marked *"Port
Level Required: Not buildable"* — its `requiredportlevel = 2` is simply never
consulted. The other seven are absent from the appendix entirely:

**29 Sail Spardeck Corvette, 31 Converted Screw Frigate, 32 Screw Spardeck
Corvette, 33 Fast Screw Ship-of-the-Line, 34 Screw Schooner, 35 Screw
Ship-of-the-Line, 37 Steam Corvette.**

These are the European powers' ships — every alliance from 2 upward has them
from the start — and they include the heaviest hulls in the game: the Fast
Screw Ship-of-the-Line carries **80 guns**, the Screw Ship-of-the-Line **92**,
against the Warrior Class's 40.

> **And the appendix's own preamble says you can end up owning them**: *"Ship
> types unavailable to a nation can end up into use, if captured during combat
> from an enemy fleet."* That is implemented. At the end of a naval battle
> every vessel on the losing side with condition below
> `conditionfordisabledships` (0.25) that is no longer active is **moved into
> the winning fleet** (`Autocalc` ~3489, `BattleUnits.MoveShip`). So beating a
> British or French intervention squadron can hand a player a 92-gun
> ship-of-the-line whose stats appear nowhere in the manual.

## 36. What the ship-type chapter gets right, with the numbers

| The manual says | The data holds |
| --- | --- |
| *"Sailing ships require no coal and will be able to remain at sea for a longer time"* | every `engines = 0` type has `coalcapacity = 0` and `daysatsea = 100`, the maximum in the file |
| *"Steam ships require coal … If the steam ship also has large sails, the need for coal is lower"* | steam-only types burn **2.3 to 16.0** units of coal per 1,000 tons per year; steam-and-sail types **0.28 to 2.2** — roughly an order of magnitude, and their `daysatsea` is 60–80 against 20–60 |
| *"Speed in knots restricts the fleet's movement speed, with the slowest ship dictating the maximum"* | `UpdateLowestShipSpeed` takes the minimum over attached ships |
| *"Ram ships can use their hull as a weapon, by steaming into enemy ship in close combat"* | the ram branch requires `fleetdistance <= 0` — literal contact — plus `isram > 0` and condition ≥ **0.60** |
| *"armor can even deflect shots without damage"* | exactly right: a hit that fails the penetration roll prints *"no effect!"* and does nothing |
| *"Disabled ships will not continue fighting and can be captured during combat"* | both halves: `GetFiringStrength` returns 0 below condition 0.25, and §35's capture loop moves them to the winner |
| *"Ships that can only move on rivers cannot be assigned to sea fleets, and vice versa"* | `abletomoveonriver` / `abletomoveonsea` are **ANDs over every ship in the fleet**, so the restriction is an intersection of capabilities rather than a prohibition |
| Fleet Information: *"The fleet can travel only as fast as its slowest ship"* | true, with an addition the manual omits — see §40 |

**Ramming rewards mass and punishes armour, in both directions.** Per tick the
rammer deals
`sqrt(ownTonnage / targetTonnage × isram) × k` and takes
`sqrt(targetTonnage / ownTonnage) × 0.15 × k` itself, where
`k = ramprobability (0.4) × elapsed × 3650` — about **0.058** on a 21-minute
autocalc tick. `isram` is a rating of 1, 2 or 3, not a flag, so a
rating-3 ram does **1.7×** the damage of a rating-1 at equal tonnage. If the
*target* is an ironclad the damage is cut to 30 %; if the *rammer* is an
ironclad, its own damage is cut to 30 %. A commander's initiative scales the
whole term by `1 + 0.5 × initiative`.

> Note that nothing rolls: `ramprobability` is applied as a **damage scale**,
> not a chance, so a qualifying ram ship rams on every tick it is in contact.

## 37. Three claims about naval combat that point at the wrong mechanism

One sentence in the ship-types chapter carries most of the naval combat model:

> *"Ship size, armor and armament influence naval combat calculation. The
> larger the ship, the easier it is to hit. The stronger the armor, the less
> damage it will suffer when hit, and armor can even deflect shots without
> damage. The number and type of guns of course makes the ship more deadly. If
> armed with turrets, the ship can fire at enemy ships with less maneuvering
> and more accuracy when the ship is moving."*

The model it describes is, in `Autocalc.UpdateNavalBattle` (~1650):

```
pen     = min(1, (avgPenetration / max(0.1, targetArmor)) ^ (1/1.25) / 10)
hull    = targetSize × hullstrength (50)
hitProb = avg over in-range guns of
            accuracy × (0.006 × relDistance + 0.075 × (1 − relDistance))
                     × rand(0.9, 1.1)                     × 10
if rand < hitProb:
    if rand < pen:
        if rand <= 0.025:  damage = 1.0        // magazine explodes; instant loss
        else:              damage = firingStrength / hull × 3
    else: "no effect!"
```

**Armour does not reduce damage.** It appears only inside the penetration
probability; on a penetrating hit the damage is `firingStrength / hull × 3`
whatever the armour was. So the sentence's second clause ("can even deflect
shots") is the whole of what armour does, and its first clause ("less damage
when hit") is not implemented.

**Size does not enter hit probability at all.** Nothing in
`GetAverageHitProbability` reads the target. What size does is enter the target
lottery: `PickTargetVessel` adds each active enemy vessel to the draw
`ceil(size)` times, so a size-25 Ocean Casemate Ironclad is drawn 25 times
against a size-1.3 tender. Bigger ships therefore *attract* fire — the
manual's claim in outcome — but by a different route, **and `hull = size × 50`
cancels most of it**: a ship twice the size is picked about twice as often and
takes about half the condition damage per penetrating hit.

> The lottery also uses the repo-wide `Random.Range(0, list.Count − 1)` slip
> ([20](../code-findings/20-ai-campaign.md) §12.2), so the last active enemy
> vessel can never be drawn.

**Turrets buy rate of fire, not accuracy.** `withinturret` reaches exactly one
formula in the game, `num7 *= (rofimprovementturrets − 1) × turretFraction + 1`
with `rofimprovementturrets = 1.4` — a rate-of-fire multiplier. There is no
accuracy term for turrets anywhere. (And three of the six turret ships do not
even get the rate-of-fire bonus — §39.)

**What is right:** the gun accuracy figure is live. Unlike land combat, where
accuracy at or below 1.0 is thrown away by the aim-scatter floor (§11), naval
hit probability multiplies the gun's `accuracy` directly, so a 1.1-rated rifle
really is 10 % more likely to hit than a 1.0 cannon. **Closing the range is the
dominant term**: hit probability runs from `0.006 × 10 = 6 %` at the opening
range to `0.075 × 10 = 75 %` at contact, a factor of **12.5**.

## 38. Naval rate of fire is a constant

`Vessel.GetFiringStrength` builds a ship's firing strength from the guns whose
range reaches the current fleet distance:

```csharp
// num4 = gun-count-weighted average range of the guns that ARE in range
float num6 = Mathf.Max(1f, distance / Mathf.Max(1f, num4));
float num7 = GamePrefs.navalbattlerofmaxdistance * num6
           + GamePrefs.navalbattlerofmindistance * (1f - num6);
```

The two prefs are *"ROF factor max distance"* = **0.25** and *"ROF factor min
distance (close combat)"* = **1.00**, so this is meant to be a blend running
from a quarter rate at long range to full rate in close action, on a factor in
[0, 1].

**`num6` is always exactly 1.** Every gun counted in `num4` passed the test
`range_yd >= distance`, so the weighted average range cannot be less than the
distance, so `distance / num4 <= 1` — and `Mathf.Max(1f, …)` then raises it to
1. The blend therefore always evaluates to `0.25 × 1 + 1.00 × 0`:

> **Every ship in every naval engagement fires at the long-range rate.** The
> close-combat rate of 1.00 is never reached, `navalbattlerofmindistance` is in
> effect a dead pref, and closing the range does nothing for rate of fire. The
> clamp wanted to be `Mathf.Min`, not `Mathf.Max` — or the ratio wanted to be
> `num4 / distance`.

Closing the range is still worth doing, through hit probability (§37), which is
where the whole of the distance effect now lives. The two surviving
modifiers on `num7` are real: the turret factor, and
`navalbattleroflowammo = 0.05` — **a ship at or below 20 % ammunition fires at
a twentieth of its rate**, which is the sharpest supply cliff in the naval
model.

## 39. Three turret ships get no turret bonus

`withinturret` is not a flag: it is the number of turrets a weapon system is
spread across. The rate-of-fire bonus tests it for equality with 1:

```csharp
if (GameVars.shiptypes[type].weaponsystems[i].withinturret == 1)
    num3 += weaponsystems[i].guns;      // guns that count as turret-mounted
```

The ship panel's own `gunsinturrets` figure, by contrast, counts
`withinturret > 0`. The six turret ships split cleanly:

| Ship | Armament as the appendix prints it | `withinturret` | ROF bonus |
| --- | --- | --- | --- |
| 19 Ironclad Monitor | 2 × 11-inch, *2 in 1 turret* | 1 | **×1.4** |
| 16 Ironclad Warship | 2 + 2 + 2, each *in a turret* | 1, 1, 1 | **×1.4** |
| 18 Ocean Ironclad Ram | 1 + 2, each *in a turret* | 1, 1 | **×1.4** |
| 15 Ocean Casemate Ironclad | 4 × 15-inch, *4 in 2 turrets* | 2 | **none** |
| 17 Ironclad Turret Ship | 4 × 9-inch, *4 in 2 turrets* | 2 | **none** |
| 27 River Monitor | 4 × 15-inch, *4 in 2 turrets* | 2 | **none** |

So the three ships whose guns are divided between **two** turrets get no
turret benefit, while the three with everything in one turret get the full
40 %. The panel still reports their guns as turret-mounted, so nothing in the
interface shows the difference.

> **What to do with this:** all six turret ships are obtainable — 15 and 16
> from project 35, 19 and 27 from project 30, 17 and 18 as British and French
> imports — and three of them fire 40 % faster for their turrets while the
> **Ocean Casemate Ironclad**, the **Ironclad Turret Ship** and the **River
> Monitor** do not. For a modder, changing a weapon system's third field from
> `2` to `1` restores the bonus at no cost: the ship-level `turrets` field is
> parsed and read nowhere, so splitting the same guns into one nominal turret
> changes nothing else.

## 40. What the naval chapters leave out

**Only seven ships a side can fire in a sea battle, three on a river.** The
firing loop breaks once the number of vessels that engaged a target this tick
passes `combatwidthsea = 7` or `combatwidthrivers = 3`. The manual explains
fleet attrition entirely in terms of repairs and resupply — *"a fleet of 10
ships on paper could mean only 3 available"* — and never mentions that a
fully-crewed eleventh ship contributes nothing to a tick's damage either. The
fleet-level Firepower figure, by contrast, sums **every** ship, so the victory
balance and the damage model disagree about how much a large fleet is worth.

**A raiding fleet forces a blockading fleet to fight on even numbers.** When
one side's `fleetorders` is Raid (3) and the other's is Blockade (1), the
battle is flagged a naval raid and the **blockading** side has vessels removed
at random until it has no more than the raider does (floor of two):

```csharp
while (envolvedfleets[blockader].singlevessels.Count > Mathf.Max(2, smallerFleetCount))
    RemoveVessel(random);
```

The removed ships are not sunk; they simply do not take part. Nothing in the
Fleet Status chapter hints at this, and it is the mechanical content of
commerce raiding: a three-ship raider can engage a twenty-ship blockade
squadron three against three.

**The Firepower number is computed at point-blank range.** `Fleet.UpdateFleetData`
calls `GetFiringStrength(0f, …)`, so every gun counts as in range and the
rate-of-fire term takes its best value. `Autocalc` then passes the real fleet
distance to `EnvolvedFleets.GetCombatValue(float _fleetdistance)` — **which
never reads its argument** and returns that stored sum. So the figure the panel
shows, and the figure that sets the victory balance, are both the fleet's
close-action strength regardless of the range the battle is actually fought at.

**Damage slows a fleet.** A vessel's speed is
`speedkn × condition + speedkn × 0.3 × (1 − condition)`, so a ship at zero
condition still makes 30 % of its rated knots — and because the fleet moves at
its slowest ship, **one badly shot-up vessel drags the whole squadron down**.
The manual gives only the slowest-ship rule.

**One hit in forty sinks the target outright.** `extraordinaryeventhit = 0.025`:
on a penetrating hit, a 2.5 % roll sets the damage to 1.0 and the ship is gone
*"ammunition magazine explodes!"* — independent of its size, armour or
remaining condition. No tonnage of ironclad protects against it.

**Ten of the gun names in Appendix E belong to other weapons.** Ships draw from
19 naval weapon types, all named *"… Navy …"* in `weapons.txt`. Appendix E
drops the word, and for ten of them the shortened name is the **exact name of a
different weapon** — the fort or field piece:

| Appendix E name | Naval weapon | Same-named other weapon |
| --- | --- | --- |
| 13-inch Mortar | 48 | 74, a fort mortar |
| 12-pounder Howitzer | 49 | 40, the field howitzer |
| 24-pounder Howitzer | 50 | 41, the field howitzer |
| 8-inch Cannon | 51 | 76, a fort gun |
| 10-inch Cannon | 53 | 77 |
| 11-inch Cannon | 54 | 78 |
| 32-pounder Cannon | 56 | 79 |
| 30-pounder Rifle | 59 | 70 |
| 42-pounder Rifle | 61 | 80 |
| 100-pounder Rifle | 64 | 71 |

Eight of the ten pairs are numerically identical, so the collision is only a
naming trap for modders. **Two are not.** The naval howitzers fire a shell
equal to their nominal poundage where the field versions do not:

| | Naval | Field |
| --- | --- | --- |
| 12-pounder Howitzer | 12.0 lb shell, 1085 yd, firepower 6.37, accuracy 0.9 | 8.9 lb, 1072 yd, firepower 5.24, accuracy 1.0 |
| 24-pounder Howitzer | 24.0 lb shell, 1270 yd, firepower 10.79, accuracy 0.9 | 18.4 lb, 1322 yd, firepower 8.74, accuracy 1.0 |

So the naval howitzers hit about **30 % harder** than their same-named field
twins, at slightly shorter range in the 24-pounder's case. A modder editing
"12-pounder Howitzer" because Appendix E lists it on a gunboat will change the
field artillery of both armies instead.

## 41. For modders: one naval threshold you must not raise

`Fleet.UpdateFleetData` accumulates the fleet's combat value like this:

```csharp
else   // reached only when condition >= conditionfordisabledships
{
    guns += …;
    num2 = attachedships[i].GetFiringStrength(0f, …);
    if (attachedships[i].condition <= GamePrefs.strengthrelationtriggerdefeatnavybattles)
    {
        combatvalue = 0f;          // assigns, does not subtract
    }
    combatvalue += num2;
}
```

As shipped this branch is **dead**: it needs a ship whose condition is at or
below `strengthrelationtriggerdefeatnavybattles` (**0.10**) inside a block that
only runs for ships at or above `conditionfordisabledships` (**0.25**).

Raise the first pref above the second — the config labels it `[0-1]` and
invites exactly that — and the branch comes alive with the wrong operator: one
sufficiently damaged ship **wipes the running total**, so the fleet's combat
value becomes the sum of only the ships positioned after it in the array. It
is the same assign-for-accumulate slip as `Alliance.powratio`
([23](../code-findings/23-game-state.md) §6.2), waiting behind a threshold
ordering.

> The pref itself is live elsewhere, as the strength ratio below which a naval
> battle is scored a defeat, so it cannot simply be zeroed.
>
> And it is read a third time for something unrelated: `BattleUnits` ~2848
> tests a **garrison's condition** against it when deciding whether a unit can
> hear a bugle. One pref, three meanings — a naval firepower ratio, a ship
> condition and a garrison condition.

---

## 42. Appendix F, and the three battles it does not list

Appendix F is the manual's only direct word to modders, and the checkable half
of it is right.

**The scenario letters are exact.** *"A = Spring 1861, B = Summer 1861, C =
Spring 1862, D = Spring 1863, E = Spring 1864"* matches each folder's own
`ScenarioDescr.txt` name for name. The install also has an **F**, the Tutorial,
which the appendix does not mention.

**The battlefield table is exact as far as it goes.** All sixteen entries —
`000\A` 1st Manassas through `027\A` Olustee — match the shipped
`ScenarioDescr.txt` titles. But the install holds **twenty** named sub-battles,
and three playable ones are missing from the list:

| Folder | Name |
| --- | --- |
| `000\C` | 1st Manassas (Regt.) — a regiment-scale version of the same field |
| `011\A` | Champion Hill |
| `020\A` | Stones River |

(The twentieth, `006\B`, is the Tutorial.) So a player reading Appendix F to
find a save folder for Stones River or Champion Hill will not find the battle
listed at all, although it ships with the game.

**The warning about file format is exactly right**, and worth repeating here
because it is the premise of this whole repository: *"the code is looking for
certain headers and lines in these files, so the file structure and line
positions must remain intact."* Every config file in the game is parsed **by
position**, with the label lines skipped and never matched
([06](../code-findings/06-config-file-format.md)) — so inserting or deleting a
single line silently shifts every value after it into the wrong field, which is
the mechanism behind the eaten reload pref in §13.

**The four files it names are not where most of the errors are.** Appendix F
calls out `Debug.txt`, `Battleprefs.txt`, `Campaignprefs.txt` and
`Unitprefs.txt` as *"the most important files"*, which is fair for tuning. But
every data error found in six passes lives somewhere else — `resources.dat` and
`buildingtypes.dat` (§2), `weapons.txt` (§9), `campaignprefs.txt` (§27, the one
exception), `campaignobjectives.dat` (§32) and `shiptypes.dat` (§34) — and
`policies.dat` cannot introduce a new policy at all, because `Policy` is
prefab-authored and the file only supplies fields for records that already
exist ([22](../code-findings/22-policy-and-project-catalogue.md) §1.1). That is
the single most useful thing a modder could have been told here.

> Appendix G is headed **"Engineer Corps Organization"** and contains the
> development credits. Whatever was planned for it is not in the document.

---

## 43. Open questions

1. **The manual is now fully checked.** Six passes: the economy chapters with
   both policy appendices (§1–8), combat with the weapons tables (§9–14),
   supply, depots, raiding and readiness (§15–20), the commander chapters with
   the attribute matrix (§21–25), politics, national morale and diplomacy
   (§26–33), and the naval chapters with Appendices E and F (§34–42). What
   remains is not a chapter but the two appendices written qualitatively, which
   cannot be checked number against number: **Appendix B** (projects, partly
   done in §7) and **Appendix C** (perks, question 4). Appendix G is the
   credits (§42).
2. **Whether the fog-of-war accuracy function reads the wrong side
   deliberately.** §24 states what the code does; the config labels state the
   opposite intent. One word from a developer settles it, and nothing in the
   code does.
3. **Whether the three unimplemented matrix cells were ever wired.** Order
   delays by initiative, flanking by cunning and rallying by leadership all
   read like effects that existed and were replaced — order delay by the
   courier model, flanking by the stance thresholds, rallying by the proximity
   bonus. A changelog would say; the assembly does not.
4. **Perk reachability.** [19](../code-findings/19-commanders-and-perks.md) §1.6
   records the perk defects found so far, but the perk *catalogue* has not been
   cross-checked against the manual's special-training chapters, which are a
   separate body of claims from the attribute matrix.
5. **What "2,000" is in map units** for the foraging interpolation
   ([14](../code-findings/14-supply-and-logistics.md) §11 question 8).
6. **Whether the forced-march supply cut-off is intended**, against the 25 %
   cap the config ships and never reads.
7. **Whether the accuracy floor is intentional** —
   [11](../code-findings/11-combat-resolution.md) §8 question 7. Still the
   largest single finding of any pass.
8. **The ~137 unverified dead prefs.** The sweep in
   [07](../code-findings/07-config-data-model.md) lists them; the supply and
   commander passes between them promoted thirteen from candidates to
   confirmed.
9. **A reachability audit of every project effect.** §7's sweep rules out any
   further *discarded* prefs, but not the 113/114 shape — a live-looking use
   inside a condition that cannot be true. Three of roughly 125 project
   effects have been found inert by hand; the remainder are unaudited.
10. **The weapons tables' three non-numeric columns** — Production Cost,
    Required Project and Availability — and whether the project names match
    [22](../code-findings/22-policy-and-project-catalogue.md).
11. **Whether the doubled Locomotive and Transport Ship costs, and the
    6-pounder field gun's firepower, were deliberate rebalances.** The patterns
    are too clean to be coincidence, but the manual is at version 1.13 and the
    build is unversioned
    ([04](../code-findings/04-version-identification.md)).
12. **`maxpricebonusresource`** gates the bonus multipliers in §2 and its
    shipped value was not read.
13. **`fillratiotarget`**, the per-depot buy/sell target, and whether the
    player can set it.
14. **Whether objective 24's `−0.10` is a sign error or its subtitle is wrong**
    (§31.1). Both readings leave the Union's major victory helping Europe side
    with the Confederacy, so the shipped behaviour is wrong either way, but
    which line to patch depends on the answer.
15. **Whether the 1862 midterm penalty is meant to outlive chapter 1**
    (§32.1). Its own config label describes a per-week rate "for the rest of
    the campaign"; the chapter list gives it two days.
16. **Whether the 1864 election's doubled penalty is intended.** Two objectives
    with identical tooltips each charge −0.20 in the same pass (§32.2). If one
    of them is meant to be the Confederate *score* rather than a second
    penalty, the Union's loss condition at the election is −20, not −40.
17. **What `adjustmentmovingspeedsupport0` was tuned to before it became 1.0**
    (§33), and whether the hostile-territory movement penalty the manual
    describes was ever live.
18. **Whether `averagesupport`'s ownership keying is deliberate** (§33).
    Capturing enemy towns lowering your own policy research speed is a
    defensible design; it is also exactly what a loyalty-vs-ownership mix-up
    would look like.
19. **Whether the 4th Rate Steamer's length is 27 m or 37 m in the published
    PDF** (§34). It is the one finding in six passes that the Markdown
    conversion could in principle have caused, and the only way to settle it is
    to read the original.
20. **Whether `Mathf.Max(1f, distance / averageRange)` in the naval
    rate-of-fire term was meant to be `Mathf.Min`** (§38). Under either reading
    the shipped behaviour is a constant, and the close-combat pref the config
    ships is unreachable.
21. **Whether `withinturret` was meant to be a flag or a count** (§39). As a
    count it is correct data and a wrong consumer; as a flag the three "in 2
    turrets" records are wrong data. The ship-level `turrets` field, which is
    parsed and read nowhere, suggests the count reading.
22. **Whether the seven European-only ship types are reachable by capture in
    practice** (§35). The capture loop is unconditional on type, but it needs a
    European squadron beaten in a fleet action, which needs an intervention —
    and intervention is one-shot per power (§31.2). *Untested:* no play
    evidence either way.
23. **What the `combatwidthsea = 7` cap does to large-fleet value** (§40). The
    firepower comparison that sets the victory balance sums every ship while
    the damage model caps at seven a tick, so the two halves of a naval battle
    disagree about fleet size. Whether that is deliberate pacing or an
    oversight is not decidable from the code.
