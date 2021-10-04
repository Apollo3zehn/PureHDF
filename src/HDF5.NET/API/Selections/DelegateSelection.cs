using System;
using System.Collections.Generic;

namespace HDF5.NET
{
    public partial class DelegateSelection : Selection
    {
        public DelegateSelection(ulong elementCount, Func<IEnumerable<Slice>> walker)
        {
            this.ElementCount = elementCount;
            _walker = walker;
        }

        public override ulong ElementCount { get; }

        public override IEnumerator<Slice> GetEnumerator()
        {
            return _walker().GetEnumerator();
        }
    }
}