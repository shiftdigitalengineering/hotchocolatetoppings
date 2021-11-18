using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using HotChocolate.Execution.Configuration;
using HotChocolate.Language;
using HotChocolate.Stitching;
using HotChocolate;
using Dapr.Client;
using System.Collections.Concurrent;

namespace Harmony.Data.Graphql.Stitching.Dapr
{
    internal class DaprExecutorOptionsProvider : IRequestExecutorOptionsProvider, IDaprSubscriptionMessageHandler
    {
        private readonly NameString _schemaName;
        private readonly NameString _topicName;
        private readonly NameString _statestoreDaprComponentName;
        private readonly List<OnChangeListener> _listeners = new List<OnChangeListener>();
        private readonly DaprClient _daprClient;
        private readonly Action<IServiceProvider, string> _onSchemaPublished;
        private readonly IServiceProvider _serviceProvider;
        private ConcurrentDictionary<string, bool> _httpClientNamesCache = new ConcurrentDictionary<string, bool>();        

        public DaprExecutorOptionsProvider(
            IServiceProvider serviceProvider,
            NameString schemaName,
            NameString statestoreDaprComponentName,
            NameString topicName,
            Action<IServiceProvider, string> OnSchemaPublished)
        {
            _serviceProvider = serviceProvider;
            _statestoreDaprComponentName = statestoreDaprComponentName;
            _schemaName = schemaName;
            _topicName = topicName;
            _daprClient = new DaprClientBuilder().Build();
            _onSchemaPublished = OnSchemaPublished;
        }

        public async ValueTask<IEnumerable<IConfigureRequestExecutorSetup>> GetOptionsAsync(
            CancellationToken cancellationToken)
        {
            IEnumerable<RemoteSchemaDefinition> schemaDefinitions =
                await GetSchemaDefinitionsAsync(cancellationToken)
                    .ConfigureAwait(false);

            var factoryOptions = new List<IConfigureRequestExecutorSetup>();

            foreach (RemoteSchemaDefinition schemaDefinition in schemaDefinitions)
            {
                await CreateFactoryOptionsAsync(
                    schemaDefinition,
                    factoryOptions,
                    cancellationToken)
                    .ConfigureAwait(false);

                if (!_httpClientNamesCache.ContainsKey(schemaDefinition.Name.Value))
                {
                    if (_httpClientNamesCache.TryAdd(schemaDefinition.Name.Value, true))
                    {
                        _onSchemaPublished(_serviceProvider, schemaDefinition.Name.Value);
                    }
                }
            }

            return factoryOptions;
        }

        public IDisposable OnChange(Action<IConfigureRequestExecutorSetup> listener) =>
            new OnChangeListener(_listeners, listener);

        public async Task OnMessageAsync(string message)
        {
            string schemaName = message;

            RemoteSchemaDefinition schemaDefinition =
                await GetRemoteSchemaDefinitionAsync(schemaName, CancellationToken.None)
                    .ConfigureAwait(false);

            var factoryOptions = new List<IConfigureRequestExecutorSetup>();
            await CreateFactoryOptionsAsync(schemaDefinition, factoryOptions, default)
                .ConfigureAwait(false);

            bool _lockTaken = false;
            Monitor.Enter(_listeners, ref _lockTaken);

            try
            {
                foreach (OnChangeListener listener in _listeners)
                {
                    foreach (IConfigureRequestExecutorSetup options in factoryOptions)
                    {
                        listener.OnChange(options);
                    }
                }
            }
            finally
            {
                if (_lockTaken)
                {
                    Monitor.Exit(_listeners);
                }
            }

            if (!_httpClientNamesCache.ContainsKey(schemaName))
            {
                if (_httpClientNamesCache.TryAdd(schemaName, true))
                {
                    _onSchemaPublished(_serviceProvider, schemaName);
                }
            }
        }

        private async ValueTask<IEnumerable<RemoteSchemaDefinition>> GetSchemaDefinitionsAsync(
            CancellationToken cancellationToken)
        {
            var items = await _daprClient.GetStateEntryAsync<List<SchemaNameDto>>(_statestoreDaprComponentName.Value, _topicName.Value, ConsistencyMode.Strong, null, cancellationToken: cancellationToken).ConfigureAwait(false);

            var schemaDefinitions = new List<RemoteSchemaDefinition>();

            foreach (var schemaName in items.Value.Select(t => t.Name))
            {
                cancellationToken.ThrowIfCancellationRequested();

                RemoteSchemaDefinition schemaDefinition =
                    await GetRemoteSchemaDefinitionAsync(schemaName, cancellationToken).ConfigureAwait(false);

                schemaDefinitions.Add(schemaDefinition);
            }

            return schemaDefinitions;
        }

        private async Task<RemoteSchemaDefinition> GetRemoteSchemaDefinitionAsync(string schemaName, CancellationToken cancellationToken)
        {
            string key = $"{_topicName}.{schemaName}";
            var json = await _daprClient.GetStateEntryAsync<string>(_statestoreDaprComponentName.Value, key, cancellationToken: cancellationToken).ConfigureAwait(false);
            SchemaDefinitionDto dto = JsonSerializer.Deserialize<SchemaDefinitionDto>(json.Value);

            return new RemoteSchemaDefinition(
                dto.Name,
                Utf8GraphQLParser.Parse(dto.Document),
                dto.ExtensionDocuments.Select(Utf8GraphQLParser.Parse));
        }

        private async Task CreateFactoryOptionsAsync(
           RemoteSchemaDefinition schemaDefinition,
           IList<IConfigureRequestExecutorSetup> factoryOptions,
           CancellationToken cancellationToken)
        {
            await using ServiceProvider services =
                new ServiceCollection()
                    .AddGraphQL(_schemaName)
                    .AddRemoteSchema(
                        schemaDefinition.Name,
                        (sp, ct) => new ValueTask<RemoteSchemaDefinition>(schemaDefinition))
                    .Services
                    .BuildServiceProvider();

            IRequestExecutorOptionsMonitor optionsMonitor =
                services.GetRequiredService<IRequestExecutorOptionsMonitor>();

            RequestExecutorSetup options =
                await optionsMonitor.GetAsync(schemaDefinition.Name, cancellationToken)
                    .ConfigureAwait(false);

            factoryOptions.Add(new ConfigureRequestExecutorSetup(schemaDefinition.Name, options));

            options =
                await optionsMonitor.GetAsync(_schemaName, cancellationToken)
                    .ConfigureAwait(false);

            factoryOptions.Add(new ConfigureRequestExecutorSetup(_schemaName, options));
        }

        private sealed class OnChangeListener : IDisposable
        {
            private readonly List<OnChangeListener> _listeners;
            private readonly Action<IConfigureRequestExecutorSetup> _onChange;

            public OnChangeListener(
                List<OnChangeListener> listeners,
                Action<IConfigureRequestExecutorSetup> onChange)
            {
                _listeners = listeners;
                _onChange = onChange;

                bool _lockTaken = false;
                Monitor.Enter(_listeners, ref _lockTaken);
                try
                {
                    _listeners.Add(this);
                }
                finally
                {
                    if (_lockTaken)
                    {
                        Monitor.Exit(_listeners);
                    }
                }
            }

            public void OnChange(IConfigureRequestExecutorSetup options) =>
                _onChange(options);

            public void Dispose()
            {
                bool _lockTaken = false;
                Monitor.Enter(_listeners, ref _lockTaken);
                try 
                {
                    _listeners.Remove(this);
                }
                finally
                {
                    if (_lockTaken)
                    {
                        Monitor.Exit(_listeners);
                    }
                }
            }
        }
    }
}
