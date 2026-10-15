# Dev console

> **Status: 🧪 Implemented, not yet verified in-game.** New in ASL 2.0.

A Quake-style console for modders and curious players: type commands, see output, run your own mod's debug
commands without building a UI for them.

## Using the console

- Press <kbd>`</kbd> (the **BackQuote** key, left of `1`) to open and close it. Esc also closes it.
  The key that opens the console is not typed into the input field.
- Type a command and press **Enter**. **Up / Down** browse the last 50 lines you typed, **Tab** completes the command name
  (it extends to the common prefix and lists the candidates when several match).
- The output keeps the last 500 lines. The **Clear** button (or `clear`) empties it. The **Mirror BepInEx log**
  switch (or `log.mirror on|off`) also shows BepInEx log lines in the console.
- Several commands can go on one line, separated by `;`:  `scene; fps`.
- Arguments are split on spaces. `"double"` or `'single'` quotes keep spaces in one argument; inside quotes `\"`, `\\`,
  `\n` and `\t` work. Outside quotes a backslash is literal, so Windows paths work unquoted. `""` is an empty argument.
- While the console is open the cursor is free and the player's input is blocked, like any toolkit window. Mod keybinds are
  muted while you type.
- Unknown commands print `Unknown command 'x'. Type help.`; a handler that throws prints a red error line and is
  logged, and the rest of the line keeps running.

The console is built from the public [UI toolkit](ui.md), so it looks and behaves like any ASL window.

> **Rebinding the key.** The console key is an ASL keybind (`asl/console`, default `BackQuote`), but this build adds no
> row for it to the F8 menu. To change it, edit `BepInEx/config/ASL.Keybinds.cfg` and add or change the line
> `asl/console = F10` (any Unity `KeyCode` name), then restart the game.

## Built-in commands

| Command | What it does |
|---|---|
| `help [command]` | List all commands, or describe one. |
| `clear` | Clear the console. |
| `echo <text>` | Print text. |
| `asl` | ASL version and mod counts. |
| `mods` | Table of every mod: id, version, kind, state. |
| `mod <id>` | Details of one mod (state, error, author, dependencies, tags). `<id>` may be the full id or a unique suffix such as `prophunt`. |
| `reload <id>` | Hot reload a [script mod](mod-manager.md#hot-reload-script-mods). |
| `enable <id>` / `disable <id>` | Switch a mod on/off; applies after the next restart. |
| `scene` | Name of the active scene. |
| `fps` | Frames per second and frame time. |
| `perf` | Per-mod `Update` / timer cost over the last second ([profiler](timers-and-events.md#profiler)). |
| `players` | List connected players. |
| `net` | Network role (host / client / connected). |
| `config <modId> [section.key [value]]` | List a mod's settings, read one, or set one (ranges are clamped, saved at once). |
| `toast <text>` | Show a toast. |
| `announce <text>` | Show the game's announcement banner. |
| `ui.demo` | Open the UI toolkit demo (every widget, shape, size and variant). |
| `windows` | List open toolkit windows and HUDs. |
| `timescale [value]` | Show or set `Time.timeScale` (0..20). In multiplayer this changes only **your** machine and can desync. |
| `log.mirror on\|off` | Mirror BepInEx log lines into the console. |
| `history` | List lines you typed before. |

## Registering your own commands

`ctx.Commands` (`IAslCommands`):

```csharp
// Action<string[]> handler: does something, prints via ctx.Commands.Print
ctx.Commands.Register("give", "give <item> [count]", args =>
{
    int count = args.Length > 1 ? int.Parse(args[1]) : 1;
    Give(args[0], count);
    ctx.Commands.Print($"Gave {count} x {args[0]}");
});

// Func<string[], string> handler: the returned text is printed
ctx.Commands.Register("where", "print your position", args => "You are at " + player.position);
```

The two `Register` overloads differ only in the handler type; with a lambda the compiler picks by whether it returns
a value. If a lambda is ambiguous, cast it: `(Func<string[], string>)(args => "...")`.

| Member | Meaning |
|---|---|
| `Register(name, description, handler)` | Add a command. `description` is shown by `help`. |
| `Unregister(name)` | Remove a command you registered. |
| `Execute(line)` | Run a line as if the player typed it (`;`-separated allowed). Returns `false` if any command was unknown or failed. |
| `Print(text)` | Write a line to the console. TextMeshPro rich text works (`<color=#ff6b6b>error</color>`). |

### Naming and clashes

Command names are **global and case-insensitive**; they must not contain spaces, quotes or `;`. Dots are fine and
commonly used as a namespace (`mymod.reset`).

- Your command is always reachable as `<shortId>.<name>`, where **shortId is the last segment of your mod id,
  lower-cased**: `com.radomir.PropHunt` gives `prophunt`, so `give` is also `prophunt.give`.
- The bare name `give` is yours only if nobody had it first. On a clash the earlier mod keeps the bare name, the later one
  only gets the qualified name, and a warning is logged. `help give` shows which mod owns it and the other name.
- ASL's built-ins are bare-only and cannot be replaced by a mod.
- Your commands are removed when your mod unloads or hot-reloads.

### Log mirror and output

`Print` and returned text also go to the BepInEx log, so they are in `LogOutput.log` too. With log mirroring on, BepInEx
log lines (from any plugin) appear in the console as well; the console only buffers them, so it costs nothing while closed.

### Tips

- Prefer a descriptive `description`; `help` lists `name - description` for every command.
- Parse arguments defensively: players type anything. Return a usage string rather than throwing.
- A command that needs the game world should check that the local player exists (`MetaPlayer.LocalPlayerInstance`).
- You can drive the console from a [JSON layout](ui.md#actions) button with `"onClick": "command:mymod.reset"`.
