using System.Buffers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using Hsds.Api;
using PureHDF.Selections;
using PureHDF.VOL.Native;

namespace PureHDF.VOL.Hsds;

internal class HsdsDataset : HsdsObject, IH5Dataset
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
        var (elementType, _) = WriteUtils.GetElementType(typeof(T));

        if (DataUtils.IsReferenceOrContainsReferences(elementType))
            throw new Exception("Only types which meet the struct constraint are supported.");

        // TODO cache this
        var method = _methodInfoReadCoreAsync.MakeGenericMethod(typeof(T), elementType);

        var result = ((Task<T>)method.Invoke(this,
        [
            false /* useAsync */,
            default /* buffer */,
            fileSelection,
            memorySelection,
            memoryDims,
            default(CancellationToken)
        ])!).GetAwaiter().GetResult();

        return result;
    }

    public void Read<T>(
        T buffer,
        Selection? fileSelection = null,
        Selection? memorySelection = null,
        ulong[]? memoryDims = null)
    {
        var (elementType, _) = WriteUtils.GetElementType(typeof(T));

        if (DataUtils.IsReferenceOrContainsReferences(elementType))
            throw new Exception("Only types which meet the struct constraint are supported.");

        // TODO cache this
        var method = _methodInfoReadCoreAsync.MakeGenericMethod(typeof(T), elementType);

        method.Invoke(this,
        [
            false /* useAsync */,
            buffer,
            fileSelection,
            memorySelection,
            memoryDims,
            default(CancellationToken)
        ]);
    }

    public async Task<T> ReadAsync<T>(
        Selection? fileSelection = null,
        Selection? memorySelection = null,
        ulong[]? memoryDims = null,
        CancellationToken cancellationToken = default)
    {
        var (elementType, _) = WriteUtils.GetElementType(typeof(T));

        if (DataUtils.IsReferenceOrContainsReferences(elementType))
            throw new Exception("Only types which meet the struct constraint are supported.");

        // TODO cache this
        var method = _methodInfoReadCoreAsync.MakeGenericMethod(typeof(T), elementType);

        var result = await (Task<T>)method.Invoke(this,
        [
            true /* useAsync */,
            default /* buffer */,
            fileSelection,
            memorySelection,
            memoryDims,
            cancellationToken
        ])!;

        return result;
    }

    public Task ReadAsync<T>(
        T buffer,
        Selection? fileSelection = null,
        Selection? memorySelection = null,
        ulong[]? memoryDims = null,
        CancellationToken cancellationToken = default)
    {
        var (elementType, _) = WriteUtils.GetElementType(typeof(T));

        if (DataUtils.IsReferenceOrContainsReferences(elementType))
            throw new Exception("Only types which meet the struct constraint are supported.");

        // TODO cache this
        var method = _methodInfoReadCoreAsync.MakeGenericMethod(typeof(T), elementType);

        return (Task)method.Invoke(this,
        [
            true /* useAsync */,
            buffer,
            fileSelection,
            memorySelection,
            memoryDims,
            cancellationToken
        ])!;
    }

    private async Task<TResult?> ReadCoreAsync<TResult, TElement>(
        bool useAsync,
        TResult? buffer,
        Selection? fileSelection = null,
        Selection? memorySelection = null,
        ulong[]? memoryDims = null,
        CancellationToken cancellationToken = default)
            where TElement : struct
    {
        var resultType = typeof(TResult);
        var isRawMode = typeof(TResult) == typeof(byte[]) || typeof(TResult) == typeof(Memory<byte>);

        /* fast path for null dataspace */
        if (Space.Type == H5DataspaceType.Null)
            throw new Exception("Datasets with null dataspace cannot be read.");

        /* memory selection + dims validation */
        if (memorySelection is not null && memoryDims is null)
            throw new Exception("If a memory selection is specified, the memory dimensions must be specified, too.");

        /* file dimensions */
        var fileDims = Space.GetDims();

        /* file selection */
        if (fileSelection is null || fileSelection is AllSelection)
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

        /* result buffer / result array */
        Memory<TElement> resultBuffer;
        var resultArray = default(Array);

        if (buffer is null || buffer.Equals(default(TResult)))
        {
            /* memory dims */
            if (isRawMode)
            {
                memoryDims ??= [fileSelection.TotalElementCount * (ulong)Type.Size];
            }

            else if (DataUtils.IsArray(resultType))
            {
                var rank = resultType.GetArrayRank();

                if (rank == 1)
                    memoryDims ??= [fileSelection.TotalElementCount];

                else if (rank == fileDims.Length)
                    memoryDims ??= fileDims;

                else
                    throw new Exception("The rank of the memory space must match the rank of the file space if no memory dimensions are provided.");
            }

            else
            {
                memoryDims ??= [1];
            }

            /* result buffer */
            resultArray = DataUtils.IsArray(resultType) || DataUtils.IsMemory(resultType)
                ? Array.CreateInstance(typeof(TElement), memoryDims.Select(dim => (int)dim).ToArray())
                : new TResult[1];

            resultBuffer = new ArrayMemoryManager<TElement>(resultArray).Memory;
        }

        else
        {
            /* result buffer */
            (resultBuffer, memoryDims) = ReadUtils.ToMemory<TResult, TElement>(buffer);
        }

        /* memory selection */
        if (memorySelection is null || memorySelection is AllSelection)
        {
            // TODO make use of the memory selection
            memorySelection = new HyperslabSelection(
                rank: memoryDims.Length,
                starts: new ulong[memoryDims.Length],
                blocks: memoryDims);
        }

        /* validation */
        if (memorySelection.TotalElementCount != fileSelection.TotalElementCount)
            throw new Exception("The total file selection element count does not match the total memory selection element count.");

        /* decode */
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

        var byteMemory = resultBuffer.Cast<TElement, byte>();

        await ReadExactlyAsync(
            stream,
            buffer: byteMemory,
            useAsync: useAsync,
            cancellationToken);

        /* ensure correct endianness */
        if (Type.Class == H5DataTypeClass.FixedPoint) // TODO are there other types that are byte order aware?
        {
            ByteOrder byteOrder;

            // TODO: is this reliable?
            if (_dataset.Type.Base!.EndsWith("LE"))
                byteOrder = ByteOrder.LittleEndian;

            else if (_dataset.Type.Base!.EndsWith("BE"))
                byteOrder = ByteOrder.BigEndian;

            else
                byteOrder = ByteOrder.VaxEndian;

            DataUtils.EnsureEndianness(
                source: MemoryMarshal.AsBytes(byteMemory.Span).ToArray() /* make copy of array */,
                destination: MemoryMarshal.AsBytes(byteMemory.Span),
                byteOrder,
                (uint)Unsafe.SizeOf<TElement>()); // TODO: this is not 100% correct, it should be this.Type.Size.
        }

        /* return */
        if (DataUtils.IsMemory(resultType))
        {
            return (TResult)(object)resultBuffer;
        }

        else
        {
            return resultArray is null
                ? default
                : ReadUtils.FromArray<TResult, TElement>(resultArray);
        }
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