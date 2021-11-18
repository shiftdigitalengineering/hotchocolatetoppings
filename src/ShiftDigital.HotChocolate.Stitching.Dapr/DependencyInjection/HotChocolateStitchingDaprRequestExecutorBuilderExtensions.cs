using System;
using Microsoft.Extensions.DependencyInjection.Extensions;
using HotChocolate;
using HotChocolate.Execution.Configuration;
using HotChocolate.Stitching.Requests;
using Harmony.Data.Graphql.Stitching.Dapr;
using System.Net.Http;
using ShiftDigital.HotChocolate.Stitching.Dapr.Http;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class HotChocolateStitchingDaprRequestExecutorBuilderExtensions
    {
        public static IRequestExecutorBuilder AddRemoteSchemasFromDapr(
            this IRequestExecutorBuilder builder,
            NameString statestoreDaprComponentName,
            NameString topicName,
            Action<IServiceProvider, string> callbackForNameOnSchemaPublished,
            Action<IServiceProvider, string, HttpClient> callbackForHttpClientOnSchemaPublished)
        {
            topicName.EnsureNotEmpty(nameof(topicName));
            topicName.EnsureNotEmpty(nameof(statestoreDaprComponentName));
            
            if (callbackForHttpClientOnSchemaPublished != null)
            {
                builder.Services.AddHttpClient();
            }
            
            builder.Services.AddSingleton<DownstreamGraphHttpClientFactoryOptionsConfigBySchemaNameCollection>();

            if (callbackForHttpClientOnSchemaPublished != null)
            {
                builder.Services.ConfigureOptions<DownstreamGraphHttpClientFactoryOptions>();
            }

            builder.Services.AddSingleton<IRequestExecutorOptionsProvider>(sp =>
            {
                return new DaprExecutorOptionsProvider(sp, builder.Name, statestoreDaprComponentName, topicName, callbackForNameOnSchemaPublished, callbackForHttpClientOnSchemaPublished);
            });

            builder.Services.AddSingleton<IDaprSubscriptionMessageHandler>(x => (DaprExecutorOptionsProvider)x.GetService<IRequestExecutorOptionsProvider>());

            // Last but not least, we will setup the stitching context which will
            // provide access to the remote executors which in turn use the just configured
            // request executor proxies to send requests to the downstream services.
            builder.Services.TryAddScoped<IStitchingContext, StitchingContext>();

            return builder;
        }

        public static IRequestExecutorBuilder AddRemoteSchemasFromDapr(
            this IRequestExecutorBuilder builder,
            NameString statestoreDaprComponentName,
            NameString topicName,
            Action<IServiceProvider, string> callbackForNameOnSchemaPublished)
        {
            return builder.AddRemoteSchemasFromDapr(statestoreDaprComponentName, topicName, callbackForNameOnSchemaPublished, null);
        }

        public static IRequestExecutorBuilder AddRemoteSchemasFromDapr(
           this IRequestExecutorBuilder builder,
           NameString statestoreDaprComponentName,
           NameString topicName,
           Action<IServiceProvider, string, HttpClient> callbackForHttpClientOnSchemaPublished)
        {
            return builder.AddRemoteSchemasFromDapr(statestoreDaprComponentName, topicName, null, callbackForHttpClientOnSchemaPublished);
        }
    }
}
