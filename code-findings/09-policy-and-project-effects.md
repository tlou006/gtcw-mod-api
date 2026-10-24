# 09 — Policy and project effect dispatch

**Build:** see [00-build-provenance](00-build-provenance.md)
**Source:** `Assembly-CSharp.dll` — `Policies` (1,926 lines), `Policy`,
`Projects` (1,042 lines)
**Summary:** [summary-effects](summary-effects.md)

This was **the open unknown for project goal #2** (the hook library). It is
now resolved, and the answer is the favourable one.

## Context

The Quick Guide states that policy and project *effects* are hardcoded — the
data files hold only names, costs, prerequisites and descriptions. A modder can
add a project today and it will appear, be researchable, and do nothing. That
is what makes **effect registration** the hook library's headline capability
rather than a convenience.

The question was *where* effects are dispatched, and whether new ids can be
injected or only existing ones intercepted.

## The dispatch sites

| System | Method | Shape |
| --- | --- | --- |
| Policies, one-shot | `Policies.LaunchPolicyEffect(Policy policy, int alliance, float timediff = 0f, bool startupset = false)` | ~1,200-line `switch` on `policy.UniquePolicyID`, **117 `case` labels** |
| Policies, teardown | `Policies.DeactivatePolicyEffect(Policy policy, int alliance)` | mirror of the above |
| Policies, recurring | `Policies.UpdatePolicyEffects(int alliance, float timediff = 0f, bool startsetup = false)` | per-tick application |
| Projects, one-shot | `Projects.UpdateOneTimeProjectsEffects(int projectid, int alliance)` | `switch (projectid)`, **12 arms** |
| Projects, recurring | `Projects.UpdateProjectEffects()` | `switch` on project id **inside** an alliance loop |

Each `LaunchPolicyEffect` arm hardcodes its policy's effect: adjusting subsidy
factors, unlocking content, shifting national support, setting trade-node
ownership, constructing railroad segments, applying rating modifiers.

## Finding 1 — no `default:` arm anywhere

**`LaunchPolicyEffect` has no `default:` case. Neither does either `Projects`
switch** — `Projects.cs` contains no `default:` at all.

So an unrecognised id **falls straight through** and the method completes
normally. For policies, execution then continues past the switch to record the
policy in `GameVars.alliance[alliance].alreadyactivatedacts` and log it.

> **A modder can already add a policy or project with a new id today, and the
> game will accept, research, appoint and track it. It simply has no effect.**

### Why this is the key structural fact

A **Harmony postfix** on `LaunchPolicyEffect` receives
`(policy, alliance, timediff, startupset)` and runs *after* the game's switch.
Effects registered against unused ids therefore **compose** with the game's own
dispatch instead of fighting it:

- no prefix, so the game's own arms are never suppressed;
- no transpiler, so no IL rewriting to break on patch day;
- **none of the 117 existing arms need reimplementing.**

The `plan.md` Phase 4 sketch is implementable as designed:

```csharp
GTCW.ModApi.Projects.Register("Repeating Rifles", effect => {
    effect.OnCompleted += ctx => ctx.Nation.UnlockWeaponClass("Repeater");
});
```

Both effect systems are open at the top end. This is the single most important
finding so far and it is what makes goal #2 worth building.

## Finding 2 — policy ids carry a `+100` offset per side

Arms are written `case 0: case 100:`, `case 1: case 101:`, `case 2: case 102:`
and so on. Policy ids **`0–99` and `100–199` are the Union and Confederate
instances of the same policy**, falling through to shared code that then
indexes `GameVars.alliance[alliance]`.

This is the "numeric suffix = nation index" convention already noted in
[07-config-data-model](07-config-data-model.md), appearing here as an **id
block offset** rather than a filename suffix.

> **A registered effect must handle both id blocks, or key off the `alliance`
> argument instead.** An effect registered only against the low id will not
> fire for the other side.

The `requiredpolicyids` field in `projects.dat` is 3 characters wide
([06-config-file-format](06-config-file-format.md)), which accommodates ids up
to 999 — so there is numeric room above 199 for mod-defined policies.

## Supporting members of `Policies`

All static, all hookable:

| Member | Purpose |
| --- | --- |
| `DeactivatePolicyEffect(Policy, int alliance)` | The teardown mirror. **A registered effect needs a path here too**, or it will persist after the policy is removed. |
| `UpdatePolicyEffects(int alliance, float timediff, bool startsetup)` | Recurring per-tick application, distinct from the one-shot launch. Registration needs both. |
| `CanBeActivated(int alliance, Policy policy)` | Gating — the hook point for custom prerequisites. |
| `UpdateAllTooltips(int activatedpolicies = -1)` | Appends research-speed text to each policy's tooltip. **Not** an id → effect map — the description lives in the prefab's `Tooltip` field and the mechanics only in `LaunchPolicyEffect`. Corrected in [22](22-policy-and-project-catalogue.md) §3.1. |
| `AddResearch` / `RemoveResearch` / `UpdatePolicyResearch` / `UpdateCounter` | Research progress plumbing. |
| `UpdatePolicyResearch(int alliance, float timediff)` | Research advance per tick. |
| `CheckAIPolicyChange(int alliance)` | How the AI chooses policies — a mod adding policies must teach the AI about them, or cede them to the player only. |
| `IsDeactivated(Policy policy)` | Contains at least one **date-hardcoded special case** (id 36, keyed to a 1861 date). |
| `UpdateSubsidyEffect`, `UpdateGrandStrategies`, `UpdateGrandStrategyIcons`, `UpdateTradeNodesEurope`, `HandoverForts`, `LaunchNewspaperMessage`, `GetAvailablePolicies`, `ClearResearchNumbers`, `UpdateActivations` | Supporting behaviour. |

## `Policy` is prefab-authored — the gating constraint

`Policy` is a `MonoBehaviour` carrying **UI references alongside its data**:
`PolicyLevelText`, `DurationText`, `QueueImage`, `ResearchTimeImage`,
`ButtonTooltipRef` (all `TextMeshProUGUI` / `Image` / component refs), mixed in
with `UniquePolicyID`, `validscenarios`, `NeededPrePolicies`,
`ResearchDuration`, `IsAct`, `ReferedChapter`, `TooltipHeadline`, `Tooltip`,
`ShowNewspaperMessageEnemy`, `Headlines`, `PictureID`, `HeadlinePicture`,
`Content`.

Static registries `Policy.allpolicies` and `Policy.grandpolicies` are populated
by `Policy.InitializeStatics(bool skiplevelcheck = false)`.

> **Policies are therefore not purely data-driven.** They are authored as
> prefabs in the Unity scene, which confirms the "prefab-authored mechanics"
> risk in `plan.md`.

**This is now the gating unknown for the whole effect-registration design:**
can the ModApi register a policy by **cloning an existing `Policy` at runtime
and rewriting its fields**, or must modders ship prefabs? The clone route would
inherit working UI wiring, which is exactly the hard part — but it depends on
load order relative to `InitializeStatics`, and on whether the registries are
rebuilt per scene.

## Projects

`Projects` dispatches in two places rather than one:

- **`UpdateOneTimeProjectsEffects(int projectid, int alliance)`** — 12 arms for
  one-shot grants. Observed effects: adding to
  `GameVars.alliance[alliance].weaponstock[...]` by weapon index, and
  `GameVars.nation[m].AdjustSupport(...)` looped over the alliance's nations.
- **`UpdateProjectEffects()`** — the recurring path, iterating alliances and
  their active `projects[]` with the switch inside the loop.

> **Some magnitudes are already data-tunable even though the effect is
> hardcoded.** Several arms read their value from `GamePrefs` (e.g.
> `machinegunprojectstock`, `project_propagandainfl`) rather than from a
> literal. Mapping which effects are `GamePrefs`-backed versus literal defines
> how much actually needs patching — a `GamePrefs`-backed effect is already
> balanceable through config alone.

### The Rosetta Stone

```csharp
private string GetProjectEffect(int id, float effect, float secondeffect = 0f)
```

Builds the human-readable effect string per project id. **This maps id →
meaning directly**, and is the cheapest possible route to documenting every
project — the same trick as the `Get*Tooltip` methods elsewhere. It has **no
policy-side counterpart**: `Policies.UpdateAllTooltips` only appends research
speed, so the policy catalogue had to be derived from `LaunchPolicyEffect`
itself. See [22](22-policy-and-project-catalogue.md) §3.1 and §12.3.

### Other `Projects` members

`LoadedProjects.Import` (the parser —
[06-config-file-format](06-config-file-format.md)),
`GetLoadedProjectFromID`, `GetProjectNameByID`, `IsAppointable`,
`AppointProject`, `CheckProjectUnlocks`, `IsProjectAvailableInScenario`,
`HasPolicy`, `GetRequiredPolicies`, `FulfillsDLC`, `GetSubsidyCost(int level)`,
`UpdateDynamicData`, `UpdateAllProjectEffects`.

## Open questions

1. ~~**Can a policy be registered without shipping a prefab?**~~ **Answered** in
   [22](22-policy-and-project-catalogue.md) §15: not from `policies.dat`, which
   only overrides ids the scene already has — but `Policy.InitializeStatics()`
   is `public static` and rebuilds `allpolicies` from the `Policy` components
   under `UI`, so a runtime clone plus that call is the route. Projects need no
   prefab at all, and 56 ids ship as `PLACEHOLDER`.
2. ~~**The id → effect table for all 117 policy arms and every project id.**~~
   **Done** — [22-policy-and-project-catalogue](22-policy-and-project-catalogue.md).
   Note the harvest route in this file was half wrong: `GetProjectEffect` is a
   real rosetta stone, `UpdateAllTooltips` is not.
3. ~~**Which effects are `GamePrefs`-backed vs. literal.**~~ **Answered** for
   projects in [28-project-effect-reachability](28-project-effect-reachability.md)
   §7: **33** `project_*` prefs carry every project magnitude except project
   5's two `weaponstock` literals, so rebalancing projects is almost entirely
   config work. That file also audits reachability — four projects are
   unappointable in a base-game install and twelve have an effect that misses
   its target.
4. **`GameVars.alliance[]` and `GameVars.nation[]` field maps** — the state
   every effect writes to, and therefore the ModApi's core data surface.
   `AdjustSubsidyFactor(int type, float value)` and
   `AdjustSupport(int alliance, float value, int reason)` are the two mutators
   seen so far; the third argument of `AdjustSupport` looks like a reason code
   worth enumerating.
5. **Does the AI need teaching?** `CheckAIPolicyChange` decides AI policy
   selection; a mod-added policy is presumably invisible to it.
6. **Grand strategies** (`grandpolicies`, `markedasgrandpolicy`,
   `UpdateGrandStrategies`) are a distinct tier of policy that is not yet
   understood.
