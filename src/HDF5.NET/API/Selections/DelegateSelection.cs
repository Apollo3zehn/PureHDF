using System;
using System.Collections.Generic;

namespace HDF5.NET
{
    public partial class DelegateSelection : Selection
    {
        public DelegateSelection(ulong totalElementCount, Func<ulong[], IEnumerable<Step>> walker)
        {
            TotalElementCount = totalElementCount;
            _walker = walker;
        }

        public override ulong TotalElementCount { get; }

        public override IEnumerable<Step> Walk(ulong[] limits)
        {
            return _walker(limits);
        }
    }
}