namespace MASA.Contrib.SearchEngine.AutoComplete.Options;

public class AutoCompleteRelationsOptions
{
    internal List<AutoCompleteRelations> Relations = new();

    public AutoCompleteRelationsOptions AddRelation(AutoCompleteRelations options)
    {
        Relations.Add(options);
        return this;
    }
}

public class AutoCompleteRelations
{
    internal bool IsDefault { get; }

    internal string IndexName { get; }

    internal string? Alias { get; }

    internal string RealIndexName { get; }

    internal IElasticClient ElasticClient { get; }

    internal AutoCompleteRelations(string indexName, string? alias, IElasticClient elasticClient, bool isDefault)
    {
        IndexName = indexName;
        Alias = alias;
        RealIndexName = alias ?? indexName;
        ElasticClient = elasticClient;
        IsDefault = isDefault;
    }
}
