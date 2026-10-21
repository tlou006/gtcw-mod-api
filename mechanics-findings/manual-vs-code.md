# The manual against the code

**Build:** see [00-build-provenance](../code-findings/00-build-provenance.md)
**Source:** `game-manual.md` — the official manual, version **1.13**, checked
against `Config/resources.dat`, `Config/buildingtypes.dat`,
`Config/campaignprefs.txt`, `Config/weapons.txt`, `Config/unitprefs.txt`,
`Config/battleprefs.txt` and the decompiled assembly
**Translates:** [18](../code-findings/18-economy.md),
[23](../code-findings/23-game-state.md),
[11](../code-findings/11-combat-resolution.md),
[12](../code-findings/12-melee.md),
[13](../code-findings/13-morale-and-cohesion.md)

The manual is the developers' own account of the mechanics, so it is the right
yardstick for this folder: where it and the code agree, a finding is confirmed
from both ends; where they disagree, one of them is misleading players.

Two passes are recorded here. **Part I (§1–8)** covers the economy chapters and
both policy appendices; **Part II (§9–14)** covers the combat chapters and the
three weapons tables. The manual is 9,000 lines; supply, commanders, politics
and the naval chapters are still unchecked (§15).

> **The manual is accurate far more often than not**, in both passes. Of 37
> production recipes and 38 producer assignments, 33 and 37 match the shipped
> data exactly, including every connector in the four-term boolean recipes; of
> some 180 numbers in the weapons tables, four are wrong. What follows is the
> exceptions, which is not a fair picture of the document as a whole.

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

## 15. Open questions

1. **The chapters still unchecked.** Two passes are done: the economy
   chapters with both appendices (§1–8) and the combat chapters with the
   weapons tables (§9–14). Still open: **supply and logistics**, **commanders
   and perks**, **politics and national morale**, the **naval** chapters, and
   **Appendix E**, the ship and ship-weapon tables. On the evidence of two
   passes the prose will hold up and the data tables are where to look.
2. **The weapons tables' three non-numeric columns.** Production Cost
   (*Always Available*, *Not in production*, *Low* … *Very High*), Required
   Project and Availability were **not** checked — the mapping from
   `complexity`, `productionmultiplier`, `techavailableforalliance` and
   `importavailableforalliance` to those words has not been traced. Project
   names in particular are a cross-reference to
   [22](../code-findings/22-policy-and-project-catalogue.md) worth doing,
   because a wrong project name sends a player down the wrong research path.
3. **Whether the accuracy floor is intentional** — see
   [11](../code-findings/11-combat-resolution.md) §8 question 7. This is the
   largest single thing found in the combat pass and the only one where the
   finding would change if a developer said "that `Mathf.Max` is deliberate".
4. **The ~137 unverified dead prefs.** The sweep in
   [07](../code-findings/07-config-data-model.md) lists them; each needs the
   same by-hand check the eight confirmed ones got before being called dead.
5. **A reachability audit of every project effect.** §7's sweep rules out any
   further *discarded* prefs, but not the 113/114 shape — a live-looking use
   inside a condition that cannot be true. Three of roughly 125 project
   effects have now been found inert by hand; the remainder are unaudited.
6. **Whether the doubled Locomotive and Transport Ship costs were a deliberate
   rebalance.** The 2× pattern is too clean to be coincidence, but the manual
   is at version 1.13 and the build is unversioned
   ([04](../code-findings/04-version-identification.md)), so the order of
   events is not recoverable from here. Worth asking the developers — along
   with the 6-pounder field gun's firepower, which has the same shape.
7. **`maxpricebonusresource`** gates the bonus multipliers in §2 and its
   shipped value was not read.
