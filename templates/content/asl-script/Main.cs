using ASL.Api;
using UnityEngine;

// A script mod is a plain .cs file next to a manifest.json with "type": "script". ASL compiles it at
// runtime (Roslyn), so there is no project file and no build step. Subclass AslMod as in a compiled mod.
public sealed class MyMod : AslMod
{
    public override void OnLoad(IModContext ctx)
    {
        IUiWindow win = ctx.Ui.CreateWindow("main", "MyMod");   // hidden until opened
        win.AddHeader("Hello from a script mod");
        win.AddButton("Show a toast", () => ctx.Ui.Toast("Toast from MyMod!", UiToastKind.Success), UiButtonStyle.Pill(UiVariant.Success));
        win.BindToggleKey(ctx.Input.RegisterKey("window", "Toggle MyMod window", KeyCode.F6));

        ctx.Log.Info("MyMod script loaded. Press F6.");
    }
}
