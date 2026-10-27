# Trade warfare

**Covers:** `GameVars.Alliance.AddTradeWarfare`, `tradewarfare[]`,
`tradewarfarelast[]`, `IIP.lossestradewarfare`, `CBuilding.lossestradewarfare`,
`Regiment.UpdateRaids`, `CampaignStrategy`'s commerce-war panel
**Build:** see [00-build-provenance](00-build-provenance.md)

Opened to settle one question for the hook library: what the `tradewarfare`
categories mean, so a modifier could be offered over them. The answer makes the
modifier pointless and turns up a defect instead.

## In one paragraph

**`tradewarfare[]` is a ledger, not a mechanic.** Nothing in the simulation
reads it — the only consumers are the commerce-war panel, a trailing average of
itself, and the save file. The economic damage it records is applied at the
source and would happen identically if the array did not exist. The game names
its own categories in UI code: **1 blockades and privateering, 2 raids,
3 repairs**, with **0 a recomputed total** and 4–9 unused. Values are stored
**negative** and negated for display. And category 2 is fed by two writers with
**opposite signs**, so cavalry raiding an enemy trade node makes the victim's
displayed raid losses go *down*.

## 1. The categories, named by the game

No guesswork required: `CampaignStrategy` builds the panel text from the array
and labels each slot.

| Slot | Label in the game's own UI | Writer |
| --- | --- | --- |
| 0 | "total costs" | recomputed as the sum of 1–9 on every write |
| 1 | "…through blockades & privateering" | `IIP` (a trade node) |
| 2 | "…through raids" | `CBuilding`, **and** `Regiment.UpdateRaids` |
| 3 | "…through repairs" | `CBuilding` |
| 4–9 | *(none)* | none — the array is `float[10]` |

So the usable category space is three, not ten, and the six spare slots have no
label, no writer and no reader.

## 2. Negative means cost

Every display site negates: `"$ " + Tools.GetAbrevStrValue(0f - tradewarfare[n])`.
So a cost is stored as a negative number and shown as a positive dollar figure.
Two of the three writers honour this:

```csharp
// IIP, category 1 — lost tariff revenue on blocked trade
float num13 = (num10 - num9) * price[l] * tax[tarifs] * taxcaps[tarifs] * taxrevenuemodifier[tarifs];
// num10 is the throttled volume and num9 the potential, so the term is negative

// CBuilding, category 2 — lost corporate tax from a damaged works
float num6 = … * (0f - (1f - Condition)) * …;   // explicitly negated

// CBuilding, category 3 — resources burned on repairs
AddTradeWarfare(3, 0f - num3);                   // explicitly negated
```

## 3. The defect: category 2 is fed with both signs

`Regiment.UpdateRaids` — the cavalry raiding mechanic — writes the same
category **unnegated**:

```csharp
float num = GameVars.GetCampaignFloatingDate() - lastraidupdate;  // elapsed, > 0
…
num2 /= Mathf.Max(1, list.Count);
if (num2 < 0.1f) num2 = 0.1f;                                      // clamped, >= 0.1
…
float resourcesCost = Economy.GetResourcesCost(…);                 // sum of price x amount, > 0
GameVars.alliance[list[l].allianceowner].AddTradeWarfare(2, resourcesCost * num * num2);
```

All three factors are strictly positive, and `GetResourcesCost` sums
`GetLocalPrice(…) * resourceamount` with no negation anywhere. So this adds a
**positive** number to a slot whose convention is negative.

> **Consequences.** A raid on an enemy trade node *reduces* that alliance's
> "losses through raids" figure, and with enough raiding drives it past zero so
> the panel reports a negative cost. Because slot 0 is recomputed as the sum of
> the rest, it corrupts the **total** as well — and therefore the
> trade-warfare slider, which is a ratio of the two sides' totals
> (`CampaignStrategy` ~562):
> ```csharp
> TradeWarfareSlider.value = 1f - Mathf.Min(1f,
>     (0f - alliance[1].tradewarfare[0]) /
>     Mathf.Max(0.0001f, 0f - alliance[0].tradewarfare[0] - alliance[1].tradewarfare[0]));
> ```
> So the headline "who is winning the commerce war" bar is fed partly by a
> term with the wrong sign.

The attribution is right, for what it is worth: the charge goes to
`list[l].allianceowner`, the owner of the raided node, so the victim bears it.
Only the sign is wrong.

**A second, smaller gap in the same method.** `IIP` and `CBuilding` both
mirror their contribution into a per-object `lossestradewarfare` field, which
is what the map icon tooltips read ("Cumulated losses in tax revenues through
blockades and raids"). `Regiment.UpdateRaids` updates the alliance ledger but
**not** the node's own `lossestradewarfare`, so a raided trade node's tooltip
omits regiment raid losses entirely.

## 4. Nothing in the simulation reads it

The complete reader set for `tradewarfare[]`:

| Reader | What for |
| --- | --- |
| `CampaignStrategy` ~480, ~502–505, ~562 | the commerce-war panel text and slider |
| `BattleUnits` ~3851–3853 | a trailing average into `tradewarfarelast[]`, which only the same panel reads |
| `ImportExportUnitData` ~932–935, ~1383 | save and load |

There is **no gameplay consumer at all**. The damage is applied where it is
computed — the IIP's tariff revenue is already reduced, the building's corporate
tax is already lower, repairs already consume resources via `Economy.Consume`,
and `Regiment.UpdateRaids` already calls `Economy.AdjustStockIIP` to take the
goods. `tradewarfare[]` records what happened; it causes nothing.

## 5. What that means for the hook library

**Do not offer a trade-warfare modifier.** Writing to it would change the
player's own accounting of a war they are fighting, without changing the war —
a mod that made the commerce-war panel lie. The mechanism is sound and would
have been trivial to wire into the contribution model, which is exactly why
this was worth checking first.

This **downgrades `AddTradeWarfare` as a mod seam.**
[summary-game-state](summary-game-state.md) ranks it alongside
`Nation.AdjustSupport` and `Alliance.AdjustNationalMorale` as a single-chokepoint
write target. The three are structurally alike, but the other two move
simulation state and this one moves a display.

The useful seams for anyone actually wanting to change commerce warfare are
upstream, where the damage is computed: `IIP`'s blocked-volume term,
`CBuilding`'s condition term, `GamePrefs.raidvolume`, and
`groupraidingefficiency` against `groupguardefficiency` in
`Regiment.UpdateRaids`.

## 6. Also still true

`AddTradeWarfare` has **no guard and no clamp**. It opens with two orphaned
statements where a magnitude check used to be:

```csharp
public void AddTradeWarfare(int type, float value)
{
    Mathf.Abs(value);
    _ = 1000000f;
    tradewarfare[type] += value;
    …
}
```

Already recorded in [summary-game-state](summary-game-state.md); repeated here
because it is the reason a wrong-signed caller goes unnoticed.

## Open questions

- Whether the sign error is reachable in a way a player would notice, which
  depends on how much raiding a campaign sees relative to building damage.
  Measurable in-game now that the library can read the array.
- What slots 4–9 were for. Nothing names them, and the save format writes all
  ten, so they are reserved rather than removed.
