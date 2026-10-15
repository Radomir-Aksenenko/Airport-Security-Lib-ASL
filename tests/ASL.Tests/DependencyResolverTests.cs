using System.Collections.Generic;
using System.Linq;
using ASL.Core;
using Xunit;

namespace ASL.Tests
{
    public class DependencyResolverTests
    {
        private static ResolverMod M(string id, string version = "1.0.0", string folder = null, string[] deps = null,
            string[] opt = null, string[] after = null, string[] before = null, string min = null, bool disabled = false)
            => new ResolverMod
            {
                Id = id, Version = version, Folder = folder ?? id, Dependencies = deps, OptionalDependencies = opt,
                LoadAfter = after, LoadBefore = before, MinAslVersion = min, Disabled = disabled,
            };

        private static ResolveResult Run(params ResolverMod[] mods) => DependencyResolver.Resolve(mods, "2.0.0");
        private static string[] Order(ResolveResult r) => r.LoadOrder.Select(x => x.Mod.Id).ToArray();
        private static ResolvedMod Get(ResolveResult r, string id) => r.All.First(x => x.Mod.Id == id);

        [Fact]
        public void NoDeps_OrderIsByFolderName()
        {
            var r = Run(M("c"), M("a"), M("b"));
            Assert.Equal(new[] { "a", "b", "c" }, Order(r));
        }

        [Fact]
        public void FolderOrder_IsCaseInsensitive_AndStable()
        {
            var r = Run(M("x", folder: "Beta"), M("y", folder: "alpha"), M("z", folder: "Charlie"));
            Assert.Equal(new[] { "y", "x", "z" }, Order(r));
        }

        [Fact]
        public void Empty_Input()
        {
            var r = DependencyResolver.Resolve(new List<ResolverMod>(), "2.0.0");
            Assert.Empty(r.All);
            Assert.Empty(r.LoadOrder);
        }

        [Fact]
        public void Dependency_LoadsFirst()
        {
            var r = Run(M("a", deps: new[] { "z" }), M("z"));
            Assert.Equal(new[] { "z", "a" }, Order(r));
        }

        [Fact]
        public void Chain_IsOrdered()
        {
            var r = Run(M("a", deps: new[] { "b" }), M("b", deps: new[] { "c" }), M("c"));
            Assert.Equal(new[] { "c", "b", "a" }, Order(r));
        }

        [Fact]
        public void Order_IsDeterministic_RegardlessOfInputOrder()
        {
            var mods = new[] { M("a", deps: new[] { "d" }), M("b"), M("c", after: new[] { "a" }), M("d") };
            var one = Order(Run(mods));
            var two = Order(Run(mods.Reverse().ToArray()));
            Assert.Equal(one, two);
            Assert.Equal(new[] { "b", "d", "a", "c" }, one);
        }

        [Fact]
        public void MissingDependency_IsSkipped_WithReason()
        {
            var r = Run(M("a", deps: new[] { "ghost" }), M("b"));
            Assert.Equal(ResolvedState.MissingDependency, Get(r, "a").State);
            Assert.Contains("ghost", Get(r, "a").Reason);
            Assert.Contains("not installed", Get(r, "a").Reason);
            Assert.Equal(new[] { "b" }, Order(r));
        }

        [Fact]
        public void MissingDependency_IsTransitive()
        {
            var r = Run(M("a", deps: new[] { "b" }), M("b", deps: new[] { "c" }), M("c", deps: new[] { "ghost" }), M("ok"));
            Assert.Equal(ResolvedState.MissingDependency, Get(r, "c").State);
            Assert.Equal(ResolvedState.MissingDependency, Get(r, "b").State);
            Assert.Equal(ResolvedState.MissingDependency, Get(r, "a").State);
            Assert.Equal(new[] { "ok" }, Order(r));
        }

        [Fact]
        public void DependencyOnDisabledMod_IsSkipped()
        {
            var r = Run(M("a", deps: new[] { "b" }), M("b", disabled: true));
            Assert.Equal(ResolvedState.Disabled, Get(r, "b").State);
            Assert.Equal(ResolvedState.MissingDependency, Get(r, "a").State);
            Assert.Contains("disabled", Get(r, "a").Reason);
            Assert.Empty(r.LoadOrder);
        }

        [Fact]
        public void VersionRange_Unsatisfied_IsMissingDependency()
        {
            var r = Run(M("a", deps: new[] { "lib@>=1.2.0" }), M("lib", version: "1.1.9"));
            Assert.Equal(ResolvedState.MissingDependency, Get(r, "a").State);
            Assert.Contains("1.1.9", Get(r, "a").Reason);
        }

        [Fact]
        public void VersionRange_Satisfied_Loads()
        {
            var r = Run(M("a", deps: new[] { "lib@^1.2.0" }), M("lib", version: "1.4.0"));
            Assert.Equal(new[] { "lib", "a" }, Order(r));
        }

        [Fact]
        public void MalformedDependency_FailsTheMod()
        {
            var r = Run(M("a", deps: new[] { "lib@bogus!" }), M("lib"));
            Assert.Equal(ResolvedState.Failed, Get(r, "a").State);
            Assert.Equal(new[] { "lib" }, Order(r));
        }

        [Fact]
        public void Cycle_FailsAllMembers_AndSkipsDependents()
        {
            var r = Run(M("a", deps: new[] { "b" }), M("b", deps: new[] { "c" }), M("c", deps: new[] { "a" }),
                        M("d", deps: new[] { "a" }), M("e"));
            Assert.Equal(ResolvedState.Failed, Get(r, "a").State);
            Assert.Equal(ResolvedState.Failed, Get(r, "b").State);
            Assert.Equal(ResolvedState.Failed, Get(r, "c").State);
            Assert.Contains("circular", Get(r, "a").Reason);
            Assert.Equal(ResolvedState.MissingDependency, Get(r, "d").State);
            Assert.Equal(new[] { "e" }, Order(r));
        }

        [Fact]
        public void SelfDependency_IsACycle()
        {
            var r = Run(M("a", deps: new[] { "a" }));
            Assert.Equal(ResolvedState.Failed, Get(r, "a").State);
        }

        [Fact]
        public void DuplicateIds_SecondFolderFails()
        {
            var r = Run(M("same", folder: "b_copy", version: "2.0.0"), M("same", folder: "a_orig"));
            Assert.Equal(new[] { "a_orig" }, r.LoadOrder.Select(x => x.Mod.Folder).ToArray());
            var dup = r.All.First(x => x.Mod.Folder == "b_copy");
            Assert.Equal(ResolvedState.Failed, dup.State);
            Assert.Contains("duplicate", dup.Reason);
            Assert.Contains("a_orig", dup.Reason);
        }

        [Fact]
        public void MinAslVersion_TooNew_IsIncompatible()
        {
            var r = Run(M("a", min: "2.1.0"), M("b", min: "2.0.0"), M("c", min: "1.0.0"));
            Assert.Equal(ResolvedState.Incompatible, Get(r, "a").State);
            Assert.Contains("2.1.0", Get(r, "a").Reason);
            Assert.Equal(new[] { "b", "c" }, Order(r));
        }

        [Fact]
        public void MinAslVersion_Garbage_IsIgnoredWithWarning()
        {
            var r = Run(M("a", min: "lots"));
            Assert.Equal(new[] { "a" }, Order(r));
            Assert.Single(r.Warnings);
        }

        [Fact]
        public void Incompatible_Dependency_SkipsDependent()
        {
            var r = Run(M("lib", min: "9.0.0"), M("app", deps: new[] { "lib" }));
            Assert.Equal(ResolvedState.MissingDependency, Get(r, "app").State);
        }

        [Fact]
        public void Disabled_IsReportedAndNotLoaded()
        {
            var r = Run(M("a", disabled: true), M("b"));
            Assert.Equal(ResolvedState.Disabled, Get(r, "a").State);
            Assert.Equal(new[] { "b" }, Order(r));
        }

        [Fact]
        public void OptionalDependency_Present_OrdersBefore()
        {
            var r = Run(M("a", opt: new[] { "z" }), M("z"));
            Assert.Equal(new[] { "z", "a" }, Order(r));
        }

        [Fact]
        public void OptionalDependency_Absent_IsIgnored()
        {
            var r = Run(M("a", opt: new[] { "ghost" }));
            Assert.Equal(new[] { "a" }, Order(r));
            Assert.Equal(ResolvedState.Load, Get(r, "a").State);
        }

        [Fact]
        public void OptionalDependency_Disabled_DoesNotSkipDependent()
        {
            var r = Run(M("a", opt: new[] { "b" }), M("b", disabled: true));
            Assert.Equal(new[] { "a" }, Order(r));
        }

        [Fact]
        public void OptionalDependency_WrongVersion_IsIgnored()
        {
            var r = Run(M("a", opt: new[] { "z@>=5.0.0" }), M("z", version: "1.0.0"));
            Assert.Equal(new[] { "a", "z" }, Order(r));   // no ordering edge: plain folder order
        }

        [Fact]
        public void LoadAfter_And_LoadBefore()
        {
            var r = Run(M("a", after: new[] { "c" }), M("b", before: new[] { "a" }), M("c"));
            // a after c; b before a; ties by folder => c, b, a
            Assert.Equal(new[] { "b", "c", "a" }, Order(r));
        }

        [Fact]
        public void LoadAfter_Missing_IsIgnored()
        {
            var r = Run(M("a", after: new[] { "ghost" }, before: new[] { "ghost2" }));
            Assert.Equal(new[] { "a" }, Order(r));
        }

        [Fact]
        public void SoftCycle_IsBroken_NotFatal()
        {
            var r = Run(M("a", after: new[] { "b" }), M("b", after: new[] { "a" }));
            Assert.Equal(2, r.LoadOrder.Count);
            Assert.All(r.All, x => Assert.Equal(ResolvedState.Load, x.State));
            Assert.NotEmpty(r.Warnings);
        }

        [Fact]
        public void SoftCycle_StillHonoursHardDependencies()
        {
            // b hard-depends on c; a and b form a soft cycle.
            var r = Run(M("a", after: new[] { "b" }), M("b", after: new[] { "a" }, deps: new[] { "c" }), M("c"));
            var o = Order(r);
            Assert.Equal(3, o.Length);
            Assert.True(System.Array.IndexOf(o, "c") < System.Array.IndexOf(o, "b"));
        }

        [Fact]
        public void LoadIndex_MatchesOrder()
        {
            var r = Run(M("b", deps: new[] { "a" }), M("a"), M("x", disabled: true));
            Assert.Equal(0, Get(r, "a").LoadIndex);
            Assert.Equal(1, Get(r, "b").LoadIndex);
            Assert.Equal(-1, Get(r, "x").LoadIndex);
        }

        [Fact]
        public void EveryModAppearsInAll_WithTagPreserved()
        {
            var tag = new object();
            var m = M("a"); m.Tag = tag;
            var r = Run(m, M("b", disabled: true));
            Assert.Equal(2, r.All.Count);
            Assert.Same(tag, r.Find(m).Mod.Tag);
        }

        [Fact]
        public void MissingId_Fails()
        {
            var r = Run(M(null, folder: "oops"));
            Assert.Equal(ResolvedState.Failed, r.All[0].State);
        }

        [Fact]
        public void DependencySpec_Parsing()
        {
            var plain = DependencySpec.Parse("coreutils");
            Assert.Equal("coreutils", plain.Id);
            Assert.False(plain.HasRange);
            Assert.True(plain.IsSatisfiedBy("0.0.1"));
            Assert.True(plain.IsSatisfiedBy("garbage"));

            var ranged = DependencySpec.Parse(" coreutils@>=1.2.0 ");
            Assert.Equal("coreutils", ranged.Id);
            Assert.True(ranged.HasRange);
            Assert.True(ranged.IsSatisfiedBy("1.2.0"));
            Assert.False(ranged.IsSatisfiedBy("1.1.0"));
            Assert.False(ranged.IsSatisfiedBy("garbage"));

            Assert.True(DependencySpec.TryParse("m@^2.0.0", out var caret, out _));
            Assert.True(caret.IsSatisfiedBy("2.9.0"));
            Assert.False(caret.IsSatisfiedBy("3.0.0"));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("@1.0.0")]
        [InlineData("m@not-a-range")]
        public void DependencySpec_Rejects(string text)
        {
            Assert.False(DependencySpec.TryParse(text, out var spec, out var err));
            Assert.Null(spec);
            Assert.False(string.IsNullOrEmpty(err));
            Assert.Throws<System.FormatException>(() => DependencySpec.Parse(text));
        }
    }
}
