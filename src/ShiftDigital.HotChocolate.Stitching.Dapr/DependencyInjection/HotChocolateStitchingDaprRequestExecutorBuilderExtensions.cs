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
            Func<IServiceProvider, DaprClient> connectionFactory)
        {
            if (connectionFactory is null)
            {
                throw new ArgumentNullException(nameof(connectionFactory));
            }

            topicName.EnsureNotEmpty(nameof(topicName));

            builder.Services.AddSingleton<IRequestExecutorOptionsProvider>(sp =>
            {
                var daprClient = connectionFactory(sp);                
                return new DaprExecutorOptionsProvider(builder.Name, statestoreDaprComponentName, topicName, daprClient);
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
