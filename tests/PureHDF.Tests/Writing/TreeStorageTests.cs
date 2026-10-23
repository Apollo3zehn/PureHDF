using PureHDF.Filters;
using PureHDF.Selections;
using Xunit;

namespace PureHDF.Tests.Writing;

/// <summary>
///     The tree a caller builds keeps everything it was given, however compactly it stores it.
/// </summary>
/// <remarks>
///     A file of many small datasets is mostly tree: at 300,000 datasets in groups of five, stored as plain
///     fields and dictionaries, the datasets and the groups' link tables are three quarters of what writing
///     it holds. These pin down what the tree answers, so that how it stores it can change.
/// </remarks>
public class TreeStorageTests
{
    [Fact]
    public void ADatasetWithDataKeepsItAndItsType()
    {
        var data = new[] { 1, 2, 3 };
        var dataset = new H5Dataset(data);

        Assert.Same(data, dataset.Data);
        Assert.Equal(typeof(int[]), dataset.Type);
        Assert.Null(dataset.FileDims);
        Assert.Null(dataset.Chunks);
        Assert.Null(dataset.MemorySelection);
        Assert.Null(dataset.FileSelection);
        Assert.Equal(default, dataset.DatasetCreation);
        Assert.Null(dataset.OpaqueInfo);
        Assert.False(dataset.IsNullDataspace);
    }

    [Fact]
    public void AGenericDatasetKeepsItsDeclaredType()
    {
        var dataset = new H5Dataset<int?[]>([1, null]);

        Assert.Equal(typeof(int?[]), dataset.Type);
    }

    [Theory]
    [InlineData(new ulong[] { 7 })]
    [InlineData(new ulong[] { 3, 4 })]
    [InlineData(new ulong[] { 2, 3, 4 })]
    [InlineData(new ulong[0])]
    public void ADatasetKeepsItsDimensions(ulong[] fileDims)
    {
        var dataset = new H5Dataset<double[]>(fileDims: fileDims);

        Assert.Equal(fileDims, dataset.FileDims);
        Assert.Null(dataset.Data);
        Assert.Equal(typeof(double[]), dataset.Type);
    }

    [Fact]
    public void ADatasetKeepsWhatItIsAskedToDo()
    {
        var memorySelection = new HyperslabSelection(0, 2);
        var fileSelection = new HyperslabSelection(1, 2);
        var creation = new H5DatasetCreation(Filters: [DeflateFilter.Id]);
        var opaque = new H5OpaqueInfo(TypeSize: 2, Tag: "tag");

        var dataset = new H5Dataset(
            new byte[4],
            chunks: [2],
            memorySelection: memorySelection,
            fileSelection: fileSelection,
            fileDims: [4],
            datasetCreation: creation,
            opaqueInfo: opaque);

        Assert.Equal(new uint[] { 2 }, dataset.Chunks);
        Assert.Same(memorySelection, dataset.MemorySelection);
        Assert.Same(fileSelection, dataset.FileSelection);
        Assert.Equal(new ulong[] { 4 }, dataset.FileDims);
        Assert.Equal(creation, dataset.DatasetCreation);
        Assert.Same(opaque, dataset.OpaqueInfo);
    }

    [Fact]
    public void ANullDataspaceDatasetSaysSo()
    {
        var dataset = new H5Dataset<int>();

        Assert.True(dataset.IsNullDataspace);
        Assert.Equal(typeof(int), dataset.Type);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(100)]
    public void AGroupKeepsItsLinksInTheOrderTheyWereAdded(int count)
    {
        var group = new H5Group();
        var names = Enumerable.Range(0, count).Select(i => $"link-{count - i}").ToArray();

        foreach (var name in names)
            group[name] = name;

        Assert.Equal(count, group.Count);
        Assert.Equal(names, group.Keys);
        Assert.Equal(names, group.Values);
        Assert.Equal(names, group.Select(entry => entry.Key));

        foreach (var name in names)
        {
            Assert.True(group.ContainsKey(name));
            Assert.Equal(name, group[name]);
            Assert.True(group.TryGetValue(name, out var value));
            Assert.Equal(name, value);
        }

        Assert.False(group.ContainsKey("missing"));
        Assert.False(group.TryGetValue("missing", out _));
        Assert.Throws<KeyNotFoundException>(() => group["missing"]);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(20)]
    public void AGroupReplacesRemovesAndRejectsDuplicates(int count)
    {
        var group = new H5Group();

        for (var i = 0; i < count; i++)
            group.Add($"link-{i}", i);

        Assert.Throws<ArgumentException>(() => group.Add("link-0", -1));
        Assert.Throws<ArgumentNullException>(() => group.Add(null!, -1));

        group["link-1"] = "replaced";
        Assert.Equal("replaced", group["link-1"]);
        Assert.Equal(count, group.Count);

        Assert.True(group.Remove("link-0"));
        Assert.False(group.Remove("link-0"));
        Assert.Equal(count - 1, group.Count);
        Assert.False(group.ContainsKey("link-0"));

        // Through the group's own Contains, which compares the value too
        var containsCurrent = group.Contains(new KeyValuePair<string, object>("link-1", "replaced"));
        var containsStale = group.Contains(new KeyValuePair<string, object>("link-1", 1));

        Assert.True(containsCurrent);
        Assert.False(containsStale);
        Assert.False(group.Remove(new KeyValuePair<string, object>("link-1", 1)));
        Assert.True(group.Remove(new KeyValuePair<string, object>("link-1", "replaced")));

        var copy = new KeyValuePair<string, object>[group.Count + 1];
        group.CopyTo(copy, 1);
        Assert.Equal(group.ToArray(), copy[1..]);

        group.Clear();
        Assert.Empty(group);

        group["again"] = 1;
        Assert.Equal(1, group["again"]);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(20)]
    public void ChangingAGroupWhileEnumeratingItFails(int count)
    {
        var group = new H5Group();

        for (var i = 0; i < count; i++)
            group[$"link-{i}"] = i;

        Assert.Throws<InvalidOperationException>(() =>
        {
            foreach (var entry in group)
                group[$"new-{entry.Key}"] = 0;
        });
    }
}
