namespace ASL.Api
{
    /// <summary>
    /// A message received over ASL's Mirror transport (see <see cref="IAslNet"/>). Mods get one of
    /// these for each packet delivered to a channel they subscribed to. It carries only plain data —
    /// no Mirror types leak through.
    /// </summary>
    public sealed class AslNetMessage
    {
        /// <summary>The channel the message arrived on (the same string the sender used).</summary>
        public string Channel { get; }

        /// <summary>The raw bytes the sender passed to <c>Send</c>. Never null (empty if no data).</summary>
        public byte[] Data { get; }

        /// <summary>
        /// Who sent it, from the receiver's point of view:
        /// <list type="bullet">
        /// <item><b>On the server</b> (we received from a client): the sender's Mirror connection id
        /// (<c>&gt;= 0</c>), so the server can tell clients apart and reply to a specific one.</item>
        /// <item><b>On a client</b> (we received from the server): <c>-1</c>.</item>
        /// </list>
        /// </summary>
        public int SenderConnectionId { get; }

        /// <summary>True when this message came from the server (received on a client).</summary>
        public bool FromServer => SenderConnectionId < 0;

        /// <summary>True when this message came from a client (received on the server).</summary>
        public bool FromClient => SenderConnectionId >= 0;

        /// <summary>Constructs a message. ASL builds these; mods normally just read them.</summary>
        public AslNetMessage(string channel, byte[] data, int senderConnectionId)
        {
            Channel = channel;
            Data = data ?? System.Array.Empty<byte>();
            SenderConnectionId = senderConnectionId;
        }
    }
}
