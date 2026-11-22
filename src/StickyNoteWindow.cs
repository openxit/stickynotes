using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;
using FontFamily = System.Windows.Media.FontFamily;
using Orientation = System.Windows.Controls.Orientation;
using Point = System.Windows.Point;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace StickyNotes
{
    public class StickyNoteWindow : Window
    {
        private const double LineHeightPx = 28.0;
        private const double MarginX = 34.0;

        public StickyNoteData Data { get; private set; }
        public IntPtr Hwnd { get; private set; }

        private readonly Action _onSaveRequested;
        private readonly Action<StickyNoteWindow> _onUserHideRequested;

        private Border _paperBorder;
        private Border _adhesiveHeader;
        private Grid _ruledSurface;
        private Rectangle _marginLine;
        private StackPanel _rowsPanel;
        private StackPanel _themePickerBar;
        private TextBlock _appTitleText;
        private System.Windows.Controls.Image _appIconImage;
        private PaperTheme _theme;
        private bool _isResizing;
        private Point _resizeStartMouse;
        private double _resizeStartWidth;
        private double _resizeStartHeight;

        private readonly List<TodoRowControl> _rowControls = new List<TodoRowControl>();

        public StickyNoteWindow(
            StickyNoteData data,
            Action onSaveRequested,
            Action<StickyNoteWindow> onUserHideRequested)
        {
            Data = data;
            _onSaveRequested = onSaveRequested;
            _onUserHideRequested = onUserHideRequested;

            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;
            MinWidth = 230;
            MinHeight = 190;

            Left = data.Left;
            Top = data.Top;
            Width = data.Width;
            Height = data.Height;

            BuildUi();
            ApplyTheme(PaperThemes.Get(data.PaperTheme));
            RebuildRows();

            SourceInitialized += (s, e) =>
            {
                Hwnd = new WindowInteropHelper(this).Handle;
            };

            LocationChanged += (s, e) =>
            {
                if (IsLoaded && WindowState == WindowState.Normal)
                {
                    Data.Left = Left;
                    Data.Top = Top;
                    _onSaveRequested();
                }
            };
        }

        private void BuildUi()
        {
            var outerGrid = new Grid { Margin = new Thickness(12) };

            _paperBorder = new Border
            {
                CornerRadius = new CornerRadius(2),
                BorderThickness = new Thickness(1),
                SnapsToDevicePixels = true,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 18,
                    ShadowDepth = 5,
                    Direction = 275,
                    Opacity = 0.26
                }
            };

            var rootLayout = new Grid();
            rootLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
            rootLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // --- Row 0: Adhesive Header Strip ---
            _adhesiveHeader = new Border
            {
                BorderThickness = new Thickness(0, 0, 0, 1),
                Cursor = Cursors.SizeAll,
                Padding = new Thickness(8, 0, 6, 0)
            };
            _adhesiveHeader.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ButtonState == MouseButtonState.Pressed && e.ClickCount == 1)
                {
                    DragMove();
                }
            };

            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var appBadge = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            _appIconImage = new System.Windows.Controls.Image
            {
                Width = 14,
                Height = 14,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            LoadAppIcon();
            appBadge.Children.Add(_appIconImage);

            _appTitleText = new TextBlock
            {
                Text = string.IsNullOrEmpty(Data.AppName) ? Data.ProcessKey : Data.AppName,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            appBadge.Children.Add(_appTitleText);
            Grid.SetColumn(appBadge, 0);
            headerGrid.Children.Add(appBadge);

            // Header action buttons
            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            var addTodoBtn = CreateHeaderButton("☑", "Add checkbox todo", () => AddRowAndFocus(Data.Items.Count, true));
            var addNoteBtn = CreateHeaderButton("＋", "Add plain line", () => AddRowAndFocus(Data.Items.Count, false));
            var themeBtn = CreateHeaderButton("●", "Change paper color", ToggleThemePicker);
            var hideBtn = CreateHeaderButton("✕", "Hide note (press hotkey to restore)", () =>
            {
                Data.UserHidden = true;
                _onSaveRequested();
                _onUserHideRequested(this);
            });

            actions.Children.Add(addTodoBtn);
            actions.Children.Add(addNoteBtn);
            actions.Children.Add(themeBtn);
            actions.Children.Add(hideBtn);

            Grid.SetColumn(actions, 1);
            headerGrid.Children.Add(actions);

            _adhesiveHeader.Child = headerGrid;
            Grid.SetRow(_adhesiveHeader, 0);
            rootLayout.Children.Add(_adhesiveHeader);

            // --- Row 1: Ruled Paper Body ---
            _ruledSurface = new Grid { ClipToBounds = true };

            // Vertical coral margin line
            _marginLine = new Rectangle
            {
                Width = 1,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(MarginX, 0, 0, 0),
                IsHitTestVisible = false
            };
            _ruledSurface.Children.Add(_marginLine);

            // Scrollable lines
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Background = Brushes.Transparent
            };

            var bodyContainer = new Grid { Background = Brushes.Transparent };
            bodyContainer.MouseLeftButtonDown += (s, e) =>
            {
                if (e.OriginalSource == bodyContainer || e.OriginalSource == _ruledSurface)
                {
                    FocusOrAppendLine();
                }
            };

            _rowsPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Top
            };
            bodyContainer.Children.Add(_rowsPanel);
            scroll.Content = bodyContainer;
            _ruledSurface.Children.Add(scroll);

            // Inline theme picker bar (floating at top-right of paper when toggled)
            _themePickerBar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 4, 6, 0),
                Visibility = Visibility.Collapsed
            };
            foreach (var kv in PaperThemes.All)
            {
                var themeName = kv.Key;
                var swatch = new Border
                {
                    Width = 16,
                    Height = 16,
                    CornerRadius = new CornerRadius(8),
                    Background = new SolidColorBrush(kv.Value.SwatchColor),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(110, 0, 0, 0)),
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(3, 0, 0, 0),
                    Cursor = Cursors.Hand,
                    ToolTip = themeName
                };
                swatch.MouseLeftButtonDown += (s, e) =>
                {
                    e.Handled = true;
                    Data.PaperTheme = themeName;
                    ApplyTheme(PaperThemes.Get(themeName));
                    _themePickerBar.Visibility = Visibility.Collapsed;
                    _onSaveRequested();
                };
                _themePickerBar.Children.Add(swatch);
            }
            _ruledSurface.Children.Add(_themePickerBar);

            // Bottom-right subtle resize grip
            var resizeGrip = CreateResizeGrip();
            _ruledSurface.Children.Add(resizeGrip);

            Grid.SetRow(_ruledSurface, 1);
            rootLayout.Children.Add(_ruledSurface);

            _paperBorder.Child = rootLayout;
            outerGrid.Children.Add(_paperBorder);
            Content = outerGrid;
        }

        private Button CreateHeaderButton(string glyph, string tooltip, Action onClick)
        {
            var btn = new Button
            {
                Content = glyph,
                ToolTip = tooltip,
                Width = 22,
                Height = 22,
                FontSize = 11,
                FontFamily = new FontFamily("Segoe UI Symbol"),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Opacity = 0.62,
                Padding = new Thickness(0),
                Focusable = false
            };
            btn.MouseEnter += (s, e) => btn.Opacity = 1.0;
            btn.MouseLeave += (s, e) => btn.Opacity = 0.62;
            btn.Click += (s, e) => onClick();
            return btn;
        }

        private FrameworkElement CreateResizeGrip()
        {
            var grip = new Grid
            {
                Width = 16,
                Height = 16,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Background = Brushes.Transparent,
                Cursor = Cursors.SizeNWSE
            };

            var canvas = new Canvas { Width = 16, Height = 16, IsHitTestVisible = false, Opacity = 0.35 };
            var lineBrush = new SolidColorBrush(Color.FromRgb(0x44, 0x3D, 0x2A));
            canvas.Children.Add(new Line { X1 = 6, Y1 = 13, X2 = 13, Y2 = 6, Stroke = lineBrush, StrokeThickness = 1 });
            canvas.Children.Add(new Line { X1 = 10, Y1 = 13, X2 = 13, Y2 = 10, Stroke = lineBrush, StrokeThickness = 1 });
            grip.Children.Add(canvas);

            grip.MouseLeftButtonDown += (s, e) =>
            {
                _isResizing = true;
                _resizeStartMouse = PointToScreen(e.GetPosition(this));
                _resizeStartWidth = Width;
                _resizeStartHeight = Height;
                grip.CaptureMouse();
                e.Handled = true;
            };

            grip.MouseMove += (s, e) =>
            {
                if (!_isResizing) return;
                Point current = PointToScreen(e.GetPosition(this));
                double dx = current.X - _resizeStartMouse.X;
                double dy = current.Y - _resizeStartMouse.Y;
                // Convert physical pixels to DIPs
                var source = PresentationSource.FromVisual(this);
                double dpiScaleX = source != null && source.CompositionTarget != null ? source.CompositionTarget.TransformToDevice.M11 : 1.0;
                double dpiScaleY = source != null && source.CompositionTarget != null ? source.CompositionTarget.TransformToDevice.M22 : 1.0;

                Width = Math.Max(MinWidth, _resizeStartWidth + (dx / dpiScaleX));
                Height = Math.Max(MinHeight, _resizeStartHeight + (dy / dpiScaleY));
            };

            grip.MouseLeftButtonUp += (s, e) =>
            {
                if (_isResizing)
                {
                    _isResizing = false;
                    grip.ReleaseMouseCapture();
                    Data.Width = Width;
                    Data.Height = Height;
                    _onSaveRequested();
                }
            };

            return grip;
        }

        private void ToggleThemePicker()
        {
            _themePickerBar.Visibility = _themePickerBar.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void LoadAppIcon()
        {
            try
            {
                using (Icon icon = Win32.TryExtractIcon(Data.ExePath))
                {
                    if (icon != null)
                    {
                        var bmpSource = Imaging.CreateBitmapSourceFromHIcon(
                            icon.Handle,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromWidthAndHeight(16, 16));
                        _appIconImage.Source = bmpSource;
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning("LoadAppIcon failed for {0}: {1}", Data.ExePath, ex.Message);
            }
            _appIconImage.Visibility = Visibility.Collapsed;
        }

        public void ApplyTheme(PaperTheme theme)
        {
            _theme = theme;

            _paperBorder.Background = new LinearGradientBrush(theme.PaperTop, theme.PaperBottom, 90);
            _paperBorder.BorderBrush = new SolidColorBrush(theme.BorderColor);

            _adhesiveHeader.Background = new LinearGradientBrush(theme.AdhesiveTop, theme.AdhesiveBottom, 90);
            _adhesiveHeader.BorderBrush = new SolidColorBrush(theme.BorderColor);

            _appTitleText.Foreground = new SolidColorBrush(theme.MutedInkColor);
            _marginLine.Fill = new SolidColorBrush(theme.MarginLineColor);

            // Ruled horizontal lines repeating every 28px
            var lineGeo = new GeometryGroup();
            lineGeo.Children.Add(new LineGeometry(new Point(0, LineHeightPx - 0.5), new Point(2000, LineHeightPx - 0.5)));
            var drawing = new GeometryDrawing
            {
                Geometry = lineGeo,
                Pen = new System.Windows.Media.Pen(new SolidColorBrush(theme.RuledLineColor), 1.0)
            };
            var ruledBrush = new DrawingBrush(drawing)
            {
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, 2000, LineHeightPx),
                ViewportUnits = BrushMappingMode.Absolute,
                AlignmentY = AlignmentY.Top,
                Stretch = Stretch.None
            };
            _ruledSurface.Background = ruledBrush;

            foreach (var row in _rowControls)
            {
                row.ApplyTheme(theme);
            }
        }

        private void RebuildRows()
        {
            _rowsPanel.Children.Clear();
            _rowControls.Clear();

            if (Data.Items.Count == 0)
            {
                Data.Items.Add(new TodoItemData { HasCheckbox = true, Text = "" });
            }

            for (int i = 0; i < Data.Items.Count; i++)
            {
                var row = CreateRowControl(Data.Items[i]);
                _rowControls.Add(row);
                _rowsPanel.Children.Add(row);
            }
        }

        private TodoRowControl CreateRowControl(TodoItemData item)
        {
            var ctrl = new TodoRowControl(
                item,
                _theme,
                onChanged: () => _onSaveRequested(),
                onEnterPressed: (sender) =>
                {
                    int idx = _rowControls.IndexOf(sender);
                    if (idx < 0) return;

                    if (sender.Item.HasCheckbox && string.IsNullOrWhiteSpace(sender.Item.Text))
                    {
                        // Pressing Enter on an empty checkbox line converts it to a plain line
                        sender.SetCheckbox(false);
                        _onSaveRequested();
                    }
                    else
                    {
                        AddRowAndFocus(idx + 1, sender.Item.HasCheckbox);
                    }
                },
                onBackspaceEmpty: (sender) =>
                {
                    int idx = _rowControls.IndexOf(sender);
                    if (idx >= 0 && _rowControls.Count > 1)
                    {
                        RemoveRow(idx);
                        int focusIdx = Math.Max(0, idx - 1);
                        _rowControls[focusIdx].FocusEnd();
                    }
                },
                onNavigate: (sender, delta) =>
                {
                    int idx = _rowControls.IndexOf(sender);
                    int target = idx + delta;
                    if (target >= 0 && target < _rowControls.Count)
                    {
                        _rowControls[target].FocusEnd();
                    }
                },
                onDeleteRequested: (sender) =>
                {
                    int idx = _rowControls.IndexOf(sender);
                    if (idx >= 0)
                    {
                        if (_rowControls.Count == 1)
                        {
                            sender.ClearText();
                        }
                        else
                        {
                            RemoveRow(idx);
                        }
                    }
                });

            return ctrl;
        }

        public void AddRowAndFocus(int index, bool hasCheckbox)
        {
            var newItem = new TodoItemData { HasCheckbox = hasCheckbox, Text = "" };
            index = Math.Max(0, Math.Min(index, Data.Items.Count));
            Data.Items.Insert(index, newItem);

            var ctrl = CreateRowControl(newItem);
            _rowControls.Insert(index, ctrl);
            _rowsPanel.Children.Insert(index, ctrl);

            _onSaveRequested();
            Dispatcher.BeginInvoke(new Action(() => ctrl.FocusEnd()), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void RemoveRow(int index)
        {
            Data.Items.RemoveAt(index);
            _rowControls.RemoveAt(index);
            _rowsPanel.Children.RemoveAt(index);
            _onSaveRequested();
        }

        private void FocusOrAppendLine()
        {
            if (_rowControls.Count > 0)
            {
                var last = _rowControls[_rowControls.Count - 1];
                if (string.IsNullOrEmpty(last.Item.Text))
                {
                    last.FocusEnd();
                    return;
                }
                AddRowAndFocus(_rowControls.Count, last.Item.HasCheckbox);
            }
            else
            {
                AddRowAndFocus(0, true);
            }
        }

        public void FocusFirstInput()
        {
            if (_rowControls.Count > 0)
            {
                _rowControls[0].FocusEnd();
            }
        }
    }

    internal class TodoRowControl : Grid
    {
        public TodoItemData Item { get; private set; }

        private readonly Action _onChanged;
        private readonly Action<TodoRowControl> _onEnterPressed;
        private readonly Action<TodoRowControl> _onBackspaceEmpty;
        private readonly Action<TodoRowControl, int> _onNavigate;
        private readonly Action<TodoRowControl> _onDeleteRequested;

        private Border _checkboxHitArea;
        private Border _checkboxBox;
        private Path _checkmarkPath;
        private TextBox _textBox;
        private StackPanel _hoverActions;
        private Button _toggleTypeBtn;
        private Button _deleteBtn;
        private PaperTheme _theme;
        private bool _suppressTextEvent;

        public TodoRowControl(
            TodoItemData item,
            PaperTheme theme,
            Action onChanged,
            Action<TodoRowControl> onEnterPressed,
            Action<TodoRowControl> onBackspaceEmpty,
            Action<TodoRowControl, int> onNavigate,
            Action<TodoRowControl> onDeleteRequested)
        {
            Item = item;
            _theme = theme;
            _onChanged = onChanged;
            _onEnterPressed = onEnterPressed;
            _onBackspaceEmpty = onBackspaceEmpty;
            _onNavigate = onNavigate;
            _onDeleteRequested = onDeleteRequested;

            Height = 28;
            Background = Brushes.Transparent;

            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) }); // left gutter
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // text
            ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // hover controls

            BuildRow();
            ApplyTheme(theme);
            SyncVisualState();

            MouseEnter += (s, e) => _hoverActions.Visibility = Visibility.Visible;
            MouseLeave += (s, e) => _hoverActions.Visibility = Visibility.Collapsed;
        }

        private void BuildRow()
        {
            // Left gutter checkbox
            _checkboxHitArea = new Border
            {
                Width = 34,
                Height = 28,
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand
            };
            _checkboxHitArea.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                if (Item.HasCheckbox)
                {
                    Item.IsChecked = !Item.IsChecked;
                }
                else
                {
                    Item.HasCheckbox = true;
                    Item.IsChecked = false;
                }
                SyncVisualState();
                _onChanged();
            };

            _checkboxBox = new Border
            {
                Width = 14,
                Height = 14,
                CornerRadius = new CornerRadius(2),
                BorderThickness = new Thickness(1.3),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            _checkmarkPath = new Path
            {
                Data = Geometry.Parse("M 2,6.5 L 5,9.5 L 10,2.5"),
                StrokeThickness = 1.7,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Visibility = Visibility.Collapsed
            };
            _checkboxBox.Child = _checkmarkPath;
            _checkboxHitArea.Child = _checkboxBox;
            SetColumn(_checkboxHitArea, 0);
            Children.Add(_checkboxHitArea);

            // Inline TextBox
            _textBox = new TextBox
            {
                Text = Item.Text ?? string.Empty,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(8, 0, 4, 2),
                AcceptsReturn = false
            };

            _textBox.TextChanged += (s, e) =>
            {
                if (_suppressTextEvent) return;
                string txt = _textBox.Text;

                // Shortcut: typing "- [ ] " or "[] " converts plain line into a checkbox todo
                if (!Item.HasCheckbox && (txt.StartsWith("- [ ] ") || txt.StartsWith("[] ")))
                {
                    int prefixLen = txt.StartsWith("- [ ] ") ? 6 : 3;
                    _suppressTextEvent = true;
                    Item.HasCheckbox = true;
                    Item.IsChecked = false;
                    _textBox.Text = txt.Substring(prefixLen);
                    _textBox.CaretIndex = _textBox.Text.Length;
                    _suppressTextEvent = false;
                    SyncVisualState();
                }

                Item.Text = _textBox.Text;
                _onChanged();
            };

            _textBox.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    e.Handled = true;
                    _onEnterPressed(this);
                }
                else if (e.Key == Key.Back && string.IsNullOrEmpty(_textBox.Text))
                {
                    e.Handled = true;
                    _onBackspaceEmpty(this);
                }
                else if (e.Key == Key.Up)
                {
                    e.Handled = true;
                    _onNavigate(this, -1);
                }
                else if (e.Key == Key.Down)
                {
                    e.Handled = true;
                    _onNavigate(this, 1);
                }
                else if ((Keyboard.Modifiers == ModifierKeys.Control) && (e.Key == Key.T || e.Key == Key.Space))
                {
                    e.Handled = true;
                    SetCheckbox(!Item.HasCheckbox);
                    _onChanged();
                }
            };

            SetColumn(_textBox, 1);
            Children.Add(_textBox);

            // Hover actions on right
            _hoverActions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 6, 0),
                Visibility = Visibility.Collapsed
            };

            _toggleTypeBtn = new Button
            {
                Content = "☐",
                ToolTip = "Toggle checkbox (Ctrl+T)",
                Width = 18,
                Height = 18,
                FontSize = 11,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Padding = new Thickness(0),
                Opacity = 0.55,
                Focusable = false
            };
            _toggleTypeBtn.Click += (s, e) =>
            {
                SetCheckbox(!Item.HasCheckbox);
                _onChanged();
            };

            _deleteBtn = new Button
            {
                Content = "×",
                ToolTip = "Delete line",
                Width = 18,
                Height = 18,
                FontSize = 13,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Padding = new Thickness(0),
                Opacity = 0.55,
                Focusable = false
            };
            _deleteBtn.Click += (s, e) => _onDeleteRequested(this);

            _hoverActions.Children.Add(_toggleTypeBtn);
            _hoverActions.Children.Add(_deleteBtn);
            SetColumn(_hoverActions, 2);
            Children.Add(_hoverActions);
        }

        public void SetCheckbox(bool hasCheckbox)
        {
            Item.HasCheckbox = hasCheckbox;
            if (!hasCheckbox) Item.IsChecked = false;
            SyncVisualState();
        }

        public void ClearText()
        {
            _textBox.Text = string.Empty;
            Item.Text = string.Empty;
            Item.IsChecked = false;
            SyncVisualState();
            _onChanged();
        }

        public void FocusEnd()
        {
            _textBox.Focus();
            _textBox.CaretIndex = _textBox.Text.Length;
        }

        public void ApplyTheme(PaperTheme theme)
        {
            _theme = theme;
            if (theme == null) return;
            _checkmarkPath.Stroke = new SolidColorBrush(theme.InkColor);
            _toggleTypeBtn.Foreground = new SolidColorBrush(theme.MutedInkColor);
            _deleteBtn.Foreground = new SolidColorBrush(theme.MutedInkColor);
            SyncVisualState();
        }

        private void SyncVisualState()
        {
            if (_theme == null) return;

            if (Item.HasCheckbox)
            {
                _checkboxBox.Visibility = Visibility.Visible;
                _checkboxBox.BorderBrush = new SolidColorBrush(Item.IsChecked ? _theme.MutedInkColor : _theme.InkColor);
                _checkboxBox.Background = Item.IsChecked
                    ? new SolidColorBrush(Color.FromArgb(24, _theme.InkColor.R, _theme.InkColor.G, _theme.InkColor.B))
                    : Brushes.Transparent;
                _checkmarkPath.Visibility = Item.IsChecked ? Visibility.Visible : Visibility.Collapsed;

                _textBox.TextDecorations = Item.IsChecked ? TextDecorations.Strikethrough : null;
                _textBox.Foreground = new SolidColorBrush(Item.IsChecked ? _theme.MutedInkColor : _theme.InkColor);
                _textBox.Opacity = Item.IsChecked ? 0.65 : 1.0;
                _toggleTypeBtn.Content = "¶";
            }
            else
            {
                _checkboxBox.Visibility = Visibility.Collapsed;
                _textBox.TextDecorations = null;
                _textBox.Foreground = new SolidColorBrush(_theme.InkColor);
                _textBox.Opacity = 1.0;
                _toggleTypeBtn.Content = "☐";
            }
        }
    }
}
