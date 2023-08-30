using System.Buffers;
using System.Reflection;
using System.Text.Json;
using Hsds.Api;
using PureHDF.Selections;

namespace PureHDF.VOL.Hsds;

internal class HsdsDataset : HsdsAttributableObject, IH5Dataset
{
    private static readonly MethodInfo _methodInfoReadCoreAsync = typeof(HsdsDataset)
        .GetMethod(nameof(ReadCoreAsync), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private IH5Dataspace? _space;
    private IH5DataType? _type;
    private IH5DataLayout? _layout;
    private readonly GetDatasetResponse _dataset;

    public HsdsDataset(InternalHsdsConnector connector, HsdsNamedReference reference) : base(connector, reference)
    {
        _dataset = connector.Client.Dataset.GetDataset(Id, connector.DomainName);
    }

    public IH5Dataspace Space
    {
        get
        {
            _space ??= new HsdsDataspace(_dataset.Shape);
            return _space;
        }
    }

    public IH5DataType Type
    {
        get
        {
            _type ??= new HsdsDataType(_dataset.Type);
            return _type;
        }
    }

    public IH5DataLayout Layout
    {
        get
        {
            _layout ??= new HsdsDataLayout(_dataset.Layout);
            return _layout;
        }
    }

    public IH5FillValue FillValue => throw new NotImplementedException();

    public T Read<T>(
        Selection? fileSelection = null,
        Selection? memorySelection = null,
        ulong[]? memoryDims = null)
    {
        if (DataUtils.IsReferenceOrContainsReferences(typeof(T)))
            throw new Exception("Only types which meet the unmanaged constraint are supported.");

        var (elementType, _) = WriteUtils.GetElementType(typeof(T));

        // TODO cache this
        var method = _methodInfoReadCoreAsync.MakeGenericMethod(typeof(T), elementType, typeof(AsyncReader));

        var result = ((Task<T>)method.Invoke(this, new object?[] 
        {
            false /* useAsync */,
            fileSelection,
            memorySelection,
            memoryDims,
            default(CancellationToken)
        })!).GetAwaiter().GetResult();

        return result;
    }
    
    public async Task<T> ReadAsync<T>(
        Selection? fileSelection = null,
        Selection? memorySelection = null,
        ulong[]? memoryDims = null,
        CancellationToken cancellationToken = default)
    {
        if (DataUtils.IsReferenceOrContainsReferences(typeof(T)))
            throw new Exception("Only types which meet the unmanaged constraint are supported.");

        var (elementType, _) = WriteUtils.GetElementType(typeof(T));

        // TODO cache this
        var method = _methodInfoReadCoreAsync.MakeGenericMethod(typeof(T), elementType, typeof(AsyncReader));

        var result = await (Task<T>)method.Invoke(this, new object?[] 
        {
            true /* useAsync */,
            fileSelection,
            memorySelection,
            memoryDims,
            cancellationToken
        })!;

        return result;
    }

    private async Task<TResult?> ReadCoreAsync<TResult, TElement, TReader>(
        bool useAsync,
        Selection? fileSelection = null,
        Selection? memorySelection = null,
        ulong[]? memoryDims = null,
        CancellationToken cancellationToken = default) where TElement : unmanaged
    {
        // fast path for null dataspace
        if (Space.Type == H5DataspaceType.Null)
            return default;

        /* file selection */
        if (fileSelection is null)
        {
            switch (Space.Type)
            {
                case H5DataspaceType.Scalar:
                case H5DataspaceType.Simple:

                    var starts = Space.Dimensions.ToArray();
                    starts.AsSpan().Clear();

                    var stridesAndBlocks = Space.Dimensions.ToArray();
                    stridesAndBlocks.AsSpan().Fill(1);

                    fileSelection = new HyperslabSelection(
                        rank: Space.Dimensions.Length,
                        starts: starts,
                        strides: stridesAndBlocks,
                        counts: Space.Dimensions,
                        blocks: stridesAndBlocks);

                    break;

                case H5DataspaceType.Null:
                default:
                    throw new Exception($"Unsupported data space type '{Space.Type}'.");
            }
        }

        var hyperSlabSelectString = default(string?);
        var pointSelectionJsonElement = default(JsonElement);

        if (fileSelection is HyperslabSelection hs)
        {
            var selections = Enumerable
                .Range(0, hs.Rank)
                .Select(dimension =>
                {
                    if (hs.Blocks[dimension] != 1)
                        throw new Exception($"The HSDS selection API requires a hyperslab block size of 1.");

                    var start = hs.Starts[dimension];
                    var end = hs.Starts[dimension] + hs.Strides[dimension] * hs.Counts[dimension];
                    var step = hs.Strides[dimension];

                    return $"{start}:{end}:{step}";
                });

            hyperSlabSelectString = $"[{string.Join(',', selections)}]";
        }

        else if (fileSelection is PointSelection ps)
        {
            var length = ps.PointsField.GetLength(0);
            var rank = ps.PointsField.GetLength(1);
            var jaggedArray = new ulong[length][];

            for (int i = 0; i < length; i++)
            {
                var points = new ulong[rank];

                for (int dim = 0; dim < rank; dim++)
                {
                    points[dim] = ps.PointsField[i, dim];
                }

                jaggedArray[i] = points;
            }

            pointSelectionJsonElement = JsonSerializer.SerializeToElement(new
            {
                points = jaggedArray
            });
        }

        else
        {
            throw new Exception($"The selection of type {fileSelection.GetType().Name} is not supported.");
        }

        /* memory dims */
        var sourceElementCount = fileSelection.TotalElementCount;

        if (memorySelection is not null && memoryDims is null)
            throw new Exception("If a memory selection is specified, the memory dimensions must be specified, too.");

        memoryDims ??= new ulong[] { sourceElementCount };

        /* memory selection */
        memorySelection ??= new HyperslabSelection(start: 0, block: sourceElementCount);

        /* target buffer */
        var targetElementCount = MathUtils.CalculateSize(memoryDims);
        var targetBuffer = new TElement[targetElementCount];

        // TODO make use of selections
        var streamResponse =

            hyperSlabSelectString is null

                ? useAsync
                    ? await Connector.Client.Dataset.PostValuesAsStreamAsync(_dataset.Id, Connector.DomainName, body: pointSelectionJsonElement, cancellationToken: cancellationToken)
                    : Connector.Client.Dataset.PostValuesAsStream(_dataset.Id, Connector.DomainName, body: pointSelectionJsonElement)

                : useAsync
                    ? await Connector.Client.Dataset.GetValuesAsStreamAsync(_dataset.Id, Connector.DomainName, select: hyperSlabSelectString, cancellationToken: cancellationToken)
                    : Connector.Client.Dataset.GetValuesAsStream(_dataset.Id, Connector.DomainName, select: hyperSlabSelectString);

        var stream = useAsync
            ? await streamResponse.Content.ReadAsStreamAsync(cancellationToken)
            : streamResponse.Content.ReadAsStream(cancellationToken);

        var byteMemory = targetBuffer.AsMemory().Cast<TElement, byte>();
        await ReadExactlyAsync(stream, buffer: byteMemory, useAsync: useAsync, cancellationToken);

        // /* ensure correct endianness */
        // if (Type.Class == H5DataTypeClass.FixedPoint) // TODO are there other types that are byte order aware?
        // {
        //     ByteOrder byteOrder;

        //     // TODO: is this reliable?
        //     if (_dataset.Type.Base!.EndsWith("LE"))
        //         byteOrder = ByteOrder.LittleEndian;

        //     else if (_dataset.Type.Base!.EndsWith("BE"))
        //         byteOrder = ByteOrder.BigEndian;

        //     else
        //         byteOrder = ByteOrder.VaxEndian;

        //     DataUtils.EnsureEndianness(
        //         source: MemoryMarshal.AsBytes(result.AsSpan()).ToArray() /* make copy of array */,
        //         destination: MemoryMarshal.AsBytes(result.AsSpan()),
        //         byteOrder,
        //         (uint)Unsafe.SizeOf<TResult>()); // TODO: this is not 100% correct, it should be this.Type.Size.
        // }

        return ReadUtils.FromArray<TResult, TElement>(targetBuffer);
    }

    private static async Task ReadExactlyAsync(Stream stream, Memory<byte> buffer, bool useAsync, CancellationToken cancellationToken)
    {
        var slicedBuffer = buffer;

        while (slicedBuffer.Length > 0)
        {
            var readBytes = useAsync

                ? await stream
                    .ReadAsync(slicedBuffer, cancellationToken)
                    .ConfigureAwait(false)

                : stream.Read(slicedBuffer.Span);

            slicedBuffer = slicedBuffer[readBytes..];
        };
    }
}