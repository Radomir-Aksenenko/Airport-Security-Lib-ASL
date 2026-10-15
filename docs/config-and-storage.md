# Config and storage

> **Status: 🧪 Implemented, not yet verified in-game.** New in ASL 2.0.

Two services keep your mod's data between sessions:

| | `ctx.Config` (`IAslConfig`) | `ctx.Storage` (`IAslStorage`) |
|---|---|---|
| For | **Settings** the player may change | **Data** your mod produces (progress, scores, unlocks) |
| Format | BepInEx `.cfg`, human-editable | JSON (or plain text) |
| Shown in F8 | Yes, automatically (Settings page) | No |
| Location | `BepInEx/config/<modId>.cfg` | `BepInEx/config/ASL/data/<modId>/` |

---

## Config: `ctx.Config`

```csharp
public override void OnLoad(IModContext ctx)
{
    var speed = ctx.Config.BindRange("General", "Speed", 2f, 0.5f, 10f, "Walk speed multiplier");
    var god   = ctx.Config.Bind("General", "GodMode", false, "Take no damage");
    var key   = ctx.Config.Bind("Keys", "Boost", KeyCode.LeftShift);

    ctx.Events.Update += () => { if (god.Value) Heal(speed.Value); };
    speed.Changed += v => ctx.Log.Info("speed is now " + v);       // menu, code or file reload
}
```

### Binding entries

| Call | Notes |
|---|---|
| `Bind<T>(section, key, defaultValue, description)` | Create the setting, or fetch it if already bound. The file's value wins over the default. |
| `BindRange(section, key, default, min, max, description)` | `float` or `int` limited to `[min, max]` (values are clamped). Shown as a slider. |

Supported `T`: `bool`, `int`, `float`, `double`, `string`, any `enum`, and `UnityEngine.KeyCode`. Anything else throws
`NotSupportedException`; an empty section or key throws `ArgumentException`; binding the same section/key again with a
different type throws `InvalidOperationException`. Binding it again with the same type returns the same entry.

### Entries: `IConfigEntry<T>`

| Member | Meaning |
|---|---|
| `Value` (get/set) | Current value. **Setting it saves the file immediately** and raises `Changed`. |
| `DefaultValue`, `Section`, `Key`, `Description` | As bound. |
| `Changed` | `Action<T>` raised with the new value from the F8 menu, code, JSON layouts or a file reload. |
| `Reset()` | Set back to the default. |

### The config object

| Member | Meaning |
|---|---|
| `ShowInMenu` | Default `true`: show a **Settings** page on your F8 page. Set `false` to hide it. |
| `FilePath` | Absolute path of the `.cfg` file. |
| `Save()` | Write now (setting a value already does). |
| `Reload()` | Re-read the file; entries that changed raise `Changed`. |
| `Changed` | `Action` raised after any entry changes. |

The `.cfg` file is created on first use (a mod that never binds anything touches no disk). Handlers that throw are logged
with your mod name and do not affect other handlers. When your mod unloads or hot-reloads, its handlers are dropped; the
file keeps the values.

### The automatic Settings page

With `ShowInMenu` on, **F8 > your mod > Settings** is generated from your entries, grouped by section, with each
description as a tooltip:

| Entry | Control |
|---|---|
| `bool` | toggle |
| `int` / `float` bound with `BindRange` | slider |
| `enum` | dropdown |
| `string` | text field (Enter saves) |
| `KeyCode` | click-to-capture button (Esc cancels) |
| unranged `int` / `float`, `double` | **read-only text** ("edit in the .cfg file"); use `BindRange` to get a slider |

Changes apply immediately and are saved; a **Reset to defaults** button restores everything. The page follows the entries
both ways, so a change made in code or the file updates the open page.

### Binding config from JSON UI

A [JSON layout](ui.md#binding-to-config) control with `"bind": "config:General.Speed"` is two-way bound to the entry
(it must be bound before the layout loads).

### File naming

The file is `BepInEx/config/<modId>.cfg`, with characters that are unsafe in file names replaced (`com.you.mymod` stays as is).

---

## Storage: `ctx.Storage`

```csharp
public sealed class Save { public int Coins; public string[] Unlocked = new string[0]; }

var save = ctx.Storage.Load("save", new Save());     // missing or corrupt file -> the fallback
save.Coins += 10;
ctx.Storage.Save("save", save);

ctx.Storage.SaveText("notes", "free text");
string notes = ctx.Storage.LoadText("notes", "");
```

| Member | Meaning |
|---|---|
| `DataDirectory` | Your folder: `BepInEx/config/ASL/data/<modId>/` (created on first use, survives mod updates). |
| `Save<T>(name, value)` | Serialise as JSON to `<name>.json`, **atomically** (temp file then replace). Failures are logged, never thrown. |
| `Load<T>(name, fallback = default)` | Read `<name>.json`. Missing or corrupt returns `fallback`; a corrupt file is logged and a `.bad` copy is kept. |
| `Exists(name)` / `Delete(name)` | For `<name>.json`. |
| `SaveText(name, text)` / `LoadText(name, fallback = null)` | Raw text in `<name>.txt`. |

Serialisation uses `System.Text.Json` with these options: comments and trailing commas are allowed on read, numbers
may be written as strings, enums are stored as names. Use plain classes with public get/set properties or public fields, arrays,
lists and dictionaries. (Types the JSON serialiser cannot handle log an error on save.)

### Names are sanitised

`name` is turned into a safe single file name: path separators, drive colons, control characters and `..` become `_`,
leading/trailing dots and spaces are trimmed, Windows device names (`CON`, `NUL`, ...) are prefixed, and names are capped at 100
characters. If sanitising changed the name, a short stable hash is appended (`a/b` becomes `a_b~a22d4fe9`) so that
`a/b` and `a_b` never share a file. Clean names are used unchanged. A name that is empty after sanitising throws `ArgumentException`.
A name can never escape `DataDirectory`.

### Config or storage?

- A player might want to tweak it, or you want it in the F8 menu: **config**.
- It is state your mod writes (score, discovered items, window layout): **storage**.
- Large or frequently changing data: **storage**, saved on events (round end, window close, `OnUnload`), not every frame.

Other files ASL keeps in `BepInEx/config/`: `ASL.Keybinds.cfg` (rebound keys), `ASL.ModStates.json` (enabled/disabled
mods), `ASL.Windows.json` (remembered window positions).
