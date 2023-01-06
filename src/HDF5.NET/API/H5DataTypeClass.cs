namespace HDF5.NET
{
    /// <summary>
    /// Specifies the data type class.
    /// </summary>
    public enum H5DataTypeClass : byte
    {
        /// <summary>
        /// A fixed-point number.
        /// </summary>
        FixedPoint = 0,

        /// <summary>
        /// A floating-point number.
        /// </summary>
        FloatingPoint = 1,

        /// <summary>
        /// A time structure. Not supported.
        /// </summary>
        Time = 2,

        /// <summary>
        /// A string.
        /// </summary>
        String = 3,

        /// <summary>
        /// A bitfield.
        /// </summary>
        BitField = 4,

        /// <summary>
        /// An opaque blob of bytes.
        /// </summary>
        Opaque = 5,

        /// <summary>
        /// A compound data type.
        /// </summary>
        Compound = 6,

        /// <summary>
        /// A reference.
        /// </summary>
        Reference = 7,

        /// <summary>
        /// An enumeration.
        /// </summary>
        Enumerated = 8,

        /// <summary>
        /// A variable-length data type (string or sequence).
        /// </summary>
        VariableLength = 9,

        /// <summary>
        /// An array data type.
        /// </summary>
        Array = 10
    }
}
