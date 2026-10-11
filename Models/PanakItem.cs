using System.Windows.Media;

namespace Panak.Models {
    public class PanakItem {
        public string FullPath { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }
        public ImageSource? IconSource { get; set; }
        public string SizeDisplay { get; set; } = string.Empty;
        public DateTime LastModified { get; set; }
    }
}
