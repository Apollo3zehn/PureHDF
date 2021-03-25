using Microsoft.AspNetCore.Components.Forms;

namespace HDF5.NET.BlazorBrowser.Core
{
    public record FileContainer
    {
        public IBrowserFile BrowserFile { get; init; }
        public H5File H5File { get; init; }
    }
}
