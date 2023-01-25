namespace PureHDF
{
    internal enum MantissaNormalization : byte
    {
        NoNormalization = 0,
        MsbIsAlwaysSet = 1,
        MsbIsNotStored = 2
    }
}
