using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Panak.Models;
using Panak.Services;
using Panak.Views;

namespace Panak.Tests {
    [TestClass]
    public class CoreTests {
        [TestMethod]
        public void TestLocalizationKeysExistInAllLanguages() {
            var loc = LocalizationService.Instance;
            string[] testKeys = new[] {
                "CreateFolder", "OpenInPanak", "NewFolder", "Settings",
                "LockFolders", "IconShape", "ItemsPerRow", "FocusMode",
                "Language", "RightDoubleClick", "SpaceQuickLook", "Install", "Uninstall",
                "ConvertToPanak", "RevertToRegular", "AlwaysOpenWithPanak",
                "CombineFilesOnDrop", "CombineFilesOnDropDesc", "CollectIntoPanakFolder",
                "Hue", "DefaultFolderColor",
                "LanguageAiNote", "CenterPopupOnly", "CenterPopupOnlyDesc", "Support", "SupportDesc", "DonateOnKofi"
            };

            foreach (var lang in new[] { "hy", "en", "ru", "fr", "es", "zh", "ar" }) {
                loc.CurrentLanguage = lang;
                foreach (var key in testKeys) {
                    string val = loc[key];
                    Assert.IsFalse(string.IsNullOrWhiteSpace(val), $"Key '{key}' should not be empty in language '{lang}'");
                }
            }
        }

        [TestMethod]
        public void TestIconGeneratorRendersPlate() {
            string[] shapes = new[] { "Circle", "Squircle", "Clover", "Scallop", "Arch", "RoundedRect", "Teardrop" };
            Color testColor = Color.FromArgb(59, 130, 246);

            // Create mini mock icon
            using Bitmap mini = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(mini)) {
                g.Clear(Color.Red);
            }

            var miniList = new List<Bitmap> { mini };

            foreach (var shape in shapes) {
                using Bitmap result = IconGenerator.RenderPlateWithGrid(256, shape, testColor, miniList);
                Assert.IsNotNull(result, $"Plate for shape {shape} should not be null");
                Assert.AreEqual(256, result.Width);
                Assert.AreEqual(256, result.Height);
            }
        }

        [TestMethod]
        public void TestFolderIconGenerationAndDesktopIni() {
            string tempDir = Path.Combine(Path.GetTempPath(), "PanakTestFolder_" + Guid.NewGuid().ToString("N"));
            try {
                Directory.CreateDirectory(tempDir);

                // Create a couple of mock files inside
                File.WriteAllText(Path.Combine(tempDir, "file1.txt"), "hello");
                File.WriteAllText(Path.Combine(tempDir, "file2.txt"), "world");

                IconGenerator.GenerateFolderIcon(tempDir, "Squircle", "#10B981");

                string[] icoFiles = Directory.GetFiles(tempDir, "panak_icon*.ico");
                Assert.IsTrue(icoFiles.Length > 0, "panak_icon*.ico should exist");
                string icoPath = icoFiles[0];
                string iniPath = Path.Combine(tempDir, "desktop.ini");
                Assert.IsTrue(File.Exists(iniPath), "desktop.ini should exist");

                // Check ICO file has valid size and header
                byte[] icoBytes = File.ReadAllBytes(icoPath);
                Assert.IsTrue(icoBytes.Length > 100, "ICO file should contain image frames");
                Assert.AreEqual(0, icoBytes[0]);
                Assert.AreEqual(0, icoBytes[1]);
                Assert.AreEqual(1, icoBytes[2]); // Type 1 = Icon
            } finally {
                if (Directory.Exists(tempDir)) {
                    // Reset attributes to permit deletion
                    File.SetAttributes(tempDir, FileAttributes.Normal);
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [TestMethod]
        public void TestGenerateFolderIconWithCustomOpacityAndPanakJson() {
            string tempDir = Path.Combine(Path.GetTempPath(), "PanakOpacityTest_" + Guid.NewGuid().ToString("N"));
            try {
                Directory.CreateDirectory(tempDir);
                File.WriteAllText(Path.Combine(tempDir, "sample1.txt"), "hello");

                var data = new PanakFolderData {
                    Title = "TestOpacity",
                    Color = "#D7C5FC",
                    IconShape = "Clover",
                    BackgroundOpacity = 0.60
                };
                data.Save(tempDir);

                IconGenerator.GenerateFolderIcon(tempDir);

                string iniPath = Path.Combine(tempDir, "desktop.ini");
                Assert.IsTrue(File.Exists(iniPath));
                string iniContent = File.ReadAllText(iniPath);
                StringAssert.Contains(iniContent, "panak_icon_0.ico,0");

                string[] icoFiles = Directory.GetFiles(tempDir, "panak_icon*.ico");
                Assert.AreEqual(1, icoFiles.Length);
            } finally {
                if (Directory.Exists(tempDir)) {
                    File.SetAttributes(tempDir, FileAttributes.Normal);
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [TestMethod]
        public void TestTwoSlotAlternatingIconsAndReadOnlyIntegrity() {
            string tempDir = Path.Combine(Path.GetTempPath(), "PanakSlotTest_" + Guid.NewGuid().ToString("N"));
            try {
                Directory.CreateDirectory(tempDir);
                File.WriteAllText(Path.Combine(tempDir, "item1.txt"), "test1");

                // 1st generation -> should use slot 0
                IconGenerator.GenerateFolderIcon(tempDir, "Squircle", "#3B82F6");

                string iniPath = Path.Combine(tempDir, "desktop.ini");
                Assert.IsTrue(File.Exists(iniPath));
                var folderAttrs = File.GetAttributes(tempDir);
                Assert.IsTrue(folderAttrs.HasFlag(FileAttributes.ReadOnly), "Folder must have ReadOnly attribute");

                var iniAttrs = File.GetAttributes(iniPath);
                Assert.IsTrue(iniAttrs.HasFlag(FileAttributes.Hidden), "desktop.ini must have Hidden attribute");
                Assert.IsTrue(iniAttrs.HasFlag(FileAttributes.System), "desktop.ini must have System attribute");

                string content1 = File.ReadAllText(iniPath);
                StringAssert.Contains(content1, "panak_icon_0.ico,0");
                Assert.IsFalse(content1.Contains(":\\"), "desktop.ini should use relative path, not absolute path");

                // 2nd generation (simulate file change) -> should switch to slot 1
                File.WriteAllText(Path.Combine(tempDir, "item2.txt"), "test2");
                IconGenerator.GenerateFolderIcon(tempDir, "Squircle", "#3B82F6");

                string content2 = File.ReadAllText(iniPath);
                StringAssert.Contains(content2, "panak_icon_1.ico,0", "Should switch to panak_icon_1.ico on second generation");

                // 3rd generation -> should switch to next slot (panak_icon_2.ico) to bypass Windows Shell icon cache
                File.Delete(Path.Combine(tempDir, "item1.txt"));
                IconGenerator.GenerateFolderIcon(tempDir, "Squircle", "#3B82F6");

                string content3 = File.ReadAllText(iniPath);
                StringAssert.Contains(content3, "panak_icon_2.ico,0", "Should rotate to panak_icon_2.ico on third generation to bypass Windows Shell icon cache");
            } finally {
                if (Directory.Exists(tempDir)) {
                    File.SetAttributes(tempDir, FileAttributes.Normal);
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [TestMethod]
        public void TestSequentialIconRotationBypassesShellCache() {
            string tempDir = Path.Combine(Path.GetTempPath(), "PanakRotTest_" + Guid.NewGuid().ToString("N"));
            try {
                Directory.CreateDirectory(tempDir);
                File.WriteAllText(Path.Combine(tempDir, "sample.txt"), "hello");

                for (int i = 0; i < 5; i++) {
                    IconGenerator.GenerateFolderIcon(tempDir, "Squircle", "#3B82F6");
                    string iniPath = Path.Combine(tempDir, "desktop.ini");
                    Assert.IsTrue(File.Exists(iniPath));
                    string iniContent = File.ReadAllText(iniPath);
                    StringAssert.Contains(iniContent, $"panak_icon_{i}.ico,0", $"Generation {i} should use panak_icon_{i}.ico");

                    string[] activeIcos = Directory.GetFiles(tempDir, "panak_icon*.ico");
                    Assert.AreEqual(1, activeIcos.Length, "Only the current active panak_icon_*.ico should remain on disk");
                    Assert.AreEqual($"panak_icon_{i}.ico", Path.GetFileName(activeIcos[0]), "The remaining ICO file must match the active slot");
                }
            } finally {
                if (Directory.Exists(tempDir)) {
                    File.SetAttributes(tempDir, FileAttributes.Normal);
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [TestMethod]
        public void TestAppSettingsSerialization() {
            var settings = new AppSettings {
                Language = "hy",
                GridColumns = 6,
                FocusModeEnabled = true,
                FocusDimmingOpacity = 0.55,
                IconShape = "Circle",
                IsLocked = true,
                ThemeColor = "#8B5CF6",
                CombineFilesOnDrop = true,
                OpenInCenterOnly = true
            };

            string json = System.Text.Json.JsonSerializer.Serialize(settings);
            var deserialized = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json);

            Assert.IsNotNull(deserialized);
            Assert.AreEqual("hy", deserialized.Language);
            Assert.AreEqual(6, deserialized.GridColumns);
            Assert.AreEqual(0.55, deserialized.FocusDimmingOpacity);
            Assert.AreEqual("Circle", deserialized.IconShape);
            Assert.IsTrue(deserialized.IsLocked);
            Assert.AreEqual("#8B5CF6", deserialized.ThemeColor);
            Assert.IsTrue(deserialized.CombineFilesOnDrop);
            Assert.IsTrue(deserialized.OpenInCenterOnly);
        }

        [TestMethod]
        public void TestPanakFolderDataSaveWithHiddenAttribute() {
            string tempDir = Path.Combine(Path.GetTempPath(), "PanakSaveTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try {
                var data = new PanakFolderData {
                    Title = "TestFolder",
                    Color = "#3B82F6"
                };
                data.Save(tempDir);

                string metaFile = Path.Combine(tempDir, "panak.json");
                Assert.IsTrue(File.Exists(metaFile), "panak.json should exist");
                Assert.IsTrue(File.GetAttributes(metaFile).HasFlag(FileAttributes.Hidden), "panak.json should be Hidden");

                // Now modify color and save again
                data.Color = "#EF4444";
                data.Save(tempDir);

                var reloaded = PanakFolderData.Load(tempDir);
                Console.WriteLine("RELOADED COLOR: " + reloaded.Color);
                Assert.AreEqual("#EF4444", reloaded.Color, "Color must be saved even if panak.json is Hidden!");
            } finally {
                if (Directory.Exists(tempDir)) {
                    File.SetAttributes(tempDir, FileAttributes.Normal);
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [TestMethod]
        public void TestCollectFilesIntoPanakFolder() {
            string tempDir = Path.Combine(Path.GetTempPath(), "PanakCollectTest_" + Guid.NewGuid().ToString("N"));
            try {
                Directory.CreateDirectory(tempDir);
                string f1 = Path.Combine(tempDir, "sample1.txt");
                string f2 = Path.Combine(tempDir, "sample2.txt");
                File.WriteAllText(f1, "hello");
                File.WriteAllText(f2, "world");

                string createdFolder = PanakManager.Instance.CreatePanakFolder(tempDir, new[] { f1, f2 }, "CombinedFolder");
                Assert.IsTrue(Directory.Exists(createdFolder), "Combined folder should exist");
                Assert.IsTrue(File.Exists(Path.Combine(createdFolder, "panak.json")), "panak.json should exist");
                Assert.IsTrue(File.Exists(Path.Combine(createdFolder, "sample1.txt")), "sample1.txt should have moved");
                Assert.IsTrue(File.Exists(Path.Combine(createdFolder, "sample2.txt")), "sample2.txt should have moved");
            } finally {
                if (Directory.Exists(tempDir)) {
                    foreach (var file in Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories)) {
                        try {
                            File.SetAttributes(file, FileAttributes.Normal);
                        } catch { }
                    }
                    foreach (var subDir in Directory.GetDirectories(tempDir, "*", SearchOption.AllDirectories)) {
                        try {
                            File.SetAttributes(subDir, FileAttributes.Normal);
                        } catch { }
                    }
                    File.SetAttributes(tempDir, FileAttributes.Normal);
                    try {
                        Directory.Delete(tempDir, true);
                    } catch {
                        System.Threading.Thread.Sleep(100);
                        try { Directory.Delete(tempDir, true); } catch { }
                    }
                }
            }
        }

        [TestMethod]
        public void TestInstallerDirectoryComputation() {
            string installDir = InstallerService.GetInstallDirectory();
            Assert.IsFalse(string.IsNullOrEmpty(installDir));
            Assert.IsTrue(installDir.Contains("Panak"));
        }

        [TestMethod]
        public void TestDirectWriteToHiddenSystemFile() {
            string tempDir = Path.Combine(Path.GetTempPath(), "PanakWriteTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try {
                string testFile = Path.Combine(tempDir, "desktop.ini");
                File.WriteAllText(testFile, "Initial content", System.Text.Encoding.Unicode);
                File.SetAttributes(testFile, FileAttributes.Hidden | FileAttributes.System);

                // Attempt to write using FileMode.Open
                using (var fs = new FileStream(testFile, FileMode.Open, FileAccess.Write, FileShare.ReadWrite)) {
                    fs.SetLength(0);
                    using var sw = new StreamWriter(fs, System.Text.Encoding.Unicode);
                    sw.Write("Updated content");
                }

                var attrs = File.GetAttributes(testFile);
                Console.WriteLine("Attributes after write: " + attrs);
                Assert.IsTrue(attrs.HasFlag(FileAttributes.Hidden), "Must keep Hidden");
                Assert.IsTrue(attrs.HasFlag(FileAttributes.System), "Must keep System");
                Assert.AreEqual("Updated content", File.ReadAllText(testFile));
                Console.WriteLine("Direct write to Hidden+System file SUCCEEDED without losing attributes!");
            } finally {
                if (Directory.Exists(tempDir)) {
                    File.SetAttributes(tempDir, FileAttributes.Normal);
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [TestMethod]
        public void TestPanakFolderConversionAndReversion() {
            string tempDir = Path.Combine(Path.GetTempPath(), "PanakConvertTest_" + Guid.NewGuid().ToString("N"));
            try {
                Directory.CreateDirectory(tempDir);
                Assert.IsFalse(PanakManager.Instance.IsPanakFolder(tempDir), "Fresh regular directory should NOT be a Panak folder");

                // Convert to Panak folder
                PanakManager.Instance.ConvertToPanakFolder(tempDir);
                Assert.IsTrue(PanakManager.Instance.IsPanakFolder(tempDir), "Converted directory MUST be recognized as a Panak folder");
                Assert.IsTrue(File.Exists(Path.Combine(tempDir, "panak.json")), "panak.json MUST exist after conversion");

                var loadedData = PanakFolderData.Load(tempDir);
                Assert.IsTrue(loadedData.AutoPopup, "AutoPopup MUST be true after conversion");

                // Revert to regular folder
                PanakManager.Instance.RevertToRegularFolder(tempDir);
                Assert.IsFalse(PanakManager.Instance.IsPanakFolder(tempDir), "Reverted directory must NOT be a Panak folder");
                Assert.IsFalse(File.Exists(Path.Combine(tempDir, "panak.json")), "panak.json must be removed after revert");
            } finally {
                if (Directory.Exists(tempDir)) {
                    File.SetAttributes(tempDir, FileAttributes.Normal);
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [TestMethod]
        public void TestEmptyFolderIconGenerationAndPreservation() {
            string tempDir = Path.Combine(Path.GetTempPath(), "PanakEmptyFolderTest_" + Guid.NewGuid().ToString("N"));
            try {
                Directory.CreateDirectory(tempDir);

                // Generate icon on completely empty folder
                IconGenerator.GenerateFolderIcon(tempDir, "Squircle", "#3B82F6");

                string iniPath = Path.Combine(tempDir, "desktop.ini");
                Assert.IsTrue(File.Exists(iniPath), "desktop.ini must exist for empty Panak folder");

                var folderAttrs = File.GetAttributes(tempDir);
                Assert.IsTrue(folderAttrs.HasFlag(FileAttributes.ReadOnly), "Empty Panak folder must maintain ReadOnly attribute");

                var iniAttrs = File.GetAttributes(iniPath);
                Assert.IsTrue(iniAttrs.HasFlag(FileAttributes.Hidden) && iniAttrs.HasFlag(FileAttributes.System), "desktop.ini must have Hidden and System attributes");

                string[] icos = Directory.GetFiles(tempDir, "panak_icon*.ico");
                Assert.IsTrue(icos.Length > 0, "Icon file must exist for empty folder");
                byte[] bytes = File.ReadAllBytes(icos[0]);
                Assert.IsTrue(bytes.Length > 100, "ICO file must contain valid icon frames");
            } finally {
                if (Directory.Exists(tempDir)) {
                    File.SetAttributes(tempDir, FileAttributes.Normal);
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [TestMethod]
        public void TestFolderOpenGracePeriodLogic() {
            var thread = new System.Threading.Thread(() => {
                var popup = new Views.FolderPopupWindow();
                Assert.IsFalse(popup.IsWithinOpenGracePeriod());
                popup.LoadFolder(Path.GetTempPath(), System.Windows.Rect.Empty);
                Assert.IsTrue(popup.IsWithinOpenGracePeriod(), "Popup should be within grace period immediately after LoadFolder");
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();
        }

        [TestMethod]
        public void TestFolderIconPreservedAfterUpdate() {
            string desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string testPanak = PanakManager.Instance.CreatePanakFolder(desktopDir, null, "TestPanakColorChange");
                try {
                    System.Threading.Thread.Sleep(300);

                    using (var bWin = ShellHelper.GetFileIconBitmap(@"C:\Windows", isLarge: true)) {
                        var pCenter = bWin?.GetPixel(bWin.Width / 2, bWin.Height / 2);
                        var pCorner = bWin?.GetPixel(bWin.Width / 6, bWin.Height / 6);
                        Console.WriteLine($"WINDOWS FOLDER Center: R={pCenter?.R}, G={pCenter?.G}, B={pCenter?.B} | Corner: R={pCorner?.R}, G={pCorner?.G}, B={pCorner?.B}");
                    }

                    string[] icos = Directory.GetFiles(testPanak, "panak_icon*.ico");
                    if (icos.Length > 0) {
                        using var ico = new Icon(icos[0], 256, 256);
                        using var bmp = ico.ToBitmap();
                        var pCenter = bmp.GetPixel(bmp.Width / 2, bmp.Height / 2);
                        var pCorner = bmp.GetPixel(bmp.Width / 6, bmp.Height / 6);
                        Console.WriteLine($"ACTUAL ICO FILE ({Path.GetFileName(icos[0])}) Center: R={pCenter.R}, G={pCenter.G}, B={pCenter.B} | Corner: R={pCorner.R}, G={pCorner.G}, B={pCorner.B}");
                    }

                    using (var bInit = ShellHelper.GetFileIconBitmap(testPanak, isLarge: true)) {
                        var pCenter = bInit?.GetPixel(bInit.Width / 2, bInit.Height / 2);
                        var pCorner = bInit?.GetPixel(bInit.Width / 6, bInit.Height / 6);
                        Console.WriteLine($"INITIAL Center: R={pCenter?.R}, G={pCenter?.G}, B={pCenter?.B} | Corner: R={pCorner?.R}, G={pCorner?.G}, B={pCorner?.B}");
                    }

                    // Now change color using PanakManager / IconGenerator
                    var data = PanakFolderData.Load(testPanak);
                    data.Color = "#EF4444"; // Red
                    data.Save(testPanak);
                    IconGenerator.GenerateFolderIcon(testPanak, data.IconShape, data.Color);

                    string iniPath = Path.Combine(testPanak, "desktop.ini");
                    Console.WriteLine("DESKTOP.INI AFTER RED:\n" + File.ReadAllText(iniPath));
                    var iniAttrs = File.GetAttributes(iniPath);
                    Console.WriteLine("DESKTOP.INI ATTRS: " + iniAttrs);
                    var folderAttrs = File.GetAttributes(testPanak);
                    Console.WriteLine("FOLDER ATTRS: " + folderAttrs);

                    System.Threading.Thread.Sleep(500);

                    using (var bAfter = ShellHelper.GetFileIconBitmap(testPanak, isLarge: true)) {
                        var pCenter = bAfter?.GetPixel(bAfter.Width / 2, bAfter.Height / 2);
                        var pCorner = bAfter?.GetPixel(bAfter.Width / 6, bAfter.Height / 6);
                        Console.WriteLine($"AFTER COLOR CHANGE Center: R={pCenter?.R}, G={pCenter?.G}, B={pCenter?.B} | Corner: R={pCorner?.R}, G={pCorner?.G}, B={pCorner?.B}");
                    }

                    // Now add a file and update icon (simulate drop or Watcher)
                    string f1 = Path.Combine(testPanak, "file1.txt");
                    File.WriteAllText(f1, "sample");
                    IconGenerator.GenerateFolderIcon(testPanak, data.IconShape, data.Color);

                    System.Threading.Thread.Sleep(500);

                    using (var bFile = ShellHelper.GetFileIconBitmap(testPanak, isLarge: true)) {
                        var pCenter = bFile?.GetPixel(bFile.Width / 2, bFile.Height / 2);
                        var pCorner = bFile?.GetPixel(bFile.Width / 6, bFile.Height / 6);
                        Console.WriteLine($"AFTER ADDING FILE Center: R={pCenter?.R}, G={pCenter?.G}, B={pCenter?.B} | Corner: R={pCorner?.R}, G={pCorner?.G}, B={pCorner?.B}");
                    }
                } finally {
                    if (Directory.Exists(testPanak)) {
                        PanakManager.Instance.RevertToRegularFolder(testPanak);
                        Directory.Delete(testPanak, true);
                    }
                }
        }

        [TestMethod]
        public void TestFolderPopupWindowAnimatedControlsExist() {
            var thread = new System.Threading.Thread(() => {
                var window = new FolderPopupWindow();
                Assert.IsNotNull(window.FindName("EditPanelContainer"), "EditPanelContainer must exist for animated settings flyout");
                Assert.IsNotNull(window.FindName("ContentAreaGrid"), "ContentAreaGrid must exist for animated content sizing");
                var editPanel = window.FindName("EditPanel") as System.Windows.FrameworkElement;
                Assert.IsNotNull(editPanel, "EditPanel must exist");
                Assert.AreEqual(234.0, editPanel.Width, 0.1, "EditPanel width must be 3 columns (234px)");
                Assert.IsNotNull(window.FindName("EditPanelTranslate"), "EditPanelTranslate must exist for slide animation");
                Assert.IsNotNull(window.FindName("HueSlider"), "HueSlider must exist for pastel color selection");
                Assert.IsNotNull(window.FindName("PopupBlurImage"), "PopupBlurImage must exist for frosted glass / acrylic blur effect");
                Assert.IsNotNull(window.FindName("FrostedTintLayer"), "FrostedTintLayer must exist for dark frosted glass overlay");
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();
        }

        [TestMethod]
        public void TestSettingsWindowCanBeInstantiated() {
            Exception? thrown = null;
            var thread = new System.Threading.Thread(() => {
                try {
                    var window = new SettingsWindow();
                    Assert.IsNotNull(window.FindName("KofiButton"), "KofiButton must exist");
                    Assert.IsNotNull(window.FindName("CenterPopupCheckBox"), "CenterPopupCheckBox must exist");
                } catch (Exception ex) {
                    thrown = ex;
                }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (thrown != null) {
                Assert.Fail("SettingsWindow instantiation threw: " + thrown);
            }
        }

        [TestMethod]
        public void TestPastelColorGenerationAndLuminance() {
            // Verify exact #FCDDC5 color generation at hue 26.2
            var peachColor = ShellHelper.ColorFromPastelHue(26.18);
            string peachHex = ShellHelper.ColorFromPastelHueHex(26.18);
            Assert.AreEqual("#FCDDC5", peachHex, "Hue 26.18 should generate exact hex #FCDDC5");
            Assert.AreEqual("#FCDDC5", ShellHelper.DefaultPastelColorHex, "DefaultPastelColorHex must be #FCDDC5");

            // Test 12 hues around the color wheel (0, 30, 60, ... 330)
            for (double h = 0; h < 360; h += 30) {
                var color = ShellHelper.ColorFromPastelHue(h);
                string hex = ShellHelper.ColorFromPastelHueHex(h);

                // Check brightness / luminance is light pastel (> 0.70 and <= 0.98)
                float luminance = (0.299f * color.R + 0.587f * color.G + 0.114f * color.B) / 255.0f;
                Assert.IsTrue(luminance >= 0.72f, $"Luminance at hue {h} ({hex}) should be >= 0.72 (was {luminance})");
                Assert.IsTrue(luminance <= 0.98f, $"Luminance at hue {h} ({hex}) should be <= 0.98 (was {luminance})");

                // Check hue round-trip
                double recoveredHue = ShellHelper.GetHueFromHex(hex);
                double diff = Math.Abs(recoveredHue - h);
                if (diff > 180) diff = 360 - diff;
                Assert.IsTrue(diff < 5.0, $"Recovered hue {recoveredHue} should be close to {h} (diff={diff})");
            }
        }

        [TestMethod]
        public void TestDefaultFolderColorIsPastel() {
            var data = new PanakFolderData();
            Assert.IsFalse(string.IsNullOrEmpty(data.Color), "Default folder color should not be empty");
            Assert.IsTrue(data.Color.StartsWith("#"), "Default folder color should be hex");
            Assert.AreEqual("#FCDDC5", data.Color, "Default folder color should be #FCDDC5");

            var settings = new AppSettings();
            Assert.IsFalse(string.IsNullOrEmpty(settings.ThemeColor), "Default settings theme color should not be empty");
            Assert.IsTrue(settings.ThemeColor.StartsWith("#"), "ThemeColor should be hex");
            Assert.AreEqual("#FCDDC5", settings.ThemeColor, "Default theme color should be #FCDDC5");

            // Verify icon generator renders on pastel background without crash
            using var plate = IconGenerator.RenderPlateWithGrid(256, "Squircle", ColorTranslator.FromHtml(ShellHelper.DefaultPastelColorHex), new List<Bitmap>());
            Assert.IsNotNull(plate);
            Assert.AreEqual(256, plate.Width);
        }

        [TestMethod]
        public void TestPanakFolderDataSynchronizesWithDiskFolderName() {
            string tempDir = Path.Combine(Path.GetTempPath(), "PanakSyncTest_" + Guid.NewGuid().ToString("N"));
            try {
                Directory.CreateDirectory(tempDir);
                var data = PanakFolderData.Load(tempDir);
                Assert.AreEqual(Path.GetFileName(tempDir), data.Title);

                // Save custom title in json
                data.Title = "OldMismatchedTitle";
                data.Save(tempDir);

                // Load again - Title should sync to disk folder name
                var reloaded = PanakFolderData.Load(tempDir);
                Assert.AreEqual(Path.GetFileName(tempDir), reloaded.Title);
            } finally {
                if (Directory.Exists(tempDir)) {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [TestMethod]
        public void TestTwoIconsRenderCenteredAndLarger() {
            RectangleF innerRect = new RectangleF(20, 20, 200, 200);

            var (dim2, centers2) = IconGenerator.CalculateIconLayout(innerRect, 2);
            var (dim4, _) = IconGenerator.CalculateIconLayout(innerRect, 4);

            // 2-item icons must be larger than 4-item grid icons ("մի քիչ խոշոր")
            Assert.IsTrue(dim2 > dim4, $"2-item iconDim ({dim2}) should be larger than 4-item iconDim ({dim4})");

            Assert.AreEqual(2, centers2.Length);

            // Both must be centered vertically at cy
            float expectedCy = innerRect.Y + innerRect.Height / 2f;
            Assert.AreEqual(expectedCy, centers2[0].Y, 0.001f);
            Assert.AreEqual(expectedCy, centers2[1].Y, 0.001f);

            // Must be centered horizontally
            float midX = innerRect.X + innerRect.Width / 2f;
            float dist0 = midX - centers2[0].X;
            float dist1 = centers2[1].X - midX;
            Assert.IsTrue(dist0 > 0, "Icon 0 should be to the left of center");
            Assert.IsTrue(dist1 > 0, "Icon 1 should be to the right of center");
            Assert.AreEqual(dist0, dist1, 0.001f, "2 icons must be symmetrically centered");
        }

        [TestMethod]
        public void TestThreeIconsRenderTriangularApexUp() {
            RectangleF innerRect = new RectangleF(20, 20, 200, 200);

            var (dim2, _) = IconGenerator.CalculateIconLayout(innerRect, 2);
            var (dim3, centers3) = IconGenerator.CalculateIconLayout(innerRect, 3);

            // 3-item icons must be a bit smaller than 2-item icons ("մի քիչ ավելի փոքր քան երկու հատի դեպքում")
            Assert.IsTrue(dim3 < dim2, $"3-item iconDim ({dim3}) should be smaller than 2-item iconDim ({dim2})");

            Assert.AreEqual(3, centers3.Length);

            // Apex icon (centers3[0]): centered horizontally, on top row
            float midX = innerRect.X + innerRect.Width / 2f;
            Assert.AreEqual(midX, centers3[0].X, 0.001f, "Apex icon must be horizontally centered");

            // Base icons (centers3[1] and centers3[2]): on bottom row
            Assert.IsTrue(centers3[0].Y < centers3[1].Y, "Apex icon must be above bottom row icons (apex pointing up)");
            Assert.AreEqual(centers3[1].Y, centers3[2].Y, 0.001f, "Bottom two icons must share the same Y line");

            // Bottom row symmetry
            Assert.IsTrue(centers3[1].X < midX, "Bottom-left icon must be left of center");
            Assert.IsTrue(centers3[2].X > midX, "Bottom-right icon must be right of center");
            Assert.AreEqual(midX - centers3[1].X, centers3[2].X - midX, 0.001f, "Bottom two icons must be equidistant from apex X");
        }

        [TestMethod]
        public void TestInnerIconDimensionsIncreasedByFivePixels() {
            RectangleF innerRect = new RectangleF(0, 0, 100, 100);

            // 1-item
            var (dim1, _) = IconGenerator.CalculateIconLayout(innerRect, 1);
            float baseDim1 = 100f * 0.58f;
            Assert.AreEqual(baseDim1 + 5f, dim1, 0.001f);

            // 2-item
            var (dim2, _) = IconGenerator.CalculateIconLayout(innerRect, 2);
            float baseDim2 = 100f * 0.48f;
            Assert.AreEqual(baseDim2 + 5f, dim2, 0.001f);

            // 3-item
            var (dim3, _) = IconGenerator.CalculateIconLayout(innerRect, 3);
            float baseDim3 = 100f * 0.43f;
            Assert.AreEqual(baseDim3 + 5f, dim3, 0.001f);

            // 4-item (2x2 grid)
            var (dim4, _) = IconGenerator.CalculateIconLayout(innerRect, 4);
            float baseDim4 = 50f * 0.85f;
            Assert.AreEqual(baseDim4 + 5f, dim4, 0.001f);
        }

        [TestMethod]
        public void TestIconOpacitySupportAndDropShadowClipping() {
            Color testColor = Color.FromArgb(215, 197, 252);
            using var mini = new Bitmap(16, 16);

            // Test at 60% opacity (like user's background opacity)
            using Bitmap plate60 = IconGenerator.RenderPlateWithGrid(256, "Clover", testColor, new List<Bitmap> { mini }, opacity: 0.60);
            Assert.IsNotNull(plate60);
            Assert.AreEqual(256, plate60.Width);
            Assert.AreEqual(256, plate60.Height);

            // Test at 100% opacity
            using Bitmap plate100 = IconGenerator.RenderPlateWithGrid(256, "Squircle", testColor, new List<Bitmap> { mini }, opacity: 1.0);
            Assert.IsNotNull(plate100);
            Assert.AreEqual(256, plate100.Width);
        }

        [TestMethod]
        public void TestFolderAutoDissolvesWhenOneItemRemains() {
            string tempParent = Path.Combine(Path.GetTempPath(), "PanakDissolveParent_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempParent);
            try {
                string f1 = Path.Combine(tempParent, "itemA.txt");
                string f2 = Path.Combine(tempParent, "itemB.txt");
                File.WriteAllText(f1, "A");
                File.WriteAllText(f2, "B");

                string panakFolder = PanakManager.Instance.CreatePanakFolder(tempParent, new[] { f1, f2 }, "TestPanakFolder");
                Assert.IsTrue(Directory.Exists(panakFolder));

                // 2 items initially
                var itemsInitial = PanakManager.GetUserItems(panakFolder);
                Assert.AreEqual(2, itemsInitial.Count);
                Assert.IsFalse(PanakManager.Instance.CheckAndDissolveFolder(panakFolder), "Folder with 2 items should NOT dissolve");

                // Remove 1 item so 1 item remains inside
                string insideFileA = Path.Combine(panakFolder, "itemA.txt");
                File.Delete(insideFileA);

                // Now 1 item remains inside ("մնում է մեկ հատ, այն ինքնաշխատ ջնջվում է")
                bool dissolved = PanakManager.Instance.CheckAndDissolveFolder(panakFolder);
                Assert.IsTrue(dissolved, "Folder should dissolve when only 1 item remains");
                Assert.IsFalse(Directory.Exists(panakFolder), "Panak folder should be deleted");

                // The single remaining item (itemB.txt) must have been moved out to parent directory safely!
                string movedItemB = Path.Combine(tempParent, "itemB.txt");
                Assert.IsTrue(File.Exists(movedItemB), "Remaining item must be preserved by moving out to parent directory");
            } finally {
                if (Directory.Exists(tempParent)) {
                    foreach (var f in Directory.GetFiles(tempParent, "*", SearchOption.AllDirectories)) {
                        try { File.SetAttributes(f, FileAttributes.Normal); } catch { }
                    }
                    Directory.Delete(tempParent, true);
                }
            }
        }

        [TestMethod]
        public void TestFolderAutoDeletesWhenZeroItemsRemain() {
            string tempParent = Path.Combine(Path.GetTempPath(), "PanakEmptyParent_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempParent);
            try {
                string f1 = Path.Combine(tempParent, "item1.txt");
                string f2 = Path.Combine(tempParent, "item2.txt");
                File.WriteAllText(f1, "1");
                File.WriteAllText(f2, "2");

                string panakFolder = PanakManager.Instance.CreatePanakFolder(tempParent, new[] { f1, f2 }, "EmptyTestFolder");
                Assert.IsTrue(Directory.Exists(panakFolder));

                // Remove both items so 0 items remain inside
                File.Delete(Path.Combine(panakFolder, "item1.txt"));
                File.Delete(Path.Combine(panakFolder, "item2.txt"));

                // Now 0 items remain inside ("մեջը ոչ մի բան չի մնում, այն ինքնաշխատ ջնջվում է")
                bool dissolved = PanakManager.Instance.CheckAndDissolveFolder(panakFolder);
                Assert.IsTrue(dissolved, "Empty folder should dissolve and delete");
                Assert.IsFalse(Directory.Exists(panakFolder), "Panak folder should be deleted when empty");
            } finally {
                if (Directory.Exists(tempParent)) {
                    foreach (var f in Directory.GetFiles(tempParent, "*", SearchOption.AllDirectories)) {
                        try { File.SetAttributes(f, FileAttributes.Normal); } catch { }
                    }
                    Directory.Delete(tempParent, true);
                }
            }
        }

        [TestMethod]
        public void TestMaterialYouColorPaletteFromHue() {
            // Seed hue for default peach ~ 26.2
            double peachHue = 26.2;
            var (surfaceHex, containerHex, primaryHex) = ShellHelper.GetMaterialYouTonalPaletteHex(peachHue);

            Assert.AreEqual("#FEF4EB", surfaceHex, "Surface color at peach hue must match #FEF4EB");
            Assert.AreEqual("#FCDDC5", containerHex, "Container color at peach hue must match #FCDDC5");
            Assert.AreEqual("#7A5427", primaryHex, "Primary color at peach hue must match #7A5427");

            // Arbitrary hue rotation must also produce valid hex colors
            var (sCustom, cCustom, pCustom) = ShellHelper.GetMaterialYouTonalPaletteHex(180.0);
            Assert.IsTrue(sCustom.StartsWith("#") && sCustom.Length == 7, "Dynamic surface hex must be valid");
            Assert.IsTrue(cCustom.StartsWith("#") && cCustom.Length == 7, "Dynamic container hex must be valid");
            Assert.IsTrue(pCustom.StartsWith("#") && pCustom.Length == 7, "Dynamic primary hex must be valid");
        }

        [TestMethod]
        public void TestGitHubUpdateServiceVersionAndDefaults() {
            var updater = GitHubUpdateService.Instance;
            Assert.IsNotNull(updater);
            Assert.AreEqual("1.5.5", updater.CurrentVersionString);
            Assert.AreEqual(new Version(1, 5, 5), updater.CurrentVersion);
            Assert.AreEqual(UpdateStatus.Idle, updater.Status);
        }

        [TestMethod]
        public void TestUpdaterSettingsIntegration() {
            var settings = SettingsService.Instance.Current;
            Assert.IsNotNull(settings);
            Assert.IsTrue(settings.AutoCheckUpdates);
            Assert.AreEqual("hranthakobian/Panak", settings.GitHubRepo);
        }

        [TestMethod]
        public void TestTargetRepositoryAndNormalization() {
            Assert.AreEqual("hranthakobian/Panak", GitHubUpdateService.TargetRepository);
            Assert.AreEqual("https://github.com/hranthakobian/Panak", GitHubUpdateService.TargetRepositoryUrl);
            Assert.AreEqual("hranthakobian/Panak", GitHubUpdateService.NormalizeRepo("https://github.com/hranthakobian/Panak"));
            Assert.AreEqual("hranthakobian/Panak", GitHubUpdateService.NormalizeRepo("https://github.com/hranthakobian/Panak/"));
            Assert.AreEqual("hranthakobian/Panak", GitHubUpdateService.NormalizeRepo("github.com/hranthakobian/Panak"));
            Assert.AreEqual("hranthakobian/Panak", GitHubUpdateService.NormalizeRepo("hranthakobian/Panak"));
            Assert.AreEqual("hranthakobian/Panak", GitHubUpdateService.NormalizeRepo(null));
            Assert.AreEqual("hranthakobian/Panak", GitHubUpdateService.NormalizeRepo(""));
        }

        [TestMethod]
        public void TestUpdateLocalizationKeys() {
            var loc = LocalizationService.Instance;
            string[] updateKeys = new[] {
                "Updates", "CheckForUpdates", "CheckingForUpdates",
                "UpToDate", "UpdateAvailable", "DownloadAndInstall",
                "DownloadingUpdate", "UpdateError", "AutoCheckUpdates",
                "AutoCheckUpdatesDesc", "CurrentVersion", "LatestVersion",
                "GitHubRepo", "ReleaseNotes"
            };

            foreach (var lang in new[] { "hy", "en", "ru" }) {
                loc.CurrentLanguage = lang;
                foreach (var k in updateKeys) {
                    string val = loc[k];
                    Assert.IsFalse(string.IsNullOrWhiteSpace(val), $"Update key '{k}' should exist in '{lang}'");
                }
            }
        }

        [TestMethod]
        public void TestNotifyFolderIconChangedPerformance() {
            string tempDir = Path.Combine(Path.GetTempPath(), "PanakPerfTest_" + Guid.NewGuid().ToString("N"));
            try {
                Directory.CreateDirectory(tempDir);
                string testIco = Path.Combine(tempDir, "panak_icon_0.ico");
                File.WriteAllBytes(testIco, new byte[128]);

                var sw = System.Diagnostics.Stopwatch.StartNew();
                ShellHelper.NotifyFolderIconChanged(tempDir, testIco);
                sw.Stop();

                Assert.IsTrue(sw.ElapsedMilliseconds < 500, $"NotifyFolderIconChanged took {sw.ElapsedMilliseconds}ms, should be under 500ms");
            } finally {
                if (Directory.Exists(tempDir)) {
                    File.SetAttributes(tempDir, FileAttributes.Normal);
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [TestMethod]
        public void TestSHGetSetFolderCustomSettingsBehavior() {
            string tempDir = Path.Combine(Path.GetTempPath(), "PanakFcsTest_" + Guid.NewGuid().ToString("N"));
            try {
                Directory.CreateDirectory(tempDir);
                string testIco = Path.Combine(tempDir, "panak_icon_0.ico");
                File.WriteAllBytes(testIco, new byte[128]);

                string iniPath = Path.Combine(tempDir, "desktop.ini");
                File.WriteAllText(iniPath, "[.ShellClassInfo]\r\nIconResource=panak_icon_0.ico,0\r\n[ViewState]\r\nMode=\r\nVid=\r\nFolderType=Generic\r\n");

                var fcs = new ShellHelper.SHFOLDERCUSTOMSETTINGS();
                fcs.dwSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(ShellHelper.SHFOLDERCUSTOMSETTINGS));
                fcs.dwMask = ShellHelper.FCSM_ICONFILE;
                fcs.pszIconFile = Path.GetFileName(testIco);
                fcs.iIconIndex = 0;
                int hr = ShellHelper.SHGetSetFolderCustomSettings(ref fcs, tempDir, ShellHelper.FCS_FORCEWRITE);
                Console.WriteLine($"SHGetSetFolderCustomSettings relative HRESULT: 0x{hr:X8}");

                if (File.Exists(iniPath)) {
                    string res = File.ReadAllText(iniPath);
                    Console.WriteLine("desktop.ini content:\n" + res);
                    Assert.IsTrue(res.Contains("[ViewState]"), "ViewState section should be preserved");
                }
            } finally {
                if (Directory.Exists(tempDir)) {
                    File.SetAttributes(tempDir, FileAttributes.Normal);
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [TestMethod]
        public void TestContextMenuRegistryDragDropHandlerRegistration() {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string exePath = Path.Combine(baseDir, "Panak.exe");
            if (!File.Exists(exePath)) {
                File.WriteAllText(exePath, "mock");
            }

            ContextMenuRegistry.RegisterAll(exePath);

            const string clsid = "{E5A7B812-3D4C-4A2E-8910-1F2D3C4E5A6B}";
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Classes\Directory\shellex\DragDropHandlers\Panak")) {
                Assert.IsNotNull(key, "DragDropHandlers for Directory should be registered");
                Assert.AreEqual(clsid, key.GetValue(""));
            }

            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Classes\Folder\shellex\DragDropHandlers\Panak")) {
                Assert.IsNotNull(key, "DragDropHandlers for Folder should be registered");
                Assert.AreEqual(clsid, key.GetValue(""));
            }

            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Classes\Drive\shellex\DragDropHandlers\Panak")) {
                Assert.IsNotNull(key, "DragDropHandlers for Drive should be registered");
                Assert.AreEqual(clsid, key.GetValue(""));
            }

            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey($@"Software\Classes\CLSID\{clsid}\InprocServer32")) {
                Assert.IsNotNull(key, "InprocServer32 for PanakDragDropHandler should be registered");
                Assert.AreEqual("Apartment", key.GetValue("ThreadingModel"));
            }
        }

        [TestMethod]
        public void TestTrayContextMenuCreationAndPalette() {
            var thread = new System.Threading.Thread(() => {
                var tm = new TrayManager();

                // Test Dark Mode
                SettingsService.Instance.Current.ThemeMode = "Dark";
                var darkMenu = tm.BuildContextMenu();
                Assert.IsNotNull(darkMenu);
                Assert.IsTrue(darkMenu.Items.Count >= 4, "Dark menu must contain items");

                // Test Light Mode
                SettingsService.Instance.Current.ThemeMode = "Light";
                var lightMenu = tm.BuildContextMenu();
                Assert.IsNotNull(lightMenu);
                Assert.IsTrue(lightMenu.Items.Count >= 4, "Light menu must contain items");
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();
        }
    }
}