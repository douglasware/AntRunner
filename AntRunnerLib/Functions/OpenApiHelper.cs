using System.Text;
using Functions;
using YamlDotNet.Serialization;
using static AntRunnerLib.AssistantDefinitions.AssistantDefinitionFiles;

namespace AntRunnerLib.Functions
{
    /// <summary>
    /// Provides helper methods for validating, parsing and using OpenAPI specifications
    /// </summary>
    public class OpenApiHelper
    {
        /// <summary>
        /// Validates and parses the OpenAPI specification string.
        /// </summary>
        /// <param name="specString">The OpenAPI specification string in JSON or YAML format.</param>
        /// <returns>A <see cref="ValidationResult"/> indicating the validation result.</returns>
        public static ValidationResult ValidateAndParseOpenApiSpec(string specString)
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

                return new ValidationResult
                {
                    Status = true,
                    Message = "OpenAPI spec is valid.",
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
        public static List<ToolDefinition> GetToolDefinitionsFromSchema(JsonDocument openapiSpec)
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
                                Description = schema.TryGetProperty("description", out var descriptionElement)
                                    ? descriptionElement.GetString()
                                    : null,
                                Default = schema.TryGetProperty("default", out var defaultElement)
                                    ? (defaultElement.ValueKind == JsonValueKind.String
                                        ? defaultElement.GetString()
                                        : defaultElement.ValueKind == JsonValueKind.Null
                                            ? null
                                            : defaultElement.GetRawText())
                                    : null,
                                Example = schema.TryGetProperty("example", out var exampleElement)
                                    ? exampleElement.GetString()
                                    : null,
                                Enum = (schema.TryGetProperty("enum", out var enumElement)
                                    ? enumElement.EnumerateArray().Select(e => e.GetString()).ToList()
                                    : null)!,
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
                                            : null,
                                        Default = property.Value.TryGetProperty("default", out var defaultElement)
                                            ? (defaultElement.ValueKind == JsonValueKind.String
                                                ? defaultElement.GetString()
                                                : defaultElement.ValueKind == JsonValueKind.Null
                                                    ? null
                                                    : defaultElement.GetRawText())
                                            : null,
                                        Example = property.Value.TryGetProperty("example", out var exampleElement)
                                            ? exampleElement.GetString()
                                            : null,
                                        Enum = (property.Value.TryGetProperty("enum", out var enumElement)
                                            ? enumElement.EnumerateArray().Select(e => e.GetString()).ToList()
                                            : null)!,
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
                                    foreach (var property in bodyProperties.EnumerateObject())
                                    {
                                        var paramName = property.Name;

                                        var propertyDefinition = new PropertyDefinition
                                        {
                                            Type = property.Value.GetProperty("type").GetString() ?? "string",
                                            Description = property.Value.TryGetProperty("description", out var descriptionElement)
                                                ? descriptionElement.GetString()
                                                : null,
                                            Default = property.Value.TryGetProperty("default", out var defaultElement)
                                                ? (defaultElement.ValueKind == JsonValueKind.String
                                                    ? defaultElement.GetString()
                                                    : defaultElement.ValueKind == JsonValueKind.Null
                                                        ? null
                                                        : defaultElement.GetRawText())
                                                : null,
                                            Example = property.Value.TryGetProperty("example", out var exampleElement)
                                                ? exampleElement.GetString()
                                                : null,
                                            Enum = (property.Value.TryGetProperty("enum", out var enumElement)
                                                ? enumElement.EnumerateArray().Select(e => e.GetString()).ToList()
                                                : null)!,
                                        };

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

                    var responseSchemas = new Dictionary<string, JsonElement>();
                    if (operationObj.TryGetProperty("responses", out var responses))
                    {
                        foreach (var response in responses.EnumerateObject())
                        {
                            if (response.Name == "200" && response.Value.TryGetProperty("content", out var content))
                            {
                                var mediaType = content.EnumerateObject().FirstOrDefault();
                                var schema = mediaType.Value.GetProperty("schema");

                                responseSchemas["200"] = schema;
                                break;
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
                        ContentType = contentType,
                        ResponseSchemas = responseSchemas
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
        /// Asynchronously retrieves tool definitions from a collection of OpenAPI schema files.
        /// </summary>
        /// <param name="openApiSchemaFiles">A collection of file paths to OpenAPI schema files.</param>
        /// <returns>A task representing the asynchronous operation. The task result contains a list of <see cref="ToolDefinition"/> objects.</returns>
        public static async Task<List<ToolDefinition>> GetToolDefinitionsFromOpenApiSchemaFiles(IEnumerable<string> openApiSchemaFiles)
        {
            var toolDefinitions = new List<ToolDefinition>();

            foreach (var openApiSchemaFile in openApiSchemaFiles)
            {
                var schema = await GetFile(openApiSchemaFile);
                if (schema == null)
                {
                    Trace.TraceWarning("openApiSchemaFile {0} is null. Ignoring", openApiSchemaFile);
                    continue;
                }

                var json = Encoding.Default.GetString(schema);

                var fileToolDefinitions = GetToolDefinitionsFromJson(json);
                toolDefinitions.AddRange(fileToolDefinitions);
            }

            return toolDefinitions;
        }

        /// <summary>
        /// Retrieves tool definitions from a JSON string representing an OpenAPI specification.
        /// </summary>
        /// <param name="json">A JSON string representing an OpenAPI specification.</param>
        /// <returns>A list of <see cref="ToolDefinition"/> objects parsed from the JSON string.</returns>
        public static List<ToolDefinition> GetToolDefinitionsFromJson(string json)
        {
            var validationResult = ValidateAndParseOpenApiSpec(json);
            var spec = validationResult.Spec;

            if (!validationResult.Status || spec == null)
            {
                Trace.TraceWarning("Json is not a valid openapi spec {0}. Ignoring", json);
                return new List<ToolDefinition>();
            }

            return GetToolDefinitionsFromSchema(spec);
        }
    }
}