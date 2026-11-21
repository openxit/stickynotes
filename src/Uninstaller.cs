using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace StickyNotesUninstaller
{
    public static class Program
    {
        private const string RunRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppRunName = "StickyNotesApp";

        [STAThread]
        public static void Main()
        {
            var result = MessageBox.Show(
                "This will stop StickyNotes, remove Windows autostart, and permanently delete StickyNotes.exe and all saved notes.\n\nDo you want to continue?",
                "Uninstall StickyNotes",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            // 1. Stop running StickyNotes.exe instances
            try
            {
                foreach (var proc in Process.GetProcessesByName("StickyNotes"))
                {
                    try
                    {
                        proc.Kill();
                        proc.WaitForExit(3000);
                    }
                    catch
                    {
                        // Ignore per-process errors
                    }
                }
            }
            catch
            {
                // Ignore enumeration errors
            }

            // 2. Remove Windows autostart registry entry
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunRegistryPath, true))
                {
                    if (key != null)
                    {
                        key.DeleteValue(AppRunName, false);
                    }
                }
            }
            catch
            {
                // Ignore registry errors
            }

            // 3. Delete installed exe directory (%LOCALAPPDATA%\StickyNotes)
            TryDeleteDirectory(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "StickyNotes"));

            // 4. Delete saved notes & settings (%APPDATA%\StickyNotes)
            TryDeleteDirectory(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "StickyNotes"));

            // 5. Delete StickyNotes.exe next to the uninstaller if present
            try
            {
                string currentDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
                if (!string.IsNullOrEmpty(currentDir))
                {
                    string siblingExe = Path.Combine(currentDir, "StickyNotes.exe");
                    if (File.Exists(siblingExe))
                    {
                        File.Delete(siblingExe);
                    }
                }
            }
            catch
            {
                // Ignore
            }

            MessageBox.Show(
                "StickyNotes and all saved notes have been completely removed.",
                "Uninstall StickyNotes",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch
            {
                // Ignore locked/missing files
            }
        }
    }
}
