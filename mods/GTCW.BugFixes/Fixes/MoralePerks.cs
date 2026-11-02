using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace GTCW.BugFixes.Fixes
{
    /// <summary>
    /// GTCW-024, GTCW-025 and GTCW-027 — three perks whose morale effects are
    /// misapplied inside <c>Regiment.UpdateMorale</c>, and therefore
    /// <b>one</b> patch.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>GTCW-024, Deadly volley.</b> The config label is "reduction of morale
    /// hit from high momentary casualties" and the manual gives it at every
    /// level. The code applies the 0.5 to the <em>drill reduction</em> of the
    /// term, and only from level III, so a fully drilled holder's hourly hit
    /// grows from −0.18 to −0.39 and an undrilled one sees nothing at all.
    /// </para>
    /// <para>
    /// <b>GTCW-025, Iron discipline.</b> From level II the perk zeroes the
    /// factor that <em>decays</em> the friendly-rout penalty rather than the
    /// penalty itself, so every nearby rout costs −0.075 an hour for the rest of
    /// the battle instead of for fifteen minutes.
    /// </para>
    /// <para>
    /// <b>GTCW-027, Vanguard.</b> A bare <c>HasPerk(12)</c> zeroes the
    /// outflanked and flanking-fire terms at <em>every</em> level — the manual's
    /// level II benefit, a level early — while the detached penalty it promises
    /// at level I has no perk test anywhere.
    /// </para>
    /// <para>
    /// <b>Why nothing here recomputes a morale term.</b> The method ends with
    /// <c>num22 = morale − num2</c>: a net <em>loss</em> is amplified by the
    /// unit's experience and its commander's leadership, and the result is then
    /// clamped by <c>fightingspirit</c> and to [0, 1]. A postfix adding a delta
    /// would bypass all of that. Lending the method a different value for one
    /// of its own single-reader prefs lets the game's arithmetic produce the
    /// documented number and carry it through its own clamps, its own
    /// amplification and into <c>moraleeffects[]</c> for the breakdown panel.
    /// </para>
    /// </remarks>
    internal static class MoralePerks
    {
        private const string DeadlyVolley = "GTCW-024";
        private const string IronDiscipline = "GTCW-025";
        private const string Vanguard = "GTCW-027";

        private const string Target = "Regiment:UpdateMorale";
        private const string GuardLabel = "GTCW-024/025/027";

        /// <summary>Battle perk ids, as the game numbers them.</summary>
        private const int DeadlyVolleyPerk = 5;
        private const int IronDisciplinePerk = 7;
        private const int VanguardPerk = 12;

        /// <summary>
        /// Re-entrancy depth. The spoofed values are computed for one specific
        /// unit, so a nested call must not be allowed to overwrite them.
        /// <c>UpdateMorale</c> can call <c>Rout()</c>, which reaches into other
        /// regiments; nothing found in the decompile re-enters this method, and
        /// the counter costs an increment to make that irrelevant.
        /// </summary>
        private static int _depth;

        private static bool _casualtySpoofed;
        private static float _savedHit;
        private static float _savedDrill;

        private static bool _cutoffSpoofed;
        private static float _savedCutoff;

        /// <summary>The unit and slot whose Vanguard entry is hidden, or null.</summary>
        private static Regiment? _hiddenOn;
        private static int _hiddenSlot = -1;

        public static FixGroup Group() => new FixGroup(
            Target,
            new[]
            {
                new Fix(
                    DeadlyVolley,
                    "Deadly volley halves the morale hit from heavy momentary casualties, at every level. " +
                    "Unfixed it more than doubles that hit for a well-drilled unit.",
                    FixClass.Code,
                    defaultEnabled: false,
                    SaveSafety.Stateless,
                    "code-findings/34-perks.md §3.6",
                    Probe,
                    family: Fix.Perks),
                new Fix(
                    IronDiscipline,
                    "Iron discipline II and III ignore a nearby friendly rout instead of being stuck with " +
                    "it. Unfixed the penalty never decays and accumulates for the rest of the battle.",
                    FixClass.Code,
                    defaultEnabled: false,
                    SaveSafety.ValueOnly,
                    "code-findings/34-perks.md §3.8",
                    Probe,
                    family: Fix.Perks),
                new Fix(
                    Vanguard,
                    "Vanguard I removes the morale penalty for being detached, as documented, and the " +
                    "exemption from flanking penalties arrives at level II rather than level I.",
                    FixClass.Code,
                    defaultEnabled: false,
                    SaveSafety.Stateless,
                    "code-findings/34-perks.md §3.13",
                    Probe,
                    family: Fix.Perks),
            },
            Apply);

        private static ProbeResult Probe() => FixProbe.All(
            () => FixProbe.RequireMethod("Regiment", "UpdateMorale"),
            () => FixProbe.RequireMethod("Regiment", "GetTraining"),
            () => FixProbe.RequireField("Regiment", "friendlyroutednear"),
            () => FixProbe.RequireField("Regiment", "perks"),
            () => FixProbe.RequireField("GamePrefs", "moraleeffectonhighmomentarycasualties"),
            () => FixProbe.RequireField("GamePrefs", "drillreductionfactorforhighmomentarycasualties"),
            () => FixProbe.RequireField("GamePrefs", "perk_deadlyvolley_reductionmoralehit"),
            () => FixProbe.RequireField("GamePrefs", "moraleeffectofbeingcutoff"));

        private static void Apply(Harmony harmony)
        {
            MethodInfo target = AccessTools.Method(Target)
                ?? throw new InvalidOperationException($"'{Target}' not found");

            harmony.Patch(
                target,
                prefix: new HarmonyMethod(AccessTools.Method(typeof(MoralePerks), nameof(Before))),
                finalizer: new HarmonyMethod(AccessTools.Method(typeof(MoralePerks), nameof(After))));
        }

        private static void Before(Regiment __instance, out bool __state)
        {
            _depth++;

            // True whenever the loan was *attempted*, not whenever it
            // succeeded: if `Lend` throws half way through, the finalizer still
            // has to run, and `Restore` is driven by its own flags rather than
            // by this one.
            __state = _depth == 1;

            if (__state)
            {
                FixState.Guard(GuardLabel, () => Lend(__instance));
            }
        }

        /// <summary>
        /// A finalizer, not a postfix: <c>UpdateMorale</c> calls <c>Rout()</c>,
        /// <c>LossesAreUnbearable()</c> and the DLC incident checks, any of which
        /// could throw. A spoofed <c>GamePrefs</c> field that is never put back
        /// is a global balance change for the rest of the session — far worse
        /// than the defect being fixed.
        /// </summary>
        private static void After(bool __state)
        {
            _depth--;

            if (__state)
            {
                FixState.Guard(GuardLabel, Restore);
            }
        }

        /// <summary>
        /// Lends this one call whatever values make the game produce the
        /// documented result.
        /// </summary>
        private static void Lend(Regiment unit)
        {

            // --- GTCW-025, Iron discipline ---------------------------------
            //
            // Not a loan but a repair, so there is nothing to restore. The
            // accumulator is driven to zero before the term is read, which is
            // what "holds even if other units nearby falter" means; the frozen
            // decay is then harmless because there is nothing left to decay.
            // A holder that accumulated a penalty at level I has it cleared on
            // the first pass after it reaches level II.
            if (FixState.IsOn(IronDiscipline) &&
                unit.friendlyroutednear < 0f &&
                unit.HasPerk(IronDisciplinePerk) &&
                unit.GetPerkLevel(IronDisciplinePerk) >= 1)
            {
                unit.friendlyroutednear = 0f;
            }

            // --- GTCW-024, Deadly volley -----------------------------------
            //
            // The game computes  T − T·training·d·f  and the documented term is
            // T·p·(1 − training·d), so lend  T' = T·p  and  d' = d/f, where f is
            // the factor the game is going to apply anyway. Exact at every
            // level, and the shipped p stays the authority on magnitude.
            if (FixState.IsOn(DeadlyVolley) && unit.HasPerk(DeadlyVolleyPerk))
            {
                float p = GamePrefs.perk_deadlyvolley_reductionmoralehit;
                float f = unit.GetPerkLevel(DeadlyVolleyPerk) >= 2 ? p : 1f;

                // A data mod that zeroes either factor gets left alone rather
                // than divided by zero.
                if (p > 0f && f > 0f)
                {
                    _savedHit = GamePrefs.moraleeffectonhighmomentarycasualties;
                    _savedDrill = GamePrefs.drillreductionfactorforhighmomentarycasualties;

                    GamePrefs.moraleeffectonhighmomentarycasualties = _savedHit * p;
                    GamePrefs.drillreductionfactorforhighmomentarycasualties = _savedDrill / f;

                    _casualtySpoofed = true;
                }
            }

            // --- GTCW-027, Vanguard ----------------------------------------
            if (FixState.IsOn(Vanguard) && unit.HasPerk(VanguardPerk))
            {
                // The level I headline, which the code never implements.
                _savedCutoff = GamePrefs.moraleeffectofbeingcutoff;
                GamePrefs.moraleeffectofbeingcutoff = 0f;
                _cutoffSpoofed = true;

                // The level II benefit, which the code hands out at level I.
                // Hiding the perk entry for the duration of the call is what
                // makes the two flanking terms apply again; a fighting unit has
                // exactly one perk slot, so there is nothing else in the list
                // to disturb, and slot 10's own level test would have failed at
                // this level anyway.
                if (unit.GetPerkLevel(VanguardPerk) < 1)
                {
                    Hide(unit, VanguardPerk);
                }
            }
        }

        private static void Hide(Regiment unit, int perkid)
        {
            List<Regiment.Perk> perks = unit.perks;
            if (perks == null)
            {
                return;
            }

            for (int i = 0; i < perks.Count; i++)
            {
                if (perks[i] != null && perks[i].perk == perkid)
                {
                    perks[i].perk = -1;
                    _hiddenOn = unit;
                    _hiddenSlot = i;
                    return;
                }
            }
        }

        private static void Restore()
        {
            if (_casualtySpoofed)
            {
                GamePrefs.moraleeffectonhighmomentarycasualties = _savedHit;
                GamePrefs.drillreductionfactorforhighmomentarycasualties = _savedDrill;
                _casualtySpoofed = false;
            }

            if (_cutoffSpoofed)
            {
                GamePrefs.moraleeffectofbeingcutoff = _savedCutoff;
                _cutoffSpoofed = false;
            }

            if (_hiddenOn != null && _hiddenSlot >= 0)
            {
                List<Regiment.Perk> perks = _hiddenOn.perks;
                if (perks != null && _hiddenSlot < perks.Count && perks[_hiddenSlot] != null)
                {
                    perks[_hiddenSlot].perk = VanguardPerk;
                }

                _hiddenOn = null;
                _hiddenSlot = -1;
            }
        }
    }
}
