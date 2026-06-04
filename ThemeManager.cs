using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Win32;

namespace LilAgentsWindows
{
    internal static class ThemeManager
    {
        // ── Theme Mode ────────────────────────────────────────────────────────────
        public static string CurrentTheme { get; private set; } = "Dark";

        // ── Accent Color ──────────────────────────────────────────────────────────
        private static Color _accent = Color.FromArgb(0x81, 0x8C, 0xF8); // indigo-400

        public static Color AccentColor
        {
            get => _accent;
            private set
            {
                _accent      = value;
                AccentHover  = Darken(value, 0.14f);
                AccentSubtle = Color.FromArgb(45, value.R, value.G, value.B);
                AccentBorder = Color.FromArgb(100, value.R, value.G, value.B);
            }
        }

        public static Color AccentHover  { get; private set; } = Color.FromArgb(0x6A, 0x77, 0xE6);
        public static Color AccentSubtle { get; private set; } = Color.FromArgb(45,  0x81, 0x8C, 0xF8);
        public static Color AccentBorder { get; private set; } = Color.FromArgb(100, 0x81, 0x8C, 0xF8);

        // ── Preset Accent Colors ──────────────────────────────────────────────────
        public static readonly Color[] PresetAccents = new[]
        {
            Color.FromArgb(0x81, 0x8C, 0xF8),  // Indigo (default)
            Color.FromArgb(0x34, 0xD3, 0x99),  // Emerald
            Color.FromArgb(0x38, 0xBD, 0xF8),  // Sky
            Color.FromArgb(0xC0, 0x84, 0xFC),  // Purple
            Color.FromArgb(0xF8, 0x71, 0x71),  // Rose
            Color.FromArgb(0xFB, 0xD3, 0x60),  // Amber
            Color.FromArgb(0x4A, 0xDE, 0x80),  // Green
            Color.FromArgb(0xFB, 0x92, 0x3C),  // Orange
        };

        // ── Semantic (fixed) ──────────────────────────────────────────────────────
        public static readonly Color Success     = Color.FromArgb(0x22, 0xC5, 0x5E);
        public static readonly Color Warning     = Color.FromArgb(0xF5, 0x9E, 0x0B);
        public static readonly Color ErrorColor  = Color.FromArgb(0xEF, 0x44, 0x44);
        public static readonly Color InfoColor   = Color.FromArgb(0x38, 0xBD, 0xF8);

        // ── Dynamic theme-aware properties ───────────────────────────────────────
        // Backgrounds
        public static Color FormBg       => IsDark ? Color.FromArgb(0x18, 0x18, 0x18) : Color.FromArgb(0xF4, 0xF4, 0xF5);
        public static Color PanelBg      => IsDark ? Color.FromArgb(0x1E, 0x1E, 0x1E) : Color.FromArgb(0xFF, 0xFF, 0xFF);
        public static Color InputBg      => IsDark ? Color.FromArgb(0x26, 0x26, 0x26) : Color.FromArgb(0xF9, 0xF9, 0xF9);
        public static Color ChatBg       => IsDark ? Color.FromArgb(0x1A, 0x1A, 0x1A) : Color.FromArgb(0xF8, 0xF8, 0xF8);
        public static Color Surface2     => IsDark ? Color.FromArgb(0x2A, 0x2A, 0x2A) : Color.FromArgb(0xEE, 0xEE, 0xEE);
        public static Color Surface3     => IsDark ? Color.FromArgb(0x30, 0x30, 0x30) : Color.FromArgb(0xE4, 0xE4, 0xE7);
        public static Color HeaderBg     => IsDark ? Color.FromArgb(0x1E, 0x1E, 0x1E) : Color.FromArgb(0xFF, 0xFF, 0xFF);
        public static Color DropdownBg   => IsDark ? Color.FromArgb(0x22, 0x22, 0x22) : Color.FromArgb(0xFF, 0xFF, 0xFF);

        // Chat bubble colors
        // NOTE: RichTextBox does NOT support semi-transparent BackColor, so UserBubbleBg must be fully opaque.
        // We blend the accent color at ~18% opacity over the ChatBg to get an opaque tinted background.
        public static Color UserBubbleBg
        {
            get
            {
                // Blend: chatBg * 0.82 + accent * 0.18  →  fully opaque
                var bg = ChatBg;
                var ac = AccentColor;
                return Color.FromArgb(
                    255,
                    (int)(bg.R * 0.82 + ac.R * 0.18),
                    (int)(bg.G * 0.82 + ac.G * 0.18),
                    (int)(bg.B * 0.82 + ac.B * 0.18));
            }
        }
        public static Color UserBubbleBorder => Color.FromArgb(90, AccentColor.R, AccentColor.G, AccentColor.B);
        public static Color AgentBubbleBg    => IsDark ? Color.FromArgb(0x22, 0x22, 0x22) : Color.FromArgb(0xF0, 0xF0, 0xF0);

        // Text colors
        public static Color TextPrimary   => IsDark ? Color.FromArgb(0xF0, 0xF0, 0xF0) : Color.FromArgb(0x18, 0x18, 0x18);
        public static Color TextSecondary => IsDark ? Color.FromArgb(0x9E, 0x9E, 0x9E) : Color.FromArgb(0x6B, 0x72, 0x80);
        public static Color TextMuted     => IsDark ? Color.FromArgb(0x60, 0x60, 0x60) : Color.FromArgb(0xA0, 0xA0, 0xA0);
        public static Color TextInverse   => IsDark ? Color.FromArgb(0x12, 0x12, 0x12) : Color.FromArgb(0xF8, 0xF8, 0xF8);

        // Borders
        public static Color BorderColor  => IsDark ? Color.FromArgb(0x38, 0x38, 0x38) : Color.FromArgb(0xD4, 0xD4, 0xD8);
        public static Color BorderSubtle => IsDark ? Color.FromArgb(0x28, 0x28, 0x28) : Color.FromArgb(0xE4, 0xE4, 0xE7);

        // Status / semantic UI
        public static readonly Color StatusOnline   = Color.FromArgb(0x22, 0xC5, 0x5E);
        public static readonly Color StatusThinking = Color.FromArgb(0xF9, 0x73, 0x16);
        public static readonly Color StatusOffline  = Color.FromArgb(0xEF, 0x44, 0x44);

        private static bool IsDark => CurrentTheme != "Light";

        // ── Public API ────────────────────────────────────────────────────────────

        public static bool IsSystemInLightTheme()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key?.GetValue("AppsUseLightTheme") is int i)
                    return i == 1;
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Call once at startup (after AppConfig.Load) to restore saved theme + accent.
        /// </summary>
        public static void LoadFromConfig(AppConfig config)
        {
            string theme = config.Theme ?? "Dark";
            if (theme == "Auto")
                theme = IsSystemInLightTheme() ? "Light" : "Dark";
            CurrentTheme = theme;

            string hex = config.AccentColorHex ?? "#818CF8";
            try   { AccentColor = ColorTranslator.FromHtml(hex); }
            catch { AccentColor = Color.FromArgb(0x81, 0x8C, 0xF8); }
        }

        public static void ApplyTheme(string themeName, string accentHex)
        {
            string resolved = themeName;
            if (resolved == "Auto")
                resolved = IsSystemInLightTheme() ? "Light" : "Dark";
            CurrentTheme = resolved;

            try   { AccentColor = ColorTranslator.FromHtml(accentHex); }
            catch { AccentColor = Color.FromArgb(0x81, 0x8C, 0xF8); }

            RefreshOpenForms();
        }

        public static void SetAccentColor(Color c)
        {
            AccentColor = c;
            RefreshOpenForms();
        }

        public static string AccentHex => $"#{AccentColor.R:X2}{AccentColor.G:X2}{AccentColor.B:X2}";

        public static void RefreshOpenForms()
        {
            foreach (Form form in Application.OpenForms.Cast<Form>().ToList())
            {
                if      (form is AgentChatForm    chatForm)    chatForm.ApplyTheme();
                else if (form is AgentManagerForm managerForm) managerForm.ApplyTheme();
            }
        }

        // ── Color utilities ───────────────────────────────────────────────────────
        public static Color Darken(Color c, float amount) => Color.FromArgb(c.A,
            Math.Max(0, (int)(c.R * (1f - amount))),
            Math.Max(0, (int)(c.G * (1f - amount))),
            Math.Max(0, (int)(c.B * (1f - amount))));

        public static Color Lighten(Color c, float amount) => Color.FromArgb(c.A,
            Math.Min(255, (int)(c.R + (255 - c.R) * amount)),
            Math.Min(255, (int)(c.G + (255 - c.G) * amount)),
            Math.Min(255, (int)(c.B + (255 - c.B) * amount)));

        public static Color WithAlpha(Color c, int alpha) =>
            Color.FromArgb(Math.Clamp(alpha, 0, 255), c.R, c.G, c.B);

        public static Color Lerp(Color a, Color b, float t) => Color.FromArgb(
            Math.Clamp((int)(a.A + (b.A - a.A) * t), 0, 255),
            Math.Clamp((int)(a.R + (b.R - a.R) * t), 0, 255),
            Math.Clamp((int)(a.G + (b.G - a.G) * t), 0, 255),
            Math.Clamp((int)(a.B + (b.B - a.B) * t), 0, 255));
    }
}
