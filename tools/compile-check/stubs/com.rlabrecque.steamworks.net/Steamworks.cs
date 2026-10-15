// STUB: Steamworks.NET as seen through the interop (namespace Steamworks). Minimal.
using System;
namespace Steamworks
{
    public struct CSteamID : IEquatable<CSteamID>
    {
        public CSteamID(ulong ulSteamID) { m_SteamID = ulSteamID; }
        public ulong m_SteamID;
        public bool IsValid() => throw null;
        public ulong GetAccountID() => throw null;
        public bool Equals(CSteamID other) => throw null;
    }

    public static class SteamAPI
    {
        public static bool IsSteamRunning() => throw null;
    }

    public static class SteamUser
    {
        public static CSteamID GetSteamID() => throw null;
        public static bool BLoggedOn() => throw null;
    }

    public static class SteamFriends
    {
        public static string GetPersonaName() => throw null;
        public static string GetFriendPersonaName(CSteamID steamIDFriend) => throw null;
    }
}
