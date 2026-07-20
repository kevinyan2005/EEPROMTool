using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OneWire.UI.Wpf.ViewModels
{

    public interface IFileDialogService
    {
        string? OpenFile(string filter, string? initialDirectory = null);
        string? SaveFile(string filter, string? defaultFileName = null, string? initialDirectory = null);
    }

    public class FileDialogService : IFileDialogService
    {
        public string? OpenFile(string filter, string? initialDirectory = null)
        {
            var dlg = new OpenFileDialog { Filter = filter };
            if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
                dlg.InitialDirectory = initialDirectory;
            return dlg.ShowDialog() == true ? dlg.FileName : null;
        }

        public string? SaveFile(string filter, string? defaultFileName = null, string? initialDirectory = null)
        {
            var dlg = new SaveFileDialog { Filter = filter };
            if (!string.IsNullOrWhiteSpace(defaultFileName))
                dlg.FileName = defaultFileName;
            if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
                dlg.InitialDirectory = initialDirectory;
            return dlg.ShowDialog() == true ? dlg.FileName : null;
        }
    }
}
