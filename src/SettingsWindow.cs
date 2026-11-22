using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace StickyNotes
{
    public class SettingsWindow : Window
    {
        private readonly NotesStoreData _store;
        private readonly Action _onHotkeyChanged;
        private readonly Action _onSaveRequested;
        private readonly Action<StickyNoteData> _onDeleteNote;
        private readonly Action<StickyNoteData> _onRevealNote;

        private TextBox _hotkeyBox;
        private ComboBox _themeCombo;
        private CheckBox _startupCheck;
        private StackPanel _notesListPanel;
        private uint _pendingModifiers;
        private uint _pendingKey;

        public SettingsWindow(
            NotesStoreData store,
            Action onHotkeyChanged,
            Action onSaveRequested,
            Action<StickyNoteData> onDeleteNote,
            Action<StickyNoteData> onRevealNote)
        {
            _store = store;
            _onHotkeyChanged = onHotkeyChanged;
            _onSaveRequested = onSaveRequested;
            _onDeleteNote = onDeleteNote;
            _onRevealNote = onRevealNote;

            _pendingModifiers = store.Settings.HotkeyModifiers;
            _pendingKey = store.Settings.HotkeyKey;

            Title = "StickyNotes — Settings";
            Width = 420;
            Height = 460;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;
            Background = new SolidColorBrush(Color.FromRgb(0xFA, 0xF8, 0xF2));

            BuildUi();
            RefreshNotesList();
        }

        private void BuildUi()
        {
            var root = new StackPanel { Margin = new Thickness(20) };

            var header = new TextBlock
            {
                Text = "StickyNotes",
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 19,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x28, 0x25, 0x1E))
            };
            var sub = new TextBlock
            {
                Text = "App-bound desktop notes. Press the hotkey inside any app to create or toggle its note.",
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0x6E, 0x69, 0x5C)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 16)
            };
            root.Children.Add(header);
            root.Children.Add(sub);

            // Global Hotkey
            root.Children.Add(MakeLabel("Global Hotkey (click box and press keys)"));
            _hotkeyBox = new TextBox
            {
                Text = FormatHotkey(_pendingModifiers, _pendingKey),
                IsReadOnly = true,
                Height = 30,
                Padding = new Thickness(8, 5, 8, 5),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13,
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xD6, 0xD0, 0xC2)),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 4, 0, 12)
            };
            _hotkeyBox.PreviewKeyDown += OnHotkeyPreviewKeyDown;
            root.Children.Add(_hotkeyBox);

            // Default Paper Theme
            root.Children.Add(MakeLabel("Default Paper Color"));
            _themeCombo = new ComboBox
            {
                Height = 28,
                Margin = new Thickness(0, 4, 0, 12),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13
            };
            foreach (var name in PaperThemes.All.Keys)
            {
                _themeCombo.Items.Add(name);
            }
            _themeCombo.SelectedItem = PaperThemes.All.ContainsKey(_store.Settings.DefaultTheme ?? "")
                ? _store.Settings.DefaultTheme
                : "Butter";
            _themeCombo.SelectionChanged += (s, e) =>
            {
                if (_themeCombo.SelectedItem != null)
                {
                    _store.Settings.DefaultTheme = _themeCombo.SelectedItem.ToString();
                    _onSaveRequested();
                }
            };
            root.Children.Add(_themeCombo);

            // Startup checkbox
            _startupCheck = new CheckBox
            {
                Content = "Launch StickyNotes automatically when Windows starts",
                IsChecked = StartupManager.IsStartupEnabled(),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12.5,
                Margin = new Thickness(0, 2, 0, 16)
            };
            _startupCheck.Checked += (s, e) => StartupManager.SetStartup(true);
            _startupCheck.Unchecked += (s, e) => StartupManager.SetStartup(false);
            root.Children.Add(_startupCheck);

            // Bound Notes list
            root.Children.Add(MakeLabel("Bound App Notes"));
            var listBorder = new Border
            {
                Height = 148,
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xD6, 0xD0, 0xC2)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Margin = new Thickness(0, 4, 0, 12)
            };
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            _notesListPanel = new StackPanel { Margin = new Thickness(6) };
            scroll.Content = _notesListPanel;
            listBorder.Child = scroll;
            root.Children.Add(listBorder);

            // Close button
            var closeBtn = new Button
            {
                Content = "Done",
                Width = 84,
                Height = 28,
                HorizontalAlignment = HorizontalAlignment.Right,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12.5
            };
            closeBtn.Click += (s, e) => Close();
            root.Children.Add(closeBtn);

            Content = root;
        }

        private TextBlock MakeLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x4A, 0x45, 0x3B))
            };
        }

        private void OnHotkeyPreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            Key key = (e.Key == Key.System) ? e.SystemKey : e.Key;

            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LWin || key == Key.RWin)
            {
                return;
            }

            uint mods = 0;
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) mods |= Win32.MOD_CONTROL;
            if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0) mods |= Win32.MOD_ALT;
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) mods |= Win32.MOD_SHIFT;
            if ((Keyboard.Modifiers & ModifierKeys.Windows) != 0) mods |= Win32.MOD_WIN;

            if (mods == 0) return; // Require at least one modifier key

            int vk = KeyInterop.VirtualKeyFromKey(key);
            if (vk <= 0) return;

            _pendingModifiers = mods;
            _pendingKey = (uint)vk;
            _store.Settings.HotkeyModifiers = _pendingModifiers;
            _store.Settings.HotkeyKey = _pendingKey;
            _hotkeyBox.Text = FormatHotkey(_pendingModifiers, _pendingKey);

            _onSaveRequested();
            _onHotkeyChanged();
        }

        public void RefreshNotesList()
        {
            _notesListPanel.Children.Clear();
            if (_store.Notes.Count == 0)
            {
                _notesListPanel.Children.Add(new TextBlock
                {
                    Text = "No app-bound notes yet. Focus any app and press your hotkey!",
                    FontSize = 12,
                    Foreground = Brushes.Gray,
                    Margin = new Thickness(4),
                    TextWrapping = TextWrapping.Wrap
                });
                return;
            }

            foreach (var note in new List<StickyNoteData>(_store.Notes))
            {
                var n = note;
                var row = new Grid { Margin = new Thickness(2, 3, 2, 3) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var title = new TextBlock
                {
                    Text = string.Format("{0} ({1} lines)", string.IsNullOrEmpty(n.AppName) ? n.ProcessKey : n.AppName, n.Items.Count),
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 12.5,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                Grid.SetColumn(title, 0);
                row.Children.Add(title);

                var showBtn = new Button
                {
                    Content = "Show",
                    Padding = new Thickness(8, 2, 8, 2),
                    Margin = new Thickness(6, 0, 4, 0),
                    FontSize = 11.5
                };
                showBtn.Click += (s, e) =>
                {
                    _onRevealNote(n);
                    RefreshNotesList();
                };
                Grid.SetColumn(showBtn, 1);
                row.Children.Add(showBtn);

                var delBtn = new Button
                {
                    Content = "Delete",
                    Padding = new Thickness(8, 2, 8, 2),
                    FontSize = 11.5
                };
                delBtn.Click += (s, e) =>
                {
                    _onDeleteNote(n);
                    RefreshNotesList();
                };
                Grid.SetColumn(delBtn, 2);
                row.Children.Add(delBtn);

                _notesListPanel.Children.Add(row);
            }
        }

        public static string FormatHotkey(uint modifiers, uint vk)
        {
            var parts = new List<string>();
            if ((modifiers & Win32.MOD_CONTROL) != 0) parts.Add("Ctrl");
            if ((modifiers & Win32.MOD_ALT) != 0) parts.Add("Alt");
            if ((modifiers & Win32.MOD_SHIFT) != 0) parts.Add("Shift");
            if ((modifiers & Win32.MOD_WIN) != 0) parts.Add("Win");
            Key k = KeyInterop.KeyFromVirtualKey((int)vk);
            parts.Add(k.ToString());
            return string.Join(" + ", parts);
        }

    }

    public static class StartupManager
    {
        private const string RunRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppRunName = "StickyNotesApp";

        public static string InstalledExePath
        {
            get
            {
                return System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "StickyNotes",
                    "StickyNotes.exe");
            }
        }

        public static bool IsStartupEnabled()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunRegistryPath, false))
                {
                    return key != null && key.GetValue(AppRunName) != null;
                }
            }
            catch
            {
                return false;
            }
        }

        public static void SetStartup(bool enable)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunRegistryPath, true))
                {
                    if (key == null) return;
                    if (enable)
                    {
                        string targetExe = System.IO.File.Exists(InstalledExePath)
                            ? InstalledExePath
                            : System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                        key.SetValue(AppRunName, "\"" + targetExe + "\" --autostart");
                    }
                    else
                    {
                        key.DeleteValue(AppRunName, false);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning("StartupManager.SetStartup failed: {0}", ex.Message);
            }
        }
    }
}
