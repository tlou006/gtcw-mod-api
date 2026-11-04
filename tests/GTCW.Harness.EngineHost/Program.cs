using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace GTCW.Harness.EngineHost
{
    /// <summary>
    /// Points the loader at the engine runtime and the tests, then hands over to
    /// <c>GTCW.Harness.Engine.EngineEntry.Run</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>--runtime</c> is the folder the runner builds under <c>tests/obj/</c>:
    /// the publicized game, Unity with every native method stubbed, Unity's
    /// <c>netstandard.dll</c> and BepInEx's core. They must share one folder:
    /// .NET Framework binds a loaded assembly's dependencies from its own folder
    /// before it asks the resolver, so a game assembly sitting next to the real
    /// Unity would bind to the real Unity.
    /// </para>
    /// <para>
    /// <c>--tests</c> is the tests' build output: GTCW.Harness, the tests, the
    /// mod and the library. Every other argument is passed through.
    /// </para>
    /// </remarks>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            string runtime = Argument(args, "--runtime");
            string tests = Argument(args, "--tests");
            if (runtime == null || tests == null)
            {
                Console.Error.WriteLine("usage: GTCW.Harness.EngineHost --runtime <dir> --tests <dir> [harness arguments]");
                return 64;
            }

            AppDomain.CurrentDomain.AssemblyResolve += (_, e) => Resolve(new AssemblyName(e.Name).Name, tests, runtime);
            return Run(args);
        }

        // Kept out of Main: the JIT resolves a method's callees when it compiles
        // the caller, and that must happen after the resolver is registered.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int Run(string[] args)
        {
            MethodInfo entry = Assembly.Load("GTCW.Harness")
                .GetType("GTCW.Harness.Engine.EngineEntry", throwOnError: true)
                .GetMethod("Run", BindingFlags.Public | BindingFlags.Static);
            return (int)entry.Invoke(null, new object[] { args });
        }

        private static Assembly Resolve(string name, params string[] folders)
        {
            foreach (string folder in folders)
            {
                string path = Path.Combine(folder, name + ".dll");
                if (File.Exists(path))
                {
                    // Unsafe only in name: it skips the Mark-of-the-Web refusal
                    // that downloaded BepInEx assemblies otherwise trip.
                    return Assembly.UnsafeLoadFrom(path);
                }
            }

            return null;
        }

        private static string Argument(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                {
                    return Path.GetFullPath(args[i + 1]);
                }
            }

            return null;
        }
    }
}
