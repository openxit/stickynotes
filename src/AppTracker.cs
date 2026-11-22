using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace StickyNotes
{
    public class ActiveAppInfo
    {
        public string ProcessKey { get; set; }
        public string AppName { get; set; }
        public string ExePath { get; set; }
        public Win32.RECT WindowRect { get; set; }
        public IntPtr WindowHandle { get; set; }
    }

    public class AppWindowState
    {
        public bool IsOpen { get; set; }
        public bool HasNonMinimizedWindow { get; set; }
        public IntPtr PrimaryHwnd { get; set; }
    }

    public class DesktopSnapshot
    {
        public string ForegroundProcessKey { get; set; }
        public bool IsSelfForeground { get; set; }
        public Dictionary<string, AppWindowState> Apps { get; set; }

        public DesktopSnapshot()
        {
            Apps = new Dictionary<string, AppWindowState>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public static class AppTracker
    {
        private static readonly int SelfPid = Process.GetCurrentProcess().Id;
        private static readonly Dictionary<uint, string> PidToPathCache = new Dictionary<uint, string>();
        private static int _cacheTickCounter = 0;

        public static ActiveAppInfo GetActiveAppInfo()
        {
            IntPtr hwnd = Win32.GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return null;

            uint pid;
            Win32.GetWindowThreadProcessId(hwnd, out pid);
            if (pid == 0 || pid == (uint)SelfPid) return null;

            string className = Win32.GetWindowClassName(hwnd);
            if (className == "Progman" || className == "WorkerW" || className == "Shell_TrayWnd" || className == "Shell_SecondaryTrayWnd")
            {
                return null;
            }

            string exePath = Win32.GetProcessPath(pid);
            if (string.IsNullOrEmpty(exePath)) return null;

            string fileName = Path.GetFileName(exePath);
            if (string.IsNullOrEmpty(fileName)) return null;

            string processKey = fileName.ToLowerInvariant();
            string appName = GetFriendlyAppName(exePath, fileName);

            Win32.RECT rect;
            Win32.GetWindowRect(hwnd, out rect);

            return new ActiveAppInfo
            {
                ProcessKey = processKey,
                AppName = appName,
                ExePath = exePath,
                WindowRect = rect,
                WindowHandle = hwnd
            };
        }

        public static DesktopSnapshot TakeSnapshot()
        {
            var snapshot = new DesktopSnapshot();

            // Periodically clear PID cache so exited/reused PIDs don't go stale
            _cacheTickCounter++;
            if (_cacheTickCounter >= 50)
            {
                PidToPathCache.Clear();
                _cacheTickCounter = 0;
            }

            IntPtr fgHwnd = Win32.GetForegroundWindow();
            if (fgHwnd != IntPtr.Zero)
            {
                uint fgPid;
                Win32.GetWindowThreadProcessId(fgHwnd, out fgPid);
                if (fgPid == (uint)SelfPid)
                {
                    snapshot.IsSelfForeground = true;
                }
                else if (fgPid != 0)
                {
                    string fgPath = GetCachedProcessPath(fgPid);
                    if (!string.IsNullOrEmpty(fgPath))
                    {
                        snapshot.ForegroundProcessKey = Path.GetFileName(fgPath).ToLowerInvariant();
                    }
                }
            }

            Win32.EnumWindows((hWnd, lParam) =>
            {
                if (!Win32.IsWindowVisible(hWnd)) return true;

                uint pid;
                Win32.GetWindowThreadProcessId(hWnd, out pid);
                if (pid == 0 || pid == (uint)SelfPid) return true;

                string className = Win32.GetWindowClassName(hWnd);
                if (className == "Progman" || className == "WorkerW" || className == "Shell_TrayWnd" || className == "Shell_SecondaryTrayWnd")
                {
                    return true;
                }

                string exePath = GetCachedProcessPath(pid);
                if (string.IsNullOrEmpty(exePath)) return true;

                string key = Path.GetFileName(exePath).ToLowerInvariant();
                bool isMinimized = Win32.IsIconic(hWnd);

                if (!isMinimized)
                {
                    Win32.RECT rect;
                    if (Win32.GetWindowRect(hWnd, out rect))
                    {
                        int w = rect.Right - rect.Left;
                        int h = rect.Bottom - rect.Top;
                        if (w <= 10 || h <= 10)
                        {
                            // Ignore tiny hidden helper windows
                            return true;
                        }
                    }
                }

                AppWindowState state;
                if (!snapshot.Apps.TryGetValue(key, out state))
                {
                    state = new AppWindowState { IsOpen = true };
                    snapshot.Apps[key] = state;
                }

                if (!isMinimized)
                {
                    state.HasNonMinimizedWindow = true;
                    if (state.PrimaryHwnd == IntPtr.Zero || hWnd == fgHwnd)
                    {
                        state.PrimaryHwnd = hWnd;
                    }
                }

                return true;
            }, IntPtr.Zero);

            return snapshot;
        }

        private static string GetCachedProcessPath(uint pid)
        {
            string path;
            if (PidToPathCache.TryGetValue(pid, out path))
            {
                return path;
            }
            path = Win32.GetProcessPath(pid);
            if (!string.IsNullOrEmpty(path))
            {
                PidToPathCache[pid] = path;
            }
            return path;
        }

        private static string GetFriendlyAppName(string exePath, string fileName)
        {
            try
            {
                var info = FileVersionInfo.GetVersionInfo(exePath);
                if (!string.IsNullOrWhiteSpace(info.FileDescription))
                {
                    return info.FileDescription.Trim();
                }
                if (!string.IsNullOrWhiteSpace(info.ProductName))
                {
                    return info.ProductName.Trim();
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("GetFriendlyAppName failed for {0}: {1}", exePath, ex.Message);
            }
            return Path.GetFileNameWithoutExtension(fileName);
        }
    }
}
