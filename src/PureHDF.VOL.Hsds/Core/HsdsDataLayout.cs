using Hsds.Api.V2_0;

namespace PureHDF.VOL.Hsds;

internal class HsdsDataLayout : IH5DataLayout
{
    private readonly LayoutType _layout;

    public HsdsDataLayout(LayoutType layout)
    {
        _layout = layout;

        Class = layout.Class switch
        {
            "H5D_CHUNKED" => H5DataLayoutClass.Chunked,
            _ => throw new Exception($"Unsupported data layout type {layout.Class}.")
        };
    }

    public H5DataLayoutClass Class { get; }

    public ulong[] Chunks
    {
        get
        {
            if (Class == H5DataLayoutClass.Chunked)
            {
                return _layout.Dims!
                    .Select(dim => (ulong)dim)
                    .ToArray();
            }

            else
            {
                throw new Exception("Unsupported data layout.");
            }
        }
    }
}