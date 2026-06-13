using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ASL
{
    /// <summary>
    /// Compiles <c>type: "script"</c> mods (.cs source in the mod folder) into an in-memory
    /// assembly with Roslyn, so modders can write C# without a build setup. The reference set is
    /// everything currently loaded (System.*, ASL.API, …) plus every interop assembly, so scripts
    /// can <c>using ASL.Api;</c> and touch game types just like a compiled mod.
    /// </summary>
    internal static class ScriptLoader
    {
        private static List<MetadataReference> _refs;

        public static Assembly Compile(string assemblyName, string[] sourceFiles, string interopDir, ManualLogSource log)
        {
            var trees = new List<SyntaxTree>();
            foreach (var file in sourceFiles)
                trees.Add(CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file));

            var options = new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release,
                allowUnsafe: true);

            var compilation = CSharpCompilation.Create(assemblyName, trees, GetReferences(interopDir, log), options);

            using var ms = new MemoryStream();
            var result = compilation.Emit(ms);
            if (!result.Success)
            {
                var errors = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
                log.LogError($"[script] '{assemblyName}' failed to compile ({errors.Count} error(s)):");
                foreach (var d in errors.Take(15))
                    log.LogError($"    {d.Id}: {d.GetMessage()} @ {d.Location.GetLineSpan()}");
                return null;
            }

            ms.Position = 0;
            return Assembly.Load(ms.ToArray());
        }

        // Built once: loaded assemblies (runtime + ASL.API) plus all interop assemblies.
        private static List<MetadataReference> GetReferences(string interopDir, ManualLogSource log)
        {
            if (_refs != null) return _refs;

            var byName = new Dictionary<string, MetadataReference>(StringComparer.OrdinalIgnoreCase);

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (asm.IsDynamic) continue;
                    var loc = asm.Location;
                    if (string.IsNullOrEmpty(loc) || !File.Exists(loc)) continue;
                    var key = Path.GetFileName(loc);
                    if (!byName.ContainsKey(key)) byName[key] = MetadataReference.CreateFromFile(loc);
                }
                catch { /* skip unreferenceable assemblies */ }
            }

            if (Directory.Exists(interopDir))
            {
                foreach (var dll in Directory.GetFiles(interopDir, "*.dll"))
                {
                    var key = Path.GetFileName(dll);
                    if (byName.ContainsKey(key)) continue;
                    try { byName[key] = MetadataReference.CreateFromFile(dll); } catch { }
                }
            }

            _refs = byName.Values.ToList();
            log.LogInfo($"[script] compiler reference set: {_refs.Count} assemblies.");
            return _refs;
        }
    }
}
