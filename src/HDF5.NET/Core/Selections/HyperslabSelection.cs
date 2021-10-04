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
            if (this.Counts[dimension] == 0)
            {
                return 0;
            }
            else
            {
                return
                    this.Starts[dimension] +
                    this.Counts[dimension] * this.Strides[dimension] -
                    (this.Strides[dimension] - this.Blocks[dimension]);
            }
        }
    }
}