using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.Http.Headers;

namespace ShiftDigital.HotChocolate.Stitching.Dapr.Http
{
    internal sealed class DownstreamGraphHttpClientFactoryOptionsConfigBySchemaNameCollection
    {
        private readonly ConcurrentDictionary<string, Action<HttpClient>> _httpClientNamesCache = new();
        private readonly IServiceProvider _sp;
        private readonly ILogger<DownstreamGraphHttpClientFactoryOptionsConfigBySchemaNameCollection> _logger;

        public DownstreamGraphHttpClientFactoryOptionsConfigBySchemaNameCollection(IServiceProvider sp, ILogger<DownstreamGraphHttpClientFactoryOptionsConfigBySchemaNameCollection> logger)
        {
            _sp = sp;
            _logger = logger;
        }

        public bool Exists(string sName)
        {
            return _httpClientNamesCache.ContainsKey(sName);
        }

        public bool TryAdd(string sName, Action<HttpClient> configHttpClientCallback)
        {
            return _httpClientNamesCache.TryAdd(sName, httpClient => configHttpClientCallback(httpClient));
        }

        public bool TryAdd(string sName)
        {
            return _httpClientNamesCache.TryAdd(sName, null);
        }

        public bool TryGet(string sName, out Action<HttpClient> options)
        {
            return _httpClientNamesCache.TryGetValue(sName, out options);
        }
    }
}
