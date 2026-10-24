# Events and the press

What the game tells you, in its own voice: the newspaper that stops the war to
print a front page, and the dispatch that a subordinate sends when something
goes wrong. Both are information channels, and both are **unreliable in ways
you can predict** — so this file is as much about what they do not tell you as
what they do.

Translated from
[25-newspaper-and-events](../code-findings/25-newspaper-and-events.md) and
[27-dispatches-and-the-message-log](../code-findings/27-dispatches-and-the-message-log.md),
with the monthly intelligence report's broken paragraph from
[17](../code-findings/17-politics-and-support.md) §4d.

---

## 1. The short version

- **The newspaper pauses the game. Every time.** Showing a front page sets the
  speed to zero; closing it restores the speed. One paper at a time, with a
  third of a second between papers.
- **There are 119 events in the catalogue and your start date decides how many
  you will ever see** — 94 of the 105 dated ones from Spring 1861, 30 from
  Spring 1864. Eleven can never fire in any shipped start.
- **Fifteen policies unlock events.** Indian Wars alone unlocks six. If you
  never research them, those papers never print — and the gate asks whether
  *anyone* has researched the policy, so the Confederate AI adopting King
  Cotton gives a Union player the King Cotton paper.
- **At campaign speed 50 on a machine under about 40 fps, the late-war papers
  go missing.** Not delayed: never tested. Atlanta, the 1864 election,
  Sherman's march and Appomattox all sit in the part of the file that does not
  get read.
- **Turning newspapers off in the options throws them away**, it does not defer
  them — including the end-of-campaign paper.
- **Every battle newspaper says the same thing about morale**, because the two
  thresholds it compares against are a hundred times too large.
- **The monthly intelligence report is the best deal in the game**: free,
  unconditional, and it names the enemy's finished policies, the policies it is
  still debating, every corps it raised and where that corps is meant to
  operate, its army's exact morale percentage, and the most aggressive thing it
  did last month — including a naval invasion while the invasion force is still
  assembling.
- **The dispatch log only ever shows ten game days**, and it destroys the rest
  the moment you open it. At speed 50 that is thirty seconds of real time.
- **Your army can starve without a warning dispatch**, because the supply
  warning only looks at rifle ammunition and forage.
- **The Quartermaster's Morning Report always reports morale as "intact."**

---

## 2. The newspaper is a modal interrupt

A front page is not a notification. `Show` puts a paper in a queue; once a
frame the game checks whether it can print one, and when it does it **sets the
game speed to zero** and waits for you. Closing it puts the speed back.

Four conditions have to hold before a queued paper prints: nothing else is
showing, the newspaper panel is closed, **20 frames** have passed since the
last one closed, and **the game is not already paused**. That last one has
exactly one exception — the battle report, which prints through a pause.

Three consequences you can act on:

- **If you pause, the queue stops.** Papers do not accumulate on screen while
  you plan; they wait, invisibly, and arrive in a burst when you unpause.
- **If you save and quit with a paper still queued, it is gone.** The queue is
  not written to the save.
- **Closing a dispatch while a paper is on screen discards the paper unread.**
  The dispatch panel's close handler calls the newspaper's close handler
  unconditionally.

The issue number on the masthead (*No. 37 — Vol. IV*) restarts at 1 every time
you load a save. The volume number is correct, because it is computed from the
scenario's start year.

Source: [25](../code-findings/25-newspaper-and-events.md) §1, §1.2, §1.6, §6.

### 2.1 The off switch is a delete switch

Options has a newspaper checkbox, and it reads like a debug flag in the code
but is not — it is a real, persisted player setting. **It is checked before the
paper is queued**, so with it on, papers are discarded rather than deferred.

That includes the paper the end of the campaign waits for: objectives 0 and 1
hold back the victory screen until the newspaper queue drains. With newspapers
switched off the queue is always empty, so nothing is held back — you go
straight to the end screen and never read why.

Source: [25](../code-findings/25-newspaper-and-events.md) §1.1, §1.5.

---

## 3. What is in the catalogue, and how much of it you will see

The event file holds **119 records**. Fourteen are script-only, fired by
something happening rather than by a date. The other **105 are dated**, and a
date already past when your campaign begins is simply never reached.

| Start | Date | Dated papers you can still get | Lost to the start date |
| --- | --- | --- | --- |
| Spring 1861 | 23 Feb 1861 | **94** of 105 | 11 |
| Summer 1861 | 8 Jul 1861 | 77 | 28 |
| Spring 1862 | 21 Mar 1862 | 65 | 40 |
| Spring 1863 | 10 May 1863 | 47 | 58 |
| Spring 1864 | 1 May 1864 | 30 | 75 |
| Tutorial | 15 Jul 1861 | 76 | 29 |

So **a Spring 1864 campaign is missing three quarters of the written history**.
This is not a bug — the events are dated, the dates have passed — but it is
worth knowing before you conclude that a late start "has less flavour".

**Eleven of the 105 can never fire in any shipped campaign.** Ten of them are a
complete, fully illustrated secession-winter chain — South Carolina's
convention on 24 December 1860, the first Fort Sumter reports, Mississippi's
state house, the Montgomery delegates, Fort Pickens holding in Florida,
Georgia's convention at Milledgeville, Louisiana's 113-to-17 vote, Texas as the
seventh state to secede, the founding of the Confederacy on 9 February 1861 —
written for a start date earlier than any that shipped. The eleventh, Jefferson
Davis as President of the Confederate States, is dated **29 March 1860**, which
reads as a typo for 1861.

Source: [25](../code-findings/25-newspaper-and-events.md) §2.6, §2.9.

### 3.1 Fifteen policies are also event unlocks

A dated event can carry a policy gate, and if that policy is never completed
the paper never prints even when its date arrives. Fifteen policies do this:

| Policy | Papers it unlocks |
| --- | --- |
| **Indian Wars** (Union) | **six** — the whole frontier arc: the Dakota attacks in Minnesota, the mass hanging at Mankato, Fort Snelling, the western frontier tensions, the Hungate murders, and Sand Creek |
| **Alliance with the Natives** (CSA) | two — the Choctaw and Chickasaw alliance, and Fort Arbuckle |
| Enrollment Act I (Union) | two — the New York draft riots and the Gold Room panic |
| Industrialization (Union) | *The American River Ganges* |
| Industrialization II (Union) | the engineering-feats paper |
| Confiscation Act (Union) | Union forces barred from returning anyone to slavery |
| US Colored Troops (Union) | Fort Pillow |
| Emancipation Proclamation (Union) | the Freedmen's Bureau |
| Militia Act I (Union) | the secession-convention uproar |
| Kansas a Free State (Union) | the Kansas vote |
| King Cotton (CSA) | cotton smuggled from the South |
| Letters of Marque (CSA) | the *Chesapeake* pirate at Halifax |
| Support Mexican Intervention (CSA) | the French ironclad *Normandie* |
| Filibustering (CSA) | Cuban preparations to support the Confederacy |
| The Old Dominion (CSA) | *"Could We See C.S. Capital in Virginia?"* |

Two things make this more interesting than a checklist.

**The gate does not care whose policy it is.** It asks only whether the policy
has been finished by anybody. Because the two sides' catalogues mirror each
other, that comes out as "has the owning side adopted it" — so as a Union
player you see the Confederate-flavoured papers when the Confederate AI
researches the matching policy. Those papers are a free, reliable readout of
what the enemy has adopted, arriving before the monthly intelligence report
would tell you.

**Indian Wars is the single best value in the catalogue** at six papers, and it
is also the policy that hands **both** sides +5 military experience
([policy-order](policy-order.md) §5.1) — so researching it for the narrative
also trains your opponent.

Source: [25](../code-findings/25-newspaper-and-events.md) §2.5, §2.7.

### 3.2 The fourteen papers that arrive by event, not by date

| Trigger | Paper |
| --- | --- |
| Each chapter change | *The Election of 1860* → *The Demon of War has Awoken!* → *A War so Terrible!* → *Civil Wars leave nothing but tombs.* |
| France joins the Confederacy | the ironclad *Normandie* |
| Britain joins the Confederacy | the British intervention force mobilised |
| Spain joins the **Union** | General Juan Prim of Spain |
| France withdraws | *Napoleon III Skedaddles* |
| Britain withdraws | *Royal Navy in Retreat* |
| Spain withdraws | General Prim's army defeated |
| AI recruitment crisis | *Union / C.S.A. Recruitment in Crisis* |
| AI economy crisis | *Union / C.S.A. Economy in Crisis* |

The last four are the ones to watch for: they are the **enemy AI announcing its
own emergency**. When *C.S.A. Recruitment in Crisis* prints, the Confederate AI
has switched to its emergency recruiting doctrine, which cancels the research it
had running ([fighting-the-ai](fighting-the-ai.md) §7.3). You are being told,
in plain English, that the enemy has just thrown away a policy.

The withdrawal papers are the only notice you get that an intervention has
ended, and they require **every** one of that power's expeditionary units to
have pulled out — three for France, four for Britain, two for Spain.

Source: [25](../code-findings/25-newspaper-and-events.md) §2.8.

---

## 4. The late-war papers can go missing, and it is your frame rate

This one is worth its own section because it is not a design decision.

The game does not look for today's events in one pass. It checks **one record
per frame** and moves a cursor along, resetting the cursor when the in-game
date changes. So to test all 119 records on a given day, it needs 119 unpaused
frames inside that day.

A campaign game day takes **2.5 real minutes divided by the game speed**
([time-and-speed](time-and-speed.md)), and the campaign speed ladder tops out
at **50**:

| Speed | One game day | Frames available at 60 fps | Reaches record 118? |
| --- | --- | --- | --- |
| 1 | 2.5 min | 9,000 | yes, easily |
| 5 | 30 s | 1,800 | yes |
| 20 | 7.5 s | 450 | yes |
| 50 | **3.0 s** | **180** | yes, with 1.5× to spare |

At speed 50 the day is 3 seconds, so the sweep needs about **40 frames per
second** to finish. Above that you are fine. Below it the cursor stops partway
and **the rest of the file is not tested that day at all** — and because the
file is roughly chronological, the part that goes untested is the late war. At
30 fps the cursor reaches about record 89, dated 21 May 1864; the twenty-five
real records after it include Grant's Overland campaign, the fall of Atlanta,
Lincoln's re-election, Sherman's march and Appomattox.

> **If you fast-forward 1864–65 at maximum speed on a machine that drops below
> 40 fps, you will quietly get no late-war papers.** That is exactly the
> situation a late-war campaign map produces: the most units, the most
> buildings, the lowest frame rate, and the most reason to fast-forward.
>
> The workaround costs nothing: **drop to speed 20 for the last year**, or cap
> your frame rate high enough that 60 fps is steady. **Untested** in play — the
> test is an 1865 start at speed 50 with the frame rate capped below 40,
> checking whether the late-war papers print.

Source: [25](../code-findings/25-newspaper-and-events.md) §2.4,
[time-and-speed](time-and-speed.md).

---

## 5. Reading a battle newspaper

The battle report is the only paper written procedurally rather than authored,
and the only one that prints **through a pause**. It assembles up to six
headlines — the result and place, the losing commander's fate, ships sunk or
escaped, whether a commander became a national hero or lost face, and a morale
line — then a body naming both forces' strength and guns, the hours fought,
casualties, captured weapons, prisoners, and up to **five** killed officers per
side.

Three things to know about how to read it.

**Small naval actions get no paper at all.** Under **8 ships** between both
sides, you get a dispatch in the log and nothing else. The same is true, at any
size, when **a commerce raider ran into a blockade** — that pairing also costs
neither side any national morale
([17](../code-findings/17-politics-and-support.md) §3a), so the absence of a
paper is a reliable signal that the action was politically free.

**The captured-weapons wording is a three-step scale**, not a number: *small*
below 1,000 weapons, *substantial* below 2,500, *large* below 5,000, and
*great* above. If you want the count, the battle summary panel has it.

**The morale sentence is always the same sentence.** The paper compares the
national-morale effect of the battle against two thresholds of −1.0 and −2.0 —
on a scale where a 30,000-casualty defeat is worth about **−0.026**
([how-you-lose](how-you-lose.md) §4). So the morale headline is always
*"Support for the … cause wavers."* and the closing line is always *"the public
unmoved by the recent events."* Five other authored variants, including the
whole *"in shock after the licking"* register, are unreachable. **Do not read
the morale paragraph as information**; read the national-morale figure on the
Politics panel instead.

Source: [25](../code-findings/25-newspaper-and-events.md) §5,
[27](../code-findings/27-dispatches-and-the-message-log.md) §9.

---

## 6. The dispatch channel

A dispatch is the other half of the game's reporting: a letter from a
subordinate, appearing as a blinking symbol on the map for **30 real seconds**
and then living in the log. Click it to centre the camera on the unit;
double-click it to read it. **Forty-six kinds exist** — 26 for battle and 20
for the campaign — and not one word of any of them comes from a data file.

Four facts shape how useful the channel is.

**Only your own units report.** The warning system returns immediately for any
formation that is not yours, so a dispatch is always news about your side (with
two deliberate exceptions, below).

**A dispatch is signed by the corps commander**, not by the unit in trouble.
The reporting system walks up the hierarchy to the highest parent at Corps
level or below, so a brigade's low-ammunition warning arrives over a corps
commander's name with the brigade named inside. Two dispatches break that
pattern: a rout is signed by the routed formation, and **the two casualty
warnings are signed by the regiment itself** — a copy-paste slip left the
branch that would have signed the parent as dead code.

**The text is frozen when the dispatch is raised, not when you read it.** Every
figure in the body — strength, morale, supply percentages, readiness — is the
figure at the moment the courier left. A dispatch you open twenty minutes later
is a historical document, and it will not agree with the unit panel.

**Repeats are suppressed for a window**, and in battle that window is broken.
The intended rule is "do not raise the same dispatch about the same unit twice
within half a game hour." The comparison uses the **clock time of day** rather
than elapsed time, so once a battle crosses midnight the arithmetic goes
negative and any dispatch raised at a later clock hour counts as half an hour
old. In practice: **a brigade that reported low ammunition at 18:00 on day one
cannot report it again until after 18:30 on day two.** In the campaign the
equivalent window — about **1 day 20 hours** — works correctly.

Source: [27](../code-findings/27-dispatches-and-the-message-log.md) §3.2, §4.4,
§5.

### 6.1 Enemy reinforcements are announced through the fog

Two dispatch types are about the enemy, and both are meant to be shaped by what
you can see.

The honest pair works: when an enemy formation leaves the battlefield, or
finishes a pontoon, **you are told an hour late if it was hidden and
immediately if it was visible**. Fog delays the news rather than withholding
it, which is what the configuration labels promise.

The third does not. *"<Enemy general> arrives"* — enemy reinforcements entering
the battlefield — is the only dispatch built to wait until the formation is
actually spotted, and it passes a delay of zero, which satisfies the
elapsed-time release before the sighting condition is ever consulted.

> **You are told, on the next frame, that enemy reinforcements have arrived —
> by the commander's name, with their formation type and their approximate
> strength — whether or not anything of yours can see them.** The numbers are
> still rounded by the fog filter (infantry to the nearest 1,000, cavalry 500,
> guns 5), so fog blurs the figures while leaving the arrival itself public.
>
> This is free intelligence in every battle where the enemy has scheduled
> arrivals, and it is the reason the whole "unit spotted" deferral mechanism
> appears to do nothing: one caller, and it defeats itself.

A cosmetic tell that the same bug is firing: each of those dispatches **burns
two issue numbers**, so the numbering on your letterheads skips.

Source: [27](../code-findings/27-dispatches-and-the-message-log.md) §3.3.

---

## 7. Exactly when a warning dispatch fires

These are the thresholds the game warns you at. They are worth knowing as
numbers, because they tell you how much slack a warning leaves you.

| Warning | Fires at | Re-arms at | Who can send it |
| --- | --- | --- | --- |
| Low morale | below **40 %** morale | above **48 %** | **Divisions and Corps only** |
| Routed | the whole formation routed | when it is not | **Divisions and Corps only** |
| Low ammunition | below **30 %** | at 30 % | any non-artillery unit |
| Critical ammunition | below **15 %** | at 15 % | any non-artillery unit |
| Low artillery ammunition | below **25 %** of its mean ammunition | at 25 % | artillery only |
| High casualties | above **40 %** of original strength | never | any fighting unit |
| Very high casualties | above **60 %** of original strength | never | any fighting unit |
| Out of supply (campaign) | overall supply **below 25 % and falling** | — | any unit — see below |

Four readings.

**The 40 % morale warning lands on the wrong side of one of the two break
thresholds.** A unit routs below **25 %** morale under fire and below **50 %**
in contact ([why-units-break](why-units-break.md) §2). So in a firefight the
warning gives you fifteen points of warning, while **a division already in
melee has passed its break line before the dispatch is written**. It has the
only hysteresis in the set — it re-arms at 48 % — so you get one warning per
collapse rather than a stream.

**Nothing above Corps ever warns you.** An Army or Army Group is silent, and so
is a brigade. The morale and rout warnings live at exactly two echelons.

**The ammunition warnings re-arm at the firing threshold**, with no hysteresis
at all, so a unit sitting at 30 % would warn every frame — which is the only
reason the repeat window of §6 exists. It is also why the broken battle window
matters: suppress the repeat for fourteen hours and the second night of a
battle gives you no ammunition warnings for anything that already warned.

**The casualty percentages are of original strength**, not of what is left. A
brigade "above 40 % casualties" has lost two men in five of the men it started
with.

### 7.1 Your army can starve without a dispatch

The campaign supply warning looks sensible — overall supply below a quarter and
falling — but it has a second condition: **rifle ammunition or forage must also
be below 25 %.** Artillery ammunition and **provisions** are not tested, even
though the dispatch body reports all four by name.

> **Starve an army of food and you may get no warning at all.** Keep rifle
> ammunition and forage above a quarter and the dispatch never fires, while the
> army loses morale and men to the shortage overnight exactly as it would
> otherwise ([supply-in-practice](supply-in-practice.md),
> [what-kills-your-men](what-kills-your-men.md)).
>
> The practical answer: **do not rely on the dispatch for provisions.** Watch
> the four supply bars on the army panel, or the supply column in the army
> list, and treat the dispatch as a rifle-ammunition-and-forage alarm only.

Source: [27](../code-findings/27-dispatches-and-the-message-log.md) §5, §5.1.

### 7.2 The Quartermaster's Morning Report, and its one dead sentence

At the end of each battle day you get a report signed *Quartermaster*. Most of
it is real: total men absent without leave, total wounded, the army's supply
condition, and then a four-line reserve forecast — *sufficient* at seven days
or more, otherwise a day count, and *nearly exhausted* under a day — for rifle
ammunition, artillery ammunition, provisions and forage. **That forecast is the
single most useful readout in the battle UI** and it is correct.

Its first sentence is not. *"The reports received suggest morale in the army
is …"* reads a **desertion** figure on your top formation, through a five-band
scale that saturates at 8 % desertion per night — and nothing in the game ever
writes that field for a formation.

> **The sentence always says "intact."** Win or lose, after a quiet night or a
> catastrophic one. Skip it and read the rest.

Source: [27](../code-findings/27-dispatches-and-the-message-log.md) §7.

---

## 8. The monthly intelligence report is the best deal in the game

On the first day of every month, two dispatches arrive: the Secretary of the
Treasury's economy report and the Secretary of War's intelligence report. They
describe the month that just ended, and they are **unconditional** — no
project, no commander attribute, no scouting, no cost. Nothing in the game
gates them except the month rolling over.

What the intelligence report hands you about the enemy:

| It tells you | How precisely |
| --- | --- |
| Every policy the enemy **finished** last month | by name |
| Every policy and act it is **still researching** | by name |
| Whether its government is in a **recruitment or credit crisis** | stated outright |
| Every **project** it finished last month | by name |
| Men recruited, and in how many brigades | men rounded by the fog filter; **the brigade count exact** |
| Every **Corps and Army it raised** | by name, **with the state it is meant to operate in** |
| Every **fleet** it raised | by name, with the state it is operating near |
| Ships laid down | exact |
| Its armies' **average morale** | **exact percentage, plus the change on last month** |
| High-ranking officers it lost | exact count, **naming the top three** |
| The most aggressive thing it did | plain English (below) |

Only the recruited-men figure passes through fog of war at all. Everything else
in that table is printed exactly, including the enemy's army morale to the
percentage point.

**The last row is the one to learn to read.** The sentence is one of five:

| It says | It means |
| --- | --- |
| *"consolidating and regrouping"* | the AI did none of the four things below |
| *"carrying out raids to target our supply lines"* | it formed at least one raid force — and nothing more aggressive |
| *"mostly defending his land"* | it put units on defence |
| *"carrying out offensive operations"* | it launched at least one offensive operation |
| *"planning a naval invasion"* | **a sea-invasion force exists** |

It is a **high-water mark, not a doctrine**: the five activities are ranked, and
the sentence reports the highest one reached during the month. So *"raids"*
means raids *and nothing more*, and *"consolidating"* means a genuinely idle
month — which, in a chapter where the AI has aggression
([fighting-the-ai](fighting-the-ai.md) §6), is itself information.

> **"Planning a naval invasion" is the one actionable warning in the game.**
> The flag is raised while the invasion force is still being assembled, not
> after it lands, so you get a month or more to garrison your coast. No other
> readout tells you this.

One paragraph of the report is broken and should be ignored: the European
relations line prints the wrong power's figure on both sides, and as the Union
never shows a monthly change at all
([ui-vs-simulation](ui-vs-simulation.md) §2.8). Read the European Relations row
on the Strategy panel instead — which has its own problem in Chapter I
([manual-vs-code](manual-vs-code.md)).

The economy report is about **your** nation, so nothing in it is approximated.
It flags a product as a shortage below **20 %** stock and as overproduced above
**80 %**; neither threshold feeds anything in the simulation, so they are
descriptive labels and not warnings of a penalty.

Source: [27](../code-findings/27-dispatches-and-the-message-log.md) §6,
[17](../code-findings/17-politics-and-support.md) §4d.

---

## 9. The dispatch log shows ten days, and deletes the rest when you open it

The log keeps dispatches for **ten game days**. That figure is hardcoded — it
is in no configuration file and no options screen.

The purge does not run in the background. It runs in the log panel's own
update, which only runs **while the panel is open**. So the sequence is: you
open the log, and on the first frame everything older than ten game days is
destroyed.

| Campaign speed | Ten game days in real time |
| --- | --- |
| 1 | 25 minutes |
| 5 | 5 minutes |
| 20 | 75 seconds |
| 50 | **30 seconds** |

> **At full speed, the log is a thirty-second window.** Fast-forward through a
> month and the economy report, the intelligence report, the battle results and
> every officer-loss report in it are gone before you look. If you want to read
> a monthly report, read it when the symbol appears — the two cabinet reports
> also blink their HUD buttons red for ten seconds when they arrive, which is
> the only notice you get.
>
> **Untested**: the conclusion that the purge needs the panel open rests on the
> log panel being deactivated when hidden, which the code implies but which an
> asset dump would confirm.

Two smaller things about the log. It pages a **whole screen at a time** on the
mouse wheel — there is no line scroll, no filter, no sort and no search — and
unread entries are red. And the *unread* counter beside the total is computed
by a background sweep of 50–100 records a frame, so with a long log it is
always one sweep out of date while the total beside it is current.

Source: [27](../code-findings/27-dispatches-and-the-message-log.md) §8.

---

## 10. Three letters the enemy never sends

Three dispatch types are written in full — headline, hover text and body — and
nothing in the game ever sends them:

- ***"<General> proposes cease-fire."*** The enemy commander asks for a halt
  for the rest of the day to tend the wounded from both armies, and says he
  will order his men to hold fire *"if my courier returns with your
  corroborative reply."*
- ***"<General> retreats."*** Your scouts see the enemy moving his trains off
  the field and ask whether to press the attack before they slip away.
- ***"<General> surrenders."*** A reply accepting your terms, promising to
  designate officers to carry the stipulations into effect — close to Lee's
  own words at Appomattox.

All three are written as **requests for your orders**, and the first explicitly
waits on an answer. You can already offer a cease-fire or a surrender yourself
through the battle prompts; the enemy has no path to ask. Taken together the
three describe a negotiated-battle-end layer the game does not have — the text
and the artwork slots exist, the triggers do not.

Source: [27](../code-findings/27-dispatches-and-the-message-log.md) §4.3.

---

## 11. Twelve things to do differently

1. **Do not fast-forward the last year at speed 50 on a machine that drops
   below 40 fps.** Drop to speed 20 and you will see the late-war papers; stay
   at 50 and you may silently get none of them (§4).
2. **Leave newspapers switched on, at least until the war is decided.** The off
   switch discards papers rather than deferring them, and it discards the
   end-of-campaign paper with them (§2.1).
3. **Read the two monthly reports when their HUD buttons blink.** They blink
   red for ten seconds and the log may delete them within thirty (§8, §9).
4. **Treat the intelligence report as your main source on the enemy.** It names
   finished and in-progress policies, every corps raised and its intended
   theatre, the enemy's exact army morale, and above all *"planning a naval
   invasion"* while the force is still assembling (§8).
5. **Ignore the European relations paragraph in that report**, and the morale
   paragraph of every battle newspaper. Both are wrong in a fixed direction
   (§5, §8).
6. **Research Indian Wars if you want the history** — six papers, more than any
   other policy — but know it also hands the enemy +5 military experience
   (§3.1).
7. **Watch for the enemy's own crisis papers.** *Recruitment in Crisis* or
   *Economy in Crisis* means the enemy AI has just switched doctrine and thrown
   away the research it had running (§3.2).
8. **Treat a battle with no newspaper as politically free.** Under eight ships,
   or a raider meeting a blockade, you get no paper and neither side pays
   national morale (§5).
9. **Do not trust the supply dispatch for food.** It only fires on rifle
   ammunition or forage; check the four supply bars yourself (§7.1).
10. **In a battle that runs past midnight, stop relying on ammunition
    warnings.** Anything that warned on day one is suppressed for most of day
    two (§6, §7).
11. **Skip the first line of the Quartermaster's Morning Report** and read the
    four-line reserve forecast under it, which is the best supply readout in
    the battle UI (§7.2).
12. **A dispatch about an enemy arrival is free intelligence** — it fires
    through fog. Open it for the formation type and the rounded strength before
    you commit your reserves (§6.1).

---

## 12. For modders

The press is unusually open: one data file, four public entry points, and ten
pure text functions.

1. **Events need no code.** `historicalevents.dat` is twelve lines a record,
   and it is one of the few files that supports a **per-sublevel override** —
   so the right fix for a late start is a trimmed copy in that campaign
   folder's directory, which is also how you would retime the ten unreachable
   secession papers to dates a 1861 start reaches.
2. **Keep `id == index` below record 42.** The suppression window for the Fort
   Sumter chain is keyed on the record's *position* in the file, not its id, so
   inserting one record above it moves the window onto four unrelated papers.
3. **Two config numbers fix the battle newspaper's morale paragraph** — set the
   two thresholds from −1.0 and −2.0 to about −0.01 and −0.02. But the second
   of the two morale functions also reads the **wrong side's** figure, so fix
   the index at the same time or the enemy press will report itself unmoved
   after being routed ([25](../code-findings/25-newspaper-and-events.md) §5.5).
4. **The enemy-reinforcement fog leak is a one-argument fix**: give that
   dispatch a non-zero delay (§6.1).
5. **The supply warning needs two more indices** to cover provisions and
   artillery ammunition (§7.1).
6. **The Quartermaster's morale sentence needs a different field** — the
   formation aggregate, or simply its morale (§7.2).
7. **Ids 12, 13 and 14 need only writers.** The text, tooltips and symbols
   exist; what is missing is the decision to send them (§10).
8. **Retention is a public static with no config hook.** Setting
   `holdmessagesfordays` once at campaign start is the only way to make the log
   keep more than ten days (§9).
9. **Always give a dispatch either a sender formation or a signature of more
   than one character.** With neither, the first time the player opens it from
   the log it throws
   ([27](../code-findings/27-dispatches-and-the-message-log.md) §12).
10. **The ten battle-report text functions are pure** and the single
    newspaper-display method is a chokepoint — between them they are the
    cleanest text-rewriting surface in the game
    ([25](../code-findings/25-newspaper-and-events.md) §8).

---

## 13. Still open

- **Which newspaper pictures exist.** The catalogue references image ids 11–96
  plus a 506/507/513/522/526 block that may be DLC, and the battle report uses
  0, 1, 3, 4 and 8–10. They live inside the Unity asset bundle, not on disk, so
  enumerating them needs an asset dump — which would also settle whether the
  land battle report has the **victory and defeat pictures swapped**
  ([25](../code-findings/25-newspaper-and-events.md) §5.6).
- **Whether the late-war papers really go missing below 40 fps** (§4). The
  arithmetic is clear; the play test is not done.
- **Whether the log purge really needs the panel open** (§9).
- **The `Diary`**, a third narrative channel whose data file is **absent from
  this install** — it is DLC content and does nothing here
  ([25](../code-findings/25-newspaper-and-events.md) §3.3).
- **Whether the two en-dashed event captions survive a non-Western Windows
  codepage.** The event file is read with the OS's ANSI encoding, so two
  records carrying a CP1252 en dash would decode differently on, say, a
  Japanese system ([25](../code-findings/25-newspaper-and-events.md) §9). One
  third record contains a plain authoring typo — *"wanderïng freely"* — that no
  encoding explains.
