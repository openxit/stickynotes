using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace StickyNotes
{
    public class StickyNotesApp
    {
        private const int HotkeyId = 0x534E; // "SN"
        private const string MutexName = @"Global\OpenXit_StickyNotes_SingleInstance_v1";

        private Application _wpfApp;
        private NotesStoreData _store;
        private readonly Dictionary<string, StickyNoteWindow> _windows = new Dictionary<string, StickyNoteWindow>(StringComparer.OrdinalIgnoreCase);

        private HwndSource _hotkeySource;
        private TrayManager _tray;
        private SettingsWindow _settingsWindow;
        private DispatcherTimer _trackerTimer;
        private DispatcherTimer _saveDebounceTimer;
        private bool _savePending;

        [STAThread]
        public static void Main(string[] args)
        {
            bool isAutostart = false;
            if (args != null)
            {
                foreach (var a in args)
                {
                    if (string.Equals(a, "--autostart", StringComparison.OrdinalIgnoreCase))
                    {
                        isAutostart = true;
                        break;
                    }
                }
            }

            // Self-install to %LOCALAPPDATA%\StickyNotes\StickyNotes.exe before acquiring the single-instance mutex
            if (!isAutostart && TryRelocateToAppData())
            {
                return;
            }

            bool createdNew;
            using (var mutex = new Mutex(true, MutexName, out createdNew))
            {
                if (!createdNew)
                {
                    if (!isAutostart)
                    {
                        MessageBox.Show(
                            "StickyNotes is already running in the system tray.",
                            "StickyNotes",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    return;
                }

                var app = new StickyNotesApp();
                app.Run(isAutostart);
            }
        }

        private static bool TryRelocateToAppData()
        {
            try
            {
                string currentExe = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                string targetExe = StartupManager.InstalledExePath;

                if (string.Equals(
                    System.IO.Path.GetFullPath(currentExe),
                    System.IO.Path.GetFullPath(targetExe),
                    StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                string targetDir = System.IO.Path.GetDirectoryName(targetExe);
                if (!System.IO.Directory.Exists(targetDir))
                {
                    System.IO.Directory.CreateDirectory(targetDir);
                }

                System.IO.File.Copy(currentExe, targetExe, true);
                System.Diagnostics.Process.Start(targetExe);
                return true;
            }
            catch
            {
                // If relocation fails (e.g. permissions), continue running from current path
                return false;
            }
        }

        private void Run(bool isAutostart)
        {
            _wpfApp = new Application
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown
            };

            _store = Storage.Load();

            // Enable Windows autostart by default on first run; keep exe path fresh if enabled
            if (!_store.Settings.StartupInitialized)
            {
                StartupManager.SetStartup(true);
                _store.Settings.StartupInitialized = true;
                Storage.Save(_store);
            }
            else if (StartupManager.IsStartupEnabled())
            {
                StartupManager.SetStartup(true);
            }

            _saveDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
            _saveDebounceTimer.Tick += (s, e) =>
            {
                _saveDebounceTimer.Stop();
                if (_savePending)
                {
                    _savePending = false;
                    Storage.Save(_store);
                }
            };

            _tray = new TrayManager(OpenSettings, ExitApplication);
            _tray.UpdateHotkeyLabel(_store.Settings.HotkeyModifiers, _store.Settings.HotkeyKey);

            _wpfApp.Startup += (s, e) =>
            {
                SetupHotkeySink();
                RegisterConfiguredHotkey();

                _trackerTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(160) };
                _trackerTimer.Tick += (ts, te) => SyncWindowsWithDesktop();
                _trackerTimer.Start();

                if (!isAutostart && !_store.Settings.HasShownWelcome)
                {
                    _store.Settings.HasShownWelcome = true;
                    Storage.Save(_store);
                    string hk = SettingsWindow.FormatHotkey(_store.Settings.HotkeyModifiers, _store.Settings.HotkeyKey);
                    _tray.ShowBalloon("StickyNotes is ready", "Focus any app (e.g. VS Code) and press " + hk + " to pin a sticky note.");
                }
            };

            _wpfApp.SessionEnding += (s, e) =>
            {
                if (_savePending)
                {
                    _savePending = false;
                    Storage.Save(_store);
                }
            };

            _wpfApp.Exit += (s, e) =>
            {
                if (_savePending)
                {
                    Storage.Save(_store);
                }
                UnregisterConfiguredHotkey();
                if (_hotkeySource != null) _hotkeySource.Dispose();
                if (_tray != null) _tray.Dispose();
            };

            _wpfApp.Run();
        }

        private void SetupHotkeySink()
        {
            var p = new HwndSourceParameters("StickyNotesHotkeySink")
            {
                Width = 0,
                Height = 0,
                WindowStyle = 0
            };
            _hotkeySource = new HwndSource(p);
            _hotkeySource.AddHook(HotkeyWndProc);
        }

        private void RegisterConfiguredHotkey()
        {
            if (_hotkeySource == null) return;
            Win32.UnregisterHotKey(_hotkeySource.Handle, HotkeyId);
            bool ok = Win32.RegisterHotKey(
                _hotkeySource.Handle,
                HotkeyId,
                _store.Settings.HotkeyModifiers | Win32.MOD_NOREPEAT,
                _store.Settings.HotkeyKey);

            if (!ok)
            {
                string hk = SettingsWindow.FormatHotkey(_store.Settings.HotkeyModifiers, _store.Settings.HotkeyKey);
                _tray.ShowBalloon("Hotkey Conflict", "Could not register " + hk + ". Open Settings to choose another hotkey.");
            }
            _tray.UpdateHotkeyLabel(_store.Settings.HotkeyModifiers, _store.Settings.HotkeyKey);
        }

        private void UnregisterConfiguredHotkey()
        {
            if (_hotkeySource != null)
            {
                Win32.UnregisterHotKey(_hotkeySource.Handle, HotkeyId);
            }
        }

        private IntPtr HotkeyWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == Win32.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
            {
                handled = true;
                HandleHotkeyPressed();
            }
            return IntPtr.Zero;
        }

        private void HandleHotkeyPressed()
        {
            // Check if one of our own sticky note windows is currently active
            foreach (var win in _windows.Values)
            {
                if (win.IsVisible && win.IsActive)
                {
                    win.Data.UserHidden = true;
                    win.Hide();
                    RequestSave();
                    return;
                }
            }

            ActiveAppInfo activeApp = AppTracker.GetActiveAppInfo();
            if (activeApp == null) return;

            StickyNoteData existing = FindNoteByProcessKey(activeApp.ProcessKey);
            if (existing == null)
            {
                // Create a brand new note bound to this app
                var newNote = CreateNoteForApp(activeApp);
                _store.Notes.Add(newNote);
                RequestSave();

                var win = GetOrCreateWindow(newNote);
                win.ShowActivated = true;
                win.Topmost = true;
                win.Show();
                win.Activate();
                win.FocusFirstInput();
            }
            else
            {
                var win = GetOrCreateWindow(existing);
                if (win.IsVisible && !existing.UserHidden)
                {
                    existing.UserHidden = true;
                    win.Hide();
                    RequestSave();
                }
                else
                {
                    existing.UserHidden = false;
                    RequestSave();
                    win.ShowActivated = true;
                    win.Topmost = true;
                    win.Show();
                    win.Activate();
                    win.FocusFirstInput();
                }
            }
        }

        private StickyNoteData CreateNoteForApp(ActiveAppInfo app)
        {
            double width = 310;
            double height = 350;

            // Convert active window rect from physical pixels to WPF DIPs
            double dpiX = 1.0, dpiY = 1.0;
            if (_hotkeySource != null && _hotkeySource.CompositionTarget != null)
            {
                dpiX = _hotkeySource.CompositionTarget.TransformToDevice.M11;
                dpiY = _hotkeySource.CompositionTarget.TransformToDevice.M22;
            }

            double appRight = app.WindowRect.Right / dpiX;
            double appTop = app.WindowRect.Top / dpiY;

            double workLeft = SystemParameters.WorkArea.Left;
            double workTop = SystemParameters.WorkArea.Top;
            double workRight = SystemParameters.WorkArea.Right;
            double workBottom = SystemParameters.WorkArea.Bottom;

            double left = appRight - width - 24;
            double top = appTop + 48;

            left = Math.Max(workLeft + 12, Math.Min(left, workRight - width - 12));
            top = Math.Max(workTop + 12, Math.Min(top, workBottom - height - 12));

            return new StickyNoteData
            {
                ProcessKey = app.ProcessKey,
                AppName = app.AppName,
                ExePath = app.ExePath,
                Left = left,
                Top = top,
                Width = width,
                Height = height,
                PaperTheme = _store.Settings.DefaultTheme ?? "Butter",
                UserHidden = false,
                Items = new List<TodoItemData>
                {
                    new TodoItemData { HasCheckbox = true, Text = "" }
                }
            };
        }

        private void SyncWindowsWithDesktop()
        {
            DesktopSnapshot snapshot = AppTracker.TakeSnapshot();

            foreach (var note in _store.Notes)
            {
                AppWindowState state;
                bool appVisible = snapshot.Apps.TryGetValue(note.ProcessKey, out state)
                                  && state.IsOpen
                                  && state.HasNonMinimizedWindow;

                bool shouldShow = appVisible && !note.UserHidden;

                StickyNoteWindow win;
                _windows.TryGetValue(note.ProcessKey, out win);

                if (shouldShow)
                {
                    if (win == null)
                    {
                        win = GetOrCreateWindow(note);
                    }

                    bool isAppOrSelfFg = snapshot.IsSelfForeground
                                         || string.Equals(snapshot.ForegroundProcessKey, note.ProcessKey, StringComparison.OrdinalIgnoreCase);

                    if (!win.IsVisible)
                    {
                        win.ShowActivated = false;
                        win.Topmost = isAppOrSelfFg;
                        win.Show();
                    }
                    else if (win.Topmost != isAppOrSelfFg)
                    {
                        win.Topmost = isAppOrSelfFg;
                    }
                }
                else
                {
                    if (win != null && win.IsVisible)
                    {
                        win.Hide();
                    }
                }
            }
        }

        private StickyNoteWindow GetOrCreateWindow(StickyNoteData data)
        {
            StickyNoteWindow win;
            if (_windows.TryGetValue(data.ProcessKey, out win))
            {
                return win;
            }

            win = new StickyNoteWindow(
                data,
                onSaveRequested: RequestSave,
                onUserHideRequested: (w) => w.Hide());

            _windows[data.ProcessKey] = win;
            return win;
        }

        private StickyNoteData FindNoteByProcessKey(string processKey)
        {
            foreach (var note in _store.Notes)
            {
                if (string.Equals(note.ProcessKey, processKey, StringComparison.OrdinalIgnoreCase))
                {
                    return note;
                }
            }
            return null;
        }

        private void RequestSave()
        {
            _savePending = true;
            _saveDebounceTimer.Stop();
            _saveDebounceTimer.Start();
        }

        private void OpenSettings()
        {
            if (_settingsWindow != null && _settingsWindow.IsVisible)
            {
                _settingsWindow.RefreshNotesList();
                _settingsWindow.Activate();
                return;
            }

            _settingsWindow = new SettingsWindow(
                _store,
                onHotkeyChanged: RegisterConfiguredHotkey,
                onSaveRequested: RequestSave,
                onDeleteNote: (note) =>
                {
                    StickyNoteWindow win;
                    if (_windows.TryGetValue(note.ProcessKey, out win))
                    {
                        win.Close();
                        _windows.Remove(note.ProcessKey);
                    }
                    _store.Notes.Remove(note);
                    RequestSave();
                },
                onRevealNote: (note) =>
                {
                    note.UserHidden = false;
                    RequestSave();
                    var win = GetOrCreateWindow(note);
                    win.ShowActivated = true;
                    win.Topmost = true;
                    win.Show();
                    win.Activate();
                });

            _settingsWindow.Closed += (s, e) => _settingsWindow = null;
            _settingsWindow.Show();
            _settingsWindow.Activate();
        }

        private void ExitApplication()
        {
            if (_trackerTimer != null) _trackerTimer.Stop();
            if (_savePending) Storage.Save(_store);
            _wpfApp.Shutdown();
        }
    }
}
