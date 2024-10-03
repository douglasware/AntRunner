using System.Reflection;
using System.Text;
using AntRunnerLib.AssistantDefinitions;
using Azure.Core;
using Microsoft.Extensions.DependencyInjection;

namespace AntRunnerLib.Functions
{
    /// <summary>
    /// WebApi indicates an external API call, while LocalFunction indicates a local function call based on a valid static method in a loaded assembly.
    /// </summary>
    public enum ActionType
    {
        /// <summary>
        /// Represents a web API action.
        /// </summary>
        WebApi,

        /// <summary>
        /// Represents a local function action.
        /// </summary>
        LocalFunction
    }

    /// <summary>
    /// Represents an action request to make HTTP calls.
    /// </summary>
    public class ToolCallers
    {
        private static IHttpClientFactory? _httpClientFactory;

        /// <summary>
        /// Generates request builders based on the OpenAPI specification.
        /// </summary>
        /// <param name="openapiSpec">The OpenAPI specification as a <see cref="JsonDocument"/>.</param>
        /// <param name="toolDefinitions">The list of tool definitions extracted from the OpenAPI spec.</param>
        /// <param name="domainAuth"></param>
        /// <returns>A dictionary of <see cref="ToolCallers"/> objects with operation IDs as keys.</returns>
        public static Dictionary<string, ToolCallers> GetToolCallers(JsonDocument openapiSpec, List<ToolDefinition> toolDefinitions, DomainAuth? domainAuth)
        {
            var root = openapiSpec.RootElement;
            var baseUrl = root.GetProperty("servers")[0].GetProperty("url").GetString()
                          ?? throw new InvalidOperationException("Base URL not found");

            var host = (new Uri(baseUrl)).Host;
            var oAuth = false;

            Dictionary<string, string> authHeaders = new();

            if (domainAuth != null && domainAuth.HostAuthorizationConfigurations.TryGetValue(host, out var actionAuthConfig))
            {
                if (actionAuthConfig.AuthType == AuthType.service_http && actionAuthConfig is { HeaderKey: not null, HeaderValueEnvironmentVariable: not null } && Environment.GetEnvironmentVariable(actionAuthConfig.HeaderValueEnvironmentVariable) != null)
                {
                    authHeaders[actionAuthConfig.HeaderKey] = Environment.GetEnvironmentVariable(actionAuthConfig.HeaderValueEnvironmentVariable)!;
                }
                else if (actionAuthConfig.AuthType == AuthType.azure_oauth)
                {
                    oAuth = true;
                }
            }

            return GetToolCallers(toolDefinitions, root, baseUrl, authHeaders, oAuth);
        }

        /// <summary>
        /// Generates request builders based on the OpenAPI specification.
        /// </summary>
        /// <param name="openapiSpec">The OpenAPI specification as a <see cref="JsonDocument"/>.</param>
        /// <param name="toolDefinitions">The list of tool definitions extracted from the OpenAPI spec.</param>
        /// <param name="assistantName">The assistant</param>
        /// <returns>A dictionary of <see cref="ToolCallers"/> objects with operation IDs as keys.</returns>
        public static async Task<Dictionary<string, ToolCallers>> GetToolCallers(JsonDocument openapiSpec, List<ToolDefinition> toolDefinitions, string? assistantName = null)
        {
            var root = openapiSpec.RootElement;
            var baseUrl = root.GetProperty("servers")[0].GetProperty("url").GetString()
                          ?? throw new InvalidOperationException("Base URL not found");

            var host = (new Uri(baseUrl)).Host;

            if (assistantName != null)
            {
                var authJson = await AssistantDefinitionFiles.GetActionAuth(assistantName);
                if (authJson != null)
                {
                    var domainAuth = JsonSerializer.Deserialize<DomainAuth>(authJson);

                    if (domainAuth != null && domainAuth.HostAuthorizationConfigurations.TryGetValue(host, out _))
                    {
                        return GetToolCallers(openapiSpec, toolDefinitions, domainAuth);
                    }
                }
            }

            return GetToolCallers(toolDefinitions, root, baseUrl, new(), false);
        }


        private static Dictionary<string, ToolCallers> GetToolCallers(List<ToolDefinition> toolDefinitions, JsonElement root, string baseUrl, Dictionary<string, string> authHeaders, bool oAuth)
        {
            var toolCallers = new Dictionary<string, ToolCallers>();
            foreach (var pathProperty in root.GetProperty("paths").EnumerateObject())
            {
                foreach (var methodProperty in pathProperty.Value.EnumerateObject())
                {
                    var operationObj = methodProperty.Value;
                    var operationId = operationObj.TryGetProperty("operationId", out var opId)
                        ? opId.GetString()
                        : $"{methodProperty.Name}_{pathProperty.Name}";

                    var toolDefinition = toolDefinitions.FirstOrDefault(o => o.Function?.AsObject?.Name == operationId);
                    var responseSchemas = toolDefinition?.Function?.AsObject?.ResponseSchemas;

                    var actionRequest = new ToolCallers(
                        baseUrl,
                        pathProperty.Name,
                        methodProperty.Name,
                        operationId!,
                        false,
                        toolDefinition?.Function?.AsObject?.ContentType ?? "application/json",
                        responseSchemas!,
                        authHeaders,
                        oAuth
                    );

                    if (responseSchemas != null && responseSchemas.TryGetValue("200", out var schema))
                    {
                        actionRequest.ResponseSchemas["200"] = schema;
                    }

                    toolCallers[operationId!] = actionRequest;
                }
            }
            return toolCallers;
        }

        /// <summary>
        /// Gets the type of action to perform.
        /// </summary>
        public ActionType ActionType => (new Uri(BaseUrl)).Scheme.Contains("tool", StringComparison.InvariantCultureIgnoreCase) ? ActionType.LocalFunction : ActionType.WebApi;

        /// <summary>
        /// Gets or sets the baseUrl of the request.
        /// </summary>
        public string BaseUrl { get; set; }

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
        public Dictionary<string, string> AuthHeaders { get; set; } = new();

        /// <summary>
        /// Gets or sets the additional parameters for the request.
        /// </summary>
        public Dictionary<string, object>? Params { get; set; }

        /// <summary>
        /// Gets or sets the response schemas for the API operations.
        /// This dictionary holds the response schema for the `200` status code for each operation.
        /// The key is the operation ID, and the value is the JSON schema representing the successful response.
        /// </summary>
        public Dictionary<string, JsonElement> ResponseSchemas { get; set; } = new();


        /// <summary>
        /// Gets or sets a value indicating whether the request uses OAuth for authentication.
        /// </summary>
        public bool OAuth { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ToolCallers"/> class with specified parameters.
        /// </summary>
        /// <param name="baseUrl">The baseUrl of the request.</param>
        /// <param name="path">The path of the request.</param>
        /// <param name="method">The HTTP method used in the request.</param>
        /// <param name="operation">The operation name of the request.</param>
        /// <param name="isConsequential">Indicates whether the request is consequential.</param>
        /// <param name="contentType">The content type of the request.</param>
        /// <param name="responseSchemas"></param>
        /// <param name="authHeaders">The authentication headers for the request.</param>
        /// <param name="oAuth">Indicates whether the request uses OAuth for authentication.</param>
        public ToolCallers(string baseUrl,
            string path,
            string method,
            string operation,
            bool isConsequential,
            string contentType,
            Dictionary<string, JsonElement> responseSchemas,
            Dictionary<string, string> authHeaders,
            bool oAuth = false)
        {
            // Initialize properties
            BaseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
            Path = path ?? throw new ArgumentNullException(nameof(path));
            Method = method ?? throw new ArgumentNullException(nameof(method));
            Operation = operation ?? throw new ArgumentNullException(nameof(operation));
            IsConsequential = isConsequential;
            ContentType = contentType ?? throw new ArgumentNullException(nameof(contentType));
            ResponseSchemas = responseSchemas ?? ResponseSchemas;
            AuthHeaders = authHeaders;
            this.OAuth = oAuth;

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
        /// <exception cref="ArgumentNullException"></exception>
        public async Task<HttpResponseMessage> ExecuteWebApiAsync(string? oAuthUserAccessToken = null)
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
            string url = CreateUrl(BaseUrl, Path);

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
            if (OAuth)
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

            TraceInformation($"{nameof(ExecuteWebApiAsync)}:{request.RequestUri!.Host}");

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
        /// Executes the local function asynchronously.
        /// </summary>
        /// <returns>The result of the local function execution.</returns>
        public async Task<object?> ExecuteLocalFunctionAsync()
        {
            // Get the assembly name and method name from the Path
            var methodName = Path.Split('.').Last();

            // Get the containing type name from the Path
            var typeName = Path.Substring(0, Path.LastIndexOf('.'));

            // Get the containing type from the loaded assemblies
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t => t.FullName == typeName);

            if (type == null) throw new InvalidOperationException($"Type {typeName} not found in any loaded assembly");

            // Get all methods with the specified name
            var methods = type.GetMethods().Where(m => m.Name == methodName).ToArray();

            if (methods.Length == 0) throw new InvalidOperationException($"Method {methodName} not found in type {typeName}");

            // Find a method that matches the provided parameters by name and type
            MethodInfo? method = null;
            foreach (var candidateMethod in methods)
            {
                var parameters = candidateMethod.GetParameters();
                if (Params == null && parameters.All(p => p.IsOptional))
                {
                    method = candidateMethod;
                    break;
                }

                if (Params != null)
                {
                    bool match = true;
                    foreach (var param in parameters)
                    {
                        if (!param.IsOptional && !Params.ContainsKey(param.Name!))
                        {
                            match = false;
                            break;
                        }
                    }

                    if (match)
                    {
                        method = candidateMethod;
                        break;
                    }
                }
            }

            if (method == null) throw new InvalidOperationException($"No matching method found for {methodName} with the provided parameters");

            TraceInformation($"{nameof(ExecuteLocalFunctionAsync)}:{methodName}");

            // Get the parameters for the method
            var methodParameters = method.GetParameters();
            var paramValues = new object?[methodParameters.Length];
            for (int i = 0; i < methodParameters.Length; i++)
            {
                var param = methodParameters[i];
                if (Params != null && Params.TryGetValue(param.Name!, out var paramValue))
                {
                    if (paramValue is JsonElement jsonElement)
                    {
                        // Convert JsonElement to the appropriate type
                        paramValues[i] = ConvertJsonElement(jsonElement, param.ParameterType);
                    }
                    else
                    {
                        // Convert the parameter value to the appropriate type
                        paramValues[i] = Convert.ChangeType(paramValue, param.ParameterType);
                    }
                }
                else
                {
                    paramValues[i] = param.HasDefaultValue ? param.DefaultValue : GetDefault(param.ParameterType);
                }
            }

            // Invoke the method with the parameters
            var result = method.Invoke(null, paramValues);

            // If the method is async, await its completion
            if (result is Task task)
            {
                await task.ConfigureAwait(false);
                var taskType = task.GetType();
                if (taskType.IsGenericType)
                {
                    var resultProperty = taskType.GetProperty("Result");
                    if (resultProperty != null)
                    {
                        return resultProperty.GetValue(task);
                    }
                }
                return null;
            }

            return result ?? "Failed to execute function";
        }

        private object? ConvertJsonElement(JsonElement jsonElement, Type targetType)
        {
            return targetType switch
            {
                Type t when t == typeof(int) => jsonElement.GetInt32(),
                Type t when t == typeof(double) => jsonElement.GetDouble(),
                Type t when t == typeof(bool) => jsonElement.GetBoolean(),
                Type t when t == typeof(string) => jsonElement.GetString(),
                _ => throw new InvalidOperationException($"Unsupported target type: {targetType}")
            };
        }

        private static object? GetDefault(Type type)
        {
            if (type.IsValueType)
            {
                return Activator.CreateInstance(type);
            }
            return null; // Reference types default to null
        }

        /// <summary>
        /// Creates the complete URL by combining the baseUrl and path.
        /// </summary>
        /// <param name="domain">The baseUrl of the request.</param>
        /// <param name="path">The path of the request.</param>
        /// <returns>The complete URL as a string.</returns>
        private string CreateUrl(string domain, string path)
        {
            var uri = new Uri(new Uri(domain), path);
            return uri.ToString();
        }

        /// <summary>
        /// Creates a new instance of the <see cref="ToolCallers"/> class that is a copy of the current instance.
        /// </summary>
        /// <returns>A new instance of <see cref="ToolCallers"/> that is a copy of this instance.</returns>
        public ToolCallers Clone()
        {
            // Create a new instance of ToolCallers with the same properties
            return new ToolCallers(
                baseUrl: this.BaseUrl,
                path: this.Path,
                method: this.Method,
                operation: this.Operation,
                isConsequential: this.IsConsequential,
                contentType: this.ContentType,
                responseSchemas: this.ResponseSchemas.ToDictionary(),
                authHeaders: new Dictionary<string, string>(this.AuthHeaders),
                oAuth: this.OAuth)
            {
                // Copy the Params dictionary if it is not null
                Params = this.Params != null ? new Dictionary<string, object>(this.Params) : null
            };
        }
    }
}