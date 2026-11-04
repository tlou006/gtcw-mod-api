using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace GTCW.Harness
{
    /// <summary>One test method in one tier.</summary>
    public sealed class TestCase
    {
        internal TestCase(Type type, MethodInfo method, Tier tier, string[] fixes)
        {
            Type = type;
            Method = method;
            Tier = tier;
            Fixes = fixes;
        }

        public Type Type { get; }

        public MethodInfo Method { get; }

        public Tier Tier { get; }

        public string[] Fixes { get; }

        public string Name => Type.Name + "." + Method.Name;
    }

    /// <summary>Finds tests by attribute, and the catalogued fixes no test covers.</summary>
    public static class TestCatalog
    {
        public const string TestAssemblyName = "GTCW.BugFixes.Tests";

        public static List<TestCase> Find(Assembly tests, Tier tier, string? filter = null, string? className = null)
        {
            var found = new List<TestCase>();
            foreach (Type type in tests.GetTypes().OrderBy(t => t.FullName, StringComparer.Ordinal))
            {
                if (className != null && type.FullName != className)
                {
                    continue;
                }

                string[] fixes = type.GetCustomAttribute<FixTestsAttribute>()?.Ids ?? new string[0];
                foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                             .OrderBy(m => m.MetadataToken))
                {
                    TierTestAttribute? attribute = method.GetCustomAttribute<TierTestAttribute>();
                    if (attribute == null || attribute.Tier != tier)
                    {
                        continue;
                    }

                    var test = new TestCase(type, method, tier, fixes);
                    if (Matches(test, filter))
                    {
                        found.Add(test);
                    }
                }
            }

            return found;
        }

        /// <summary>Test classes with at least one test in the tier, for one-process-per-class tiers.</summary>
        public static List<Type> Classes(Assembly tests, Tier tier, string? filter = null) =>
            Find(tests, tier, filter).Select(t => t.Type).Distinct().ToList();

        /// <summary>
        /// Every fix in the mod's catalogue that no <see cref="FixTestsAttribute"/>
        /// and no <see cref="ManualOnlyAttribute"/> names.
        /// </summary>
        public static List<string> Uncovered(Assembly tests)
        {
            var covered = new HashSet<string>(StringComparer.Ordinal);
            foreach (Type type in tests.GetTypes())
            {
                bool hasTests = type.GetMethods().Any(m => m.GetCustomAttribute<TierTestAttribute>() != null);
                if (hasTests)
                {
                    foreach (string id in type.GetCustomAttribute<FixTestsAttribute>()?.Ids ?? new string[0])
                    {
                        covered.Add(id);
                    }
                }

                foreach (ManualOnlyAttribute manual in type.GetCustomAttributes<ManualOnlyAttribute>())
                {
                    covered.Add(manual.Id);
                }
            }

            return FixControl.AllFixes().Select(f => f.Id).Where(id => !covered.Contains(id)).ToList();
        }

        private static bool Matches(TestCase test, string? filter)
        {
            if (string.IsNullOrEmpty(filter))
            {
                return true;
            }

            return test.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   test.Fixes.Any(id => id.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}
