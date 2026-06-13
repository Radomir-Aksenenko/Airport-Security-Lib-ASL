using System;

namespace ASL.Api
{
    /// <summary>
    /// Opt-in game hooks. Unlike <see cref="IAslEvents"/> (always on), a hook installs a Harmony
    /// patch on a specific game method only when a mod asks for it — so unused hooks cost nothing.
    ///
    /// Patching is best-effort on IL2CPP: <see cref="TryPostfix"/> returns <c>false</c> (and logs
    /// the reason) when the method can't be found or the patch can't be compiled, so your mod
    /// degrades gracefully instead of crashing. Do not hook hot, per-frame methods.
    /// </summary>
    public interface IModHooks
    {
        /// <summary>
        /// Invoke <paramref name="after"/> every time <paramref name="methodName"/> on
        /// <paramref name="targetType"/> returns. The callback receives the instance the method
        /// ran on (<c>null</c> for static methods) as <see cref="object"/> — cast it to the game
        /// type. Returns <c>true</c> if the hook was installed.
        /// </summary>
        /// <param name="targetType">The game type that declares the method (e.g. a type from Assembly-CSharp).</param>
        /// <param name="methodName">The method name to hook (first overload by that name).</param>
        /// <param name="after">Callback run after the method; receives the instance or null.</param>
        bool TryPostfix(Type targetType, string methodName, Action<object> after);
    }
}
