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
using System.Net.Http;
using ShiftDigital.HotChocolate.Stitching.Dapr.Http;
using Microsoft.Extensions.Logging;

namespace Harmony.Data.Graphql.Stitching.Dapr
{
    internal class DaprExecutorOptionsProvider : IRequestExecutorOptionsProvider, IDaprSubscriptionMessageHandler
    {
        private readonly string _schemaName;
        private readonly string _topicName;
        private readonly string _statestoreDaprComponentName;
        private readonly List<OnChangeListener> _listeners = new List<OnChangeListener>();
        private readonly DaprClient _daprClient;
        private readonly Action<IServiceProvider, string> _callbackForNameOnSchemaPublished;
        private readonly Action<IServiceProvider, string, HttpClient> _callbackForHttpClientOnSchemaPublished;
        private readonly IServiceProvider _serviceProvider;
        private readonly DownstreamGraphHttpClientFactoryOptionsConfigBySchemaNameCollection _publishedSchemaTrackingCollection;

        private DaprExecutorOptionsProvider(
            IServiceProvider serviceProvider,
            string schemaName,
            string statestoreDaprComponentName,
            string topicName)
        {
            _serviceProvider = serviceProvider;
            _publishedSchemaTrackingCollection = _serviceProvider.GetService<DownstreamGraphHttpClientFactoryOptionsConfigBySchemaNameCollection>();
            _statestoreDaprComponentName = statestoreDaprComponentName;
            _schemaName = schemaName;
            _topicName = topicName;
            _daprClient = new DaprClientBuilder().Build();
        }

        public DaprExecutorOptionsProvider(
            IServiceProvider serviceProvider,
            string schemaName,
            string statestoreDaprComponentName,
            string topicName,
            Action<IServiceProvider, string> callbackForNameOnSchemaPublished)
        : this(serviceProvider, schemaName, statestoreDaprComponentName, topicName, callbackForNameOnSchemaPublished, null)
        {
        }

        public DaprExecutorOptionsProvider(
           IServiceProvider serviceProvider,
           string schemaName,
           string statestoreDaprComponentName,
           string topicName,
           Action<IServiceProvider, string, HttpClient> callbackForHttpClientOnSchemaPublished)
        : this(serviceProvider, schemaName, statestoreDaprComponentName, topicName, null, callbackForHttpClientOnSchemaPublished)
        {
        }

        public DaprExecutorOptionsProvider(
           IServiceProvider serviceProvider,
           string schemaName,
           string statestoreDaprComponentName,
           string topicName,
           Action<IServiceProvider, string> callbackForNameOnSchemaPublished,
           Action<IServiceProvider, string, HttpClient> callbackForHttpClientOnSchemaPublished)
        : this(serviceProvider, schemaName, statestoreDaprComponentName, topicName)
        {
            if (callbackForHttpClientOnSchemaPublished != null)
                _callbackForHttpClientOnSchemaPublished = callbackForHttpClientOnSchemaPublished;

            if(callbackForNameOnSchemaPublished != null)
                _callbackForNameOnSchemaPublished = callbackForNameOnSchemaPublished;
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

                HandlePublishedSchemaMessages(schemaDefinition.Name);
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

            HandlePublishedSchemaMessages(schemaName);
        }

        private void HandlePublishedSchemaMessages(string schemaName)
        {
            if (!_publishedSchemaTrackingCollection.Exists(schemaName))
            {
                if (_callbackForNameOnSchemaPublished == null && _callbackForHttpClientOnSchemaPublished == null)
                {
                    var logger = _serviceProvider.GetService<ILogger<DaprExecutorOptionsProvider>>();
                    logger.LogWarning($"No callback found for the published schema name: '{schemaName}'.");
                    return;
                }

                bool _publishedSchemaTrackingCollectionAdded = false;
                if (_callbackForHttpClientOnSchemaPublished != null)
                {
                    _publishedSchemaTrackingCollection.TryAdd(schemaName,
                        httpClient => _callbackForHttpClientOnSchemaPublished(_serviceProvider, schemaName, httpClient));

                    _publishedSchemaTrackingCollectionAdded = true;

                }
                if (_callbackForNameOnSchemaPublished != null)
                {
                    if (!_publishedSchemaTrackingCollectionAdded)
                    {
                        _publishedSchemaTrackingCollection.TryAdd(schemaName, null);
                    }
                    _callbackForNameOnSchemaPublished(_serviceProvider, schemaName);
                }
            }
        }

        private async ValueTask<IEnumerable<RemoteSchemaDefinition>> GetSchemaDefinitionsAsync(
            CancellationToken cancellationToken)
        {
            var items = await _daprClient.GetStateEntryAsync<List<SchemaNameDto>>(_statestoreDaprComponentName, _topicName, ConsistencyMode.Strong, null, cancellationToken: cancellationToken).ConfigureAwait(false);

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
            var json = await _daprClient.GetStateEntryAsync<string>(_statestoreDaprComponentName, key, cancellationToken: cancellationToken).ConfigureAwait(false);
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
