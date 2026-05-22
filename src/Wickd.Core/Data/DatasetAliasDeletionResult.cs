#nullable enable

namespace Wickd.Data;

/// <summary>
/// Summarizes a removed dataset alias and the aliases left in the catalog.
/// </summary>
/// <param name="DeletedAlias">Dataset alias removed from the catalog.</param>
/// <param name="RemainingAliases">Dataset aliases still present after deletion.</param>
internal sealed record DatasetAliasDeletionResult(
    DatasetAlias DeletedAlias,
    IReadOnlyList<DatasetAlias> RemainingAliases);
