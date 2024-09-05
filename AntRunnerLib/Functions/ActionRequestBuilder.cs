using Microsoft.Extensions.DependencyInjection;
using OpenAI.ObjectModels.RequestModels;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FunctionCalling
{
    /// <summary>
    /// Represents an action request to make HTTP calls.
    /// </summary>
    public class ActionRequestBuilder
    {
        private static IHttpClientFactory? _httpClientFactory;

        /// <summary>
        /// Gets or sets the domain of the request.
        /// </summary>
        public string Domain { get; set; }

        /// <summary>
        /// Gets or sets the path of the request.
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Gets or sets the HTTP method used in the request.
        /// </summary>
        public string Method { get; set; }

        /// <summary>
        /// Gets or sets the operation name of the request.
        /// </summary>
        public string Operation { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the request is consequential.
        /// </summary>
        public bool IsConsequential { get; set; }

        /// <summary>
        /// Gets or sets the content type of the request.
        /// </summary>
        public string ContentType { get; set; }

        /// <summary>
        /// Gets the authentication headers for the request.
        /// </summary>
        public Dictionary<string, string> AuthHeaders { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Gets or sets the additional parameters for the request.
        /// </summary>
        public Dictionary<string, object>? Params { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the request uses OAuth for authentication.
        /// </summary>
        public bool oAuth { get; set; } = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="ActionRequestBuilder"/> class with specified parameters.
        /// </summary>
        /// <param name="domain">The domain of the request.</param>
        /// <param name="path">The path of the request.</param>
        /// <param name="method">The HTTP method used in the request.</param>
        /// <param name="operation">The operation name of the request.</param>
        /// <param name="isConsequential">Indicates whether the request is consequential.</param>
        /// <param name="contentType">The content type of the request.</param>
        /// <param name="authHeaders">The authentication headers for the request.</param>
        /// <param name="oAuth">Indicates whether the request uses OAuth for authentication.</param>
        public ActionRequestBuilder(
            string domain,
            string path,
            string method,
            string operation,
            bool isConsequential,
            string contentType,
            Dictionary<string, string> authHeaders,
            bool oAuth = false)
        {
            // Initialize properties
            Domain = domain ?? throw new ArgumentNullException(nameof(domain));
            Path = path ?? throw new ArgumentNullException(nameof(path));
            Method = method ?? throw new ArgumentNullException(nameof(method));
            Operation = operation ?? throw new ArgumentNullException(nameof(operation));
            IsConsequential = isConsequential;
            ContentType = contentType ?? throw new ArgumentNullException(nameof(contentType));
            AuthHeaders = authHeaders;
            this.oAuth = oAuth;

            // Initialize the HTTP Client Factory if not already done
            if (_httpClientFactory == null)
            {
                var serviceProvider = new ServiceCollection().AddHttpClient().BuildServiceProvider();
                _httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            }
        }

        /// <summary>
        /// Executes the action request asynchronously.
        /// </summary>
        /// <param name="oAuthUserAccessToken">Optional OAuth user access token for authentication.</param>
        public async Task<HttpResponseMessage> ExecuteAsync(string? oAuthUserAccessToken = null)
        {
            // Replace path parameters with actual values from Params
            foreach (var param in Params ?? new Dictionary<string, object>())
            {
                if (Path.Contains($"{{{param.Key}}}", StringComparison.OrdinalIgnoreCase))
                {
                    Path = Path.Replace($"{{{param.Key}}}", param.Value.ToString(), StringComparison.OrdinalIgnoreCase);
                }
            }

            // Construct the complete URL
            string url = CreateURL(Domain, Path);

            // Append query parameters to the URL if it's a GET request
            if (Method.Equals(HttpMethod.Get.Method, StringComparison.OrdinalIgnoreCase) && Params != null)
            {
                url += "?" + string.Join("&", Params.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value.ToString()!)}"));
            }

            // Create the HTTP client and request message
            var client = _httpClientFactory!.CreateClient();
            var request = new HttpRequestMessage(new HttpMethod(Method), url);
            request.Headers.TryAddWithoutValidation("Content-Type", ContentType);

            // Add authentication headers to the request
            foreach (var header in AuthHeaders)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            // Add OAuth token to the request if specified
            if (oAuth)
            {
                if (string.IsNullOrEmpty(oAuthUserAccessToken)) throw new ArgumentNullException("No oAuth token");
                request.Headers.TryAddWithoutValidation("Authorization", oAuthUserAccessToken);
            }

            // Add request body content for methods other than GET
            if (!Method.Equals(HttpMethod.Get.Method, StringComparison.OrdinalIgnoreCase) && Params != null)
            {
                var json = JsonSerializer.Serialize(Params);
                request.Content = new StringContent(json, Encoding.UTF8, ContentType);
            }

            // Execute the request based on the specified HTTP method
            switch (Method.ToUpperInvariant())
            {
                case "GET":
                    return await client.SendAsync(request);
                case "POST":
                    request.Method = HttpMethod.Post;
                    return await client.SendAsync(request);
                case "PUT":
                    request.Method = HttpMethod.Put;
                    return await client.SendAsync(request);
                case "DELETE":
                    request.Method = HttpMethod.Delete;
                    return await client.SendAsync(request);
                case "PATCH":
                    request.Method = HttpMethod.Patch;
                    return await client.SendAsync(request);
                default:
                    // Throw an exception if the HTTP method is not supported
                    throw new NotSupportedException($"Unsupported HTTP method: {Method}");
            }
        }

        /// <summary>
        /// Creates the complete URL by combining the domain and path.
        /// </summary>
        /// <param name="domain">The domain of the request.</param>
        /// <param name="path">The path of the request.</param>
        /// <returns>The complete URL as a string.</returns>
        private string CreateURL(string domain, string path)
        {
            var uri = new Uri(new Uri(domain), path);
            return uri.ToString();
        }
    }
}