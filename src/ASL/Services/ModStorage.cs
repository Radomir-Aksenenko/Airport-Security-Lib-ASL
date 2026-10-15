using System;
using ASL.Api;
using BepInEx.Logging;

namespace ASL
{
    /// <summary>
    /// Per-mod <see cref="IAslStorage"/> (JSON files in <c>BepInEx/config/ASL/data/&lt;modId&gt;/</c>).
    /// Owned by the Data agent. Placeholder: the constructor must stay side-effect free.
    /// </summary>
    internal sealed class ModStorage : IAslStorage
    {
        private readonly string _modId;
        private readonly ManualLogSource _log;

        public ModStorage(string modId, ManualLogSource log)
        {
            _modId = modId;
            _log = log;
        }

        public string DataDirectory => throw NotYet("Storage.DataDirectory");
        public void Save<T>(string name, T value) => throw NotYet("Storage.Save");
        public T Load<T>(string name, T fallback = default) => throw NotYet("Storage.Load");
        public bool Exists(string name) => throw NotYet("Storage.Exists");
        public void Delete(string name) => throw NotYet("Storage.Delete");
        public void SaveText(string name, string text) => throw NotYet("Storage.SaveText");
        public string LoadText(string name, string fallback = null) => throw NotYet("Storage.LoadText");

        private static NotImplementedException NotYet(string what) => new NotImplementedException($"ASL 2.0: {what} not implemented yet");
    }
}
