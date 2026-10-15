namespace ASL.Api
{
    /// <summary>
    /// The version of ASL a mod is running against. Use it to guard newer features:
    /// <code>if (AslVersion.AtLeast("2.0.0")) { /* use the UI toolkit */ }</code>
    /// Mods can also declare <c>"minAslVersion"</c> in their manifest so ASL refuses to load them on older versions.
    /// </summary>
    public static class AslVersion
    {
        /// <summary>The full ASL version string.</summary>
        public const string Current = "2.0.0";

        private static readonly SemVersion Parsed = SemVersion.Parse(Current);

        /// <summary>Major version number.</summary>
        public static int Major => Parsed.Major;

        /// <summary>Minor version number.</summary>
        public static int Minor => Parsed.Minor;

        /// <summary>Patch version number.</summary>
        public static int Patch => Parsed.Patch;

        /// <summary>True if the running ASL is at least <paramref name="version"/> (false if it cannot be parsed).</summary>
        public static bool AtLeast(string version) => SemVersion.TryParse(version, out var v) && Parsed >= v;
    }
}
