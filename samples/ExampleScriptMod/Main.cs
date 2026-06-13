using ASL.Api;

// A script mod is just a .cs file dropped in mods/<YourMod>/ next to a manifest.json with
// "type": "script". ASL compiles it at runtime (Roslyn) — no build setup, no Visual Studio.
// Subclass AslMod exactly like a compiled mod; you get the same IModContext (logger, events, hooks).
public sealed class ExampleScriptMod : AslMod
{
    public override void OnLoad(IModContext ctx)
    {
        ctx.Log.Info("Hello from a SCRIPT mod — compiled at runtime by ASL!");
        ctx.Events.SceneChanged += scene => ctx.Log.Info($"[script] active scene -> {scene}");
    }
}
