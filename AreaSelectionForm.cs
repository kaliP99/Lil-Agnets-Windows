using System;
using System.Drawing;
using System.Windows.Forms;

namespace LilAgentsWindows
{
    internal class AreaSelectionForm : Form
    {
        private Point _start;
        private Rectangle _selection;
        private bool _dragging;

        public Rectangle SelectedArea { get; private set; }

        public AreaSelectionForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            TopMost = true;
            DoubleBuffered = true;
            BackColor = Color.Black;
            Opacity = 0.28;
            Cursor = Cursors.Cross;
            ShowInTaskbar = false;

            MouseDown += (_, e) =>
            {
                _dragging = true;
                _start = e.Location;
                _selection = Rectangle.Empty;
            };

            MouseMove += (_, e) =>
            {
                if (!_dragging)
                {
                    return;
                }

                _selection = Normalize(_start, e.Location);
                Invalidate();
            };

            MouseUp += (_, e) =>
            {
                _dragging = false;
                SelectedArea = Normalize(_start, e.Location);
                DialogResult = SelectedArea.Width > 40 && SelectedArea.Height > 40
                    ? DialogResult.OK
                    : DialogResult.Cancel;
                Close();
            };

            KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    DialogResult = DialogResult.Cancel;
                    Close();
                }
            };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_selection == Rectangle.Empty)
            {
                using var font = new Font("Segoe UI Semibold", 18, FontStyle.Bold);
                e.Graphics.DrawString("Drag to select where agents can walk. Press Esc to cancel.", font, Brushes.White, 40, 40);
                return;
            }

            using var brush = new SolidBrush(Color.FromArgb(80, 0, 190, 255));
            using var pen = new Pen(Color.DeepSkyBlue, 3);
            e.Graphics.FillRectangle(brush, _selection);
            e.Graphics.DrawRectangle(pen, _selection);
        }

        private static Rectangle Normalize(Point a, Point b)
        {
            return new Rectangle(
                Math.Min(a.X, b.X),
                Math.Min(a.Y, b.Y),
                Math.Abs(a.X - b.X),
                Math.Abs(a.Y - b.Y));
        }
    }
}
