using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace StickyNotes
{
    [DataContract]
    public class TodoItemData
    {
        [DataMember(Order = 1)]
        public string Text { get; set; }

        [DataMember(Order = 2)]
        public bool HasCheckbox { get; set; }

        [DataMember(Order = 3)]
        public bool IsChecked { get; set; }

        public TodoItemData()
        {
            Text = string.Empty;
            HasCheckbox = true;
            IsChecked = false;
        }
    }

    [DataContract]
    public class StickyNoteData
    {
        [DataMember(Order = 1)]
        public string Id { get; set; }

        [DataMember(Order = 2)]
        public string ProcessKey { get; set; } // lowercase exe filename, e.g. "code.exe"

        [DataMember(Order = 3)]
        public string AppName { get; set; } // friendly name, e.g. "Visual Studio Code"

        [DataMember(Order = 4)]
        public string ExePath { get; set; }

        [DataMember(Order = 5)]
        public double Left { get; set; }

        [DataMember(Order = 6)]
        public double Top { get; set; }

        [DataMember(Order = 7)]
        public double Width { get; set; }

        [DataMember(Order = 8)]
        public double Height { get; set; }

        [DataMember(Order = 9)]
        public string PaperTheme { get; set; } // "Butter", "Ivory", "Sage", "Sky", "Blush"

        [DataMember(Order = 10)]
        public bool UserHidden { get; set; }

        [DataMember(Order = 11)]
        public List<TodoItemData> Items { get; set; }

        public StickyNoteData()
        {
            Id = Guid.NewGuid().ToString("N");
            Width = 310;
            Height = 350;
            PaperTheme = "Butter";
            Items = new List<TodoItemData>();
        }
    }

    [DataContract]
    public class AppSettings
    {
        [DataMember(Order = 1)]
        public uint HotkeyModifiers { get; set; } // Default: Ctrl + Alt

        [DataMember(Order = 2)]
        public uint HotkeyKey { get; set; } // Default: 'N' (0x4E)

        [DataMember(Order = 3)]
        public string DefaultTheme { get; set; }

        [DataMember(Order = 4)]
        public bool StartupInitialized { get; set; }

        [DataMember(Order = 5)]
        public bool HasShownWelcome { get; set; }

        public AppSettings()
        {
            HotkeyModifiers = Win32.MOD_CONTROL | Win32.MOD_ALT;
            HotkeyKey = 0x4E; // N
            DefaultTheme = "Butter";
            StartupInitialized = false;
            HasShownWelcome = false;
        }
    }

    [DataContract]
    public class NotesStoreData
    {
        [DataMember(Order = 1)]
        public AppSettings Settings { get; set; }

        [DataMember(Order = 2)]
        public List<StickyNoteData> Notes { get; set; }

        public NotesStoreData()
        {
            Settings = new AppSettings();
            Notes = new List<StickyNoteData>();
        }
    }

    public static class Storage
    {
        private static readonly string AppDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "StickyNotes");

        private static readonly string StorePath = Path.Combine(AppDataDir, "notes.json");

        public static NotesStoreData Load()
        {
            try
            {
                if (!File.Exists(StorePath))
                {
                    return new NotesStoreData();
                }

                byte[] bytes = File.ReadAllBytes(StorePath);
                using (var ms = new MemoryStream(bytes))
                {
                    var serializer = new DataContractJsonSerializer(typeof(NotesStoreData));
                    var data = serializer.ReadObject(ms) as NotesStoreData;
                    if (data == null) return new NotesStoreData();
                    if (data.Settings == null) data.Settings = new AppSettings();
                    if (data.Notes == null) data.Notes = new List<StickyNoteData>();
                    foreach (var note in data.Notes)
                    {
                        if (note.Items == null) note.Items = new List<TodoItemData>();
                        if (string.IsNullOrEmpty(note.PaperTheme)) note.PaperTheme = "Butter";
                        if (note.Width < 220) note.Width = 310;
                        if (note.Height < 180) note.Height = 350;
                    }
                    return data;
                }
            }
            catch
            {
                return new NotesStoreData();
            }
        }

        public static void Save(NotesStoreData data)
        {
            try
            {
                if (!Directory.Exists(AppDataDir))
                {
                    Directory.CreateDirectory(AppDataDir);
                }

                using (var ms = new MemoryStream())
                {
                    var serializer = new DataContractJsonSerializer(typeof(NotesStoreData));
                    serializer.WriteObject(ms, data);
                    string json = Encoding.UTF8.GetString(ms.ToArray());
                    string tempPath = StorePath + ".tmp";
                    File.WriteAllText(tempPath, json, Encoding.UTF8);
                    if (File.Exists(StorePath))
                    {
                        File.Replace(tempPath, StorePath, null);
                    }
                    else
                    {
                        File.Move(tempPath, StorePath);
                    }
                }
            }
            catch
            {
                // Ignore transient IO errors on auto-save
            }
        }
    }
}
