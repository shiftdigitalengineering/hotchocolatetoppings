using System;
using Microsoft.Extensions.DependencyInjection.Extensions;
using HotChocolate;
using HotChocolate.Execution.Configuration;
using HotChocolate.Stitching.Requests;
using Harmony.Data.Graphql.Stitching.Dapr;
using Dapr.Client;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class HotChocolateStitchingDaprRequestExecutorBuilderExtensions
    {
        public static IRequestExecutorBuilder AddRemoteSchemasFromDapr(
            this IRequestExecutorBuilder builder,
            NameString statestoreDaprComponentName,
            NameString topicName,
            Action<string> OnSchemaPublished)
        {
            topicName.EnsureNotEmpty(nameof(topicName));
            topicName.EnsureNotEmpty(nameof(statestoreDaprComponentName));            

            builder.Services.AddSingleton<IRequestExecutorOptionsProvider>(sp =>
            {               
                return new DaprExecutorOptionsProvider(builder.Name, statestoreDaprComponentName, topicName, OnSchemaPublished);
            });

            builder.Services.AddSingleton<IDaprSubscriptionMessageHandler>(x => (DaprExecutorOptionsProvider)x.GetService<IRequestExecutorOptionsProvider>());

            // Last but not least, we will setup the stitching context which will
            // provide access to the remote executors which in turn use the just configured
            // request executor proxies to send requests to the downstream services.
            builder.Services.TryAddScoped<IStitchingContext, StitchingContext>();

            return builder;
        }
    }
}
