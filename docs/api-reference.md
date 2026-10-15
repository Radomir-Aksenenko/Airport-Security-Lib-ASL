# API reference

The public surface lives in **`ASL.API.dll`** (namespace `ASL.Api`). Mods compile against it; ASL ships it at runtime.
Implementation details live in `ASL.dll` and are not part of the contract. Every public member carries XML doc comments,
so your IDE shows the same text as this page.

> **Status.** The 0.1 surface (`AslMod.OnLoad/OnUnload`, `IModContext` members `ModId`..`Net`, events `Update`/`SceneChanged`/
> `LocalPlayerChanged`, `Hooks`, the 0.1 `Menu` members, `Announce`, `Input`, `Net`) is ✅ verified in-game. Everything marked
> **(2.0)** is 🧪 implemented but not yet verified in-game. 2.0 only *adds* to the API; nothing from 0.1 changed
> ([upgrading](upgrading-to-2.0.md)). Each 2.0 area has its own guide; this page lists the members and links there.

| Guide | Covers |
|---|---|
| [ui.md](ui.md) | `ctx.Ui`, windows, widgets, themes, JSON layouts, `ctx.Menu` pages |
| [config-and-storage.md](config-and-storage.md) | `ctx.Config`, `ctx.Storage` |
| [timers-and-events.md](timers-and-events.md) | `ctx.Events`, `ctx.Timers`, profiler |
| [console.md](console.md) | `ctx.Commands`, the dev console |
| [mod-manager.md](mod-manager.md) | `ctx.Mods`, `ctx.Info`, dependencies, states, hot reload |
| [assets-and-localization.md](assets-and-localization.md) | `ctx.Assets`, `ctx.Loc`, content audio/strings |
| [networking.md](networking.md) | `ctx.Net` |

## `AslMod`

Base class for script and DLL mods.

```csharp
public abstract class AslMod
{
    public abstract void OnLoad(IModContext ctx);  // called once when the mod loads
    public virtual void OnAllModsLoaded() { }       // (2.0) after EVERY mod's OnLoad has run
    public virtual void OnUnload() { }              // on game shutdown, unload or hot reload (optional)
}
```

Subclass it (any class name, any/no namespace), override `OnLoad`. ASL finds the first non-abstract
`AslMod` subclass in your assembly, constructs it (parameterless), and calls `OnLoad`. Use `OnAllModsLoaded` to look up
services other mods published without worrying about load order. `OnUnload` runs in reverse load order; after it
everything your mod created through `ctx` is released automatically.

## `AslVersion` (2.0)

```csharp
public static class AslVersion
{
    public const string Current = "2.0.0";
    public static int Major { get; }
    public static int Minor { get; }
    public static int Patch { get; }
    public static bool AtLeast(string version);      // false if the string cannot be parsed
}
```

```csharp
if (AslVersion.AtLeast("2.0.0")) { /* safe to use the UI toolkit */ }
```

`SemVersion` and `VersionRange` (also in `ASL.Api`) parse and compare versions and ranges such as `^1.2.0` for your own use
([ranges](mod-manager.md#version-ranges)).

## `IModContext`

Passed to `OnLoad`. Your handle into ASL. Every member is non-null for DLL and script mods.

```csharp
public interface IModContext
{
    string        ModId        { get; }  // "com.author.mymod"
    string        ModName      { get; }  // human-readable
    string        ModDirectory { get; }  // absolute path to your mod folder
    IModLogger    Log          { get; }  // per-mod logger
    IAslEvents    Events       { get; }  // game events, per-mod isolated      (see timers-and-events.md)
    IModHooks     Hooks        { get; }  // opt-in Harmony hooks
    IModMenu      Menu         { get; }  // in-game menu (F8)
    IAslUi        Ui           { get; }  // windows, HUDs, toasts, dialogs, announce
    IAslInput     Input        { get; }  // keyboard input + rebindable, conflict-checked keybinds
    IAslNet       Net          { get; }  // networking: awareness + message transport
    // --- new in 2.0 ---
    IAslConfig    Config       { get; }  // typed persisted settings, auto-shown in F8
    IAslStorage   Storage      { get; }  // per-mod JSON save data
    IAslTimers    Timers       { get; }  // delays, intervals, conditions, coroutines
    IAslMods      Mods         { get; }  // other mods: info, services, messages
    IAslCommands  Commands     { get; }  // dev-console commands
    IAslAssets    Assets       { get; }  // textures, sprites, audio, bundles, text from the mod folder
    IAslLocalization Loc       { get; }  // translations from lang/*.json
    IModInfo      Info         { get; }  // this mod's own manifest info and state
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

`ctx.Events` (`IAslEvents`). Subscribe in `OnLoad`. Handlers run on the game's main thread, each mod's inside its own
try/catch. Full guide: [timers-and-events.md](timers-and-events.md#events-ctxevents).

```csharp
public interface IAslEvents
{
    event Action             Update;               // every frame - keep handlers cheap
    event Action<string>     SceneChanged;         // active scene name changed
    event Action<MetaPlayer> LocalPlayerChanged;   // local player set (non-null) or cleared (null)
    event Action             LateUpdate;           // (2.0) every frame, after Update
    event Action<bool>       MenuVisibilityChanged; // (2.0) F8 menu shown / hidden
    event Action             AllModsLoaded;        // (2.0) once, after every mod's OnLoad
    event Action<string>     LanguageChanged;      // (2.0) the player changed the game language
    event Action             GameQuitting;         // (2.0) best effort, at most once
}
```

- **`Update`** is the per-frame workhorse. It is allocation-free in ASL; do not do heavy work every frame.
- **`SceneChanged`** / **`LocalPlayerChanged`** are detected by a lightweight poll (a few times a second), so they cost
  nothing per frame. `LocalPlayerChanged` is debounced to avoid flicker during transitions.
- An exception in your `Update`/`LateUpdate` handler is logged and never reaches other mods. After 10 failing frames in a
  row the handler is retried with backoff (0.5 s ... 5 s) and returns to every frame on the first success or on a scene /
  local-player change. It is never switched off permanently.
- `MetaPlayer` is a game type from `Assembly-CSharp` (namespace `Metater`). Reference that interop assembly (DLL mods);
  script mods already have it.
- Game-specific events (NPC spawned, round start/end, contraband scans) are not in 2.0; use [`Hooks`](#hooks).

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

`ctx.Menu` (`IModMenu`). Register controls into ASL's shared in-game menu. Players open it with
**F8** or via the **Mods** button ASL adds to the game's main menu. Controls appear grouped under
your mod's name; callbacks run on the main thread.

```csharp
public interface IModMenu
{
    void AddLabel(string text);
    void AddToggle(string label, bool initial, Action<bool> onChanged);
    void AddButton(string label, Action onClick);
    void AddSlider(string label, float min, float max, float initial, Action<float> onChanged);
    // --- new in 2.0 ---
    void AddPage(string title, Action<IUiContainer> build);   // a row that opens a rich sub-page built with the UI toolkit
    void AddWindowButton(string label, IUiWindow window);     // a row that opens/toggles a window
    void AddSeparator(string title = null);                   // visual group header row
}
```

Example:
```csharp
ctx.Menu.AddToggle("God mode", false, on => _god = on);
ctx.Menu.AddButton("Give item", () => GiveItem());
ctx.Menu.AddSlider("Speed", 1f, 10f, 5f, v => _speed = v);

ctx.Menu.AddSeparator("Advanced");                        // (2.0)
ctx.Menu.AddPage("Debug tools", page => { page.AddHeader("Debug"); page.AddToggle("Verbose", false, on => _verbose = on); });
```

The 0.1 rows render exactly as before. If you bind settings with `ctx.Config`, a **Settings** page is added to your mod's F8 page
automatically ([config](config-and-storage.md#the-automatic-settings-page)). More in [ui.md](ui.md#f8-menu-pages).

## On-screen UI

`ctx.Ui` (`IAslUi`). `Announce` drives the game's own banner (✅ 0.1); everything else is the 2.0 UI toolkit. Full guide with
every widget: **[ui.md](ui.md)**.

```csharp
public interface IAslUi
{
    void Announce(string text, float seconds = 2.5f);   // the game's on-screen announcement banner (0.1)

    // --- new in 2.0 ---
    IUiWindow CreateWindow(string id, string title, UiWindowOptions options = null);   // hidden until Open()
    IUiWindow CreateHud(string id, UiAnchor anchor, UiWindowOptions options = null);   // visible overlay, click-through (honours Width/Height/OffsetX/OffsetY/Theme/Padding/SortOrder)
    IUiWindow GetWindow(string id);                                                    // this mod's window by id, or null
    void Toast(string text, UiToastKind kind = UiToastKind.Info, float seconds = 3f);
    void Confirm(string title, string message, Action onYes, Action onNo = null, string yesText = "Yes", string noText = "No");
    void Prompt(string title, string message, string initial, Action<string> onSubmit, Action onCancel = null);
    void Alert(string title, string message, Action onClose = null);
    IUiWindow LoadLayout(string relativeJsonPath);       // window from a JSON layout (null + logged error on failure)
    void RegisterAction(string name, Action action);     // named actions for JSON layouts
    void RegisterAction(string name, Action<string> action);
    UiTheme Theme { get; set; }                          // this mod's default theme for new windows
    bool IsTextInputFocused { get; }                     // an ASL text field has keyboard focus
}
```

```csharp
ctx.Ui.Announce("Round starting!");
ctx.Ui.Announce("You win!", 4f);                         // safe before a level loads (logs instead of throwing)

var win = ctx.Ui.CreateWindow("main", "My Mod");
win.AddHeader("Hello");
win.AddButton("Hi", () => ctx.Ui.Toast("Hi!", UiToastKind.Success), UiButtonStyle.Pill(UiVariant.Success));
win.BindToggleKey(ctx.Input.RegisterKey("window", "Toggle window", KeyCode.F6));
```

Toolkit types, all in `ASL.Api`:

| Group | Types |
|---|---|
| Window and elements | `IUiWindow`, `IUiContainer`, `IUiElement`, `IUiText`, `IUiButton`, `IUiToggle`, `IUiSlider`, `IUiInput`, `IUiDropdown`, `IUiProgress`, `IUiImage`, `IUiTabs` |
| Options and styling | `UiWindowOptions`, `UiButtonStyle`, `UiTheme` |
| Enums | `UiShape`, `UiVariant`, `UiSize`, `UiTextStyle`, `UiTextAlign`, `UiToggleStyle`, `UiToastKind`, `UiAnchor` |

## Input & keybinds

`ctx.Input` (`IAslInput`). The recommended path is a **named keybind**: it shows up automatically in
the F8 menu under your mod, the player can **rebind** it, the choice is **saved across restarts**, and
ASL keeps it from **clashing** with other mods' keys (and warns on keys the game itself uses). There
are also raw passthrough queries for quick throwaway keys.

```csharp
public interface IAslInput
{
    IAslKeybind RegisterKey(string id, string displayName, KeyCode defaultKey);
    bool GetKeyDown(KeyCode key);   // raw passthrough, no registration / conflict checks; NOT muted while typing in a UI text field
    bool GetKey(KeyCode key);
    bool GetKeyUp(KeyCode key);
}

public interface IAslKeybind
{
    string  Id          { get; }   // your id, namespaced by mod
    string  DisplayName { get; }    // shown in the Controls UI
    KeyCode Key         { get; }    // current binding (after any rebind); None if unbound
    bool    WasPressed  { get; }    // true on the frame it went down
    bool    IsHeld      { get; }    // true while held
    bool    WasReleased { get; }    // true on the frame it was released
    bool    HasConflict { get; }    // clashes with another mod's key or one the game uses
    event Action Pressed;           // fires once when it goes down
}
```

```csharp
private IAslKeybind _disguise;

public override void OnLoad(IModContext ctx)
{
    _disguise = ctx.Input.RegisterKey("disguise", "Disguise / undisguise", KeyCode.B);

    _disguise.Pressed += () => ToggleDisguise();                        // event style, or…
    ctx.Events.Update += () => { if (_disguise.WasPressed) ToggleDisguise(); };  // …poll in Update
}
```

How conflicts are handled:
- **Mod vs mod** — a rebind onto a key another mod already uses is **rejected** (the old binding
  stays); the menu and `HasConflict` make it visible.
- **Mod vs game** — binding onto a key the game uses (movement, jump, …) is **allowed but flagged**
  (`HasConflict`, a warning in the log), since it may double-trigger. The reserved-game-key set is a
  conservative best-effort list (WASD, arrows, Space, Shift, Ctrl, mouse, Tab, Esc).
- Saved rebinds survive while the owning mod is disabled or not loaded (they are kept in the file and applied when it loads again).
- The player rebinds in the **F8 menu**: open your mod, click the keybind row, press a key (Esc
  cancels). Bindings persist to `BepInEx/config/ASL.Keybinds.cfg` (editable by hand too).

Raw `GetKeyDown` / `GetKey` / `GetKeyUp` read the key straight from Unity, so they are **not** suppressed while the
player types in a toolkit text field. Prefer `RegisterKey` (muted while typing) or check `ctx.Ui.IsTextInputFocused`.


## Config, storage, timers, mods, commands, assets, localization (2.0)

Each service is summarised here; the linked guide has behaviour, limits and more examples.

### `ctx.Config` - [guide](config-and-storage.md)

```csharp
public interface IAslConfig
{
    IConfigEntry<T>     Bind<T>(string section, string key, T defaultValue, string description = null);   // bool,int,float,double,string,enum,KeyCode
    IConfigEntry<float> BindRange(string section, string key, float defaultValue, float min, float max, string description = null);
    IConfigEntry<int>   BindRange(string section, string key, int defaultValue, int min, int max, string description = null);
    bool   ShowInMenu { get; set; }   // default true: entries appear on the mod's F8 Settings page
    string FilePath   { get; }        // BepInEx/config/<modId>.cfg
    void   Save();
    void   Reload();
    event  Action Changed;
}

public interface IConfigEntry<T>
{
    string Section { get; }  string Key { get; }  string Description { get; }
    T Value { get; set; }    T DefaultValue { get; }          // setting Value saves and raises Changed
    event Action<T> Changed;
    void Reset();
}
```

### `ctx.Storage` - [guide](config-and-storage.md#storage-ctxstorage)

```csharp
public interface IAslStorage
{
    string DataDirectory { get; }                  // BepInEx/config/ASL/data/<modId>/
    void   Save<T>(string name, T value);          // JSON, atomic write
    T      Load<T>(string name, T fallback = default);
    bool   Exists(string name);
    void   Delete(string name);
    void   SaveText(string name, string text);
    string LoadText(string name, string fallback = null);
}
```

### `ctx.Timers` - [guide](timers-and-events.md#timers-ctxtimers)

```csharp
public interface IAslTimers
{
    IAslTimer After(float seconds, Action action, bool unscaled = true);
    IAslTimer Every(float seconds, Action action, bool unscaled = true);
    IAslTimer NextFrame(Action action);
    IAslTimer When(Func<bool> condition, Action action, float timeoutSeconds = 0f);
    IAslTimer Start(System.Collections.IEnumerator routine);   // yield null / seconds / Wait.Seconds|Until|While|Frames
    void CancelAll();
}
public interface IAslTimer { bool IsActive { get; } void Cancel(); }
public static class Wait { /* Seconds(float), Until(Func<bool>), While(Func<bool>), Frames(int) -> IAslWait */ }
```

### `ctx.Mods` and `ctx.Info` - [guide](mod-manager.md)

```csharp
public enum ModState { Loaded, Failed, Disabled, MissingDependency, Incompatible }

public interface IModInfo
{
    string Id { get; }    string Name { get; }    string Version { get; }    string Author { get; }
    string Description { get; }
    string Kind { get; }                  // "dll" | "script" | "content"
    string Directory { get; }
    ModState State { get; }
    string Error { get; }                 // null when fine
    IReadOnlyList<string> Dependencies { get; }   // as written in the manifest
    IReadOnlyList<string> Tags { get; }
}

public interface IAslMods
{
    IReadOnlyList<IModInfo> All { get; }
    bool IsLoaded(string modId);
    IModInfo Get(string modId);
    void Provide<T>(T service) where T : class;                 // publish an API object for other mods
    T GetService<T>(string fromModId = null) where T : class;   // first provider of exactly T, or that mod's
    void Publish(string topic, object payload = null);          // synchronous cross-mod message
    IDisposable Subscribe(string topic, Action<object> handler);
    event Action AllModsLoaded;
}
```

### `ctx.Commands` - [guide](console.md)

```csharp
public interface IAslCommands
{
    void Register(string name, string description, Action<string[]> handler);
    void Register(string name, string description, Func<string[], string> handler);   // returned text is printed
    void Unregister(string name);
    bool Execute(string commandLine);
    void Print(string text);
}
```

### `ctx.Assets` and `ctx.Loc` - [guide](assets-and-localization.md)

```csharp
public interface IAslAssets
{
    string GetPath(string relativePath);                  // safe join inside the mod folder (throws on traversal)
    byte[] ReadBytes(string relativePath);
    string ReadText(string relativePath);
    Texture2D LoadTexture(string relativePngPath);        // null + log on failure
    Sprite    LoadSprite(string relativePngPath, float border = 0f);   // border > 0 = 9-sliced
    AudioClip LoadAudio(string relativeWavPath);
    AssetBundle LoadBundle(string relativePath);
    UnityEngine.Object LoadBundleAsset(string bundleRelativePath, string assetName);
}

public interface IAslLocalization
{
    string Language { get; }                       // the game's language token, null if unknown
    string Get(string key, params object[] args);  // lang/<lang>.json -> lang/en.json -> the key; string.Format with args
    bool   Has(string key);
    event  Action LanguageChanged;                 // may fire once shortly after load if the detected language differs from the startup one
}
```

## Networking

`ctx.Net` (`IAslNet`). The game uses Mirror; ASL exposes two layers — read-only **awareness** and a
**message transport** for sending bytes between the host and clients.

```csharp
public interface IAslNet
{
    // --- awareness (always safe, pure Mirror reads) ---
    bool IsOnline { get; }            // server and/or client active
    bool IsServer { get; }            // we run the server (host or dedicated)
    bool IsClient { get; }            // we run a client (incl. host's local client)
    bool IsHost  { get; }             // server + client
    bool IsConnectedClient { get; }   // our client is connected
    int  ConnectionCount { get; }     // server-side connected clients
    event Action<int> ConnectionsChanged;   // server-side, fires with the new count
    // ctx.Net is a per-mod wrapper: GetSync(id) returns a per-mod wrapper (same instance per id within a mod),
    // and Net subscriptions / event handlers are removed on unload and hot reload.

    // --- message transport ---
    bool MessagingAvailable { get; }  // transport could be installed (lazy; reading it sets up)
    bool Send(string channel, byte[] data);                 // auto-route: client→server, host→all
    bool SendToServer(string channel, byte[] data);         // client → server
    bool SendToAll(string channel, byte[] data);            // server → all clients
    bool SendToClient(int connectionId, string channel, byte[] data);  // server → one client
    void Subscribe(string channel, Action<AslNetMessage> handler);
    void Unsubscribe(string channel, Action<AslNetMessage> handler);

    // --- typed messages (serialize objects, not bytes) ---
    bool Send<T>(string channel, T message) where T : IAslMessage;                   // auto-route
    bool SendToServer<T>(string channel, T message) where T : IAslMessage;
    bool SendToAll<T>(string channel, T message) where T : IAslMessage;
    bool SendToClient<T>(int connectionId, string channel, T message) where T : IAslMessage;
    void Subscribe<T>(string channel, Action<T, AslNetMessage> handler) where T : IAslMessage, new();
    void Unsubscribe<T>(string channel, Action<T, AslNetMessage> handler) where T : IAslMessage, new();

    // --- player identity ---
    IReadOnlyList<IAslPlayer> Players { get; }   // everyone in the session
    IAslPlayer LocalPlayer { get; }              // you (or null)
    IAslPlayer GetPlayer(int connectionId);      // map a message sender to a player (server-side)
    event Action<IAslPlayer> PlayerJoined;
    event Action<IAslPlayer> PlayerLeft;

    // --- synced state ---
    IAslSync GetSync(string id);                  // host-authoritative shared key/value store

    // --- spawned objects ---
    GameObject FindObject(uint netId);            // resolve a networked object by net id (host or client)
}
```

Received messages arrive as `AslNetMessage`:

```csharp
public sealed class AslNetMessage
{
    string Channel { get; }              // the channel it arrived on
    byte[] Data    { get; }              // the bytes the sender passed (never null)
    int  SenderConnectionId { get; }     // server-side: which client (>=0); client-side: -1
    bool FromServer { get; }             // received on a client (SenderConnectionId < 0)
    bool FromClient { get; }             // received on the server (SenderConnectionId >= 0)
}
```

**How it works (and why it's safe):** new mod-defined Mirror message types can't be sent on this
IL2CPP build (their serializer code is never AOT-compiled). Instead ASL tunnels your bytes through
a Mirror message the game already ships, tagged so it can never be confused with real game traffic,
and intercepts it on arrival to hand back to you. You never touch Mirror. Full detail and the
verification status are in [networking.md](networking.md).

Example — a host counts pings from clients and replies:

```csharp
public override void OnLoad(IModContext ctx)
{
    const string Ch = "com.author.mymod/ping";

    ctx.Net.Subscribe(Ch, msg =>
    {
        if (msg.FromClient)                                   // we're the server
        {
            ctx.Log.Info($"ping from client {msg.SenderConnectionId}");
            ctx.Net.SendToClient(msg.SenderConnectionId, Ch, new byte[] { 1 });  // pong
        }
        else                                                 // we're a client; got the pong
            ctx.Log.Info("pong from host");
    });

    // From a client, ping the host (e.g. on a menu button):
    ctx.Menu.AddButton("Ping host", () => ctx.Net.SendToServer(Ch, new byte[] { 0 }));
}
```

Guidance:
- **Gate on `MessagingAvailable`** (or check the `Send*` return value) — the transport installs
  lazily and could be unavailable on an unexpected build.
- **Both peers must run a mod** that subscribes to the **same channel string**. Namespace your
  channel with your mod id (e.g. `"com.author.mymod/state"`) so mods don't clash.
- **Keep messages small** and infrequent; they ride Mirror's reliable channel.
- Handlers run on the **main thread** — safe to touch game state, but keep them fast.

### Typed messages

Instead of packing `byte[]` by hand, define an `IAslMessage` and use the typed `Send<T>` /
`Subscribe<T>` overloads — ASL serializes for you.

```csharp
public interface IAslMessage
{
    void Write(AslWriter writer);   // write your fields
    void Read(AslReader reader);    // read them back in the same order
}

public sealed class ScoreUpdate : IAslMessage
{
    public int PlayerId; public int Score; public string Name;
    public void Write(AslWriter w) { w.WriteInt(PlayerId); w.WriteInt(Score); w.WriteString(Name); }
    public void Read(AslReader r)  { PlayerId = r.ReadInt(); Score = r.ReadInt(); Name = r.ReadString(); }
}

ctx.Net.Subscribe<ScoreUpdate>("com.author.mymod/score", (msg, raw) =>
    ctx.Log.Info($"{msg.Name}={msg.Score} from conn {raw.SenderConnectionId}"));
ctx.Net.SendToAll("com.author.mymod/score", new ScoreUpdate { Name = "Radomir", Score = 42 });
```

`AslWriter` / `AslReader` cover `bool`/`byte`/`sbyte`/`short`/`ushort`/`int`/`uint`/`long`/`ulong`/
`float`/`double`, length-prefixed `string` (UTF-8), and raw `byte[]` blobs. The typed handler receives
both your `T` and the raw `AslNetMessage` (for `SenderConnectionId` etc.). A short/garbled payload is
logged and skipped, never thrown at your mod. See [networking.md](networking.md) for more.

### Player identity

`ctx.Net` lists the session's players as `IAslPlayer` (the game `MetaPlayer` object + Mirror identity):

```csharp
public interface IAslPlayer
{
    MetaPlayer Player { get; }   // the game player object
    uint   NetId        { get; } // Mirror net id (same on every peer)
    int    ConnectionId { get; } // server-side id (== AslNetMessage.SenderConnectionId), else -1
    bool   IsLocal      { get; } // this is you
    string Name         { get; } // local: from Steam; remote: empty for now (best-effort)

    // --- look + control (most meaningful for the local player) ---
    LookHit GetLookedAt(float maxDistance = 6f); // camera raycast: what you're aiming at
    void Freeze();                               // stop movement + gravity (hang in place)
    void Unfreeze();
    bool IsFrozen { get; }
    void Teleport(Vector3 position);
    void SetColliderSize(float radius, float height);  // shrink/grow the movement collider
    void ResetCollider();                              // restore after SetColliderSize
}

ctx.Net.PlayerJoined += p => ctx.Log.Info($"{p.Name} joined (netId {p.NetId})");
var sender = ctx.Net.GetPlayer(raw.SenderConnectionId);   // server: who sent a message
foreach (var p in ctx.Net.Players) { /* roster */ }
```

`GetLookedAt` returns a `LookHit` (a camera raycast result):

```csharp
public struct LookHit
{
    bool      Hit;       // did the ray hit anything in range?
    GameObject Object;   // the hit object
    Transform Transform; // its transform
    Vector3   Point;     // world hit point
    float     Distance;
    uint      NetId;     // hit object's Mirror net id (0 if not networked) — feed to Net.FindObject
}

var look = ctx.Net.LocalPlayer.GetLookedAt();
if (look.Hit && look.NetId != 0) ctx.Log.Info($"aiming at networked object {look.NetId}");
```

`Freeze` / `SetColliderSize` drive the local player's movement controller (the same levers PropHunt
uses to hang on a wall and to shrink to a small prop). `SetColliderSize` remembers the originals;
`ResetCollider` puts them back.

`Players` / `PlayerJoined` / `PlayerLeft` are poll-driven on the **main thread**. `NetId`,
`ConnectionId`, `IsLocal`, and `Player` are always reliable; `Name` is best-effort (see
[networking.md](networking.md)).

### Synced state

`ctx.Net.GetSync(id)` returns a host-authoritative shared key/value store (`IAslSync`):

```csharp
var state = ctx.Net.GetSync("com.author.mymod/state");
state.Changed += (key, value) => { /* host on Set, client on receive */ };
if (ctx.Net.IsServer) state.Set("round", "warmup");   // Set is host-only
string round = state.Get("round");                    // anyone reads; also TryGet/Contains/All
```

The host sets values; they replicate to clients and late joiners get a full snapshot. Values are
strings. See [networking.md](networking.md) for details and the send caveat.

## `AslInfo` (advanced)

`ASL.AslInfo` (in `ASL.dll`) exposes the framework's `Guid` / `Name` / `Version` if you write a
BepInEx plugin that wants to declare `[BepInDependency(AslInfo.Guid)]`. Most mods don't need this —
they live in `mods/` and never touch BepInEx directly.

