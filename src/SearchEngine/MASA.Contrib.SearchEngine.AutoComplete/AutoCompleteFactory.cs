namespace MASA.Contrib.SearchEngine.AutoComplete;

public class AutoCompleteFactory : IAutoCompleteFactory
{
    private readonly List<AutoCompleteRelations> _relations;

    public AutoCompleteFactory(AutoCompleteRelationsOptions options)
    {
        _relations = options.Relations;
    }

    public IAutoCompleteClient CreateClient()
    {
        var item = _relations.SingleOrDefault(r => r.IsDefault) ?? _relations.FirstOrDefault();
        if (item == null)
        {
            throw new ArgumentException("You should use AddAutoComplete before the project starts");
        }
        return new AutoCompleteClient(item.ElasticClient, item.RealIndexName);
    }

    /// <summary>
    /// Create a client corresponding to the index
    /// </summary>
    /// <param name="name">indexName or alias</param>
    /// <returns></returns>
    public IAutoCompleteClient CreateClient(string name)
    {
        var item = _relations.FirstOrDefault(r => r.IndexName == name || r.Alias == name);
        ArgumentNullException.ThrowIfNull(item, nameof(name));
        return new AutoCompleteClient(item.ElasticClient, item.RealIndexName);
    }
}
