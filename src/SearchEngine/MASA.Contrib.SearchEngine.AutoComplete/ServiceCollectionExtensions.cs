namespace MASA.Contrib.SearchEngine.AutoComplete;

public static class ServiceCollectionExtensions
{
    public static IElasticClient AddAutoComplete(
        this IElasticClient elasticClient,
        IServiceCollection services)
        => elasticClient.AddAutoComplete<long>(services);

    public static IElasticClient AddAutoComplete<TValue>(
        this IElasticClient elasticClient,
        IServiceCollection services)
        => elasticClient.AddAutoComplete<AutoCompleteDocument<TValue>, TValue>(services);

    public static IElasticClient AddAutoComplete<TDocument, TValue>(
        this IElasticClient elasticClient,
        IServiceCollection services)
        where TDocument : AutoCompleteDocument<TValue>
    {
        var indexName = elasticClient.ConnectionSettings.DefaultIndex;
        if (string.IsNullOrEmpty(indexName))
        {
            throw new ArgumentNullException(nameof(elasticClient.ConnectionSettings.DefaultIndex), "The default IndexName is not set");
        }

        return elasticClient.AddAutoComplete<TDocument, TValue>(services, null);
    }

    public static IElasticClient AddAutoComplete(
        this IElasticClient elasticClient,
        IServiceCollection services,
        Action<AutoCompleteOptions<AutoCompleteDocument<long>, long>>? action)
        => elasticClient.AddAutoComplete<long>(services, action);

    public static IElasticClient AddAutoComplete<TValue>(
        this IElasticClient elasticClient,
        IServiceCollection services,
        Action<AutoCompleteOptions<AutoCompleteDocument<TValue>, TValue>>? action)
        => elasticClient.AddAutoComplete<AutoCompleteDocument<TValue>, TValue>(services, action);

    public static IElasticClient AddAutoComplete<TDocument, TValue>(
        this IElasticClient elasticClient,
        IServiceCollection services,
        Action<AutoCompleteOptions<TDocument, TValue>>? action)
        where TDocument : AutoCompleteDocument<TValue>
    {
        AutoCompleteOptions<TDocument, TValue> options = new AutoCompleteOptions<TDocument, TValue>();
        action?.Invoke(options);
        services.AddAutoCompleteCore(elasticClient, options);
        return elasticClient;
    }

    private static void AddAutoCompleteCore<TDocument, TValue>(this IServiceCollection services,
        IElasticClient elasticClient,
        AutoCompleteOptions<TDocument, TValue> option)
        where TDocument : AutoCompleteDocument<TValue>
    {
        ArgumentNullException.ThrowIfNull(services);

        var client = new DefaultMasaElasticClient(elasticClient);
        string indexName = option.IndexName??throw new ArgumentNullException(nameof(option.IndexName));

        services.AddLogging();

        services.TryAddSingleton(new AutoCompleteRelationsOptions());

        services.TryAddAutoCompleteRelation(new AutoCompleteRelations(indexName, option.Alias, elasticClient, option.IsDefault));

        services.TryAddSingleton<IAutoCompleteFactory, AutoCompleteFactory>();

        services.TryAddSingleton(serviceProvider =>
            serviceProvider.GetRequiredService<IAutoCompleteFactory>().CreateClient());

        IAliases? aliases = null;
        if (option.Alias != null)
        {
            aliases = new Aliases();
            aliases.Add(option.Alias, new Alias());
        }

        var existsResponse = client.IndexExistAsync(indexName, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
        if (!existsResponse.IsValid || existsResponse.Exists)
        {
            if (!existsResponse.IsValid)
            {
                var logger = services.BuildServiceProvider().GetRequiredService<ILogger<IAutoCompleteClient>>();
                logger.LogError($"AutoComplete: Initialization index is abnormal, {existsResponse.Message}");
            }

            return;
        }

        IAnalysis analysis = new AnalysisDescriptor();
        analysis.Analyzers = new Analyzers();
        analysis.TokenFilters = new TokenFilters();
        IIndexSettings indexSettings = new IndexSettings()
        {
            Analysis = analysis,
        };
        string analyzer = "ik_max_word_pinyin";
        if (option.IndexSettingAction != null)
        {
            option.IndexSettingAction.Invoke(indexSettings);
        }
        else
        {
            string defaultAnalyzer = "ik_max_word";
            string pinyinFilter = "pinyin";
            string wordDelimiterFilter = "word_delimiter";
            indexSettings.Analysis.Analyzers.Add(analyzer, new CustomAnalyzer()
            {
                Filter = new[] { pinyinFilter, wordDelimiterFilter },
                Tokenizer = defaultAnalyzer
            });
            indexSettings.Analysis.TokenFilters.Add(pinyinFilter, new PinYinTokenFilterDescriptor());
        }

        TypeMappingDescriptor<TDocument> mapping = new TypeMappingDescriptor<TDocument>();
        if (option.Action != null)
        {
            option.Action.Invoke(mapping);
        }
        else
        {
            mapping = mapping
                .AutoMap<TDocument>()
                .Properties(ps =>
                    ps.Text(s =>
                        s.Name(n => n.Id)
                            .Analyzer(analyzer)
                    )
                )
                .Properties(ps =>
                    ps.Text(s =>
                        s.Name(n => n.Text)
                            .Analyzer(analyzer)
                    )
                );
        }
        var createIndexResponse = client.CreateIndexAsync(indexName, new CreateIndexOptions()
        {
            Aliases = aliases,
            Mappings = mapping,
            IndexSettings = indexSettings
        }).ConfigureAwait(false).GetAwaiter().GetResult();
        if (!createIndexResponse.IsValid)
        {
            var logger = services.BuildServiceProvider().GetRequiredService<ILogger<IAutoCompleteClient>>();
            logger.LogWarning($"AutoComplete: Initialization index is abnormal, {createIndexResponse.Message}");
        }
    }

    private static void TryAddAutoCompleteRelation(this IServiceCollection services, AutoCompleteRelations relation)
    {
        var serviceProvider = services.BuildServiceProvider();
        var relationsOptions = serviceProvider.GetRequiredService<AutoCompleteRelationsOptions>();

        if (relationsOptions.Relations.Any(r => r.Alias == relation.Alias || r.IndexName == relation.IndexName))
            throw new ArgumentException($"indexName or alias exists");

        if (relation.IsDefault && relationsOptions.Relations.Any(r => r.IsDefault))
            throw new ArgumentNullException(nameof(ElasticsearchRelations.IsDefault), "ElasticClient can only have one default");

        relationsOptions.AddRelation(relation);
    }
}
