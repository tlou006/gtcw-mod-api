using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace GTCW.Harness.Runner
{
    /// <summary>
    /// Builds the folder the engine tier loads the game from: the game's own
    /// assemblies, BepInEx's core, and Unity rewritten so that no method is
    /// native.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Outside the engine, every Unity method implemented in native code throws
    /// on the .NET Framework ("ECall methods must be packaged into a system
    /// module") and Harmony cannot even compile a patch for a method that calls
    /// one. Giving each a managed body that returns its type's default removes
    /// the whole class of problem; tests then stand in for the few engine
    /// values they need.
    /// </para>
    /// <para>
    /// Lives under <c>tests/obj/EngineRuntime/&lt;key&gt;</c>, like the
    /// Publicizer's output: a local artifact derived from the install, never in
    /// a <c>bin/</c>, never committed. The key covers the inputs, so a game
    /// update or a generator change builds a new folder.
    /// </para>
    /// </remarks>
    internal static class EngineRuntime
    {
        /// <summary>Bump when the generated output would change.</summary>
        private const int GeneratorVersion = 1;

        private const string CompleteMarker = ".complete";

        public static string Ensure(Settings settings, Action<string> log)
        {
            string key = Key(settings);
            string root = Path.Combine(settings.RepoRoot, "tests", "obj", "EngineRuntime");
            string folder = Path.Combine(root, key);
            if (File.Exists(Path.Combine(folder, CompleteMarker)))
            {
                return folder;
            }

            log($"building engine runtime {key} from {settings.Managed}");
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }

            Directory.CreateDirectory(folder);
            int stubbed = 0;

            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(settings.Managed);
            foreach (string path in Directory.GetFiles(settings.Managed, "*.dll"))
            {
                string name = Path.GetFileName(path);
                if (name.StartsWith("UnityEngine", StringComparison.Ordinal))
                {
                    stubbed += Stub(path, Path.Combine(folder, name), resolver);
                }
                else if (!IsRuntimeLibrary(name))
                {
                    File.Copy(path, Path.Combine(folder, name));
                }
            }

            foreach (string path in Directory.GetFiles(settings.BepInExCore, "*.dll"))
            {
                File.Copy(path, Path.Combine(folder, Path.GetFileName(path)), overwrite: true);
            }

            File.WriteAllText(Path.Combine(folder, CompleteMarker), $"stubbed {stubbed} native methods{Environment.NewLine}");
            log($"engine runtime ready: {stubbed} native methods stubbed");
            return folder;
        }

        /// <summary>
        /// The CLR's own libraries stay the CLR's. Unity's <c>netstandard.dll</c>
        /// is kept: it is the facade the repo's netstandard2.1 assemblies bind to.
        /// </summary>
        private static bool IsRuntimeLibrary(string name) =>
            name == "mscorlib.dll" ||
            name.StartsWith("System", StringComparison.Ordinal) ||
            name.StartsWith("Mono.", StringComparison.Ordinal) ||
            name.StartsWith("Microsoft.", StringComparison.Ordinal);

        private static int Stub(string source, string destination, IAssemblyResolver resolver)
        {
            int count = 0;
            using AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(
                source, new ReaderParameters { AssemblyResolver = resolver, InMemory = true });
            foreach (TypeDefinition type in AllTypes(assembly.MainModule.Types))
            {
                foreach (MethodDefinition method in type.Methods)
                {
                    if (StubMethod(method))
                    {
                        count++;
                    }
                }
            }

            assembly.Write(destination);
            return count;
        }

        private static IEnumerable<TypeDefinition> AllTypes(IEnumerable<TypeDefinition> types)
        {
            foreach (TypeDefinition type in types)
            {
                yield return type;
                foreach (TypeDefinition nested in AllTypes(type.NestedTypes))
                {
                    yield return nested;
                }
            }
        }

        private static bool StubMethod(MethodDefinition method)
        {
            // Delegate Invoke and constructors are supplied by the runtime.
            if (method.IsAbstract || method.IsRuntime || method.HasBody)
            {
                return false;
            }

            if (!method.IsInternalCall && !method.IsPInvokeImpl)
            {
                return false;
            }

            // In this order: Cecil's PInvokeInfo setter turns IsPInvokeImpl back on.
            method.IsInternalCall = false;
            method.PInvokeInfo = null;
            method.IsPInvokeImpl = false;
            method.IsPreserveSig = false;
            method.IsIL = true;
            method.IsManaged = true;
            method.Body = new MethodBody(method);

            ILProcessor il = method.Body.GetILProcessor();
            TypeReference type = method.ReturnType;
            switch (type.MetadataType)
            {
                case MetadataType.Void:
                    break;
                case MetadataType.Boolean:
                case MetadataType.Char:
                case MetadataType.SByte:
                case MetadataType.Byte:
                case MetadataType.Int16:
                case MetadataType.UInt16:
                case MetadataType.Int32:
                case MetadataType.UInt32:
                    il.Emit(OpCodes.Ldc_I4_0);
                    break;
                case MetadataType.Int64:
                case MetadataType.UInt64:
                    il.Emit(OpCodes.Ldc_I8, 0L);
                    break;
                case MetadataType.Single:
                    il.Emit(OpCodes.Ldc_R4, 0f);
                    break;
                case MetadataType.Double:
                    il.Emit(OpCodes.Ldc_R8, 0d);
                    break;
                case MetadataType.IntPtr:
                case MetadataType.UIntPtr:
                case MetadataType.Pointer:
                case MetadataType.ByReference:
                    il.Emit(OpCodes.Ldc_I4_0);
                    il.Emit(OpCodes.Conv_I);
                    break;
                case MetadataType.ValueType:
                case MetadataType.Var:
                case MetadataType.MVar:
                case MetadataType.GenericInstance:
                    if (type.IsValueType || type.IsGenericParameter)
                    {
                        var local = new VariableDefinition(type);
                        method.Body.Variables.Add(local);
                        method.Body.InitLocals = true;
                        il.Emit(OpCodes.Ldloca, local);
                        il.Emit(OpCodes.Initobj, type);
                        il.Emit(OpCodes.Ldloc, local);
                    }
                    else
                    {
                        il.Emit(OpCodes.Ldnull);
                    }

                    break;
                default:
                    il.Emit(OpCodes.Ldnull);
                    break;
            }

            il.Emit(OpCodes.Ret);
            return true;
        }

        private static string Key(Settings settings)
        {
            var inputs = new StringBuilder($"v{GeneratorVersion}");
            foreach (string path in Directory.GetFiles(settings.Managed, "*.dll").OrderBy(p => p, StringComparer.Ordinal))
            {
                var info = new FileInfo(path);
                inputs.Append('|').Append(info.Name).Append(':').Append(info.Length).Append(':').Append(info.LastWriteTimeUtc.Ticks);
            }

            foreach (string path in Directory.GetFiles(settings.BepInExCore, "*.dll").OrderBy(p => p, StringComparer.Ordinal))
            {
                var info = new FileInfo(path);
                inputs.Append('|').Append(info.Name).Append(':').Append(info.Length);
            }

            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(inputs.ToString()))).Substring(0, 16);
        }
    }
}
