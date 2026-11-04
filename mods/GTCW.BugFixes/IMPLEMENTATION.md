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
"a transpiler per defect"; in practice **sixteen of the first seventeen
actionable fixes need no transpiler at all**, because the defective arithmetic is
either a pure function of state we can redo, or a factor read from a field we can
lend a different value for the duration of one call.

The perk family (§6b) made that stronger rather than weaker. Thirty-seven more
rows, nearly all of them arithmetic buried inside very long methods, and exactly
**one** — GTCW-047 — is a candidate for a transpiler. The reason is that the game
reads every perk factor out of a `GamePrefs` field, and those fields turn out to
have one reader each.

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
| **5** | 023–059, of which 024, 025, 027, 028, 035, 036, 038, 039, 041 are built | The perk family — the largest body of manual deviation found, and the first wave driven by an intention (*perks work as documented*) rather than by technique coverage |

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

> **Withdrawn — do not implement.** Re-verified against the four callers: the
> in-research count is what every one of them needs. `UpdatePolicyResearch`
> (`Policies.cs:321`) and the research tooltip (`:290`) divide research speed by
> the number of policies in research; `CheckAIPolicyChange` (`:1845`, `:1860`)
> keeps at least one in research. Grand policies are researched to 1.0 at
> campaign start, so the ignored `includegrandpolicies` never changes the
> count. Making this arm count completed policies would break all four. The
> `Policies.cs:379` risk below is also misplaced — that call uses the default
> form, which `researchedonly` does not touch. See the register row.

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
chapter 2 begins late on 5 November 1862, so a config label describing −0.01
Union morale *per week* is worth about −0.15 points in total.

> **Corrected:** the window is about 24 hours, not two days. `Tools.Date` and
> the campaign clock measure the day differently, which puts objective 9's
> trigger at about 21:24 on 4 November and chapter 2 at about 21:45 on
> 5 November ([24](../../code-findings/24-campaign-objectives.md) §3.1). The
> conclusion — a token, and the fix is a design call — is unchanged.

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

## 6b. Wave 5 — the perks, as documented

The perk family is [34 — Perks](../../code-findings/34-perks.md) turned into
patches: 37 register rows, GTCW-023 to GTCW-059, of which **nine are
implemented** and the rest are specified below. The intention is a single
sentence — *a perk should do what its tooltip and the manual say it does* — and
the rows are the distance between that and the build.

**One switch for the family.** Each row moves a combat or campaign quantity, so
each one defaults off. Thirty-seven toggles would make the family unreachable in
practice, so the registry also binds `perksasdocumented`: turning it on turns on
every implemented perk fix, and an explicit per-id entry still wins. The switch
is not a shortcut around the default-off rule — it is the one decision the rule
is really asking the player to make.

That forced a change to the config schema, and it is worth stating why. **Each
id's entry is now `Default` / `On` / `Off`, not a `bool`.** BepInEx writes every
bound entry to the config file on the first run, so with a `bool` there is no way
to tell *"the player wants this off"* from *"the player has never touched it"* —
and a family switch that cannot be overridden for one id is not a switch. With
three states the resolution order is the player's word, then the family, then the
shipped default (`FixRegistry.IsWanted`). Wave 1's six ids changed type with the
rest; the mod is unreleased, and the alternative was two kinds of toggle in one
file.

### The technique that unlocked the wave

Nine of these defects are inside three very long methods — `UpdateMorale`,
`UpdateRaiding`, `UpdateCampaignUnitStats` — where the broken arithmetic is a
local the patch cannot reach. **Technique 4 reaches all of them**, because the
game reads almost every perk factor out of a `GamePrefs` field that has exactly
one reader:

| Pref | Readers in the whole assembly |
| --- | --- |
| `perk_mountedriflesmalusfactor` | 2, both of them the defect |
| `perk_deadlyvolley_reductionmoralehit` | 1 |
| `moraleeffectonhighmomentarycasualties` | 1 |
| `drillreductionfactorforhighmomentarycasualties` | 1 |
| `moraleeffectofbeingcutoff` | 1 |
| `casualtyfactorguardingunits` | 1 |
| `skirmishingpartisanbrigades`, `raidingmoralepartisanbrigades`, `skirmishingbushwhackers` | 2 each, both in the defective block |

So a prefix can lend the method a *different* value for one call — chosen so
that **the game's own arithmetic produces the documented result** — and a
finalizer puts the shipped value back. That is worth more than it sounds: the
corrected term then goes through the method's own clamps, its net-change
amplification by experience and leadership, and into `moraleeffects[]` for the
breakdown panel, all without this mod reimplementing any of it.

Two variants appear for the first time here:

- **Perk-entry spoof.** `Regiment.Perk` is a class in a `List<Perk>`, so a
  prefix can set `perks[i].perk = -1` for the duration of one call and restore
  it in the finalizer. That is how a perk is made *not* to fire at a site where
  the game grants it too early (GTCW-027). A fighting unit has exactly one perk
  slot, so there is nothing else in the list to disturb.
- **Scoped read redirection.** A postfix on `Regiment:GetPerkLevel`, active only
  inside one scope, answers a question the caller asked of the wrong unit
  (GTCW-041). It is a chokepoint (technique 5) on a method called a few thousand
  times a second at worst, so the body is a single static `bool` test before
  anything else happens.

**A fourth rule for perk patch bodies**, on top of the two in §1: *restore in a
finalizer, never a postfix.* Every fix in this wave lends the game a value, and
`UpdateMorale`, `UpdateRaiding` and `UpdateCampaignUnitStats` all call into code
that can throw. A spoofed `GamePrefs` field that is never restored is a global
balance change for the rest of the session — far worse than the defect.

### GTCW-024 — Deadly volley's resistance works backwards

**Target** `Regiment:UpdateMorale` — `Regiment.cs:18221`
**Technique** 4, on two prefs at once.

The game computes, for the high-momentary-casualties term:

```
moraleeffects[9]  = T                       // moraleeffectonhighmomentarycasualties, −0.6
moraleeffects[9] -= moraleeffects[9] * training * d * f
```

where `d` is `drillreductionfactorforhighmomentarycasualties` (0.7) and `f` is
the perk's 0.5 **only from level III** (`GetPerkLevel(5) >= 2`). The 0.5 lands on
the drill reduction, so a fully drilled unit's hit *grows* from −0.18 to −0.39,
and an undrilled one sees nothing. The config label — "Deadly volley reduction
of morale hit from high momentary casualties (factor)" — says what was meant,
and the manual gives it at every level.

The fix lends two values so that the game produces `T·p·(1 − training·d)`:

```
T' = T * p            where p = perk_deadlyvolley_reductionmoralehit
d' = d / f            where f is the factor the game will apply anyway
```

With `f = p` at level III and `f = 1` below it, `T'·(1 − training·d'·f)` is
exactly the documented term at every level, and the shipped 0.5 stays the
authority on magnitude. No reimplementation: the perk's resistance is now the
hit halved, which is what "+50 % resistance" means.

**Repro** A drilled brigade holding Deadly volley III, taking more than
`losstriggerforhighmomentarycasualties` of its strength in one pass; watch
morale effect 9 in the unit panel's breakdown. Unfixed it reads about −0.39 per
hour, worse than the same brigade without the perk.
**Confirm** The term reads about −0.09 at full drill, and a *level I* holder
shows half the no-perk value where before it showed all of it.

### GTCW-025 — Iron discipline freezes the rout penalty it should ignore

**Target** `Regiment:UpdateMorale` — decay at `Regiment.cs:17953`, accumulation
at `Regiment.cs:13671`
**Technique** 1, in the prefix.

`friendlyroutednear` is a saved, negative accumulator: a friendly rout within
`rangeroutedunits` adds `impactownunitrouted` (−0.075), and `UpdateMorale` walks
it back toward zero at `routedunitrecovery` (0.3) per hour — fifteen minutes and
it is gone. From level II the perk sets that recovery factor to **zero**, so the
penalty never decays: every rout nearby costs −0.075 an hour for the rest of the
battle, accumulating down to the −1 clamp. The manual's level II says the unit
holds "even if other units nearby falter".

The fix is one line in the prefix: for a holder at level II or above,
`friendlyroutednear = 0f` before the term is read. The frozen decay is then
harmless — there is nothing left to decay — and a unit that accumulated a
penalty at level I has it cleared the first pass after it levels. Nothing is
reimplemented and the `enemyroutednear` half, which is a *bonus* and works, is
untouched.

**Repro** Two brigades in line, one holding Iron discipline II+. Rout the
neighbour, then withdraw the holder out of contact and watch morale effect 17.
Unfixed it sits at −0.075 (or a multiple) indefinitely.
**Confirm** Effect 17 reads 0 for the holder on the next pass, while a brigade
without the perk shows the penalty decaying away as normal.

### GTCW-027 — Vanguard: right effects, wrong levels

**Target** `Regiment:UpdateMorale` — `Regiment.cs:18034`, `:18070`, `:18371`
**Technique** 4 plus the perk-entry spoof.

Two defects, one perk, one fix:

- A bare `HasPerk(12)` zeroes `moraleeffects[2]` (outflanked, to −0.27/h
  surrounded) and `moraleeffects[3]` (flanking fire, to −1.0/h from all sides)
  **at every level**. That is the manual's level II benefit, delivered at level
  I.
- `moraleeffects[15]`, "outside the influence of its commander", has **no perk
  test at all** — so the level I headline, *no morale hit if detached*, is
  missing entirely.

The prefix therefore does two things for a holder, both scoped to one call:

| Level | Perk entry | `moraleeffectofbeingcutoff` |
| --- | --- | --- |
| I | set to `-1`, so slots 2 and 3 apply normally | lent `0f` |
| II+ | left alone | lent `0f` |

The level I case is a *nerf*, and deliberately so: the fix makes the perk
deliver what it documents, not everything it currently gives plus what it owes.
Slot 10 (falling back, retreating) already gates correctly at levels II and III
and is untouched — at level I the perk-entry spoof makes no difference to it,
because its own test would have failed anyway.

**Repro** A detached cavalry regiment holding Vanguard I, inside fire range and
out of its commander's radius: morale effect 15 shows the cut-off penalty
unfixed, and effects 2 and 3 are zero even while it is being flanked.
**Confirm** Effect 15 is 0; effect 2 or 3 now appears when flanked. A Vanguard
II holder shows 15 at 0 *and* 2 and 3 at 0.

### GTCW-028 — Mounted rifles is backwards

**Target** `Regiment:FireBullet` — `Regiment.cs:9620`; `Regiment:UpdateFiringAndReloading` — `Regiment.cs:10004`
**Technique** 4, lending the complement of an array.

Both sites compute `(1 − base) × A[level] + base`, treating
`perk_mountedriflesmalusfactor` as the share of the mounted penalty
**recovered**. The config label calls it the *malus* factor and the shipped
values decline (0.75 / 0.5 / 0.2), which is a remaining-malus array — so the
code reads it inside out:

| | No perk | I | II | III |
| --- | --- | --- | --- | --- |
| Mounted accuracy, as shipped | 0.60 | 0.90 | 0.80 | **0.68** |
| Mounted accuracy, fixed | 0.60 | 0.70 | 0.80 | 0.92 |
| Mounted reload time, as shipped | 1.25 | 1.06 | 1.13 | **1.20** |
| Mounted reload time, fixed | 1.25 | 1.19 | 1.13 | 1.05 |

The fix lends `1 − A[i]` for the duration of each call, so level I removes a
quarter of the malus and level III four fifths — the declining array read the
way its label describes. The complement is computed from the *live* array and
cached by reference identity, so a data mod that re-authors those three numbers
is respected rather than overwritten.

Only the reload half changes a fight: mounted fire is multiplied by 0.6 before
the perk, which keeps the shot accuracy under the aim-scatter floor at every
level either way (§2.4 of the audit). The accuracy site is fixed anyway, because
leaving a known-inverted read in place because it currently cannot be seen is
how it becomes visible later.

**Note on level III.** The manual says the mounted reload penalty is removed
entirely at level III; the shipped array leaves 20 % of it. The fix follows the
data, and the gap is a one-line data question, not a defect to patch.

**Repro** A dismountable cavalry regiment, mounted, firing: compare its rate of
fire at perk level I against the same regiment at level III. Unfixed, level III
is slower.
**Confirm** Reload time now falls as the perk levels, and level I is strictly
worse than level III.

### GTCW-035 + GTCW-036 — the attrition perks read the brigade's battle perks

**Target** `Regiment:UpdateCampaignUnitStats` — `Regiment.cs:5998` (Ambulance
Corps), `Regiment.cs:6040` (Foot Cavalry)
**Technique** 1 — and the method hands us the answer.

`UpdateCampaignUnitStats(Regiment groupunit, int state)` runs **on the
brigades** — it returns early above unit type 13 — so `HasPerk(8)` there is
battle perk 8, Expert scouts, and `HasPerk(6)` is Ace of Spade. `HasPerkParent`
is false for a brigade, because it only answers for a Corps. The two army perks
therefore never reach their own effects, while cavalry brigades holding Expert
scouts quietly get the wounded-mortality cut and infantry brigades holding Ace
of Spade get the forced-march attrition cut.

The fix needs no search for the formation, because the method is already given
it: `groupunit` **is** the parent formation (the caller is
`array[m].UpdateCampaignUnitStats(this, …)`). Both fixes are the same shape — a
prefix that, for the duration of the call, answers perk 6 and perk 8 on the
brigade with the formation's army perk instead:

- the brigade's own battle-perk 6 or 8 is hidden (perk-entry spoof), so the
  wrong effect stops firing;
- the formation's level, including its own parent's through
  `groupunit.HasPerkParent`, is lent to the two factor arrays by the same
  single-reader trick — `woundedtokilledambulancecorps` and
  `attritionfootcavalry` each have exactly two readers and both are these sites.

The two ids are separate because the two perks are separate and a player may
want one without the other; they share one patch because they share one method.

**Open question carried from the audit.** Foot Cavalry's attrition half depends
on a brigade's `doublequick` being true on the campaign map, and the only
campaign writer found is `RegimentMove` on the unit that *moves*. If an attached
brigade's flag stays false the whole branch never runs — for the perk or for the
defect. The fix is written; confirming it is what settles the question.

**Repro** An army holding Ambulance Corps III, encamped with wounded: watch
`losses[0]` converting to dead over a week against an army without the perk.
Unfixed the two are identical, while a cavalry brigade with Expert scouts in a
third army shows the reduction.
**Confirm** The Ambulance Corps army's wounded die at half the rate; the Expert
scouts brigade no longer gets it. For Foot Cavalry: forced-march sickness drops
by 10–30 % for the holder's brigades.

### GTCW-038 + GTCW-039 — Partisan Brigades and Bushwhackers are inverted

**Target** `Autocalc:UpdateRaiding` — `Autocalc.cs:2403`; `Autocalc:SufferMoraleHit`
**Technique** 4 plus technique 5.

`UpdateRaiding` has two blocks. The first (`:2354`) damages the **raided enemy**
and reads no perk at all. The second (`:2400`) inflicts return casualties and a
morale hit on one of the **raider's own** brigades — and that is where all three
multipliers sit: Partisan Brigades' skirmishing (×1.25 / 1.5 / 2) and raiding
morale (×1.10 / 1.25 / 1.50), and Bushwhackers' skirmishing
(×1.25 / 1.20 / 1.10). The config label for the morale factor reads "Raiding
morale hit on **enemy** partisan brigades", which settles what was intended.

The two blocks use *different* casualty prefs — `casualtyfactorguardingunits`
for the enemy, `casualtyfactorraidingunits` for the raider — so the casualty
half is pure technique 4:

1. the prefix computes the three factors from `raidingunits[0].topunits[0]`,
   own level and parent, exactly as the defective block does;
2. it lends `casualtyfactorguardingunits × (partisan × bushwhacker)`, so the
   **enemy** takes the damage the perks promise;
3. it lends all-ones arrays for the three perk factors, so the raider's own
   return casualties and morale hit lose the amplification they should never
   have had.

The morale half cannot go the same way, because both blocks scale
`moralelosspercasualtyraiding`. It goes through the chokepoint instead: a prefix
on `Autocalc:SufferMoraleHit` multiplies the hit by the Partisan factor **only
when the target is not on the raiding side**, which `EnvolvedUnitsIncludeReg`
answers from the method's own data. The scope flag is set by the `UpdateRaiding`
prefix and cleared in its finalizer, so the chokepoint is inert everywhere else
— including `UpdateSkirmishing`, which calls the same helper.

**Repro** A cavalry division holding Partisan Brigades III raiding an enemy
state with a guarding force present. Unfixed, the raider's own brigade takes
double casualties and a 50 % deeper morale hit, and the guarding force takes
exactly what it would against a perkless raider.
**Confirm** The guarding force's casualties double and its morale hit deepens by
half; the raider's own return casualties fall back to the perkless value.

> **Harness finding, 2026-11-04 — the guards' casualties quadruple, not
> double.** Both blocks compute `num3 *= Random.Range(num3 * f, num3 * (1 + f))`,
> which squares the casualty term, so a factor lent to `casualtyfactorguardingunits`
> scales the guards' casualties by its square. Measured over one seeded raid
> round in 001/E: Partisan III unfixed — guards ×1.00, raider's own casualties
> ×2.00 and own morale hit ×1.50; fixed — guards ×4 (2²), raider ×1.00. Whether
> the squaring is itself a defect is not settled here; the fix's direction is
> confirmed either way. `tests/GTCW.BugFixes.Tests/RaidingPerkDirectionTests.cs`.

### GTCW-041 — Embedded Reporters skips the holder

**Target** `BattleUnits:CheckForGroupPerk` — `BattleUnits.cs:5848`;
`BattleUnits:CalculateCommanderFame` — `BattleUnits.cs:5912`;
`Regiment:GetPerkLevel`
**Technique** 1 and 5.

Two defects in one line of code:

```
if (unit.unittyp >= 15 && CheckForGroupPerk(unit, 14) != null)
    num5 *= GamePrefs.fameadjustmentembeddedreporters[unit.GetPerkLevel(14)];
```

`CheckForGroupPerk` opens its walk with `GrabNextParentUnit`, so it **never
tests the unit it was asked about** — the holder's own commander is excluded
unless the holder's parent also holds the perk. And the factor is indexed by the
*subordinate's* level, which is 0 whenever the subordinate does not hold the
perk, so every Corps under a holder gets ×1.25 whatever the holder has reached.

The fix is two patches that leave the arithmetic alone:

- a **postfix on `CheckForGroupPerk`**: if the walk found nobody and the unit
  itself holds the perk, return the unit. The method has exactly one caller, so
  this changes nothing else;
- a **postfix on `GetPerkLevel`**, live only inside `CalculateCommanderFame`,
  which answers perk 14 for the unit the holder was just found for with *the
  holder's* level. C# evaluates the `&&` before the indexer, so the holder is
  always known by the time the level is asked for; the postfix additionally
  requires the instance to be that same unit, and clears itself after one use.

The fame factor scales defame as well as fame (`num5` can be negative), which is
not obviously wrong and is left as it is: the manual says "fame development",
and a commander whose reporters amplify a disaster is a defensible reading.

**Repro** An army holding Embedded Reporters III that wins a battle with no
parent above it. Unfixed its commander's fame gain is unmultiplied, while a
subordinate Corps that holds nothing gets ×1.25.
**Confirm** The holder's commander gains ×2 at level III, and a Corps under it
gains ×2 rather than ×1.25.

> **Harness finding, 2026-11-04 — the repro above does not reproduce as
> written.** At the top of the hierarchy `GrabNextParentUnit` returns the unit
> itself (its parent is the "Units" folder), so `CheckForGroupPerk`'s first step
> *does* test a top-level holder: an Army with no parent above it already gets
> ×2 unfixed, and the fix changes nothing there. The "holder is skipped" half
> needs a holder **with** a parent — a Corps holding the perk under an Army that
> does not. Measured in 001/E (13th Corps under the Army of the Gulf): Corps
> holder unfixed ×1.00, fixed ×2.00; Corps under an Army holder unfixed ×1.25,
> fixed ×2.00; top-level Army holder ×2.00 with the fix off and on. Only
> formations of type 15 and up are multiplied at all, so 001/A, which has no
> Corps under anything, shows no effect. `tests/GTCW.BugFixes.Tests/EmbeddedReportersTests.cs`.

### The rest of the family — specified, not written

Ordered by what the fix is worth against what it costs. Every one of these has
had its target read; none has had its patch written.

| Id | Target | Technique sketch | Why it is not in this wave |
| --- | --- | --- | --- |
| GTCW-034 | `Autocalc:FightUnit` ×2, `Autocalc:UpdateFatigue` | Scoped read redirection: swap ids 1 and 2 for the three auto-resolve sites | `FightUnit` is the delicate method of the whole mod — 900 lines, shared with GTCW-042/043/045. One patch, four fixes, written once and carefully |
| GTCW-042 | `Autocalc:FightUnit` `:3082`–`:3137` | Capture the four multipliers in a prefix and re-apply them to the overwritten `num5`; needs the `GetFortWeaponStrength` postfix as the hook | Same method as GTCW-034 |
| GTCW-043 | `Autocalc:FightUnit` `:3137` | Scoped redirection of `HasPerk(10)` to 17 at that one site | Same method |
| GTCW-045 | `Autocalc:FightUnit` `:3112` | Reaching types 1 and 6 means calling the factor from `UpdateRaiding` and `UpdateSkirmishing` instead — closer to GTCW-044 than to a one-site repair | Same method, and overlaps GTCW-044 |
| GTCW-044 | `Autocalc:UpdateSkirmishing` | The two vestigial locals mark the spot; lend `withdrawal_skirmishingcasualties` and route the morale half through the `SufferMoraleHit` chokepoint GTCW-038 already builds | Needs the two raiding sides' roles in `UpdateSkirmishing` pinned down first — which unit is the rear guard is not yet established |
| GTCW-052 | `SpottedEnemy:UpdateMarks` `:261` | Lend the reciprocal, exactly as GTCW-003 does for the Confiscation Act | Low reach: the base term saturates within about eight game hours (GTCW-051), so the operator is wrong but nearly inert. Worth doing with GTCW-051, not before |
| GTCW-055 + GTCW-059 | `IIP:UpdateBlockades` `:671`, `:676` | Lend each blockading fleet's `blockadeefficencyvalue` the holder's factor, so the discount reaches the whole sum instead of the part accumulated so far; `orgrunningtradewar` the same way | One patch, two fixes; GTCW-059 is not a perk and wants its own confirm |
| GTCW-056 | `Autocalc:UpdateNavalBattle` `:1643` | Move the experience grant behind the same test the effect uses — the *target's* `fleetorders`, which is already in hand at that site | Levelling only; grouped with the other naval perk work |
| GTCW-031 | `Regiment:UpdateMorale` `:18170` | Walk `receivedfire` in the prefix, compute the mean distance the term *should* divide by, and lend `moraleleveragedistancearifire` the ratio. Exact, because every later factor is multiplicative | It belongs in the `UpdateMorale` group this wave already patches, and is the next thing added there |
| GTCW-026 | `Regiment:CalculateCohesion` `:19554` | Apply the factor only when cohesion is below its target. Needs the target recomputed in a prefix, or the rate lent asymmetrically | Changes cohesion behaviour in every battle; wants its own confirm pass |
| GTCW-029, GTCW-030 | `Regiment:FireBullet`, `Regiment:UpdateFiringAndReloading` | Lend the factor arrays per call, gated on the condition the manual states (skirmish order, canister, cover) | GTCW-030 is a **nerf** of three perks. Correct, and worth landing with its own release note rather than inside a wave |
| GTCW-032 | `ObstacleLine:Build`, `Pontoons:Build` | Reset the multiplier per unit in a prefix on the per-unit step | Battle-scene engineering only; small reach |
| GTCW-033 | six sites | Each is a different test; no shared technique | Levelling rates, the least visible class of perk defect. Recorded as one row so the six are not re-derived |
| GTCW-037 | `RegimentMove` `:22207` | Make the factor conditional on `doublequick` and on `unittyp != 17`, by lending the array | Waits on GTCW-035's confirm, which settles whether `doublequick` is ever true for an attached brigade |
| GTCW-040 | `Regiment:UpdateRaids` | Same method as GTCW-014 and GTCW-015; do all three at once | Grouping |
| GTCW-046 | `CBuilding:UpdateRaidSmoke` `:419` | Skip the re-arm while the building is still inside its construction timer | The audit's open question first: watch one fort finish |
| GTCW-047, GTCW-048 | `Regiment:UpdateIIPSupplyLink` `:5044`, `RegimentMove` | GTCW-047 must separate resupply from consumption and upkeep inside one elapsed-time term — the only row in the family that may need a transpiler. GTCW-048 adds a speed factor on creek terrain, mirroring River Expedition | GTCW-047 is the hardest fix in the family; GTCW-048 **adds** an effect and is a design call |
| GTCW-049 | `Autocalc.EnvolvedUnits:UpdateSiegeStrength` `:407` | Postfix rescale so a lone holder's relative gain matches the label | The averaging over `topunits.Count` may well be deliberate, and "correcting" it doubles every siege bonus. Needs a decision, not a patch |
| GTCW-054 | `GamePrefs` | Post-parse override of three floats to 1.05 / 1.10 / 1.15 | The first class D row, and the one that most clearly belongs to a data mod. Default off, and it needs the config-override surface Phase 4 owes |
| GTCW-057 | `FogOfWar:UnitIsHidden`, `Regiment:CheckAbilityToRun`, `Regiment:UpdateBlockades` | Scoped redirection: answer `false` for a perk id read outside its own namespace | Five sites, all of them *removing* an unearned benefit. A nerf of five perks that nobody has complained about; last in the family |
| GTCW-058 | `AICampaign:ChoosePerk` `:2856` | Lend the exclusive bound, and refuse the fourth slot | Changes what the AI holds in every campaign. Real, and the single largest balance change in the family |

### Three that are design calls, not repairs

These three are recorded, and deliberately have no patch planned. Each would
*add* a mechanic the game does not have, which is a different mod from this one.

- **GTCW-023 — the melee perks.** Spirited charge, Cold steel and Close support
  all multiply `meleestrength`, and melee casualties never read it
  ([12](../../code-findings/12-melee.md) §3). Making the perks work means
  deciding what melee strength should scale — casualties, the cohesion
  comparison, or the morale term — and no two of those choices give the same
  game. Until that is settled, the honest statement is that three perks are
  display-only.
- **GTCW-051 — Field Telegraph's information delay.** The multiplier is applied
  to the right value with the right operator; the value itself saturates at its
  clamp within hours, so the perk's second half is worth nothing whatever we do
  to it. Fixing *that* means changing the mark-accumulation term, which is not
  a perk defect.
- **GTCW-053 — Sea Raiders' import prices.** `priceadjustment` has one reader
  and it is a tooltip. A fix would have to find the import price path and apply
  the factor there, inventing the mechanic the manual describes. That is a
  feature, and it would move money.

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

### The harness runs this protocol

[`tests/`](../../tests/README.md) automates every rule above, in three tiers:
the game's code against stubbed Unity with no engine, a windowless campaign,
and a windowless battle. Each fix test runs one scenario off, on, and off again
with the patch still applied (`Verify.Fix`), with only that fix switched; the
harness also checks that every fix in `FixCatalogue` has a test, that the mod
with every fix Off patches nothing (in the real game as well as outside it),
and that a fresh config enables exactly the default-on fixes.

**What a pass means for the status ladder.** A fix whose off/on/off test passes
in the campaign or battle tier — the real game, the real plugin, the real
scheduler — is `confirmed (headless)`. That stands in for "seen working in
game" for the simulation. It does not look at the screen, so a fix whose effect
is a number on a panel still gets a look in the real UI before release. A fix
tested only in the engine tier stays `fixed`: that tier proves the arithmetic
against stubbed Unity, not the game.

**Prove a test can fail.** `--sabotage GTCW-NNN` keeps one fix off whatever the
tests ask; every test of that fix must then fail, and no other. Do this when
adding a test, and watch for a measurement of zero, which makes "unchanged" and
"scaled" the same claim.

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
