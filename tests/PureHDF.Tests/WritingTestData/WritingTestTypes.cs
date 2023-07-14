
namespace PureHDF.Tests;

internal struct WritingTestStruct
{
    public int x;
    public double y;
}

internal readonly record struct WritingTestRecordStruct(
    int X,
    int Y
);