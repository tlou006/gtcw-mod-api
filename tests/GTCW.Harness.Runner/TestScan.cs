using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;

namespace GTCW.Harness.Runner
{
    /// <summary>
    /// Finds the test classes a headless tier must launch a game for, by reading
    /// the tests assembly's metadata: the runner never loads game types.
    /// </summary>
    internal static class TestScan
    {
        public sealed class TestClass
        {
            public string FullName { get; init; } = string.Empty;

            public string Name { get; init; } = string.Empty;

            public string[] Fixes { get; init; } = Array.Empty<string>();

            public List<string> Tests { get; init; } = new List<string>();
        }

        public static List<TestClass> Classes(string testsFolder, string tierAttribute, string? filter)
        {
            string path = Path.Combine(testsFolder, "GTCW.BugFixes.Tests.dll");
            if (!File.Exists(path))
            {
                throw new HarnessException($"tests not built: {path}");
            }

            var found = new List<TestClass>();
            using AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(path);
            foreach (TypeDefinition type in assembly.MainModule.Types.OrderBy(t => t.FullName, StringComparer.Ordinal))
            {
                string[] fixes = type.CustomAttributes
                    .Where(a => a.AttributeType.Name == "FixTestsAttribute")
                    .SelectMany(a => a.ConstructorArguments)
                    .SelectMany(arg => arg.Value is CustomAttributeArgument[] items ? items.Select(i => (string)i.Value) : new[] { (string)arg.Value })
                    .ToArray();

                List<string> tests = type.Methods
                    .Where(m => m.CustomAttributes.Any(a => a.AttributeType.Name == tierAttribute))
                    .Select(m => type.Name + "." + m.Name)
                    .Where(name => Matches(name, fixes, filter))
                    .ToList();

                if (tests.Count > 0)
                {
                    found.Add(new TestClass { FullName = type.FullName, Name = type.Name, Fixes = fixes, Tests = tests });
                }
            }

            return found;
        }

        private static bool Matches(string name, string[] fixes, string? filter) =>
            string.IsNullOrEmpty(filter) ||
            name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
            fixes.Any(f => f.Contains(filter, StringComparison.OrdinalIgnoreCase));
    }
}
