using AntRunnerLib.AssistantDefinitions;
using AntRunnerLib.Functions;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels.SharedModels;
using System.Text.Json;
using YamlDotNet.Serialization;

namespace FunctionCalling
{
    /// <summary>
    /// Provides helper methods for validating and parsing OpenAPI specifications.
    /// </summary>
    public class OpenApiHelper
    {
        /// <summary>
        /// Validates and parses the OpenAPI specification string.
        /// </summary>
        /// <param name="specString">The OpenAPI specification string in JSON or YAML format.</param>
        /// <returns>A <see cref="ValidationResult"/> indicating the validation result.</returns>
        public ValidationResult ValidateAndParseOpenAPISpec(string specString)
        {
            try
            {
                JsonDocument parsedSpec;
                try
                {
                    parsedSpec = JsonDocument.Parse(specString);
                }
                catch
                {
                    var yamlDeserializer = new Deserializer();
                    using var reader = new StringReader(specString);
                    var yamlObject = yamlDeserializer.Deserialize(reader);
                    var jsonString = JsonSerializer.Serialize(yamlObject);
                    parsedSpec = JsonDocument.Parse(jsonString);
                }

                var root = parsedSpec.RootElement;

                if (!root.TryGetProperty("servers", out var servers) || servers.GetArrayLength() == 0)
                {
                    return new ValidationResult { Status = false, Message = "Could not find a valid URL in `servers`", Spec = parsedSpec };
                }

                if (!root.TryGetProperty("paths", out var paths) || paths.GetRawText() == "{}")
                {
                    return new ValidationResult { Status = false, Message = "No paths found in the OpenAPI spec.", Spec = parsedSpec };
                }

                var messages = new List<string>();

                // Additional validation logic...

                return new ValidationResult
                {
                    Status = true,
                    Message = string.Join("\n", messages) ?? "OpenAPI spec is valid.",
                    Spec = parsedSpec
                };
            }
            catch (Exception ex)
            {
                return new ValidationResult { Status = false, Message = "Error parsing OpenAPI spec: " + ex.Message };
            }
        }

        /// <summary>
        /// Extracts tool definitions from the OpenAPI specification.
        /// </summary>
        /// <param name="openapiSpec">The OpenAPI specification as a <see cref="JsonDocument"/>.</param>
        /// <returns>A list of <see cref="ToolDefinition"/> objects extracted from the OpenAPI spec.</returns>
        public List<ToolDefinition> GetToolDefinitions(JsonDocument openapiSpec)
        {
            var toolDefinitions = new List<ToolDefinition>();
            var root = openapiSpec.RootElement;

            // Iterate over all paths in the OpenAPI spec
            foreach (var pathProperty in root.GetProperty("paths").EnumerateObject())
            {
                // Iterate over all HTTP methods in each path
                foreach (var methodProperty in pathProperty.Value.EnumerateObject())
                {
                    var operationObj = methodProperty.Value;

                    // Extract operation ID, or generate one if not present
                    var operationId = operationObj.TryGetProperty("operationId", out var opId)
                        ? opId.GetString()
                        : $"{methodProperty.Name}_{pathProperty.Name}";

                    // Extract description, preferring 'summary' over 'description'
                    var description = operationObj.TryGetProperty("summary", out var summary)
                        ? summary.GetString()
                        : operationObj.TryGetProperty("description", out var desc)
                        ? desc.GetString()
                        : "";

                    var properties = new Dictionary<string, PropertyDefinition>();
                    var required = new List<string>();

                    // Extract 'parameters' schema
                    if (operationObj.TryGetProperty("parameters", out var parameters))
                    {
                        foreach (var param in parameters.EnumerateArray())
                        {
                            var paramName = param.GetProperty("name").GetString();
                            var schema = param.GetProperty("schema");

                            var propertyDefinition = new PropertyDefinition
                            {
                                Type = schema.GetProperty("type").GetString() ?? "string",
                                Description = param.TryGetProperty("description", out var descriptionElement)
                                    ? descriptionElement.GetString()
                                    : null
                            };

                            properties[paramName ?? throw new InvalidOperationException("Parameter name not found")] = propertyDefinition;

                            if (param.GetProperty("required").GetBoolean())
                            {
                                required.Add(paramName);
                            }
                        }
                    }

                    string? contentType = null;
                    if (operationObj.TryGetProperty("requestBody", out var requestBody))
                    {
                        // TODO: DTW don't have a good example of an API with multiple media types, but the spec allows it
                        // This does not handle it... future bug fix required
                        var mediaType = requestBody.GetProperty("content").EnumerateObject().FirstOrDefault();
                        contentType = mediaType.Name;

                        var schema = mediaType.Value.GetProperty("schema");

                        var type = schema.GetProperty("type").GetString() ?? "string";
                        if (type == "object")
                        {
                            if (schema.TryGetProperty("properties", out var propertiesElement))
                            {
                                foreach (var property in propertiesElement.EnumerateObject())
                                {
                                    var propertyDefinition = new PropertyDefinition
                                    {
                                        Type = property.Value.GetProperty("type").GetString() ?? "string",
                                        Description = property.Value.TryGetProperty("description", out var descriptionElement)
                                            ? descriptionElement.GetString()
                                            : null
                                    };

                                    properties[property.Name] = propertyDefinition;
                                }

                                if (schema.TryGetProperty("required", out var requiredElement))
                                {
                                    foreach (var requiredField in requiredElement.EnumerateArray())
                                    {
                                        var requiredFieldVal = requiredField.GetString();
                                        if (requiredFieldVal != null)
                                        {
                                            required.Add(requiredFieldVal);
                                        }
                                    }
                                }
                            }
                        }
                        else if (type == "array")
                        {
                            if (schema.TryGetProperty("items", out var itemsElement))
                            {
                                var body = properties["requestBody"] = new PropertyDefinition
                                {
                                    Type = type,
                                    Items = new ParametersDefinition(),
                                };
                                body.Items!.Properties = new Dictionary<string, PropertyDefinition>();

                                if (itemsElement.TryGetProperty("properties", out var bodyProperties))
                                {
                                    foreach (var param in bodyProperties.EnumerateObject())
                                    {
                                        var paramName = param.Name;

                                        var propertyDefinition = new PropertyDefinition
                                        {
                                            Type = param.Value.GetProperty("type").GetString() ?? "string",
                                            Description = param.Value.TryGetProperty("description", out var descriptionElement)
                                                ? descriptionElement.GetString()
                                                : null,
                                        };

                                        if (param.Value.TryGetProperty("enum", out var enumElement))
                                        {
                                            propertyDefinition.Enum = new List<string>();

                                            foreach (var enumValue in enumElement.EnumerateArray())
                                            {
                                                propertyDefinition.Enum.Add(enumValue.GetString()!);
                                            }
                                        }

                                        body.Items.Properties[paramName] = propertyDefinition;
                                    }
                                }

                                if (itemsElement.TryGetProperty("required", out var requiredElement))
                                {
                                    body.Items.Required = new List<string>();
                                    foreach (var requiredParam in requiredElement.EnumerateArray())
                                    {
                                        var requiredParamVal = requiredParam.GetString();
                                        if (requiredParamVal != null) body.Items.Required.Add(requiredParamVal);
                                    }
                                }
                            }
                        }
                        else
                        {
                            // Handle the case where requestBody is a simple type
                            var body = properties["requestBody"] = new PropertyDefinition
                            {
                                Type = type
                            };
                            if (schema.TryGetProperty("example", out var example))
                            {
                                body.Example = example.GetString();
                            }
                        }
                    }

                    // Construct ParametersDefinition from properties and required fields
                    var parametersDefinition = new ParametersDefinition
                    {
                        Type = "object",
                        Properties = properties,
                        Required = required
                    };

                    // Construct FunctionDefinition from operationId, description, and parametersDefinition
                    var functionDefinition = new FunctionDefinition
                    {
                        Name = operationId ?? throw new InvalidOperationException("Operation ID not found"),
                        Description = description ?? string.Empty,
                        Parameters = parametersDefinition,
                        ContentType = contentType
                    };

                    // Wrap FunctionDefinition in a ToolDefinition
                    var toolDefinition = ToolDefinition.DefineFunction(new AssistantsApiToolFunctionOneOfType
                    {
                        AsObject = functionDefinition
                    });
                    toolDefinitions.Add(toolDefinition);
                }
            }

            return toolDefinitions;
        }

        /// <summary>
        /// Generates request builders based on the OpenAPI specification.
        /// </summary>
        /// <param name="openapiSpec">The OpenAPI specification as a <see cref="JsonDocument"/>.</param>
        /// <param name="toolDefinitions">The list of tool definitions extracted from the OpenAPI spec.</param>
        /// <param name="assistantName">The assistant</param>
        /// <returns>A dictionary of <see cref="ActionRequestBuilder"/> objects with operation IDs as keys.</returns>
        public async Task<Dictionary<string, ActionRequestBuilder>> GetRequestBuilders(JsonDocument openapiSpec, List<ToolDefinition> toolDefinitions, string? assistantName = null)
        {
            var requestBuilders = new Dictionary<string, ActionRequestBuilder>();
            var root = openapiSpec.RootElement;
            var baseUrl = root.GetProperty("servers")[0].GetProperty("url").GetString()
                          ?? throw new InvalidOperationException("Base URL not found");

            var host = (new Uri(baseUrl)).Host;
            var oAuth = false;

            Dictionary<string, string> authHeaders = new();
            if (assistantName != null)
            {
                var authJson = await AssistantDefinitionFiles.GetActionAuth(assistantName);
                if (authJson != null)
                {
                    var domainAuth = JsonSerializer.Deserialize<DomainAuth>(authJson);

                    if (domainAuth != null && domainAuth.HostAuthorizationConfigurations.ContainsKey(host))
                    {
                        var actionAuthConfig = domainAuth.HostAuthorizationConfigurations[host];
                        if(actionAuthConfig.AuthType == AuthType.service_http && actionAuthConfig.HeaderKey != null && actionAuthConfig.HeaderValueEnvironmentVariable != null && Environment.GetEnvironmentVariable(actionAuthConfig.HeaderValueEnvironmentVariable) != null)
                        {
                            authHeaders[actionAuthConfig.HeaderKey] = Environment.GetEnvironmentVariable(actionAuthConfig.HeaderValueEnvironmentVariable)!;
                        }
                        else if(actionAuthConfig.AuthType == AuthType.azure_oauth)
                        {
                            oAuth = true;
                        }
                    }
                }
            }

            foreach (var pathProperty in root.GetProperty("paths").EnumerateObject())
            {
                foreach (var methodProperty in pathProperty.Value.EnumerateObject())
                {
                    var operationObj = methodProperty.Value;
                    var operationId = operationObj.TryGetProperty("operationId", out var opId)
                        ? opId.GetString()
                        : $"{methodProperty.Name}_{pathProperty.Name}";

                    var toolDefinition = toolDefinitions.FirstOrDefault(o => o.Function?.AsObject?.Name == operationId);

                    var actionRequest = new ActionRequestBuilder(
                        baseUrl,
                        pathProperty.Name,
                        methodProperty.Name,
                        operationId!,
                        false, 
                        toolDefinition?.Function?.AsObject?.ContentType ?? "application/json",
                        authHeaders,
                        oAuth
                    );

                    requestBuilders[operationId!] = actionRequest;
                }
            }

            return requestBuilders;
        }
    }
}