using PureHDF.Filters;
using PureHDF.Selections;
using PureHDF.VOL.Native;
using Xunit;

namespace PureHDF.Tests.Writing;

/// <summary>
///     A file written piece by piece through <see cref="H5File.BeginStreaming(Stream, H5WriteOptions?)" />.
/// </summary>
public class StreamingWriteTests
{
    private static readonly H5DatasetCreation Deflate = new(Filters: [DeflateFilter.Id]);

    [Fact]
    public void AppendedSubtreesAreLinkedUnderTheirOpenGroups()
    {
        // Arrange
        using var stream = new MemoryStream();

        // Act
        using (var writer = H5File.BeginStreaming(stream))
        {
            for (var i = 0; i < 3; i++)
            {
                writer.Append($"documents/{i}", new H5Group
                {
                    ["values"] = new[] { i, i + 1, i + 2 },
                    ["nested"] = new H5Group { ["name"] = $"document {i}" }
                });
            }

            writer.Append("other/deeper/scalar", new H5Dataset(42.5));
        }

        // Assert
        var file = Open(stream);

        for (var i = 0; i < 3; i++)
        {
            var document = file.Group("documents").Group($"{i}");

            Assert.Equal([i, i + 1, i + 2], document.Dataset("values").Read<int[]>());
            Assert.Equal($"document {i}", document.Group("nested").Dataset("name").Read<string>());
        }

        Assert.Equal(42.5, file.Group("other").Group("deeper").Dataset("scalar").Read<double>());
    }

    [Fact]
    public void RootAttributesAreWritten()
    {
        // Arrange
        using var stream = new MemoryStream();

        // Act
        using (var writer = H5File.BeginStreaming(stream))
        {
            writer.Root.Attributes["id"] = 7;
            writer.Append("a", new H5Dataset(new[] { 1 }));
            writer.Root.Attributes["name"] = "set after an append";
        }

        // Assert
        var file = Open(stream);

        Assert.Equal(7, file.Attribute("id").Read<int>());
        Assert.Equal("set after an append", file.Attribute("name").Read<string>());
    }

    [Fact]
    public void OpenGroupAttributesAreWrittenWhenTheGroupCloses()
    {
        // Arrange
        using var stream = new MemoryStream();

        // Act
        using (var writer = H5File.BeginStreaming(stream))
        {
            var documents = writer.Group("documents");
            documents.Attributes["kind"] = "batch";

            writer.Append("documents/0", new H5Dataset(new[] { 1 }));
            documents.Attributes["count"] = 1;

            writer.Close("documents");
        }

        // Assert
        var group = Open(stream).Group("documents");

        Assert.Equal("batch", group.Attribute("kind").Read<string>());
        Assert.Equal(1, group.Attribute("count").Read<int>());
        Assert.Equal([1], group.Dataset("0").Read<int[]>());
    }

    [Fact]
    public void AGroupCreatedByAnAppendTakesAttributes()
    {
        // Arrange
        using var stream = new MemoryStream();

        // Act
        using (var writer = H5File.BeginStreaming(stream))
        {
            writer.Append("a/b/c", new H5Dataset(new[] { 1 }));

            writer.Group("a").Attributes["level"] = 1;
            writer.Group("a/b").Attributes["level"] = 2;
        }

        // Assert
        var a = Open(stream).Group("a");

        Assert.Equal(1, a.Attribute("level").Read<int>());
        Assert.Equal(2, a.Group("b").Attribute("level").Read<int>());
        Assert.Equal([1], a.Group("b").Dataset("c").Read<int[]>());
    }

    [Fact]
    public void GroupCreatesTheGroupsOnItsPath()
    {
        // Arrange
        using var stream = new MemoryStream();

        // Act
        using (var writer = H5File.BeginStreaming(stream))
        {
            writer.Group("a/b").Attributes["name"] = "b";
            writer.Append("a/b/c", new H5Dataset(new[] { 1 }));
        }

        // Assert
        var b = Open(stream).Group("a").Group("b");

        Assert.Equal("b", b.Attribute("name").Read<string>());
        Assert.Equal([1], b.Dataset("c").Read<int[]>());
    }

    [Fact]
    public void GroupReturnsTheSameObjectForTheSameOpenGroup()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var writer = H5File.BeginStreaming(stream);

        // Act
        var first = writer.Group("a");
        var second = writer.Group("a");

        // Assert
        Assert.Same(first, second);
    }

    [Fact]
    public void AnEmptyOpenGroupWithAttributesIsWritten()
    {
        // Arrange
        using var stream = new MemoryStream();

        // Act
        using (var writer = H5File.BeginStreaming(stream))
        {
            writer.Group("empty").Attributes["id"] = 3;
        }

        // Assert
        var group = Open(stream).Group("empty");

        Assert.Equal(3, group.Attribute("id").Read<int>());
        Assert.Empty(group.Children());
        AssertTheCLibraryAccepts(stream);
    }

    [Fact]
    public void GroupOnAPathThatIsNotAnOpenGroupThrows()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var writer = H5File.BeginStreaming(stream);

        writer.Append("dataset", new H5Dataset(new[] { 1 }));
        writer.Append("appended", new H5Group { ["values"] = new[] { 1 } });
        writer.Append("closed/a", new H5Dataset(new[] { 1 }));
        writer.Close("closed");

        // Act / Assert
        Assert.Throws<InvalidOperationException>(() => writer.Group("dataset"));
        Assert.Throws<InvalidOperationException>(() => writer.Group("appended"));
        Assert.Throws<InvalidOperationException>(() => writer.Group("appended/inner"));
        Assert.Throws<InvalidOperationException>(() => writer.Group("closed"));
        Assert.Throws<InvalidOperationException>(() => writer.Group("closed/b"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("/")]
    [InlineData("a//b")]
    public void GroupWithAnInvalidPathThrows(string path)
    {
        // Arrange
        using var stream = new MemoryStream();
        using var writer = H5File.BeginStreaming(stream);

        // Act
        void action() => writer.Group(path);

        // Assert
        Assert.Throws<ArgumentException>(action);
    }

    [Fact]
    public void GroupAfterDisposeThrows()
    {
        // Arrange
        using var stream = new MemoryStream();
        var writer = H5File.BeginStreaming(stream);
        writer.Dispose();

        // Act
        void action() => writer.Group("a");

        // Assert
        Assert.Throws<ObjectDisposedException>(action);
    }

    [Fact]
    public void AnOpenGroupAppendsAndCreatesGroupsBelowItself()
    {
        // Arrange
        using var stream = new MemoryStream();

        // Act
        using (var writer = H5File.BeginStreaming(stream))
        {
            var documents = writer.Group("documents");

            documents.Append("0", new H5Dataset(new[] { 1 }));
            documents.Append("deeper/1", new H5Dataset(new[] { 2 }));
            documents.Group("nested").Attributes["name"] = "nested";

            Assert.Equal("documents/nested", documents.Group("nested").Path);
        }

        // Assert
        var group = Open(stream).Group("documents");

        Assert.Equal([1], group.Dataset("0").Read<int[]>());
        Assert.Equal([2], group.Group("deeper").Dataset("1").Read<int[]>());
        Assert.Equal("nested", group.Group("nested").Attribute("name").Read<string>());
    }

    [Fact]
    public void DisposingAnOpenGroupClosesIt()
    {
        // Arrange
        using var stream = new MemoryStream();

        // Act
        using (var writer = H5File.BeginStreaming(stream))
        {
            using (var documents = writer.Group("documents"))
            {
                documents.Attributes["kind"] = "batch";
                documents.Append("0", new H5Dataset(new[] { 1 }));
            }

            Assert.Throws<InvalidOperationException>(() => writer.Append("documents/1", new H5Dataset(new[] { 2 })));
        }

        // Assert
        var group = Open(stream).Group("documents");

        Assert.Equal("batch", group.Attribute("kind").Read<string>());
        Assert.Equal([1], group.Dataset("0").Read<int[]>());
    }

    [Fact]
    public void AClosedGroupRejectsChangesAndKeepsItsAttributesReadable()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var writer = H5File.BeginStreaming(stream);

        var documents = writer.Group("documents");
        var attributes = documents.Attributes;
        attributes["kind"] = "batch";

        // Act
        documents.Close();

        // Assert
        Assert.Throws<InvalidOperationException>(() => documents.Attributes["count"] = 1);
        Assert.Throws<InvalidOperationException>(() => attributes.Add("count", 1));
        Assert.Throws<InvalidOperationException>(() => attributes.Remove("kind"));
        Assert.Throws<InvalidOperationException>(() => attributes.Clear());
        Assert.Throws<InvalidOperationException>(() => documents.Append("0", new H5Dataset(new[] { 1 })));
        Assert.Throws<InvalidOperationException>(() => documents.Group("nested"));

        Assert.Equal("batch", attributes["kind"]);
        Assert.Single(attributes);
    }

    [Fact]
    public void ClosingAnAncestorOrTheWriterClosesTheOpenGroupsBelowIt()
    {
        // Arrange
        using var stream = new MemoryStream();
        var writer = H5File.BeginStreaming(stream);

        var inner = writer.Group("a/b");
        var other = writer.Group("other");

        // Act
        writer.Close("a");
        writer.Dispose();

        // Assert
        Assert.Throws<InvalidOperationException>(() => inner.Attributes["late"] = 1);
        Assert.Throws<InvalidOperationException>(() => other.Attributes["late"] = 1);
    }

    [Fact]
    public void ClosingAnOpenGroupTwiceDoesNothing()
    {
        // Arrange
        using var stream = new MemoryStream();
        var writer = H5File.BeginStreaming(stream);
        var documents = writer.Group("documents");

        // Act
        documents.Close();
        documents.Close();
        writer.Dispose();
        documents.Dispose();

        // Assert
        Assert.Empty(Open(stream).Group("documents").Children());
    }

    [Fact]
    public void GroupPrefillsAttributesOfTheGroupItNames()
    {
        // Arrange
        using var stream = new MemoryStream();
        var attributes = new Dictionary<string, object> { ["kind"] = "batch", ["count"] = 2 };

        // Act
        using (var writer = H5File.BeginStreaming(stream))
        {
            var group = writer.Group("a/documents", attributes);
            group.Group("nested", new Dictionary<string, object> { ["level"] = 2 });

            attributes["kind"] = "changed after the call";
        }

        // Assert
        var a = Open(stream).Group("a");
        var documents = a.Group("documents");

        Assert.Empty(a.Attributes());
        Assert.Equal("batch", documents.Attribute("kind").Read<string>());
        Assert.Equal(2, documents.Attribute("count").Read<int>());
        Assert.Equal(2, documents.Group("nested").Attribute("level").Read<int>());
    }

    [Fact]
    public void GroupWithAttributesOnAnExistingOpenGroupAddsAndOverwritesThem()
    {
        // Arrange
        using var stream = new MemoryStream();

        // Act
        using (var writer = H5File.BeginStreaming(stream))
        {
            var documents = writer.Group("documents");
            documents.Attributes["kind"] = "batch";
            documents.Attributes["count"] = 1;

            var again = writer.Group("documents", new Dictionary<string, object> { ["count"] = 2, ["id"] = 7 });

            Assert.Same(documents, again);
        }

        // Assert
        var group = Open(stream).Group("documents");

        Assert.Equal("batch", group.Attribute("kind").Read<string>());
        Assert.Equal(2, group.Attribute("count").Read<int>());
        Assert.Equal(7, group.Attribute("id").Read<int>());
    }

    [Fact]
    public void GroupWithAttributesOnAClosedGroupThrows()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var writer = H5File.BeginStreaming(stream);

        writer.Group("documents").Close();

        // Act
        void action() => writer.Group("documents", new Dictionary<string, object> { ["id"] = 7 });

        // Assert
        Assert.Throws<InvalidOperationException>(action);
    }

    [Fact]
    public void AppendOpenWritesTheChildrenAndKeepsTheGroupOpen()
    {
        // Arrange
        using var stream = new MemoryStream();

        var group = new H5Group
        {
            ["a"] = new[] { 1 },
            ["nested"] = new H5Group { ["b"] = new[] { 2 } },
            Attributes = { ["kind"] = "batch" }
        };

        // Act
        using (var writer = H5File.BeginStreaming(stream))
        {
            var batch = writer.AppendOpen("outer/batch", group);

            Assert.Equal("outer/batch", batch.Path);
            Assert.Same(batch, writer.Group("outer/batch"));

            batch.Append("c", new H5Dataset(new[] { 3 }));
            batch.Attributes["count"] = 3;

            group.Attributes["ignored"] = 0;
            group["ignored"] = new[] { 0 };
        }

        // Assert
        var file = Open(stream).Group("outer").Group("batch");

        Assert.Equal([1], file.Dataset("a").Read<int[]>());
        Assert.Equal([2], file.Group("nested").Dataset("b").Read<int[]>());
        Assert.Equal([3], file.Dataset("c").Read<int[]>());
        Assert.Equal("batch", file.Attribute("kind").Read<string>());
        Assert.Equal(3, file.Attribute("count").Read<int>());
        Assert.False(file.AttributeExists("ignored"));
        Assert.False(file.LinkExists("ignored"));
        AssertTheCLibraryAccepts(stream);
    }

    [Fact]
    public void AppendOpenCountsHardLinksBetweenItsChildren()
    {
        // Arrange
        using var stream = new MemoryStream();
        var shared = new H5Dataset(new[] { 1, 2 });

        // Act
        using (var writer = H5File.BeginStreaming(stream))
        {
            writer.AppendOpen("group", new H5Group
            {
                ["x"] = shared,
                ["inner"] = new H5Group { ["y"] = shared }
            });
        }

        // Assert
        var group = Open(stream).Group("group");

        Assert.Equal(Address(group.Dataset("x")), Address(group.Group("inner").Dataset("y")));
        AssertTheCLibraryAccepts(stream);
    }

    [Fact]
    public void AppendOpenTakesDeferredDatasetsAndSoftLinks()
    {
        // Arrange
        using var stream = new MemoryStream();
        var deferred = new H5Dataset<int[]>(fileDims: [2]);

        // Act
        using (var writer = H5File.BeginStreaming(stream))
        {
            writer.AppendOpen("group", new H5Group
            {
                ["deferred"] = deferred,
                ["soft"] = new H5SoftLink("/group/deferred")
            });

            writer.Write(deferred, [4, 5]);
        }

        // Assert
        var group = Open(stream).Group("group");

        Assert.Equal([4, 5], group.Dataset("deferred").Read<int[]>());
        Assert.Equal([4, 5], group.Dataset("soft").Read<int[]>());
        AssertTheCLibraryAccepts(stream);
    }

    [Fact]
    public void AnOpenGroupAppendsOpenGroupsBelowItself()
    {
        // Arrange
        using var stream = new MemoryStream();

        // Act
        using (var writer = H5File.BeginStreaming(stream))
        {
            var outer = writer.Group("outer");
            var inner = outer.AppendOpen("inner", new H5Group { ["a"] = new[] { 1 } });

            Assert.Equal("outer/inner", inner.Path);
        }

        // Assert
        Assert.Equal([1], Open(stream).Group("outer").Group("inner").Dataset("a").Read<int[]>());
    }

    [Fact]
    public void AGroupAppendedOpenIsNotLinkedAgain()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var writer = H5File.BeginStreaming(stream);

        var group = new H5Group { ["a"] = new[] { 1 } };
        writer.AppendOpen("group", group);

        // Act / Assert
        Assert.Throws<InvalidOperationException>(() => writer.Append("again", group));
        Assert.Throws<InvalidOperationException>(() => writer.AppendOpen("again", group));
        Assert.Throws<InvalidOperationException>(() => writer.Append("holder", new H5Group { ["link"] = group }));
        Assert.Throws<InvalidOperationException>(() =>
            writer.Append("reference", new H5Dataset(new H5ObjectReference[] { group })));
    }

    [Fact]
    public void AppendOpenRejectsAGroupThatIsLinkedElsewhere()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var writer = H5File.BeginStreaming(stream);

        var appended = new H5Group();
        writer.Append("appended", appended);

        // Act / Assert
        Assert.Throws<InvalidOperationException>(() => writer.AppendOpen("a", new H5Group { HardLinkCount = 2 }));
        Assert.Throws<InvalidOperationException>(() => writer.AppendOpen("b", new H5Group { IsShared = true }));
        Assert.Throws<InvalidOperationException>(() => writer.AppendOpen("c", appended));
    }

    [Fact]
    public void AFailedAppendOpenLinksNothingAndTheWriterGoesOn()
    {
        // Arrange
        using var stream = new MemoryStream();

        using (var writer = H5File.BeginStreaming(stream))
        {
            var group = new H5Group { ["values"] = new[] { 1 } };
            group["self"] = new H5Dataset(new H5ObjectReference[] { group });

            writer.Append("dataset", new H5Dataset(new[] { 1 }));

            // Act / Assert
            Assert.Throws<InvalidOperationException>(() => writer.AppendOpen("group", group));
            Assert.Throws<InvalidOperationException>(() => writer.AppendOpen("dataset", new H5Group()));
            Assert.Throws<InvalidOperationException>(() => writer.AppendOpen("dataset/inner", new H5Group()));

            writer.AppendOpen("group", new H5Group { ["values"] = new[] { 2 } });
        }

        Assert.Equal([2], Open(stream).Group("group").Dataset("values").Read<int[]>());
    }

    [Fact]
    public void AnEmptyStreamingWriteIsAnEmptyFile()
    {
        // Arrange
        using var stream = new MemoryStream();

        // Act
        using (H5File.BeginStreaming(stream))
        {
        }

        // Assert
        Assert.Empty(Open(stream).Children());
    }

    public static TheoryData<string> DeferredLayouts() => ["contiguous", "implicit", "filtered", "single chunk", "filtered single chunk"];

    [Theory]
    [MemberData(nameof(DeferredLayouts))]
    public void DeferredDatasetsInsideAppendedGroupsAreWrittenAfterLaterAppends(string layout)
    {
        // Arrange
        using var stream = new MemoryStream();
        var data = Enumerable.Range(0, 1000).Select(value => (double)value).ToArray();
        var dataset = CreateDeferred(layout);

        // Act
        using (var writer = H5File.BeginStreaming(stream))
        {
            writer.Append("documents/0", new H5Group { ["data"] = dataset });

            for (var i = 1; i < 20; i++)
                writer.Append($"documents/{i}", new H5Group { ["data"] = new[] { i } });

            writer.Write(dataset, data);
        }

        // Assert
        var file = Open(stream);

        Assert.Equal(data, file.Group("documents").Group("0").Dataset("data").Read<double[]>());
        Assert.Equal([19], file.Group("documents").Group("19").Dataset("data").Read<int[]>());
        AssertTheCLibraryAccepts(stream);
    }

    [Theory]
    [MemberData(nameof(DeferredLayouts))]
    public void DeferredDatasetsAppendedDirectlyAreWrittenAfterLaterAppends(string layout)
    {
        // Arrange
        using var stream = new MemoryStream();
        var data = Enumerable.Range(0, 1000).Select(value => (double)value).ToArray();
        var dataset = CreateDeferred(layout);

        // Act
        using (var writer = H5File.BeginStreaming(stream))
        {
            writer.Append("documents/data", dataset);

            for (var i = 1; i < 20; i++)
                writer.Append($"documents/{i}", new H5Dataset(new[] { i }));

            writer.Write(dataset, data);
        }

        // Assert
        var file = Open(stream);

        Assert.Equal(data, file.Group("documents").Dataset("data").Read<double[]>());
        AssertTheCLibraryAccepts(stream);
    }

    [Theory]
    [MemberData(nameof(DeferredLayouts))]
    public void DeferredDatasetsAppendedDirectlyCanBeWrittenAfterTheirGroupIsClosed(string layout)
    {
        // Arrange
        using var stream = new MemoryStream();
        var data = Enumerable.Range(0, 1000).Select(value => (double)value).ToArray();
        var dataset = CreateDeferred(layout);

        // Act
        using (var writer = H5File.BeginStreaming(stream))
        {
            writer.Append("documents/data", dataset);
            writer.Close("documents");
            writer.Append("later", new H5Dataset(new[] { 1 }));
            writer.Write(dataset, data);
        }

        // Assert
        Assert.Equal(data, Open(stream).Group("documents").Dataset("data").Read<double[]>());
        AssertTheCLibraryAccepts(stream);
    }

    [Theory]
    [MemberData(nameof(DeferredLayouts))]
    public void DeferredDatasetsNeverWrittenReadAsZeros(string layout)
    {
        // Arrange
        using var stream = new MemoryStream();

        // Act
        using (var writer = H5File.BeginStreaming(stream))
        {
            writer.Append("direct", CreateDeferred(layout));
            writer.Append("group", new H5Group { ["nested"] = CreateDeferred(layout) });
        }

        // Assert
        var file = Open(stream);

        Assert.All(file.Dataset("direct").Read<double[]>(), value => Assert.Equal(0, value));
        Assert.All(file.Group("group").Dataset("nested").Read<double[]>(), value => Assert.Equal(0, value));
        AssertTheCLibraryAccepts(stream);
    }

    [Fact]
    public void GroupsWaitingForDeeplyNestedDatasetsAreWrittenOnceTheyAre()
    {
        // Arrange
        using var stream = new MemoryStream();
        var first = new H5Dataset<int[]>(fileDims: [2]);
        var second = new H5Dataset<int[]>(fileDims: [2]);

        // Act
        using (var writer = H5File.BeginStreaming(stream))
        {
            writer.Append("documents/0", new H5Group
            {
                Attributes = { ["name"] = "outer" },
                ["given"] = new[] { 1 },
                ["inner"] = new H5Group
                {
                    Attributes = { ["name"] = "inner" },
                    ["first"] = first,
                    ["deeper"] = new H5Group { ["second"] = second }
                }
            });

            writer.Append("between", new H5Dataset(new double[1000]));
            writer.Write(second, [3, 4]);
            writer.Append("later", new H5Dataset(new double[1000]));
            writer.Write(first, [1, 2]);
        }

        // Assert
        var document = Open(stream).Group("documents").Group("0");

        Assert.Equal("outer", document.Attribute("name").Read<string>());
        Assert.Equal("inner", document.Group("inner").Attribute("name").Read<string>());
        Assert.Equal([1], document.Dataset("given").Read<int[]>());
        Assert.Equal([1, 2], document.Group("inner").Dataset("first").Read<int[]>());
        Assert.Equal([3, 4], document.Group("inner").Group("deeper").Dataset("second").Read<int[]>());
        AssertTheCLibraryAccepts(stream);
    }

    [Fact]
    public void GroupsWaitingForDatasetsAreWrittenWhenTheirOpenGroupCloses()
    {
        // Arrange
        using var stream = new MemoryStream();
        var dataset = new H5Dataset<int[]>(fileDims: [2]);

        // Act
        using (var writer = H5File.BeginStreaming(stream))
        {
            writer.Append("documents/0", new H5Group { ["inner"] = new H5Group { ["data"] = dataset } });
            writer.Close("documents");
            writer.Write(dataset, [1, 2]);
        }

        // Assert
        Assert.Equal([1, 2], Open(stream).Group("documents").Group("0").Group("inner").Dataset("data").Read<int[]>());
        AssertTheCLibraryAccepts(stream);
    }

    [Fact]
    public void ObjectReferencesResolveToGroupsWaitingForDatasets()
    {
        // Arrange
        using var stream = new MemoryStream();
        var dataset = new H5Dataset<int[]>(fileDims: [2]);
        var group = new H5Group { ["data"] = dataset };

        // Act
        using (var writer = H5File.BeginStreaming(stream))
        {
            writer.Append("group", group);
            writer.Append("references", new H5Dataset(new H5ObjectReference[] { group }));
            writer.Write(dataset, [1, 2]);
        }

        // Assert
        var file = (NativeFile)Open(stream);
        var reference = file.Dataset("references").Read<NativeObjectReference1[]>()[0];

        Assert.Equal(Address(file.Group("group")), reference.Value);
        Assert.Equal([1, 2], ((IH5Group)file.Get(reference)).Dataset("data").Read<int[]>());
    }

    [Fact]
    public void GroupsWithADeclaredCountDoNotWait()
    {
        // Arrange
        using var stream = new MemoryStream();
        var dataset = new H5Dataset<int[]>(fileDims: [2]);
        var group = new H5Group { ["data"] = dataset, HardLinkCount = 2 };

        // Act
        using (var writer = H5File.BeginStreaming(stream))
        {
            writer.Append("a", group);
            writer.Append("b", group);
            writer.Write(dataset, [1, 2]);
        }

        // Assert
        var file = Open(stream);

        Assert.Equal(Address(file.Group("a")), Address(file.Group("b")));
        Assert.Equal([1, 2], file.Group("b").Dataset("data").Read<int[]>());
        AssertTheCLibraryAccepts(stream);
    }

    [Fact]
    public void ADatasetLinkedTwiceInOneSubtreeDoesNotWait()
    {
        // Arrange
        using var stream = new MemoryStream();
        var dataset = new H5Dataset<int[]>(fileDims: [2]);

        // Act
        using (var writer = H5File.BeginStreaming(stream))
        {
            writer.Append("group", new H5Group { ["a"] = dataset, ["b"] = dataset });
            writer.Write(dataset, [1, 2]);
        }

        // Assert
        var group = Open(stream).Group("group");

        Assert.Equal(Address(group.Dataset("a")), Address(group.Dataset("b")));
        Assert.Equal(2U, ((NativeObject)group.Dataset("a")).ReferenceCount);
        Assert.Equal([1, 2], group.Dataset("b").Read<int[]>());
    }

    [Fact]
    public void DeferredDatasetsCanBeWrittenInParts()
    {
        // Arrange
        using var stream = new MemoryStream();
        var data = Enumerable.Range(0, 1000).Select(value => (double)value).ToArray();
        var dataset = new H5Dataset<double[]>(fileDims: [1000]);

        // Act
        using (var writer = H5File.BeginStreaming(stream))
        {
            writer.Append("data", dataset);
            writer.Write(dataset, data[..500], fileSelection: new HyperslabSelection(0, 500));
            writer.Append("between", new H5Dataset(new[] { 1 }));
            writer.Write(dataset, data[500..], fileSelection: new HyperslabSelection(500, 500));
        }

        // Assert
        Assert.Equal(data, Open(stream).Dataset("data").Read<double[]>());
    }

    [Fact]
    public void AppendingBelowAClosedGroupThrows()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var writer = H5File.BeginStreaming(stream);

        writer.Append("documents/0", new H5Dataset(new[] { 1 }));
        writer.Close("documents");

        // Act
        void action() => writer.Append("documents/1", new H5Dataset(new[] { 2 }));

        // Assert
        Assert.Throws<InvalidOperationException>(action);
    }

    [Fact]
    public void ClosingAGroupClosesItsOpenDescendants()
    {
        // Arrange
        using var stream = new MemoryStream();

        using (var writer = H5File.BeginStreaming(stream))
        {
            writer.Append("a/b/c", new H5Dataset(new[] { 1 }));
            writer.Close("a");

            // Act
            void action() => writer.Append("a/b/d", new H5Dataset(new[] { 2 }));

            // Assert
            Assert.Throws<InvalidOperationException>(action);
        }

        Assert.Equal([1], Open(stream).Group("a").Group("b").Dataset("c").Read<int[]>());
    }

    [Fact]
    public void AppendingThroughADatasetThrows()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var writer = H5File.BeginStreaming(stream);

        writer.Append("a", new H5Dataset(new[] { 1 }));

        // Act
        void action() => writer.Append("a/b", new H5Dataset(new[] { 2 }));

        // Assert
        Assert.Throws<InvalidOperationException>(action);
    }

    [Fact]
    public void AppendingAnExistingLinkNameThrows()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var writer = H5File.BeginStreaming(stream);

        writer.Append("group/a", new H5Dataset(new[] { 1 }));

        // Act
        void action() => writer.Append("group/a", new H5Dataset(new[] { 2 }));

        // Assert
        Assert.Throws<InvalidOperationException>(action);
    }

    [Fact]
    public void AppendingOverAnOpenGroupThrows()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var writer = H5File.BeginStreaming(stream);

        writer.Append("group/a", new H5Dataset(new[] { 1 }));

        // Act
        void action() => writer.Append("group", new H5Group());

        // Assert
        Assert.Throws<InvalidOperationException>(action);
    }

    [Fact]
    public void AppendingAfterDisposeThrows()
    {
        // Arrange
        using var stream = new MemoryStream();
        var writer = H5File.BeginStreaming(stream);
        writer.Dispose();

        // Act
        void action() => writer.Append("a", new H5Dataset(new[] { 1 }));

        // Assert
        Assert.Throws<ObjectDisposedException>(action);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/")]
    [InlineData("a//b")]
    public void AnInvalidPathThrows(string path)
    {
        // Arrange
        using var stream = new MemoryStream();
        using var writer = H5File.BeginStreaming(stream);

        // Act
        void action() => writer.Append(path, new H5Dataset(new[] { 1 }));

        // Assert
        Assert.Throws<ArgumentException>(action);
    }

    [Fact]
    public void ClosingAPathThatIsNotAnOpenGroupThrows()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var writer = H5File.BeginStreaming(stream);

        writer.Append("a", new H5Dataset(new[] { 1 }));

        // Act / Assert
        Assert.Throws<InvalidOperationException>(() => writer.Close("a"));
        Assert.Throws<InvalidOperationException>(() => writer.Close("missing"));
    }

    [Theory]
    [InlineData(H5MetadataPlacement.Interleaved)]
    [InlineData(H5MetadataPlacement.Aggregated)]
    [InlineData(H5MetadataPlacement.FrontLoaded)]
    public void EveryPlacementIsStreamedAggregated(H5MetadataPlacement placement)
    {
        // Arrange
        using var stream = new MemoryStream();
        var options = new H5WriteOptions { MetadataPlacement = placement };

        // Act
        using (var writer = H5File.BeginStreaming(stream, options))
            writer.Append("group", new H5Group { ["values"] = new H5Dataset(new[] { 1, 2, 3 }) });

        // Assert
        Assert.Equal(new[] { 1, 2, 3 }, Open(stream).Group("group").Dataset("values").Read<int[]>());
    }

    [Theory]
    [InlineData(H5MetadataPlacement.Interleaved)]
    [InlineData(H5MetadataPlacement.Aggregated)]
    [InlineData(H5MetadataPlacement.FrontLoaded)]
    public void AMetadataReservationIsRejected(H5MetadataPlacement placement)
    {
        // Arrange
        using var stream = new MemoryStream();
        var options = new H5WriteOptions { MetadataPlacement = placement, MetadataReservation = 1024 * 1024 };

        // Act
        void action() => H5File.BeginStreaming(stream, options).Dispose();

        // Assert
        Assert.Throws<ArgumentException>(action);
    }

    [Fact]
    public void HardLinksWithinASubtreeWork()
    {
        // Arrange
        using var stream = new MemoryStream();
        var shared = new H5Dataset(new[] { 1, 2, 3 });

        // Act
        using (var writer = H5File.BeginStreaming(stream))
            writer.Append("group", new H5Group { ["a"] = shared, ["b"] = shared });

        // Assert
        var group = Open(stream).Group("group");

        Assert.Equal(Address(group.Dataset("a")), Address(group.Dataset("b")));
        Assert.Equal(2U, ((NativeObject)group.Dataset("a")).ReferenceCount);
    }

    [Fact]
    public void LinkingAWrittenObjectAgainWithoutADeclaredCountThrows()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var writer = H5File.BeginStreaming(stream);
        var shared = new H5Dataset(new[] { 1, 2, 3 });

        writer.Append("a", shared);

        // Act
        void action() => writer.Append("group", new H5Group { ["b"] = shared });

        // Assert
        Assert.Throws<InvalidOperationException>(action);
    }

    [Fact]
    public void ObjectsWithADeclaredCountCanBeLinkedAcrossSubtrees()
    {
        // Arrange
        using var stream = new MemoryStream();
        var shared = new H5Dataset(new[] { 1, 2, 3 }) { HardLinkCount = 3 };

        // Act
        using (var writer = H5File.BeginStreaming(stream))
        {
            writer.Append("a", shared);
            writer.Append("group/b", shared);
            writer.Append("other", new H5Group { ["c"] = shared });
        }

        // Assert
        var file = Open(stream);
        var address = Address(file.Dataset("a"));

        Assert.Equal(address, Address(file.Group("group").Dataset("b")));
        Assert.Equal(address, Address(file.Group("other").Dataset("c")));
        Assert.Equal(3U, ((NativeObject)file.Dataset("a")).ReferenceCount);
        AssertTheCLibraryAccepts(stream);
    }

    [Fact]
    public void DeferredDatasetsWithADeclaredCountCanBeLinkedAcrossSubtrees()
    {
        // Arrange
        using var stream = new MemoryStream();
        var shared = new H5Dataset<int[]>(fileDims: [3]) { HardLinkCount = 2 };

        // Act
        using (var writer = H5File.BeginStreaming(stream))
        {
            writer.Append("a", shared);
            writer.Append("group/b", shared);
            writer.Write(shared, [1, 2, 3]);
        }

        // Assert
        var file = Open(stream);

        Assert.Equal(Address(file.Dataset("a")), Address(file.Group("group").Dataset("b")));
        Assert.Equal([1, 2, 3], file.Group("group").Dataset("b").Read<int[]>());
    }

    [Fact]
    public void LinkingBeyondADeclaredCountThrows()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var writer = H5File.BeginStreaming(stream);
        var shared = new H5Dataset(new[] { 1 }) { HardLinkCount = 1 };

        writer.Append("a", shared);

        // Act
        void action() => writer.Append("b", shared);

        // Assert
        Assert.Throws<InvalidOperationException>(action);
    }

    [Fact]
    public void AFailedAppendLeavesTheWriterUsable()
    {
        // Arrange
        using var stream = new MemoryStream();

        using (var writer = H5File.BeginStreaming(stream))
        {
            writer.Append("before", new H5Dataset(new[] { 1 }));

            // Act
            void action() => writer.Append("documents/a", new H5Group
            {
                ["values"] = new[] { 2 },
                ["has/slash"] = new[] { 3 }
            });

            // Assert
            Assert.Throws<ArgumentException>(action);

            writer.Append("documents/a", new H5Group { ["values"] = new[] { 4 } });
            writer.Append("after", new H5Dataset(new[] { 5 }));
        }

        var file = Open(stream);

        Assert.Equal([4], file.Group("documents").Group("a").Dataset("values").Read<int[]>());
        Assert.Equal([5], file.Dataset("after").Read<int[]>());
        AssertTheCLibraryAccepts(stream);
    }

    [Fact]
    public void AFailedAppendCreatesNoGroups()
    {
        // Arrange
        using var stream = new MemoryStream();

        using (var writer = H5File.BeginStreaming(stream))
        {
            // Act
            void action() => writer.Append("new/deeper/a", new H5Group { ["has/slash"] = new[] { 1 } });

            // Assert
            Assert.Throws<ArgumentException>(action);
        }

        Assert.Empty(Open(stream).Children());
    }

    [Fact]
    public void AFailedAppendDoesNotCountItsLinks()
    {
        // Arrange
        using var stream = new MemoryStream();
        var declared = new H5Dataset(new[] { 1 }) { HardLinkCount = 1 };
        var shared = new H5Dataset(new[] { 2 }) { IsShared = true };

        using (var writer = H5File.BeginStreaming(stream))
        {
            // Act
            void action() => writer.Append("failed", new H5Group
            {
                ["declared"] = declared,
                ["shared"] = shared,
                ["has/slash"] = new[] { 3 }
            });

            // Assert
            Assert.Throws<ArgumentException>(action);

            writer.Append("declared", declared);
            writer.Append("shared", shared);
        }

        var file = Open(stream);

        Assert.Equal([1], file.Dataset("declared").Read<int[]>());
        Assert.Equal(1U, ((NativeObject)file.Dataset("shared")).ReferenceCount);
        AssertTheCLibraryAccepts(stream);
    }

    [Fact]
    public void WritingADatasetGivenWithDataThrows()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var writer = H5File.BeginStreaming(stream);
        var dataset = new H5Dataset<int[]>([1, 2]);

        writer.Append("a", dataset);

        // Act
        void action() => writer.Write(dataset, [3, 4]);

        // Assert
        var exception = Assert.Throws<InvalidOperationException>(action);
        Assert.Contains("given with data", exception.Message);
    }

    [Fact]
    public void AFileWithADeclaredCountNotReachedIsStillComplete()
    {
        // Arrange
        using var stream = new MemoryStream();
        var writer = H5File.BeginStreaming(stream);

        writer.Append("a", new H5Dataset(new[] { 1 }) { HardLinkCount = 2 });

        // Act
        Assert.Throws<InvalidOperationException>(writer.Dispose);

        // Assert
        Assert.Equal([1], Open(stream).Dataset("a").Read<int[]>());
    }

    [Fact]
    public void ADeclaredCountNotReachedThrowsAtDispose()
    {
        // Arrange
        using var stream = new MemoryStream();
        var writer = H5File.BeginStreaming(stream);
        var shared = new H5Dataset(new[] { 1 }) { HardLinkCount = 2 };

        writer.Append("a", shared);

        // Act
        void action() => writer.Dispose();

        // Assert
        Assert.Throws<InvalidOperationException>(action);
    }

    [Fact]
    public void BeginWriteChecksADeclaredCount()
    {
        // Arrange
        var shared = new H5Dataset(new[] { 1 }) { HardLinkCount = 3 };
        var file = new H5File { ["a"] = shared, ["b"] = shared };

        // Act
        void action() => file.Write(new MemoryStream());

        // Assert
        Assert.Throws<InvalidOperationException>(action);
    }

    [Fact]
    public void BeginWriteUsesAMatchingDeclaredCount()
    {
        // Arrange
        using var stream = new MemoryStream();
        var shared = new H5Dataset(new[] { 1 }) { HardLinkCount = 2 };

        // Act
        new H5File { ["a"] = shared, ["b"] = shared }.Write(stream);

        // Assert
        Assert.Equal(2U, ((NativeObject)Open(stream).Dataset("a")).ReferenceCount);
    }

    public static TheoryData<string> SharedLayouts() => ["given", "contiguous", "implicit", "filtered", "single chunk"];

    [Theory]
    [MemberData(nameof(SharedLayouts))]
    public void SharedDatasetsCanBeLinkedAcrossSubtreesWithoutACount(string layout)
    {
        // Arrange
        using var stream = new MemoryStream();
        var data = Enumerable.Range(0, 1000).Select(value => (double)value).ToArray();

        var shared = layout == "given"
            ? new H5Dataset<double[]>(data)
            : CreateDeferred(layout);

        shared.IsShared = true;

        // Act
        using (var writer = H5File.BeginStreaming(stream))
        {
            writer.Append("a", shared);
            writer.Append("documents/0", new H5Group { ["shared"] = shared });
            writer.Append("documents/1", new H5Group { ["shared"] = shared });

            if (layout != "given")
                writer.Write(shared, data);
        }

        // Assert
        var file = Open(stream);
        var address = Address(file.Dataset("a"));

        Assert.Equal(address, Address(file.Group("documents").Group("1").Dataset("shared")));
        Assert.Equal(3U, ((NativeObject)file.Dataset("a")).ReferenceCount);
        Assert.Equal(data, file.Group("documents").Group("0").Dataset("shared").Read<double[]>());
        AssertTheCLibraryAccepts(stream);
    }

    [Fact]
    public void SharedGroupsCanBeLinkedAcrossSubtreesWithoutACount()
    {
        // Arrange
        using var stream = new MemoryStream();
        var shared = new H5Group { ["values"] = new[] { 1, 2, 3 }, IsShared = true };

        // Act
        using (var writer = H5File.BeginStreaming(stream))
        {
            writer.Append("a", shared);
            writer.Append("b", shared);
        }

        // Assert
        var file = Open(stream);

        Assert.Equal(Address(file.Group("a")), Address(file.Group("b")));
        Assert.Equal(2U, ((NativeObject)file.Group("b")).ReferenceCount);
        Assert.Equal([1, 2, 3], file.Group("b").Dataset("values").Read<int[]>());
        AssertTheCLibraryAccepts(stream);
    }

    [Fact]
    public void ASharedObjectLinkedOnceIsValid()
    {
        // Arrange
        using var stream = new MemoryStream();

        // Act
        using (var writer = H5File.BeginStreaming(stream))
            writer.Append("a", new H5Dataset(new[] { 1 }) { IsShared = true });

        // Assert
        Assert.Equal(1U, ((NativeObject)Open(stream).Dataset("a")).ReferenceCount);
        AssertTheCLibraryAccepts(stream);
    }

    [Fact]
    public void SharedObjectsArePlacedAtTheFrontOfTheFile()
    {
        // Arrange
        using var stream = new MemoryStream();
        var shared = new H5Dataset(new double[100_000]) { IsShared = true };

        // Act
        using (var writer = H5File.BeginStreaming(stream))
        {
            writer.Append("before", new H5Dataset(new double[100_000]));
            writer.Append("a", shared);
            writer.Append("b", shared);
        }

        // Assert - within the region reserved after the superblock
        Assert.True(Address(Open(stream).Dataset("a")) < 64 * 1024 + 48);
    }

    [Fact]
    public void SharedObjectsOverflowingTheRegionOpenAnother()
    {
        // Arrange
        using var stream = new MemoryStream();
        var options = new H5WriteOptions { SharedObjectRegionSize = 1024 };
        var shared = Enumerable.Range(0, 50).Select(i => new H5Dataset(new[] { i }) { IsShared = true }).ToArray();

        // Act
        using (var writer = H5File.BeginStreaming(stream, options))
        {
            for (var i = 0; i < shared.Length; i++)
            {
                writer.Append($"first/{i}", shared[i]);
                writer.Append($"given/{i}", new H5Dataset(new double[1000]));
            }

            for (var i = 0; i < shared.Length; i++)
                writer.Append($"second/{i}", shared[i]);
        }

        // Assert
        var file = Open(stream);

        for (var i = 0; i < shared.Length; i++)
        {
            Assert.Equal([i], file.Group("second").Dataset($"{i}").Read<int[]>());
            Assert.Equal(2U, ((NativeObject)file.Group("first").Dataset($"{i}")).ReferenceCount);
        }

        AssertTheCLibraryAccepts(stream);
    }

    // Every region opened at the end of the file holds a part of an upload until the end, so there have to
    // be few of them however many objects are shared.
    [Fact]
    public void RegionsOpenedForSharedObjectsGrow()
    {
        // Arrange
        using var stream = new MemoryStream();
        var options = new H5WriteOptions { SharedObjectRegionSize = 1024 };

        using var writer = H5File.BeginStreaming(stream, options);

        // Act - about 200 bytes each, 200 kB in all
        for (var i = 0; i < 1_000; i++)
            writer.Append($"shared/{i}", new H5Dataset(new[] { i }) { IsShared = true });

        // Assert
        var regions = writer.NativeWriter.Context.FreeSpaceManager.SharedRegions;
        Assert.True(regions.Count <= 9, $"{regions.Count} regions.");
    }

    [Fact]
    public void ADeclaredCountWinsOverShared()
    {
        // Arrange
        using var stream = new MemoryStream();
        var shared = new H5Dataset(new[] { 1 }) { IsShared = true, HardLinkCount = 2 };

        // Act
        using (var writer = H5File.BeginStreaming(stream))
        {
            writer.Append("a", shared);
            writer.Append("b", shared);
        }

        // Assert - written at once, after the region, rather than held in it
        var file = Open(stream);

        Assert.Equal(2U, ((NativeObject)file.Dataset("b")).ReferenceCount);
        Assert.True(Address(file.Dataset("a")) >= 64 * 1024 + 48);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ANonPositiveSharedObjectRegionSizeIsRejected(long size)
    {
        // Arrange
        using var stream = new MemoryStream();
        var options = new H5WriteOptions { SharedObjectRegionSize = size };

        // Act
        void action() => H5File.BeginStreaming(stream, options).Dispose();

        // Assert
        Assert.Throws<ArgumentException>(action);
    }

    [Fact]
    public void ObjectReferencesResolveToObjectsAppendedEarlier()
    {
        // Arrange
        using var stream = new MemoryStream();
        var target = new H5Dataset(new[] { 1, 2, 3 });

        // Act
        using (var writer = H5File.BeginStreaming(stream))
        {
            writer.Append("target", target);
            writer.Append("references", new H5Dataset(new H5ObjectReference[] { target }));
        }

        // Assert
        var file = (NativeFile)Open(stream);
        var reference = file.Dataset("references").Read<NativeObjectReference1[]>()[0];

        Assert.Equal(Address(file.Dataset("target")), reference.Value);
        Assert.Equal([1, 2, 3], ((IH5Dataset)file.Get(reference)).Read<int[]>());
    }

    [Fact]
    public void WritingADatasetNotAppendedThrows()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var writer = H5File.BeginStreaming(stream);

        // Act
        void action() => writer.Write(new H5Dataset<int[]>(fileDims: [1]), [1]);

        // Assert
        Assert.ThrowsAny<Exception>(action);
    }

    [Fact]
    public void WrittenSubtreesAreNotKeptByTheWriter()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var writer = H5File.BeginStreaming(stream);

        // Act
        for (var i = 0; i < 100; i++)
        {
            writer.Append($"documents/{i}", new H5Group
            {
                ["values"] = new[] { i },
                ["nested"] = new H5Group { ["name"] = $"document {i}" }
            });
        }

        // Assert
        Assert.Empty(writer.NativeWriter.Context.Objects);
        Assert.Empty(writer.NativeWriter.Context.ObjectReferenceCountMap);
        Assert.Empty(writer.NativeWriter.Context.RawValueToDatasetMap);
    }

    [Fact]
    public void OnlyDeferredDatasetsAreKeptByTheWriter()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var writer = H5File.BeginStreaming(stream);
        var deferred = new H5Dataset<int[]>(fileDims: [1]);

        // Act
        writer.Append("documents/0", new H5Group { ["deferred"] = deferred, ["given"] = new[] { 1 } });

        // Assert
        Assert.Equal([deferred], writer.NativeWriter.Context.Objects.Keys);
    }

    [Fact]
    public void ManyGroupsAndDatasetsRoundTripThroughTheCLibrary()
    {
        // Arrange
        using var stream = new MemoryStream();
        var deferred = new List<(H5Dataset<float[]> Dataset, float[] Data)>();
        var written = new HashSet<int>();

        // Act
        using (var writer = H5File.BeginStreaming(stream, new H5WriteOptions { MetadataBlockSize = 64 * 1024 }))
        {
            writer.Root.Attributes["title"] = "streamed";

            for (var i = 0; i < 500; i++)
            {
                var dataset = new H5Dataset<float[]>(fileDims: [64]);
                var data = Enumerable.Range(i, 64).Select(value => (float)value).ToArray();

                deferred.Add((dataset, data));

                writer.Append($"batch_{i / 100}/document_{i}", new H5Group
                {
                    ["given"] = new H5Dataset(new[] { i }) { Attributes = { ["index"] = i } },
                    ["strings"] = new[] { $"a{i}", $"b{i}" },
                    ["deferred"] = dataset
                });

                writer.Append($"batch_{i / 100}/direct_{i}", new H5Dataset<int[]>(fileDims: [4]));

                // Some written soon, out of order, the rest at the end.
                if (i % 7 == 0 && written.Add(i / 2))
                    writer.Write(deferred[i / 2].Dataset, deferred[i / 2].Data);

                if (i % 100 == 99)
                    writer.Close($"batch_{i / 100}");
            }

            for (var i = 0; i < deferred.Count; i++)
            {
                if (!written.Contains(i))
                    writer.Write(deferred[i].Dataset, deferred[i].Data);
            }
        }

        // Assert
        var file = Open(stream);

        Assert.Equal("streamed", file.Attribute("title").Read<string>());

        for (var i = 0; i < 500; i += 37)
        {
            var document = file.Group($"batch_{i / 100}").Group($"document_{i}");

            Assert.Equal([i], document.Dataset("given").Read<int[]>());
            Assert.Equal(i, document.Dataset("given").Attribute("index").Read<int>());
            Assert.Equal([$"a{i}", $"b{i}"], document.Dataset("strings").Read<string[]>());
            Assert.Equal(deferred[i].Data, document.Dataset("deferred").Read<float[]>());
        }

        AssertTheCLibraryAccepts(stream);
    }

    private static H5Dataset<double[]> CreateDeferred(string layout) => layout switch
    {
        "contiguous" => new H5Dataset<double[]>(fileDims: [1000]),
        "implicit" => new H5Dataset<double[]>(fileDims: [1000], chunks: [100]),
        "filtered" => new H5Dataset<double[]>(fileDims: [1000], chunks: [100], datasetCreation: Deflate),
        "single chunk" => new H5Dataset<double[]>(fileDims: [1000], chunks: [1000]),
        "filtered single chunk" => new H5Dataset<double[]>(fileDims: [1000], chunks: [1000], datasetCreation: Deflate),
        _ => throw new ArgumentOutOfRangeException(nameof(layout))
    };

    private static ulong Address(IH5Object h5Object)
        => ((NativeObject)h5Object).Reference.Value;

    private static IH5Group Open(MemoryStream stream)
        => H5File.Open(new MemoryStream(stream.ToArray()));

    private static void AssertTheCLibraryAccepts(MemoryStream stream)
    {
        var filePath = Path.GetTempFileName();

        try
        {
            File.WriteAllBytes(filePath, stream.ToArray());

            var result = TestUtils.RunH5Dump(filePath);

            if (result is not null)
                Assert.False(result.Failed, result.Diagnostics);
        }

        finally
        {
            File.Delete(filePath);
        }
    }
}
