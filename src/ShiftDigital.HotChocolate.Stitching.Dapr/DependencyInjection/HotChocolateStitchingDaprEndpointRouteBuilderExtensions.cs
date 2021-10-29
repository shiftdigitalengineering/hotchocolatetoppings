using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Harmony.Data.Graphql.Stitching.Dapr;
using System.IO;
using Microsoft.AspNetCore.Http;
using System.Text;
using HotChocolate;
using System;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class HotChocolateStitchingDaprEndpointRouteBuilderExtensions
    {
        public static void MapGraphQLDapr(this IEndpointRouteBuilder endpointRouteBuilder, IApplicationBuilder app, NameString daprPubSubComponentName, NameString topicName)
        {            
            endpointRouteBuilder.MapPost(string.Concat("/",topicName.Value), async context =>
            {
                var message = "";
                var req = context.Request;
                req.EnableBuffering();

                using (StreamReader reader
                  = new(req.Body, Encoding.UTF8, true, 1024, true))
                {
                    message = reader.ReadToEnd();
                    message = message.TrimStart('"').TrimEnd('"');
                }

                req.Body.Position = 0;

                var handler = app.ApplicationServices.GetRequiredService<IDaprSubscriptionMessageHandler>();
                await handler.OnMessageAsync(message).ConfigureAwait(false);
            })
            .WithTopic(daprPubSubComponentName, topicName);
        }
    }
}
