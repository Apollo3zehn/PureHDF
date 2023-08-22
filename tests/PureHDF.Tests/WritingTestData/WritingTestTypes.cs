
namespace PureHDF.Tests;

internal struct WritingTestStruct
{
    public int x;

    [field: H5Name("y2")] 
    public double y;
}

internal readonly record struct WritingTestRecordStruct(
    int X,

    [property: H5Name("Y2")] 
    double Y
);

internal record class WritingTestRecordClass(
    int X,

    [property: H5Name("Y2")] 
    double Y
);

internal struct WritingTestStringStruct
{
    public int x;

    [field: H5StringLength(6)]
    public string y;
}

internal record class WritingTestStringRecordClass(
    int X,

    [property: H5StringLength(6)]
    string Y
);