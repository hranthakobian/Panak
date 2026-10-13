# What's New in Panak v1.9.0

## 🌟 Highlights & Major Features

### 1. 🚀 Interactive 3-Step Setup Wizard & Onboarding
* **Guided Initial Setup**: First-time users are greeted with a modernized 3-step configuration wizard designed to personalize Panak in seconds before launching.
  * **Step 1 — Appearance & Styling**: Customize folder color, border corner radius, opacity, and blur effect in real time with an instant interactive preview card.
  * **Step 2 — Interaction & Focus**: Configure focus mode (desktop dimming & backdrop blur) and paging controls with live visual feedback.
  * **Step 3 — Ready & Welcome**: A seamless transition into the Welcome card, detailing essential shortcuts, features, and direct access to Settings or community links.
* **Morphing Step Indicator**: Fluid pagination indicators with an animated elongated pill for the active step.
* **Easy Navigation**: Full support for `Next`, `Back`, and `Skip` actions at any stage.

---

### 2. 📄 Horizontal Paging Slider Navigation
* **Slider Page Mode**: Folders now support a horizontal slider view in addition to the standard vertical scrollbar.
* **Navigation Controls**:
  * **Arrow Controls**: Intuitive `<` and `>` navigation buttons on the left and right to paginate smoothly across screens.
  * **Swipe & Drag Gestures**: Mouse click-and-drag / touch-swipe to fling or smoothly navigate between pages.
  * **Page Indicator Dots**: Interactive round dots with an elongated active pill dot showing the current page; clicking any dot navigates directly to that page.
* **Configurable in Settings**:
  * Toggle between **Slider** and **Scrollbar** paging modes.
  * Customize grid rows (2 to 5 rows).
  * Toggle slider navigation arrows on/off.
  * Toggle swipe/drag mouse gesture on/off.

---

### 3. 🌫️ Folder Blur Control & Backdrop Fixes
* **Folder Blur Setting**: Added independent control over folder popup blur background (`Enable Blur` toggle and configurable blur radius slider from 5 to 60).
* **Backdrop Blur Fix**: Resolved the critical bug where enabling the backdrop blur effect caused the entire screen to turn black. The screen is now captured cleanly with GPU-accelerated Gaussian blur and dimming overlay.

---

### 4. 🌊 Material Design 3 Expressive Wavy Progress Bar
* **Sinusoidal Wave Motion**: Upgraded linear progress bars in the installer and setup dialogs to the Material Design 3 Expressive wavy progress bar.
* **Smooth Amplitude Modulation**: Features Hann window envelope tapering at both ends, a 4dp clean gap between active wave and remaining track, and eliminates track overlap.

---

### 5. 🌐 Settings Clean-Up & Multi-Language Enhancements
* **Cleaned Parenthetical English Labels**: Removed repetitive English hints in parentheses across all supported languages (Armenian, English, Russian, French, Spanish, Chinese, Arabic) for a clean, unified, and native appearance.
* **New Localized Strings**: Complete translations for all new paging, blur, and wizard features across 7 languages.

---

## 📦 Distribution & Packaging
* Bumped version to **v1.9.0** across all binaries:
  * `Panak.exe`
  * `Panak.ShellExt.dll`
  * `Setup.exe` (Self-contained standalone single-file installer)
  * `Uninstall.exe`
* Updated installer bundle: `Panak-Installer-v1.9.0.zip` ready for release distribution.
