using Microsoft.AspNetCore.Components.Forms;

namespace HDF5.NET.BlazorBrowser.Core
{
    public record FileContainer(
        IBrowserFile BrowserFile,
        H5File H5File);
}
