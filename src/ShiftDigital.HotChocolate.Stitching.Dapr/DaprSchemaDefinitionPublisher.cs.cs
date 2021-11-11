using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Client;
using HotChocolate;
using HotChocolate.Stitching;
using HotChocolate.Stitching.SchemaDefinitions;

namespace Harmony.Data.Graphql.Stitching.Dapr
{
    public class DaprSchemaDefinitionPublisher : ISchemaDefinitionPublisher
    {
        private DaprClient _daprClient;
        private readonly NameString _topicName;
        private readonly NameString _pubsubDaprComponentName;
        private readonly NameString _statestoreDaprComponentName;

        public DaprSchemaDefinitionPublisher(
            NameString statestoreDaprComponentName,
            NameString pubsubDaprComponentName,
            NameString topicName,
            DaprClient daprClient)
        {
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

            if(await _daprClient.TrySaveSetStateAsync<SchemaNameDto>(_statestoreDaprComponentName.Value, _topicName.Value, (HashSet<SchemaNameDto> set) =>
            {
                if (set == null)
                {
                    set = new HashSet<SchemaNameDto>();
                }
                var newItm = new SchemaNameDto() { Name = schemaDefinition.Name, };
                if (set.Count == 0 || !set.Contains(newItm))
                {
                    set.Add(newItm);
                }
                return Task.FromResult(set);
            }, retryAttempts: 1, cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                await _daprClient.SaveStateAsync<string>(_statestoreDaprComponentName.Value, key, json).ConfigureAwait(false);

                await _daprClient.PublishEventAsync(_pubsubDaprComponentName.Value, _topicName.Value, schemaDefinition.Name.Value, cancellationToken: cancellationToken).ConfigureAwait(false);
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
