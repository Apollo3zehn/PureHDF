namespace HDF5.NET
{
    partial class HyperslabSelection : Selection
    {
        internal ulong[] StartsField;
        internal ulong[] StridesField;
        internal ulong[] CountsField;
        internal ulong[] BlocksField;

        private ulong GetStop(int dimension)
        {
            // prevent underflow of ulong
            if (Counts[dimension] == 0)
            {
                return 0;
            }
            else
            {
                return
                    Starts[dimension] +
                    Counts[dimension] * Strides[dimension] -
                    (Strides[dimension] - Blocks[dimension]);
            }
        }
    }
}