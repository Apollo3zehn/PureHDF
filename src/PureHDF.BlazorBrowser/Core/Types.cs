using Microsoft.AspNetCore.Components.Forms;

namespace PureHDF.BlazorBrowser.Core
{
    public record FileContainer(
        IBrowserFile BrowserFile,
        H5File H5File);
}
