using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace GTCW.Harness.Game
{
    /// <summary>
    /// Finding and temporarily rearranging live units, for tests that need a
    /// unit in a particular place or with a particular perk. Every change made
    /// through here is undone by <see cref="Restore"/>, which the game runner
    /// also calls after each test.
    /// </summary>
    public static class Units
    {
        private static readonly List<Action> Undo = new List<Action>();

        /// <summary>
        /// A campaign formation of one side with brigades attached. When none
        /// matches, the failure describes the candidates, so the test's
        /// assumption about the scenario can be corrected from the report.
        /// </summary>
        public static Regiment Formation(int alliance, Func<Regiment, bool>? also = null)
        {
            List<Regiment> candidates = BattleUnits.completeunitlist
                .Where(u => u != null && u.alliance == alliance && u.unittyp >= 14 && u.unittyp != 17 && Brigades(u).Length > 0)
                .ToList();
            Regiment? found = candidates.FirstOrDefault(u => also == null || also(u));
            if (found != null)
            {
                return found;
            }

            IEnumerable<string> described = candidates
                .OrderByDescending(u => u.gameObject.activeInHierarchy).ThenByDescending(u => Brigades(u).Any(b => b != null && b.unittyp <= 1))
                .Take(8)
                .Select(u => $"{u.name}(type {u.unittyp}, {(u.gameObject.activeInHierarchy ? "active" : "inactive")}" +
                             $"{(u.inbattle ? ", in battle" : "")}, raid {u.groupraidingefficiency:0}; brigades " +
                             string.Join("/", Brigades(u).Where(b => b != null).Take(4).Select(b => $"t{b.unittyp}:{b.strength}")) + ")");
            throw new ExpectationFailedException(
                $"no formation of alliance {alliance} matches; {candidates.Count} have brigades " +
                $"({candidates.Count(u => u.gameObject.activeInHierarchy)} active): " + string.Join("; ", described));
        }

        /// <summary>
        /// A formation's brigades, asked for exactly as <c>UpdateIIPSupplyLink</c>
        /// and <c>UpdateCampaignUnitStats</c>'s caller ask for them.
        /// </summary>
        public static Regiment[] Brigades(Regiment formation) =>
            formation.GetAttachedUnitsReg(excludedechainedunits: true, excludeskirmishers: true, 13, directonly: false,
                includenonactiveunits: true, includebasicgarrisons: true, undersamecampaignunitonly: true)
            ?? new Regiment[0];

        /// <summary>
        /// Moves a unit's transform, refreshing the position the unit caches per
        /// frame. Undone by <see cref="Restore"/>.
        /// </summary>
        public static void MoveTo(Regiment unit, Vector3 position)
        {
            Vector3 home = unit.transform.position;
            Undo.Add(() =>
            {
                unit.transform.position = home;
                Traverse.Create(unit).Field("lastpositionupdateframe").SetValue(-1);
            });
            unit.transform.position = position;
            Traverse.Create(unit).Field("lastpositionupdateframe").SetValue(-1);
        }

        /// <summary>
        /// Gives a unit exactly one perk at a level (0 = I), or none with
        /// a perk id of -1, hiding whatever it held. Undone by
        /// <see cref="Restore"/>.
        /// </summary>
        public static void SetPerks(Regiment unit, params (int perk, int level)[] perks)
        {
            List<Regiment.Perk> original = unit.perks;
            Undo.Add(() => unit.perks = original);
            unit.perks = perks.Where(p => p.perk >= 0).Select(p => new Regiment.Perk { perk = p.perk, perklevel = p.level }).ToList();
        }

        /// <summary>
        /// Puts two sides at war for the test. Scenario 001/A opens in February
        /// 1861, before Fort Sumter, when nobody is yet at war. Undone by
        /// <see cref="Restore"/>.
        /// </summary>
        public static void AtWar(int side, int enemy)
        {
            foreach ((int a, int b) in new[] { (side, enemy), (enemy, side) })
            {
                List<int> wars = GameVars.alliance[a].atwarwith;
                if (!wars.Contains(b))
                {
                    wars.Add(b);
                    Undo.Add(() => wars.Remove(b));
                }
            }
        }

        /// <summary>A trade node owned by one side, with the most raidable stock.</summary>
        public static IIP RichestNode(int owner) =>
            BattleUnits.iips
                .Where(i => i != null && i.allianceowner == owner && i.unitsinrange != null)
                .OrderByDescending(i => Economy.GetResourcesCost(GamePrefs.raidvolume, i.transform.position, owner, i))
                .FirstOrDefault()
            ?? throw new ExpectationFailedException($"no trade node owned by alliance {owner}");

        /// <summary>
        /// Remembers every instance field of an object, shallowly, to put back
        /// afterwards: arrays are cloned so element writes are undone too.
        /// </summary>
        public static void KeepFields(object target)
        {
            var saved = new List<(System.Reflection.FieldInfo field, object? value)>();
            for (Type? t = target.GetType(); t != null && t != typeof(UnityEngine.Object) && t != typeof(object); t = t.BaseType)
            {
                foreach (System.Reflection.FieldInfo field in t.GetFields(System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly))
                {
                    object? value = field.GetValue(target);
                    saved.Add((field, value is Array array ? array.Clone() : value));
                }
            }

            Undo.Add(() =>
            {
                foreach ((System.Reflection.FieldInfo field, object? value) in saved)
                {
                    field.SetValue(target, value is Array array ? array.Clone() : value);
                }
            });
        }

        /// <summary>Remembers a value-type field to put back afterwards.</summary>
        public static void Keep<T>(Func<T> get, Action<T> set)
        {
            T value = get();
            Undo.Add(() => set(value));
        }

        /// <summary>Puts back everything changed through this class, newest first.</summary>
        public static void Restore()
        {
            for (int i = Undo.Count - 1; i >= 0; i--)
            {
                Undo[i]();
            }

            Undo.Clear();
        }
    }
}
