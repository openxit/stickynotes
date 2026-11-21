using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace StickyNotes
{
    public class TrayManager : IDisposable
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly Icon _trayIcon;
        private readonly ContextMenuStrip _menu;
        private readonly ToolStripMenuItem _hotkeyHintItem;

        public TrayManager(
            Action onOpenSettings,
            Action onExitRequested)
        {
            _trayIcon = CreateStickyNoteIcon();

            _menu = new ContextMenuStrip();
            _hotkeyHintItem = new ToolStripMenuItem("Press Ctrl + Alt + N inside any app")
            {
                Enabled = false
            };
            var settingsItem = new ToolStripMenuItem("Settings & Bound Notes...", null, (s, e) => onOpenSettings());
            var exitItem = new ToolStripMenuItem("Exit", null, (s, e) => onExitRequested());

            _menu.Items.Add(_hotkeyHintItem);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(settingsItem);
            _menu.Items.Add(exitItem);

            _notifyIcon = new NotifyIcon
            {
                Icon = _trayIcon,
                Text = "StickyNotes — App-Bound Desktop Notes",
                ContextMenuStrip = _menu,
                Visible = true
            };

            _notifyIcon.DoubleClick += (s, e) => onOpenSettings();
        }

        public void UpdateHotkeyLabel(uint modifiers, uint vk)
        {
            string formatted = SettingsWindow.FormatHotkey(modifiers, vk);
            _hotkeyHintItem.Text = "Hotkey: " + formatted + " (inside any app)";
            _notifyIcon.Text = "StickyNotes (" + formatted + ")";
        }

        public void ShowBalloon(string title, string text)
        {
            _notifyIcon.ShowBalloonTip(2500, title, text, ToolTipIcon.Info);
        }

        private static Icon CreateStickyNoteIcon()
        {
            using (var bmp = new Bitmap(32, 32))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                // Subtle shadow
                using (var shadowBrush = new SolidBrush(Color.FromArgb(65, 0, 0, 0)))
                {
                    g.FillRectangle(shadowBrush, 4, 5, 25, 25);
                }

                // Warm butter paper
                using (var paperBrush = new SolidBrush(Color.FromArgb(255, 244, 158)))
                {
                    g.FillRectangle(paperBrush, 3, 3, 25, 25);
                }

                // Adhesive top band
                using (var stripBrush = new SolidBrush(Color.FromArgb(242, 224, 108)))
                {
                    g.FillRectangle(stripBrush, 3, 3, 25, 6);
                }

                // Ruled lines
                using (var linePen = new Pen(Color.FromArgb(140, 184, 162, 74), 1f))
                {
                    g.DrawLine(linePen, 6, 13, 25, 13);
                    g.DrawLine(linePen, 6, 18, 25, 18);
                    g.DrawLine(linePen, 6, 23, 25, 23);
                }

                // Border
                using (var borderPen = new Pen(Color.FromArgb(204, 182, 82), 1.2f))
                {
                    g.DrawRectangle(borderPen, 3, 3, 25, 25);
                }

                IntPtr hIcon = bmp.GetHicon();
                try
                {
                    using (var temp = Icon.FromHandle(hIcon))
                    {
                        return (Icon)temp.Clone();
                    }
                }
                finally
                {
                    Win32.DestroyIcon(hIcon);
                }
            }
        }

        public void Dispose()
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _menu.Dispose();
            _trayIcon.Dispose();
        }
    }
}
