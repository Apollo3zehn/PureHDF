using Microsoft.OpenApi.Readers;
using System.Reflection;

namespace PureHDF.HsdsClientGenerator
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            var solutionRoot = args.Length >= 1
                ? args[0]
                : "../../../../../";

            var openApiFileName = args.Length == 2
                ? args[1]
                : "openapi.json";

            // read open API document
            var client = new HttpClient();
            var response = await client.GetAsync("https://raw.githubusercontent.com/HDFGroup/hdf-rest-api/master/openapi.yaml");

            response.EnsureSuccessStatusCode();

            var openApiJsonString = await response.Content.ReadAsStringAsync();

            // TODO: workaround
            openApiJsonString = openApiJsonString.Replace("3.1.0", "3.0.3");

            var document = new OpenApiStringReader()
                .Read(openApiJsonString, out _);

            // generate C# client
            var csharpSettings = new GeneratorSettings(
                Namespace: "PureHDF",
                ClientName: "HsdsClient",
                ExceptionType: "HsdsException");

            var csharpOutputPath = $"{solutionRoot}src/PureHDF/VOL/HsdsClient.g.cs";
            var csharpGenerator = new CSharpGenerator();
            var csharpCode = csharpGenerator.Generate(document, csharpSettings);

            File.WriteAllText(csharpOutputPath, csharpCode);
        }
    }
}