# What's New in Panak v1.9.0

## 🌟 Highlights & Major Features

### 1. 🖼️ Real-Time Wallpaper Engine & Desktop Background in Preview
* **Live Wallpaper Capture**: The Setup Wizard and Welcome experience capture the live Windows desktop background in real time. If **Wallpaper Engine** or interactive live wallpapers are active, Panak captures frames directly from `WorkerW` to display live, moving wallpaper inside the preview cards.
* **Direct Blur Slider**: An interactive blur slider is integrated directly into the live preview toolbar, offering instantaneous feedback on the wallpaper underlay.
* **Fallback High-Res Wallpaper**: When static wallpapers are active, Panak reads the exact full-resolution Windows wallpaper (`TranscodedWallpaper` / registry).

---

### 2. 🌫️ True Folder Background Blur & Material 3 Rounded Corners
* **Rounded Corners (28px)**: Standardized `CornerRadius="28"` across all folder popup cards, frosted glass underlays, and preview surfaces for authentic Material 3 Expressive aesthetics.
* **Real Wallpaper Blur Effect**: Folder blur is applied directly to the desktop backdrop behind the popup using hardware-accelerated shaders and customizable `FolderBlurRadius` (2px to 60px).
* **Focus Mode Unpin Fix**: Resolved the z-order drop/flicker where unpinned popups could briefly fall behind the dimmed backdrop window.

---

### 3. 📂 Authentic Desktop Folder Icon Preview & Origin Animation
* **Authentic 2x2 Desktop Icon Plate**: Preview renders an authentic folder icon featuring the selected Expressive shape plate (Circle, Squircle, Clover, Scallop, Arch), a 2x2 miniature icon grid (`media_1791914326888.png`), and a shadowed label.
* **Origin-Based Opening Motion**: When `OpenInCenterOnly` is disabled, the folder expands and morphs directly from the desktop icon; when enabled, it scales smoothly from the screen center.
* **Interactive Open/Close Toggle**: Clicking the desktop icon or preview close button (`✕`) triggers realistic cubic-ease scale and fade animations.

---

### 4. 🗂️ Authentic Folder Header & Dynamic Dynamic Sizing
* **Authentic Header**: Features the folder title, an items count badge (e.g., "18 նիշք" / "18 items"), and four interactive action buttons (Pin, Edit, Explorer, Close) matching `media_1791914326899.png`.
* **Dynamic Sizing**: Rows and columns represent maximum boundaries. When a folder has fewer items, the popup dimensions conform tightly to the exact count without vacant space.
* **Dynamic Slider Pages**: Pages are calculated dynamically without empty trailing slides. Pager controls and dots are automatically hidden when there is only a single page. Active dots feature an elongated pill indicator (18x6), and any dot can be clicked to jump between pages.

---

### 5. 🎛️ Smooth Animated Toggles across All Windows
* **Fluid Storyboard Animations**: Standardized Material 3 Expressive switch toggles across `WelcomeWindow`, `SettingsWindow`, `SetupWindow`, and `UninstallerWindow` with smooth sliding animations (`Canvas.Left` 2 to 21 with `CubicEase` over 0.22s).

---

### 6. 🎲 Random Folder Attributes & Custom Color Swatches
* **Random Attributes on Folder Creation**: Added toggles for Random Shape, Random Color, and Random Opacity in both Settings and Welcome Wizard, dynamically applied when new Panak folders are created.
* **Custom Hue Swatches**: Clicking the Hue color indicator circle immediately adds the current tint to custom palette colors (`CustomPaletteColors`), persisting it and rendering accessible swatches in Settings.
* **JSON Settings Export & Import**: Convenient Export and Import buttons in the Settings footer allow backing up and restoring complete configurations via `.json`.

---

### 7. 🌐 Comprehensive Multi-Language Localization
* **Cleaned Parenthetical English**: Removed extraneous parenthetical English labels across all 7 supported languages (`hy`, `en`, `ru`, `fr`, `es`, `zh`, `ar`).
* **Fully Localized Controls**: Complete localization for all new keys, including "Step", "Live Preview", "Random", "Export/Import Settings", and count badges.

---

## 📦 Distribution & Packaging
* **Version**: `v1.9.0`
* **Standalone Installer**: `publish\Setup.exe` (Single-file self-contained installer with embedded payload).
* **Installer Package**: `Panak-Installer-v1.9.0.zip` ready for distribution.
