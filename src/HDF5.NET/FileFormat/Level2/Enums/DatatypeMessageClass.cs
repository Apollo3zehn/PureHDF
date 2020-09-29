namespace HDF5.NET
{
    public enum DatatypeMessageClass : byte
    {
        FixedPoint = 0,
        FloatingPoint = 1,
        Time = 2,
        String = 3,
        BitField = 4,
        Opaque = 5,
        Compound = 6,
        Reference = 7,
        Enumerated = 8,
        VariableLength = 9,
        Array = 10
    }
}
