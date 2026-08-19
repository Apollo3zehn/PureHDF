using PureHDF;
using PureHDF.VOL.Native;

namespace PureHdfWasm;

public sealed class H5Node
{
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "";
    public string? Shape { get; set; }
    public string? DataType { get; set; }
    public bool IsExpanded { get; set; }
    public List<H5Node> Attributes { get; } = new();
    public List<H5Node> Children { get; } = new();
}

public static class H5TreeWalker
{
    public static async Task<H5Node> WalkAsync(IH5Object obj, CancellationToken ct = default)
    {
        return await WalkAsync(obj, depth: 0, ct);
    }

    private static async Task<H5Node> WalkAsync(IH5Object obj, int depth, CancellationToken ct)
    {
        var node = new H5Node
        {
            Name = obj.Name,
            IsExpanded = depth < 2
        };

        if (obj is IH5Group group)
        {
            node.Kind = group is NativeFile ? "File" : "Group";

            var children = await group.ChildrenAsync(ct).ConfigureAwait(false);
            foreach (var child in children)
                node.Children.Add(await WalkAsync(child, depth + 1, ct).ConfigureAwait(false));
        }
        else if (obj is IH5Dataset dataset)
        {
            node.Kind = "Dataset";
            node.Shape = FormatShape(dataset.Space);
            node.DataType = FormatType(dataset.Type);
        }
        else
        {
            node.Kind = obj.GetType().Name;
        }

        var attrs = await obj.AttributesAsync(ct).ConfigureAwait(false);
        foreach (var attr in attrs)
        {
            node.Attributes.Add(new H5Node
            {
                Name = attr.Name,
                Kind = "Attribute",
                Shape = FormatShape(attr.Space),
                DataType = FormatType(attr.Type)
            });
        }

        return node;
    }

    private static string FormatShape(IH5Dataspace space)
    {
        return space.Type switch
        {
            H5DataspaceType.Scalar => "Scalar",
            H5DataspaceType.Null => "Null",
            H5DataspaceType.Simple => space.Rank == 0
                ? "Simple []"
                : $"Simple [{string.Join(", ", space.Dimensions)}]",
            _ => space.Type.ToString()
        };
    }

    private static string FormatType(IH5DataType type)
    {
        return $"{type.Class} ({type.Size} bytes)";
    }
}
