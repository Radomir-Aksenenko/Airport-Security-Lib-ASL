namespace ASL.Api
{
    /// <summary>
    /// A typed network message a mod can send and receive without packing bytes by hand. Define a
    /// class with your fields, serialize them in <see cref="Write"/> and read them back in the same
    /// order in <see cref="Read"/>, then use the typed <c>IAslNet.Send&lt;T&gt;</c> /
    /// <c>IAslNet.Subscribe&lt;T&gt;</c> overloads. ASL handles serialization, the wire, and dispatch.
    /// </summary>
    /// <example>
    /// <code>
    /// public sealed class ScoreUpdate : IAslMessage
    /// {
    ///     public int PlayerId;
    ///     public int Score;
    ///     public string Name;
    ///
    ///     public void Write(AslWriter w) { w.WriteInt(PlayerId); w.WriteInt(Score); w.WriteString(Name); }
    ///     public void Read(AslReader r)  { PlayerId = r.ReadInt(); Score = r.ReadInt(); Name = r.ReadString(); }
    /// }
    /// </code>
    /// </example>
    public interface IAslMessage
    {
        /// <summary>Serialize this message's fields into <paramref name="writer"/>.</summary>
        void Write(AslWriter writer);

        /// <summary>Read this message's fields from <paramref name="reader"/>, in the same order they were written.</summary>
        void Read(AslReader reader);
    }
}
