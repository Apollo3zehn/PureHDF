using System;
using System.Collections.Generic;

namespace HDF5.NET
{
    partial class DelegateSelection : Selection
    {
        private Func<IEnumerable<Slice>> _walker;
    }
}