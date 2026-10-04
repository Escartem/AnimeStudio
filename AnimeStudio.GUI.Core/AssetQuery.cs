using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using AnimeStudio.App;

namespace AnimeStudio.GUI.Core
{
    public enum AssetColumn
    {
        Name,
        Container,
        Type,
        PathID,
        Size,
        Hash
    }

    public sealed record AssetFilter(IReadOnlySet<ClassIDType> Types, bool ModelsOnly, string Search);

    public readonly record struct AssetSort(AssetColumn Column, bool Descending);

    // Filtering and sorting run on index arrays off the UI thread, the source rows are never reordered.
    public static class AssetQuery
    {
        public static Regex CreateRegex(string search)
        {
            if (string.IsNullOrEmpty(search))
                return null;
            return new Regex(search, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        public static int[] Run(IReadOnlyList<AssetRow> rows, AssetFilter filter, AssetSort? sort, CancellationToken token)
        {
            var regex = CreateRegex(filter.Search);
            var types = filter.Types;
            var result = new List<int>(rows.Count);
            for (var i = 0; i < rows.Count; i++)
            {
                if ((i & 0xFFF) == 0)
                    token.ThrowIfCancellationRequested();
                var row = rows[i];
                if (types != null && !types.Contains(row.Type))
                    continue;
                if (filter.ModelsOnly && !IsModel(row))
                    continue;
                if (regex != null && !(regex.IsMatch(row.Name) || regex.IsMatch(row.Container) || regex.IsMatch(row.PathID.ToString(CultureInfo.InvariantCulture))))
                    continue;
                result.Add(i);
            }

            var indices = result.ToArray();
            if (sort is { } s)
                Sort(rows, indices, s, token);
            return indices;
        }

        // Matches the old "Filter models only": other types stay, GameObjects/Animators without a model go.
        private static bool IsModel(AssetRow row) => row.Asset switch
        {
            GameObject m_GameObject => m_GameObject.HasModel(),
            Animator m_Animator => m_Animator.m_GameObject.TryGet(out var gameObject) && gameObject.HasModel(),
            _ => true,
        };

        public static void Sort(IReadOnlyList<AssetRow> rows, int[] indices, AssetSort sort, CancellationToken token)
        {
            Comparison<int> compare = sort.Column switch
            {
                AssetColumn.PathID => (a, b) => rows[a].PathID.CompareTo(rows[b].PathID),
                AssetColumn.Size => (a, b) => rows[a].FullSize.CompareTo(rows[b].FullSize),
                AssetColumn.Type => (a, b) => string.CompareOrdinal(rows[a].TypeString, rows[b].TypeString),
                AssetColumn.Container => (a, b) => string.CompareOrdinal(rows[a].Container, rows[b].Container),
                AssetColumn.Hash => (a, b) => string.CompareOrdinal(rows[a].CachedHash, rows[b].CachedHash),
                _ => (a, b) => string.Compare(rows[a].Name, rows[b].Name, StringComparison.OrdinalIgnoreCase),
            };
            token.ThrowIfCancellationRequested();
            // Index tiebreak keeps the sort stable.
            if (sort.Descending)
                Array.Sort(indices, (a, b) => { var c = compare(b, a); return c != 0 ? c : a.CompareTo(b); });
            else
                Array.Sort(indices, (a, b) => { var c = compare(a, b); return c != 0 ? c : a.CompareTo(b); });
        }
    }
}
