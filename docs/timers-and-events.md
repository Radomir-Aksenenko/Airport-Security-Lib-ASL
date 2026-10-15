# Timers and events

> **Status: 🧪 Implemented, not yet verified in-game** (everything new in 2.0). The 0.1 events `Update`,
> `SceneChanged` and `LocalPlayerChanged` are ✅; their *behaviour* changed slightly in 2.0 (per-mod isolation, backoff),
> see below.

## Events: `ctx.Events`

```csharp
public interface IAslEvents
{
    event Action          Update;               // every frame
    event Action<string>  SceneChanged;         // active scene changed (new scene name)
    event Action<MetaPlayer> LocalPlayerChanged; // local player spawned (non-null) / cleared (null)
    event Action          LateUpdate;           // every frame, after Update logic            (2.0)
    event Action<bool>    MenuVisibilityChanged; // F8 menu shown (true) / hidden (false)      (2.0)
    event Action          AllModsLoaded;        // once, after every mod's OnLoad             (2.0)
    event Action<string>  LanguageChanged;      // the player changed the game language       (2.0)
    event Action          GameQuitting;         // the game is shutting down                  (2.0)
}
```

All handlers run on the main thread.

| Event | When | Cost / notes |
|---|---|---|
| `Update` | Every frame (Unity `Update`). | The workhorse. Keep it cheap; heavy work or allocations stutter the game. |
| `LateUpdate` | Every frame, after all `Update` handlers. | Same rules as `Update`. |
| `SceneChanged` | The active scene name changed. | Polled a few times a second, so free per frame. |
| `LocalPlayerChanged` | Your player spawned/despawned. | Polled and debounced against flicker. |
| `MenuVisibilityChanged` | F8 menu opened/closed. | Raised after the native menu reacted. |
| `AllModsLoaded` | Once, after every mod's `OnLoad` has run. | Same moment as `AslMod.OnAllModsLoaded()`. Also raised after a [hot reload](mod-manager.md#hot-reload-script-mods) of your mod. |
| `LanguageChanged` | The game language changed (argument: the new token). May fire once shortly after load if the detected language differs from the startup one. | Pair with [`ctx.Loc`](assets-and-localization.md). |
| `GameQuitting` | The application is quitting. | **Best effort**: raised at most once, from the application-quit message, because BepInEx IL2CPP does not reliably call plugin unload on exit. Keep handlers quick and do not rely on it alone for critical data; `OnUnload` follows. |

Game-specific events (NPC spawned, round start/end, contraband scans) are **not** in 2.0; they need a game dump and
will be built on the opt-in [hooks](api-reference.md#hooks) system. Until then use `ctx.Hooks.TryPostfix` yourself.

### Per-mod isolation and backoff

`ctx.Events` is a per-mod view of the shared bus:

- Each mod's `Update` / `LateUpdate` handlers run inside **that mod's own try/catch**. In 0.1 one throwing handler
  aborted the whole multicast and starved later mods; in 2.0 a throwing mod never stops the others.
- The first failure of a streak is logged with the mod id. If the handler throws **10 frames in a row** it is not switched
  off: ASL **retries it with backoff** (0.5 s, 1 s, 2 s ... capped at 5 s), logging at most one line per retry (with how
  many similar errors were suppressed). On the first success, or on a scene change or local-player change, it goes
  back to running every frame. This lets "throw until the player exists" mods heal themselves.
- The infrequent events (`SceneChanged`, ...) are dispatched per handler; an exception is logged and the next handler runs.
- Unload and hot reload drop all of the mod's handlers; after that the proxy ignores new subscriptions.
- Handlers run in **mod load order** each frame.

### Profiler

ASL measures each mod's `Update`, `LateUpdate` and timer cost (stopwatch ticks, no allocation) and averages it over one second.
See it in **F8 > ASL** ("Mod cost per frame") and with the console command `perf`; `fps` shows the frame rate. A mod that is
currently in backoff is flagged. Every code mod is listed (content mods have no code to measure).

---

## Timers: `ctx.Timers`

```csharp
ctx.Timers.After(3f, () => ctx.Ui.Toast("3 seconds later"));
var tick = ctx.Timers.Every(1f, () => _seconds++);
ctx.Timers.NextFrame(() => ctx.Log.Info("next frame"));
ctx.Timers.When(() => MetaPlayer.LocalPlayerInstance != null, OnPlayerReady, timeoutSeconds: 30f);
tick.Cancel();
```

| Method | Behaviour |
|---|---|
| `After(seconds, action, unscaled = true)` | Run once after the delay. |
| `Every(seconds, action, unscaled = true)` | Run repeatedly until cancelled. At most once per frame; a long hitch does not cause a burst of catch-up calls. |
| `NextFrame(action)` | Run on the next frame. |
| `When(condition, action, timeoutSeconds = 0)` | Poll `condition` every frame; run `action` once when it returns `true`. `timeoutSeconds > 0` gives up silently after that long. |
| `Start(IEnumerator routine)` | Run a coroutine ([below](#coroutines)). |
| `CancelAll()` | Cancel everything this mod started. |

All return an `IAslTimer` (`IsActive`, `Cancel()`; cancelling twice is fine). `unscaled: true` (default) ignores
`Time.timeScale`, so timers keep running while the game is paused or slowed; pass `false` to follow game time.

Guarantees:

- Everything runs on the main thread, in order, once per frame, from the shared bus. Nothing is allocated per frame while idle,
  and a mod that never creates a timer costs nothing.
- A timer created inside a callback first runs on the **next** frame. Callbacks may cancel timers (including themselves) freely.
- If a callback throws, the error is logged with your mod id and **that timer is cancelled** (an `Every` stops). Other timers are unaffected.
- Timers are cancelled when your mod unloads or hot-reloads; after that, new timer requests return an inactive handle.

### Coroutines

`ctx.Timers.Start(routine)` runs a Unity-style coroutine without `MonoBehaviour`. Inside the iterator:

| `yield return ...` | Waits |
|---|---|
| `null` | one frame |
| `1.5f`, `2` (float, int or double) | that many seconds |
| `Wait.Seconds(2f)` | 2 seconds |
| `Wait.Frames(5)` | 5 frames |
| `Wait.Until(() => ready)` | until the condition is true |
| `Wait.While(() => busy)` | while the condition is true |
| anything else | one frame |

```csharp
IEnumerator Countdown()
{
    for (int i = 3; i > 0; i--)
    {
        ctx.Ui.Toast(i.ToString());
        yield return Wait.Seconds(1f);
    }
    yield return Wait.Until(() => MetaPlayer.LocalPlayerInstance != null);
    ctx.Ui.Toast("Go!", UiToastKind.Success);
}

ctx.Timers.Start(Countdown());     // the first step runs on the next frame
```

Waits in routines are in unscaled seconds. A throwing routine step is logged and ends that routine. You can cancel a routine through the returned `IAslTimer`.

---

## Mod-to-mod messages

Events are between ASL and your mod. For mod-to-mod communication (services and topics) see
[`ctx.Mods`](mod-manager.md#inter-mod-communication).
