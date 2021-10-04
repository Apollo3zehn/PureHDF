using System.Collections;
using System.Collections.Generic;

namespace HDF5.NET
{
    public abstract class Selection : IEnumerable<Slice>
    {
        public abstract ulong ElementCount { get; }

        public abstract IEnumerator<Slice> GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}