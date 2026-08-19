using Hsds.Api.V2_0;
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

    // An HSDS attribute arrives fully materialized in the JSON of the request that listed it, so by the
    // time this is reachable there is nothing left to await - the value is deserialized out of memory.
    // Completing synchronously is honest here, rather than a bridge that hides a blocking read.
    public Task<T> ReadAsync<T>(ulong[]? memoryDims = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(Read<T>(memoryDims));
    }

    public Task ReadAsync<T>(T buffer, ulong[]? memoryDims = null, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("This methods is not yet implemented on the HSDS attribute.");
    }
}