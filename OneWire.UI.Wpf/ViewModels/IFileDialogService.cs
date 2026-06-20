using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OneWire.UI.Wpf.ViewModels
{

    public interface IFileDialogService
    {
        string? OpenFile(string filter);
        string? SaveFile(string filter, string? defaultFileName = null);
    }

    public class FileDialogService : IFileDialogService
    {
        public string? OpenFile(string filter)
        {
            var dlg = new OpenFileDialog { Filter = filter };
            return dlg.ShowDialog() == true ? dlg.FileName : null;
        }

        public string? SaveFile(string filter, string? defaultFileName = null)
        {
            var dlg = new SaveFileDialog { Filter = filter };
            if (!string.IsNullOrWhiteSpace(defaultFileName))
                dlg.FileName = defaultFileName;
            return dlg.ShowDialog() == true ? dlg.FileName : null;
        }
    }
}
