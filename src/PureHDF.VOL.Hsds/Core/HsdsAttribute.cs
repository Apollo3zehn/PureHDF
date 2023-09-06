using Hsds.Api;
using System.Text.Json;

namespace PureHDF.VOL.Hsds;

internal class HsdsAttribute : IH5Attribute
{
    private readonly AttributeType _attribute;
    private IH5Dataspace? _space;
    private IH5DataType? _type;

    public HsdsAttribute(AttributeType attribute)
    {
        _attribute = attribute;
        Name = attribute.Name;
    }

    public string Name { get; }

    public IH5Dataspace Space
    {
        get
        {
            _space ??= new HsdsDataspace(_attribute.Shape);
            return _space;
        }
    }

    public IH5DataType Type
    {
        get
        {
            _type ??= new HsdsDataType(_attribute.Type);
            return _type;
        }
    }

    public T Read<T>(ulong[]? memoryDims = null)
    {
        if (!_attribute.Value.HasValue)
            throw new Exception("The attribute contains no data.");

        var value = _attribute.Value.Value;

        if (value.ValueKind != JsonValueKind.Array)
            throw new Exception($"Invalid value kind {value.ValueKind}.");

        var data = JsonSerializer.Deserialize<T>(value)
            ?? throw new Exception($"Unable to deserialize data.");

        return data;
    }

    public void Read<T>(T buffer, ulong[]? memoryDims = null)
    {
        throw new NotImplementedException("This methods is not yet implemented on the HSDS attribute.");
    }
}