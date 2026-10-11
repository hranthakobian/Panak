namespace Panak.Models {
    public class AppSettings {
        public string Language { get; set; } = "hy";
        public int GridColumns { get; set; } = 5;
        public bool FocusModeEnabled { get; set; } = true;
        public double FocusDimmingOpacity { get; set; } = 0.40;
        public string IconShape { get; set; } = "Squircle";
        public bool IsLocked { get; set; } = false;
        public bool RightDoubleClickEnabled { get; set; } = true;
        public bool SpaceQuickLookEnabled { get; set; } = true;
        public bool AutoStart { get; set; } = true;
        public string ThemeColor { get; set; } = Services.ShellHelper.GetDefaultPastelColorHex();
        public string ThemeMode { get; set; } = "Auto";
        public bool ExplorerContextMenuEnabled { get; set; } = true;
        public bool CombineFilesOnDrop { get; set; } = true;
        public bool OpenInCenterOnly { get; set; } = false;
        public bool AutoCheckUpdates { get; set; } = true;
        public double FolderOpacity { get; set; } = 0.85;
        public string GitHubRepo { get; set; } = "hranthakobian/Panak";
        public DateTime? LastUpdateCheck { get; set; }
    }
}
