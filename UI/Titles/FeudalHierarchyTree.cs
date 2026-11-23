using System;
using System.Collections.Generic;
using System.Linq;
using CasusBelli.Managers.Titles;
using TaleWorlds.CampaignSystem;

namespace CasusBelli.UI.Titles
{
    // Read-only navigation model. No save data or changes to feudal ownership.
    // The tree is rebuilt when the kingdom or the de jure/de facto mode changes.
    internal sealed class FeudalHierarchyTree
    {
        internal sealed class Entry
        {
            internal readonly FeudalTitle Title;
            internal readonly int Depth;
            internal readonly int ChildCount;

            internal Entry(FeudalTitle title, int depth, int childCount)
            {
                Title = title;
                Depth = depth;
                ChildCount = childCount;
            }
        }

        private readonly Dictionary<FeudalTitle, List<FeudalTitle>> _children =
            new Dictionary<FeudalTitle, List<FeudalTitle>>();
        private readonly List<FeudalTitle> _roots = new List<FeudalTitle>();

        internal IReadOnlyList<FeudalTitle> Roots => _roots;
        internal IReadOnlyCollection<FeudalTitle> Titles => _children.Keys;
        internal int Count => _children.Count;

        internal IReadOnlyList<FeudalTitle> ChildrenOf(FeudalTitle title) =>
            title != null && _children.TryGetValue(title, out var children)
                ? children : (IReadOnlyList<FeudalTitle>)Array.Empty<FeudalTitle>();

        // At de jure mode every title below a crown is shown. At de facto mode
        // show titles possessed by this realm, retaining legal parentage where
        // possible and attaching occupied enclaves directly to its crown.
        internal static FeudalHierarchyTree Build(
            Kingdom kingdom, bool deFacto, IEnumerable<FeudalTitle> crowns,
            IEnumerable<FeudalTitle> titles)
        {
            var tree = new FeudalHierarchyTree();
            var all = (titles ?? Enumerable.Empty<FeudalTitle>())
                .Where(t => t != null).Distinct().ToList();
            var roots = (crowns ?? Enumerable.Empty<FeudalTitle>())
                .Where(t => t != null).Distinct().ToList();
            foreach (var root in roots)
                if (!all.Contains(root)) all.Add(root);
            if (roots.Count == 0) return tree;

            // Saveable Sovereign is the crown, not always the immediate liege.
            // The immediate legal parent is reconstructed from Vassals.
            var parents = new Dictionary<FeudalTitle, FeudalTitle>();
            foreach (var t in all)
                if (t.Vassals != null)
                    foreach (var child in t.Vassals)
                        if (child != null && child != t && !parents.ContainsKey(child))
                            parents.Add(child, t);

            var eligible = new HashSet<FeudalTitle>(roots);
            if (deFacto)
            {
                foreach (var t in all)
                    if (t.DeFactoHolder?.Clan?.Kingdom == kingdom)
                        eligible.Add(t);
            }
            else
            {
                // Reachable legal descendants, with cycle protection.
                var pending = new Stack<FeudalTitle>(roots);
                var traversed = new HashSet<FeudalTitle>();
                while (pending.Count > 0)
                {
                    var title = pending.Pop();
                    if (title == null || !traversed.Add(title)) continue;
                    eligible.Add(title);
                    foreach (var child in title.Vassals ?? Enumerable.Empty<FeudalTitle>())
                        if (child != null && !traversed.Contains(child))
                            pending.Push(child);
                }
                // Recover titles whose Sovereign points at this crown but whose
                // immediate parent disappeared from an older save.
                foreach (var t in all)
                    if (t.Sovereign != null && roots.Contains(t.Sovereign))
                        eligible.Add(t);
            }

            foreach (var title in eligible)
                tree._children[title] = new List<FeudalTitle>();
            tree._roots.AddRange(roots);

            var rootSet = new HashSet<FeudalTitle>(roots);
            foreach (var title in eligible)
            {
                if (rootSet.Contains(title)) continue;

                FeudalTitle parent = null;
                var visited = new HashSet<FeudalTitle> { title };
                var walk = title;
                while (parents.TryGetValue(walk, out var candidate) &&
                       candidate != null && visited.Add(candidate))
                {
                    if (eligible.Contains(candidate))
                    {
                        parent = candidate;
                        break;
                    }
                    walk = candidate;
                }
                if (parent == null && eligible.Contains(title.Sovereign) &&
                    title.Sovereign != title)
                    parent = title.Sovereign;
                if (parent == null) parent = roots[0];
                tree._children[parent].Add(title);
            }
            foreach (var children in tree._children.Values)
                children.Sort(CompareTitles);
            tree._roots.Sort(CompareTitles);
            return tree;
        }

        private static int CompareTitles(FeudalTitle left, FeudalTitle right)
        {
            int rank = left.TitleType.CompareTo(right.TitleType);
            if (rank != 0) return rank;
            return StringComparer.CurrentCultureIgnoreCase.Compare(
                left.FullName?.ToString() ?? left.StringId ?? "",
                right.FullName?.ToString() ?? right.StringId ?? "");
        }

        internal List<Entry> Visible(HashSet<FeudalTitle> expanded)
        {
            var result = new List<Entry>();
            var visited = new HashSet<FeudalTitle>();
            void Walk(FeudalTitle title, int depth)
            {
                if (title == null || !visited.Add(title) || !_children.ContainsKey(title))
                    return;
                var children = _children[title];
                result.Add(new Entry(title, depth, children.Count));
                if (expanded != null && expanded.Contains(title))
                    foreach (var child in children)
                        Walk(child, depth + 1);
            }
            foreach (var root in _roots)
                Walk(root, 0);
            return result;
        }
    }
}