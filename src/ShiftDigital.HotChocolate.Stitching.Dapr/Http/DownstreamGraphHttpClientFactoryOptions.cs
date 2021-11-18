using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ShiftDigital.HotChocolate.Stitching.Dapr.Http
{
    internal sealed class DownstreamGraphHttpClientFactoryOptions : IConfigureNamedOptions<HttpClientFactoryOptions>
    {
        private readonly DownstreamGraphHttpClientFactoryOptionsConfigBySchemaNameCollection _optionsConfig;
        private readonly ILogger<DownstreamGraphHttpClientFactoryOptions> _logger;

        public DownstreamGraphHttpClientFactoryOptions(DownstreamGraphHttpClientFactoryOptionsConfigBySchemaNameCollection optionsConfig, ILogger<DownstreamGraphHttpClientFactoryOptions> logger)
        {
            _optionsConfig = optionsConfig;
            _logger = logger;
        }

        public void Configure(HttpClientFactoryOptions options)
        {
            _logger.LogWarning($"Configure(HttpClientFactoryOptions options) method is not implemented in class {nameof(DownstreamGraphHttpClientFactoryOptions)}. This should not be called.");
        }

        public void Configure(string name, HttpClientFactoryOptions options)
        {
            _logger.LogTrace("HttpClient factory options configuration called from .NET core HttpClientFactory for downstream graph service: '{name}' .", name);

            if (!_optionsConfig.TryGet(name, out var configure))
                return;

            options.HttpClientActions.Add(httpClient => configure(httpClient));
        }
    }
}
