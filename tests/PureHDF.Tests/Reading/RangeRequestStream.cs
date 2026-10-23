namespace PureHDF.Tests.Reading;

/// <summary>
/// An <see cref="IConcurrentStream" /> that serves reads from a file on disk while counting what an HTTP
/// range-request client would have had to fetch to answer them.
/// </summary>
/// <remarks>
/// A remote reader cannot issue one request per struct field, so it fetches fixed-size blocks and keeps
/// them. That block model is what makes these numbers transferable to a real HTTP or object-store client.
/// <para>
/// The counters are the point. <see cref="Requests" /> is round trips - contiguous missing blocks
/// coalesce into one, since a range request can span them - and <see cref="BytesFetched" /> is
/// transfer volume. They move independently: a walk can be cheap in bytes and ruinous in round trips,
/// or the reverse, and only the two together say whether a source is usable over a network.
/// </para>
/// <para>
/// Blocks are retained without limit, deliberately. This measures the LOWER BOUND on what a file's
/// layout forces a client to fetch; adding eviction would fold a cache-sizing policy into a number that
/// is supposed to be about the file. A client with a bounded cache can only do worse, since a block it
/// evicts and needs again costs a second request.
/// </para>
/// <para>
/// A bare <see cref="IConcurrentStream" /> with no <see cref="Stream" /> base, as in
/// <see cref="ConcurrentStream" />: there is no cursor to read through, so no read can slip past the
/// counters, and silently wrong numbers are worse than no numbers.
/// </para>
/// </remarks>
internal sealed class RangeRequestStream : IConcurrentStream
{
    private readonly FileStream _file;
    private readonly long _blockSize;
    private readonly HashSet<long> _resident = [];
    private readonly object _gate = new();

    private int _requests;
    private long _bytesFetched;
    private int _metadataReads;
    private int _datasetReads;

    public RangeRequestStream(string filePath, long blockSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(blockSize, 1);

        _file = File.OpenRead(filePath);
        _blockSize = blockSize;
    }

    /// <summary>Range requests a client would have issued, contiguous blocks counted once.</summary>
    public int Requests => Volatile.Read(ref _requests);

    /// <summary>Bytes those requests would have transferred.</summary>
    public long BytesFetched => Volatile.Read(ref _bytesFetched);

    /// <summary>Distinct blocks a client would be holding, i.e. its peak memory in blocks.</summary>
    public int BlocksResident
    {
        get
        {
            lock (_gate)
                return _resident.Count;
        }
    }

    public int MetadataReadCount => Volatile.Read(ref _metadataReads);

    public int DatasetReadCount => Volatile.Read(ref _datasetReads);

    public void ResetCounts()
    {
        Volatile.Write(ref _requests, 0);
        Volatile.Write(ref _bytesFetched, 0);
        Volatile.Write(ref _metadataReads, 0);
        Volatile.Write(ref _datasetReads, 0);
    }

    /// <summary>Drops the block cache too, for measuring a cold open rather than a warm one.</summary>
    public void ResetAll()
    {
        ResetCounts();

        lock (_gate)
            _resident.Clear();
    }

    public ValueTask ReadDatasetAsync(long offset, Memory<byte> buffer)
    {
        Interlocked.Increment(ref _datasetReads);

        return ReadCore(offset, buffer);
    }

    public ValueTask ReadMetadataAsync(long offset, Memory<byte> buffer)
    {
        Interlocked.Increment(ref _metadataReads);

        return ReadCore(offset, buffer);
    }

    public long Length => _file.Length;

    public void Dispose()
    {
        _file.Dispose();
    }

    private ValueTask ReadCore(long offset, Memory<byte> buffer)
    {
        if (buffer.Length == 0)
            return default;

        lock (_gate)
        {
            Account(offset, buffer.Length);

            // Read under the same lock as the accounting: one FileStream has one cursor, and the
            // driver issues these concurrently.
            _file.Seek(offset, SeekOrigin.Begin);
            _file.ReadExactly(buffer.Span);
        }

        return default;
    }

    /// <summary>
    /// Charges a read to the counters: every block it touches that is not already resident becomes
    /// part of a request, and runs of adjacent new blocks are charged as one.
    /// </summary>
    private void Account(long offset, int length)
    {
        var firstBlock = offset / _blockSize;
        var lastBlock = (offset + length - 1) / _blockSize;

        var runLength = 0L;

        for (var block = firstBlock; block <= lastBlock; block++)
        {
            if (_resident.Add(block))
            {
                runLength++;
                continue;
            }

            // A resident block breaks the run: a client already holding it would not re-request it,
            // so what follows is a separate request.
            CloseRun(block - runLength, runLength);
            runLength = 0;
        }

        CloseRun(lastBlock + 1 - runLength, runLength);
    }

    private void CloseRun(long firstBlock, long blockCount)
    {
        if (blockCount == 0)
            return;

        Interlocked.Increment(ref _requests);

        // Clipped to the file, so the final partial block is not counted as a whole one.
        var start = firstBlock * _blockSize;
        var end = Math.Min(_file.Length, start + (blockCount * _blockSize));

        Interlocked.Add(ref _bytesFetched, end - start);
    }
}
