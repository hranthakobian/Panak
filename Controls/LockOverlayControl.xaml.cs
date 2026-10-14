using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Panak.Models;
using Panak.Services;

namespace Panak.Controls {
    public partial class LockOverlayControl : UserControl {
        public event Action? Unlocked;
        public event Action? LockRemoved;
        public event Action? Cancelled;

        private string _targetFolderPath = string.Empty;
        private bool _isSettingsMode = false;
        private bool _isRemoveLockMode = false;
        private FolderLockInfo? _folderLockInfo;
        private string _currentPin = string.Empty;
        private readonly List<int> _currentPattern = new();
        private string _currentFolderColor = "#EA580C";
        private bool _isDarkTheme = true;
        private Brush _primaryBrush = new SolidColorBrush(Color.FromRgb(234, 88, 12));

        public LockOverlayControl() {
            InitializeComponent();
        }

        public void ApplyFolderTheme(string hexColor, bool isDarkTheme) {
            if (string.IsNullOrEmpty(hexColor)) {
                hexColor = "#EA580C";
            }
            _currentFolderColor = hexColor;
            _isDarkTheme = isDarkTheme;

            try {
                double hue = ShellHelper.GetHueFromHex(hexColor);
                var (_, _, primaryHex) = ShellHelper.GetMaterialYouTonalPaletteHex(hue);

                var primaryColor = (Color)ColorConverter.ConvertFromString(primaryHex);
                _primaryBrush = new SolidColorBrush(primaryColor);

                var textFg = isDarkTheme
                    ? new SolidColorBrush(Colors.White)
                    : new SolidColorBrush(Colors.Black);
                var subtextFg = isDarkTheme
                    ? new SolidColorBrush(Colors.White)
                    : new SolidColorBrush(Colors.Black);

                // Container background tinted with folder color
                Color containerColor = isDarkTheme
                    ? Color.FromArgb(50, primaryColor.R, primaryColor.G, primaryColor.B)
                    : Color.FromArgb(32, primaryColor.R, primaryColor.G, primaryColor.B);

                Color hoverColor = isDarkTheme
                    ? Color.FromArgb(85, primaryColor.R, primaryColor.G, primaryColor.B)
                    : Color.FromArgb(60, primaryColor.R, primaryColor.G, primaryColor.B);

                Color pressedColor = isDarkTheme
                    ? Color.FromArgb(125, primaryColor.R, primaryColor.G, primaryColor.B)
                    : Color.FromArgb(90, primaryColor.R, primaryColor.G, primaryColor.B);

                Color borderColor = isDarkTheme
                    ? Color.FromArgb(80, primaryColor.R, primaryColor.G, primaryColor.B)
                    : Color.FromArgb(60, primaryColor.R, primaryColor.G, primaryColor.B);

                Color badgeBg = Color.FromArgb(45, primaryColor.R, primaryColor.G, primaryColor.B);

                Resources["LockPrimaryBrush"] = _primaryBrush;
                Resources["LockOnPrimaryBrush"] = new SolidColorBrush(Colors.White);
                Resources["LockContainerBrush"] = new SolidColorBrush(containerColor);
                Resources["LockOnContainerBrush"] = textFg;
                Resources["LockBtnHoverBrush"] = new SolidColorBrush(hoverColor);
                Resources["LockBtnPressedBrush"] = new SolidColorBrush(pressedColor);
                Resources["LockBorderBrush"] = new SolidColorBrush(borderColor);
                Resources["LockBadgeBgBrush"] = new SolidColorBrush(badgeBg);
                Resources["LockTextBrush"] = textFg;
                Resources["LockSubtextBrush"] = subtextFg;

                LockTitleText.Foreground = textFg;
                LockSubtitleText.Foreground = subtextFg;

                if (PasswordInputContainer != null) {
                    PasswordInputContainer.Background = new SolidColorBrush(containerColor);
                    PasswordInputContainer.BorderBrush = new SolidColorBrush(borderColor);
                }

                // Refresh pattern graphics if initialized
                if (_patternPolyline != null) {
                    _patternPolyline.Stroke = _primaryBrush;
                }
                if (_patternActiveLine != null) {
                    _patternActiveLine.Stroke = new SolidColorBrush(Color.FromArgb(180, primaryColor.R, primaryColor.G, primaryColor.B));
                }
                for (int i = 0; i < 9; i++) {
                    if (_patternDotRings[i] != null) {
                        _patternDotRings[i].Stroke = new SolidColorBrush(Color.FromArgb(160, primaryColor.R, primaryColor.G, primaryColor.B));
                    }
                }
                UpdatePinDots();
            } catch { }
        }

        public void ApplyThemeContrast(bool isDarkTheme) {
            ApplyFolderTheme(_currentFolderColor, isDarkTheme);
        }

        public void SetupForFolder(string folderPath) {
            _targetFolderPath = folderPath;
            _isSettingsMode = false;
            _isRemoveLockMode = false;
            _folderLockInfo = FolderLockService.Instance.GetFolderLockInfo(folderPath);

            try {
                if (Directory.Exists(folderPath)) {
                    var data = PanakFolderData.Load(folderPath);
                    if (!string.IsNullOrEmpty(data.Color)) {
                        _currentFolderColor = data.Color;
                    }
                }
            } catch { }

            ApplyFolderTheme(_currentFolderColor, _isDarkTheme);
            if (RemoveLockCancelBtn != null) RemoveLockCancelBtn.Visibility = Visibility.Collapsed;
            SetupUI(_folderLockInfo?.LockType ?? "Password");
        }

        public void SetupForRemoveLock(string folderPath) {
            _targetFolderPath = folderPath;
            _isSettingsMode = false;
            _isRemoveLockMode = true;
            _folderLockInfo = FolderLockService.Instance.GetFolderLockInfo(folderPath);

            try {
                if (Directory.Exists(folderPath)) {
                    var data = PanakFolderData.Load(folderPath);
                    if (!string.IsNullOrEmpty(data.Color)) {
                        _currentFolderColor = data.Color;
                    }
                }
            } catch { }

            ApplyFolderTheme(_currentFolderColor, _isDarkTheme);
            SetupUI(_folderLockInfo?.LockType ?? "Password");

            LockIconText.Text = "🔓";
            LockTitleText.Text = "Հեռացնել փականը";
            LockSubtitleText.Text = "Մուտքագրեք ընթացիկ գաղտնաբառը կամ կոդը";
            if (RemoveLockCancelBtn != null) {
                RemoveLockCancelBtn.Visibility = Visibility.Visible;
            }
        }

        public bool IsRemoveLockMode => _isRemoveLockMode;

        public void CancelRemoveLock() {
            _isRemoveLockMode = false;
            ErrorBadge.Visibility = Visibility.Collapsed;
            Cancelled?.Invoke();
        }

        private void RemoveLockCancelBtn_Click(object sender, RoutedEventArgs e) {
            CancelRemoveLock();
        }

        public void SetupForSettings() {
            _targetFolderPath = string.Empty;
            _isSettingsMode = true;
            _isRemoveLockMode = false;
            _folderLockInfo = null;
            _currentFolderColor = "#EA580C";
            ApplyFolderTheme(_currentFolderColor, _isDarkTheme);
            if (RemoveLockCancelBtn != null) RemoveLockCancelBtn.Visibility = Visibility.Collapsed;
            SetupUI(FolderLockService.Instance.SettingsLockType);
        }

        private void SetupUI(string lockType) {
            _currentPin = string.Empty;
            _currentPattern.Clear();
            ErrorBadge.Visibility = Visibility.Collapsed;

            PinContainer.Visibility = Visibility.Collapsed;
            PatternContainer.Visibility = Visibility.Collapsed;
            PasswordContainer.Visibility = Visibility.Collapsed;

            var loc = LocalizationService.Instance;

            switch (lockType) {
                case "PIN":
                    PinContainer.Visibility = Visibility.Visible;
                    LockTitleText.Text = loc["EnterPinToUnlock"] ?? "Մուտքագրեք PIN կոդը";
                    LockSubtitleText.Text = loc["PinDesc"] ?? "Հավաքեք թվային կոդը";
                    UpdatePinDots();
                    break;
                case "Pattern":
                    PatternContainer.Visibility = Visibility.Visible;
                    LockTitleText.Text = loc["DrawPatternToUnlock"] ?? "Գծեք նախշը";
                    LockSubtitleText.Text = loc["PatternDesc"] ?? "Քաշեք մկնիկով՝ նախշը միացնելու համար";
                    InitPatternCanvas();
                    ResetPatternDots();
                    break;
                default:
                    PasswordContainer.Visibility = Visibility.Visible;
                    LockTitleText.Text = loc["EnterPasswordToUnlock"] ?? "Մուտքագրեք գաղտնաբառը";
                    LockSubtitleText.Text = loc["PasswordDesc"] ?? "Հավաքեք պանակի գաղտնաբառը";
                    PasswordInputBox.Password = string.Empty;
                    PasswordInputBox.Focus();
                    break;
            }
        }

        // --- PIN Handlers ---
        private void NumpadBtn_Click(object sender, RoutedEventArgs e) {
            if (sender is Button b && b.Content is string digit) {
                if (_currentPin.Length < 8) {
                    _currentPin += digit;
                    UpdatePinDots();
                    CheckPinComplete();
                }
            }
        }

        private void PinClear_Click(object sender, RoutedEventArgs e) {
            _currentPin = string.Empty;
            UpdatePinDots();
            ErrorBadge.Visibility = Visibility.Collapsed;
        }

        private void PinBackspace_Click(object sender, RoutedEventArgs e) {
            if (_currentPin.Length > 0) {
                _currentPin = _currentPin.Substring(0, _currentPin.Length - 1);
                UpdatePinDots();
                ErrorBadge.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdatePinDots() {
            if (PinDotsPanel == null) return;
            PinDotsPanel.Children.Clear();
            int count = Math.Max(4, _currentPin.Length);
            var activeFill = Resources["LockPrimaryBrush"] as Brush ?? _primaryBrush;
            var inactiveFill = Resources["LockContainerBrush"] as Brush ?? new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));

            for (int i = 0; i < count; i++) {
                var dot = new Ellipse {
                    Width = 13,
                    Height = 13,
                    Margin = new Thickness(5, 0, 5, 0),
                    Fill = (i < _currentPin.Length) ? activeFill : inactiveFill
                };
                PinDotsPanel.Children.Add(dot);
            }
        }

        private void CheckPinComplete() {
            if (_currentPin.Length >= 4) {
                ValidateCredential(_currentPin);
            }
        }

        // --- Drag-to-Draw Pattern Handlers ---
        private bool _isDrawingPattern = false;
        private readonly Point[] _patternDotPositions = new Point[9];
        private readonly Ellipse[] _patternDotElements = new Ellipse[9];
        private readonly Ellipse[] _patternDotRings = new Ellipse[9];
        private Polyline? _patternPolyline;
        private Line? _patternActiveLine;

        private void InitPatternCanvas() {
            if (PatternCanvas == null) return;
            PatternCanvas.Children.Clear();

            var activeStroke = Resources["LockPrimaryBrush"] as Brush ?? _primaryBrush;
            var activeLineStroke = Resources["LockPrimaryBrush"] is SolidColorBrush sb
                ? new SolidColorBrush(Color.FromArgb(180, sb.Color.R, sb.Color.G, sb.Color.B))
                : new SolidColorBrush(Color.FromArgb(180, 234, 88, 12));

            _patternPolyline = new Polyline {
                Stroke = activeStroke,
                StrokeThickness = 3.5,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round
            };
            PatternCanvas.Children.Add(_patternPolyline);

            _patternActiveLine = new Line {
                Stroke = activeLineStroke,
                StrokeThickness = 3,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Visibility = Visibility.Collapsed
            };
            PatternCanvas.Children.Add(_patternActiveLine);

            double w = 180;
            double h = 180;
            double stepX = w / 3.0;
            double stepY = h / 3.0;

            var ringStroke = Resources["LockPrimaryBrush"] is SolidColorBrush rsb
                ? new SolidColorBrush(Color.FromArgb(160, rsb.Color.R, rsb.Color.G, rsb.Color.B))
                : new SolidColorBrush(Color.FromArgb(160, 234, 88, 12));

            var dotFill = Resources["LockOnContainerBrush"] as Brush ?? new SolidColorBrush(Color.FromArgb(200, 255, 255, 255));

            for (int i = 0; i < 9; i++) {
                int row = i / 3;
                int col = i % 3;
                double cx = col * stepX + stepX / 2.0;
                double cy = row * stepY + stepY / 2.0;
                _patternDotPositions[i] = new Point(cx, cy);

                var ring = new Ellipse {
                    Width = 34,
                    Height = 34,
                    Stroke = ringStroke,
                    StrokeThickness = 2,
                    Visibility = Visibility.Collapsed
                };
                Canvas.SetLeft(ring, cx - 17);
                Canvas.SetTop(ring, cy - 17);
                PatternCanvas.Children.Add(ring);
                _patternDotRings[i] = ring;

                var dot = new Ellipse {
                    Width = 14,
                    Height = 14,
                    Fill = dotFill
                };
                Canvas.SetLeft(dot, cx - 7);
                Canvas.SetTop(dot, cy - 7);
                PatternCanvas.Children.Add(dot);
                _patternDotElements[i] = dot;
            }
        }

        private void PatternCanvas_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            _isDrawingPattern = true;
            _currentPattern.Clear();
            ResetPatternDots();
            ErrorBadge.Visibility = Visibility.Collapsed;

            Point p = e.GetPosition(PatternCanvas);
            CheckHitDot(p);
            PatternCanvas.CaptureMouse();
        }

        private void PatternCanvas_MouseMove(object sender, MouseEventArgs e) {
            if (!_isDrawingPattern) return;
            Point p = e.GetPosition(PatternCanvas);
            CheckHitDot(p);

            if (_currentPattern.Count > 0 && _patternActiveLine != null) {
                int lastIndex = _currentPattern.Last() - 1;
                if (lastIndex >= 0 && lastIndex < 9) {
                    var lastDot = _patternDotPositions[lastIndex];
                    _patternActiveLine.X1 = lastDot.X;
                    _patternActiveLine.Y1 = lastDot.Y;
                    _patternActiveLine.X2 = p.X;
                    _patternActiveLine.Y2 = p.Y;
                    _patternActiveLine.Visibility = Visibility.Visible;
                }
            }
        }

        private void PatternCanvas_MouseUp(object sender, MouseButtonEventArgs e) {
            FinishDrawingPattern();
        }

        private void PatternCanvas_MouseLeave(object sender, MouseEventArgs e) {
            if (_isDrawingPattern && !PatternCanvas.IsMouseCaptured) {
                FinishDrawingPattern();
            }
        }

        private void FinishDrawingPattern() {
            if (!_isDrawingPattern) return;
            _isDrawingPattern = false;
            if (PatternCanvas.IsMouseCaptured) {
                PatternCanvas.ReleaseMouseCapture();
            }
            if (_patternActiveLine != null) {
                _patternActiveLine.Visibility = Visibility.Collapsed;
            }

            if (_currentPattern.Count >= 4) {
                string pattern1 = string.Join("-", _currentPattern);
                string pattern0 = string.Join("-", _currentPattern.Select(x => x - 1));
                ValidatePatternCredential(pattern1, pattern0);
            } else if (_currentPattern.Count > 0) {
                ShowError("Նախշը պետք է ունենա առնվազն 4 կետ");
                _currentPattern.Clear();
                ResetPatternDots();
            }
        }

        private static int GetIntermediateDot(int a, int b) {
            if (a > b) (a, b) = (b, a);
            return (a, b) switch {
                (1, 3) => 2,
                (4, 6) => 5,
                (7, 9) => 8,
                (1, 7) => 4,
                (2, 8) => 5,
                (3, 9) => 6,
                (1, 9) => 5,
                (3, 7) => 5,
                _ => 0
            };
        }

        private void SelectDot(int val) {
            if (_currentPattern.Contains(val) || val < 1 || val > 9) return;
            int idx = val - 1;
            _currentPattern.Add(val);
            HighlightDot(idx);
            _patternPolyline?.Points.Add(_patternDotPositions[idx]);
        }

        private void CheckHitDot(Point p) {
            double radiusSq = 22 * 22; // detection radius

            if (_currentPattern.Count == 0) {
                // First point - check direct hit
                for (int i = 0; i < 9; i++) {
                    int val = i + 1;
                    var dotPos = _patternDotPositions[i];
                    double dx = p.X - dotPos.X;
                    double dy = p.Y - dotPos.Y;
                    if (dx * dx + dy * dy <= radiusSq) {
                        SelectDot(val);
                        break;
                    }
                }
                return;
            }

            // User is dragging from the last selected dot to current mouse position p
            Point start = _patternDotPositions[_currentPattern.Last() - 1];
            Point end = p;

            double segDx = end.X - start.X;
            double segDy = end.Y - start.Y;
            double segLenSq = segDx * segDx + segDy * segDy;

            // Find all unselected dots crossed by the line segment [start, end]
            var crossedDots = new List<(int val, double t)>();

            for (int i = 0; i < 9; i++) {
                int val = i + 1;
                if (_currentPattern.Contains(val)) continue;

                var dotPos = _patternDotPositions[i];
                double distSq;
                double t;

                if (segLenSq < 0.001) {
                    double ddx = p.X - dotPos.X;
                    double ddy = p.Y - dotPos.Y;
                    distSq = ddx * ddx + ddy * ddy;
                    t = 0;
                } else {
                    t = ((dotPos.X - start.X) * segDx + (dotPos.Y - start.Y) * segDy) / segLenSq;
                    double tClamped = Math.Max(0, Math.Min(1, t));
                    double projX = start.X + tClamped * segDx;
                    double projY = start.Y + tClamped * segDy;
                    double ddx = dotPos.X - projX;
                    double ddy = dotPos.Y - projY;
                    distSq = ddx * ddx + ddy * ddy;
                }

                if (distSq <= radiusSq) {
                    crossedDots.Add((val, t));
                }
            }

            // Sort by t ascending so dots are selected in the order the line crosses them
            crossedDots.Sort((a, b) => a.t.CompareTo(b.t));

            foreach (var (val, _) in crossedDots) {
                if (_currentPattern.Contains(val)) continue;
                if (_currentPattern.Count > 0) {
                    int mid = GetIntermediateDot(_currentPattern.Last(), val);
                    if (mid > 0 && !_currentPattern.Contains(mid)) {
                        SelectDot(mid);
                    }
                }
                SelectDot(val);
            }
        }

        private void HighlightDot(int i) {
            if (i >= 0 && i < 9) {
                if (_patternDotElements[i] != null) {
                    _patternDotElements[i].Fill = Resources["LockPrimaryBrush"] as Brush ?? _primaryBrush;
                }
                if (_patternDotRings[i] != null) {
                    _patternDotRings[i].Visibility = Visibility.Visible;
                }
            }
        }

        private void PatternClear_Click(object sender, RoutedEventArgs e) {
            _currentPattern.Clear();
            ResetPatternDots();
            ErrorBadge.Visibility = Visibility.Collapsed;
        }

        private void ResetPatternDots() {
            if (_patternPolyline != null) {
                _patternPolyline.Points.Clear();
            }
            if (_patternActiveLine != null) {
                _patternActiveLine.Visibility = Visibility.Collapsed;
            }
            var dotFill = Resources["LockOnContainerBrush"] as Brush ?? new SolidColorBrush(Color.FromArgb(200, 255, 255, 255));
            for (int i = 0; i < 9; i++) {
                if (_patternDotElements[i] != null) {
                    _patternDotElements[i].Fill = dotFill;
                }
                if (_patternDotRings[i] != null) {
                    _patternDotRings[i].Visibility = Visibility.Collapsed;
                }
            }
        }

        // --- Password Handlers ---
        private void PasswordInputBox_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                ValidateCredential(PasswordInputBox.Password);
            }
        }

        private void PasswordInputBox_GotFocus(object sender, RoutedEventArgs e) {
            if (PasswordInputContainer != null) {
                PasswordInputContainer.BorderBrush = Resources["LockPrimaryBrush"] as Brush ?? _primaryBrush;
                PasswordInputContainer.BorderThickness = new Thickness(2);
            }
        }

        private void PasswordInputBox_LostFocus(object sender, RoutedEventArgs e) {
            if (PasswordInputContainer != null) {
                PasswordInputContainer.BorderBrush = Resources["LockBorderBrush"] as Brush ?? new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));
                PasswordInputContainer.BorderThickness = new Thickness(1.5);
            }
        }

        private void PasswordSubmit_Click(object sender, RoutedEventArgs e) {
            ValidateCredential(PasswordInputBox.Password);
        }

        // --- Credential Validation ---
        private void ValidateCredential(string input) {
            if (_isRemoveLockMode) {
                bool removed = FolderLockService.Instance.RemoveFolderLock(_targetFolderPath, input);
                if (removed) {
                    _isRemoveLockMode = false;
                    ErrorBadge.Visibility = Visibility.Collapsed;
                    LockRemoved?.Invoke();
                    Unlocked?.Invoke();
                    return;
                } else {
                    ShowError(LocalizationService.Instance["IncorrectCredential"] ?? "Սխալ գաղտնաբառ կամ կոդ");
                    _currentPin = string.Empty;
                    UpdatePinDots();
                    _currentPattern.Clear();
                    ResetPatternDots();
                    return;
                }
            }

            bool success = false;
            if (_isSettingsMode) {
                success = FolderLockService.Instance.VerifySettingsUnlock(input);
            } else if (!string.IsNullOrEmpty(_targetFolderPath)) {
                success = FolderLockService.Instance.UnlockFolderSession(_targetFolderPath, input);
            }

            if (success) {
                OnUnlockSuccess();
            } else {
                ShowError(LocalizationService.Instance["IncorrectCredential"] ?? "Սխալ գաղտնաբառ կամ կոդ");
                _currentPin = string.Empty;
                UpdatePinDots();
                _currentPattern.Clear();
                ResetPatternDots();
            }
        }

        private void ValidatePatternCredential(string pattern1, string pattern0) {
            if (_isRemoveLockMode) {
                bool removed = FolderLockService.Instance.RemoveFolderLock(_targetFolderPath, pattern1) ||
                               FolderLockService.Instance.RemoveFolderLock(_targetFolderPath, pattern0);
                if (removed) {
                    _isRemoveLockMode = false;
                    ErrorBadge.Visibility = Visibility.Collapsed;
                    LockRemoved?.Invoke();
                    Unlocked?.Invoke();
                    return;
                } else {
                    ShowError(LocalizationService.Instance["IncorrectCredential"] ?? "Սխալ գաղտնաբառ կամ կոդ");
                    _currentPattern.Clear();
                    ResetPatternDots();
                    return;
                }
            }

            bool success = false;
            if (_isSettingsMode) {
                success = FolderLockService.Instance.VerifySettingsUnlock(pattern1) ||
                          FolderLockService.Instance.VerifySettingsUnlock(pattern0);
            } else if (!string.IsNullOrEmpty(_targetFolderPath)) {
                success = FolderLockService.Instance.UnlockFolderSession(_targetFolderPath, pattern1) ||
                          FolderLockService.Instance.UnlockFolderSession(_targetFolderPath, pattern0);
            }

            if (success) {
                OnUnlockSuccess();
            } else {
                ShowError(LocalizationService.Instance["IncorrectCredential"] ?? "Սխալ գաղտնաբառ կամ կոդ");
                _currentPattern.Clear();
                ResetPatternDots();
            }
        }

        private void OnUnlockSuccess() {
            ErrorBadge.Visibility = Visibility.Collapsed;
            Unlocked?.Invoke();
        }

        private void ShowError(string msg) {
            ErrorText.Text = msg;
            ErrorBadge.Visibility = Visibility.Visible;
        }

        private void UserControl_KeyDown(object sender, KeyEventArgs e) {
            if (PinContainer.Visibility == Visibility.Visible) {
                if (e.Key >= Key.D0 && e.Key <= Key.D9) {
                    string d = (e.Key - Key.D0).ToString();
                    if (_currentPin.Length < 8) {
                        _currentPin += d;
                        UpdatePinDots();
                        CheckPinComplete();
                    }
                    e.Handled = true;
                } else if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9) {
                    string d = (e.Key - Key.NumPad0).ToString();
                    if (_currentPin.Length < 8) {
                        _currentPin += d;
                        UpdatePinDots();
                        CheckPinComplete();
                    }
                    e.Handled = true;
                } else if (e.Key == Key.Back) {
                    PinBackspace_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                }
            }
        }
    }
}
