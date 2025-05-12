using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntRunner.Chat
{
    internal class HttpClientUtility
    {
        private static IHttpClientFactory? _httpClientFactory;
        
        internal static HttpClient Get()
        {
            if (_httpClientFactory == null)
            {
                var serviceProvider = new ServiceCollection().AddHttpClient().BuildServiceProvider();
                _httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            }
            return _httpClientFactory.CreateClient();
        }
    }
}
