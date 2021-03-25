using System.Collections.Generic;

namespace HDF5.NET.BlazorBrowser.Core
{
    public class AppState
    {
        public AppState()
        {
            this.Files = new List<FileContainer>();
        }

        #region Properties

        public List<FileContainer> Files { get; }

        #endregion
    }
}
