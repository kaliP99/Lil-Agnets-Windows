using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LilAgentsWindows
{
    // ══════════════════════════════════════════════════════════════════════════════
    //  UIHelpers — shared GDI+ drawing utilities for the Lil Agents UI
    // ══════════════════════════════════════════════════════════════════════════════
    internal static class UIHelpers
    {
        // ── Graphics Quality ──────────────────────────────────────────────────────

        /// <summary>Sets high-quality anti-aliased rendering on a Graphics context.</summary>
        public static void SetHighQuality(Graphics g)
        {
            g.SmoothingMode      = SmoothingMode.AntiAlias;
            g.InterpolationMode  = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode    = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;
        }

        // ── Rounded Rectangle ─────────────────────────────────────────────────────

        /// <summary>Creates a rounded rectangle GraphicsPath from integer bounds.</summary>
        public static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            radius = Math.Max(1, Math.Min(radius, Math.Min(r.Width, r.Height) / 2));
            var path = new GraphicsPath();
            path.AddArc(r.X,                  r.Y,                  radius * 2, radius * 2, 180, 90);
            path.AddArc(r.Right - radius * 2, r.Y,                  radius * 2, radius * 2, 270, 90);
            path.AddArc(r.Right - radius * 2, r.Bottom - radius * 2, radius * 2, radius * 2, 0,   90);
            path.AddArc(r.X,                  r.Bottom - radius * 2, radius * 2, radius * 2, 90,  90);
            path.CloseFigure();
            return path;
        }

        /// <summary>Creates a rounded rectangle GraphicsPath from float bounds.</summary>
        public static GraphicsPath RoundedRect(RectangleF r, float radius)
        {
            radius = Math.Max(1f, Math.Min(radius, Math.Min(r.Width, r.Height) / 2f));
            var path = new GraphicsPath();
            path.AddArc(r.X,                  r.Y,                  radius * 2, radius * 2, 180, 90);
            path.AddArc(r.Right - radius * 2, r.Y,                  radius * 2, radius * 2, 270, 90);
            path.AddArc(r.Right - radius * 2, r.Bottom - radius * 2, radius * 2, radius * 2, 0,   90);
            path.AddArc(r.X,                  r.Bottom - radius * 2, radius * 2, radius * 2, 90,  90);
            path.CloseFigure();
            return path;
        }

        /// <summary>Fills a rounded rectangle.</summary>
        public static void FillRoundedRect(Graphics g, Brush brush, Rectangle r, int radius)
        {
            using var path = RoundedRect(r, radius);
            g.FillPath(brush, path);
        }

        /// <summary>Draws the outline of a rounded rectangle.</summary>
        public static void DrawRoundedRect(Graphics g, Pen pen, Rectangle r, int radius)
        {
            using var path = RoundedRect(r, radius);
            g.DrawPath(pen, path);
        }

        // ── Shadow ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Draws a soft multi-layer shadow around a rectangle.
        /// </summary>
        public static void DrawSoftShadow(Graphics g, Rectangle r, int radius, int layers = 3, int yOffset = 2)
        {
            for (int i = layers; i >= 1; i--)
            {
                int inflate = i * 2;
                int alpha   = 20 - i * 4;
                if (alpha <= 0) continue;
                var shadowRect = new Rectangle(
                    r.X - inflate + 1,
                    r.Y - inflate + yOffset + 1,
                    r.Width  + inflate * 2,
                    r.Height + inflate * 2);
                using var brush = new SolidBrush(Color.FromArgb(alpha, 0, 0, 0));
                FillRoundedRect(g, brush, shadowRect, radius + inflate);
            }
        }

        // ── Hover Animation ───────────────────────────────────────────────────────

        /// <summary>
        /// Attaches a smooth lerp hover animation to any Control.
        /// BackColor transitions from restColor→hoverColor on mouse-enter and back on leave.
        /// </summary>
        public static void AddHoverAnimation(Control ctrl, Color restColor, Color hoverColor)
        {
            float t       = 0f;
            bool hovering = false;
            var timer = new System.Windows.Forms.Timer { Interval = 16 };

            timer.Tick += (_, _) =>
            {
                float target = hovering ? 1f : 0f;
                t += (target - t) * 0.22f;
                if (Math.Abs(t - target) < 0.01f) { t = target; timer.Stop(); }
                ctrl.BackColor = ThemeManager.Lerp(restColor, hoverColor, t);
            };

            ctrl.MouseEnter += (_, _) => { hovering = true;  timer.Start(); };
            ctrl.MouseLeave += (_, _) => { hovering = false; timer.Start(); };
            ctrl.Disposed   += (_, _) => { timer.Stop(); timer.Dispose(); };

            ctrl.BackColor = restColor;
        }

        // ── Color Utilities ───────────────────────────────────────────────────────

        public static bool IsColorDark(Color c) =>
            (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) < 140;

        /// <summary>Returns Black or White whichever contrasts best with bg.</summary>
        public static Color ContrastColor(Color bg) =>
            IsColorDark(bg) ? Color.White : Color.Black;
    }

    // ══════════════════════════════════════════════════════════════════════════════
    //  Additional Custom Controls (DoubleBufferedPanel is in AgentManagerForm.cs)
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>FlowLayoutPanel with double-buffering enabled.</summary>
    internal class DoubleBufferedFlow : FlowLayoutPanel
    {
        public DoubleBufferedFlow()
        {
            SetStyle(
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint  |
                ControlStyles.UserPaint,
                true);
            UpdateStyles();
        }
    }

    /// <summary>
    /// A small animated dot that pulses when in Pulsing mode.
    /// Use for "Thinking" status indicators.
    /// </summary>
    internal class PulsingDot : Panel
    {
        private float _phase;
        private readonly System.Windows.Forms.Timer _timer;

        public Color DotColor { get; set; } = ThemeManager.StatusOnline;
        public bool  Pulsing  { get; set; } = false;

        public PulsingDot()
        {
            Size      = new Size(10, 10);
            BackColor = Color.Transparent;
            SetStyle(
                ControlStyles.SupportsTransparentBackColor |
                ControlStyles.UserPaint                    |
                ControlStyles.OptimizedDoubleBuffer        |
                ControlStyles.AllPaintingInWmPaint,
                true);
            UpdateStyles();
            _timer = new System.Windows.Forms.Timer { Interval = 33 };
            _timer.Tick += (_, _) => { _phase += 0.09f; Invalidate(); };
        }

        public void StartPulse()  { Pulsing = true;  _timer.Start(); }
        public void StopPulse()   { Pulsing = false; _timer.Stop();  Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            float alpha = Pulsing ? (0.45f + 0.55f * MathF.Abs(MathF.Sin(_phase))) : 1f;
            int   a     = (int)(alpha * 255);
            using var brush = new SolidBrush(Color.FromArgb(a, DotColor));
            g.FillEllipse(brush, 1, 1, Width - 2, Height - 2);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { _timer.Stop(); _timer.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
