using System;
using System.Collections.Generic;

namespace HDF5.NET
{
    public partial class DelegateSelection : Selection
    {
        public DelegateSelection(ulong elementCount, Func<ulong[], IEnumerable<Step>> walker)
        {
            this.ElementCount = elementCount;
            _walker = walker;
        }

        public override ulong ElementCount { get; }

        public override IEnumerable<Step> Walk(ulong[] limits)
        {
            return _walker(limits);
        }
    }
}