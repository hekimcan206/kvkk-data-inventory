using KvkkInventory.Classification;

namespace KvkkInventory.Inventory;

/// <summary>
/// Builds the VERBİS-style inventory: classification (what exists, where) × policy (why, how long, to whom).
/// A category found in the database without a policy entry is reported as a gap: that is exactly the situation a
/// KVKK audit looks for — personal data processed without a documented purpose and retention period.
/// </summary>
public static class InventoryBuilder
{
    public static VerbisInventory Build(ClassificationReport classification, InventoryPolicy policy, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(classification);
        ArgumentNullException.ThrowIfNull(policy);

        var groupOf = policy.SubjectGroups
            .SelectMany(g => g.Tables.Select(t => (Table: t, g.Name)))
            .ToDictionary(x => x.Table, x => x.Name, StringComparer.OrdinalIgnoreCase);

        var entries = new List<InventoryEntry>();
        var gaps = new List<string>();

        var grouped = classification.Columns
            .GroupBy(c => (Group: groupOf.GetValueOrDefault(c.Table, policy.DefaultSubjectGroup), c.Category))
            .OrderBy(g => g.Key.Group, StringComparer.Ordinal)
            .ThenBy(g => g.Key.Category, StringComparer.Ordinal);

        foreach (var group in grouped)
        {
            var first = group.First();
            var details = policy.Processing.GetValueOrDefault($"{group.Key.Group}:{group.Key.Category}")
                ?? policy.Processing.GetValueOrDefault(group.Key.Category);
            var sources = group
                .Select(c => $"{c.Table}.{c.Column} ({c.Type})")
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();

            if (details is null)
            {
                gaps.Add($"{group.Key.Group} / {first.CategoryName}: found in {sources.Length} column(s) but no purpose, legal basis or retention period is documented.");
            }

            if (groupOf.Count > 0 && group.Any(c => !groupOf.ContainsKey(c.Table)))
            {
                var unassigned = group.Where(c => !groupOf.ContainsKey(c.Table)).Select(c => c.Table).Distinct(StringComparer.OrdinalIgnoreCase);
                gaps.Add($"Tables without a data subject group: {string.Join(", ", unassigned)}.");
            }

            details ??= new ProcessingDetails();
            entries.Add(new InventoryEntry(group.Key.Group, group.Key.Category, first.CategoryName, first.Special,
                details.Purposes, details.LegalBases, details.Retention, details.Recipients, details.ForeignTransfer, sources,
                PolicyMissing: details.Purposes.Count == 0));
        }

        return new VerbisInventory(policy.Controller, classification.Dialect, classification.Schema,
            (time ?? TimeProvider.System).GetUtcNow(), entries, [.. gaps.Distinct(StringComparer.Ordinal)]);
    }
}
