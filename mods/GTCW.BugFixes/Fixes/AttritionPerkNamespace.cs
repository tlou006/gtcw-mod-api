using System;
using System.Reflection;
using HarmonyLib;

namespace GTCW.BugFixes.Fixes
{
    /// <summary>
    /// GTCW-035 and GTCW-036 — Foot Cavalry's and Ambulance Corps' attrition
    /// effects read a <em>brigade's battle perks</em>, and therefore <b>one</b>
    /// patch.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One perk id means three different things: a battle perk on a brigade, an
    /// army perk on a formation, a fleet perk on a fleet, and nothing in
    /// <c>HasPerk</c> knows which. <c>UpdateCampaignUnitStats</c> runs on the
    /// brigades — it returns early above unit type 13 — so its
    /// <c>HasPerk(8)</c> is Expert scouts rather than Ambulance Corps, and its
    /// <c>HasPerk(6)</c> is Ace of Spade rather than Foot Cavalry.
    /// <c>HasPerkParent</c> cannot save it: that only answers for a Corps. The
    /// result is that cavalry brigades holding Expert scouts quietly get the
    /// wounded-mortality cut, infantry brigades holding Ace of Spade get the
    /// forced-march attrition cut, and the two army perks that paid for those
    /// effects get nothing.
    /// </para>
    /// <para>
    /// <b>The method already has the formation.</b> Its first parameter,
    /// <c>groupunit</c>, is the parent — the caller is
    /// <c>array[m].UpdateCampaignUnitStats(this, …)</c> — so the fix needs no
    /// walk up the hierarchy and no guess about who owns the brigade.
    /// </para>
    /// <para>
    /// Both halves are the same two loans. The perk's factor array is lent
    /// all-ones, which stops the brigade's battle perk from doing anything; and
    /// the base pref the factor would have multiplied is lent the factor the
    /// <em>formation</em> is owed. Each of those prefs
    /// (<c>mortalityratewoundedencamped</c>,
    /// <c>mortalityratewoundednotencamped</c>,
    /// <c>sicknessadjustmentforcedmarch</c>) has exactly one reader in the whole
    /// assembly and it is inside this method, so the loan cannot reach anything
    /// else. Both encamped variants are lent because which one is read depends
    /// on a local this patch deliberately does not try to predict.
    /// </para>
    /// <para>
    /// <b>Two ids, one patch.</b> The perks are independent and a player may
    /// want one without the other, so each keeps its own toggle; they share the
    /// patch because they share the method.
    /// </para>
    /// <para>
    /// <b>Open question, carried from the audit.</b> Foot Cavalry's attrition
    /// half only runs when a brigade's <c>doublequick</c> is true, and the only
    /// campaign writer found for that flag is <c>RegimentMove</c> on the unit
    /// that moves. If an attached brigade's flag stays false the branch never
    /// runs at all — for the perk or for the defect. Confirming GTCW-035 in game
    /// is what settles it.
    /// </para>
    /// </remarks>
    internal static class AttritionPerkNamespace
    {
        private const string FootCavalry = "GTCW-035";
        private const string AmbulanceCorps = "GTCW-036";

        private const string Target = "Regiment:UpdateCampaignUnitStats";
        private const string GuardLabel = "GTCW-035/036";

        /// <summary>Army perk ids, as the game numbers them.</summary>
        private const int FootCavalryPerk = 6;
        private const int AmbulanceCorpsPerk = 8;

        private static readonly float[] Ones = { 1f, 1f, 1f };

        private static int _depth;

        private static bool _mortalityLent;
        private static float _savedEncamped;
        private static float _savedNotEncamped;
        private static float[]? _savedMortalityFactors;

        private static bool _sicknessLent;
        private static float _savedForcedMarch;
        private static float[]? _savedSicknessFactors;

        public static FixGroup Group() => new FixGroup(
            Target,
            new[]
            {
                new Fix(
                    FootCavalry,
                    "Foot Cavalry reduces forced-march attrition for the brigades of the formation that " +
                    "holds it. Unfixed the reduction goes to infantry brigades holding Ace of Spade.",
                    FixClass.Code,
                    defaultEnabled: false,
                    SaveSafety.Stateless,
                    "code-findings/34-perks.md §4.7, §7",
                    Probe,
                    family: Fix.Perks),
                new Fix(
                    AmbulanceCorps,
                    "Ambulance Corps reduces how many of the formation's wounded die. Unfixed the " +
                    "reduction goes to cavalry brigades holding Expert scouts.",
                    FixClass.Code,
                    defaultEnabled: false,
                    SaveSafety.Stateless,
                    "code-findings/34-perks.md §4.9, §7",
                    Probe,
                    family: Fix.Perks),
            },
            Apply);

        private static ProbeResult Probe() => FixProbe.All(
            () => FixProbe.RequireMethod("Regiment", "UpdateCampaignUnitStats"),
            () => FixProbe.RequireMethod("Regiment", "HasPerkParent"),
            () => FixProbe.RequireField("GamePrefs", "woundedtokilledambulancecorps"),
            () => FixProbe.RequireField("GamePrefs", "attritionfootcavalry"),
            () => FixProbe.RequireField("GamePrefs", "mortalityratewoundedencamped"),
            () => FixProbe.RequireField("GamePrefs", "mortalityratewoundednotencamped"),
            () => FixProbe.RequireField("GamePrefs", "sicknessadjustmentforcedmarch"));

        private static void Apply(Harmony harmony)
        {
            MethodInfo target = AccessTools.Method(Target)
                ?? throw new InvalidOperationException($"'{Target}' not found");

            harmony.Patch(
                target,
                prefix: new HarmonyMethod(AccessTools.Method(typeof(AttritionPerkNamespace), nameof(Before))),
                finalizer: new HarmonyMethod(AccessTools.Method(typeof(AttritionPerkNamespace), nameof(After))));
        }

        private static void Before(Regiment __instance, Regiment groupunit, out bool __state)
        {
            _depth++;

            // True whenever the loan was *attempted*: if `Lend` throws between
            // two of its writes the finalizer still has to run, and `Restore`
            // decides what to put back from its own flags.
            __state = _depth == 1;

            if (__state)
            {
                FixState.Guard(GuardLabel, () => Lend(__instance, groupunit));
            }
        }

        /// <summary>
        /// A finalizer: the method walks buildings, weather and the commander
        /// pool, and the loans are global prefs.
        /// </summary>
        private static void After(bool __state)
        {
            _depth--;

            if (__state)
            {
                FixState.Guard(GuardLabel, Restore);
            }
        }

        private static void Lend(Regiment brigade, Regiment groupunit)
        {
            // Only the per-brigade call matters. A formation calling this on
            // itself (`UpdateCampaignUnitStats(this, 0)`) returns before the
            // attrition block, and a brigade above type 13 does too.
            if (groupunit == null || ReferenceEquals(groupunit, brigade) || brigade.unittyp > 13)
            {
                return;
            }

            if (FixState.IsOn(AmbulanceCorps))
            {
                float factor = Factor(GamePrefs.woundedtokilledambulancecorps, groupunit, AmbulanceCorpsPerk);
                bool brigadeWouldFire = brigade.HasPerk(AmbulanceCorpsPerk);

                if (factor != 1f || brigadeWouldFire)
                {
                    _savedEncamped = GamePrefs.mortalityratewoundedencamped;
                    _savedNotEncamped = GamePrefs.mortalityratewoundednotencamped;
                    _savedMortalityFactors = GamePrefs.woundedtokilledambulancecorps;

                    GamePrefs.mortalityratewoundedencamped = _savedEncamped * factor;
                    GamePrefs.mortalityratewoundednotencamped = _savedNotEncamped * factor;
                    GamePrefs.woundedtokilledambulancecorps = Ones;

                    _mortalityLent = true;
                }
            }

            if (FixState.IsOn(FootCavalry))
            {
                float factor = Factor(GamePrefs.attritionfootcavalry, groupunit, FootCavalryPerk);
                bool brigadeWouldFire = brigade.HasPerk(FootCavalryPerk);

                if (factor != 1f || brigadeWouldFire)
                {
                    _savedForcedMarch = GamePrefs.sicknessadjustmentforcedmarch;
                    _savedSicknessFactors = GamePrefs.attritionfootcavalry;

                    GamePrefs.sicknessadjustmentforcedmarch = _savedForcedMarch * factor;
                    GamePrefs.attritionfootcavalry = Ones;

                    _sicknessLent = true;
                }
            }
        }

        private static void Restore()
        {
            if (_mortalityLent)
            {
                GamePrefs.mortalityratewoundedencamped = _savedEncamped;
                GamePrefs.mortalityratewoundednotencamped = _savedNotEncamped;

                if (_savedMortalityFactors != null)
                {
                    GamePrefs.woundedtokilledambulancecorps = _savedMortalityFactors;
                    _savedMortalityFactors = null;
                }

                _mortalityLent = false;
            }

            if (_sicknessLent)
            {
                GamePrefs.sicknessadjustmentforcedmarch = _savedForcedMarch;

                if (_savedSicknessFactors != null)
                {
                    GamePrefs.attritionfootcavalry = _savedSicknessFactors;
                    _savedSicknessFactors = null;
                }

                _sicknessLent = false;
            }
        }

        /// <summary>
        /// What the formation is owed: its own level, times its parent's if it
        /// is a Corps under an Army that holds the same perk — the same
        /// own-and-parent product the defective lines build, read from the right
        /// unit.
        /// </summary>
        private static float Factor(float[] array, Regiment formation, int perkid)
        {
            if (array == null || array.Length == 0)
            {
                return 1f;
            }

            float factor = 1f;

            if (formation.HasPerk(perkid))
            {
                factor *= array[Clamp(formation.GetPerkLevel(perkid), array.Length)];
            }

            if (formation.HasPerkParent(perkid))
            {
                factor *= array[Clamp(formation.GetPerkLevelParent(perkid), array.Length)];
            }

            return factor;
        }

        private static int Clamp(int level, int length) =>
            level < 0 ? 0 : (level >= length ? length - 1 : level);
    }
}
