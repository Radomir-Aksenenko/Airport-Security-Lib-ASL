// The real UnityEngine.dll is (almost) only type-forwards to the *Module assemblies; the stubs put types
// directly in their modules, so this assembly is empty. Exists so `<Reference Include="UnityEngine">` resolves.
namespace UnityEngine.Stub { internal static class Marker { } }
