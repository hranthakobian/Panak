# What's New in Panak v2.0.0

## 🌟 Highlights & Major Features (v2.0.0)

### 1. 🔒 Comprehensive Multi-Factor Folder Security & Locking
* **Individual Folder Locks**: Lock folders independently with Password (PBKDF2/SHA-256), PIN (4-8 digits), or phone-like drag-to-draw 3x3 Pattern.
* **Settings Lock**: Lock the entire Settings interface with Password, PIN, or Pattern.
* **Explorer Protection**: The "Open in Windows Explorer" button and context menu item are completely hidden and blocked when a folder is locked.
* **Tamper & Offline Resistance**: Folder locks write persistent security markers to `panak_lock.json` and `desktop.ini` attributes (`PanakLocked=1`).
* **Uninstaller Safeguard**: The Standalone Uninstaller strictly blocks uninstallation if any folder in the system is still locked, preventing accidental loss of access.

---

### 2. 📦 Unpack Panak Folder («Ապահավաքել պանակը»)
* **One-Click Unpack**: Move all contents out of the Panak container directly into its parent folder, clean up the container, and dissolve the custom folder state. Accessible from both header actions and folder context menu.

---

### 3. 🖥️ Redesigned Two-Pane Settings Window with Live Desktop Preview
* **Dual-Column Layout**: Left column houses scrollable setting cards; right column displays a full-height Live Preview with real desktop wallpaper, folder icon, and popup card.
* **Four Section Tabs with Smooth Scroll**: Instant navigation between «Արտաքին», «Դիրք», «Անվտանգություն», and «Համակարգ» with fluid cubic-ease scrolling.
* **Collapsible Preview**: Header button collapses or expands the live preview pane, smoothly transitioning window width between 880px and 520px.
* **Color First**: Color & Hue card is positioned before the Theme Mode card for improved setup ergonomics.

---

### 4. 🪟 Windows 10 Black Blur Fix & Visual Polish
* **BitBlt Exclude Window Compatibility**: Exclude-from-capture is applied strictly on Windows 11 (Build >= 22000), eliminating black rectangles on Windows 10 and falling back to clean wallpaper sampling.
* **Authentic Uninstaller Assets**: Modern dynamic vector SVG trash can with theme-bound brushes and authentic uninstaller icons.

---

# What's New in Panak v1.9.0

## 🌟 Highlights & Major Features (v1.9.0)

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
