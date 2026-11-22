using System;
using System.Collections.Generic;
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

            var warnings = new List<string>();

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
                    catch (Exception ex)
                    {
                        warnings.Add("Could not stop process: " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                warnings.Add("Could not enumerate running processes: " + ex.Message);
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
            catch (Exception ex)
            {
                warnings.Add("Could not remove startup entry: " + ex.Message);
            }

            // 3. Delete installed exe directory (%LOCALAPPDATA%\StickyNotes)
            TryDeleteDirectory(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StickyNotes"),
                warnings);

            // 4. Delete saved notes & settings (%APPDATA%\StickyNotes)
            TryDeleteDirectory(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StickyNotes"),
                warnings);

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
            catch (Exception ex)
            {
                warnings.Add("Could not delete sibling StickyNotes.exe: " + ex.Message);
            }

            if (warnings.Count > 0)
            {
                MessageBox.Show(
                    "StickyNotes was uninstalled with some warnings:\n\n" + string.Join("\n", warnings),
                    "Uninstall StickyNotes",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            else
            {
                MessageBox.Show(
                    "StickyNotes and all saved notes have been completely removed.",
                    "Uninstall StickyNotes",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private static void TryDeleteDirectory(string path, List<string> warnings)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch (Exception ex)
            {
                warnings.Add(string.Format("Could not remove '{0}': {1}", path, ex.Message));
            }
        }
    }
}
