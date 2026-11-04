using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;

namespace GTCW.Harness.Runner
{
    /// <summary>
    /// Finds framework members the engine tier cannot call.
    /// </summary>
    /// <remarks>
    /// The repo's assemblies target netstandard2.1; the engine tier runs them on
    /// the .NET Framework 4.8 CLR, which implements most of that surface but not
    /// all of it — <c>string.Split(char, ...)</c> compiles, then throws
    /// <c>MissingMethodException</c> at the first call. Resolving every member
    /// reference against the framework's own assemblies before the run turns
    /// that into a report that names the member. The game's Mono implements
    /// netstandard2.1 fully, so this concerns the engine tier only.
    /// </remarks>
    internal static class FrameworkApiCheck
    {
        private static readonly string FrameworkFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Microsoft.NET", "Framework64", "v4.0.30319");

        private static readonly HashSet<string> FrameworkScopes = new HashSet<string>(StringComparer.Ordinal)
        {
            "netstandard", "mscorlib", "System", "System.Core", "System.Runtime",
        };

        /// <summary>Unresolvable members, keyed by the assembly that calls them.</summary>
        public static Dictionary<string, List<string>> Find(string runtimeFolder, string assembliesFolder, IEnumerable<string> assemblies)
        {
            var resolver = new DefaultAssemblyResolver();
            foreach (string d in resolver.GetSearchDirectories())
            {
                resolver.RemoveSearchDirectory(d);
            }

            // Order matters: Unity's netstandard.dll facade from the runtime
            // folder, then the CLR's own mscorlib/System/System.Core — exactly
            // what binds when the engine host runs.
            resolver.AddSearchDirectory(runtimeFolder);
            resolver.AddSearchDirectory(FrameworkFolder);
            resolver.AddSearchDirectory(assembliesFolder);

            var found = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (string name in assemblies)
            {
                string path = Path.Combine(assembliesFolder, name + ".dll");
                if (!File.Exists(path))
                {
                    continue;
                }

                using AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(path, new ReaderParameters { AssemblyResolver = resolver });
                var missing = new SortedSet<string>(StringComparer.Ordinal);
                foreach (MemberReference member in assembly.MainModule.GetMemberReferences())
                {
                    if (member.DeclaringType?.Scope?.Name is not string scope || !FrameworkScopes.Contains(scope))
                    {
                        continue;
                    }

                    // Get/Set/Address on a multi-dimensional array are supplied
                    // by the runtime and have no definition anywhere to resolve.
                    if (member.DeclaringType is ArrayType)
                    {
                        continue;
                    }

                    if (!Resolves(member))
                    {
                        missing.Add(member.FullName);
                    }
                }

                if (missing.Count > 0)
                {
                    found[name] = missing.ToList();
                }
            }

            return found;
        }

        private static bool Resolves(MemberReference member)
        {
            try
            {
                return member switch
                {
                    MethodReference method => method.Resolve() != null,
                    FieldReference field => field.Resolve() != null,
                    _ => true,
                };
            }
            catch (AssemblyResolutionException)
            {
                return false;
            }
        }
    }
}
