# What's New in Panak v1.9.0

## 🌟 Highlights & Major Features

### 1. 🖼️ Real-Time Wallpaper Engine & Desktop Background in Preview
* **Live Wallpaper Capture**: The Setup Wizard now captures the live Windows desktop background in real time. If **Wallpaper Engine** or any interactive live wallpaper is running, Panak captures frames directly from `WorkerW` to display live, moving wallpaper inside the preview cards.
* **Fallback High-Res Wallpaper**: When static wallpapers are active, Panak reads the exact full-resolution Windows wallpaper (`TranscodedWallpaper` / registry).

---

### 2. 🌫️ True Folder Background Blur (Behind Popup)
* **Real Wallpaper Blur Effect**: Folder blur is now applied directly to the desktop backdrop behind the popup using hardware-accelerated shaders and customizable `FolderBlurRadius` (2px to 60px).
* **Blur Toggle & Opacity Slider**: Full control to enable or disable background blur and set folder surface opacity with instant visual feedback.
* **Fixed Screen Blackout**: Fixed the critical bug where backdrop blur caused the entire screen to turn black.

---

### 3. 📂 Desktop Icon Preview & Realistic Open/Close Toggle
* **Interactive Desktop Folder Icon**: The preview showcases an authentic desktop icon on the wallpaper with the chosen Material 3 shape plate and folder title.
* **Open & Close Popup Animation**: Clicking the desktop icon or popup close button (`✕`) triggers realistic cubic-ease scale and fade animations to open or dismiss the folder widget.

---

### 4. 📄 Slider Page Navigation & Mouse Wheel Support
* **Horizontal Paging Mode**: In addition to standard vertical scrollbar, folders support smooth horizontal paged slider navigation.
* **Mouse Wheel Page Flipping**: Scroll the mouse wheel forward or backward to flip through pages naturally.
* **Swipe & Drag Gestures**: Mouse drag / touch swipe across pages with configurable toggle in settings.
* **Centered Pager Controls**: Centered navigation arrows (`‹` / `›`) and pagination dots with an elongated active pill dot.

---

### 5. 📐 Dynamic Grid Wrapping (Max Columns & Rows)
* **Adaptive Dimensions**: Configured grid rows and columns now act as **maximums**. If a folder has fewer items (e.g. 2 items), the popup automatically sizes to the exact rows and columns present rather than leaving empty space.
* **Consistent Item Width**: Items maintain fixed, proportional sizing (~68–70px) and never stretch or warp when configuring column counts.

---

### 6. 🎨 Material 3 Expressive Controls & Horizontal Slide Transitions
* **M3 Expressive Controls**: Toggle switches feature 48x28 pills with morphing thumbs, and radio buttons feature 20px rings with inner bloom dots.
* **Horizontal Glide Transition**: Wizard step changes glide horizontally from left to right (or right to left) with smooth cubic easing.
* **Clean Badges & Centered Footer**: Fully centered step pagination dots across all resolutions, and clean badges without extraneous star characters.

---

### 7. 🌐 Comprehensive Multi-Language Localization
* **Localized "Step" & "Live Preview"**: Added and translated "Step" (Քայլ, Шаг, Étape, Paso, 步骤, خطوة) and "Live Preview" (Ուղիղ նախադիտում, Живой просмотр, etc.) across all 7 supported languages.
* **Settings Clean-Up**: Removed redundant parenthetical English labels across Armenian, Russian, French, Spanish, Chinese, and Arabic.

---

## 📦 Distribution & Packaging
* **Version**: `v1.9.0`
* **Standalone Installer**: `publish\Setup.exe` (Single-file self-contained installer with embedded payload).
* **Installer Package**: `Panak-Installer-v1.9.0.zip` ready for distribution.
