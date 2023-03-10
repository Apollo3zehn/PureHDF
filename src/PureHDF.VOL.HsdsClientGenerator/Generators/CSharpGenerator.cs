using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace PureHDF.VOL.HsdsClientGenerator
{
    public class CSharpGenerator
    {
        private readonly GeneratorSettings _settings;

        public CSharpGenerator(GeneratorSettings settings)
        {
            _settings = settings;
        }

        public string Generate(OpenApiDocument document)
        {
            var sourceTextBuilder = new StringBuilder();

            // add clients
            var groupedClients = document.Paths
                .SelectMany(path => path.Value.Operations.First().Value.Tags.Select(tag => (path, tag)))
                .GroupBy(value => value.tag.Name);

            var subClients = groupedClients.Select(group => group.Key);

            // SubClientFields
            sourceTextBuilder.Clear();

            foreach (var subClient in subClients)
            {
                sourceTextBuilder.AppendLine($"    private {subClient}Client _{Shared.FirstCharToLower(subClient)};");
            }

            var subClientFields = sourceTextBuilder.ToString();

            // SubClientFieldAssignments
            sourceTextBuilder.Clear();

            foreach (var subClient in subClients)
            {
                sourceTextBuilder.AppendLine($"        _{Shared.FirstCharToLower(subClient)} = new {subClient}Client(this);");
            }

            var subClientFieldAssignments = sourceTextBuilder.ToString();

            // SubClientProperties
            sourceTextBuilder.Clear();

            foreach (var subClient in subClients)
            {
                sourceTextBuilder.AppendLine("    /// <inheritdoc />");
                sourceTextBuilder.AppendLine($"    public I{subClient}Client {subClient} => _{Shared.FirstCharToLower(subClient)};");
                sourceTextBuilder.AppendLine();
            }

            var subClientProperties = sourceTextBuilder.ToString();

            // SubClientInterfaceProperties
            sourceTextBuilder.Clear();

            foreach (var subClient in subClients)
            {
                sourceTextBuilder.AppendLine(
$@"    /// <summary>
    /// Gets the <see cref=""I{subClient}Client""/>.
    /// </summary>");
                sourceTextBuilder.AppendLine($"    I{subClient}Client {subClient} {{ get; }}");
                sourceTextBuilder.AppendLine();
            }

            var subClientInterfaceProperties = sourceTextBuilder.ToString();

            // SubClientSource
            sourceTextBuilder.Clear();

            foreach (var clientGroup in groupedClients)
            {
                AppendSubClientSourceText(
                    clientGroup.Key,
                    clientGroup.ToDictionary(entry => entry.path.Key, entry => entry.path.Value),
                    sourceTextBuilder);

                sourceTextBuilder.AppendLine();
            }

            var subClientSource = sourceTextBuilder.ToString();

            // Models
            sourceTextBuilder.Clear();

            foreach (var schema in document.Components.Schemas)
            {
                AppendModelSourceText(
                    schema.Key,
                    schema.Value,
                    sourceTextBuilder);

                sourceTextBuilder.AppendLine();
            }

            var models = sourceTextBuilder.ToString();

            // Build final source text
            var basePath = Assembly.GetExecutingAssembly().Location;

            var template = File
                .ReadAllText(Path.Combine(basePath, "..", "Templates", "CSharpTemplate.cs"))
                .Replace("{", "{{")
                .Replace("}", "}}");

            template = Regex
                .Replace(template, "{{{{([0-9]+)}}}}", match => $"{{{match.Groups[1].Value}}}");

            return string.Format(
                template,
                _settings.Namespace,
                _settings.ClientName,
                string.Empty,
                string.Empty,
                subClientFields,
                subClientFieldAssignments,
                subClientProperties,
                subClientSource,
                _settings.ExceptionType,
                models,
                subClientInterfaceProperties);
        }

        private void AppendSubClientSourceText(
            string className,
            IDictionary<string, OpenApiPathItem> methodMap,
            StringBuilder sourceTextBuilder)
        {
            var augmentedClassName = className + "Client";

            // interface
            sourceTextBuilder.AppendLine(
$@"/// <summary>
/// Provides methods to interact with {Shared.SplitCamelCase(className).ToLower()}.
/// </summary>
public interface I{augmentedClassName}
{{");

            foreach (var entry in methodMap)
            {
                if (entry.Value.Parameters.Any())
                    throw new Exception("Parameters on the path item level are not supported.");

                foreach (var operation in entry.Value.Operations)
                {
                    AppendInterfaceMethodSourceText(
                        path: entry.Key,
                        operation.Key, 
                        operation.Value, 
                        sourceTextBuilder);
                        
                    sourceTextBuilder.AppendLine();
                }
            }

            sourceTextBuilder.AppendLine("}");
            sourceTextBuilder.AppendLine();

            // implementation
            sourceTextBuilder
                .AppendLine("/// <inheritdoc />");

            sourceTextBuilder.AppendLine(
$@"public class {augmentedClassName} : I{augmentedClassName}
{{
    private {_settings.ClientName} _client;
    
    internal {augmentedClassName}({_settings.ClientName} client)
    {{
        _client = client;
    }}
");

            foreach (var entry in methodMap)
            {
                if (entry.Value.Parameters.Any())
                    throw new Exception("Parameters on the path item level are not supported.");

                foreach (var operation in entry.Value.Operations)
                {
                    AppendImplementationMethodSourceText(
                        path: entry.Key,
                        operation.Key,
                        operation.Value,
                        sourceTextBuilder);

                    sourceTextBuilder.AppendLine();
                }
            }

            sourceTextBuilder.AppendLine("}");
        }

        private void AppendInterfaceMethodSourceText(
            string path,
            OperationType operationType,
            OpenApiOperation operation,
            StringBuilder sourceTextBuilder)
        {
            var signature = GetMethodSignature(
                path,
                operationType,
                operation,
                out var returnType,
                out var parameters,
                out var body);

            var preparedReturnType = string.IsNullOrWhiteSpace(returnType)
                ? returnType
                : $"<{returnType}>";

            sourceTextBuilder.AppendLine(
$@"    /// <summary>
    /// {operation.Summary}
    /// </summary>");

            foreach (var parameter in parameters)
            {
                sourceTextBuilder.AppendLine($"    /// <param name=\"{parameter.Item2.Name}\">{GetFirstLine(parameter.Item2.Description)}</param>");
            }

            if (operation.RequestBody is not null && body is not null)
                sourceTextBuilder.AppendLine($"    /// <param name=\"{body.Split(" ")[1]}\">{GetFirstLine(operation.RequestBody.Description)}</param>");

            sourceTextBuilder.AppendLine($"    /// <param name=\"cancellationToken\">The token to cancel the current operation.</param>");

            sourceTextBuilder.AppendLine($"    Task{preparedReturnType} {signature};");
        }

        private void AppendImplementationMethodSourceText(
            string path,
            OperationType operationType,
            OpenApiOperation operation,
            StringBuilder sourceTextBuilder)
        {
            var signature = GetMethodSignature(
                path,
                operationType,
                operation,
                out var returnType,
                out var parameters,
                out var bodyParameter);

            sourceTextBuilder
                .AppendLine("    /// <inheritdoc />");

            var isVoidReturnType = string.IsNullOrWhiteSpace(returnType);
            var actualReturnType = isVoidReturnType ? "" : $"<{returnType}>";

            sourceTextBuilder
                .AppendLine($"    public Task{actualReturnType} {signature}")
                .AppendLine($"    {{");

            sourceTextBuilder
                .AppendLine("        var urlBuilder = new StringBuilder();")
                .AppendLine($"        urlBuilder.Append(\"{(path == "/" ? "" : path)}\");");

            // path parameters
            var pathParameters = parameters
                .Where(parameter => parameter.Item2.In == ParameterLocation.Path)
                .ToList();

            foreach (var parameter in pathParameters)
            {
                var parameterName = parameter.Item1.Split(" ")[1];
                var parameterToStringCode = GetParameterToStringCode(parameterName, parameter.Item2.Schema);
                sourceTextBuilder.AppendLine($"        urlBuilder.Replace(\"{{{parameterName}}}\", Uri.EscapeDataString({parameterToStringCode}));");
            }

            // query parameters
            var queryParameters = parameters
                .Where(parameter => parameter.Item2.In == ParameterLocation.Query)
                .ToList();

            if (queryParameters.Any())
            {
                sourceTextBuilder.AppendLine();
                sourceTextBuilder.AppendLine("        var queryValues = new Dictionary<string, string>();");

                foreach (var parameter in queryParameters)
                {
                    var parameterName = parameter.Item1.Split(" ")[1];
                    var parameterToStringCode = GetParameterToStringCode(parameterName, parameter.Item2.Schema);
                    var parameterValue = $"Uri.EscapeDataString({parameterToStringCode})";

                    sourceTextBuilder.AppendLine($"        if ({parameterName} is not null) queryValues.Add(\"{parameterName}\", {parameterValue});");
                }

                sourceTextBuilder.AppendLine();
                sourceTextBuilder.AppendLine("        var __query = queryValues.Any() ? \"?\" + string.Join(\"&\", queryValues.Select(entry => $\"{entry.Key}={entry.Value}\")) : default;");
                sourceTextBuilder.AppendLine("        urlBuilder.Append(__query);");
            }

            // url
            sourceTextBuilder.AppendLine();
            sourceTextBuilder.Append("        var url = urlBuilder.ToString();");
            sourceTextBuilder.AppendLine();

            if (isVoidReturnType)
                returnType = "object";

            var response = operation.Responses.First().Value.Content.FirstOrDefault();

            var acceptHeaderValue = response.Equals(default(KeyValuePair<string, OpenApiMediaType>))
                ? "default"
                : $"\"{response.Key}\"";

            var contentTypeValue = operation.RequestBody is null
                ? "default"
                : $"\"{operation.RequestBody?.Content.Keys.First()}\"";

            var content = bodyParameter is null
                ? "default"
                : operation.RequestBody?.Content.Keys.First() switch
                {
                    "application/json" => $"JsonContent.Create({bodyParameter.Split(" ")[1]}, options: Utilities.JsonOptions)",
                    "application/octet-stream" => $"new StreamContent({bodyParameter.Split(" ")[1]})",
                    _ => throw new Exception($"The media type {operation.RequestBody!.Content.Keys.First()} is not supported.")
                };

            sourceTextBuilder.AppendLine($"        return _client.InvokeAsync<{returnType}>(\"{operationType.ToString().ToUpper()}\", url, {acceptHeaderValue}, {contentTypeValue}, {content}, cancellationToken);");
            sourceTextBuilder.AppendLine($"    }}");
        }

        private void AppendModelSourceText(
            string modelName,
            OpenApiSchema schema,
            StringBuilder sourceTextBuilder)
        {
            // Maybe schema.Extensions[0].x-enumNames would be a better selection.

            if (schema.Enum.Any())
            {
                if (schema.Type != "string")
                    throw new Exception("Only enum of type string is supported.");

                var enumValues = string
                    .Join($",{Environment.NewLine}{Environment.NewLine}", schema.Enum
                    .OfType<OpenApiString>()
                    .Select(current =>
$@"    /// <summary>
    /// {current.Value}
    /// </summary>
    {current.Value}"));

                sourceTextBuilder.AppendLine(
@$"/// <summary>
/// {schema.Description}
/// </summary>");

                sourceTextBuilder.AppendLine(
@$"public enum {modelName}
{{
{enumValues}
}}");

                sourceTextBuilder.AppendLine();
            }

            else
            {
                var parameters = schema.Properties is null
                   ? string.Empty
                   : GetProperties(schema.Properties);

                sourceTextBuilder.AppendLine(
@$"/// <summary>
/// {schema.Description}
/// </summary>");

                if (schema.Properties is not null)
                {
                    foreach (var property in schema.Properties)
                    {
                        sourceTextBuilder.AppendLine($"/// <param name=\"{Shared.FirstCharToUpper(property.Key)}\">{GetFirstLine(property.Value.Description)}</param>");
                    }
                }

                sourceTextBuilder
                    .AppendLine($"public record {modelName}({parameters});");
            }
        }

        private string GetParameterToStringCode(string parameterName, OpenApiSchema schema)
        {
            var type = GetType(schema);

            return type switch
            {
                "DateTime" => $"{parameterName}.ToString(\"o\", CultureInfo.InvariantCulture)",
                _ => $"Convert.ToString({parameterName}, CultureInfo.InvariantCulture)!"
            };
        }

        private string GetProperties(IDictionary<string, OpenApiSchema> propertyMap)
        {
            var methodParameters = propertyMap.Select(entry =>
            {
                var type = GetType(entry.Value);
                var parameterName = Shared.FirstCharToUpper(entry.Key);
                return $"{type} {parameterName}";
            });

            return string.Join(", ", methodParameters);
        }

        private string GetType(string mediaTypeKey, OpenApiMediaType mediaType, bool returnValue = false)
        {
            return mediaTypeKey switch
            {
                "application/octet-stream" => returnValue ? "StreamResponse" : "Stream",
                "application/json" => GetType(mediaType.Schema),
                _ => throw new Exception($"The media type {mediaTypeKey} is not supported.")
            };
        }

        private string GetType(OpenApiSchema schema, bool isRequired = true)
        {
            string type;

            if (schema.Reference is null)
            {
                type = (schema.Type, schema.Format, schema.AdditionalPropertiesAllowed) switch
                {
                    (null, _, _) => schema.OneOf.Count switch
                    {
                        0 => "JsonElement",
                        1 => GetType(schema.OneOf.First()),
                        _ => throw new Exception("Only zero or one entries are supported.")
                    },
                    ("boolean", _, _) => "bool",
                    ("number", _, _) => "double",
                    ("integer", _, _) => "int",
                    ("string", "uri", _) => "Uri",
                    ("string", "guid", _) => "Guid",
                    ("string", "duration", _) => "TimeSpan",
                    ("string", "date-time", _) => "DateTime",
                    ("string", _, _) => "string",
                    ("array", _, _) => $"IReadOnlyList<{GetType(schema.Items)}>",
                    ("object", _, true) => $"IReadOnlyDictionary<string, {(schema.AdditionalProperties is null ? "JsonElement" : GetType(schema.AdditionalProperties))}>",
                    (_, _, _) => throw new Exception($"The schema type {schema.Type} (or one of its formats) is not supported.")
                };
            }

            else
            {
                type = schema.Reference.Id;
            }

            return (schema.Nullable || !isRequired)
                ? $"{type}?"
                : type;
        }

        private string GetMethodSignature(
            string path,
            OperationType operationType,
            OpenApiOperation operation,
            out string returnType,
            out IEnumerable<(string, OpenApiParameter)> parameters,
            out string? bodyParameter)
        {
            if (!(operationType == OperationType.Get ||
                operationType == OperationType.Put ||
                operationType == OperationType.Post ||
                operationType == OperationType.Delete))
                throw new Exception("Only get, put, post or delete operations are supported.");

            if (!_settings.PathToMethodNameMap.TryGetValue($"{operationType}:{path}", out var methodName))
                methodName = _settings.PathToMethodNameMap[path];

            methodName = $"{operationType}{methodName}";

            var asyncMethodName = methodName + "Async";

            // if (operation.Responses.Count != 1)
            //     throw new Exception("Only a single response is supported.");

            var responseEntry = operation.Responses.First();
            var responseType = responseEntry.Key;
            var response = responseEntry.Value;

            // if (responseType != "200")
            //     throw new Exception("Only response type '200' is supported.");

            returnType = response.Content.Count switch
            {
                0 => string.Empty,
                1 => $"{GetType(response.Content.Keys.First(), response.Content.Values.First(), returnValue: true)}",
                2 => $"{GetType(response.Content.Keys.First(), response.Content.Values.First(), returnValue: true)}",
                _ => throw new Exception("Only zero or one response contents are supported.")
            };

            parameters = Enumerable.Empty<(string, OpenApiParameter)>();
            bodyParameter = default;

            if (!operation.Parameters.Any() && operation.RequestBody is null)
            {
                return $"{asyncMethodName}(CancellationToken cancellationToken = default)";
            }

            else
            {
                // if (operation.Parameters.Any(parameter
                //     => parameter.In != ParameterLocation.Path && parameter.In != ParameterLocation.Query))
                //     throw new Exception("Only path or query parameters are supported.");

                parameters = operation.Parameters
                    .Where(parameter => parameter.In == ParameterLocation.Query || parameter.In == ParameterLocation.Path)
                    .Select(parameter => ($"{GetType(parameter.Schema, parameter.Required)} {parameter.Name}{(parameter.Required ? "" : " = default")}", parameter));

                if (operation.RequestBody is not null)
                {
                    if (operation.RequestBody.Content.Count != 1)
                        throw new Exception("Only a single request body content is supported.");

                    var content = operation.RequestBody.Content.First();

                    if (!(content.Key == "application/json" || content.Key == "application/octet-stream"))
                        throw new Exception("Only body content media types application/json or application/octet-stream are supported.");

                    string type;
                    string name;

                    if (operation.RequestBody.Extensions.TryGetValue("x-name", out var value))
                    {
                        if (value is not OpenApiString openApiString)
                            throw new Exception("The actual x-name value type is not supported.");

                        type = GetType(content.Key, content.Value);
                        name = openApiString.Value;
                    }
                    else
                    {
                        type = "JsonElement";
                        name = "body";
                    }
                    
                    bodyParameter = $"{type} {name}";
                }

                var parametersString = bodyParameter == default

                    ? string.Join(", ", parameters
                        .OrderByDescending(parameter => parameter.Item2.Required)
                        .Select(parameter => parameter.Item1))

                    : string.Join(", ", parameters
                        .Concat(new[] { (bodyParameter, default(OpenApiParameter)!) })
                        .OrderByDescending(parameter => parameter.Item2 is null || parameter.Item2.Required)
                        .Select(parameter => parameter.Item1));

                return $"{asyncMethodName}({parametersString}, CancellationToken cancellationToken = default)";
            }
        }

        private static string? GetFirstLine(string? value)
        {
            if (value is null)
                return null;

            using var reader = new StringReader(value);
            return reader.ReadLine();
        }
    }
}