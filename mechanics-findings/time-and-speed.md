# Time and speed

What a game day costs in real minutes, what the speed buttons actually change,
what pausing stops, and which parts of the war are measured by the clock rather
than by your frame rate.

**Translates:** [`10-simulation-tick`](../code-findings/10-simulation-tick.md),
with the per-subsystem intervals from
[`20-ai-campaign`](../code-findings/20-ai-campaign.md) §2,
[`21-ai-battle`](../code-findings/21-ai-battle.md) §1.4 and
[`25-newspaper-and-events`](../code-findings/25-newspaper-and-events.md) §2.4.
**Summary:** [summary-simulation](../code-findings/summary-simulation.md).
Conventions, including the Derived / Observed / Untested tags, are in
[`README.md`](README.md).

---

## 1. How long a day takes

One number sets everything: the real-world length of half a game day, divided
by your game speed. The campaign and the battlefield use **different values of
that number**, from two different config files, and nothing in the interface
says so ([10](../code-findings/10-simulation-tick.md) §5).

| | Battle | Campaign |
| --- | --- | --- |
| Half-day length | 120 real minutes | 1.25 real minutes |
| Speed ladder | 1, 2, 5, 20 | 1, 5, 20, 50 |
| One game day at speed 1 | **4 hours** | **2 min 30 s** |
| …at the second step | 2 hours | 30 s |
| …at the third step | 48 min | 7.5 s |
| …at the top of the ladder | **12 min** | **3.0 s** |

So the whole four-year war, played start to finish at campaign speed 1, is
about **61 hours of wall-clock time**; at speed 50 it is **73 minutes**. One
game month at speed 20 is just under four minutes.

A battle is shorter than its day suggests, because you only fight in daylight
and daylight is not twelve hours ([10](../code-findings/10-simulation-tick.md)
§8):

| Month | Daylight | Real time at battle speed 1 | …at speed 20 |
| --- | --- | --- | --- |
| January, February, December | 9 h (8–17) | 90 min | 4 min 30 s |
| March, April, November | 11 h (7–18) | 110 min | 5 min 30 s |
| May, September | 13 h | 130 min | 6 min 30 s |
| June, August | 15 h | 150 min | 7 min 30 s |
| **July** | **16 h (5–21)** | **160 min** | **8 min** |

**A July battle gives you nearly twice the fighting day of a January one** —
seven more game hours to finish an attack in, and over an hour more of real
time at 1× in which to spend them. This is the one seasonal effect the game
never mentions and the largest single lever on whether a multi-day battle gets
a second day.

### The campaign starts paused, and dawn resets your speed

A new or loaded campaign comes up paused with the speed set to zero
([10](../code-findings/10-simulation-tick.md) §11), so the first thing you do
in every session is pick a speed. In a battle, the overnight sequence calls
"speed 1" and then "pause" in consecutive lines
([10](../code-findings/10-simulation-tick.md) §8), so **the morning of day two
always begins at 1× regardless of what you were running the previous
afternoon.** If a multi-day battle suddenly feels slow, that is why.

### The night costs five to nine seconds

When the end-of-day cycle reaches the point where both sides have eaten,
resupplied and had their condition adjusted, the game sets speed **1000** —
bypassing the normal ceiling, the only place in the game that does
([10](../code-findings/10-simulation-tick.md) §8). At that rate a whole game
day is 14.4 real seconds, so the dark hours pass in **4.8 s in July and 9.0 s
in January**. Nothing you can do happens during it; the useful consequence is
simply that a second battle day costs you no real time to reach.

---

## 2. What the speed buttons actually do

The four buttons on the timer panel set the four ladder values, and the
keyboard shortcuts 1–4 do the same. But the **increase-speed and decrease-speed
keys step by exactly 1.0**, not along the ladder
([10](../code-findings/10-simulation-tick.md) §11). In the campaign that makes
speeds 2, 3, 4, 6, 7 … 49 all reachable, and the panel highlights a button only
when the speed matches a ladder entry — so **at speed 2 no speed button is lit
and the interface is showing you nothing about how fast the game is running.**

Two more things change the speed without saying so:

- **Holding Left Ctrl with a unit selected pauses the game** and releasing it
  resumes at the same speed. It exists so you can lay a path on a still map.
- **Pressing decrease-speed at 1× pauses**, because it computes 1 − 1 = 0 and
  zero is the pause value. Your previous speed is kept, so the next unpause
  returns to 1×.

Opening a panel that pauses the game and closing it restores the speed you were
at — unless you have *"remains paused when panel closed"* on in the options, in
which case the stored speed is overwritten with zero and lost.

> **Untested:** that last sentence is read from the code path, not played. With
> the option on, the pause is recorded as a deliberate "set speed to 0" rather
> than a temporary one, so the resume call has nothing to resume to. The test
> is to set a speed, open a pausing panel, close it, and press the pause key
> twice; if the game stays still, the stored speed is gone.

---

## 3. Playing fast does not change the war — it samples it less often

This is the single most useful thing to know about game speed, and it is more
reassuring than it sounds.

Real frame time enters the game in exactly **one** place: the clock inside
UniStorm, the third-party weather asset the game uses to keep the date
([10](../code-findings/10-simulation-tick.md) §1). Not one simulation class
reads it. Every rate in the game — march speed, supply consumption, morale
recovery, cohesion, fatigue, attrition, construction, research — is computed as
a **difference of two game times**, so the amount that happens per game day is
the same whether your machine renders 20 frames a second or 200.

> **A slow machine does not fight a slower war. It looks at the same war less
> often.** ([10](../code-findings/10-simulation-tick.md) §1)

Four things, however, are counted in **frames**, and those do change with
speed, because a frame covers 50× more game time at speed 50 than at speed 1.

### 3.1 The AI gets less thinking time the faster you play

The campaign AI runs `floor(sqrt(speed))` passes per frame: one at speed 1, two
at speed 5, four at speed 20, seven at speed 50
([20](../code-findings/20-ai-campaign.md) §2). Game time per frame grows
**linearly** with speed while AI throughput grows as its **square root**, so
the AI's thinking per game day falls as roughly `sqrt(speed)` — **about seven
times less at speed 50 than at speed 1.**

In a battle the same asymmetry splits the AI in half
([21](../code-findings/21-ai-battle.md) §1.4): the army-level AI is on the
clock and is unaffected, while the unit-level AI is on frames and degrades.
Fast-forwarding a battle produces an enemy that still has a sensible plan and
executes it clumsily.

> **A fast-forwarded campaign feels passive because it is.** If you want a
> fight, run the approach at speed 20 or 50 and drop to 5 when the armies are
> close.

### 3.2 Your units spot each other later

Each campaign unit scans `ceil(sqrt(speed))` other units per frame for contact
([10](../code-findings/10-simulation-tick.md) §10). Same shape, same
consequence: the time, in game hours, for a unit to notice an enemy in range
grows as `speed / ceil(sqrt(speed))` —

| Campaign speed | Detection latency, relative |
| --- | --- |
| 1 | 1× |
| 5 | 1.7× |
| 20 | 4× |
| **50** | **6.25×** |

At speed 50 a unit takes six times longer, in game time, to register an enemy
it could already see. That is how an army walks past a raiding brigade, and why
a screen does its job much better at moderate speed.

### 3.3 The unit you have selected is simulated better than the rest

The cycle that reprocesses orders, flanking, unit status and visuals runs every
25 frames in the campaign — **except for the selected unit, whose cycle is
divided by the game speed** ([10](../code-findings/10-simulation-tick.md) §10).
Nobody else gets that. So at speed 50, the unit you are watching has its orders
handled on a corrected cadence; every other unit stays on the flat 25-frame
cadence, which at speed 50 covers fifty times more game time per pass than it
does at speed 1.

The slower cycle — 65 frames, holding fatigue, cohesion, supply lines, strength
calculations, raids, blockades, fighting spirit and readiness actions — gets no
correction at all:

| Campaign speed | Slow-cycle passes per game day (at 60 fps) |
| --- | --- |
| 1 | ~139 |
| 5 | ~28 |
| 20 | ~7 |
| **50** | **~2.8** |

Where the work behind that gate integrates elapsed game time — fatigue,
cohesion, morale, movement, firing — the **total is preserved** and only the
granularity coarsens ([10](../code-findings/10-simulation-tick.md) §10). Where
it tests a condition — "has this unit lost its orders?", "should readiness
actions fire?", "is this port blockaded?" — the test simply happens less often,
so events land later.

### 3.4 The late-war newspapers can go missing

The historical-event catalogue is checked **one record per frame**, 119
records, and the cursor resets every game day
([25](../code-findings/25-newspaper-and-events.md) §2.4). A campaign game day
at speed 50 is 3.0 real seconds, which is `3 × fps` frames — so the sweep needs
about **40 frames a second** to reach the last record.

At the game's own 60 fps cap it clears with a 1.5× margin. Below 40 fps it does
not, and because the file is roughly date-ordered, what sits past the reachable
point is **the whole late war**: Grant's Overland campaign, the fall of
Atlanta, Lincoln's re-election, Sherman's march, Appomattox.

> **Untested, and worth knowing anyway:** the arithmetic is Derived; nobody has
> watched it happen. But the conditions line up badly — maximum game speed is
> what players use for 1864–65, and a late-war map with two full armies on it
> is when the frame rate is lowest. If your late-war papers stop arriving, drop
> to speed 20. The test that settles it is an 1865 start at speed 50 with the
> frame rate capped under 40.

Note that the game caps itself at 60 fps, but Unity ignores that cap whenever
VSync is on — so on a 144 Hz monitor with VSync enabled, every frame-paced
subsystem above runs 2.4× faster than this file assumes
([10](../code-findings/10-simulation-tick.md) §1). **Turning VSync on is the
cheapest way to buy back everything in §3.**

---

## 4. What pausing stops

Pausing stops **the clock**, not the game
([10](../code-findings/10-simulation-tick.md) §7). The distinction is worth
money.

**Stopped:** the date and time; both AIs; commander histories; fleet updates;
historical events and the newspaper queue; campaign enemy-contact checks; unit
animations. Everything gated on elapsed game time — the macro-economy, policy
research, attrition, construction, autosave — cannot fire, because no game time
elapses.

**Still running, every frame:** the workforce and support filter maps, the
economy flow lines, transport capacity, the frontline and railroad-usage
accumulators, camp updates, the AI's defensive-move and fort-construction order
queues, all three diary entry points, and retreat-order issuing.

> **A paused campaign keeps settling.** The derived figures in your panels —
> transport capacity, the labour map, supply flow lines — are produced by
> rolling sweeps that do not care about the clock, so they keep converging
> while you sit still. If a number looks wrong right after a big change, pause
> and give it a few seconds before acting on it.

Two quirks in the same code:

- **Holding the right mouse button suspends retreat-order processing**, in both
  scenes. Harmless, but it means a retreat you ordered does not begin to be
  worked down until you let go.
- Retreat orders *are* issued while paused — the waypoints get set. The units
  cannot move until the clock runs, so a pause is a safe place to sort out a
  collapse.

---

## 5. Which numbers are on the clock

If you want to know how often something is recalculated, this is the list
([10](../code-findings/10-simulation-tick.md) §10). Clock-paced work has the
same cadence in game time at every speed; this is what the simulation is
actually made of.

| How often, in game time | What |
| --- | --- |
| **1.8 game seconds** | every unit's firing and reloading sweep |
| **36 game seconds** | battle cease-fire check; the left-guns list the AI reads |
| **54 game seconds** | battle side statistics and army-level AI behaviour; victory-condition group totals |
| 79 / 90 game seconds | battle commander rankings / unit rankings |
| 3 game minutes | battle weather check |
| **10.5 game minutes** | population, workforce, macro-economy, **chapter advance and every campaign objective check**, railroad construction, ship imports |
| **21 game minutes** | one round of every auto-resolved battle, land and naval |
| 1.75 game hours | the top toolbar's figures and the transport-capacity tooltip |
| 12 game hours | alliance data |
| 17.5 game hours | fleets; the availability of importable ship types |
| 1.8 game days | resource tax data |
| **a month boundary** | the Treasury's economy report, the Secretary of War's intelligence report, and every "last month" figure in them |

Two of those rows are worth pausing on.

**The 10.5-minute row is the campaign's heartbeat.** The chapter advance and
every objective check ride it, which is what makes the 1864 election resolve
inside a single ten-minute tick rather than over a day ([the manual against the
code](manual-vs-code.md) §32). It is also why a policy that completes does not
visibly take effect for a few seconds of game time.

**The month boundary is detected by the day counter going backwards** — 31 → 1
([10](../code-findings/10-simulation-tick.md) §9). It is the only monthly
mechanism in the game, and the "last month" numbers in both reports are
snapshotted at that instant, after the reports are queued. So the report you
open describes the month that just ended, which is what you would want, but it
means any mid-month figure you compare it against is measuring something else.

---

## 6. For modders

- **`battledaylengthinmins` exists twice.** It is in both `battleprefs.txt`
  (120) and `campaignprefs.txt` (1.25) under the identical label *"Day Length
  in Realtime Minutes (calculated out of a day of 6-18h)"*. Changing the one
  you found first will change only one scene. Both files are parsed **by
  position**, so edit the value line and never add or remove a line
  ([`ui-vs-simulation`](ui-vs-simulation.md) §5).
- **`maxgamespeed` is dead.** Both files expose *"Maximum Gamespeed 10"* and
  nothing in the assembly reads it. The real ceilings are hardcoded arrays —
  `{1, 2, 5, 20}` in battle, `{1, 5, 20, 50}` in the campaign — so raising the
  cap is a code change ([10](../code-findings/10-simulation-tick.md) §5).
- **Changing the ladder arrays does not change the reachable speeds.** The +/−
  keys step by 1.0 from wherever you are, clamped to the array tail, so every
  integer up to the tail stays reachable either way
  ([10](../code-findings/10-simulation-tick.md) §11).
- **`updatetimeforcampaigndata` is the one knob that moves everything.**
  Lowering it from 0.00002 makes the economy, chapter checks and objective
  checks finer-grained; raising it makes the whole campaign chunkier. It is the
  highest-leverage single value in `campaignprefs.txt`.
- **Copy the delta pattern, not the frame pattern.** The game's own good
  examples take the elapsed game time as an argument and scale by it —
  `Economy.UpdateEconomyEstimates(num2)`, and the firing sweep's `ceil(checks ×
  Δ / interval)`. Anything a mod adds on a bare frame gate will starve at speed
  50 and over-run at 144 Hz with VSync on.
- **One shipped integration is only first-order.** Cohesion recovery is an
  explicit Euler step, `cohesion += (target − cohesion) × rate × Δ`, which
  overshoots once `rate × Δ > 1`. At the shipped rate of 2 per game hour it is
  nowhere near that, but a mod raising the rate much past 40, or raising the
  battle speed ceiling, makes the step size the governing term rather than the
  rate ([10](../code-findings/10-simulation-tick.md) §10).

---

## 7. Still open

- **Whether a long frame loses game time.** The clock advances by Unity's
  `Time.deltaTime`, which Unity clamps at `maximumDeltaTime` — 0.333 s by
  default. At campaign speed 50 that is 11 % of a game day per hitch. Whether
  the project overrides the default is inside `globalgamemanagers` and needs an
  asset dump to read. **Untested.**
- **Whether any other rolling sweep misses its window** the way the
  historical-event cursor can. The IIP, town and building data cycles run on
  40-, 200-, 150- and 175-frame periods and have not been checked against a
  reset window.
- **What the slow cycle's condition tests cost in practice.** The totals behind
  fatigue, cohesion and morale are preserved at any speed; whether a readiness
  action or a missing-orders check that fires two game days late changes an
  outcome has not been measured. The test is a controlled march at speed 1
  against the same march at speed 50.
