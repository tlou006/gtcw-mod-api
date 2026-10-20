# Summary — Policy and project effects

**Covers:** [09-policy-and-project-effects](09-policy-and-project-effects.md)
**Build:** see [00-build-provenance](00-build-provenance.md)

## In one paragraph

Policy and project effects are hardcoded as giant `switch` statements on id —
`Policies.LaunchPolicyEffect` alone is ~1,200 lines with 117 arms. **None of
the switches has a `default:` arm**, so an unrecognised id falls through and
the game still accepts, researches and tracks the policy or project; it simply
does nothing. That means **new ids can be injected and given behaviour by a
Harmony postfix**, composing with the game's own dispatch instead of replacing
it. This resolves the critical unknown for project goal #2 in the favourable
direction. The remaining constraint is that `Policy` is a prefab-authored
`MonoBehaviour`, so whether a policy can be registered *without* shipping a
prefab is now the gating question.

## The dispatch sites

| System | Method | Shape |
| --- | --- | --- |
| Policies, one-shot | `Policies.LaunchPolicyEffect(Policy, int alliance, float timediff, bool startupset)` | `switch` on `UniquePolicyID`, **117 arms, ~1,200 lines** |
| Policies, teardown | `Policies.DeactivatePolicyEffect(Policy, int alliance)` | mirror |
| Policies, recurring | `Policies.UpdatePolicyEffects(int alliance, float timediff, bool startsetup)` | per-tick |
| Projects, one-shot | `Projects.UpdateOneTimeProjectsEffects(int projectid, int alliance)` | `switch`, 12 arms |
| Projects, recurring | `Projects.UpdateProjectEffects()` | `switch` inside an alliance loop |

## Why the missing `default:` is the whole story

A **postfix** runs after the game's switch and receives the same arguments. So:

- the game's own 117 arms keep working, untouched — **no reimplementation**;
- no prefix means nothing is suppressed; no transpiler means no IL to break on
  patch day;
- ids the game does not know are free real estate.

```csharp
GTCW.ModApi.Projects.Register("Repeating Rifles", effect => {
    effect.OnCompleted += ctx => ctx.Nation.UnlockWeaponClass("Repeater");
});
```

**Both effect systems are open at the top end.** This is the single most
important structural finding so far, and it is what makes the hook library
worth building.

## Three constraints on the design

1. **The `+100` id offset.** Arms are written `case 0: case 100:` — ids `0–99`
   and `100–199` are the Union and Confederate instances of one policy, sharing
   code that then indexes `GameVars.alliance[alliance]`. A registered effect
   must cover both blocks or key off `alliance`. `requiredpolicyids` is 3 chars
   wide, so there is numeric room above 199 for mod ids.
2. **Teardown and recurrence are separate methods.** Registration needs
   `DeactivatePolicyEffect` (or the effect persists after removal) and
   `UpdatePolicyEffects` (for continuous effects), not just the launch path.
3. **`Policy` is prefab-authored.** It carries `TextMeshProUGUI` / `Image`
   references alongside its data, with static registries built by
   `Policy.InitializeStatics`. Policies are **not** purely data-driven.

## The gating question

> **Can the ModApi register a policy by cloning an existing `Policy` at runtime
> and rewriting its fields, or must modders ship prefabs?**

Cloning would inherit working UI wiring — exactly the hard part.

**Largely answered** in
[22-policy-and-project-catalogue](22-policy-and-project-catalogue.md) §15:
`Policy.InitializeStatics()` is `public static` and rebuilds `allpolicies` from
every `Policy` component under the `UI` GameObject, so a clone placed there and
followed by that call should register. Three things still need a live build:
whether the clone is found, whether `ButtonTooltipRef` and
`markedasgrandpolicy` survive cloning, and what the panel does with a button it
has no layout slot for. Note also that `InitializeStatics` runs
`ImportPolicyData` immediately afterwards, which overwrites the clone's fields
if `policies.dat` carries that id.

## The cheap documentation win — half right

Two methods looked like they map **id → meaning** directly, because they
generate the player-facing effect text:

- `Projects.GetProjectEffect(int id, float effect, float secondeffect = 0f)` —
  **yes.** It is the id → pref mapping for the 26 numeric-modifier projects.
- `Policies.UpdateAllTooltips(int activatedpolicies = -1)` — **no.** It only
  appends research-speed text; each policy's description is a prefab string and
  its mechanics exist only inside `LaunchPolicyEffect`.

So the policy half of the catalogue had to be read out of the 1,200-line switch
after all. It is done:
[22-policy-and-project-catalogue](22-policy-and-project-catalogue.md), with the
correction recorded in its §3.1.

## A useful nuance

**Some magnitudes are already data-tunable even though the effect is
hardcoded.** Several project arms read their value from `GamePrefs` (e.g.
`machinegunprojectstock`, `project_propagandainfl`) rather than from a literal.
Mapping `GamePrefs`-backed versus literal effects defines how much actually
needs patching — the former are already balanceable through config alone.

## Still open

- The prefab question (above) — gating.
- The id → effect catalogue.
- `GameVars.alliance[]` / `GameVars.nation[]` field maps: the state every
  effect writes to. `AdjustSubsidyFactor` and `AdjustSupport` are the two
  mutators seen; `AdjustSupport`'s third argument looks like a reason code.
- Whether the AI needs teaching about mod-added policies
  (`CheckAIPolicyChange`).
- Grand strategies (`grandpolicies`, `markedasgrandpolicy`) — a distinct policy
  tier, not yet understood.
- `IsDeactivated` holds at least one date-hardcoded special case (id 36, a 1861
  date); there may be more such one-offs lurking.
