# Implementation plan

How each row of [`defect-register.md`](../../defect-register.md) gets built.

The register is the **specification** — what is wrong, and the evidence. This is
the **plan** — what to patch, with what technique, what it is allowed to change,
and how to tell it worked. The join is the `GTCW-NNN` id, which is also the
config key a player sees.

> **Line numbers** below are from a local decompile of the build in
> [`00-build-provenance`](../../code-findings/00-build-provenance.md) and every
> target in this document was read before being written down. They are a
> navigation aid, not an identity: the patch targets are resolved by name at
> runtime and probed before use, so a shifted line breaks nothing. A changed
> *method body* is what matters, and that is the signature manifest's job.

---

## 1. Six techniques, ranked by how little they assume

Reading the actual code changed the shape of this mod. The naive expectation was
"a transpiler per defect"; in practice **sixteen of seventeen actionable fixes
need no transpiler at all**, because the defective arithmetic is either a pure
function of state we can redo, or a factor read from a field we can lend a
different value for the duration of one call.

| # | Technique | Use when | Costs |
| --- | --- | --- | --- |
| 1 | **Postfix recompute** | The broken value is a pure function of state still readable after the call | Nothing. Prefer this always |
| 2 | **Postfix rescale** | The method *returns* a value built from a factor we can recompute from its own inputs | Must reproduce the game's formula; drifts if the formula changes |
| 3 | **Prefix capture + postfix restore** | The pass corrupts a value by averaging, so the old value must be known to redo it | One `__state` field; must handle the pass not running |
| 4 | **Field spoof + finalizer** | The defect is a *factor misapplied* from a field the method reads | The field must have no other use in that call; a **finalizer**, not a postfix, or an exception leaves it spoofed |
| 5 | **Chokepoint normalisation** | Several callers write one accumulator and only some are wrong, distinguishably | Fixes the symptom at the gate, not the defective caller |
| 6 | **Pattern-matched transpiler** | None of the above reaches it | IL-shaped. Last resort |

**On transpilers.** [`plan.md`](../../plan.md) says to avoid them as
IL-offset-fragile, and that stands — but the reason is worth stating precisely,
because one row here may still need one. A transpiler matched on an **operand
pattern** (find the `ldfld` of a named field, then the next `mul`) is not
offset-fragile: it either finds its pattern or it does not, and it must
**refuse to apply and log loudly** when it does not. What makes transpilers
dangerous is matching on position. Technique 6 means pattern-matched and
fail-closed, never "patch instruction 47".

### The two rules every patch body follows

- **Wrapped in `FixState.Guard`.** A bug-fix mod that crashes the game is worse
  than the bugs it fixes, and most of these bodies run in the campaign's hottest
  pass. Five throws and the fix switches itself off for the session.
- **Asks `FixState.IsOn` per fix, not per patch.** A method with several fixes on
  it is patched once, because two postfixes independently rescaling one result
  compose into nonsense. See `FixGroup`.

---

## 2. Build order

Four waves. Wave 1 is chosen so that between its five fixes **every technique
above is exercised once**, which is worth more than doing the easy ones first.

| Wave | Rows | What it establishes |
| --- | --- | --- |
| **1** | 001, 002, 003, 004 + 006, 014 | The five highest-confidence fixes, and one worked example of each technique |
| **2** | 007, 009, 011, 012 | The `UpdateAllianceData` group — one patch, four fixes, shared with the ModApi |
| **3** | 005, 008, 010, 013 | Pure functions and load-time repair |
| **4** | 015, 016, 017, 018–021 | Deferred, declined, and the rows that are recorded so nobody fixes them |

---

## 3. Wave 1 — **implemented, awaiting in-game confirmation**

All five are written and build clean: `Fixes/ElectionDoubleCharge.cs`,
`SupportBreakdownNaN.cs`, `ConfiscationActSupply.cs`,
`NavalFiringStrength.cs`, `RaidLedgerSign.cs`. Every one is `fixed`, none is
`confirmed` — the repro and confirm below are still owed.

**Two things changed from the spec as written, both because the code said so.**

1. **GTCW-001 no longer predicts the charge; it watches for it.** The planned
   prefix was to replicate each objective's window test and spoof the pref. That
   needs the fresh value of `deactivated`, and `CheckIfAccomplished` reassigns
   that on its own first line — so a prefix reads the *previous* pass's value.
   Guessing wrong in the safe direction does nothing; guessing wrong in the
   other direction suppresses the **only** charge instead of the second one.
   The implementation instead marks which objective is executing and filters at
   `AdjustNationalMorale`, which observes what actually happens and needs no
   copy of the game's date arithmetic.
2. **GTCW-002 gained a one-shot sweep.** The postfix repairs every *new* NaN,
   but a save written before the fix was enabled keeps its NaN in any slot whose
   reason code never fires again. A sweep at `Milestone.CampaignLive` clears
   those — 10 × 50 floats per state, once per campaign.

### GTCW-001 — the 1864 election charges twice

**Target** `CampaignObjective:CheckIfAccomplished` — `CampaignObjective.cs:534`,
objective 26 at `:923`, objective 27 at `:955`
**Class** C · **default on** · **save:** stateless

Verified, and more precisely than the register states it. Both objectives test
the same date and apply their own penalty under the same guard:

```csharp
// objective 26, Lincoln's Second Term
if (campaignFloatingDate4 >= yearfraction3 + 0.002739726f && !accomplished)
    if (lastupdate < yearfraction3 + 0.002739726f)
        GameVars.alliance[0].AdjustNationalMorale(8, GamePrefs.lincolninfluenceunionmorale);

// objective 27, Demoralize Union Voters — same window, same guard
    if (lastupdate < yearfraction5 + 0.002739726f)
        GameVars.alliance[0].AdjustNationalMorale(8, GamePrefs.demoralizevotersunionmoraleinfluence);
```

Note that objective 27 sets `accomplished = true` **before** applying, but the
guard on applying is `lastupdate`, not `accomplished` — so it fires regardless.
Both prefs are −0.20 and the sweep runs both objectives in one pass.

**Fix.** Allow the first election penalty of the campaign and suppress the
second. Net −20, which is what both tooltips promise.

**Technique 4, field spoof.** A prefix on `CheckIfAccomplished` replicates the
objective's own window test — all of `UniqueObjectiveID`, `lastupdate`,
`accomplished` and the date are readable — and when the fix's per-campaign latch
is already set, spoofs that objective's pref to `0f` for the duration of the
call, restoring in a **finalizer**. When the latch is clear, it sets it and lets
the charge through.

- The latch resets on `SessionStarted` from the ModApi's lifecycle, which is why
  the mod has a hard dependency on the library rather than only on Harmony.
- Spoofing the pref rather than skipping the call keeps every other effect of
  that objective — the newspaper, `accomplished`, the end-of-campaign branch —
  exactly as the game wrote it. Suppressing the whole method would change far
  more than the defect.

**Repro.** Union campaign, reach 9 November 1864 with national morale just
under 50 (`lincolnmaintainmoraleabove`). Unfixed: morale drops ~40 points and
`breakmoraletrigger` (0.25) ends the campaign within one campaign-data pass,
about ten game minutes.
**Confirm.** The same save with the fix on: morale drops ~20 points, the
campaign continues, and the log shows one suppression for the second objective.

**Risk.** Low mechanically, high in consequence — this is the only fix that
changes whether a campaign ends. Worth its own test save committed as a
fixture description in `scripts/`.

### GTCW-002 — the support breakdown is destroyed by a NaN

**Target** `GameVars+Nation:AdjustSupport` — `GameVars.cs:4680`, defect at
`:4699`
**Class** C · **default on** · **save:** value only

```csharp
amount = support[_alliance] - num;                       // the clamped delta
if (Mathf.Abs(supportinfluencelast[_alliance, typ]) < 0.0001f)
    supportinfluence[_alliance, typ] = 0f;               // the reset this gates
supportinfluencelast[_alliance, typ] = Mathf.Pow(amount, 0.5f);   // NaN for a loss
if (supportinfluencelast[_alliance, typ] == float.NaN)   // never true in C#
    supportinfluencelast[_alliance, typ] = 0f;
```

**Fix: make the author's own guard work.** `float.IsNaN(x)` instead of
`x == float.NaN`. Nothing more.

This is worth being strict about. A sign-preserving root —
`Mathf.Sign(amount) * Mathf.Sqrt(Mathf.Abs(amount))` — is a *more useful*
tracker and is **not** this fix: it invents a formula the author did not write.
The guard's existence is the evidence of intent; the formula is not. A
speculative improvement belongs in a different mod, and conflating the two is
how a bug-fix mod loses its credibility.

**Technique 1, postfix recompute.** A Harmony postfix sees the method's final
value of the `amount` argument — the clamped delta, not the caller's original —
so the postfix can recompute `Mathf.Pow(amount, 0.5f)`, test it properly and
write `0f` when it is NaN.

**Interaction worth knowing.** The ModApi's contribution fold *calls*
`AdjustSupport` with reason code 42, so this fix applies to ModApi
contributions too. That is correct and desirable, and it means the fix is
exercised by the Testing Project in `samples/` — a support contribution large
enough to clamp at 0 or 1 produces exactly the loss case that writes NaN.

**Repro.** Lose support in any state for any reason — towns falling to the
enemy is the easiest — then read `supportinfluencelast` for that reason code.
Unfixed it is NaN forever, and because `Mathf.Abs(NaN) < 0.0001f` is false, the
matching `supportinfluence` slot is never reset again.
**Confirm.** The slot returns a real number, and the reset fires on the next
adjustment.

### GTCW-003 — the Confiscation Act makes enemy-soil foraging worse

**Target** `Regiment:UpdateIIPSupplyLink` — `Regiment.cs:4980`, defect at
`:5231`
**Class** M · **default off** · **save:** stateless

```csharp
if (GameVars.alliance[alliance].supplyenemysoilfactor != 1f && unittyp != 17)
    if (/* standing in a state at war with us */)
        num15 *= GameVars.alliance[alliance].supplyenemysoilfactor;   // num15 is TIME
```

Confirmed from the neighbouring lines that `num15` is a duration, not a rate:
`num15 /= Mathf.Max(0.01f, closestiipforsupply.condition)` — worse condition,
longer resupply. So multiplying by 1.15 makes foraging **13 % slower** where the
manual, the appendix and the config comment all promise 15 % faster.

**Fix:** divide instead of multiply.

**Technique 4, field spoof — and here it is elegant rather than a workaround.**
A prefix sets `alliance.supplyenemysoilfactor` to its **reciprocal** for the
duration of the call and a finalizer restores it. The multiply then computes the
correct value with no IL rewriting at all, and the guard above still passes
because `1/1.15 ≠ 1`.

- Verified precondition: the field is read exactly twice in this method, the
  guard and the multiply, and the reciprocal is correct for both.
- **Finalizer, not postfix**, so an exception inside a 300-line method cannot
  leave a permanently halved supply factor on an alliance.
- Fallback if that precondition ever breaks: technique 6, matching `ldfld
  supplyenemysoilfactor` followed by `mul`, replacing with `div`.

**Repro.** Take the Confiscation Act, park an army on enemy soil away from a
depot, and time local resupply against the same army on friendly soil.
**Confirm.** Resupply on enemy soil becomes measurably faster than friendly,
by the ratio the act promises. The Quartermaster's Morning Report is the best
readout.

**Risk.** Default off: this changes supply rates, which data mods tune.

### GTCW-004 + GTCW-006 — naval rate of fire, and the turret test

**Target** `Vessel:GetFiringStrength` — `Vessel.cs:627`; turret test at `:658`,
rate-of-fire blend at `:676`
**Class** C · **both default off** · **save:** stateless

One method, two defects, therefore **one patch and two toggles**.

```csharp
if (GameVars.shiptypes[type].weaponsystems[i].withinturret == 1)   // GTCW-006
    num3 += guns;
...
num4 /= Mathf.Max(1f, num2);                        // weighted mean range of guns in range
float num6 = Mathf.Max(1f, distance / Mathf.Max(1f, num4));   // GTCW-004: always 1
float num7 = navalbattlerofmaxdistance * num6 + navalbattlerofmindistance * (1f - num6);
num7 *= (rofimprovementturrets - 1f) * num + 1f;    // num = num3 / max(1, num2)
```

`num6` cannot be below 1: every gun counted in `num4` passed
`range_yd >= distance`, so `distance / num4 <= 1`, and `Mathf.Max` raises it.
Every ship in every engagement therefore fires at `navalbattlerofmaxdistance`
= 0.25. With `Mathf.Min`, a ship closing to point-blank reaches 1.00 — **four
times the rate of fire**, which is why this is default off however certain it is.

**Technique 2, postfix rescale.** The postfix has `__instance` and the
`distance` argument, so it re-walks `weaponsystems` — a handful of entries —
recomputes `num2`, `num3`, `num4`, builds the game's `num7` and the corrected
one, and scales `__result` by the ratio. It also rewrites
`strengthdata[3]` (the rate-of-fire figure the ship panel reads) and, for
GTCW-006, `strengthdata[2]`.

Three details that must be got right, all verified in the method:

- **The low-ammo branch wins.** At or below `lowonammotriggerfleets`, `num7` is
  overwritten with `navalbattleroflowammo` and the blend is irrelevant — the
  postfix must detect that and apply no correction.
- **`reduceammo` already spent ammo at the wrong rate.** When that argument is
  true the postfix must also refund the difference:
  `ammo += (oldNum7 - newNum7) / averagesalvopervessel * num5 * ammoconsumptionfactor`.
  Skipping this makes the fix silently change ammunition endurance as well.
- **GTCW-006 changes `num3`, which feeds `num`, which scales `num7`.** The two
  fixes interact inside one expression, so the postfix computes the corrected
  `num7` once from whichever toggles are on — not two independent corrections.

**Repro (004).** A fleet action at long range against the same action closed to
minimum range: unfixed, `strengthdata[3]` reads 0.25 in both.
**Confirm (004).** It reads 0.25 at maximum range and approaches 1.00 as the
range closes.
**Repro (006).** Compare an Ironclad Monitor (one turret, gets ×1.4) with a
River Monitor (four guns in two turrets, gets nothing) — `withinturret` is a
count, not a flag.
**Confirm (006).** All six turret ships show the turret bonus in
`strengthdata`, and the ship panel's own `gunsinturrets` figure finally agrees
with it.

### GTCW-014 — the raid ledger is written with the wrong sign

**Target** `GameVars+Alliance:AddTradeWarfare` — `GameVars.cs:3948`; the
defective caller is `Regiment:UpdateRaids`, `Regiment.cs:4619`
**Class** C · **default on** · **save:** value only

```csharp
// Regiment.UpdateRaids:4619 — all three factors strictly positive
GameVars.alliance[list[l].allianceowner].AddTradeWarfare(2, resourcesCost * num * num2);

// CBuilding.cs:1876 — the same category, explicitly negated
GameVars.alliance[Owner].AddTradeWarfare(2, num6);        // num6 = ... * (0f - (1f - Condition)) * ...
```

**Fix: enforce the array's own sign convention at the chokepoint.** Technique 5
— a prefix on `AddTradeWarfare` that negates a positive `value` for a category
whose convention is negative.

This is a symptom fix at the gate rather than a repair of the defective caller,
and that is the right trade here. The alternative is a transpiler on a 60-line
method to negate one argument, for an array
[nothing in the simulation reads](../../code-findings/33-trade-warfare.md).

**Precondition, verified:** the correctly-signed caller never passes a positive
for category 2 — `CBuilding`'s term is negative for any `Condition < 1`, and
condition is bounded above by 1. The prefix is therefore a no-op for the
correct caller. State this in the code comment, because it is the whole
justification for the technique.

**Repro.** Set cavalry to raid (`cavalryorders == 2`) on an enemy trade node and
watch the victim's "losses through raids" figure on the commerce-war panel
*fall*, and the commerce-war slider move the wrong way.
**Confirm.** It rises, slot 0 agrees with the sum of 1–3, and the slider tracks
the side actually losing trade.

---

## 4. Wave 2 — the `UpdateAllianceData` group

**Target** `BattleUnits:UpdateAllianceData` — `BattleUnits.cs:3582`
Four fixes, **one patch**: `:3726` (011), `:3788` (007), `:3793–3800` (009),
`:3842–3848` (012).

This is the delicate wave, for two reasons.

**It is the ModApi's own hook point.** `ModifierPatches` already runs a
prefix/postfix pair here with a `PassState` capture, and folds contributions by
calling `AdjustSupport` / `AdjustNationalMorale` / `AdjustTreasury`. Our pair is
a second prefix/postfix on the same method from a different assembly. They do
not collide — the ModApi's fold touches support, morale and treasury, and these
four fixes touch `powratio`, `transportcapacitypeak`, `battlecasualtiesavg` and
`weaponindustrylast` — but the patch must **set an explicit Harmony priority**
so the order is defined rather than incidental, and the implementation must
record which it chose and why.

**The pass is skipped more often than it runs.** `UpdateAllianceData` processes
one alliance per beat and refuses to advance until half a game day has passed
for that alliance, returning early otherwise. A postfix runs on the early
return too, so every fix here must detect whether the body actually ran. The
ModApi already solved this by watching `lastalliancedataupdate` change across
the pass; copy that, do not invent a second method.

Every fix in this group also needs the pass's own elapsed time, `num2`. **Use
the measured value, never the nominal 0.5/365** — it is about 6 % longer in
practice, because an alliance is visited once per eight-beat round-robin so the
threshold lands on the ninth cycle.

### GTCW-007 — `powratio` counts only the last POW camp

```csharp
num9 += (float)Tools.SumUp(powcamps[n].prisoners);          // accumulates
num8 = GamePrefs.powcampcapacity[powcamps[n].level];        // ASSIGNS
...
GameVars.alliance[a].powratio = num9 / Mathf.Max(1f, num8);
if (num8 == 0f) GameVars.alliance[a].powratio = 10f;
```

**Default off** · **save:** value only. **Technique 1** — the cleanest fix in
the set: a postfix re-walks `powcamps` (private, hence the publicizer), sums
capacity properly, and recomputes, reproducing the `num8 == 0f → 10f` special
case exactly.

**Repro.** Hold prisoners across two or more camps of different levels and
compare `powratio` against prisoners ÷ total capacity.
**Confirm.** They agree, and adding a second camp now lowers the ratio.

### GTCW-009 — `transportcapacitypeak` is sampled three times a pass

```csharp
for (int num10 = 0; num10 < ...transportcapacity.Length; num10++)   // outer index UNUSED
    for (int num11 = 0; num11 < 3; num11++)
        ...transportcapacitypeak[num11] = <trailing average step>;
```

The outer loop's index appears nowhere in the body, so each of the three slots
takes three identical samples per pass and the trailing window is a third of
`calculationdurationaveragetransportutilization`.

**Default off** · **save:** value only. **Technique 3** — a prefix captures the
three values, and the postfix restores them and applies the step exactly once,
using the same formula and the measured `num2`.

**Repro.** Saturate rail capacity briefly and watch the peak figure decay about
three times too fast.
**Confirm.** The decay matches the configured window.

### GTCW-011 — `battlecasualtiesavg` averages a cumulative counter

```csharp
...battlecasualtiesavg = Tools.AddToAverage(...battlecasualtiesavg,
                                            ...casualtiessuffered,   // CUMULATIVE
                                            num, num2);
```

Every other call in that block averages a `_tmp` field reset each pass; this one
averages a running total, so the "average" climbs monotonically for the whole
campaign.

**Default on** · **save:** value only. Verified display-only: the readers are
`CampaignTopPanel`, the save file, and this line.

**Technique 3 plus a little state of our own** — the correct sample is the
*change* in `casualtiessuffered` since the previous pass, so the fix keeps a
last-seen value per alliance. It must survive a load, which means seeding from
the current value on `SessionStarted` rather than assuming zero, or the first
pass after a load books the entire campaign's casualties as one period's worth.

**Repro.** Fight several battles over a year and watch the figure only ever rise.
**Confirm.** It falls during a quiet year.

### GTCW-012 — the warm-up gate uses the rewinding frame counter

```csharp
if ((...weaponindustrylast == 0f) | (GameVars.frame < 1000))
    ...weaponindustrylast = ...weaponindustry;        // snap, no averaging
else
    ...weaponindustrylast = Tools.AddToAverage(...);
```

`GameVars.frame` rewinds during a load, so for ~1000 frames after **every** load
the trailing average is replaced by the instantaneous value on every pass.

**Default on** · **save:** value only. Readers are `CampaignStrategy`, the save
file, and this line — display only.

**Technique 3** — the prefix captures `weaponindustrylast`; the postfix detects
that the game took the snap branch (the published value now equals
`weaponindustry` exactly) and, if the fix's own per-alliance warm-up flag says
this alliance is already warmed, re-applies the trailing average from the
captured value instead.

Deliberately **not** technique 4: spoofing `GameVars.frame` around the call
would be shorter, but `frame` is read elsewhere in a 250-line method and lying
about it is not a contained change.

**Repro.** Save, load, and watch the weapon-industry trend line go flat and
jumpy for the first few seconds, then settle.
**Confirm.** It is continuous across a load.

---

## 5. Wave 3 — pure functions and load-time repair

### GTCW-005 — `occupiedby` is a latch

**Target** `GameVars:UpdateNationsData` — `GameVars.cs:7913`, defect at `:7947`

```csharp
bool flag = true;
for (...) if (nation[j].townsinstate[l].Owner != num) flag = false;
if (flag) nation[j].occupiedby = num;        // and no else
```

**Default off** · **save: one way** — this is the register's clearest case for a
repair-on-load, because patching the write does nothing about a flag already
latched in a save.

**Technique 1** — a postfix recomputes `occupiedby` for every nation from town
ownership, setting `-1` where no single alliance holds them all. The same code
serves as the load-time repair, so there is one implementation and not two.

**Repro.** Fully occupy a state, then lose a town back. `occupiedby` still names
you, your units there stay exempt from the enemy-soil `fightingspirit` and
supply penalties, and projects 93/94 keep paying.
**Confirm.** It clears on the pass after the first town is lost.

**Risk.** Removes a standing advantage a long campaign may have accumulated, so
turning this on mid-campaign is a visible nerf. Say so in the config
description, not just here.

### GTCW-008 — the all-types tax total omits the corporate modifier

**Target** `GameVars+Alliance:GetCurrentTaxValuePa` — `GameVars.cs:4358`

```csharp
-1 => ... + corporateprofits * tax[tax_corporate] * taxcaps[tax_corporate] + ...
//                                      ^ no GamePrefs.taxrevenuemodifier[tax_corporate]
 3 => corporateprofits * tax[taxid] * taxcaps[taxid] * GamePrefs.taxrevenuemodifier[taxid],
```

**Default off** · **save:** stateless. **The register's "pending: identify the
consumer" is now resolved, and the answer is why it stays off:** the no-argument
form feeds `Economy:2130` (`surplus`) and `Alliance.GetBalance()`, so this is
real money, not a readout.

**Technique 1** — a postfix adds the missing term when `taxid == -1`.

**Repro.** Set a corporate tax rate with `taxrevenuemodifier[corporate] != 1`
and compare the all-types total against the sum of the five per-type calls.
**Confirm.** They agree.

### GTCW-010 — the save loader's treasury floor is dead

**Target** `ImportExportUnitData:OverwriteNationsData` — `:1092`; clamp at
`:1215–1218`, overwrite at `:1237`

Verified exactly: the writer emits `treasury` **twice** (`:804`, `:831`) and the
loader reads it twice — the first read clamped to −100 M, the second
unclamped. The second wins.

**Default off** · **save: one way**. **Technique 1** — a postfix clamps every
alliance's treasury to ≥ −100 M after the load.

**Risk, and the reason this is off:** the clamp is the game's own stated intent,
but applying it *forgives* debt beyond −100 M for whoever is past that line,
which is a real swing in a late campaign.

**Repro.** Borrow past −100 M, save, reload: the treasury is unchanged rather
than floored.
**Confirm.** It loads at exactly −100 M.

### GTCW-013 — `researchedonly` counts policies still in research

**Target** `GameVars+Alliance:GetActivatedPolicies` — `GameVars.cs:3991`

```csharp
if (researchedonly)
{
    if (activatedpolicies[i].researchprogress < 1f) num++;   // in research, not researched
}
else if ((!IsAct | includeactsinresearch) && (includegrandpolicies | !grandpolicies.Contains(...)))
    num++;
```

The `researchedonly` arm also ignores both other flags, which four call sites
bother to pass.

**Default off** · **save:** stateless. **Technique 1**, a postfix recompute —
but **this is the riskiest "obvious" fix in the register** and the plan should
say so plainly: `Policies.cs:379` uses the count in a `while` loop that
**removes policies** when the total exceeds `maxpolicies`. Inverting a
comparison here can delete a player's finished policies. It needs its own test
campaign, and it ships off until that exists.

**Repro.** Put two policies into research and call the method with
`researchedonly: true`; the count includes them.
**Confirm.** It counts only completed ones, and the four call sites behave
sensibly — specifically that `Policies.cs:379` does not start removing
policies.

---

## 6. Wave 4 — deferred, declined, and recorded-so-nobody-fixes-them

### GTCW-015 — raids are missing from the trade node's own tooltip

**Target** `Regiment:UpdateRaids` — `Regiment.cs:4574`
**Deferred.** `IIP` and `CBuilding` both mirror their charge into a per-object
`lossestradewarfare`, which the map tooltip reads; `UpdateRaids` does not.

The per-node amount is a local inside a loop, so techniques 1–3 cannot reach
it. There is one route that avoids a transpiler: the line immediately above
calls `Economy.AdjustStockIIP(list[l], GamePrefs.raidvolume, num * num2)`,
which **takes the IIP as its first argument**, so a postfix there sees the node
and the factor and can compute the cost itself. The discriminator would be
reference-equality on `GamePrefs.raidvolume` to tell a raid from any other
caller — workable but fragile.

Deferred because the payoff is one tooltip line. Revisit only if `UpdateRaids`
is being patched for something else anyway. **Default on when it lands**
(display-only).

### GTCW-016 — runtime state renaming breaks name matching

**Reclassified to "recorded, not fixed" — and the scaffolding is what settled
it.** The register had this as default-on. Checking the consumers:

- `BattlefieldSetup:UpdateStateOccupation` (`:1239`) renames
  `nation[i].name` from `statesetname[set]` — and that is **intended**;
  territorial names genuinely change with the active state-set.
- `GameVars.SearchNationByName` (`:4597`) matches by string and would break —
  but it has **no callers anywhere in the assembly.**
- `Policies.cs:726` compares seven names inline (Illinois, Michigan, Wisconsin,
  Indiana, Ohio, New York, Pennsylvania). **None of them is a state whose name
  changes**, so the policy bonus is not at risk.
- `AvatarManagement.cs:1049` matches a name against UI text.

So there is **no reachable consumer**, and a patch would repair nothing. What
remains is real but is advice rather than code: a scenario mod that fills
`statesetname` for a state referenced by name elsewhere would create the bug.
That belongs in the modder documentation, and the row stays in the register so
the conclusion is not re-derived.

### GTCW-017 — the 1862 midterms window is two days

**Target** `CampaignObjective:CheckIfAccomplished` — penalty at `:705`
**Default off, and arguably not ours.** Objective 9's chapter list is `{1}` and
chapter 2 begins 6 November 1862, so a config label describing −0.01 Union
morale *per week* is worth about −0.3 points in total.

"Fixing" it means extending the window, which turns a token into a −52/year
penalty. That is a **design decision, not a repair** — nothing in the code says
which was meant. It stays recorded, off, and flagged as a candidate for a
balance mod rather than this one.

### GTCW-018 to GTCW-021 — not fixed, by class

| Id | Why not |
| --- | --- |
| GTCW-018 | Class X — three Acts promise the removed Transportation subsidy. Restoring a cut category is content, not repair |
| GTCW-019 | Class X — the Navy rank-change dates are authored data for an unimplemented feature |
| GTCW-020 | Class B — unlimited intervention recruits read as a deliberate shortcut for non-belligerents. Classification unconfirmed; revisit only if European forces become playable |
| GTCW-021 | Class C with **no behavioural effect** — `AddTradeWarfare`'s orphaned guard statements. Recorded so nobody spends a patch on it, and because it is the reason GTCW-014 went unnoticed |

---

## 7. Verification protocol

A fix is `fixed` when it is written and `confirmed` when it has been seen
working in game. Nothing ships between those two states.

- **Each fix carries its own repro and confirm**, above. They are part of the
  spec, not an afterthought — a fix whose confirm cannot be described is a fix
  whose effect is not understood.
- **Use the Testing Project** in `samples/HelloWorldMod` where a fix needs a
  large, fast effect to be visible: free, repeatable, and already wired to move
  support, morale and treasury per fold.
- **Toggle each fix off and repeat the repro.** This is the real test of the
  compatibility promise: with everything off, no patch is applied at all and
  the behaviour must be indistinguishable from the mod's absence.
- **One fix at a time, with the others off.** Wave 2 shares a method with the
  ModApi's own patches, and a wrong number there could come from either side.

---

## 8. API gaps this mod has already found

The point of building the fix mod first is that **a fix the public API cannot
express names a gap in it**. Three appeared while scaffolding, before a single
fix was written:

| # | Gap | Evidence |
| --- | --- | --- |
| 1 | **No public build-probing surface.** `GameProbe` and `ProbeResult` are `internal` to `GTCW.ModApi.Runtime`, so this mod reimplemented them in `FixProbe.cs`. Any game-aware mod wants exactly this | `FixProbe.cs` |
| 2 | **No public fail-soft guard.** `FailSoft.Guard` and its throw budget are internal, so `FixState.Guard` duplicates them | `FixState.cs` |
| 3 | **No public build identity.** The library computes a SHA-256 of `Assembly-CSharp.dll` and knows the hash of the build these findings were gathered on; a mod cannot ask. Every fix here is evidence-bound to that build and would like to say so in its own log line | `BugFixesPlugin.cs` |

All three are the same shape: **machinery a mod that patches the game needs, currently
visible only to the library.** They belong behind one small public surface — a
`ModApi.Build` facade for identity and a `ModApi.Diagnostics` one for probing
and guarding — and that is now a concrete Phase 4 requirement rather than a
guess about what modders want.

A fourth, smaller note for the modder documentation: a mod whose own namespace
begins with `GTCW.` cannot write a bare `ModApi.` to reach the facade, because
the namespace shadows the class. It needs
`using Api = GTCW.ModApi.ModApi;`, as `BugFixesPlugin.cs` does.
