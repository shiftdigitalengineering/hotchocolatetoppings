using System;
using HotChocolate;
using HotChocolate.Stitching.SchemaDefinitions;
using Harmony.Data.Graphql.Stitching.Dapr;
using Dapr.Client;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class HotChocolateStitchingDaprPublishSchemaDefinitionDescriptorExtensions
    {
        public static IPublishSchemaDefinitionDescriptor PublishToDapr(
            this IPublishSchemaDefinitionDescriptor descriptor,
            string statestoreDaprComponentName,
            string pubsubDaprComponentName,
            string topicName,
            Func<IServiceProvider, DaprClient> daprCreator,
            ILoggerFactory logFactory = null)
        {
            if (daprCreator is null)
            {
                throw new ArgumentNullException(nameof(daprCreator));
            }
            
            if (string.IsNullOrWhiteSpace(pubsubDaprComponentName))
                throw new ArgumentNullException(nameof(pubsubDaprComponentName));
            
            if (string.IsNullOrWhiteSpace(topicName))
                throw new ArgumentNullException(nameof(topicName));

            return descriptor.SetSchemaDefinitionPublisher(sp =>
            {
                var daprClient = daprCreator(sp);

                return new DaprSchemaDefinitionPublisher(statestoreDaprComponentName, pubsubDaprComponentName, topicName, daprClient, logFactory);
            });
        }
    }
}
