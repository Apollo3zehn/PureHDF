namespace PureHDF.Tests;

public class SharedTestData
{
    static SharedTestData()
    {
        TinyData = new byte[] { 99 };
        SmallData = Enumerable.Range(0, 100).ToArray();
        MediumData = Enumerable.Range(0, 10_000).ToArray();
        HugeData = Enumerable.Range(0, 10_000_000).ToArray();
    }

    public static byte[] TinyData { get; }

    public static int[] SmallData { get; }

    public static int[] MediumData { get; }

    public static int[] HugeData { get; }
}