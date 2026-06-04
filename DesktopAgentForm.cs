using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LilAgentsWindows
{
    internal class DesktopAgentForm : Form
    {
        private readonly Func<Rectangle> _walkBoundsProvider;
        private readonly System.Windows.Forms.Timer _walkTimer;
        private readonly Random _random = new();
        private int _dx;
        private int _dy;
        private int _step;
        private bool _isDragging;
        private bool _isPaused;
        private bool _isThinking;
        private Point _dragOffset;
        private Point _lastDragPos;
        private float _dragVelocityX;
        private float _dragVelocityY;
        private double _lastDragTime;
        private double _pauseEndTime;
        private int _currentFrameIndex;
        private readonly System.Windows.Forms.Timer _frameTimer;

        // Interactive states
        private double _lastActivityTime;
        private bool _isSleeping;
        private double _excitedEndTime;
        private int _particleSpawnCounter;
        private readonly List<SleepParticle> _particles = new();

        // Multi-Agent Registry & Speech Bubble
        public static readonly List<DesktopAgentForm> ActiveAgents = new();
        private string _speechText = "";
        private double _speechEndTime = 0;
        private double _collisionCooldown = 0;
        private bool _isHandshaking = false;
        private double _handshakeEndTime = 0;

        // Group Autonomous Mode
        public static bool IsAutonomousGroupMode { get; set; } = false;

        // Window Crawler Physics
        private enum CrawlerState
        {
            FreeWalk,
            WalkingTitleBar,
            CrawlingLeftBorder,
            CrawlingRightBorder,
            FallingPhysical
        }
        private CrawlerState _crawlerState = CrawlerState.FreeWalk;
        private IntPtr _lastCrawledWindow = IntPtr.Zero;
        private float _velocityY = 0f;
        private float _velocityX = 0f;
        private const float Gravity = 1.6f;
        private bool _isThudRecovery = false;
        private bool _isRollRecovery = false;
        private IntPtr _trackingWindowHandle = IntPtr.Zero;
        private RECT _lastWindowRect;

        private enum AgentState
        {
            Idle,
            Walking,
            Running,
            Jumping,
            Falling,
            Sleeping,
            Thinking,
            Excited,
            Dragged,
            Climbing,
            Sitting,
            Yawning,
            LookingAround,
            Stretching,
            Flying
        }
        private AgentState _state = AgentState.Idle;
        private bool _hasPlayedExcitedNearMouse = false;
        private bool _isReturningHome = false;
        private double _outsideExplorationEndTime = 0;
        private bool _hasAlertedOutside = false;
        private Rectangle _lastWalkAreaRect = Rectangle.Empty;
        private double _stretchEndTime = 0;
        private IntPtr _currentCrawledWindow = IntPtr.Zero;
        private List<VisibleWindow> _cachedWindows = new();
        private double _lastWindowScanTime = 0;

        private struct VisibleWindow
        {
            public IntPtr Handle;
            public Rectangle Rect;
        }

        private class WalkableSurface
        {
            public IntPtr Handle;
            public Rectangle Rect;
            public string SurfaceType { get; set; } = "Desktop"; // "Desktop" or "WindowTitleBar"
        }
        private WalkableSurface? _currentSurface = null;

        public Agent Agent { get; }
        public new CharacterSize Size
        {
            get => _size;
            set
            {
                _size = value;
                UpdateSize();
            }
        }
        private CharacterSize _size = CharacterSize.Medium;

        // WS_EX_LAYERED Support Structures & Win32 Functions
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
            public int Width => Right - Left;
            public int Height => Bottom - Top;
        }

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref Point pptDst, ref Size psize, IntPtr hdcSrc, ref Point pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hGDIOBJ);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00080000; // WS_EX_LAYERED
                return cp;
            }
        }

        public DesktopAgentForm(Agent agent, Func<Rectangle> walkBoundsProvider)
        {
            Agent = agent;
            _walkBoundsProvider = walkBoundsProvider;
            _size = agent.Size;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            DoubleBuffered = true;
            AllowDrop = true;

            // Set initial size
            UpdateSize();

            _dx = _random.Next(0, 2) == 0 ? -2 : 2;
            _dy = _random.Next(0, 2) == 0 ? -1 : 1;

            double intensity = 1.0;
            try
            {
                var config = AppConfig.Load();
                intensity = config.AnimationIntensity;
            }
            catch { }

            _walkTimer = new System.Windows.Forms.Timer { Interval = intensity <= 0.05 ? 90 : (int)(90 / intensity) };
            _walkTimer.Tick += (_, _) => Walk();
            if (intensity > 0.05) _walkTimer.Start();

            // Frame update timer for animation
            _frameTimer = new System.Windows.Forms.Timer { Interval = intensity <= 0.05 ? 220 : (int)(220 / intensity) };
            _frameTimer.Tick += (_, _) =>
            {
                _currentFrameIndex++;
                Redraw();
            };
            if (intensity > 0.05) _frameTimer.Start();

            MouseDown += OnMouseDown;
            MouseMove += OnMouseMove;
            MouseUp += OnMouseUp;
            MouseDoubleClick += (_, _) => { UpdateActivity(); AgentChatForm.ShowForAgent(Agent); };

            var menu = new ContextMenuStrip();
            menu.Items.Add($"Ask {Agent.CharacterName}", null, (_, _) => { UpdateActivity(); AgentChatForm.ShowForAgent(Agent); });
            menu.Items.Add("Pause walking", null, (_, _) => { _isPaused = true; UpdateActivity(); });
            menu.Items.Add("Resume walking", null, (_, _) => { _isPaused = false; _pauseEndTime = GetTickCount() + RandomDouble(3000, 8000); UpdateActivity(); });

            var sizeSubMenu = new ToolStripMenuItem("Size");
            sizeSubMenu.DropDownItems.Add("Small", null, (_, _) => { Size = CharacterSize.Small; UpdateSize(); UpdateActivity(); });
            sizeSubMenu.DropDownItems.Add("Medium", null, (_, _) => { Size = CharacterSize.Medium; UpdateSize(); UpdateActivity(); });
            sizeSubMenu.DropDownItems.Add("Large", null, (_, _) => { Size = CharacterSize.Large; UpdateSize(); UpdateActivity(); });
            menu.Items.Add(sizeSubMenu);

            var movementSubMenu = new ToolStripMenuItem("Movement Mode");
            var freeRoamItem = new ToolStripMenuItem("Free Roam");
            var restrictedTerritoryItem = new ToolStripMenuItem("Restricted Territory");
            var setTerritoryItem = new ToolStripMenuItem("Set Territory...");

            freeRoamItem.Click += (_, _) =>
            {
                Agent.MovementType = "FreeRoam";
                Agent.WalkArea.Enabled = false;
                SaveConfigHelper();
                _crawlerState = CrawlerState.FreeWalk;
                UpdateActivity();
                Redraw();
            };

            restrictedTerritoryItem.Click += (_, _) =>
            {
                if (Agent.WalkArea.Width <= 40 || Agent.WalkArea.Height <= 40)
                {
                    menu.Close();
                    using var selector = new AreaSelectionForm();
                    if (selector.ShowDialog() == DialogResult.OK)
                    {
                        Agent.MovementType = "RestrictedTerritory";
                        Agent.WalkArea.Enabled = true;
                        Agent.WalkArea.X = selector.SelectedArea.X;
                        Agent.WalkArea.Y = selector.SelectedArea.Y;
                        Agent.WalkArea.Width = selector.SelectedArea.Width;
                        Agent.WalkArea.Height = selector.SelectedArea.Height;
                        SaveConfigHelper();
                        _crawlerState = CrawlerState.FreeWalk;

                        // If outside new territory, walk home immediately
                        var bounds = _walkBoundsProvider();
                        int feetY = Top + Height;
                        int centerX = Left + Width / 2;
                        if (centerX < bounds.Left || centerX > bounds.Right || feetY < bounds.Top || feetY > bounds.Bottom)
                        {
                            _isReturningHome = true;
                            _outsideExplorationEndTime = 0;
                            SetSpeechBubble("New territory! Walking home...", 2000);
                        }
                    }
                }
                else
                {
                    Agent.MovementType = "RestrictedTerritory";
                    Agent.WalkArea.Enabled = true;
                    SaveConfigHelper();
                    _crawlerState = CrawlerState.FreeWalk;

                    // If outside territory, walk home immediately
                    var bounds = _walkBoundsProvider();
                    int feetY = Top + Height;
                    int centerX = Left + Width / 2;
                    if (centerX < bounds.Left || centerX > bounds.Right || feetY < bounds.Top || feetY > bounds.Bottom)
                    {
                        _isReturningHome = true;
                        _outsideExplorationEndTime = 0;
                        SetSpeechBubble("Walking to my territory...", 2000);
                    }
                }
                UpdateActivity();
                Redraw();
            };

            setTerritoryItem.Click += (_, _) =>
            {
                menu.Close();
                using var selector = new AreaSelectionForm();
                if (selector.ShowDialog() == DialogResult.OK)
                {
                    Agent.MovementType = "RestrictedTerritory";
                    Agent.WalkArea.Enabled = true;
                    Agent.WalkArea.X = selector.SelectedArea.X;
                    Agent.WalkArea.Y = selector.SelectedArea.Y;
                    Agent.WalkArea.Width = selector.SelectedArea.Width;
                    Agent.WalkArea.Height = selector.SelectedArea.Height;
                    SaveConfigHelper();
                    _crawlerState = CrawlerState.FreeWalk;

                    // Walk home immediately
                    _isReturningHome = true;
                    _outsideExplorationEndTime = 0;
                    SetSpeechBubble("Territory updated! Heading home...", 2000);

                    UpdateActivity();
                    Redraw();
                }
            };

            movementSubMenu.DropDownItems.Add(freeRoamItem);
            movementSubMenu.DropDownItems.Add(restrictedTerritoryItem);
            movementSubMenu.DropDownItems.Add(setTerritoryItem);
            menu.Items.Add(movementSubMenu);

            menu.Opening += (s, e) =>
            {
                bool isRestricted = Agent.MovementType == "RestrictedTerritory" || Agent.MovementType == "FreeWindow";
                freeRoamItem.Checked = !isRestricted;
                restrictedTerritoryItem.Checked = isRestricted;
                setTerritoryItem.Enabled = isRestricted;
            };

            menu.Items.Add("Hide", null, (_, _) => Hide());
            menu.Items.Add("Bring to front", null, (_, _) => { BringToFront(); UpdateActivity(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, (_, _) => Application.Exit());
            ContextMenuStrip = menu;

            Agent.ThinkingChanged += (_, _) =>
            {
                _isThinking = Agent.IsThinking;
                UpdateActivity();
                Redraw();
            };

            // Initial activity tick
            _lastActivityTime = GetTickCount();

            // Initial pause with random duration
            _pauseEndTime = GetTickCount() + RandomDouble(2000, 5000);

            // Register with ActiveAgents
            ActiveAgents.Add(this);
            FormClosed += (s, e) => ActiveAgents.Remove(this);

            // Trigger initial redraw once handle is created
            HandleCreated += (s, e) => Redraw();
        }

        private bool HasCustomFrames()
        {
            if (Agent == null) return false;
            return (Agent.WalkingFrames != null && Agent.WalkingFrames.Any(path => !string.IsNullOrWhiteSpace(path)))
                || (Agent.SleepingFrames != null && Agent.SleepingFrames.Any(path => !string.IsNullOrWhiteSpace(path)))
                || (Agent.ThinkingFrames != null && Agent.ThinkingFrames.Any(path => !string.IsNullOrWhiteSpace(path)))
                || (Agent.HandshakeFrames != null && Agent.HandshakeFrames.Any(path => !string.IsNullOrWhiteSpace(path)))
                || (Agent.ExcitedFrames != null && Agent.ExcitedFrames.Any(path => !string.IsNullOrWhiteSpace(path)))
                || (Agent.FallingFrames != null && Agent.FallingFrames.Any(path => !string.IsNullOrWhiteSpace(path)))
                || (Agent.SittingFrames != null && Agent.SittingFrames.Any(path => !string.IsNullOrWhiteSpace(path)))
                || (Agent.StretchingFrames != null && Agent.StretchingFrames.Any(path => !string.IsNullOrWhiteSpace(path)))
                || (Agent.YawningFrames != null && Agent.YawningFrames.Any(path => !string.IsNullOrWhiteSpace(path)))
                || (Agent.LookingAroundFrames != null && Agent.LookingAroundFrames.Any(path => !string.IsNullOrWhiteSpace(path)))
                || (Agent.FlyingFrames != null && Agent.FlyingFrames.Any(path => !string.IsNullOrWhiteSpace(path)))
                || (Agent.RunningFrames != null && Agent.RunningFrames.Any(path => !string.IsNullOrWhiteSpace(path)));
        }

        private bool StateHasCustomFrames()
        {
            if (Agent == null) return false;
            List<string> frames;
            if (_state == AgentState.Sleeping)
                frames = Agent.SleepingFrames;
            else if (_state == AgentState.Thinking)
                frames = Agent.ThinkingFrames;
            else if (_isHandshaking)
                frames = Agent.HandshakeFrames;
            else if (_state == AgentState.Excited)
                frames = Agent.ExcitedFrames;
            else if (_state == AgentState.Falling)
                frames = Agent.FallingFrames;
            else if (_state == AgentState.Sitting)
                frames = Agent.SittingFrames;
            else if (_state == AgentState.Stretching)
                frames = Agent.StretchingFrames;
            else if (_state == AgentState.Yawning)
                frames = Agent.YawningFrames;
            else if (_state == AgentState.LookingAround)
                frames = Agent.LookingAroundFrames;
            else if (_state == AgentState.Flying)
                frames = Agent.FlyingFrames;
            else if (_state == AgentState.Running)
                frames = Agent.RunningFrames;
            else
                frames = Agent.WalkingFrames;

            if (frames == null) return false;
            var validFrames = frames.Where(path => !string.IsNullOrWhiteSpace(path)).ToList();
            if (validFrames.Count == 0) return false;

            var framePath = validFrames[_currentFrameIndex % validFrames.Count];
            var image = ImageFrameCache.Get(framePath);
            return image != null;
        }

        private float GetScale()
        {
            return Size switch
            {
                CharacterSize.Small => HasCustomFrames() ? 0.68f : 0.72f,
                CharacterSize.Medium => 1.0f,
                CharacterSize.Large => HasCustomFrames() ? 1.36f : 1.34f,
                _ => 1.0f
            };
        }

        private float GetAgentHeadY(float scale, float bob)
        {
            if (StateHasCustomFrames())
            {
                float targetH = 112f * scale;
                return Height - targetH + bob;
            }
            else
            {
                float bodyH = 72f * scale;
                return Height - bodyH - 10f * scale + bob;
            }
        }

        private void UpdateSize()
        {
            float scale = GetScale();

            if (HasCustomFrames())
            {
                Width = (int)(108 * scale);
                Height = (int)(112 * scale + 30 * scale);
            }
            else
            {
                var (w, h) = Size switch
                {
                    CharacterSize.Small => (110, 125),
                    CharacterSize.Medium => (150, 170),
                    CharacterSize.Large => (200, 225),
                    _ => (150, 170)
                };
                Width = w;
                Height = h;
            }
            Redraw();
        }

        private static double GetTickCount()
        {
            return Environment.TickCount64;
        }

        private double RandomDouble(double min, double max)
        {
            return _random.NextDouble() * (max - min) + min;
        }

        private void UpdateActivity()
        {
            double now = GetTickCount();
            double inactiveTime = now - _lastActivityTime;
            _lastActivityTime = now;

            if (_isSleeping || inactiveTime > 60000)
            {
                _isSleeping = false;
                lock (_particles)
                {
                    _particles.Clear();
                }
                _stretchEndTime = now + 1500;
                var yawnSayings = new[] { "Yawn... *stretch*", "Ah, morning?", "*stretches*", "Who woke me up?" };
                SetSpeechBubble(yawnSayings[_random.Next(yawnSayings.Length)], 1500);
                Redraw();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            // Do nothing - layered windows draw using UpdateLayeredWindow
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // Do nothing - prevents flickering
        }

        private void Redraw()
        {
            if (Width <= 0 || Height <= 0 || IsDisposed || !IsHandleCreated) return;

            using var bmp = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                // Apply rotation transform if crawling
                bool rotated = false;
                if (Agent.MovementType == "FreeRoam" || Agent.MovementType == "RestrictedTerritory" || Agent.MovementType == "FreeWindow" || Agent.MovementType == "FreeCrawler" || Agent.MovementType == "WindowCrawler")
                {
                    if (_crawlerState == CrawlerState.CrawlingLeftBorder)
                    {
                        g.TranslateTransform(Width / 2f, Height / 2f);
                        g.RotateTransform(90);
                        g.TranslateTransform(-Width / 2f, -Height / 2f);
                        rotated = true;
                    }
                    else if (_crawlerState == CrawlerState.CrawlingRightBorder)
                    {
                        g.TranslateTransform(Width / 2f, Height / 2f);
                        g.RotateTransform(270);
                        g.TranslateTransform(-Width / 2f, -Height / 2f);
                        rotated = true;
                    }
                }

                if (!TryDrawFrame(g))
                {
                    DrawCuteAgent(g);
                }

                if (rotated)
                {
                    g.ResetTransform();
                }

                // Draw sleep particles if sleeping
                if (_isSleeping)
                {
                    DrawSleepParticles(g);
                }

                // Draw GDI+ Speech bubble if speaking, otherwise draw name plate
                if (!string.IsNullOrEmpty(_speechText) && GetTickCount() <= _speechEndTime)
                {
                    DrawSpeechBubble(g);
                }
                else
                {
                    DrawNamePlate(g);
                }
            }

            UpdateWindow(bmp);
        }

        private void UpdateWindow(Bitmap bitmap)
        {
            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memDc = CreateCompatibleDC(screenDc);
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr oldBitmap = IntPtr.Zero;

            try
            {
                hBitmap = bitmap.GetHbitmap(Color.FromArgb(0)); // preserves transparency
                oldBitmap = SelectObject(memDc, hBitmap);

                Size size = bitmap.Size;
                Point pointSource = new Point(0, 0);
                Point pointDestination = this.Location;

                BLENDFUNCTION blend = new BLENDFUNCTION
                {
                    BlendOp = 0, // AC_SRC_OVER
                    BlendFlags = 0,
                    SourceConstantAlpha = 255,
                    AlphaFormat = 1 // AC_SRC_ALPHA
                };

                UpdateLayeredWindow(this.Handle, screenDc, ref pointDestination, ref size, memDc, ref pointSource, 0, ref blend, 2); // 2 = ULW_ALPHA
            }
            catch
            {
                // Ignore draw failures during window close or resize
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, screenDc);
                if (hBitmap != IntPtr.Zero)
                {
                    SelectObject(memDc, oldBitmap);
                    DeleteObject(hBitmap);
                }
                DeleteDC(memDc);
            }
        }

        private bool TryDrawFrame(Graphics g)
        {
            if (Agent == null) return false;
            List<string> frames;
            if (_state == AgentState.Sleeping)
                frames = Agent.SleepingFrames;
            else if (_state == AgentState.Thinking)
                frames = Agent.ThinkingFrames;
            else if (_isHandshaking)
                frames = Agent.HandshakeFrames;
            else if (_state == AgentState.Excited)
                frames = Agent.ExcitedFrames;
            else if (_state == AgentState.Falling)
                frames = Agent.FallingFrames;
            else if (_state == AgentState.Sitting)
                frames = Agent.SittingFrames;
            else if (_state == AgentState.Stretching)
                frames = Agent.StretchingFrames;
            else if (_state == AgentState.Yawning)
                frames = Agent.YawningFrames;
            else if (_state == AgentState.LookingAround)
                frames = Agent.LookingAroundFrames;
            else if (_state == AgentState.Flying)
                frames = Agent.FlyingFrames;
            else if (_state == AgentState.Running)
                frames = Agent.RunningFrames;
            else
                frames = Agent.WalkingFrames;

            if (frames == null) return false;
            var validFrames = frames.Where(path => !string.IsNullOrWhiteSpace(path)).ToList();
            if (validFrames.Count == 0)
            {
                return false;
            }

            var framePath = validFrames[_currentFrameIndex % validFrames.Count];
            var image = ImageFrameCache.Get(framePath);
            if (image is null)
            {
                return false;
            }

            var scale = GetScale();
            var targetW = (int)(108 * scale);
            var targetH = (int)(112 * scale);
            var targetX = (Width - targetW) / 2;

            float bob = 0;
            if (_state == AgentState.Walking || _state == AgentState.Climbing)
            {
                bob = (float)Math.Sin(_step / 2.0) * 3f * scale;
            }
            else if (_state == AgentState.Running)
            {
                bob = (float)Math.Sin(_step / 1.0) * 5f * scale;
            }
            else if (_state == AgentState.Excited)
            {
                bob = (float)Math.Sin(_step / 1.0) * 6f * scale;
            }
            else if (_state == AgentState.Jumping)
            {
                bob = -10f * scale;
            }
            else if (_state == AgentState.Falling)
            {
                bob = 2f * scale;
            }
            else if (_state == AgentState.Dragged)
            {
                bob = (float)Math.Sin(_step / 1.5) * 2f * scale;
            }

            var targetY = (int)(Height - targetH + bob);
            var target = new Rectangle(targetX, targetY, targetW, targetH);
            g.DrawImage(image, target);
            return true;
        }

        private void DrawCuteAgent(Graphics g)
        {
            var scale = GetScale();

            // Calculate state-driven body bobbing
            float bob = 0;
            if (_state == AgentState.Walking || _state == AgentState.Climbing)
            {
                bob = (float)Math.Sin(_step / 2.0) * 3f * scale;
            }
            else if (_state == AgentState.Running)
            {
                bob = (float)Math.Sin(_step / 1.0) * 5f * scale;
            }
            else if (_state == AgentState.Excited)
            {
                bob = (float)Math.Sin(_step / 1.0) * 6f * scale;
            }
            else if (_state == AgentState.Jumping)
            {
                bob = -10f * scale;
            }
            else if (_state == AgentState.Falling)
            {
                bob = 2f * scale;
            }
            else if (_state == AgentState.Dragged)
            {
                bob = (float)Math.Sin(_step / 1.5) * 2f * scale;
            }

            // State-driven body width & height (squish and stretch physics)
            var bodyW = 72 * scale;
            var bodyH = 72 * scale;
            if (_isThudRecovery)
            {
                bodyW = 86 * scale;
                bodyH = 50 * scale;
            }
            else if (_isRollRecovery)
            {
                bodyW = 68 * scale;
                bodyH = 76 * scale;
            }
            else if (_state == AgentState.Jumping || _state == AgentState.Dragged)
            {
                bodyW = 66 * scale;
                bodyH = 78 * scale;
            }
            else if (_state == AgentState.Falling)
            {
                bodyW = 78 * scale;
                bodyH = 66 * scale;
            }

            var bodyX = (Width - bodyW) / 2f;
            var bodyY = Height - bodyH - 10 * scale + bob;

            // Soft drop shadow (dimmed or hidden in air states)
            if (_state != AgentState.Falling && _state != AgentState.Jumping && _state != AgentState.Dragged)
            {
                using var shadowBrush = new SolidBrush(Color.FromArgb(50, 0, 0, 0));
                g.FillEllipse(shadowBrush, bodyX + 10 * scale, Height - 12 * scale, bodyW - 20 * scale, 10 * scale);
            }

            // Draw Body (Linear 3D sphere gradient)
            using var bodyBrush = new LinearGradientBrush(
                new PointF(bodyX, bodyY),
                new PointF(bodyX + bodyW, bodyY + bodyH),
                LighterColor(Agent.BodyColor),
                DarkerColor(Agent.BodyColor)
            );
            g.FillEllipse(bodyBrush, bodyX, bodyY, bodyW, bodyH);

            // Blush (only if not sleeping/sitting)
            if (_state != AgentState.Sleeping && _state != AgentState.Sitting)
            {
                using var blushBrush = new SolidBrush(Color.FromArgb(90, 255, 120, 150));
                g.FillEllipse(blushBrush, bodyX + 14 * scale, bodyY + 36 * scale, 10 * scale, 6 * scale);
                g.FillEllipse(blushBrush, bodyX + 48 * scale, bodyY + 36 * scale, 10 * scale, 6 * scale);
            }

            // White glass highlight
            using var shineBrush = new SolidBrush(Color.FromArgb(120, 255, 255, 255));
            g.FillEllipse(shineBrush, bodyX + 12 * scale, bodyY + 8 * scale, 22 * scale, 12 * scale);

            var eyeW = 14 * scale;
            var eyeH = 16 * scale;
            var mouthPenColor = Color.FromArgb(140, 20, 20, 20);
            using var mouthPen = new Pen(mouthPenColor, Math.Max(1.8f, 2.2f * scale));
            mouthPen.StartCap = LineCap.Round;
            mouthPen.EndCap = LineCap.Round;

            // DRAW ARMS (Procedural based on state)
            if (_state == AgentState.Dragged)
            {
                // Dangling arms pointing up
                g.DrawLine(mouthPen, bodyX + 12 * scale, bodyY + 16 * scale, bodyX + 8 * scale, bodyY - 6 * scale);
                g.DrawLine(mouthPen, bodyX + bodyW - 12 * scale, bodyY + 16 * scale, bodyX + bodyW - 8 * scale, bodyY - 6 * scale);
            }
            else if (_state == AgentState.Stretching)
            {
                // Raising arms straight up
                g.DrawLine(mouthPen, bodyX + 6 * scale, bodyY + bodyH / 2f, bodyX - 8 * scale, bodyY + 10 * scale);
                g.DrawLine(mouthPen, bodyX + bodyW - 6 * scale, bodyY + bodyH / 2f, bodyX + bodyW + 8 * scale, bodyY + 10 * scale);
            }
            else if (_state == AgentState.Excited)
            {
                // Waving arms
                float waveOffset = (float)Math.Sin(_step * 1.2) * 8f * scale;
                g.DrawLine(mouthPen, bodyX + 4 * scale, bodyY + bodyH / 2f, bodyX - 10 * scale, bodyY + bodyH / 2f + waveOffset);
                g.DrawLine(mouthPen, bodyX + bodyW - 4 * scale, bodyY + bodyH / 2f, bodyX + bodyW + 10 * scale, bodyY + bodyH / 2f - waveOffset);
            }
            else if (_state == AgentState.Climbing)
            {
                // Grabbing side borders
                if (_crawlerState == CrawlerState.CrawlingLeftBorder)
                {
                    g.DrawLine(mouthPen, bodyX + 10 * scale, bodyY + bodyH / 2f, bodyX - 6 * scale, bodyY + bodyH / 2f - 4 * scale);
                    g.DrawLine(mouthPen, bodyX + 10 * scale, bodyY + bodyH / 2f + 10 * scale, bodyX - 6 * scale, bodyY + bodyH / 2f + 6 * scale);
                }
                else
                {
                    g.DrawLine(mouthPen, bodyX + bodyW - 10 * scale, bodyY + bodyH / 2f, bodyX + bodyW + 6 * scale, bodyY + bodyH / 2f - 4 * scale);
                    g.DrawLine(mouthPen, bodyX + bodyW - 10 * scale, bodyY + bodyH / 2f + 10 * scale, bodyX + bodyW + 6 * scale, bodyY + bodyH / 2f + 6 * scale);
                }
            }

            // DRAW FACE (Eyes & Mouth based on state)
            if (_state == AgentState.Sleeping)
            {
                // Closed sleeping eyes
                g.DrawArc(mouthPen, bodyX + 18 * scale, bodyY + 28 * scale, 15 * scale, 10 * scale, 0, 180);
                g.DrawArc(mouthPen, bodyX + 40 * scale, bodyY + 28 * scale, 15 * scale, 10 * scale, 0, 180);

                // Small sleeping mouth
                g.DrawLine(mouthPen, bodyX + 33 * scale, bodyY + 46 * scale, bodyX + 39 * scale, bodyY + 46 * scale);
            }
            else if (_state == AgentState.Yawning)
            {
                // Yawning closed eyes > <
                g.DrawLine(mouthPen, bodyX + 18 * scale, bodyY + 33 * scale, bodyX + 28 * scale, bodyY + 31 * scale);
                g.DrawLine(mouthPen, bodyX + 18 * scale, bodyY + 33 * scale, bodyX + 28 * scale, bodyY + 35 * scale);
                
                g.DrawLine(mouthPen, bodyX + 54 * scale, bodyY + 33 * scale, bodyX + 44 * scale, bodyY + 31 * scale);
                g.DrawLine(mouthPen, bodyX + 54 * scale, bodyY + 33 * scale, bodyX + 44 * scale, bodyY + 35 * scale);

                // Wide open yawning mouth
                g.DrawEllipse(mouthPen, bodyX + 31 * scale, bodyY + 44 * scale, 10 * scale, 12 * scale);
                using var yawnFill = new SolidBrush(Color.FromArgb(90, 20, 20, 20));
                g.FillEllipse(yawnFill, bodyX + 31 * scale, bodyY + 44 * scale, 10 * scale, 12 * scale);
            }
            else if (_state == AgentState.LookingAround)
            {
                // Eyes normal
                g.FillEllipse(Brushes.White, bodyX + 20 * scale, bodyY + 28 * scale, eyeW, eyeH);
                g.FillEllipse(Brushes.White, bodyX + 42 * scale, bodyY + 28 * scale, eyeW, eyeH);

                // Pupils look side-to-side based on timer
                float lookX = (float)Math.Sin(_step / 4.0) * 4.5f * scale;
                g.FillEllipse(Brushes.Black, bodyX + 25 * scale + lookX, bodyY + 33 * scale, 5.5f * scale, 6.5f * scale);
                g.FillEllipse(Brushes.Black, bodyX + 47 * scale + lookX, bodyY + 33 * scale, 5.5f * scale, 6.5f * scale);

                // Curious mouth
                g.DrawArc(mouthPen, bodyX + 32 * scale, bodyY + 43 * scale, 8 * scale, 5 * scale, 0, 180);
            }
            else if (_state == AgentState.Sitting)
            {
                if (_isThudRecovery)
                {
                    // Dizzy spinning crossed eyes for thud splatted look!
                    var dragEyeW = 16 * scale;
                    var dragEyeH = 18 * scale;
                    g.FillEllipse(Brushes.White, bodyX + 18 * scale, bodyY + 16 * scale, dragEyeW, dragEyeH);
                    g.FillEllipse(Brushes.White, bodyX + 40 * scale, bodyY + 16 * scale, dragEyeW, dragEyeH);
                    g.DrawEllipse(mouthPen, bodyX + 18 * scale, bodyY + 16 * scale, dragEyeW, dragEyeH);
                    g.DrawEllipse(mouthPen, bodyX + 40 * scale, bodyY + 16 * scale, dragEyeW, dragEyeH);

                    float leftEyeCenterX = bodyX + 18 * scale + dragEyeW / 2f;
                    float leftEyeCenterY = bodyY + 16 * scale + dragEyeH / 2f;
                    float rightEyeCenterX = bodyX + 40 * scale + dragEyeW / 2f;
                    float rightEyeCenterY = bodyY + 16 * scale + dragEyeH / 2f;

                    float radius = 5f * scale;

                    // Left eye spinning cross
                    var stateLeft = g.Save();
                    g.TranslateTransform(leftEyeCenterX, leftEyeCenterY);
                    g.RotateTransform((float)(_step * 25));
                    g.DrawLine(mouthPen, -radius, -radius, radius, radius);
                    g.DrawLine(mouthPen, -radius, radius, radius, -radius);
                    g.Restore(stateLeft);

                    // Right eye spinning cross (opposite direction)
                    var stateRight = g.Save();
                    g.TranslateTransform(rightEyeCenterX, rightEyeCenterY);
                    g.RotateTransform((float)(-_step * 25));
                    g.DrawLine(mouthPen, -radius, -radius, radius, radius);
                    g.DrawLine(mouthPen, -radius, radius, radius, -radius);
                    g.Restore(stateRight);

                    // Squished dizzy open mouth
                    g.DrawEllipse(mouthPen, bodyX + 32 * scale, bodyY + 34 * scale, 8 * scale, 9 * scale);
                }
                else
                {
                    // Sleepy half-closed eyes
                    g.FillEllipse(Brushes.White, bodyX + 20 * scale, bodyY + 28 * scale, eyeW, eyeH);
                    g.FillEllipse(Brushes.White, bodyX + 42 * scale, bodyY + 28 * scale, eyeW, eyeH);

                    g.FillEllipse(Brushes.Black, bodyX + 25 * scale, bodyY + 35 * scale, 5.5f * scale, 4.5f * scale);
                    g.FillEllipse(Brushes.Black, bodyX + 47 * scale, bodyY + 35 * scale, 5.5f * scale, 4.5f * scale);

                    // Half eyelids
                    using var lidBrush = new SolidBrush(Agent.BodyColor);
                    g.FillRectangle(lidBrush, bodyX + 20 * scale, bodyY + 28 * scale, eyeW, eyeH / 2f);
                    g.FillRectangle(lidBrush, bodyX + 42 * scale, bodyY + 28 * scale, eyeW, eyeH / 2f);
                    g.DrawLine(mouthPen, bodyX + 20 * scale, bodyY + 28 * scale + eyeH / 2f, bodyX + 20 * scale + eyeW, bodyY + 28 * scale + eyeH / 2f);
                    g.DrawLine(mouthPen, bodyX + 42 * scale, bodyY + 28 * scale + eyeH / 2f, bodyX + 42 * scale + eyeW, bodyY + 28 * scale + eyeH / 2f);

                    // Neutral line mouth
                    g.DrawLine(mouthPen, bodyX + 32 * scale, bodyY + 44 * scale, bodyX + 40 * scale, bodyY + 44 * scale);
                }
            }
            else if (_state == AgentState.Falling)
            {
                // Crossed "x" eyes
                g.DrawLine(mouthPen, bodyX + 18 * scale, bodyY + 28 * scale, bodyX + 30 * scale, bodyY + 38 * scale);
                g.DrawLine(mouthPen, bodyX + 30 * scale, bodyY + 28 * scale, bodyX + 18 * scale, bodyY + 38 * scale);

                g.DrawLine(mouthPen, bodyX + 42 * scale, bodyY + 28 * scale, bodyX + 54 * scale, bodyY + 38 * scale);
                g.DrawLine(mouthPen, bodyX + 54 * scale, bodyY + 28 * scale, bodyX + 42 * scale, bodyY + 38 * scale);

                // Surprised open 'O' mouth
                g.DrawEllipse(mouthPen, bodyX + 32 * scale, bodyY + 46 * scale, 8 * scale, 9 * scale);
            }
            else if (_state == AgentState.Dragged)
            {
                // Wide surprised/dizzy eyes
                var dragEyeW = 16 * scale;
                var dragEyeH = 18 * scale;
                g.FillEllipse(Brushes.White, bodyX + 18 * scale, bodyY + 26 * scale, dragEyeW, dragEyeH);
                g.FillEllipse(Brushes.White, bodyX + 40 * scale, bodyY + 26 * scale, dragEyeW, dragEyeH);
                g.DrawEllipse(mouthPen, bodyX + 18 * scale, bodyY + 26 * scale, dragEyeW, dragEyeH);
                g.DrawEllipse(mouthPen, bodyX + 40 * scale, bodyY + 26 * scale, dragEyeW, dragEyeH);

                float leftEyeCenterX = bodyX + 18 * scale + dragEyeW / 2f;
                float leftEyeCenterY = bodyY + 26 * scale + dragEyeH / 2f;
                float rightEyeCenterX = bodyX + 40 * scale + dragEyeW / 2f;
                float rightEyeCenterY = bodyY + 26 * scale + dragEyeH / 2f;

                float radius = 5f * scale;

                // Left Eye spinning cross
                var stateLeft = g.Save();
                g.TranslateTransform(leftEyeCenterX, leftEyeCenterY);
                g.RotateTransform((float)(_step * 25));
                g.DrawLine(mouthPen, -radius, -radius, radius, radius);
                g.DrawLine(mouthPen, -radius, radius, radius, -radius);
                g.Restore(stateLeft);

                // Right Eye spinning cross (opposite direction)
                var stateRight = g.Save();
                g.TranslateTransform(rightEyeCenterX, rightEyeCenterY);
                g.RotateTransform((float)(-_step * 25));
                g.DrawLine(mouthPen, -radius, -radius, radius, radius);
                g.DrawLine(mouthPen, -radius, radius, radius, -radius);
                g.Restore(stateRight);

                // Surprised mouth
                g.DrawEllipse(mouthPen, bodyX + 32 * scale, bodyY + 46 * scale, 8 * scale, 9 * scale);
            }
            else if (_state == AgentState.Stretching)
            {
                // Happy squinting eyes ^ ^
                g.DrawArc(mouthPen, bodyX + 18 * scale, bodyY + 32 * scale, 14 * scale, 8 * scale, 180, 180);
                g.DrawArc(mouthPen, bodyX + 40 * scale, bodyY + 32 * scale, 14 * scale, 8 * scale, 180, 180);

                // Smile
                g.DrawArc(mouthPen, bodyX + 28 * scale, bodyY + 41 * scale, 22 * scale, 12 * scale, 15, 150);
            }
            else if (_state == AgentState.Excited)
            {
                // Normal eyes
                g.FillEllipse(Brushes.White, bodyX + 20 * scale, bodyY + 28 * scale, eyeW, eyeH);
                g.FillEllipse(Brushes.White, bodyX + 42 * scale, bodyY + 28 * scale, eyeW, eyeH);

                // Tracking pupils
                float lookX = 0;
                float lookY = 0;
                var cursor = PointToClient(Cursor.Position);
                float mouseDx = cursor.X - Width / 2f;
                float mouseDy = cursor.Y - Height / 2f;
                float dist = (float)Math.Sqrt(mouseDx * mouseDx + mouseDy * mouseDy);

                if (dist < 320)
                {
                    lookX = (mouseDx / dist) * 3.5f * scale;
                    lookY = (mouseDy / dist) * 2.8f * scale;
                }
                else
                {
                    lookX = (_dx > 0 ? 1.8f : -1.8f) * scale;
                    lookY = 0;
                }

                g.FillEllipse(Brushes.Black, bodyX + 23.5f * scale + lookX, bodyY + 32.5f * scale + lookY, 6.5f * scale, 7.5f * scale);
                g.FillEllipse(Brushes.Black, bodyX + 45.5f * scale + lookX, bodyY + 32.5f * scale + lookY, 6.5f * scale, 7.5f * scale);

                // Huge happy pie smile mouth
                using var excitedMouthBrush = new SolidBrush(Color.FromArgb(140, 20, 20, 20));
                g.FillPie(excitedMouthBrush, bodyX + 24 * scale, bodyY + 38 * scale, 24 * scale, 18 * scale, 0, 180);
            }
            else
            {
                // Idle or Thinking or Walking
                g.FillEllipse(Brushes.White, bodyX + 20 * scale, bodyY + 28 * scale, eyeW, eyeH);
                g.FillEllipse(Brushes.White, bodyX + 42 * scale, bodyY + 28 * scale, eyeW, eyeH);

                float lookX = 0;
                float lookY = 0;
                var cursor = PointToClient(Cursor.Position);
                float mouseDx = cursor.X - Width / 2f;
                float mouseDy = cursor.Y - Height / 2f;
                float dist = (float)Math.Sqrt(mouseDx * mouseDx + mouseDy * mouseDy);

                if (dist < 320)
                {
                    lookX = (mouseDx / dist) * 3.5f * scale;
                    lookY = (mouseDy / dist) * 2.8f * scale;
                }
                else
                {
                    lookX = (_dx > 0 ? 1.8f : -1.8f) * scale;
                    lookY = 0;
                }

                g.FillEllipse(Brushes.Black, bodyX + 25 * scale + lookX, bodyY + 33 * scale + lookY, 5.5f * scale, 6.5f * scale);
                g.FillEllipse(Brushes.Black, bodyX + 47 * scale + lookX, bodyY + 33 * scale + lookY, 5.5f * scale, 6.5f * scale);

                if (_state == AgentState.Thinking)
                {
                    DrawThinkingDots(g, bodyX, bodyY, scale);
                }
                else
                {
                    g.DrawArc(mouthPen, bodyX + 28 * scale, bodyY + 41 * scale, 22 * scale, 12 * scale, 15, 150);
                }
            }

            // Draw Character Specific Accessories
            DrawAccessories(g, bodyX, bodyY, scale);

            // DRAW FEET (State-driven foot movement)
            using var footBrush = new SolidBrush(Color.FromArgb(110, 20, 20, 20));
            if (_state == AgentState.Sitting || _state == AgentState.Sleeping)
            {
                // Tucked-in static feet
                g.FillEllipse(footBrush, bodyX + 18 * scale, bodyY + bodyH - 6 * scale, 16 * scale, 8 * scale);
                g.FillEllipse(footBrush, bodyX + 38 * scale, bodyY + bodyH - 6 * scale, 16 * scale, 8 * scale);
            }
            else if (_state == AgentState.Walking || _state == AgentState.Climbing)
            {
                // Alternating foot steps
                var footOffset = (_step % 4 < 2 ? 3 * scale : -3 * scale);
                g.FillEllipse(footBrush, bodyX + 15 * scale + footOffset, bodyY + bodyH - 3 * scale, 22 * scale, 11 * scale);
                g.FillEllipse(footBrush, bodyX + 35 * scale - footOffset, bodyY + bodyH - 3 * scale, 22 * scale, 11 * scale);
            }
            else if (_state == AgentState.Running)
            {
                // Faster alternating foot steps
                var footOffset = (_step % 2 == 0 ? 5 * scale : -5 * scale);
                g.FillEllipse(footBrush, bodyX + 15 * scale + footOffset, bodyY + bodyH - 3 * scale, 22 * scale, 11 * scale);
                g.FillEllipse(footBrush, bodyX + 35 * scale - footOffset, bodyY + bodyH - 3 * scale, 22 * scale, 11 * scale);
            }
            else if (_state == AgentState.Flying)
            {
                // Floating feet
                var floatOffset = (float)Math.Sin(_step / 2.0) * 2f * scale;
                g.FillEllipse(footBrush, bodyX + 15 * scale, bodyY + bodyH - 3 * scale + floatOffset, 22 * scale, 11 * scale);
                g.FillEllipse(footBrush, bodyX + 35 * scale, bodyY + bodyH - 3 * scale - floatOffset, 22 * scale, 11 * scale);
            }
            else if (_state == AgentState.Jumping || _state == AgentState.Falling || _state == AgentState.Dragged)
            {
                // Dangling/hanging feet
                var swingOffset = (_state == AgentState.Dragged) ? (float)(Math.Sin(_step * 0.6) * 7f * scale) : 0f;
                g.FillEllipse(footBrush, bodyX + 16 * scale + swingOffset, bodyY + bodyH - 1 * scale, 18 * scale, 11 * scale);
                g.FillEllipse(footBrush, bodyX + 38 * scale - swingOffset, bodyY + bodyH - 1 * scale, 18 * scale, 11 * scale);
            }
            else
            {
                // Static feet (Idle, Thinking, Yawning, LookingAround, Stretching, Static)
                g.FillEllipse(footBrush, bodyX + 15 * scale, bodyY + bodyH - 3 * scale, 22 * scale, 11 * scale);
                g.FillEllipse(footBrush, bodyX + 35 * scale, bodyY + bodyH - 3 * scale, 22 * scale, 11 * scale);
            }
        }

        private void DrawAccessories(Graphics g, float bodyX, float bodyY, float scale)
        {
            var charName = Agent.CharacterName.ToLowerInvariant();
            if (charName.Contains("nova"))
            {
                // Hovering code brackets "[ ]"
                var hover = (float)Math.Sin(_step / 4.0) * 2f;
                using var font = new Font("Consolas", 10.5f * scale, FontStyle.Bold);
                using var brush = new SolidBrush(Color.FromArgb(220, 50, 180, 255));
                g.DrawString("[  ]", font, brush, bodyX + 21 * scale, bodyY - 14 * scale + hover);
            }
            else if (charName.Contains("sage"))
            {
                // Cute reading glasses
                using var glassesPen = new Pen(Color.FromArgb(170, 240, 240, 240), 1.5f * scale);
                g.DrawEllipse(glassesPen, bodyX + 17 * scale, bodyY + 25 * scale, 18 * scale, 18 * scale);
                g.DrawEllipse(glassesPen, bodyX + 39 * scale, bodyY + 25 * scale, 18 * scale, 18 * scale);
                g.DrawLine(glassesPen, bodyX + 35 * scale, bodyY + 34 * scale, bodyX + 39 * scale, bodyY + 34 * scale);
            }
            else if (charName.Contains("bolt"))
            {
                // Lighting bolt badge
                using var boltBrush = new SolidBrush(Color.Gold);
                var points = new[] {
                    new PointF(bodyX + 37 * scale, bodyY + 8 * scale),
                    new PointF(bodyX + 31 * scale, bodyY + 18 * scale),
                    new PointF(bodyX + 35 * scale, bodyY + 18 * scale),
                    new PointF(bodyX + 33 * scale, bodyY + 27 * scale),
                    new PointF(bodyX + 41 * scale, bodyY + 16 * scale),
                    new PointF(bodyX + 36 * scale, bodyY + 16 * scale)
                };
                g.FillPolygon(boltBrush, points);
            }
            else if (charName.Contains("lumi"))
            {
                // Hovering creative star
                var hover = (float)Math.Sin(_step / 4.0) * 2f;
                using var starBrush = new SolidBrush(Color.FromArgb(220, 245, 220, 90));
                var points = new[] {
                    new PointF(bodyX + 36 * scale, bodyY - 14 * scale + hover),
                    new PointF(bodyX + 39 * scale, bodyY - 8 * scale + hover),
                    new PointF(bodyX + 46 * scale, bodyY - 8 * scale + hover),
                    new PointF(bodyX + 40 * scale, bodyY - 4 * scale + hover),
                    new PointF(bodyX + 43 * scale, bodyY + 2 * scale + hover),
                    new PointF(bodyX + 36 * scale, bodyY - 2 * scale + hover),
                    new PointF(bodyX + 29 * scale, bodyY + 2 * scale + hover),
                    new PointF(bodyX + 32 * scale, bodyY - 4 * scale + hover),
                    new PointF(bodyX + 26 * scale, bodyY - 8 * scale + hover),
                    new PointF(bodyX + 33 * scale, bodyY - 8 * scale + hover)
                };
                g.FillPolygon(starBrush, points);
            }
        }

        private static void DrawThinkingDots(Graphics g, float bodyX, float bodyY, float scale)
        {
            using var dotBrush = new SolidBrush(Color.FromArgb(160, 20, 20, 20));
            g.FillEllipse(dotBrush, bodyX + 28 * scale, bodyY + 45 * scale, 5.2f * scale, 5.2f * scale);
            g.FillEllipse(dotBrush, bodyX + 38 * scale, bodyY + 45 * scale, 5.2f * scale, 5.2f * scale);
            g.FillEllipse(dotBrush, bodyX + 48 * scale, bodyY + 45 * scale, 5.2f * scale, 5.2f * scale);
        }

        private void DrawSleepParticles(Graphics g)
        {
            lock (_particles)
            {
                foreach (var p in _particles)
                {
                    using var font = new Font("Segoe UI", 10.5f * p.Scale, FontStyle.Bold);
                    using var brush = new SolidBrush(Color.FromArgb((int)p.Alpha, Agent.BodyColor));
                    g.DrawString(p.Text, font, brush, p.X, p.Y);
                }
            }
        }

        private void DrawNamePlate(Graphics g)
        {
            if (Agent == null) return;
            using var nameFont = new Font("Segoe UI", Size == CharacterSize.Small ? 8 : 9, FontStyle.Bold);
            using var textBrush = new SolidBrush(Color.FromArgb(240, 248, 255));
            var nameSize = g.MeasureString(Agent.CharacterName, nameFont);
            
            var scale = GetScale();
            
            float bob = 0;
            if (_state == AgentState.Walking || _state == AgentState.Climbing)
            {
                bob = (float)Math.Sin(_step / 2.0) * 3f * scale;
            }
            else if (_state == AgentState.Running)
            {
                bob = (float)Math.Sin(_step / 1.0) * 5f * scale;
            }
            else if (_state == AgentState.Excited)
            {
                bob = (float)Math.Sin(_step / 1.0) * 6f * scale;
            }
            else if (_state == AgentState.Jumping)
            {
                bob = -10f * scale;
            }
            else if (_state == AgentState.Falling)
            {
                bob = 2f * scale;
            }
            else if (_state == AgentState.Dragged)
            {
                bob = (float)Math.Sin(_step / 1.5) * 2f * scale;
            }

            var headY = GetAgentHeadY(scale, bob);
            var labelWidth = nameSize.Width + 16 * scale;
            var labelHeight = 20f * scale;
            var labelRect = new RectangleF((Width - labelWidth) / 2f, headY - 24f * scale, labelWidth, labelHeight);

            using var labelBrush = new SolidBrush(Color.FromArgb(210, 10, 18, 30));
            using var labelPen = new Pen(Color.FromArgb(180, Agent.BodyColor), 1.2f);
            using var labelPath = RoundedRect(labelRect, Size == CharacterSize.Small ? 6 : 8);
            g.FillPath(labelBrush, labelPath);
            g.DrawPath(labelPen, labelPath);
            
            // Center text inside labelRect
            float textX = labelRect.Left + (labelRect.Width - nameSize.Width) / 2f;
            float textY = labelRect.Top + (labelRect.Height - nameSize.Height) / 2f;
            g.DrawString(Agent.CharacterName, nameFont, textBrush, textX, textY);
        }

        private void DrawSpeechBubble(Graphics g)
        {
            if (string.IsNullOrEmpty(_speechText) || GetTickCount() > _speechEndTime) return;

            using var bubbleFont = new Font("Segoe UI", 9, FontStyle.Bold);
            var textSize = g.MeasureString(_speechText, bubbleFont);
            
            float maxW = 120;
            if (textSize.Width > maxW)
            {
                textSize = g.MeasureString(_speechText, bubbleFont, (int)maxW);
            }

            float bubbleW = textSize.Width + 16;
            float bubbleH = textSize.Height + 12;
            
            float bubbleX = (Width - bubbleW) / 2f;
            
            var scale = GetScale();
            
            float bob = 0;
            if (_state == AgentState.Walking || _state == AgentState.Climbing)
            {
                bob = (float)Math.Sin(_step / 2.0) * 3f * scale;
            }
            else if (_state == AgentState.Running)
            {
                bob = (float)Math.Sin(_step / 1.0) * 5f * scale;
            }
            else if (_state == AgentState.Excited)
            {
                bob = (float)Math.Sin(_step / 1.0) * 6f * scale;
            }
            else if (_state == AgentState.Jumping)
            {
                bob = -10f * scale;
            }
            else if (_state == AgentState.Falling)
            {
                bob = 2f * scale;
            }
            else if (_state == AgentState.Dragged)
            {
                bob = (float)Math.Sin(_step / 1.5) * 2f * scale;
            }

            var headY = GetAgentHeadY(scale, bob);

            float bubbleY = Math.Max(2, headY - bubbleH - 8 * scale);

            var bubbleRect = new RectangleF(bubbleX, bubbleY, bubbleW, bubbleH);
            
            using var bgBrush = new SolidBrush(Color.FromArgb(240, 255, 255, 255));
            using var borderPen = new Pen(Color.FromArgb(200, Agent.BodyColor), 1.5f);
            using var path = RoundedRect(bubbleRect, 6);
            
            float triX = Width / 2f;
            float triY = bubbleY + bubbleH;
            
            g.FillPath(bgBrush, path);
            
            PointF[] triPoints = new[]
            {
                new PointF(triX - 6, triY),
                new PointF(triX + 6, triY),
                new PointF(triX, triY + 6)
            };
            g.FillPolygon(bgBrush, triPoints);
            
            g.DrawPath(borderPen, path);
            g.DrawLine(borderPen, triX - 6, triY, triX, triY + 6);
            g.DrawLine(borderPen, triX + 6, triY, triX, triY + 6);

            using var textBrush = new SolidBrush(Color.FromArgb(15, 23, 42));
            g.DrawString(_speechText, bubbleFont, textBrush, bubbleRect, new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            });
        }

        private bool IsLocationOccupied(int nextX, int nextY)
        {
            var myProposedBounds = new Rectangle(nextX, nextY, Width, Height);
            myProposedBounds.Inflate(5, 5);

            foreach (var other in ActiveAgents)
            {
                if (other == this) continue;
                if (!other.Visible) continue;

                if (myProposedBounds.IntersectsWith(other.Bounds))
                {
                    int curDist = Math.Abs(Left - other.Left) + Math.Abs(Top - other.Top);
                    int nextDist = Math.Abs(nextX - other.Left) + Math.Abs(nextY - other.Top);
                    if (nextDist < curDist)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public void UpdateFromAgent()
        {
            _size = Agent.Size;
            UpdateSize();

            // Dynamic animation scaling
            try
            {
                var config = AppConfig.Load();
                double intensity = config.AnimationIntensity;
                if (intensity <= 0.05)
                {
                    _walkTimer.Stop();
                    _frameTimer.Stop();
                }
                else
                {
                    _walkTimer.Interval = (int)(90 / intensity);
                    _frameTimer.Interval = (int)(220 / intensity);
                    if (!_walkTimer.Enabled && !_isPaused && !_isThinking) _walkTimer.Start();
                    if (!_frameTimer.Enabled) _frameTimer.Start();
                }
            }
            catch { }

            if (Agent.MovementType == "RestrictedTerritory" || Agent.MovementType == "FreeWindow")
            {
                var bounds = _walkBoundsProvider();
                int feetY = Top + Height;
                int centerX = Left + Width / 2;
                if (centerX < bounds.Left || centerX > bounds.Right || feetY < bounds.Top || feetY > bounds.Bottom)
                {
                    _isReturningHome = true;
                    _outsideExplorationEndTime = 0;
                    SetSpeechBubble("Walking to my territory...", 2000);
                }
            }
            else
            {
                _isReturningHome = false;
            }

            _crawlerState = CrawlerState.FreeWalk;
            UpdateActivity();
            Redraw();
        }

        private void Walk()
        {
            _step++;

            if (_isDragging)
            {
                _state = AgentState.Dragged;
                _crawlerState = CrawlerState.FreeWalk;
                _velocityY = 0;
                _velocityX = 0;
                _currentFrameIndex++;
                Redraw();
                return;
            }

            // Real-time window riding and sliding physics
            if (_currentCrawledWindow != IntPtr.Zero)
            {
                if (GetWindowRect(_currentCrawledWindow, out RECT currentRect))
                {
                    if (_trackingWindowHandle == _currentCrawledWindow)
                    {
                        int deltaX = currentRect.Left - _lastWindowRect.Left;
                        int deltaY = currentRect.Top - _lastWindowRect.Top;
                        if (deltaX != 0 || deltaY != 0)
                        {
                            this.Location = new Point(this.Left + deltaX, this.Top + deltaY);
                            
                            double speed = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
                            if (speed > Agent.SlideOffSpeedThreshold) // Rapid window drag
                            {
                                if (_random.NextDouble() < Agent.SlideOffProbability)
                                {
                                    _crawlerState = CrawlerState.FallingPhysical;
                                    _velocityY = 2f;
                                    _velocityX = (float)(deltaX * 0.4f);
                                    _currentCrawledWindow = IntPtr.Zero;
                                    _currentSurface = null;
                                    _trackingWindowHandle = IntPtr.Zero;
                                    var slips = new[] { "Whoaaa!", "Aaaah!", "Slipped!", "Hold on- *fall*", "Too fast!" };
                                    SetSpeechBubble(slips[_random.Next(slips.Length)], 2000);
                                }
                                else // 60% grab edge and lose balance / dizzy pause
                                {
                                    _isPaused = true;
                                    _pauseEndTime = GetTickCount() + 1500;
                                    _state = AgentState.LookingAround;
                                    var dizzy = new[] { "Whoa, dizzy!", "Hold on tight!", "Steady...", "Whoops!", "Catching my breath!" };
                                    SetSpeechBubble(dizzy[_random.Next(dizzy.Length)], 1500);
                                }
                            }
                        }
                    }
                    else
                    {
                        _trackingWindowHandle = _currentCrawledWindow;
                    }
                    _lastWindowRect = currentRect;
                }
                else
                {
                    // Window closed/disappeared
                    _crawlerState = CrawlerState.FallingPhysical;
                    _velocityY = 1f;
                    _velocityX = 0f;
                    _currentCrawledWindow = IntPtr.Zero;
                    _currentSurface = null;
                    _trackingWindowHandle = IntPtr.Zero;
                }
            }
            else
            {
                _trackingWindowHandle = IntPtr.Zero;
            }

            // Proximity checks
            Point cursorAbsolute = Cursor.Position;
            Point formCenter = new Point(Left + Width / 2, Top + Height / 2);
            int cdx = cursorAbsolute.X - formCenter.X;
            int cdy = cursorAbsolute.Y - formCenter.Y;
            double cDist = Math.Sqrt(cdx * cdx + cdy * cdy);

            if (cDist < 150)
            {
                if (!_isThinking && !_isReturningHome && _crawlerState != CrawlerState.FallingPhysical && !_isHandshaking)
                {
                    if (!_hasPlayedExcitedNearMouse)
                    {
                        _excitedEndTime = GetTickCount() + 3500;
                        _hasPlayedExcitedNearMouse = true;
                        
                        // Wake up if inactive/sleeping
                        if (_isSleeping || (GetTickCount() - _lastActivityTime > 60000))
                        {
                            UpdateActivity();
                        }
                        
                        // Small hop!
                        if (Agent.MovementType == "FreeRoam" || Agent.MovementType == "RestrictedTerritory" || Agent.MovementType == "FreeWindow" || Agent.MovementType == "FreeCrawler" || Agent.MovementType == "WindowCrawler")
                        {
                            _velocityY = -8f;
                            _crawlerState = CrawlerState.FallingPhysical;
                        }
                        
                        var waveSayings = new[] { "Hello! o/*", "Hey! *waves*", "Hi there!", "Welcome back!", "*happy dance*" };
                        SetSpeechBubble(waveSayings[_random.Next(waveSayings.Length)], 2000);
                    }
                }
            }
            else if (cDist >= 180)
            {
                _hasPlayedExcitedNearMouse = false;
            }

            if (IsAutonomousGroupMode)
            {
                ExecuteCircleFormation();
                _state = AgentState.Walking;
                _currentFrameIndex++;
                Redraw();
                return;
            }

            // State assignment based on active conditions and inactivity timer
            double inactiveTime = GetTickCount() - _lastActivityTime;

            if (_isThinking)
            {
                _state = AgentState.Thinking;
            }
            else if (GetTickCount() < _stretchEndTime)
            {
                _state = AgentState.Stretching;
            }
            else if (GetTickCount() < _excitedEndTime)
            {
                _state = AgentState.Excited;
            }
            else if (_isHandshaking)
            {
                _state = AgentState.Excited;
            }
            else if (_crawlerState == CrawlerState.FallingPhysical)
            {
                _state = AgentState.Falling;
            }
            else if (_crawlerState == CrawlerState.CrawlingLeftBorder || _crawlerState == CrawlerState.CrawlingRightBorder)
            {
                _state = AgentState.Climbing;
            }
            else if (_isReturningHome)
            {
                _state = AgentState.Running;
            }
            else if (inactiveTime > 75000)
            {
                _state = AgentState.Sleeping;
                _isSleeping = true;
            }
            else if (inactiveTime > 70000)
            {
                _state = AgentState.Sitting;
                _isSleeping = false;
            }
            else if (inactiveTime > 65000)
            {
                _state = AgentState.LookingAround;
                _isSleeping = false;
            }
            else if (inactiveTime > 60000)
            {
                _state = AgentState.Yawning;
                _isSleeping = false;
            }
            else if (_isPaused)
            {
                if (_isThudRecovery)
                {
                    _state = AgentState.Sitting;
                }
                else if (_isRollRecovery)
                {
                    _state = AgentState.Stretching;
                }
                else
                {
                    _state = AgentState.Idle;
                }
            }
            else if (Agent.MovementType == "Static")
            {
                _state = AgentState.Idle;
            }
            else if (Agent.MovementType == "Flying")
            {
                _state = AgentState.Flying;
            }
            else
            {
                _state = AgentState.Walking;
            }

            // Handle sleep Zzz particles
            if (_state == AgentState.Sleeping)
            {
                UpdateSleepParticles();
                _currentFrameIndex++;
                Redraw();
                return;
            }

            // Handle yawn speech bubble trigger once
            if (_state == AgentState.Yawning && (string.IsNullOrEmpty(_speechText) || GetTickCount() > _speechEndTime))
            {
                SetSpeechBubble("Yawn...", 1500);
            }

            // Non-moving states skip movement execution
            if (_state == AgentState.Sitting || _state == AgentState.Yawning || 
                _state == AgentState.LookingAround || _state == AgentState.Idle || 
                _state == AgentState.Stretching)
            {
                lock (_particles)
                {
                    _particles.Clear();
                }

                if (_isPaused && GetTickCount() >= _pauseEndTime)
                {
                    _isPaused = false;
                    _isThudRecovery = false;
                    _isRollRecovery = false;
                }

                _currentFrameIndex++;
                Redraw();
                return;
            }

            // Handle handshake logic
            if (_isHandshaking)
            {
                if (GetTickCount() > _handshakeEndTime)
                {
                    _isHandshaking = false;
                    _pauseEndTime = GetTickCount() + 1500;
                    _isPaused = true;
                    _dx *= -1;
                    _dy *= -1;
                }
                _currentFrameIndex++;
                Redraw();
                return;
            }

            // Multi-Agent collision check
            if (GetTickCount() > _collisionCooldown)
            {
                foreach (var other in ActiveAgents)
                {
                    if (other == this) continue;
                    if (!other.Visible || other._isSleeping || other._isThinking || other._isHandshaking) continue;

                    if (this.Bounds.IntersectsWith(other.Bounds))
                    {
                        TriggerHandshakeCollision(other);
                        break;
                    }
                }
            }

            // Territory tracking & out-of-bounds exploration for FreeWindow mode
            if (Agent.MovementType == "RestrictedTerritory" || Agent.MovementType == "FreeWindow")
            {
                var curRect = new Rectangle(Agent.WalkArea.X, Agent.WalkArea.Y, Agent.WalkArea.Width, Agent.WalkArea.Height);
                var tBounds = _walkBoundsProvider();
                int feetY = Top + Height;
                int centerX = Left + Width / 2;

                if (_lastWalkAreaRect != curRect)
                {
                    _lastWalkAreaRect = curRect;
                    if (centerX < tBounds.Left || centerX > tBounds.Right || feetY < tBounds.Top || feetY > tBounds.Bottom)
                    {
                        _isReturningHome = true;
                        _outsideExplorationEndTime = 0;
                        var homeMoved = new[] { "My territory changed!", "Heading to new zone!", "Walking home...", "Moving to my new space!" };
                        SetSpeechBubble(homeMoved[_random.Next(homeMoved.Length)], 2000);
                    }
                }

                bool isOutside = (centerX < tBounds.Left || centerX > tBounds.Right || feetY < tBounds.Top || feetY > tBounds.Bottom);
                
                if (isOutside && !_isReturningHome)
                {
                    if (Agent.MovementType == "RestrictedTerritory")
                    {
                        _isReturningHome = true;
                        _outsideExplorationEndTime = 0;
                        var returnBubble = new[] { "Time to head back to my territory!", "Wandering back home.", "Going back home!" };
                        SetSpeechBubble(returnBubble[_random.Next(returnBubble.Length)], 2000);
                    }
                    else
                    {
                        if (_outsideExplorationEndTime == 0)
                        {
                            _outsideExplorationEndTime = GetTickCount() + 30000; // 30 seconds
                            _hasAlertedOutside = false;
                        }

                        if (GetTickCount() >= _outsideExplorationEndTime)
                        {
                            _isReturningHome = true;
                            _outsideExplorationEndTime = 0;
                            var realizeBubble = new[] { "Wait, this isn't my territory...", "Time to go home!", "I should head back.", "Wandering back home." };
                            SetSpeechBubble(realizeBubble[_random.Next(realizeBubble.Length)], 2000);
                        }
                        else
                        {
                            // Check if we should alert that we are lost (e.g. 5 seconds before going home)
                            if (!_hasAlertedOutside && _outsideExplorationEndTime - GetTickCount() < 5000)
                            {
                                _hasAlertedOutside = true;
                                SetSpeechBubble("Where am I? Let me look around...", 2000);
                                _isPaused = true;
                                _pauseEndTime = GetTickCount() + 2000;
                                _state = AgentState.LookingAround;
                            }
                        }
                    }
                }
                else if (!isOutside)
                {
                    _outsideExplorationEndTime = 0;
                    _hasAlertedOutside = false;
                }
            }

            // Execution path for movement profiles
            if (_isReturningHome)
            {
                ExecuteReturnHomeMovement();
            }
            else if (Agent.MovementType == "RestrictedTerritory" || Agent.MovementType == "FreeWindow")
            {
                var tBounds = _walkBoundsProvider();
                int feetY = Top + Height;
                int centerX = Left + Width / 2;
                bool isOutside = (centerX < tBounds.Left || centerX > tBounds.Right || feetY < tBounds.Top || feetY > tBounds.Bottom);
                if (isOutside)
                {
                    ExecuteFreeCrawlerMovement();
                }
                else
                {
                    ExecuteFreeWindowMovement();
                }
            }
            else if (Agent.MovementType == "FreeRoam")
            {
                ExecuteFreeCrawlerMovement();
            }
            else if (Agent.MovementType == "WindowCrawler")
            {
                ExecuteCrawlerMovement();
            }
            else if (Agent.MovementType == "FreeCrawler")
            {
                ExecuteFreeCrawlerMovement();
            }
            else if (Agent.MovementType == "GroundOnly")
            {
                ExecuteGroundOnlyMovement();
            }
            else if (Agent.MovementType == "Flying")
            {
                ExecuteFlyingMovement();
            }
            else
            {
                ExecuteFreeWalkMovement();
            }

            // Absolute screen bounds clamp to ensure agent never goes off-screen
            var screen = Screen.FromControl(this)?.Bounds ?? Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
            int minX = screen.Left;
            int maxX = screen.Right - Width;
            int minY = screen.Top;
            int maxY = screen.Bottom - Height;

            if (Left < minX) { Left = minX; _dx = Math.Abs(_dx); _velocityX = Math.Abs(_velocityX); }
            if (Left > maxX) { Left = maxX; _dx = -Math.Abs(_dx); _velocityX = -Math.Abs(_velocityX); }
            if (Top < minY) { Top = minY; _velocityY = 0; }
            if (Top > maxY) { Top = maxY; _velocityY = 0; }

            _currentFrameIndex++;
            Redraw();
        }

        private void ExecuteFreeWalkMovement()
        {
            var bounds = _walkBoundsProvider();
            var speed = Size == CharacterSize.Large ? 1.5f : (Size == CharacterSize.Small ? 0.8f : 1.0f);

            var nextX = Left + (int)(_dx * speed);
            var nextY = Top + (int)(_dy * speed);

            if (IsLocationOccupied(nextX, nextY))
            {
                _dx = -_dx;
                _dy = -_dy;
                _pauseEndTime = GetTickCount() + RandomDouble(1500, 4000);
                _isPaused = true;
                return;
            }

            if (nextX < bounds.Left)
            {
                _dx = Math.Abs(_dx == 0 ? 1 : _dx);
                nextX = bounds.Left;
                _pauseEndTime = GetTickCount() + RandomDouble(2000, 6000);
                _isPaused = true;
            }
            else if (nextX + Width > bounds.Right)
            {
                _dx = -Math.Abs(_dx == 0 ? 1 : _dx);
                nextX = bounds.Right - Width;
                _pauseEndTime = GetTickCount() + RandomDouble(2000, 6000);
                _isPaused = true;
            }

            if (nextY < bounds.Top)
            {
                _dy = Math.Abs(_dy == 0 ? 1 : _dy);
                nextY = bounds.Top;
                _pauseEndTime = GetTickCount() + RandomDouble(2000, 6000);
                _isPaused = true;
            }
            else if (nextY + Height > bounds.Bottom)
            {
                _dy = -Math.Abs(_dy == 0 ? 1 : _dy);
                nextY = bounds.Bottom - Height;
                _pauseEndTime = GetTickCount() + RandomDouble(2000, 6000);
                _isPaused = true;
            }

            if (_random.NextDouble() < 0.008)
            {
                _dx = _random.Next(-2, 3);
                _dy = _random.Next(-1, 2);
                if (_dx == 0 && _dy == 0)
                {
                    _dx = 1;
                }
                _pauseEndTime = GetTickCount() + RandomDouble(3000, 8000);
                _isPaused = true;
            }

            Location = new Point(nextX, nextY);
        }

        private void ExecuteReturnHomeMovement()
        {
            var bounds = _walkBoundsProvider();
            
            // Find nearest coordinate inside the walk zone
            int targetX = Math.Clamp(Left, bounds.Left, Math.Max(bounds.Left, bounds.Right - Width));
            int targetY = Math.Clamp(Top, bounds.Top, Math.Max(bounds.Top, bounds.Bottom - Height));

            if (Left == targetX && Top == targetY)
            {
                _isReturningHome = false;
                _velocityX = 0;
                _velocityY = 0;
                if (Agent.MovementType == "RestrictedTerritory" || Agent.MovementType == "FreeWindow")
                {
                    _isPaused = true;
                    _pauseEndTime = GetTickCount() + 2500;
                    _state = AgentState.Excited;
                    var greetings = new[] { "I'm back!", "Back home!", "Ah, safe!", "Home sweet home!" };
                    SetSpeechBubble(greetings[_random.Next(greetings.Length)], 2000);
                }
                else
                {
                    var greetings = new[] { "I'm home!", "Back in bounds!", "Home sweet home!", "Ah, safe!" };
                    SetSpeechBubble(greetings[_random.Next(greetings.Length)], 1800);
                }
            }
            else
            {
                int diffX = targetX - Left;
                int diffY = targetY - Top;
                float dist = (float)Math.Sqrt(diffX * diffX + diffY * diffY);
                float speed = 4.5f * (Size == CharacterSize.Large ? 1.5f : (Size == CharacterSize.Small ? 0.8f : 1.0f));

                if (dist > 0)
                {
                    int moveX = (int)Math.Round((diffX / dist) * speed);
                    int moveY = (int)Math.Round((diffY / dist) * speed);

                    if (Math.Abs(moveX) < 1 && diffX != 0) moveX = Math.Sign(diffX);
                    if (Math.Abs(moveY) < 1 && diffY != 0) moveY = Math.Sign(diffY);

                    Left += moveX;
                    Top += moveY;
                    _dx = moveX > 0 ? 2 : -2;
                }
            }
        }

        private void ExecuteGroundOnlyMovement()
        {
            var bounds = _walkBoundsProvider();
            
            // Lock Y to bottom of bounds
            Top = bounds.Bottom - Height;

            var speed = Size == CharacterSize.Large ? 1.5f : (Size == CharacterSize.Small ? 0.8f : 1.0f);
            var nextX = Left + (int)(_dx * speed);

            if (nextX < bounds.Left)
            {
                _dx = Math.Abs(_dx == 0 ? 1 : _dx);
                nextX = bounds.Left;
                _pauseEndTime = GetTickCount() + RandomDouble(2000, 6000);
                _isPaused = true;
            }
            else if (nextX + Width > bounds.Right)
            {
                _dx = -Math.Abs(_dx == 0 ? 1 : _dx);
                nextX = bounds.Right - Width;
                _pauseEndTime = GetTickCount() + RandomDouble(2000, 6000);
                _isPaused = true;
            }

            if (_random.NextDouble() < 0.008)
            {
                _dx = _random.Next(-2, 3);
                if (_dx == 0) _dx = 1;
                _pauseEndTime = GetTickCount() + RandomDouble(3000, 8000);
                _isPaused = true;
            }

            Left = nextX;
        }

        private void ExecuteFlyingMovement()
        {
            var bounds = _walkBoundsProvider();
            var speed = 2.5f * (Size == CharacterSize.Large ? 1.4f : (Size == CharacterSize.Small ? 0.75f : 1.0f));

            int nextX = Left + (int)(_dx * speed);
            int nextY = Top + (int)(_dy * speed);

            if (nextX < bounds.Left)
            {
                _dx = Math.Abs(_dx == 0 ? 2 : _dx);
                nextX = bounds.Left;
            }
            else if (nextX + Width > bounds.Right)
            {
                _dx = -Math.Abs(_dx == 0 ? 2 : _dx);
                nextX = bounds.Right - Width;
            }

            if (nextY < bounds.Top)
            {
                _dy = Math.Abs(_dy == 0 ? 2 : _dy);
                nextY = bounds.Top;
            }
            else if (nextY + Height > bounds.Bottom)
            {
                _dy = -Math.Abs(_dy == 0 ? 2 : _dy);
                nextY = bounds.Bottom - Height;
            }

            if (_random.NextDouble() < 0.015)
            {
                _dx = _random.Next(0, 2) == 0 ? 2 : -2;
                _dy = _random.Next(0, 2) == 0 ? 2 : -2;
            }

            Location = new Point(nextX, nextY);
        }

        private void ExecuteFreeCrawlerMovement()
        {
            var surfaces = GetWalkableSurfaces();
            ExecuteCrawlerLogic(surfaces);
        }

        private void ExecuteFreeWindowMovement()
        {
            var bounds = _walkBoundsProvider(); // Home Territory
            var rawSurfaces = GetWalkableSurfaces();
            var clippedSurfaces = new List<WalkableSurface>();

            foreach (var s in rawSurfaces)
            {
                Rectangle rect;
                if (s.SurfaceType == "Desktop")
                {
                    // Ground of the territory is at the bottom of the territory bounds
                    rect = new Rectangle(bounds.Left, bounds.Bottom - 5, bounds.Width, 10);
                }
                else
                {
                    // Skip the window entirely if its actual title bar is above bounds.Top,
                    // to prevent the agent from attempting to climb an unreachable window.
                    if (s.Rect.Top < bounds.Top)
                    {
                        continue;
                    }
                    rect = Rectangle.Intersect(s.Rect, bounds);
                }

                if (rect.Width >= 20 && rect.Height >= 2)
                {
                    clippedSurfaces.Add(new WalkableSurface
                    {
                        Handle = s.Handle,
                        Rect = rect,
                        SurfaceType = s.SurfaceType
                    });
                }
            }

            ExecuteCrawlerLogic(clippedSurfaces);
        }

        private void ExecuteCrawlerMovement()
        {
            var surfaces = new List<WalkableSurface>();
            
            // Add desktop floor
            var screenBounds = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
            surfaces.Add(new WalkableSurface
            {
                Handle = IntPtr.Zero,
                Rect = new Rectangle(screenBounds.Left, screenBounds.Bottom - 5, screenBounds.Width, 10),
                SurfaceType = "Desktop"
            });

            // Add active window only
            IntPtr activeHwnd = GetActiveWindowToCrawl();
            if (activeHwnd != IntPtr.Zero && GetWindowRect(activeHwnd, out RECT r))
            {
                if (r.Width > 150 && r.Height > 50 && r.Top > -10000 && r.Left > -10000)
                {
                    surfaces.Add(new WalkableSurface
                    {
                        Handle = activeHwnd,
                        Rect = new Rectangle(r.Left, r.Top, r.Width, r.Height),
                        SurfaceType = "WindowTitleBar"
                    });
                }
            }

            ExecuteCrawlerLogic(surfaces);
        }

        private void ExecuteCrawlerLogic(List<WalkableSurface> surfaces)
        {
            var screenBounds = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
            var bounds = (Agent.MovementType == "RestrictedTerritory" || Agent.MovementType == "FreeWindow") ? _walkBoundsProvider() : screenBounds;
            
            // Screen/Territory boundaries check to prevent walking off bounds completely
            int limitLeft = bounds.Left;
            int limitRight = bounds.Right;
            if (Left < limitLeft) { Left = limitLeft; _dx = Math.Abs(_dx == 0 ? 1 : _dx); _velocityX = Math.Abs(_velocityX); }
            if (Right > limitRight) { Left = limitRight - Width; _dx = -Math.Abs(_dx == 0 ? 1 : _dx); _velocityX = -Math.Abs(_velocityX); }

            if (_crawlerState == CrawlerState.FallingPhysical)
            {
                _velocityY += Gravity;
                int nextX = Left + (int)_velocityX;
                int nextY = Top + (int)_velocityY;

                // Check collision with bottom boundary
                int floorBottom = bounds.Bottom;
                if (nextY + Height >= floorBottom)
                {
                    nextY = floorBottom - Height;
                    _crawlerState = CrawlerState.FreeWalk;
                    
                    if (_velocityY > Agent.ThudVelocityThreshold)
                    {
                        _isPaused = true;
                        _pauseEndTime = GetTickCount() + Agent.ThudRecoveryDuration;
                        _isThudRecovery = true;
                        _isRollRecovery = false;
                        var splats = new[] { "*Splat!*", "*Thud!*", "Ouch! *flat*", "Ugh! *splat*" };
                        SetSpeechBubble(splats[_random.Next(splats.Length)], Agent.ThudRecoveryDuration);
                    }
                    else if (_velocityY > Agent.RollVelocityThreshold)
                    {
                        _isPaused = true;
                        _pauseEndTime = GetTickCount() + Agent.RollRecoveryDuration;
                        _isThudRecovery = false;
                        _isRollRecovery = true;
                        var rolls = new[] { "*Roll-recovery!*", "Safe!", "Whew! *roll*", "Ta-da!" };
                        SetSpeechBubble(rolls[_random.Next(rolls.Length)], 1200);
                    }
                    
                    _velocityY = 0;
                    _velocityX = 0;
                    _currentCrawledWindow = IntPtr.Zero;
                    _currentSurface = surfaces.FirstOrDefault(s => s.SurfaceType == "Desktop");
                }
                else
                {
                    // Check landing on any walkable surface
                    foreach (var s in surfaces)
                    {
                        if (_velocityY > 0) // falling down
                        {
                            if (nextX + Width / 2 >= s.Rect.Left && nextX + Width / 2 <= s.Rect.Right)
                            {
                                if (nextY + Height >= s.Rect.Top && Top + Height - _velocityY <= s.Rect.Top + 15)
                                {
                                    nextY = s.Rect.Top - Height + 5;
                                    _crawlerState = (s.SurfaceType == "Desktop") ? CrawlerState.FreeWalk : CrawlerState.WalkingTitleBar;
                                    
                                    if (_velocityY > Agent.ThudVelocityThreshold)
                                    {
                                        _isPaused = true;
                                        _pauseEndTime = GetTickCount() + Agent.ThudRecoveryDuration;
                                        _isThudRecovery = true;
                                        _isRollRecovery = false;
                                        var splats = new[] { "*Splat!*", "*Thud!*", "Ouch! *flat*", "Ugh! *splat*" };
                                        SetSpeechBubble(splats[_random.Next(splats.Length)], Agent.ThudRecoveryDuration);
                                    }
                                    else if (_velocityY > Agent.RollVelocityThreshold)
                                    {
                                        _isPaused = true;
                                        _pauseEndTime = GetTickCount() + Agent.RollRecoveryDuration;
                                        _isThudRecovery = false;
                                        _isRollRecovery = true;
                                        var rolls = new[] { "*Roll-recovery!*", "Safe!", "Whew! *roll*", "Ta-da!" };
                                        SetSpeechBubble(rolls[_random.Next(rolls.Length)], 1200);
                                    }

                                    _velocityY = 0;
                                    _velocityX = 0;
                                    _currentCrawledWindow = s.Handle;
                                    _currentSurface = s;
                                    break;
                                }
                            }
                        }
                    }
                }
                Location = new Point(nextX, nextY);
                return;
            }

            if (_crawlerState == CrawlerState.CrawlingLeftBorder || _crawlerState == CrawlerState.CrawlingRightBorder)
            {
                WalkableSurface? currentWindow = null;
                foreach (var s in surfaces)
                {
                    if (s.Handle == _currentCrawledWindow && s.SurfaceType == "WindowTitleBar")
                    {
                        currentWindow = s;
                        break;
                    }
                }

                if (currentWindow == null)
                {
                    // Window disappeared, fall!
                    _crawlerState = CrawlerState.FallingPhysical;
                    _velocityY = 1f;
                    _velocityX = _dx;
                    _currentSurface = null;
                    return;
                }

                int nextX = (_crawlerState == CrawlerState.CrawlingLeftBorder) ? 
                            (currentWindow.Rect.Left - Width / 2) : 
                            (currentWindow.Rect.Right - Width / 2);
                int nextY = Top + _dy;

                // If climbing down and reach bottom
                if (nextY + Height >= currentWindow.Rect.Bottom)
                {
                    _crawlerState = CrawlerState.FallingPhysical;
                    _velocityY = 1f;
                    _velocityX = _dx * 1.5f;
                    _currentSurface = null;
                }
                // Safety check: if climbing up and blocked by the top of the screen (or territory top), abort and fall
                else if (_dy < 0 && nextY <= bounds.Top)
                {
                    _crawlerState = CrawlerState.FallingPhysical;
                    _velocityY = 1f;
                    _velocityX = -_dx * 1.5f;
                    _currentSurface = null;
                }
                // If climbing up and reach top
                else if (nextY <= currentWindow.Rect.Top - Height + 5)
                {
                    _crawlerState = CrawlerState.WalkingTitleBar;
                    _currentSurface = currentWindow;
                    nextY = currentWindow.Rect.Top - Height + 5;
                    _dx = (_crawlerState == CrawlerState.CrawlingLeftBorder) ? 2 : -2; // walk inwards
                }

                Location = new Point(nextX, nextY);
                return;
            }

            if (_crawlerState == CrawlerState.WalkingTitleBar)
            {
                // Validate current window surface still exists
                WalkableSurface? currentWindow = null;
                foreach (var s in surfaces)
                {
                    if (s.Handle == _currentCrawledWindow && s.SurfaceType == "WindowTitleBar")
                    {
                        currentWindow = s;
                        break;
                    }
                }

                if (currentWindow == null)
                {
                    _crawlerState = CrawlerState.FallingPhysical;
                    _velocityY = 1f;
                    _velocityX = _dx;
                    _currentSurface = null;
                    return;
                }

                int speed = 2;
                int nextX = Left + (_dx > 0 ? speed : -speed);
                int nextY = currentWindow.Rect.Top - Height + 5;

                if (IsLocationOccupied(nextX, nextY))
                {
                    _dx = -_dx;
                    _pauseEndTime = GetTickCount() + RandomDouble(1500, 4000);
                    _isPaused = true;
                    return;
                }

                // Handle walking off left edge
                if (nextX + Width / 2 < currentWindow.Rect.Left)
                {
                    // Check JUMP: Is there another window nearby to jump to?
                    WalkableSurface? jumpTarget = null;
                    foreach (var s in surfaces)
                    {
                        if (s.Handle != currentWindow.Handle && s.SurfaceType == "WindowTitleBar")
                        {
                            if (currentWindow.Rect.Left - s.Rect.Right >= 0 && currentWindow.Rect.Left - s.Rect.Right < 180)
                            {
                                if (Math.Abs(s.Rect.Top - currentWindow.Rect.Top) < 120)
                                {
                                    jumpTarget = s;
                                    break;
                                }
                            }
                        }
                    }

                    if (jumpTarget != null) // Leap!
                    {
                        _crawlerState = CrawlerState.FallingPhysical;
                        _velocityY = -9.5f;
                        _velocityX = -3.8f;
                        _currentSurface = null;
                        var leaps = new[] { "Leap!", "Jump!", "Hup!", "Wheee!" };
                        SetSpeechBubble(leaps[_random.Next(leaps.Length)], 1500);
                    }
                    else
                    {
                        // Check DROP: What is below?
                        WalkableSurface? below = null;
                        foreach (var s in surfaces)
                        {
                            if (s.Rect.Top > currentWindow.Rect.Top + 10 &&
                                nextX + Width / 2 >= s.Rect.Left && nextX + Width / 2 <= s.Rect.Right)
                            {
                                if (below == null || s.Rect.Top < below.Rect.Top)
                                {
                                    below = s;
                                }
                            }
                        }

                        if (below != null)
                        {
                            int dropDist = below.Rect.Top - currentWindow.Rect.Top;
                            if (dropDist < 65) // Small drop, hop down
                            {
                                nextY = below.Rect.Top - Height + 5;
                                _currentSurface = below;
                                _crawlerState = (below.SurfaceType == "Desktop") ? CrawlerState.FreeWalk : CrawlerState.WalkingTitleBar;
                                var hops = new[] { "Hop!", "Boop!", "Step!" };
                                SetSpeechBubble(hops[_random.Next(hops.Length)], 1200);
                            }
                            else // Large drop, fall
                            {
                                _crawlerState = CrawlerState.FallingPhysical;
                                _velocityY = 1f;
                                _velocityX = -1.5f;
                                _currentSurface = null;
                            }
                        }
                        else
                        {
                            _crawlerState = CrawlerState.FallingPhysical;
                            _velocityY = 1f;
                            _velocityX = -1.5f;
                            _currentSurface = null;
                        }
                    }
                }
                // Handle walking off right edge
                else if (nextX + Width / 2 > currentWindow.Rect.Right)
                {
                    // Check JUMP: Is there another window nearby to jump to?
                    WalkableSurface? jumpTarget = null;
                    foreach (var s in surfaces)
                    {
                        if (s.Handle != currentWindow.Handle && s.SurfaceType == "WindowTitleBar")
                        {
                            if (s.Rect.Left - currentWindow.Rect.Right >= 0 && s.Rect.Left - currentWindow.Rect.Right < 180)
                            {
                                if (Math.Abs(s.Rect.Top - currentWindow.Rect.Top) < 120)
                                {
                                    jumpTarget = s;
                                    break;
                                }
                            }
                        }
                    }

                    if (jumpTarget != null) // Leap!
                    {
                        _crawlerState = CrawlerState.FallingPhysical;
                        _velocityY = -9.5f;
                        _velocityX = 3.8f;
                        _currentSurface = null;
                        var leaps = new[] { "Leap!", "Jump!", "Hup!", "Wheee!" };
                        SetSpeechBubble(leaps[_random.Next(leaps.Length)], 1500);
                    }
                    else
                    {
                        // Check DROP: What is below?
                        WalkableSurface? below = null;
                        foreach (var s in surfaces)
                        {
                            if (s.Rect.Top > currentWindow.Rect.Top + 10 &&
                                nextX + Width / 2 >= s.Rect.Left && nextX + Width / 2 <= s.Rect.Right)
                            {
                                if (below == null || s.Rect.Top < below.Rect.Top)
                                {
                                    below = s;
                                }
                            }
                        }

                        if (below != null)
                        {
                            int dropDist = below.Rect.Top - currentWindow.Rect.Top;
                            if (dropDist < 65) // Small drop, hop down
                            {
                                nextY = below.Rect.Top - Height + 5;
                                _currentSurface = below;
                                _crawlerState = (below.SurfaceType == "Desktop") ? CrawlerState.FreeWalk : CrawlerState.WalkingTitleBar;
                                var hops = new[] { "Hop!", "Boop!", "Step!" };
                                SetSpeechBubble(hops[_random.Next(hops.Length)], 1200);
                            }
                            else // Large drop, fall
                            {
                                _crawlerState = CrawlerState.FallingPhysical;
                                _velocityY = 1f;
                                _velocityX = 1.5f;
                                _currentSurface = null;
                            }
                        }
                        else
                        {
                            _crawlerState = CrawlerState.FallingPhysical;
                            _velocityY = 1f;
                            _velocityX = 1.5f;
                            _currentSurface = null;
                        }
                    }
                }

                Location = new Point(nextX, nextY);
            }
            else if (_crawlerState == CrawlerState.FreeWalk)
            {
                // Walking on desktop floor
                int speed = 2;
                int nextX = Left + (_dx > 0 ? speed : -speed);

                // OBSTACLE COLLISION DETECTION
                foreach (var w in surfaces)
                {
                    if (w.SurfaceType == "WindowTitleBar")
                    {
                        // Bumping into left side of window moving right
                        if (_dx > 0 && Math.Abs(Right - w.Rect.Left) < 8 && Top + Height > w.Rect.Top && Top < w.Rect.Bottom)
                        {
                            // Verify reachability: bottom of window must be near or below agent's feet level
                            if (w.Rect.Bottom >= Top + Height - 15)
                            {
                                if (Agent.EnableWindowPushing && w.Rect.Width < 450 && w.Rect.Height < 450)
                                {
                                    int pushAmount = 2;
                                    int newLeft = w.Rect.Left + pushAmount;
                                    if (newLeft + w.Rect.Width <= bounds.Right)
                                    {
                                        // Push external window!
                                        SetWindowPos(w.Handle, IntPtr.Zero, newLeft, w.Rect.Top, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
                                        
                                        // Update the WalkableSurface rect in the current tick list
                                        w.Rect = new Rectangle(newLeft, w.Rect.Top, w.Rect.Width, w.Rect.Height);
                                        
                                        // Update cached windows so subsequent ticks don't use stale positions
                                        for (int idx = 0; idx < _cachedWindows.Count; idx++)
                                        {
                                            if (_cachedWindows[idx].Handle == w.Handle)
                                            {
                                                var vw = _cachedWindows[idx];
                                                vw.Rect = w.Rect;
                                                _cachedWindows[idx] = vw;
                                                break;
                                            }
                                        }

                                        if (_random.NextDouble() < 0.15 && (string.IsNullOrEmpty(_speechText) || GetTickCount() > _speechEndTime))
                                        {
                                            var pushes = new[] { "Pushing... oof!", "Heavy!", "Get out of the way!", "Nudge nudge!", "Making space!" };
                                            SetSpeechBubble(pushes[_random.Next(pushes.Length)], 1200);
                                        }
                                        // Let the agent walk as normal, so we don't return early to climb
                                    }
                                    else
                                    {
                                        _dx = -2; // boundary hit, turn back
                                        return;
                                    }
                                }
                                else
                                {
                                    // Check window height limit: too tall?
                                    if (w.Rect.Height > 450 || w.Rect.Top - Height + 10 < bounds.Top)
                                    {
                                        // Too tall: refuse to climb! Look up and pause.
                                        _isPaused = true;
                                        _pauseEndTime = GetTickCount() + 2000;
                                        _state = AgentState.LookingAround;
                                        var refuses = new[] { "Too high!", "Whoa, big window!", "No way...", "?_?" };
                                        SetSpeechBubble(refuses[_random.Next(refuses.Length)], 2000);
                                        _dx = -2; // turn back
                                        return;
                                    }
                                    else
                                    {
                                        // Climb up!
                                        _crawlerState = CrawlerState.CrawlingLeftBorder;
                                        _dy = -2; // climb speed upwards
                                        _currentCrawledWindow = w.Handle;
                                        _currentSurface = null;
                                        Left = w.Rect.Left - Width / 2;
                                        return;
                                    }
                                }
                            }
                        }
                        // Bumping into right side of window moving left
                        else if (_dx < 0 && Math.Abs(Left - w.Rect.Right) < 8 && Top + Height > w.Rect.Top && Top < w.Rect.Bottom)
                        {
                            // Verify reachability
                            if (w.Rect.Bottom >= Top + Height - 15)
                            {
                                if (Agent.EnableWindowPushing && w.Rect.Width < 450 && w.Rect.Height < 450)
                                {
                                    int pushAmount = -2;
                                    int newLeft = w.Rect.Left + pushAmount;
                                    if (newLeft >= bounds.Left)
                                    {
                                        // Push external window!
                                        SetWindowPos(w.Handle, IntPtr.Zero, newLeft, w.Rect.Top, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
                                        
                                        // Update the WalkableSurface rect in the current tick list
                                        w.Rect = new Rectangle(newLeft, w.Rect.Top, w.Rect.Width, w.Rect.Height);
                                        
                                        // Update cached windows
                                        for (int idx = 0; idx < _cachedWindows.Count; idx++)
                                        {
                                            if (_cachedWindows[idx].Handle == w.Handle)
                                            {
                                                var vw = _cachedWindows[idx];
                                                vw.Rect = w.Rect;
                                                _cachedWindows[idx] = vw;
                                                break;
                                            }
                                        }

                                        if (_random.NextDouble() < 0.15 && (string.IsNullOrEmpty(_speechText) || GetTickCount() > _speechEndTime))
                                        {
                                            var pushes = new[] { "Pushing... oof!", "Heavy!", "Get out of the way!", "Nudge nudge!", "Making space!" };
                                            SetSpeechBubble(pushes[_random.Next(pushes.Length)], 1200);
                                        }
                                    }
                                    else
                                    {
                                        _dx = 2; // boundary hit, turn back
                                        return;
                                    }
                                }
                                else
                                {
                                    // Check window height limit: too tall?
                                    if (w.Rect.Height > 450 || w.Rect.Top - Height + 10 < bounds.Top)
                                    {
                                        // Too tall: refuse
                                        _isPaused = true;
                                        _pauseEndTime = GetTickCount() + 2000;
                                        _state = AgentState.LookingAround;
                                        var refuses = new[] { "Too high!", "Whoa, big window!", "No way...", "?_?" };
                                        SetSpeechBubble(refuses[_random.Next(refuses.Length)], 2000);
                                        _dx = 2; // turn back
                                        return;
                                    }
                                    else
                                    {
                                        // Climb up!
                                        _crawlerState = CrawlerState.CrawlingRightBorder;
                                        _dy = -2;
                                        _currentCrawledWindow = w.Handle;
                                        _currentSurface = null;
                                        Left = w.Rect.Right - Width / 2;
                                        return;
                                    }
                                }
                            }
                        }
                    }
                }

                ExecuteFreeWalkMovement();
            }
        }

        private void ExecuteCircleFormation()
        {
            var screenBounds = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
            
            // Find my index in the active agents
            var activeList = ActiveAgents.Where(a => a.Visible).ToList();
            int myIndex = activeList.IndexOf(this);
            if (myIndex < 0) return;

            // Wake up if sleeping
            if (_isSleeping)
            {
                _isSleeping = false;
                _particles.Clear();
            }
            
            // Stop crawling if crawling
            if (_crawlerState != CrawlerState.FreeWalk)
            {
                _crawlerState = CrawlerState.FreeWalk;
            }

            int totalAgents = activeList.Count;
            float radius = totalAgents > 1 ? Math.Max(150f, totalAgents * 40f) : 0f;
            
            // Center of the screen
            int centerX = screenBounds.Left + screenBounds.Width / 2;
            int centerY = screenBounds.Top + screenBounds.Height / 2;

            // Calculate my target angle
            double angle = (2 * Math.PI / totalAgents) * myIndex;
            
            // Let the circle slowly rotate for a dynamic feel
            double timeOffset = GetTickCount() / 5000.0;
            angle += timeOffset;

            int targetX = centerX + (int)(Math.Cos(angle) * radius) - (Width / 2);
            int targetY = centerY + (int)(Math.Sin(angle) * radius) - (Height / 2);

            // Move towards target smoothly
            int currentX = Left;
            int currentY = Top;
            
            float diffX = targetX - currentX;
            float diffY = targetY - currentY;
            float dist = (float)Math.Sqrt(diffX * diffX + diffY * diffY);

            if (dist > 5)
            {
                float speed = Size == CharacterSize.Large ? 3.5f : (Size == CharacterSize.Small ? 2.0f : 2.5f);
                // Move faster if further away
                if (dist > 300) speed *= 2f;
                
                int moveX = (int)((diffX / dist) * speed);
                int moveY = (int)((diffY / dist) * speed);
                
                // Update face direction based on movement or center
                if (dist > 50)
                {
                    _dx = moveX > 0 ? 2 : -2;
                }
                else
                {
                    // Face the center of the circle once arrived
                    _dx = centerX > (Left + Width / 2) ? 2 : -2;
                }

                Location = new Point(currentX + moveX, currentY + moveY);
            }
            else
            {
                // Already at destination, face center
                _dx = centerX > (Left + Width / 2) ? 2 : -2;
            }
        }

        private List<VisibleWindow> GetVisibleWindows()
        {
            double now = GetTickCount();
            if (now - _lastWindowScanTime < 1500 && _cachedWindows.Count > 0)
            {
                return _cachedWindows;
            }

            var list = new List<VisibleWindow>();
            EnumWindows((hWnd, lParam) =>
            {
                if (hWnd == this.Handle) return true;
                if (!IsWindowVisible(hWnd) || IsIconic(hWnd)) return true;

                // Skip our own agents
                foreach (Form form in Application.OpenForms)
                {
                    if (form.Handle == hWnd) return true;
                }

                if (GetWindowRect(hWnd, out RECT rect))
                {
                    if (rect.Width > 150 && rect.Height > 50 && rect.Top > -10000 && rect.Left > -10000)
                    {
                        list.Add(new VisibleWindow
                        {
                            Handle = hWnd,
                            Rect = new Rectangle(rect.Left, rect.Top, rect.Width, rect.Height)
                        });
                    }
                }
                return true;
            }, IntPtr.Zero);

            _cachedWindows = list;
            _lastWindowScanTime = now;
            return list;
        }

        private List<WalkableSurface> GetWalkableSurfaces()
        {
            var list = new List<WalkableSurface>();
            
            // Add desktop floor
            var screenBounds = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
            list.Add(new WalkableSurface
            {
                Handle = IntPtr.Zero,
                Rect = new Rectangle(screenBounds.Left, screenBounds.Bottom - 5, screenBounds.Width, 10),
                SurfaceType = "Desktop"
            });

            // Add visible windows
            var windows = GetVisibleWindows();
            foreach (var w in windows)
            {
                list.Add(new WalkableSurface
                {
                    Handle = w.Handle,
                    Rect = w.Rect,
                    SurfaceType = "WindowTitleBar"
                });
            }

            return list;
        }

        private IntPtr GetActiveWindowToCrawl()
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return IntPtr.Zero;
            if (IsIconic(hwnd)) return IntPtr.Zero;
            if (!IsWindowVisible(hwnd)) return IntPtr.Zero;

            foreach (Form form in Application.OpenForms)
            {
                if (form.Handle == hwnd)
                {
                    return IntPtr.Zero;
                }
            }

            if (GetWindowRect(hwnd, out RECT rect))
            {
                if (rect.Width > 160 && rect.Height > 40 && rect.Top > -10000)
                {
                    return hwnd;
                }
            }

            return IntPtr.Zero;
        }

        private void TriggerHandshakeCollision(DesktopAgentForm other)
        {
            this._collisionCooldown = GetTickCount() + 45000;
            other._collisionCooldown = GetTickCount() + 45000;

            this._dx = (this.Left < other.Left) ? 2 : -2;
            other._dx = (other.Left < this.Left) ? 2 : -2;

            this._isHandshaking = true;
            this._handshakeEndTime = GetTickCount() + 3500;
            this.UpdateActivity();

            other._isHandshaking = true;
            other._handshakeEndTime = GetTickCount() + 3500;
            other.UpdateActivity();

            var greetings = new[]
            {
                ("Hi {0}!", "Oh, hello {0}!"),
                ("Beep boop!", "Boop beep! Code looks clean!"),
                ("Ready to code?", "Let's do this!"),
                ("Need a coffee?", "Yes please!"),
                ("Let's build a feature!", "Approved!")
            };

            var pair = greetings[_random.Next(greetings.Length)];
            this._speechText = string.Format(pair.Item1, other.Agent.CharacterName);
            this._speechEndTime = GetTickCount() + 3000;

            other._speechText = string.Format(pair.Item2, this.Agent.CharacterName);
            other._speechEndTime = GetTickCount() + 3000;

            this.Redraw();
            other.Redraw();
        }

        private void UpdateSleepParticles()
        {
            _particleSpawnCounter++;
            if (_particleSpawnCounter >= 12)
            {
                _particleSpawnCounter = 0;
                lock (_particles)
                {
                    var p = new SleepParticle
                    {
                        X = Width / 2f + _random.Next(-12, 12),
                        Y = Height * 0.2f,
                        Scale = (float)(_random.NextDouble() * 0.4 + 0.6),
                        Alpha = 255f,
                        SpeedY = (float)(_random.NextDouble() * 1.5 + 0.8),
                        SpeedX = (float)(_random.NextDouble() * 0.8 - 0.4),
                        Text = _random.Next(0, 2) == 0 ? "z" : "Z"
                    };
                    _particles.Add(p);
                }
            }

            lock (_particles)
            {
                for (int i = _particles.Count - 1; i >= 0; i--)
                {
                    var p = _particles[i];
                    p.Y -= p.SpeedY;
                    p.X += p.SpeedX + (float)Math.Sin(p.Y * 0.05f) * 0.4f;
                    p.Alpha -= 5f;
                    if (p.Alpha <= 0)
                    {
                        _particles.RemoveAt(i);
                    }
                }
            }
        }

        private void OpenChatWithFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return;
                AgentChatForm.ShowForAgent(Agent);
                AgentChatForm.AttachFileToActiveChat(filePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to attach file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void OnDragEnter(DragEventArgs drgevent)
        {
            if (drgevent.Data != null && drgevent.Data.GetDataPresent(DataFormats.FileDrop))
            {
                drgevent.Effect = DragDropEffects.Copy;
                _excitedEndTime = GetTickCount() + 3000;
                UpdateActivity();
                Redraw();
            }
            base.OnDragEnter(drgevent);
        }

        protected override void OnDragDrop(DragEventArgs drgevent)
        {
            if (drgevent.Data != null && drgevent.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = drgevent.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null && files.Length > 0)
                {
                    string filePath = files[0];
                    _excitedEndTime = GetTickCount() + 4000;
                    UpdateActivity();
                    Redraw();
                    OpenChatWithFile(filePath);
                }
            }
            base.OnDragDrop(drgevent);
        }

        private void OnMouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            _isDragging = true;
            _dragOffset = e.Location;
            _lastDragPos = Cursor.Position;
            _dragVelocityX = 0f;
            _dragVelocityY = 0f;
            _lastDragTime = GetTickCount();
            UpdateActivity();
        }

        private void OnMouseMove(object? sender, MouseEventArgs e)
        {
            if (!_isDragging)
            {
                Redraw();
                return;
            }
            double now = GetTickCount();
            double dt = now - _lastDragTime;
            if (dt > 0)
            {
                Point currPos = Cursor.Position;
                float instantVx = (float)(currPos.X - _lastDragPos.X) / (float)dt * 90f;
                float instantVy = (float)(currPos.Y - _lastDragPos.Y) / (float)dt * 90f;
                _dragVelocityX = _dragVelocityX * 0.7f + instantVx * 0.3f;
                _dragVelocityY = _dragVelocityY * 0.7f + instantVy * 0.3f;
                _lastDragPos = currPos;
                _lastDragTime = now;
            }
            Location = new Point(Cursor.Position.X - _dragOffset.X, Cursor.Position.Y - _dragOffset.Y);
            UpdateActivity();
        }

        private void OnMouseUp(object? sender, MouseEventArgs e)
        {
            _isDragging = false;
            
            // Set falling physics if in crawler/window modes on drop
            if (Agent.MovementType == "FreeRoam" || Agent.MovementType == "RestrictedTerritory" || Agent.MovementType == "FreeWindow" || Agent.MovementType == "FreeCrawler" || Agent.MovementType == "WindowCrawler")
            {
                _crawlerState = CrawlerState.FallingPhysical;
                float maxFlingSpeed = (float)Agent.MaxFlingSpeed;
                _velocityX = Math.Clamp(_dragVelocityX, -maxFlingSpeed, maxFlingSpeed);
                _velocityY = Math.Clamp(_dragVelocityY, -maxFlingSpeed, maxFlingSpeed);
                if (Math.Abs(_velocityX) > 5f && _velocityY > -2f)
                {
                    _velocityY -= 2f;
                }
            }

            // Check if dropped outside boundaries when walk area limits are enabled
            if (Agent.WalkArea.Enabled)
            {
                var bounds = _walkBoundsProvider();
                int feetY = Top + Height;
                int centerX = Left + Width / 2;
                if (centerX < bounds.Left || centerX > bounds.Right || feetY < bounds.Top || feetY > bounds.Bottom)
                {
                    if (Agent.MovementType == "RestrictedTerritory")
                    {
                        _isReturningHome = true;
                        _outsideExplorationEndTime = 0;
                        var returnBubble = new[] { "Going back to my territory!", "Heading back home!", "Let me return..." };
                        SetSpeechBubble(returnBubble[_random.Next(returnBubble.Length)], 2000);
                    }
                    else if (Agent.MovementType == "FreeWindow")
                    {
                        // In Free Window mode, temporarily explore first!
                        _isReturningHome = false;
                        _outsideExplorationEndTime = GetTickCount() + 30000; // 30 seconds
                        _hasAlertedOutside = false;
                    }
                    else
                    {
                        _isReturningHome = true;
                        var returnBubble = new[] { "Wait, where am I?", "Too far!", "Heading back home!", "Let me return..." };
                        SetSpeechBubble(returnBubble[_random.Next(returnBubble.Length)], 2000);
                    }
                }
            }

            UpdateActivity();
        }

        private static Color LighterColor(Color c)
        {
            return Color.FromArgb(c.A,
                Math.Min(255, c.R + 40),
                Math.Min(255, c.G + 40),
                Math.Min(255, c.B + 40));
        }

        private static Color DarkerColor(Color c)
        {
            return Color.FromArgb(c.A,
                Math.Max(0, c.R - 40),
                Math.Max(0, c.G - 40),
                Math.Max(0, c.B - 40));
        }

        private void SaveConfigHelper()
        {
            try
            {
                var config = AppConfig.Load();
                var definition = config.Agents.Find(a => a.CharacterName == Agent.CharacterName);
                if (definition != null)
                {
                    definition.MovementType = Agent.MovementType;
                    definition.WalkArea.Enabled = Agent.WalkArea.Enabled;
                    definition.WalkArea.X = Agent.WalkArea.X;
                    definition.WalkArea.Y = Agent.WalkArea.Y;
                    definition.WalkArea.Width = Agent.WalkArea.Width;
                    definition.WalkArea.Height = Agent.WalkArea.Height;
                    config.Save();

                    Agent.NotifyAgentSettingsChanged(config);

                    // Notify open AgentManagerForm to reload this agent's settings
                    var managerForm = Application.OpenForms.OfType<AgentManagerForm>().FirstOrDefault();
                    if (managerForm != null)
                    {
                        managerForm.ReloadAgentFromDisk(Agent.CharacterName);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to save config: " + ex.Message);
            }
        }

        public void SetSpeechBubble(string text, double durationMs = 3000)
        {
            _speechText = text;
            _speechEndTime = GetTickCount() + durationMs;
            UpdateActivity();
            Redraw();
        }

        private static GraphicsPath RoundedRect(RectangleF bounds, int radius)
        {
            var path = new GraphicsPath();
            var diameter = radius * 2;
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private class SleepParticle
        {
            public float X;
            public float Y;
            public float Scale;
            public float Alpha;
            public float SpeedY;
            public float SpeedX;
            public string Text { get; set; } = "z";
        }
    }
}
