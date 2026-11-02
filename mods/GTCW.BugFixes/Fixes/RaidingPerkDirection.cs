using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace GTCW.BugFixes.Fixes
{
    /// <summary>
    /// GTCW-038 and GTCW-039 — Partisan Brigades and Bushwhackers are applied
    /// to the raider instead of to the raided, and therefore <b>one</b> patch.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>Autocalc.UpdateRaiding</c> has two blocks. The first damages the
    /// raided enemy and reads no perk at all. The second inflicts return
    /// casualties and a morale hit on one of the <em>raider's own</em> brigades,
    /// picked from <c>raidingunits[0]</c> — and all three multipliers sit in the
    /// second: Partisan Brigades' skirmishing (×1.25 / 1.5 / 2) and raiding
    /// morale (×1.10 / 1.25 / 1.50), and Bushwhackers' skirmishing
    /// (×1.25 / 1.20 / 1.10). As shipped, both perks make raiding more expensive
    /// for the raider and do nothing whatever to the enemy.
    /// </para>
    /// <para>
    /// The config label settles what was meant: <i>"Raiding morale hit on
    /// <b>enemy</b> partisan brigades"</i>.
    /// </para>
    /// <para>
    /// <b>The casualty half is a loan.</b> The two blocks happen to use
    /// different prefs — <c>casualtyfactorguardingunits</c> for the enemy,
    /// <c>casualtyfactorraidingunits</c> for the raider — so lending the first
    /// the perk factor moves the damage onto the enemy, and lending all-ones
    /// arrays for the perk factors themselves takes the amplification off the
    /// raider. Nothing is recomputed: the game still rolls its own randomness
    /// and picks its own targets.
    /// </para>
    /// <para>
    /// <b>The morale half goes through a chokepoint</b>, because both blocks
    /// scale the same <c>moralelosspercasualtyraiding</c>. A prefix on
    /// <c>SufferMoraleHit</c> scales the hit only when the target is not on the
    /// raiding side, which the raider's own alliance answers directly. The scope
    /// flag is set by the <c>UpdateRaiding</c> prefix and cleared in its
    /// finalizer, so the chokepoint is inert everywhere else — including
    /// <c>UpdateSkirmishing</c> and <c>UpdateTransportInterception</c>, which
    /// call the same helper.
    /// </para>
    /// </remarks>
    internal static class RaidingPerkDirection
    {
        private const string Partisan = "GTCW-038";
        private const string Bushwhackers = "GTCW-039";

        private const string Target = "Autocalc:UpdateRaiding";
        private const string Chokepoint = "Autocalc:SufferMoraleHit";
        private const string GuardLabel = "GTCW-038/039";

        /// <summary>Army perk ids, as the game numbers them.</summary>
        private const int PartisanPerk = 12;
        private const int BushwhackersPerk = 13;

        private static readonly float[] Ones = { 1f, 1f, 1f };

        private static bool _inRaiding;
        private static Autocalc? _scope;

        /// <summary>The morale factor the enemy's hit is owed, or 1.</summary>
        private static float _enemyMorale = 1f;

        /// <summary>The raiding side's alliance, for telling the two apart.</summary>
        private static int _raiderAlliance = -1;

        private static bool _casualtyLent;
        private static float _savedCasualty;

        private static bool _partisanLent;
        private static float[]? _savedPartisanCasualty;
        private static float[]? _savedPartisanMorale;

        private static bool _bushwhackerLent;
        private static float[]? _savedBushwhacker;

        public static FixGroup Group() => new FixGroup(
            Target + " + " + Chokepoint,
            new[]
            {
                new Fix(
                    Partisan,
                    "Partisan Brigades raises the casualties and morale loss a raid inflicts on the enemy. " +
                    "Unfixed it raises the raider's own losses instead and does nothing to the enemy.",
                    FixClass.Code,
                    defaultEnabled: false,
                    SaveSafety.Stateless,
                    "code-findings/34-perks.md §4.13",
                    Probe,
                    family: Fix.Perks),
                new Fix(
                    Bushwhackers,
                    "Bushwhackers' skirmishing bonus is applied to the enemy's casualties. Unfixed it is " +
                    "applied to the raider's own.",
                    FixClass.Code,
                    defaultEnabled: false,
                    SaveSafety.Stateless,
                    "code-findings/34-perks.md §4.14",
                    Probe,
                    family: Fix.Perks),
            },
            Apply);

        private static ProbeResult Probe() => FixProbe.All(
            () => FixProbe.RequireMethod("Autocalc", "UpdateRaiding"),
            () => FixProbe.RequireMethod("Autocalc", "SufferMoraleHit"),
            () => FixProbe.RequireField("Autocalc", "raidingunits"),
            () => FixProbe.RequireField("GamePrefs", "casualtyfactorguardingunits"),
            () => FixProbe.RequireField("GamePrefs", "skirmishingpartisanbrigades"),
            () => FixProbe.RequireField("GamePrefs", "raidingmoralepartisanbrigades"),
            () => FixProbe.RequireField("GamePrefs", "skirmishingbushwhackers"));

        private static void Apply(Harmony harmony)
        {
            MethodInfo raiding = AccessTools.Method(Target)
                ?? throw new InvalidOperationException($"'{Target}' not found");

            harmony.Patch(
                raiding,
                prefix: new HarmonyMethod(AccessTools.Method(typeof(RaidingPerkDirection), nameof(Before))),
                finalizer: new HarmonyMethod(AccessTools.Method(typeof(RaidingPerkDirection), nameof(After))));

            MethodInfo morale = AccessTools.Method(Chokepoint)
                ?? throw new InvalidOperationException($"'{Chokepoint}' not found");

            harmony.Patch(
                morale,
                prefix: new HarmonyMethod(AccessTools.Method(typeof(RaidingPerkDirection), nameof(BeforeMoraleHit))));
        }

        private static void Before(Autocalc __instance, out bool __state)
        {
            // A nested raid update is not a thing the decompile shows, but the
            // loan is computed for one raiding party, so a second one must not
            // overwrite it. The flag is true whenever the loan was *attempted*:
            // if `Lend` throws between two of its writes the finalizer still has
            // to run, and `Restore` decides what to put back from its own flags.
            __state = !_inRaiding;

            if (__state)
            {
                FixState.Guard(GuardLabel, () => Lend(__instance));
            }
        }

        private static void After(bool __state)
        {
            if (!__state)
            {
                return;
            }

            FixState.Guard(GuardLabel, Restore);
        }

        private static void Lend(Autocalc autocalc)
        {
            Regiment? raider = Raider(autocalc);
            if (raider == null)
            {
                return;
            }

            float partisanCasualty = 1f;
            float partisanMorale = 1f;
            float bushwhackerCasualty = 1f;

            if (FixState.IsOn(Partisan))
            {
                partisanCasualty = Factor(GamePrefs.skirmishingpartisanbrigades, raider, PartisanPerk);
                partisanMorale = Factor(GamePrefs.raidingmoralepartisanbrigades, raider, PartisanPerk);
            }

            if (FixState.IsOn(Bushwhackers))
            {
                bushwhackerCasualty = Factor(GamePrefs.skirmishingbushwhackers, raider, BushwhackersPerk);
            }

            float enemyCasualty = partisanCasualty * bushwhackerCasualty;

            // Nothing to move: the raider holds neither perk, or a data mod has
            // set the factors to 1.
            if (enemyCasualty == 1f && partisanMorale == 1f)
            {
                return;
            }

            _inRaiding = true;
            _scope = autocalc;
            _raiderAlliance = raider.alliance;
            _enemyMorale = partisanMorale;

            if (enemyCasualty != 1f)
            {
                _savedCasualty = GamePrefs.casualtyfactorguardingunits;
                GamePrefs.casualtyfactorguardingunits = _savedCasualty * enemyCasualty;
                _casualtyLent = true;
            }

            // The raider's own block reads these three arrays and nothing else
            // does, so all-ones takes the amplification off the raider without
            // touching anything the enemy's block needs.
            if (FixState.IsOn(Partisan))
            {
                _savedPartisanCasualty = GamePrefs.skirmishingpartisanbrigades;
                _savedPartisanMorale = GamePrefs.raidingmoralepartisanbrigades;
                GamePrefs.skirmishingpartisanbrigades = Ones;
                GamePrefs.raidingmoralepartisanbrigades = Ones;
                _partisanLent = true;
            }

            if (FixState.IsOn(Bushwhackers))
            {
                _savedBushwhacker = GamePrefs.skirmishingbushwhackers;
                GamePrefs.skirmishingbushwhackers = Ones;
                _bushwhackerLent = true;
            }
        }

        private static void Restore()
        {
            if (_casualtyLent)
            {
                GamePrefs.casualtyfactorguardingunits = _savedCasualty;
                _casualtyLent = false;
            }

            if (_partisanLent)
            {
                if (_savedPartisanCasualty != null)
                {
                    GamePrefs.skirmishingpartisanbrigades = _savedPartisanCasualty;
                }

                if (_savedPartisanMorale != null)
                {
                    GamePrefs.raidingmoralepartisanbrigades = _savedPartisanMorale;
                }

                _savedPartisanCasualty = null;
                _savedPartisanMorale = null;
                _partisanLent = false;
            }

            if (_bushwhackerLent)
            {
                if (_savedBushwhacker != null)
                {
                    GamePrefs.skirmishingbushwhackers = _savedBushwhacker;
                }

                _savedBushwhacker = null;
                _bushwhackerLent = false;
            }

            _inRaiding = false;
            _scope = null;
            _raiderAlliance = -1;
            _enemyMorale = 1f;
        }

        /// <summary>
        /// The raid's owner — the formation whose perks the raid should use.
        /// </summary>
        private static Regiment? Raider(Autocalc autocalc)
        {
            Autocalc.EnvolvedUnits[] sides = autocalc.raidingunits;
            if (sides == null || sides.Length < 1 || sides[0] == null)
            {
                return null;
            }

            List<Regiment> top = sides[0].topunits;
            return top == null || top.Count < 1 ? null : top[0];
        }

        /// <summary>
        /// The factor the defective block would have built: own level, times
        /// the parent's if the holder is a Corps under a holder.
        /// </summary>
        private static float Factor(float[] array, Regiment raider, int perkid)
        {
            if (array == null || array.Length == 0)
            {
                return 1f;
            }

            float factor = 1f;

            if (raider.HasPerk(perkid))
            {
                factor *= array[Clamp(raider.GetPerkLevel(perkid), array.Length)];
            }

            if (raider.HasPerkParent(perkid))
            {
                factor *= array[Clamp(raider.GetPerkLevelParent(perkid), array.Length)];
            }

            return factor;
        }

        private static int Clamp(int level, int length) =>
            level < 0 ? 0 : (level >= length ? length - 1 : level);

        /// <summary>
        /// Gives the enemy the morale hit Partisan Brigades promises, and leaves
        /// the raider's own alone.
        /// </summary>
        private static void BeforeMoraleHit(Autocalc __instance, Regiment unit1, ref float moraleeffect)
        {
            if (!_inRaiding ||
                _enemyMorale == 1f ||
                unit1 == null ||
                !ReferenceEquals(__instance, _scope))
            {
                return;
            }

            // The second block's target is one of the raider's own brigades, so
            // alliance is the discriminator: the two sides of a raid are at war
            // by the time this method is reached.
            //
            // Multiplying rather than adding also keeps the loan sign-agnostic.
            // `SufferMoraleHit` adds its argument to morale, so the shipped
            // `moralelosspercasualtyraiding` is negative; a factor above 1
            // deepens the hit whichever way the data is authored.
            if (unit1.alliance == _raiderAlliance)
            {
                return;
            }

            moraleeffect *= _enemyMorale;
        }
    }
}
