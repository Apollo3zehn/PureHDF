using System.Collections.Generic;

namespace HDF5.NET
{
    public abstract class Selection
    {
        public abstract ulong ElementCount { get; }

        public abstract IEnumerable<Step> Walk(ulong[] limits);
    }
}