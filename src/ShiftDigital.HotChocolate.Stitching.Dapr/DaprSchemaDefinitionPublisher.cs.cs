using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Client;
using HotChocolate;
using HotChocolate.Stitching;
using HotChocolate.Stitching.SchemaDefinitions;
using Microsoft.Extensions.Logging;
using ShiftDigital.HotChocolate.Stitching.Dapr.Exception;

namespace Harmony.Data.Graphql.Stitching.Dapr
{
    public class DaprSchemaDefinitionPublisher : ISchemaDefinitionPublisher
    {
        private DaprClient _daprClient;
        private readonly NameString _topicName;
        private readonly NameString _pubsubDaprComponentName;
        private readonly NameString _statestoreDaprComponentName;
        private ILogger<DaprSchemaDefinitionPublisher> _logger;
        private ILogger _defaultLogger;
        private ILoggerFactory _loggerFactory = null;

        public DaprSchemaDefinitionPublisher(
            NameString statestoreDaprComponentName,
            NameString pubsubDaprComponentName,
            NameString topicName,
            DaprClient daprClient,
            ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<DaprSchemaDefinitionPublisher>();
            _defaultLogger = loggerFactory.CreateLogger("DaprClientExtensions");
            _daprClient = daprClient;
            _topicName = topicName;
            _pubsubDaprComponentName = pubsubDaprComponentName;
            _statestoreDaprComponentName = statestoreDaprComponentName;
        }

        public async ValueTask PublishAsync(
            RemoteSchemaDefinition schemaDefinition,
            CancellationToken cancellationToken = default)
        {
            string key = $"{_topicName}.{schemaDefinition.Name}";
            string json = SerializeSchemaDefinition(schemaDefinition);

            if (_logger != null)
            {
                _logger.LogInformation("Begin Publish for : " + schemaDefinition.Name);
            }

            bool notAnException = false;

            if (await _daprClient.TrySaveSetStateAsync<SchemaNameDto>(_statestoreDaprComponentName.Value, _topicName.Value, (HashSet<SchemaNameDto> set) =>
             {
                 if (set == null)
                 {
                     if (_logger != null)
                     {
                         _logger.LogInformation("Inside Publish for : " + schemaDefinition.Name + ", save schemanames. Set is empty.");
                     }
                     set = new HashSet<SchemaNameDto>();
                 }
                 var newItm = new SchemaNameDto() { Name = schemaDefinition.Name, };
                 if (set.Count == 0 || !set.Contains(newItm))
                 {
                     if (set.Count > 0 && _logger != null)
                     {
                         _logger.LogInformation("Inside Publish for : " + schemaDefinition.Name + ", save schemanames. Set does not contain this schema, trying to add.");
                     }

                     set.Add(newItm);
                 }
                 else
                 {
                     set = null;
                     notAnException = true;
                     if (_logger != null)
                     {
                         _logger.LogWarning("Inside Publish for : " + schemaDefinition.Name + ", save schemanames. Schema name already exists in this set.");
                     }
                 }

                 return Task.FromResult(set);
             }, retryAttempts: 1, cancellationToken: cancellationToken, logger: _defaultLogger).ConfigureAwait(false))
            {
                if (_logger != null)
                {
                    _logger.LogInformation("Inside Publish for : " + schemaDefinition.Name + ", save Schema. Schema JSON save.");
                }
                await _daprClient.SaveStateAsync<string>(_statestoreDaprComponentName.Value, key, json).ConfigureAwait(false);

                if (_logger != null)
                {
                    _logger.LogInformation("Inside Publish for : " + schemaDefinition.Name + ", save Schema. publish event.");
                }
                await _daprClient.PublishEventAsync(_pubsubDaprComponentName.Value, _topicName.Value, schemaDefinition.Name.Value, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            else
            {
                if (!notAnException)
                {
                    _logger.LogError("Error while trying to publish message to statestore/ pubsub. Schema name: " + schemaDefinition.Name);
                    throw new UnableToPublishSchemaException("Error while trying to publish message to statestore/ pubsub. Schema name: " + schemaDefinition.Name);
                }
            }
        }

        private string SerializeSchemaDefinition(RemoteSchemaDefinition schemaDefinition)
        {
            var dto = new SchemaDefinitionDto
            {
                Name = schemaDefinition.Name.Value,
                Document = schemaDefinition.Document.ToString(false),
            };

            dto.ExtensionDocuments.AddRange(
                schemaDefinition.ExtensionDocuments.Select(t => t.ToString()).ToList());

            return JsonSerializer.Serialize(dto);
        }
    }
}
