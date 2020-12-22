using HDF5.NET.BlazorBrowser.Core;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using System;
using System.IO;
using System.Threading.Tasks;

namespace HDF5.NET.BlazorBrowser.Pages
{
    public partial class Index
    {
        #region Properties

        [Inject]
        public AppState AppState { get; set; }

        #endregion

        #region Methods

        private async Task OnInputFileChange(InputFileChangeEventArgs e)
        {
#warning workaround, remove this
            var stream = new MemoryStream();
            await e.File.OpenReadStream().CopyToAsync(stream);
            stream.Seek(0, SeekOrigin.Begin);

            var fileContainer = new FileContainer()
            {
                BrowserFile = e.File,
                H5File = H5File.Open(stream, string.Empty, deleteOnClose: false)
            };

            this.AppState.Files.Add(fileContainer);
        }

        #endregion
    }
}
