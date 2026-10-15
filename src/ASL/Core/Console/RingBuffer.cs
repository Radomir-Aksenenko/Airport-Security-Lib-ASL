using System;
using System.Collections.Generic;
using System.Text;

namespace ASL.Core
{
    /// <summary>Fixed-capacity ring buffer: adding past capacity drops the oldest item. <see cref="Version"/> bumps on every change.</summary>
    internal sealed class RingBuffer<T>
    {
        private readonly T[] _items;
        private int _head;     // index of the oldest item
        private int _count;

        public RingBuffer(int capacity)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            _items = new T[capacity];
        }

        public int Capacity => _items.Length;
        public int Count => _count;
        /// <summary>Incremented by every <see cref="Add"/> / <see cref="Clear"/> (cheap "did it change" check).</summary>
        public int Version { get; private set; }

        /// <summary>Item by age: 0 = oldest, Count-1 = newest.</summary>
        public T this[int index]
        {
            get
            {
                if ((uint)index >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));
                return _items[(_head + index) % _items.Length];
            }
        }

        public void Add(T item)
        {
            if (_count < _items.Length) _items[(_head + _count++) % _items.Length] = item;
            else { _items[_head] = item; _head = (_head + 1) % _items.Length; }
            Version++;
        }

        public void Clear()
        {
            Array.Clear(_items, 0, _items.Length);
            _head = 0; _count = 0;
            Version++;
        }
    }

    /// <summary>Bounded list of previously typed lines with Up/Down navigation (readline-like).</summary>
    internal sealed class CommandHistory
    {
        private readonly List<string> _items = new List<string>();
        private readonly int _max;
        private int _cursor;          // == Count means "not browsing"
        private string _draft = string.Empty;

        public CommandHistory(int max = 50) { _max = Math.Max(1, max); }

        public IReadOnlyList<string> Items => _items;

        /// <summary>Record a submitted line (blank lines and immediate repeats are ignored) and stop browsing.</summary>
        public void Add(string line)
        {
            if (!string.IsNullOrWhiteSpace(line) && (_items.Count == 0 || _items[_items.Count - 1] != line))
            {
                _items.Add(line);
                if (_items.Count > _max) _items.RemoveAt(0);
            }
            _cursor = _items.Count;
            _draft = string.Empty;
        }

        /// <summary>Up: older entry. <paramref name="current"/> is the text being typed (kept as the draft when browsing starts).</summary>
        public string Previous(string current)
        {
            if (_items.Count == 0) return current;
            if (_cursor >= _items.Count) { _draft = current ?? string.Empty; _cursor = _items.Count; }
            if (_cursor > 0) _cursor--;
            return _items[_cursor];
        }

        /// <summary>Down: newer entry, finally the draft.</summary>
        public string Next(string current)
        {
            if (_cursor >= _items.Count) return current;
            _cursor++;
            return _cursor >= _items.Count ? _draft : _items[_cursor];
        }

        public void Clear() { _items.Clear(); _cursor = 0; _draft = string.Empty; }
    }

    /// <summary>Pure helpers for TextMeshPro rich text in console lines.</summary>
    internal static class ConsoleText
    {
        /// <summary>Wrap text so TMP shows it literally (user input, foreign log lines).</summary>
        public static string Literal(string s) =>
            string.IsNullOrEmpty(s) ? string.Empty : "<noparse>" + s.Replace("</noparse>", "<\\/noparse>") + "</noparse>";

        /// <summary>Colour a message (hex like "ff6b6b").</summary>
        public static string Color(string hex, string s) => "<color=#" + hex + ">" + s + "</color>";

        /// <summary>Remove <c>&lt;tag&gt;</c> rich-text markup (for mirroring to the plain log).</summary>
        public static string Strip(string s)
        {
            if (string.IsNullOrEmpty(s) || s.IndexOf('<') < 0) return s ?? string.Empty;
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '<')
                {
                    int close = s.IndexOf('>', i + 1);
                    // only short things that look like tags: "<b>", "</color>", "<color=#fff>", "<size=80%>"
                    if (close > i && close - i <= 40 && IsTagStart(s, i + 1)) { i = close; continue; }
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        private static bool IsTagStart(string s, int i)
        {
            if (i >= s.Length) return false;
            char c = s[i];
            if (c == '/') return i + 1 < s.Length && char.IsLetter(s[i + 1]);
            return char.IsLetter(c);
        }
    }
}
