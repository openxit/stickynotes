using System.Collections.Generic;
using System.Windows.Media;

namespace StickyNotes
{
    public class PaperTheme
    {
        public string Name { get; set; }
        public Color PaperTop { get; set; }
        public Color PaperBottom { get; set; }
        public Color AdhesiveTop { get; set; }
        public Color AdhesiveBottom { get; set; }
        public Color BorderColor { get; set; }
        public Color RuledLineColor { get; set; }
        public Color MarginLineColor { get; set; }
        public Color InkColor { get; set; }
        public Color MutedInkColor { get; set; }
        public Color SwatchColor { get; set; }
    }

    public static class PaperThemes
    {
        public static readonly Dictionary<string, PaperTheme> All = new Dictionary<string, PaperTheme>
        {
            {
                "Butter", new PaperTheme
                {
                    Name = "Butter",
                    PaperTop = Color.FromRgb(0xFF, 0xF7, 0xBC),
                    PaperBottom = Color.FromRgb(0xFA, 0xEE, 0x9C),
                    AdhesiveTop = Color.FromRgb(0xF6, 0xE9, 0x8F),
                    AdhesiveBottom = Color.FromRgb(0xF3, 0xE5, 0x86),
                    BorderColor = Color.FromRgb(0xE0, 0xCF, 0x6F),
                    RuledLineColor = Color.FromArgb(0x70, 0xC9, 0xB5, 0x58),
                    MarginLineColor = Color.FromArgb(0x55, 0xD1, 0x66, 0x5C),
                    InkColor = Color.FromRgb(0x2C, 0x28, 0x1E),
                    MutedInkColor = Color.FromRgb(0x7D, 0x74, 0x5B),
                    SwatchColor = Color.FromRgb(0xF5, 0xE3, 0x7A)
                }
            },
            {
                "Ivory", new PaperTheme
                {
                    Name = "Ivory",
                    PaperTop = Color.FromRgb(0xFC, 0xFA, 0xF0),
                    PaperBottom = Color.FromRgb(0xF5, 0xEF, 0xDC),
                    AdhesiveTop = Color.FromRgb(0xF0, 0xE8, 0xCE),
                    AdhesiveBottom = Color.FromRgb(0xEB, 0xE2, 0xC4),
                    BorderColor = Color.FromRgb(0xDC, 0xD2, 0xB4),
                    RuledLineColor = Color.FromArgb(0x68, 0xA8, 0xBA, 0xCC),
                    MarginLineColor = Color.FromArgb(0x5A, 0xD4, 0x68, 0x64),
                    InkColor = Color.FromRgb(0x26, 0x26, 0x24),
                    MutedInkColor = Color.FromRgb(0x7A, 0x77, 0x6E),
                    SwatchColor = Color.FromRgb(0xF2, 0xEA, 0xCE)
                }
            },
            {
                "Sage", new PaperTheme
                {
                    Name = "Sage",
                    PaperTop = Color.FromRgb(0xE8, 0xF2, 0xE1),
                    PaperBottom = Color.FromRgb(0xDA, 0xE9, 0xCF),
                    AdhesiveTop = Color.FromRgb(0xCE, 0xE0, 0xC1),
                    AdhesiveBottom = Color.FromRgb(0xC8, 0xDB, 0xBA),
                    BorderColor = Color.FromRgb(0xB8, 0xCC, 0xAA),
                    RuledLineColor = Color.FromArgb(0x68, 0x8F, 0xAD, 0x81),
                    MarginLineColor = Color.FromArgb(0x4D, 0xC7, 0x6B, 0x66),
                    InkColor = Color.FromRgb(0x22, 0x2C, 0x20),
                    MutedInkColor = Color.FromRgb(0x67, 0x78, 0x63),
                    SwatchColor = Color.FromRgb(0xC9, 0xDF, 0xBA)
                }
            },
            {
                "Sky", new PaperTheme
                {
                    Name = "Sky",
                    PaperTop = Color.FromRgb(0xE6, 0xF1, 0xFA),
                    PaperBottom = Color.FromRgb(0xD6, 0xE7, 0xF6),
                    AdhesiveTop = Color.FromRgb(0xC9, 0xDE, 0xF1),
                    AdhesiveBottom = Color.FromRgb(0xC2, 0xD9, 0xEE),
                    BorderColor = Color.FromRgb(0xB0, 0xC8, 0xDF),
                    RuledLineColor = Color.FromArgb(0x68, 0x89, 0xAB, 0xCA),
                    MarginLineColor = Color.FromArgb(0x4D, 0xCE, 0x6C, 0x6C),
                    InkColor = Color.FromRgb(0x1E, 0x27, 0x32),
                    MutedInkColor = Color.FromRgb(0x63, 0x73, 0x85),
                    SwatchColor = Color.FromRgb(0xC2, 0xDC, 0xF2)
                }
            },
            {
                "Blush", new PaperTheme
                {
                    Name = "Blush",
                    PaperTop = Color.FromRgb(0xFC, 0xEA, 0xEA),
                    PaperBottom = Color.FromRgb(0xF7, 0xDB, 0xDB),
                    AdhesiveTop = Color.FromRgb(0xF0, 0xCC, 0xCC),
                    AdhesiveBottom = Color.FromRgb(0xEC, 0xC5, 0xC5),
                    BorderColor = Color.FromRgb(0xDC, 0xB4, 0xB4),
                    RuledLineColor = Color.FromArgb(0x68, 0xC9, 0x8E, 0x8E),
                    MarginLineColor = Color.FromArgb(0x55, 0xC4, 0x5A, 0x5A),
                    InkColor = Color.FromRgb(0x30, 0x20, 0x22),
                    MutedInkColor = Color.FromRgb(0x82, 0x65, 0x68),
                    SwatchColor = Color.FromRgb(0xF0, 0xC8, 0xC8)
                }
            }
        };

        public static PaperTheme Get(string name)
        {
            PaperTheme theme;
            if (!string.IsNullOrEmpty(name) && All.TryGetValue(name, out theme))
            {
                return theme;
            }
            return All["Butter"];
        }
    }
}
