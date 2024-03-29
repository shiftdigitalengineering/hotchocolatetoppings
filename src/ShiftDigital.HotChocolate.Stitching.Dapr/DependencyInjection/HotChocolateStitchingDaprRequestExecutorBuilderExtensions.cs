using System;
using Microsoft.Extensions.DependencyInjection.Extensions;
using HotChocolate;
using HotChocolate.Execution.Configuration;
using HotChocolate.Stitching.Requests;
using Harmony.Data.Graphql.Stitching.Dapr;
using System.Net.Http;
using ShiftDigital.HotChocolate.Stitching.Dapr.Http;
using HotChocolate.Execution;
using System.Linq;
using HotChocolate.Internal;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class HotChocolateStitchingDaprRequestExecutorBuilderExtensions
    {
        public static IRequestExecutorBuilder AddRemoteSchemasFromDapr(
            this IRequestExecutorBuilder builder,
            string statestoreDaprComponentName,
            string topicName,
            Action<IServiceProvider, string> callbackForNameOnSchemaPublished,
            Action<IServiceProvider, string, HttpClient> callbackForHttpClientOnSchemaPublished)
        {
            if (string.IsNullOrWhiteSpace(topicName))
                throw new ArgumentNullException(nameof(topicName));
            
            if (string.IsNullOrWhiteSpace(statestoreDaprComponentName))
                throw new ArgumentNullException(nameof(statestoreDaprComponentName));

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
            builder.ConfigureSchemaServices(c => c.TryAddSingleton<IRequestContextEnricher, StitchingContextEnricher>());

            if (builder.Services.All(t => t.ImplementationType !=
                typeof(StitchingContextParameterExpressionBuilder)))
            {
                builder.Services.AddSingleton<IParameterExpressionBuilder, StitchingContextParameterExpressionBuilder>();
            }

            return builder;
        }

        public static IRequestExecutorBuilder AddRemoteSchemasFromDapr(
            this IRequestExecutorBuilder builder,
            string statestoreDaprComponentName,
            string topicName,
            Action<IServiceProvider, string> callbackForNameOnSchemaPublished)
        {
            return builder.AddRemoteSchemasFromDapr(statestoreDaprComponentName, topicName, callbackForNameOnSchemaPublished, null);
        }

        public static IRequestExecutorBuilder AddRemoteSchemasFromDapr(
           this IRequestExecutorBuilder builder,
           string statestoreDaprComponentName,
           string topicName,
           Action<IServiceProvider, string, HttpClient> callbackForHttpClientOnSchemaPublished)
        {
            return builder.AddRemoteSchemasFromDapr(statestoreDaprComponentName, topicName, null, callbackForHttpClientOnSchemaPublished);
        }
    }
}
