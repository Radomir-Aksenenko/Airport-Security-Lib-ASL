using System;
using System.Collections.Generic;
using ASL.Api;

namespace ASL.Core
{
    /// <summary>What the resolver decided for one mod.</summary>
    public enum ResolvedState
    {
        /// <summary>Candidate to load (appears in <see cref="ResolveResult.LoadOrder"/>).</summary>
        Load,
        /// <summary>Switched off by the player (Mod Manager).</summary>
        Disabled,
        /// <summary>A hard dependency is missing, failed, disabled, or has the wrong version (also true transitively).</summary>
        MissingDependency,
        /// <summary>Needs a newer ASL than the one running.</summary>
        Incompatible,
        /// <summary>Unusable regardless of the environment: duplicate id, dependency cycle, malformed dependency entry.</summary>
        Failed,
    }

    /// <summary>The manifest data the resolver needs about one mod folder (pure data; no Unity types).</summary>
    public sealed class ResolverMod
    {
        /// <summary>Manifest id.</summary>
        public string Id;
        /// <summary>Manifest version (semver text; unparseable versions only satisfy range-less dependencies).</summary>
        public string Version;
        /// <summary>Folder name — the deterministic tie-break for load order.</summary>
        public string Folder;
        /// <summary>Hard dependencies (<c>"id"</c> or <c>"id@range"</c>).</summary>
        public string[] Dependencies;
        /// <summary>Soft dependencies: loaded first when present and satisfied.</summary>
        public string[] OptionalDependencies;
        /// <summary>Ordering preference: load after these (when present).</summary>
        public string[] LoadAfter;
        /// <summary>Ordering preference: load before these (when present).</summary>
        public string[] LoadBefore;
        /// <summary>Minimum ASL version (semver text) or null.</summary>
        public string MinAslVersion;
        /// <summary>True if the player disabled this mod.</summary>
        public bool Disabled;
        /// <summary>Caller's tag (e.g. its registry record); the resolver never looks at it.</summary>
        public object Tag;
    }

    /// <summary>The resolver's verdict for one mod.</summary>
    public sealed class ResolvedMod
    {
        /// <summary>The input record.</summary>
        public ResolverMod Mod { get; internal set; }
        /// <summary>Outcome.</summary>
        public ResolvedState State { get; internal set; }
        /// <summary>Human-readable explanation (null for <see cref="ResolvedState.Load"/>).</summary>
        public string Reason { get; internal set; }
        /// <summary>Position in <see cref="ResolveResult.LoadOrder"/>, or -1.</summary>
        public int LoadIndex { get; internal set; } = -1;
    }

    /// <summary>Result of <see cref="DependencyResolver.Resolve"/>.</summary>
    public sealed class ResolveResult
    {
        /// <summary>Every input mod, in deterministic (folder-name) order.</summary>
        public List<ResolvedMod> All { get; } = new();
        /// <summary>The mods to load, in the order to load them.</summary>
        public List<ResolvedMod> LoadOrder { get; } = new();
        /// <summary>Non-fatal notes (ignored malformed optional entries, ordering preferences that had to be dropped).</summary>
        public List<string> Warnings { get; } = new();

        /// <summary>The verdict for <paramref name="mod"/>, or null.</summary>
        public ResolvedMod Find(ResolverMod mod)
        {
            foreach (var r in All) if (ReferenceEquals(r.Mod, mod)) return r;
            return null;
        }
    }

    /// <summary>
    /// Pure dependency / load-order logic for manifest v2. Input: every mod's manifest data. Output: a load
    /// order plus a state and human-readable reason for every mod that will NOT load.
    ///
    /// Rules: order is topological (hard deps, present optional deps, <c>loadAfter</c>/<c>loadBefore</c>);
    /// ties are broken by folder name (case-insensitive, then ordinal) so the order is deterministic.
    /// A mod whose hard dependency is missing, disabled, failed or of the wrong version is skipped
    /// (<see cref="ResolvedState.MissingDependency"/>) — transitively. Hard-dependency cycles fail every
    /// member; cycles made only of ordering preferences are broken (a warning), never fatal.
    /// Duplicate ids: the first folder (in folder order) keeps the id, later ones fail.
    /// </summary>
    public static class DependencyResolver
    {
        public static ResolveResult Resolve(IReadOnlyList<ResolverMod> input, string aslVersion)
        {
            var res = new ResolveResult();
            int n = input?.Count ?? 0;
            if (n == 0) return res;

            // 1. Deterministic order: folder name (ignore-case, then ordinal), then input position.
            var idx = new int[n];
            for (int i = 0; i < n; i++) idx[i] = i;
            Array.Sort(idx, (a, b) =>
            {
                int c = string.Compare(input[a].Folder ?? "", input[b].Folder ?? "", StringComparison.OrdinalIgnoreCase);
                if (c == 0) c = string.CompareOrdinal(input[a].Folder ?? "", input[b].Folder ?? "");
                return c != 0 ? c : a.CompareTo(b);
            });

            var mods = new ResolverMod[n];
            var rm = new ResolvedMod[n];
            for (int i = 0; i < n; i++)
            {
                mods[i] = input[idx[i]];
                rm[i] = new ResolvedMod { Mod = mods[i], State = ResolvedState.Load };
                res.All.Add(rm[i]);
            }

            SemVersion asl = null;
            SemVersion.TryParse(aslVersion, out asl);

            // 2. Ids, duplicates, per-mod local checks.
            var byId = new Dictionary<string, int>(StringComparer.Ordinal);
            var hard = new DependencySpec[n][];
            var optional = new DependencySpec[n][];
            var after = new string[n][];
            var before = new string[n][];

            for (int i = 0; i < n; i++)
            {
                var m = mods[i];
                if (string.IsNullOrWhiteSpace(m.Id)) { Set(rm[i], ResolvedState.Failed, "manifest has no id"); continue; }
                if (byId.TryGetValue(m.Id, out var first))
                {
                    Set(rm[i], ResolvedState.Failed, "duplicate mod id '" + m.Id + "' (already used by folder '" + mods[first].Folder + "')");
                    continue;
                }
                byId[m.Id] = i;

                if (m.Disabled) { Set(rm[i], ResolvedState.Disabled, "disabled in the Mod Manager"); continue; }

                if (!string.IsNullOrWhiteSpace(m.MinAslVersion))
                {
                    if (SemVersion.TryParse(m.MinAslVersion, out var min))
                    {
                        if (asl != null && asl < min)
                        {
                            Set(rm[i], ResolvedState.Incompatible, "requires ASL " + min + " or newer (this is ASL " + asl + ")");
                            continue;
                        }
                    }
                    else res.Warnings.Add("'" + m.Id + "': ignoring unparseable minAslVersion '" + m.MinAslVersion + "'");
                }

                string bad = null;
                hard[i] = ParseSpecs(m.Dependencies, true, m.Id, res, ref bad);
                if (bad != null) { Set(rm[i], ResolvedState.Failed, "invalid dependency list: " + bad); continue; }
                string ignored = null;
                optional[i] = ParseSpecs(m.OptionalDependencies, false, m.Id, res, ref ignored);
                after[i] = ParseIds(m.LoadAfter, m.Id, res);
                before[i] = ParseIds(m.LoadBefore, m.Id, res);
            }

            // 3. Skip mods whose hard dependencies cannot be met (transitively); fail hard-dependency cycles.
            bool changed;
            do
            {
                changed = false;
                for (int i = 0; i < n; i++)
                {
                    if (rm[i].State != ResolvedState.Load || hard[i] == null) continue;
                    foreach (var spec in hard[i])
                    {
                        if (!byId.TryGetValue(spec.Id, out var j))
                        {
                            Set(rm[i], ResolvedState.MissingDependency, "requires '" + spec.Id + "' which is not installed");
                        }
                        else if (rm[j].State != ResolvedState.Load)
                        {
                            Set(rm[i], ResolvedState.MissingDependency, "requires '" + spec.Id + "' which " + Why(rm[j]));
                        }
                        else if (!spec.IsSatisfiedBy(mods[j].Version))
                        {
                            Set(rm[i], ResolvedState.MissingDependency, "requires '" + spec.Id + "' " + spec.Range + " but version " + (mods[j].Version ?? "?") + " is installed");
                        }
                        else continue;
                        changed = true;
                        break;
                    }
                }
                if (!changed) changed = FailHardCycles(n, mods, rm, hard, byId);
            }
            while (changed);

            // 4. Order the survivors.
            var succ = new List<int>[n];
            var preds = new List<int>[n];
            var hardPred = new List<int>[n];
            var indeg = new int[n];
            var seen = new HashSet<long>();
            for (int i = 0; i < n; i++) { succ[i] = new List<int>(); preds[i] = new List<int>(); hardPred[i] = new List<int>(); }

            void Edge(int from, int to, bool isHard)
            {
                if (from == to) return;
                if (isHard) hardPred[to].Add(from);
                if (!seen.Add(((long)from << 32) | (uint)to)) return;
                succ[from].Add(to); preds[to].Add(from); indeg[to]++;
            }

            for (int i = 0; i < n; i++)
            {
                if (rm[i].State != ResolvedState.Load) continue;
                if (hard[i] != null)
                    foreach (var s in hard[i]) if (byId.TryGetValue(s.Id, out var j)) Edge(j, i, true);
                if (optional[i] != null)
                    foreach (var s in optional[i])
                        if (byId.TryGetValue(s.Id, out var j) && rm[j].State == ResolvedState.Load && s.IsSatisfiedBy(mods[j].Version)) Edge(j, i, false);
                if (after[i] != null)
                    foreach (var id in after[i]) if (byId.TryGetValue(id, out var j) && rm[j].State == ResolvedState.Load) Edge(j, i, false);
                if (before[i] != null)
                    foreach (var id in before[i]) if (byId.TryGetValue(id, out var j) && rm[j].State == ResolvedState.Load) Edge(i, j, false);
            }

            var ready = new SortedSet<int>();
            var emitted = new bool[n];
            int remaining = 0;
            for (int i = 0; i < n; i++)
            {
                if (rm[i].State != ResolvedState.Load) continue;
                remaining++;
                if (indeg[i] == 0) ready.Add(i);
            }

            while (remaining > 0)
            {
                int v;
                if (ready.Count > 0)
                {
                    v = ready.Min;
                    ready.Remove(v);
                }
                else
                {
                    // Only ordering preferences are left in a cycle: drop them for the first mod (in folder order)
                    // whose hard predecessors are all placed.
                    v = -1;
                    for (int i = 0; i < n && v < 0; i++)
                    {
                        if (rm[i].State != ResolvedState.Load || emitted[i]) continue;
                        bool ok = true;
                        foreach (var p in hardPred[i]) if (!emitted[p]) { ok = false; break; }
                        if (ok) v = i;
                    }
                    if (v < 0) break;   // cannot happen (hard cycles were removed); never loop forever
                    res.Warnings.Add("load-order preferences for '" + mods[v].Id + "' form a cycle; ignoring them");
                }

                emitted[v] = true;
                remaining--;
                rm[v].LoadIndex = res.LoadOrder.Count;
                res.LoadOrder.Add(rm[v]);
                foreach (var s in succ[v])
                {
                    if (emitted[s]) continue;
                    if (--indeg[s] == 0) ready.Add(s);
                }
            }

            // Defensive: anything not emitted (should be impossible) is reported rather than silently dropped.
            for (int i = 0; i < n; i++)
                if (rm[i].State == ResolvedState.Load && !emitted[i])
                    Set(rm[i], ResolvedState.Failed, "could not determine a load order");

            return res;
        }

        private static void Set(ResolvedMod r, ResolvedState state, string reason)
        {
            r.State = state;
            r.Reason = reason;
        }

        private static string Why(ResolvedMod dep)
        {
            switch (dep.State)
            {
                case ResolvedState.Disabled: return "is disabled";
                case ResolvedState.Incompatible: return "is incompatible with this ASL version";
                case ResolvedState.MissingDependency: return "cannot load (" + dep.Reason + ")";
                default: return "failed to load (" + dep.Reason + ")";
            }
        }

        private static DependencySpec[] ParseSpecs(string[] raw, bool strict, string owner, ResolveResult res, ref string firstError)
        {
            if (raw == null || raw.Length == 0) return null;
            var list = new List<DependencySpec>(raw.Length);
            foreach (var r in raw)
            {
                if (string.IsNullOrWhiteSpace(r)) continue;
                if (DependencySpec.TryParse(r, out var spec, out var err)) list.Add(spec);
                else if (strict) { firstError ??= err; }
                else res.Warnings.Add("'" + owner + "': ignoring optional dependency entry: " + err);
            }
            return list.Count == 0 ? null : list.ToArray();
        }

        private static string[] ParseIds(string[] raw, string owner, ResolveResult res)
        {
            if (raw == null || raw.Length == 0) return null;
            var list = new List<string>(raw.Length);
            foreach (var r in raw)
            {
                if (string.IsNullOrWhiteSpace(r)) continue;
                if (DependencySpec.TryParse(r, out var spec, out var err)) list.Add(spec.Id);
                else res.Warnings.Add("'" + owner + "': ignoring ordering entry: " + err);
            }
            return list.Count == 0 ? null : list.ToArray();
        }

        // Tarjan over hard edges among still-loadable mods; every member of a cycle becomes Failed.
        private static bool FailHardCycles(int n, ResolverMod[] mods, ResolvedMod[] rm, DependencySpec[][] hard, Dictionary<string, int> byId)
        {
            var index = new int[n];
            var low = new int[n];
            var onStack = new bool[n];
            for (int i = 0; i < n; i++) index[i] = -1;
            var stack = new Stack<int>();
            int counter = 0;
            bool any = false;

            IEnumerable<int> Out(int v)
            {
                if (hard[v] == null) yield break;
                foreach (var s in hard[v])
                    if (byId.TryGetValue(s.Id, out var j) && rm[j].State == ResolvedState.Load) yield return j;
            }

            void Strong(int v)
            {
                index[v] = low[v] = counter++;
                stack.Push(v); onStack[v] = true;
                foreach (var w in Out(v))
                {
                    if (index[w] < 0) { Strong(w); low[v] = Math.Min(low[v], low[w]); }
                    else if (onStack[w]) low[v] = Math.Min(low[v], index[w]);
                }
                if (low[v] != index[v]) return;

                var comp = new List<int>();
                int x;
                do { x = stack.Pop(); onStack[x] = false; comp.Add(x); } while (x != v);

                bool cyclic = comp.Count > 1;
                if (!cyclic) foreach (var w in Out(v)) if (w == v) cyclic = true;
                if (!cyclic) return;

                comp.Sort();
                var names = new List<string>();
                foreach (var c in comp) names.Add("'" + mods[c].Id + "'");
                var msg = "circular dependency: " + string.Join(" -> ", names) + (comp.Count > 1 ? " -> " + names[0] : "");
                foreach (var c in comp) Set(rm[c], ResolvedState.Failed, msg);
                any = true;
            }

            for (int i = 0; i < n; i++)
                if (rm[i].State == ResolvedState.Load && index[i] < 0) Strong(i);
            return any;
        }
    }
}
