# API reference

The public, stable surface lives in **`ASL.API.dll`** (namespace `ASL.Api`). Mods compile
against it; ASL ships it at runtime. Implementation details live in `ASL.dll` and are not part of
the contract.

## `AslMod`

Base class for script and DLL mods.

```csharp
public abstract class AslMod
{
    public abstract void OnLoad(IModContext ctx);  // called once when the mod loads
    public virtual void OnUnload() { }             // called on game shutdown (optional)
}
```

Subclass it (any class name, any/no namespace), override `OnLoad`. ASL finds the first non-abstract
`AslMod` subclass in your assembly, constructs it (parameterless), and calls `OnLoad`.

## `IModContext`

Passed to `OnLoad`. Your handle into ASL.

```csharp
public interface IModContext
{
    string      ModId        { get; }  // "com.author.mymod"
    string      ModName      { get; }  // human-readable
    string      ModDirectory { get; }  // absolute path to your mod folder (load assets here)
    IModLogger  Log          { get; }  // per-mod logger
    IAslEvents  Events       { get; }  // game events
    IModHooks   Hooks        { get; }  // opt-in Harmony hooks
    IModMenu    Menu         { get; }  // in-game menu (F8)
    IAslNet     Net          { get; }  // networking awareness
}
```

## `IModLogger`

```csharp
public interface IModLogger
{
    void Info(string message);
    void Warning(string message);
    void Error(string message);
}
```

Output is tagged with your mod name and lands in `BepInEx\LogOutput.log` (and the console).

## Events

`ctx.Events` (`IAslEvents`). Subscribe in `OnLoad`. Handlers run on the game's main thread.

```csharp
public interface IAslEvents
{
    event Action          Update;             // every frame — keep handlers cheap
    event Action<string>  SceneChanged;       // active scene name changed
    event Action<MetaPlayer> LocalPlayerChanged; // local player set (non-null) or cleared (null)
}
```

- **`Update`** is the per-frame workhorse. It is allocation-free in ASL; do not do heavy work
  every frame.
- **`SceneChanged`** / **`LocalPlayerChanged`** are detected by a lightweight poll (a few times a
  second), so they cost nothing per frame. `LocalPlayerChanged` is debounced to avoid flicker
  during transitions.
- `MetaPlayer` is a game type from `Assembly-CSharp` (namespace `Metater`). Reference that interop
  assembly (DLL mods) — script mods already have it.

Example:
```csharp
ctx.Events.SceneChanged += scene => ctx.Log.Info($"Entered {scene}");
ctx.Events.LocalPlayerChanged += p => { if (p != null) ctx.Log.Info("spawned"); };
```

## Hooks

`ctx.Hooks` (`IModHooks`). **Opt-in** Harmony patches on arbitrary game methods. Unlike events,
nothing is patched until you ask, so unused hooks cost nothing.

```csharp
public interface IModHooks
{
    // Run `after` whenever targetType.methodName returns. `after` gets the instance (null if
    // static) as object — cast it. Returns true if the hook installed.
    bool TryPostfix(Type targetType, string methodName, Action<object> after);
}
```

Example — react after a game method runs:
```csharp
bool ok = ctx.Hooks.TryPostfix(typeof(SomeGameType), "SomeMethod", instance =>
{
    var self = instance as SomeGameType;
    ctx.Log.Info($"SomeMethod ran on {self}");
});
if (!ok) ctx.Log.Warning("hook unavailable on this build");
```

Guidance:
- **Do not hook hot, per-frame methods** — that reintroduces overhead. Prefer events for frequent
  signals.
- Patching is best-effort on IL2CPP. `TryPostfix` returns `false` (and logs why) when the method
  isn't found or the patch can't compile — for example if another plugin has already poisoned the
  same method. Your mod should handle `false` gracefully.
- Each target method is patched once; multiple callbacks on the same method are dispatched in turn,
  each isolated so one throwing callback doesn't stop the others.

## Menu

`ctx.Menu` (`IModMenu`). Register controls into ASL's shared in-game menu, toggled with **F8**.
Controls appear grouped under your mod's name; callbacks run on the main thread.

```csharp
public interface IModMenu
{
    void AddLabel(string text);
    void AddToggle(string label, bool initial, Action<bool> onChanged);
    void AddButton(string label, Action onClick);
    void AddSlider(string label, float min, float max, float initial, Action<float> onChanged);
}
```

Example:
```csharp
ctx.Menu.AddToggle("God mode", false, on => _god = on);
ctx.Menu.AddButton("Give item", () => GiveItem());
ctx.Menu.AddSlider("Speed", 1f, 10f, 5f, v => _speed = v);
```

## Networking

`ctx.Net` (`IAslNet`). Read-only networking awareness (the game uses Mirror).

```csharp
public interface IAslNet
{
    bool IsOnline { get; }            // server and/or client active
    bool IsServer { get; }            // we run the server (host or dedicated)
    bool IsClient { get; }            // we run a client (incl. host's local client)
    bool IsHost  { get; }             // server + client
    bool IsConnectedClient { get; }   // our client is connected
    int  ConnectionCount { get; }     // server-side connected clients
    event Action<int> ConnectionsChanged;   // server-side, fires with the new count
}
```

This is awareness only — ASL does **not yet** provide a custom-message transport (sending data
between clients). See [networking.md](networking.md) for the IL2CPP/Mirror constraint behind that
and the planned approach.

## `AslInfo` (advanced)

`ASL.AslInfo` (in `ASL.dll`) exposes the framework's `Guid` / `Name` / `Version` if you write a
BepInEx plugin that wants to declare `[BepInDependency(AslInfo.Guid)]`. Most mods don't need this —
they live in `mods/` and never touch BepInEx directly.
