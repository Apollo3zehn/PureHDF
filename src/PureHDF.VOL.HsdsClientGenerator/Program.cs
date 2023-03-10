using Microsoft.OpenApi.Readers;

namespace PureHDF.VOL.HsdsClientGenerator;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var solutionRoot = args.Length >= 1
            ? args[0]
            : "../../../../../";

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

        // TODO: remove when https://github.com/HDFGroup/hdf-rest-api/issues/10 is resolved
        var pathToMethodNameMap = new Dictionary<string, string>()
        {
            ["/"] = "Domain",
            ["Post:/groups"] = "Group",
            ["Get:/groups"] = "Groups",
            ["/groups/{id}"] = "Group",
            ["/groups/{id}/links"] = "Links",
            ["/groups/{id}/links/{linkname}"] = "Link",
            ["Post:/datasets"] = "Dataset",
            ["Get:/datasets"] = "Datasets",
            ["/datasets/{id}"] = "Dataset",
            ["/datasets/{id}/shape"] = "Shape",
            ["/datasets/{id}/type"] = "DataType",
            ["/datasets/{id}/value"] = "Values",
            ["/datatypes"] = "DataType",
            ["/datatypes/{id}"] = "Datatype",
            ["/{collection}/{obj_uuid}/attributes"] = "Attributes",
            ["/{collection}/{obj_uuid}/attributes/{attr}"] = "Attribute",
            ["/acls"] = "AccessLists",
            ["/acls/{user}"] = "UserAccess",
            ["/groups/{id}/acls"] = "GroupAccessLists",
            ["/groups/{id}/acls/{user}"] = "GroupUserAccess",
            ["/datasets/{id}/acls"] = "DatasetAccessLists",
            ["/datatypes/{id}/acls"] = "DataTypeAccessLists"
        };

        var csharpSettings = new GeneratorSettings(
            Namespace: "PureHDF.VOL.Hsds",
            ClientName: "HsdsClient",
            ExceptionType: "HsdsException",
            pathToMethodNameMap);

        var csharpOutputPath = $"{solutionRoot}src/PureHDF.VOL.Hsds/HsdsClient.g.cs";
        var csharpGenerator = new CSharpGenerator(csharpSettings);
        var csharpCode = csharpGenerator.Generate(document);

        File.WriteAllText(csharpOutputPath, csharpCode);
    }
}