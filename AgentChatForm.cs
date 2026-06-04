using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace LilAgentsWindows
{
    internal class AgentChatForm : Form
    {
        private static AgentChatForm? _instance;

        private Agent _agent;
        private LocalAgentChatService _chatService;
        private AppConfig _config;
        private List<ChatMessage> _history = new();
        private List<Attachment> _attachments = new();
        private AgentGroup? _activeGroup;

        // UI Panels & Layout Containers
               // Null/unused
             // Null/unused
         // Null/unused
        private Panel _mainPanel = null!;          // Main chat conversation area (Fill)
        
        private Panel _headerPanel = null!;
        private FlowLayoutPanel _chatPanel = null!;
        private Panel _inputContainer = null!;
        private Panel _inputCard = null!;
        private RichTextBox _messageBox = null!;
        private Label _placeholderLabel = null!;
        private FlowLayoutPanel _attachmentsPanel = null!;
        private FlowLayoutPanel _templatesPanel = null!;
        private Panel _dropZoneOverlay = null!;
        private Panel _waveOverlayPanel = null!;   // Listening voice wave animation

        // Control Center Buttons & Inputs
        private Button _sendButton = null!;
        private Button _clearButton = null!;
        private Button _cancelButton = null!;
        private Button _cameraButton = null!;
        private Button _micButton = null!;
        private Button _uploadButton = null!;
        private Button _closeBtn = null!;
        private Button _minBtn = null!;
        
        private Button _agentSelectBtn = null!;
        private ContextMenuStrip _agentGroupMenu = null!;
        private Button _autoChatBtn = null!;
        private Panel? _scrollSpacer;
        private bool _isScrollPending;
        private System.Windows.Forms.Timer? _typingIndicatorTimer;
        private int _typingIndicatorStep;
        private Dictionary<string, bool> _firstDeltaMap = new();
        private bool _isOnline;
        private bool _isDragOverLeft = true;
        private CancellationTokenSource? _connectionCheckCts;

        private Button _modelSelectBtn = null!;
        private ContextMenuStrip _modelMenu = null!;
        private Label _statusLabel = null!;
        private Panel _statusIndicator = null!;
        private Dictionary<string, Label> _activeThinkingLabels = new();
        private Dictionary<string, Panel> _activeThinkingPanels = new();
        private ListBox? _mentionListBox;

        private System.Windows.Forms.Timer _voiceTimer = null!;
        private CancellationTokenSource? _currentCts;

        
        
        
        private int _lastInputCardWidth = 0;

        private bool _isRecording = false;
        private float[] _waveAmplitudes = new float[5];
        private int _waveTickCount = 0;
        private bool _isAutoChatActive = false;
        private bool _inputFocused = false; // for focus glow on input card

        // Mentions queue
        private Queue<Agent> _mentionedAgentsQueue = new();



        // Win32 API Interops
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;
        private const byte VK_VOLUME_MUTE = 0xAD;
        private const byte VK_VOLUME_DOWN = 0xAE;
        private const byte VK_VOLUME_UP = 0xAF;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        public Agent Agent => _agent;

        // Static Singleton Accessors
        public static void ShowForAgent(Agent agent)
        {
            if (_instance == null || _instance.IsDisposed)
            {
                _instance = new AgentChatForm(agent);
            }
            else
            {
                _instance.SwitchToAgent(agent);
            }
            _instance.SlideIn();
        }

        public static void AttachFileToActiveChat(string fileName, string content)
        {
            if (_instance != null && !_instance.IsDisposed)
            {
                _instance.AddAttachmentFromFile(fileName, content);
            }
        }

        public static void AttachFileToActiveChat(string filePath)
        {
            if (_instance != null && !_instance.IsDisposed)
            {
                _instance.AttachFile(filePath);
            }
        }

        public void AttachFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return;
                string fileName = Path.GetFileName(filePath);
                string content = ProcessBinaryFile(filePath);
                AddAttachmentFromFile(fileName, content);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"File attachment issue: {ex.Message}");
            }
        }

        private AgentChatForm(Agent agent)
        {
            _agent = agent;
            _chatService = new LocalAgentChatService(agent);
            _config = AppConfig.Load();

            // Set Form Settings
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = _config.KeepChatOnTop;
            DoubleBuffered = true;

            MinimumSize = new Size(250, 300);

            BackColor = ThemeManager.FormBg;
            AllowDrop = true;

            // Initialize Position and Size
            var bounds = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
            
            // Migrate old configurations to half-tripled dimensions (480x630)
            if (_config.ChatWidth <= 0 || _config.ChatWidth == 320 || _config.ChatWidth == 960 || _config.ChatWidth == 455 || _config.ChatWidth == 480 || _config.ChatWidth == 380 || _config.ChatWidth == 1072 || _config.ChatWidth == 1100)
            {
                _config.ChatWidth = 480;
            }
            if (_config.ChatHeight <= 0 || _config.ChatHeight == 420 || _config.ChatHeight == 1260 || _config.ChatHeight == 680 || _config.ChatHeight == 550 || _config.ChatHeight == -1 || _config.ChatHeight == 1100 || _config.ChatHeight == 820)
            {
                _config.ChatHeight = 630;
            }

            Width = _config.ChatWidth;
            Height = _config.ChatHeight;
            
            if (_config.ChatLeft < 0 || _config.ChatTop < 0 || _config.ChatWidth == 480)
            {
                Left = bounds.X + (bounds.Width - Width) / 2;
                Top = bounds.Y + (bounds.Height - Height) / 2;
                _config.ChatLeft = Left;
                _config.ChatTop = Top;
                _config.Save(); // Persist centered location
            }
            else
            {
                Left = _config.ChatLeft;
                Top = _config.ChatTop;
            }

            _voiceTimer = new System.Windows.Forms.Timer { Interval = 50 };
            _voiceTimer.Tick += VoiceTimer_Tick;

            LoadChatHistory();
            SetupUI();
            LoadInitialMessage();

            // Handle Drag Events
            DragEnter += OnFormDragEnter;
            DragLeave += OnFormDragLeave;
            DragDrop += OnFormDragDrop;

            // Trigger connection check on load
            _ = CheckModelConnectionAsync(isReconnecting: false);
        }

        private void SetupUI()
        {
            Controls.Clear();

            // 3. Main Chat Panel (Docked Fill)
            _mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ThemeManager.ChatBg
            };
            Controls.Add(_mainPanel);

            // Custom Title Header Panel
            _headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 68,
                BackColor = ThemeManager.HeaderBg
            };
            _headerPanel.MouseDown += (s, e) => { if (e.Clicks == 1) HeaderPanel_MouseDown(s, e); };
            // Paint subtle gradient and bottom border on header
            _headerPanel.Paint += (s, e) =>
            {
                var g = e.Graphics;
                var r = new Rectangle(0, 0, _headerPanel.Width, _headerPanel.Height);
                // Subtle vertical gradient (top slightly lighter)
                using var grad = new LinearGradientBrush(r,
                    ThemeManager.Lighten(ThemeManager.HeaderBg, 0.04f),
                    ThemeManager.HeaderBg,
                    LinearGradientMode.Vertical);
                g.FillRectangle(grad, r);
                // Bottom separator line
                using var borderPen = new Pen(ThemeManager.BorderSubtle, 1f);
                g.DrawLine(borderPen, 0, _headerPanel.Height - 1, _headerPanel.Width, _headerPanel.Height - 1);
            };
            _mainPanel.Controls.Add(_headerPanel);

            // Window control buttons (top right of header) with smooth hover animations
            _closeBtn = new Button
            {
                Text = "\xE8BB",
                Font = new Font("Segoe MDL2 Assets", 9f),
                ForeColor = ThemeManager.TextSecondary,
                BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(32, 32),
                Location = new Point(_headerPanel.Width - 38, 10),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Cursor = Cursors.Hand
            };
            _closeBtn.FlatAppearance.BorderSize = 0;
            _closeBtn.Click += (_, _) => Hide();
            {
                // Smooth close button hover: fades to red
                float t = 0f; bool hovering = false;
                var closeTimer = new System.Windows.Forms.Timer { Interval = 16 };
                closeTimer.Tick += (_, _) =>
                {
                    float target = hovering ? 1f : 0f;
                    t += (target - t) * 0.25f;
                    if (Math.Abs(t - target) < 0.01f) { t = target; closeTimer.Stop(); }
                    _closeBtn.BackColor = ThemeManager.Lerp(Color.Transparent, Color.FromArgb(239, 68, 68), t);
                    _closeBtn.ForeColor = ThemeManager.Lerp(ThemeManager.TextSecondary, Color.White, t);
                };
                _closeBtn.MouseEnter += (_, _) => { hovering = true;  closeTimer.Start(); };
                _closeBtn.MouseLeave += (_, _) => { hovering = false; closeTimer.Start(); };
                _closeBtn.Disposed  += (_, _) => closeTimer.Dispose();
            }
            _headerPanel.Controls.Add(_closeBtn);

            _minBtn = new Button
            {
                Text = "\xE921",
                Font = new Font("Segoe MDL2 Assets", 8.5f),
                ForeColor = ThemeManager.TextSecondary,
                BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(32, 32),
                Location = new Point(_headerPanel.Width - 72, 10),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Cursor = Cursors.Hand
            };
            _minBtn.FlatAppearance.BorderSize = 0;
            _minBtn.Click += (_, _) => this.WindowState = FormWindowState.Minimized;
            UIHelpers.AddHoverAnimation(_minBtn, Color.Transparent,
                Color.FromArgb(50, ThemeManager.TextSecondary.R, ThemeManager.TextSecondary.G, ThemeManager.TextSecondary.B));
            _headerPanel.Controls.Add(_minBtn);

            // Dropdown button for switching agent/group (Header top-left)
            _agentSelectBtn = new Button
            {
                BackColor = ThemeManager.InputBg,
                ForeColor = ThemeManager.TextPrimary,
                Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(120, 28),
                Location = new Point(16, 18),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(4, 0, 4, 0),
                Visible = _config.ShowHeaderSelectors
            };
            _agentSelectBtn.FlatAppearance.BorderSize = 1;
            _agentSelectBtn.FlatAppearance.BorderColor = ThemeManager.BorderColor;
            _agentSelectBtn.Click += AgentSelectBtn_Click;
            _headerPanel.Controls.Add(_agentSelectBtn);

            _agentGroupMenu = new ContextMenuStrip
            {
                BackColor = ThemeManager.InputBg,
                ForeColor = ThemeManager.TextPrimary,
                ShowImageMargin = false,
                Font = new Font("Segoe UI", 9f)
            };

            // Status Indicator — PulsingDot (animates when thinking)
            var pulsingDot = new PulsingDot
            {
                Size     = new Size(8, 8),
                Location = new Point(18, 52),
                DotColor = ThemeManager.StatusOnline,
                Pulsing  = false
            };
            // Keep _statusIndicator pointing to underlying control for legacy code
            _statusIndicator = pulsingDot;
            _headerPanel.Controls.Add(pulsingDot);

            // Status Text label
            _statusLabel = new Label
            {
                Text     = "Online",
                Font     = new Font("Segoe UI", 7.5f),
                ForeColor = ThemeManager.TextSecondary,
                Location = new Point(30, 48),
                Size     = new Size(120, 16),
                BackColor = Color.Transparent
            };
            _headerPanel.Controls.Add(_statusLabel);

            // Model Selection button dropdown in header
            _modelSelectBtn = new Button
            {
                BackColor = ThemeManager.InputBg,
                ForeColor = ThemeManager.TextSecondary,
                Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(120, 28),
                Location = new Point(144, 18),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(4, 0, 4, 0),
                Visible = _config.ShowHeaderSelectors
            };
            _modelSelectBtn.FlatAppearance.BorderSize = 1;
            _modelSelectBtn.FlatAppearance.BorderColor = ThemeManager.BorderColor;
            _modelSelectBtn.Click += ModelSelectBtn_Click;
            _headerPanel.Controls.Add(_modelSelectBtn);

            _modelMenu = new ContextMenuStrip
            {
                BackColor = ThemeManager.InputBg,
                ForeColor = ThemeManager.TextPrimary,
                ShowImageMargin = false,
                Font = new Font("Segoe UI", 9f)
            };

            // Clear chat history button inside header
            _clearButton = new Button
            {
                Text = "\xE74D",
                Font = new Font("Segoe MDL2 Assets", 9.5f),
                ForeColor = ThemeManager.TextSecondary,
                BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(28, 28),
                Location = new Point(272, 18),
                Cursor = Cursors.Hand
            };
            _clearButton.FlatAppearance.BorderSize = 0;
            _clearButton.FlatAppearance.MouseOverBackColor = ThemeManager.CurrentTheme == "Light" ? Color.FromArgb(220, 220, 220) : Color.FromArgb(64, 64, 64);
            _clearButton.Click += (_, _) => ClearChatHistory();
            _headerPanel.Controls.Add(_clearButton);

            // Cancel Generation button (visible when generating)
            _cancelButton = new Button
            {
                Text = "\xE71A",
                Font = new Font("Segoe MDL2 Assets", 9.5f),
                ForeColor = Color.FromArgb(244, 63, 94), // rose-500
                BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(30, 30),
                Location = new Point(272, 18),
                Visible = false,
                Cursor = Cursors.Hand
            };

            _cancelButton.FlatAppearance.BorderSize = 0;
            _cancelButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(63, 29, 36); // Deep wine hover tint
            _cancelButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(88, 38, 49);
            _cancelButton.SizeChanged += (s, e) =>
            {
                using var p = RoundedRect(new RectangleF(0, 0, _cancelButton.Width, _cancelButton.Height), _cancelButton.Width / 2);
                _cancelButton.Region = new Region(p);
            };
            _cancelButton.Click += (_, _) => CancelGeneration();

            // Autonomous Mode Toggle Button
            _autoChatBtn = new Button
            {
                Text = "\xE768", // Play icon
                Font = new Font("Segoe MDL2 Assets", 10.5f),
                ForeColor = Color.FromArgb(148, 163, 184),
                BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(30, 30),
                Location = new Point(304, 18), // Left of clear/cancel button
                Visible = false,
                Cursor = Cursors.Hand
            };

            _autoChatBtn.FlatAppearance.BorderSize = 0;
            _autoChatBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(64, 64, 64);
            _autoChatBtn.Click += AutoChatBtn_Click;

            // 4. Conversation Scroll Stream
            _chatPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = ThemeManager.ChatBg,
                Padding = new Padding(12, 12, 12, 20)
            };
            _chatPanel.SizeChanged += (s, e) => ResizeMessageRows();
            _mainPanel.Controls.Add(_chatPanel);
            _chatPanel.SendToBack(); // layer properly below header and above input container

            _scrollSpacer = new Panel
            {
                Height = 220,
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            };

            // 5. Compose Text Input Container (docks bottom)
            _inputContainer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 120,
                BackColor = ThemeManager.ChatBg,
                Padding = new Padding(100, 10, 100, 20)
            };
            _inputContainer.SizeChanged += (s, e) =>
            {
                int w = _inputContainer.Width;
                int desiredCardWidth = Math.Min(800, w - 40);
                int sidePadding = Math.Max(20, (w - desiredCardWidth) / 2);
                _inputContainer.Padding = new Padding(sidePadding, 10, sidePadding, 20);
            };
            _mainPanel.Controls.Add(_inputContainer);
            _inputContainer.BringToFront();

            // Dotted highlight drag overlay (hidden by default)
            _dropZoneOverlay = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(200, 15, 23, 42),
                Visible = false
            };
            _dropZoneOverlay.Paint += DropZoneOverlay_Paint;
            _mainPanel.Controls.Add(_dropZoneOverlay);

            // Rounded Input Capsule Card (Odysseus style)
            _inputCard = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ThemeManager.InputBg, // panel color
                Padding = new Padding(10, 6, 10, 6)
            };
            _inputCard.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = RoundedRect(new RectangleF(0.5f, 0.5f, _inputCard.Width - 1f, _inputCard.Height - 1f), 12);
                if (_inputFocused)
                {
                    // Accent focus glow: slightly wider translucent stroke
                    using var glowPen = new Pen(ThemeManager.AccentBorder, 2f);
                    g.DrawPath(glowPen, path);
                    using var accentPen = new Pen(ThemeManager.AccentColor, 1f);
                    g.DrawPath(accentPen, path);
                }
                else
                {
                    using var pen = new Pen(ThemeManager.BorderColor, 1f);
                    g.DrawPath(pen, path);
                }
            };
            _inputCard.SizeChanged += (s, e) =>
            {
                using var p = RoundedRect(new RectangleF(0, 0, _inputCard.Width, _inputCard.Height), 12);
                _inputCard.Region = new Region(p);
                _inputCard.Invalidate(); // Force repaint of custom borders

                if (_inputCard.Width != _lastInputCardWidth)
                {
                    _lastInputCardWidth = _inputCard.Width;
                    AdjustInputHeight();
                }
                else
                {
                    PositionInputControls();
                }
            };
            _inputContainer.Controls.Add(_inputCard);

            // Removable attached files FlowLayoutPanel inside the card
            _attachmentsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 0, // initially 0 height (no attachments)
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoScroll = true,
                BackColor = Color.Transparent,
                Padding = new Padding(0)
            };
            _inputCard.Controls.Add(_attachmentsPanel);

            _templatesPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 2, 0, 4),
                Padding = new Padding(0),
                Visible = false
            };
            var actionsLabel = new Label
            {
                Text = "Quick Actions:",
                Font = new Font("Segoe UI Semibold", 8f, FontStyle.Bold),
                ForeColor = ThemeManager.TextSecondary,
                AutoSize = true,
                Margin = new Padding(0, 3, 6, 0)
            };
            _templatesPanel.Controls.Add(actionsLabel);
            _templatesPanel.Controls.Add(CreateTemplatePill("📝 Summarize", "Summarize this file."));
            _templatesPanel.Controls.Add(CreateTemplatePill("🔍 Explain Code", "Explain this code or content."));
            _templatesPanel.Controls.Add(CreateTemplatePill("📊 Analyze Data", "Analyze this data file."));
            _inputCard.Controls.Add(_templatesPanel);

            // Compose Expandable Text Box
            _messageBox = new RichTextBox
            {
                Multiline = true,
                ScrollBars = RichTextBoxScrollBars.None, // Hide ugly native scrollbars
                Font = new Font("Segoe UI", 11f), // Larger, airier modern font
                BackColor = ThemeManager.InputBg,
                ForeColor = ThemeManager.TextPrimary,
                BorderStyle = BorderStyle.None,
                Location = new Point(12, 6),
                Width = 260,
                Height = 45
            };
            _messageBox.KeyDown += OnMessageKeyDown;
            _messageBox.TextChanged += MessageBox_TextChanged;
            _messageBox.GotFocus  += (_, _) => { _inputFocused = true;  _inputCard?.Invalidate(); };
            _messageBox.LostFocus += (_, _) => { _inputFocused = false; _inputCard?.Invalidate(); };
            
            // Ghost Placeholder Label
            _placeholderLabel = new Label
            {
                Text = "Message agents...",
                Font = new Font("Segoe UI", 11f),
                ForeColor = ThemeManager.TextSecondary, // gray-400
                BackColor = ThemeManager.InputBg,
                AutoSize = true,
                Cursor = Cursors.IBeam
            };
            _placeholderLabel.Click += (s, e) => _messageBox.Focus();
            
            _inputCard.Controls.Add(_placeholderLabel);
            _inputCard.Controls.Add(_messageBox);

            _sendButton = new Button
            {
                Text = "\xE724", // Paper plane send icon
                Font = new Font("Segoe MDL2 Assets", 10.5f),
                BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(90, 95, 105), // Start with muted gray-blue
                FlatStyle = FlatStyle.Flat,
                Size = new Size(30, 30),
                Cursor = Cursors.Default
            };

            _sendButton.FlatAppearance.BorderSize = 0;
            _sendButton.FlatAppearance.MouseOverBackColor = Color.Transparent;
            _sendButton.FlatAppearance.MouseDownBackColor = Color.Transparent;
            _sendButton.SizeChanged += (s, e) =>
            {
                using var p = RoundedRect(new RectangleF(0, 0, _sendButton.Width, _sendButton.Height), _sendButton.Width / 2);
                _sendButton.Region = new Region(p);
            };
            _sendButton.Click += async (_, _) => await SendCurrentMessageAsync();
            _inputCard.Controls.Add(_sendButton);
            _inputCard.Controls.Add(_cancelButton);
            _inputCard.Controls.Add(_autoChatBtn);

            if (_autoChatBtn != null) {
                _autoChatBtn.BackColor = Color.Transparent;
                _autoChatBtn.ForeColor = Color.FromArgb(171, 178, 191);
                _autoChatBtn.FlatAppearance.BorderSize = 0;
            }
            if (_cancelButton != null) {
                _cancelButton.BackColor = Color.Transparent;
                _cancelButton.ForeColor = Color.FromArgb(224, 108, 117); // Red for cancel
                _cancelButton.FlatAppearance.BorderSize = 0;
            }
            if (_sendButton != null) {
                _sendButton.BackColor = Color.Transparent;
                _sendButton.ForeColor = ThemeManager.TextMuted; // starts muted until text typed
                _sendButton.FlatAppearance.BorderSize = 0;
            }
    

            // Microphone Voice Input button
            _micButton = new Button
            {
                Text = "\xE720",
                Font = new Font("Segoe MDL2 Assets", 9.5f),
                BackColor = Color.FromArgb(82, 82, 82), // slate-600
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(28, 28),
                Cursor = Cursors.Hand
            };
            _micButton.FlatAppearance.BorderSize = 0;
            _micButton.SizeChanged += (s, e) =>
            {
                using var p = RoundedRect(new RectangleF(0, 0, _micButton.Width, _micButton.Height), _micButton.Width / 2);
                _micButton.Region = new Region(p);
            };
            _micButton.Click += MicButton_Click;
            _micButton.MouseEnter += (s, e) => { _micButton.BackColor = Color.FromArgb(64, 64, 64); };
            _micButton.MouseLeave += (s, e) => { _micButton.BackColor = Color.FromArgb(82, 82, 82); };
            _inputCard.Controls.Add(_micButton);

            // Camera Screen capture button
            _cameraButton = new Button
            {
                Text = "\xE722",
                Font = new Font("Segoe MDL2 Assets", 9f),
                BackColor = Color.FromArgb(82, 82, 82),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(28, 28),
                Cursor = Cursors.Hand
            };
            _cameraButton.FlatAppearance.BorderSize = 0;
            _cameraButton.SizeChanged += (s, e) =>
            {
                using var p = RoundedRect(new RectangleF(0, 0, _cameraButton.Width, _cameraButton.Height), _cameraButton.Width / 2);
                _cameraButton.Region = new Region(p);
            };
            _cameraButton.Click += (_, _) => CaptureScreenAsync();
            _cameraButton.MouseEnter += (s, e) => { _cameraButton.BackColor = Color.FromArgb(64, 64, 64); };
            _cameraButton.MouseLeave += (s, e) => { _cameraButton.BackColor = Color.FromArgb(82, 82, 82); };
            _inputCard.Controls.Add(_cameraButton);

            // Upload File button inside input capsule (Gemini style '+' icon)
            _uploadButton = new Button
            {
                Text = "\xE710", // Plus icon
                Font = new Font("Segoe MDL2 Assets", 11f, FontStyle.Bold),
                BackColor = Color.Transparent, // transparent background
                ForeColor = Color.FromArgb(171, 178, 191), // slate-400
                FlatStyle = FlatStyle.Flat,
                Size = new Size(30, 30),
                Cursor = Cursors.Hand
            };
            _uploadButton.FlatAppearance.BorderSize = 0;
            _uploadButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(45, 49, 57);
            _uploadButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(53, 58, 66);
            _uploadButton.SizeChanged += (s, e) =>
            {
                using var p = RoundedRect(new RectangleF(0, 0, _uploadButton.Width, _uploadButton.Height), _uploadButton.Width / 2);
                _uploadButton.Region = new Region(p);
            };

            _uploadButton.Click += (s, e) =>
            {
                using var ofd = new OpenFileDialog();
                ofd.Title = "Select file to upload";
                ofd.Filter = "All files (*.*)|*.*";
                ofd.Multiselect = true;
                
                bool oldTopMost = this.TopMost;
                this.TopMost = false;
                try
                {
                    if (ofd.ShowDialog(this) == DialogResult.OK)
                    {
                        foreach (var file in ofd.FileNames)
                        {
                            try
                            {
                                string fileName = Path.GetFileName(file);
                                string content = ProcessBinaryFile(file);
                                AddAttachmentFromFile(fileName, content);
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"File load issue: {ex.Message}");
                            }
                        }
                    }
                }
                finally
                {
                    this.TopMost = oldTopMost;
                }
            };
            _uploadButton.MouseEnter += (s, e) => { _uploadButton.ForeColor = Color.White; };
            _uploadButton.MouseLeave += (s, e) => { _uploadButton.ForeColor = Color.FromArgb(171, 178, 191); };
            _inputCard.Controls.Add(_uploadButton);

            // Voice visualizer wave overlay
            _waveOverlayPanel = new Panel
            {
                Height = 36,
                BackColor = ThemeManager.InputBg, // seamless background
                Visible = false
            };
            _waveOverlayPanel.Paint += WaveOverlayPanel_Paint;
            _inputCard.Controls.Add(_waveOverlayPanel);

            PositionInputControls();

            PopulateModelsDropdown();
            PopulateAgentGroupDropdown();

            // Create mention suggestion dropdown
            _mentionListBox = new ListBox
            {
                Visible = false,
                BackColor = ThemeManager.InputBg,
                ForeColor = ThemeManager.TextPrimary,
                Font = new Font("Segoe UI", 9.5f),
                BorderStyle = BorderStyle.FixedSingle,
                Size = new Size(140, 120),
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 24
            };
            _mentionListBox.DrawItem += MentionListBox_DrawItem;
            _mentionListBox.Click += MentionListBox_Click;
            _mentionListBox.KeyDown += MentionListBox_KeyDown;
            _mainPanel.Controls.Add(_mentionListBox);
            _mentionListBox.BringToFront();

            // Set up drag and drop routing for all relevant panels/controls
            RegisterDragDrop(this);
            RegisterDragDrop(_mainPanel);
            RegisterDragDrop(_chatPanel);
            RegisterDragDrop(_inputContainer);
            RegisterDragDrop(_inputCard);
            RegisterDragDrop(_messageBox);
            RegisterDragDrop(_dropZoneOverlay);
            LayoutHeaderControls();
        }

        private void PositionInputControls()
        {
            if (_messageBox == null || _inputCard == null) return;

            int cardW = _inputCard.Width;
            int cardH = _inputCard.Height;
            int attachmentsHeight = _attachmentsPanel.Height;
            int templatesHeight = (_templatesPanel != null && _templatesPanel.Visible) ? _templatesPanel.PreferredSize.Height : 0;

            // Positioning the '+' (Upload) button on the left
            if (_uploadButton != null) 
            {
                _uploadButton.Location = new Point(10, cardH - 35);
            }

            int contentStartX = 46;
            
            // Layout right buttons from right to left
            int currentRight = cardW - 10;
            
            if (_sendButton != null && _sendButton.Visible)
            {
                currentRight -= 32;
                _sendButton.Location = new Point(currentRight, cardH - 35);
                currentRight -= 5;
            }
            
            if (_cancelButton != null && _cancelButton.Visible)
            {
                currentRight -= 32;
                _cancelButton.Location = new Point(currentRight, cardH - 35);
                _cancelButton.BringToFront();
                currentRight -= 5;
            }
            
            if (_autoChatBtn != null && _autoChatBtn.Visible)
            {
                currentRight -= 32;
                _autoChatBtn.Location = new Point(currentRight, cardH - 35);
                _autoChatBtn.BringToFront();
                currentRight -= 5;
            }
            
            // Model select button is placed in the header, no repositioning needed here

            int contentWidth = Math.Max(50, currentRight - contentStartX);

            _attachmentsPanel.Location = new Point(contentStartX, 4);
            _attachmentsPanel.Width = contentWidth;

            if (_templatesPanel != null && _templatesPanel.Visible)
            {
                _templatesPanel.Location = new Point(contentStartX, _attachmentsPanel.Bottom + 2);
                _templatesPanel.Width = contentWidth;
            }

            int tbY = attachmentsHeight + templatesHeight + 6;
            int tbH = cardH - attachmentsHeight - templatesHeight - 12;

            _messageBox.Location = new Point(contentStartX, tbY);
            _messageBox.Size = new Size(contentWidth, tbH);
            
            if (_placeholderLabel != null)
            {
                _placeholderLabel.Location = new Point(contentStartX + 2, tbY + 2);
            }

            if (_micButton != null) _micButton.Visible = false;
            if (_cameraButton != null) _cameraButton.Visible = false;

            _waveOverlayPanel.Location = new Point(contentStartX, tbY - 2);
            _waveOverlayPanel.Size = new Size(contentWidth, tbH + 4);
        }

        private void LayoutHeaderControls()
        {
            if (_headerPanel == null) return;

            int headerW = _headerPanel.Width;

            // 1. Position right-side controls
            if (_closeBtn != null)
            {
                _closeBtn.Location = new Point(headerW - 40, 10);
            }
            if (_minBtn != null)
            {
                _minBtn.Location = new Point(headerW - 75, 10);
            }
            if (_clearButton != null)
            {
                _clearButton.Location = new Point(headerW - 104, 18);
            }

            // 2. Position left-side dropdowns
            if (_agentSelectBtn != null && _modelSelectBtn != null)
            {
                int leftBoundary = 16;
                int rightBoundary = headerW - 108; // X start of clearBtn is headerW - 100
                int totalAvailable = rightBoundary - leftBoundary;

                if (_agentSelectBtn.Visible && _modelSelectBtn.Visible)
                {
                    // Split the space between the two
                    int gap = 8;
                    int singleWidth = (totalAvailable - gap) / 2;
                    singleWidth = Math.Clamp(singleWidth, 40, 120);

                    _agentSelectBtn.Size = new Size(singleWidth, 28);
                    _agentSelectBtn.Location = new Point(leftBoundary, 18);

                    _modelSelectBtn.Size = new Size(singleWidth, 28);
                    _modelSelectBtn.Location = new Point(leftBoundary + singleWidth + gap, 18);
                }
                else if (_agentSelectBtn.Visible)
                {
                    int singleWidth = Math.Clamp(totalAvailable, 40, 120);
                    _agentSelectBtn.Size = new Size(singleWidth, 28);
                    _agentSelectBtn.Location = new Point(leftBoundary, 18);
                }
                else if (_modelSelectBtn.Visible)
                {
                    int singleWidth = Math.Clamp(totalAvailable, 40, 120);
                    _modelSelectBtn.Size = new Size(singleWidth, 28);
                    _modelSelectBtn.Location = new Point(leftBoundary, 18);
                }
            }
        }

        private void PopulateModelsDropdown()
        {
            if (_modelSelectBtn == null) return;
            if (_agent == null)
            {
                _modelSelectBtn.Visible = false;
                LayoutHeaderControls();
                return;
            }
            _modelSelectBtn.Visible = true;
            _modelSelectBtn.Text = _agent.ModelName + " \u25BE";

            _modelMenu.Items.Clear();
            if (_agent.AIProvider == "OLLAMA")
            {
                AddModelMenuItem(_agent.ModelName);
                LoadEndpointOllamaModelsAsync();
            }
            else
            {
                // General default model options
                string[] defaults = { "gpt-4o", "claude-3-5-sonnet", "gemini-1.5-pro", "gemini-2.5-flash", "deepseek-chat" };
                foreach (var m in defaults)
                {
                    AddModelMenuItem(m);
                }
                if (Array.IndexOf(defaults, _agent.ModelName) == -1)
                {
                    AddModelMenuItem(_agent.ModelName);
                }
            }
            LayoutHeaderControls();
        }

        private async void LoadEndpointOllamaModelsAsync()
        {
            try
            {
                // Fetch models dynamically from local Ollama tags URL
                string url = _agent.Endpoint.Trim();
                if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                    !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    url = "http://" + url;
                }
                string tagsUrl = new Uri(url).Authority;
                tagsUrl = $"http://{tagsUrl}/api/tags";

                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
                var response = await client.GetAsync(tagsUrl);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("models", out var modelsElement))
                    {
                        var modelsList = new List<string>();
                        foreach (var m in modelsElement.EnumerateArray())
                        {
                            if (m.TryGetProperty("name", out var nameProp))
                            {
                                modelsList.Add(nameProp.GetString()!);
                            }
                        }

                        if (modelsList.Count > 0 && !IsDisposed)
                        {
                            BeginInvoke(() =>
                            {
                                _modelMenu.Items.Clear();
                                foreach (var m in modelsList)
                                {
                                    AddModelMenuItem(m);
                                }
                            });
                        }
                    }
                }
            }
            catch { }
        }

        private void ModelSelectBtn_Click(object? sender, EventArgs e)
        {
            if (_modelSelectBtn != null && _modelMenu != null)
            {
                _modelMenu.Show(_modelSelectBtn, new Point(0, _modelSelectBtn.Height));
            }
        }

        private void AddModelMenuItem(string modelName)
        {
            if (_modelMenu == null) return;

            // Avoid duplicates
            if (_modelMenu.Items.Cast<ToolStripItem>().Any(item => item.Text == modelName))
            {
                return;
            }

            var item = new ToolStripMenuItem(modelName);
            item.Click += (s, e) =>
            {
                if (_agent == null) return;
                _agent.ModelName = modelName;
                if (_modelSelectBtn != null)
                {
                    _modelSelectBtn.Text = modelName + " \u25BE";
                }
                _chatService = new LocalAgentChatService(_agent);
                
                // Persist model change back to JSON
                try
                {
                    var config = AppConfig.Load();
                    var definition = config.Agents.FirstOrDefault(a => a.CharacterName == _agent.CharacterName);
                    if (definition != null)
                    {
                        definition.ModelName = modelName;
                        config.Save();
                        _config = config;
                    }
                }
                catch { }

                SetThinking(false);
                _ = CheckModelConnectionAsync(isReconnecting: true);
            };
            _modelMenu.Items.Add(item);
        }

        private void PopulateAgentGroupDropdown()
        {
            if (_agentSelectBtn == null || _agentGroupMenu == null) return;

            _agentSelectBtn.Text = (_activeGroup != null ? _activeGroup.GroupName : _agent.CharacterName) + " \u25BE";

            _agentGroupMenu.Items.Clear();

            // Section: Agents
            var agentsHeader = new ToolStripMenuItem("AGENTS") { Enabled = false };
            agentsHeader.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            agentsHeader.ForeColor = Color.Gray;
            _agentGroupMenu.Items.Add(agentsHeader);

            foreach (var def in _config.Agents)
            {
                var item = new ToolStripMenuItem(def.CharacterName);
                bool isActive = _activeGroup == null && def.CharacterName == _agent?.CharacterName;
                if (isActive)
                {
                    item.Checked = true;
                    item.Font = new Font(item.Font, FontStyle.Bold);
                }

                item.Click += (s, e) =>
                {
                    var runningAgent = DesktopAgentForm.ActiveAgents
                        .FirstOrDefault(a => a.Agent.CharacterName == def.CharacterName)?.Agent 
                        ?? new Agent(def);
                    SwitchToAgent(runningAgent);
                };
                _agentGroupMenu.Items.Add(item);
            }

            _agentGroupMenu.Items.Add(new ToolStripSeparator());

            // Section: Groups
            var groupsHeader = new ToolStripMenuItem("GROUPS") { Enabled = false };
            groupsHeader.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            groupsHeader.ForeColor = Color.Gray;
            _agentGroupMenu.Items.Add(groupsHeader);

            foreach (var grp in _config.Groups)
            {
                var item = new ToolStripMenuItem(grp.GroupName);
                bool isActive = _activeGroup == grp;
                if (isActive)
                {
                    item.Checked = true;
                    item.Font = new Font(item.Font, FontStyle.Bold);
                }

                item.Click += (s, e) =>
                {
                    SwitchToGroup(grp);
                };
                _agentGroupMenu.Items.Add(item);
            }

            _agentGroupMenu.Items.Add(new ToolStripSeparator());

            // Section: Actions
            var newGroupItem = new ToolStripMenuItem("+ Create New Group...");
            newGroupItem.ForeColor = Color.FromArgb(97, 175, 239);
            newGroupItem.Click += (s, e) =>
            {
                _config = AppConfig.Load();
                var groupMgr = new GroupManagerForm(_config);
                groupMgr.GroupsChanged += (s2, ev) =>
                {
                    _config = AppConfig.Load();
                    PopulateAgentGroupDropdown();
                };
                groupMgr.Show(this);
            };
            _agentGroupMenu.Items.Add(newGroupItem);

            var manageAgentsItem = new ToolStripMenuItem("⚙ Manage Agents...");
            manageAgentsItem.Click += (s, e) =>
            {
                _config = AppConfig.Load();
                var manager = new AgentManagerForm(_config);
                manager.ConfigSaved += (s2, updatedConfig) =>
                {
                    _config = updatedConfig;
                    
                    if (_agent != null)
                    {
                        var updatedAgentDef = _config.Agents.FirstOrDefault(a => a.CharacterName == _agent.CharacterName);
                        if (updatedAgentDef != null)
                        {
                            _agent = DesktopAgentForm.ActiveAgents
                                .FirstOrDefault(a => a.Agent.CharacterName == _agent.CharacterName)?.Agent 
                                ?? new Agent(updatedAgentDef);
                            _chatService = new LocalAgentChatService(_agent);
                        }
                    }
                    PopulateAgentGroupDropdown();
                    PopulateModelsDropdown();
                };
                manager.Show(this);
            };
            _agentGroupMenu.Items.Add(manageAgentsItem);
        }

        private void AgentSelectBtn_Click(object? sender, EventArgs e)
        {
            if (_agentSelectBtn != null && _agentGroupMenu != null)
            {
                PopulateAgentGroupDropdown();
                _agentGroupMenu.Show(_agentSelectBtn, new Point(0, _agentSelectBtn.Height));
            }
        }

        public void SwitchToAgent(Agent agent)
        {
            if (_activeGroup == null && _agent != null && _agent.CharacterName == agent.CharacterName) return;

            SaveChatHistory();
            CancelGeneration();

            _activeGroup = null;

            _agent = agent;
            _chatService = new LocalAgentChatService(agent);
            _attachments.Clear();
            _attachmentsPanel.Controls.Clear();
            _attachmentsPanel.Height = 0;

            _history.Clear();
            LoadChatHistory();

            UpdateAgentLabels();
            PopulateModelsDropdown();
            PopulateAgentGroupDropdown();

            _chatPanel.Controls.Clear();
            LoadInitialMessage();

            AdjustInputHeight();
            _messageBox.Focus();

            _ = CheckModelConnectionAsync(isReconnecting: false);
        }

        public void SwitchToGroup(AgentGroup group)
        {
            if (_activeGroup == group) return;

            SaveChatHistory();
            CancelGeneration();

            _activeGroup = group;
            
            var firstMember = group.MemberNames.FirstOrDefault();
            var def = _config.Agents.FirstOrDefault(a => a.CharacterName == firstMember) ?? _config.Agents[0];
            var runningAgent = DesktopAgentForm.ActiveAgents
                .FirstOrDefault(a => a.Agent.CharacterName == def.CharacterName)?.Agent 
                ?? new Agent(def);
                
            _agent = runningAgent;
            _chatService = new LocalAgentChatService(_agent);
            _attachments.Clear();
            _attachmentsPanel.Controls.Clear();
            _attachmentsPanel.Height = 0;

            _history.Clear();
            LoadChatHistory();

            UpdateAgentLabels();
            PopulateModelsDropdown();
            PopulateAgentGroupDropdown();

            _chatPanel.Controls.Clear();
            LoadInitialMessage();

            AdjustInputHeight();
            _messageBox.Focus();

            _ = CheckModelConnectionAsync(isReconnecting: false);
        }

        private void UpdateAgentLabels()
        {
            if (_agentSelectBtn != null)
            {
                _agentSelectBtn.Text = (_activeGroup != null ? _activeGroup.GroupName : _agent.CharacterName) + " \u25BE";
            }
            SetThinking(false);
            
            if (_autoChatBtn != null)
            {
                _autoChatBtn.Visible = _activeGroup != null;
                _autoChatBtn.BringToFront();
                if (_isAutoChatActive)
                {
                    _isAutoChatActive = false;
                    UpdateAutoChatUI();
                }
            }
        }

        private void AutoChatBtn_Click(object? sender, EventArgs e)
        {
            _isAutoChatActive = !_isAutoChatActive;
            UpdateAutoChatUI();

            if (_isAutoChatActive && _activeGroup != null)
            {
                // Kick off the autonomous loop
                _ = RunAutonomousGroupChatLoopAsync();
            }
        }

        private void UpdateAutoChatUI()
        {
            DesktopAgentForm.IsAutonomousGroupMode = _isAutoChatActive;

            if (_isAutoChatActive)
            {
                _autoChatBtn.Text = "\xE769"; // Pause/Stop icon
                _autoChatBtn.ForeColor = Color.FromArgb(34, 197, 94); // Green when active
            }
            else
            {
                _autoChatBtn.Text = "\xE768"; // Play icon
                _autoChatBtn.ForeColor = Color.FromArgb(148, 163, 184); // Slate when inactive
            }
        }

        private async Task RunAutonomousGroupChatLoopAsync()
        {
            if (_activeGroup == null) return;
            
            int agentIndex = 0;
            
            while (_isAutoChatActive && _activeGroup != null && !IsDisposed)
            {
                // Find next agent in the group
                var agentName = _activeGroup.MemberNames[agentIndex % _activeGroup.MemberNames.Count];
                var nextDef = _config.Agents.FirstOrDefault(a => a.CharacterName == agentName);
                
                if (nextDef != null)
                {
                    var agent = DesktopAgentForm.ActiveAgents
                        .FirstOrDefault(a => a.Agent.CharacterName == agentName)?.Agent 
                        ?? new Agent(nextDef);
                        
                    // Build a generic prompt for autonomous chat continuation
                    string prompt = "Please continue the conversation based on the context above. Provide a thoughtful and concise response.";
                    
                    try 
                    {
                        await StreamAgentResponseAsync(agent, prompt);
                    } 
                    catch { }
                }
                
                agentIndex++;
                
                if (_isAutoChatActive)
                {
                    await Task.Delay(3000);
                }
            }
        }

        private void MessageBox_TextChanged(object? sender, EventArgs e)
        {
            if (_messageBox != null && _placeholderLabel != null)
            {
                _placeholderLabel.Visible = string.IsNullOrEmpty(_messageBox.Text);
                
                // Smart Send Button Highlighting — uses accent color
                if (string.IsNullOrWhiteSpace(_messageBox.Text) && _attachments.Count == 0)
                {
                    _sendButton.BackColor = Color.Transparent;
                    _sendButton.ForeColor = ThemeManager.TextMuted;
                    _sendButton.Cursor = Cursors.Default;
                    _sendButton.FlatAppearance.MouseOverBackColor = Color.Transparent;
                    _sendButton.FlatAppearance.MouseDownBackColor = Color.Transparent;
                }
                else
                {
                    _sendButton.BackColor = Color.Transparent;
                    _sendButton.ForeColor = ThemeManager.AccentColor;
                    _sendButton.Cursor = Cursors.Hand;
                    _sendButton.FlatAppearance.MouseOverBackColor = ThemeManager.AccentSubtle;
                    _sendButton.FlatAppearance.MouseDownBackColor = ThemeManager.AccentSubtle;
                }
            }
            AdjustInputHeight();
            CheckMentionSuggestion();
        }

        private int GetTargetMessageBoxWidth()
        {
            if (_inputCard == null) return 260;
            int cardW = _inputCard.Width;
            int contentStartX = 46;
            int currentRight = cardW - 10;

            if (_sendButton != null && _sendButton.Visible) currentRight -= 37;
            if (_cancelButton != null && _cancelButton.Visible) currentRight -= 37;
            if (_autoChatBtn != null && _autoChatBtn.Visible) currentRight -= 37;

            return Math.Max(50, currentRight - contentStartX);
        }

        private void AdjustInputHeight()
        {
            if (_messageBox == null) return;

            int targetWidth = GetTargetMessageBoxWidth();
            using var g = _messageBox.CreateGraphics();
            var size = g.MeasureString(_messageBox.Text + "A\nA", _messageBox.Font, targetWidth - 10);

            // 45px is roughly 1.5 lines of 11pt Segoe UI padding. Max 180px before native mouse-wheel scroll is needed.
            int textHeight = Math.Clamp((int)size.Height + 5, 45, 180);
            
            int attachmentsHeight = 0;
            int templatesHeight = 0;
            if (_attachmentsPanel.Controls.Count > 0)
            {
                _attachmentsPanel.PerformLayout();
                attachmentsHeight = _attachmentsPanel.PreferredSize.Height;
                if (_templatesPanel != null)
                {
                    _templatesPanel.Visible = true;
                    _templatesPanel.PerformLayout();
                    templatesHeight = _templatesPanel.PreferredSize.Height + 4;
                }
            }
            else
            {
                if (_templatesPanel != null) _templatesPanel.Visible = false;
            }
            _attachmentsPanel.Height = attachmentsHeight;

            int finalContainerHeight = textHeight + attachmentsHeight + templatesHeight + 36;
            if (_inputContainer.Height != finalContainerHeight)
            {
                _inputContainer.Height = finalContainerHeight;
            }
            // Always position controls to ensure correct layout on width or height change
            PositionInputControls();
        }

        // Add File Attachment Chip
        public void AddAttachmentFromFile(string fileName, string content)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            string type = "\xE8A5";
            bool isImg = ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".gif";
            
            if (isImg) type = "\xE8B9";
            else if (ext == ".pdf") type = "\xE77F";
            else if (ext == ".docx") type = "\xE8A5";
            else if (ext == ".csv" || ext == ".xlsx") type = "\xEA69";

            var att = new Attachment
            {
                FileName = fileName,
                ContentType = isImg ? (ext == ".jpg" || ext == ".jpeg" ? "image/jpeg" : $"image/{ext.TrimStart('.')}") : "text/plain",
                TextContent = content
            };

            _attachments.Add(att);
            AddAttachmentChip(fileName, type, att);
        }

        private void AddAttachmentChip(string name, string emojiIcon, Attachment att)
        {
            var chip = new Panel
            {
                Size = new Size(110, 26),
                BackColor = Color.FromArgb(82, 82, 82), // slate-600
                Margin = new Padding(0, 0, 8, 0)
            };
            chip.Paint += (s, e) =>
            {
                using var path = RoundedRect(new RectangleF(0.5f, 0.5f, chip.Width - 1f, chip.Height - 1f), 6);
                using var p = new Pen(Color.FromArgb(100, 255, 255, 255), 1f);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.DrawPath(p, path);
            };

            var iconLbl = new Label
            {
                Text = emojiIcon,
                Font = new Font("Segoe MDL2 Assets", 8f),
                ForeColor = Color.White,
                Location = new Point(4, 6),
                Size = new Size(16, 16),
                Cursor = Cursors.Default
            };
            chip.Controls.Add(iconLbl);

            var lbl = new Label
            {
                Text = name,
                Font = new Font("Segoe UI", 7.5f),
                ForeColor = Color.White,
                Location = new Point(20, 5),
                Size = new Size(66, 16),
                Cursor = Cursors.Default,
                AutoEllipsis = true
            };
            chip.Controls.Add(lbl);

            var deleteBtn = new Label
            {
                Text = "Ã—",
                Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(244, 63, 94), // rose-500
                Location = new Point(90, 3),
                Size = new Size(16, 20),
                Cursor = Cursors.Hand
            };
            deleteBtn.Click += (s, e) =>
            {
                _attachments.Remove(att);
                _attachmentsPanel.Controls.Remove(chip);
                AdjustInputHeight();

            };
            chip.Controls.Add(deleteBtn);

            _attachmentsPanel.Controls.Add(chip);
            AdjustInputHeight();
        }

        // Multi-Agent Mentions parsing & sequential queue execution
        private List<Agent> ParseMentions(string text)
        {
            var mentions = new List<Agent>();
            var words = text.Split(new[] { ' ', '\t', '\n', '\r', ',', '.', '?', '!', ';', ':' }, StringSplitOptions.RemoveEmptyEntries);
            
            bool mentionsAll = false;
            foreach (var word in words)
            {
                string cleanWord = word;
                if (word.StartsWith("@"))
                {
                    cleanWord = word[1..];
                }
                
                string name = cleanWord.ToLowerInvariant();
                if (name == "all")
                {
                    mentionsAll = true;
                    continue;
                }

                var def = _config.Agents.FirstOrDefault(a => a.CharacterName.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (def != null)
                {
                    var runningAgent = DesktopAgentForm.ActiveAgents
                        .FirstOrDefault(a => a.Agent.CharacterName.Equals(name, StringComparison.OrdinalIgnoreCase))?.Agent
                        ?? new Agent(def);
                    
                    if (!mentions.Contains(runningAgent))
                    {
                        mentions.Add(runningAgent);
                    }
                }
            }

            if (mentionsAll)
            {
                mentions.Clear();
                var targets = (_activeGroup != null && _activeGroup.MemberNames.Count > 0)
                    ? _activeGroup.MemberNames
                    : _config.Agents.Select(a => a.CharacterName).ToList();

                foreach (var memberName in targets)
                {
                    var def = _config.Agents.FirstOrDefault(a => a.CharacterName.Equals(memberName, StringComparison.OrdinalIgnoreCase));
                    if (def != null)
                    {
                        var runningAgent = DesktopAgentForm.ActiveAgents
                            .FirstOrDefault(a => a.Agent.CharacterName.Equals(memberName, StringComparison.OrdinalIgnoreCase))?.Agent
                            ?? new Agent(def);
                        if (!mentions.Contains(runningAgent))
                        {
                            mentions.Add(runningAgent);
                        }
                    }
                }
            }

            return mentions;
        }

        private void CheckMentionSuggestion()
        {
            if (_mentionListBox == null || _messageBox == null) return;

            int selectionIndex = _messageBox.SelectionStart;
            string text = _messageBox.Text;

            int atIndex = -1;
            for (int i = selectionIndex - 1; i >= 0; i--)
            {
                if (text[i] == ' ' || text[i] == '\n') break;
                if (text[i] == '@')
                {
                    atIndex = i;
                    break;
                }
            }

            if (atIndex != -1)
            {
                string query = text.Substring(atIndex + 1, selectionIndex - (atIndex + 1)).ToLowerInvariant();
                var options = new List<string>();
                
                var availableNames = (_activeGroup != null && _activeGroup.MemberNames.Count > 0)
                    ? _activeGroup.MemberNames
                    : _config.Agents.Select(a => a.CharacterName).ToList();

                foreach (var name in availableNames)
                {
                    if (name.ToLowerInvariant().Contains(query))
                    {
                        options.Add("@" + name);
                    }
                }
                if ("all".Contains(query))
                {
                    options.Add("@all");
                }

                if (options.Count > 0)
                {
                    _mentionListBox.Items.Clear();
                    foreach (var opt in options)
                    {
                        _mentionListBox.Items.Add(opt);
                    }
                    _mentionListBox.SelectedIndex = 0;

                    int x = _inputCard.Left + _messageBox.Left;
                    int y = _inputContainer.Top - _mentionListBox.Height - 5;
                    _mentionListBox.Location = new Point(x, y);
                    _mentionListBox.Visible = true;
                    _mentionListBox.BringToFront();
                }
                else
                {
                    _mentionListBox.Visible = false;
                }
            }
            else
            {
                _mentionListBox.Visible = false;
            }
        }

        private void InsertSelectedMention()
        {
            if (_mentionListBox == null || _messageBox == null || !_mentionListBox.Visible) return;
            if (_mentionListBox.SelectedItem == null) return;

            string selectedOpt = _mentionListBox.SelectedItem.ToString() ?? "";
            int selectionIndex = _messageBox.SelectionStart;
            string text = _messageBox.Text;

            int atIndex = -1;
            for (int i = selectionIndex - 1; i >= 0; i--)
            {
                if (text[i] == ' ' || text[i] == '\n') break;
                if (text[i] == '@')
                {
                    atIndex = i;
                    break;
                }
            }

            if (atIndex != -1)
            {
                string before = text[..atIndex];
                string after = text[selectionIndex..];
                _messageBox.Text = before + selectedOpt + " " + after;
                _messageBox.SelectionStart = atIndex + selectedOpt.Length + 1;
            }
            _mentionListBox.Visible = false;
            _messageBox.Focus();
        }

        private void MentionListBox_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || _mentionListBox == null) return;
            
            e.DrawBackground();
            bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            
            using var bgBrush = new SolidBrush(isSelected ? Color.FromArgb(45, 52, 64) : Color.FromArgb(33, 37, 43));
            e.Graphics.FillRectangle(bgBrush, e.Bounds);
            
            string text = _mentionListBox.Items[e.Index]?.ToString() ?? "";
            using var textBrush = new SolidBrush(isSelected ? Color.White : Color.FromArgb(200, 200, 200));
            
            var sf = new StringFormat
            {
                LineAlignment = StringAlignment.Center,
                Alignment = StringAlignment.Near
            };
            e.Graphics.DrawString(text, e.Font ?? this.Font, textBrush, e.Bounds, sf);
            
            e.DrawFocusRectangle();
        }

        private void MentionListBox_Click(object? sender, EventArgs e)
        {
            InsertSelectedMention();
        }

        private void MentionListBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Tab)
            {
                InsertSelectedMention();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                if (_mentionListBox != null) _mentionListBox.Visible = false;
                _messageBox?.Focus();
                e.Handled = true;
            }
        }

        private async Task SendCurrentMessageAsync()
        {
            var text = _messageBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(text) && _attachments.Count == 0) return;

            this.SuspendLayout();
            string finalContent = "";
            Agent? targetAgent = null;

            try
            {
                _messageBox.Clear();
                AdjustInputHeight();

                // Construct final content prepending attachment payloads
                var imagesList = new List<string>();
                var payloadBuilder = new StringBuilder();
                foreach (var att in _attachments)
                {
                    if (att.IsImage)
                    {
                        imagesList.Add(att.TextContent);
                    }
                    else
                    {
                        payloadBuilder.AppendLine($"[Attached File Context: {att.FileName}]");
                        payloadBuilder.AppendLine("```");
                        payloadBuilder.AppendLine(att.TextContent);
                        payloadBuilder.AppendLine("```");
                        payloadBuilder.AppendLine();
                    }
                }
                payloadBuilder.AppendLine(text);
                finalContent = payloadBuilder.ToString();

                // Clear active attachments
                _attachments.Clear();
                _attachmentsPanel.Controls.Clear();
                _attachmentsPanel.Height = 0;

                // Render user bubble
                var userMsg = new ChatMessage("user", finalContent);
                if (imagesList.Count > 0)
                {
                    userMsg.Images = imagesList;
                }
                _history.Add(userMsg);
                AddMessageBubble(text, true);

                // Parse Mentions
                var mentions = ParseMentions(text);
                _mentionedAgentsQueue.Clear();
                foreach (var agent in mentions)
                {
                    _mentionedAgentsQueue.Enqueue(agent);
                }

                if (_mentionedAgentsQueue.Count == 0)
                {
                    if (_activeGroup != null && _activeGroup.MemberNames.Count > 0)
                    {
                        foreach (var name in _activeGroup.MemberNames)
                        {
                            var def = _config.Agents.FirstOrDefault(a => a.CharacterName.Equals(name, StringComparison.OrdinalIgnoreCase));
                            if (def != null)
                            {
                                var runningAgent = DesktopAgentForm.ActiveAgents
                                    .FirstOrDefault(a => a.Agent.CharacterName.Equals(name, StringComparison.OrdinalIgnoreCase))?.Agent
                                    ?? new Agent(def);
                                if (!_mentionedAgentsQueue.Contains(runningAgent))
                                {
                                    _mentionedAgentsQueue.Enqueue(runningAgent);
                                }
                            }
                        }
                    }
                }

                var respondingAgents = new List<Agent>();
                if (_mentionedAgentsQueue.Count > 0)
                {
                    respondingAgents.AddRange(_mentionedAgentsQueue);
                }
                else if (_agent != null)
                {
                    respondingAgents.Add(_agent);
                }

                if (respondingAgents.Count > 0)
                {
                    targetAgent = respondingAgents[0];
                    foreach (var agent in respondingAgents)
                    {
                        // Pre-emptively transition the UI state inside the suspended layout block
                        agent.SetThinking(true);
                        var companion = DesktopAgentForm.ActiveAgents.FirstOrDefault(a => a.Agent.CharacterName == agent.CharacterName);
                        companion?.SetSpeechBubble("Thinking...", 4000);

                        StartAgentResponseBubble(agent);
                    }
                    SetThinking(true);
                }
            }
            finally
            {
                this.ResumeLayout(true);
            }

            // Stream response asynchronously outside SuspendLayout
            if (targetAgent != null)
            {
                if (_mentionedAgentsQueue.Count > 0)
                {
                    _mentionedAgentsQueue.Dequeue();
                }
                await StreamAgentResponseAsync(targetAgent, finalContent);
            }
        }

        private async Task StreamAgentResponseAsync(Agent respondingAgent, string prompt)
        {
            if (respondingAgent == null) return;
            // Sync desktop companion visual state
            respondingAgent.SetThinking(true);
            
            // Set temporary active speech on companion
            var companion = DesktopAgentForm.ActiveAgents.FirstOrDefault(a => a.Agent.CharacterName == respondingAgent.CharacterName);
            companion?.SetSpeechBubble("Thinking...", 4000);

            StartAgentResponseBubble(respondingAgent);
            SetThinking(true);
            var replyBuilder = new StringBuilder();
            string finalReply = "";

            try
            {
                _currentCts = new CancellationTokenSource();
                
                // Route stream
                var reply = await _chatService.SendStreamingAsync(respondingAgent, _history, delta =>
                {
                    replyBuilder.Append(delta);
                    BeginInvoke(() => AddAgentDelta(respondingAgent, delta));
                }, _currentCts.Token);

                finalReply = string.IsNullOrWhiteSpace(replyBuilder.ToString()) ? reply : replyBuilder.ToString();
                
                // Add name tag to history context so other agents know who spoke
                _history.Add(new ChatMessage("assistant", $"{respondingAgent.CharacterName}: {finalReply}"));
                SaveChatHistory();
            }
            catch (OperationCanceledException)
            {
                finalReply = replyBuilder.ToString() + "\n*Generation cancelled*";
                _history.Add(new ChatMessage("assistant", $"{respondingAgent.CharacterName}: {finalReply}"));
                SaveChatHistory();
            }
            catch (Exception ex)
            {
                finalReply = replyBuilder.ToString() + $"\n*Error: {ex.Message}*";
            }
            finally
            {
                respondingAgent.SetThinking(false);
                if (_mentionedAgentsQueue.Count == 0)
                {
                    SetThinking(false);
                }

                bool hasThinking = false;
                foreach (var kvp in _activeThinkingLabels)
                {
                    if (kvp.Value.Tag as string == "THINKING")
                    {
                        hasThinking = true;
                        break;
                    }
                }

                if (!hasThinking && _typingIndicatorTimer != null)
                {
                    _typingIndicatorTimer.Stop();
                    _typingIndicatorTimer.Dispose();
                    _typingIndicatorTimer = null;
                }

                if (_activeThinkingPanels.TryGetValue(respondingAgent.CharacterName, out var panel))
                {
                    var contentFlow = panel.Controls.OfType<FlowLayoutPanel>().FirstOrDefault();
                    if (contentFlow != null)
                    {
                        _activeThinkingLabels.TryGetValue(respondingAgent.CharacterName, out var label);
                        string displayReply = !string.IsNullOrEmpty(finalReply) ? finalReply : (label?.Text ?? "");
                        if (displayReply == "●" || displayReply == "● ●" || displayReply == "● ● ●")
                        {
                            displayReply = "";
                        }
                        contentFlow.Controls.Clear();
                        RenderFormattedMessageIntoFlow(contentFlow, displayReply, false, respondingAgent);
                    }
                }

                _activeThinkingLabels.Remove(respondingAgent.CharacterName);
                _activeThinkingPanels.Remove(respondingAgent.CharacterName);
                _firstDeltaMap.Remove(respondingAgent.CharacterName);

                _currentCts?.Dispose();
                _currentCts = null;

                ScrollToBottomSmooth();

                // Process next agent in the mentions queue
                if (_mentionedAgentsQueue.Count > 0)
                {
                    var next = _mentionedAgentsQueue.Dequeue();
                    // Let the next agent respond to the accumulated chat history
                    await Task.Delay(800); // brief pause to feel natural
                    await StreamAgentResponseAsync(next, prompt);
                }
            }
        }

        private void AddMessageBubble(string text, bool isUserMessage)
        {
            AddMessageBubbleWithAgent(text, isUserMessage, _agent);
        }

        private void AddMessageBubbleWithAgent(string text, bool isUserMessage, Agent respondingAgent)
        {
            bool isCompact = _config.ChatLayout == "Compact";
            int avatarSize = isCompact ? 24 : 32;
            int rowMargin = isCompact ? 4 : 10;
            int textMarginY = isCompact ? 2 : 5;
            int flowOffsetY = isCompact ? 20 : 26;

            int panelWidth = _chatPanel.ClientSize.Width > 100 ? _chatPanel.ClientSize.Width : 455;
            var rowPanel = new Panel
            {
                Width = panelWidth - 25,
                Margin = new Padding(0, rowMargin, 0, rowMargin),
                BackColor = ThemeManager.ChatBg,
                Tag = isUserMessage ? "USER" : "AGENT"
            };

            var avatar = new Panel
            {
                Name = "avatar",
                Size = new Size(avatarSize, avatarSize),
                Location = new Point(40, textMarginY),
                BackColor = Color.Transparent
            };
            avatar.Paint += (s, e) =>
            {
                var g = e.Graphics;
                UIHelpers.SetHighQuality(g);
                int innerSize = avatarSize - 1;
                if (isUserMessage)
                {
                    // User avatar uses accent color for consistent theming
                    using var brush = new SolidBrush(ThemeManager.AccentColor);
                    g.FillEllipse(brush, 0, 0, innerSize, innerSize);
                    // Subtle inner highlight
                    using var highlight = new SolidBrush(Color.FromArgb(40, 255, 255, 255));
                    g.FillEllipse(highlight, 1, 1, innerSize / 2, innerSize / 3);
                    using var font = new Font("Segoe UI", isCompact ? 8f : 9.5f, FontStyle.Bold);
                    string letter = "U";
                    var strSz = g.MeasureString(letter, font);
                    g.DrawString(letter, font, Brushes.White, (avatarSize - strSz.Width) / 2f, (avatarSize - strSz.Height) / 2f);
                }
                else
                {
                    Color agentColor = respondingAgent?.BodyColor ?? Color.FromArgb(224, 108, 117);
                    using var brush = new SolidBrush(agentColor);
                    g.FillEllipse(brush, 0, 0, innerSize, innerSize);
                    // Subtle inner highlight
                    using var highlight = new SolidBrush(Color.FromArgb(40, 255, 255, 255));
                    g.FillEllipse(highlight, 1, 1, innerSize / 2, innerSize / 3);
                    using var font = new Font("Segoe UI", isCompact ? 8f : 9.5f, FontStyle.Bold);
                    string letter = (respondingAgent?.CharacterName ?? "A").Substring(0, 1).ToUpper();
                    var strSz = g.MeasureString(letter, font);
                    g.DrawString(letter, font, Brushes.White, (avatarSize - strSz.Width) / 2f, (avatarSize - strSz.Height) / 2f);
                }
            };
            rowPanel.Controls.Add(avatar);

            var nameLbl = new Label
            {
                Name = "senderLabel",
                Text = isUserMessage ? "You" : (respondingAgent != null ? respondingAgent.CharacterName : "Agent"),
                Font = new Font("Inter", isCompact ? 8f : 9f, FontStyle.Bold),
                ForeColor = ThemeManager.TextSecondary,
                Location = new Point(85, textMarginY),
                AutoSize = true,
                BackColor = ThemeManager.ChatBg
            };
            rowPanel.Controls.Add(nameLbl);
            
            int targetContentWidth = rowPanel.Width - 120;
            int initialFlowWidth = isUserMessage ? 100 : targetContentWidth;
            int maxBubbleWidth = (_config.ChatLayout == "Wide") ? (rowPanel.Width - 70) : Math.Min(400, rowPanel.Width - 70);

            var contentFlow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = isUserMessage ? (isCompact ? new Padding(8, 4, 8, 4) : new Padding(12, 8, 12, 8)) : new Padding(0),
                BackColor = isUserMessage ? ThemeManager.UserBubbleBg : ThemeManager.ChatBg,
                Location = isUserMessage ? new Point(rowPanel.Width - initialFlowWidth - 46, flowOffsetY) : new Point(85, flowOffsetY),
                MinimumSize = isUserMessage ? new Size(0, 0) : new Size(targetContentWidth, 0),
                MaximumSize = isUserMessage ? new Size(maxBubbleWidth, 0) : new Size(targetContentWidth, 0),
                Width = initialFlowWidth
            };

            if (isUserMessage)
            {
                contentFlow.SizeChanged += (s, e) =>
                {
                    if (contentFlow.Width > 0 && contentFlow.Height > 0)
                    {
                        using var path = RoundedRect(new RectangleF(0, 0, contentFlow.Width, contentFlow.Height), 12);
                        contentFlow.Region = new Region(path);
                    }
                    contentFlow.Left = rowPanel.Width - contentFlow.Width - 46;
                };
            }
            
            contentFlow.SizeChanged += (s, e) =>
            {
                rowPanel.Height = Math.Max(40, contentFlow.Bottom + 5);
            };

            RenderFormattedMessageIntoFlow(contentFlow, text, isUserMessage, respondingAgent ?? _agent);
            rowPanel.Controls.Add(contentFlow);

            rowPanel.Height = Math.Max(40, contentFlow.Bottom + 5);

            _chatPanel.Controls.Add(rowPanel);
            if (_scrollSpacer != null)
            {
                if (!_chatPanel.Controls.Contains(_scrollSpacer))
                {
                    _chatPanel.Controls.Add(_scrollSpacer);
                }
                _chatPanel.Controls.SetChildIndex(_scrollSpacer, _chatPanel.Controls.Count - 1);
            }
            ScrollToBottomSmooth();
        }

        private void StartAgentResponseBubble(Agent respondingAgent)
        {
            if (respondingAgent == null) return;
            if (_activeThinkingPanels.ContainsKey(respondingAgent.CharacterName)) return;

            bool isCompact = _config.ChatLayout == "Compact";
            int avatarSize = isCompact ? 24 : 32;
            int rowMargin = isCompact ? 4 : 10;
            int textMarginY = isCompact ? 2 : 5;
            int flowOffsetY = isCompact ? 20 : 26;

            int panelWidth = _chatPanel.ClientSize.Width > 100 ? _chatPanel.ClientSize.Width : 455;
            var rowPanel = new Panel
            {
                Width = panelWidth - 25,
                Margin = new Padding(0, rowMargin, 0, rowMargin),
                BackColor = ThemeManager.ChatBg,
                Tag = "AGENT"
            };

            var avatar = new Panel
            {
                Name = "avatar",
                Size = new Size(avatarSize, avatarSize),
                Location = new Point(40, textMarginY),
                BackColor = Color.Transparent
            };
            avatar.Paint += (s, e) =>
            {
                var g = e.Graphics;
                UIHelpers.SetHighQuality(g);
                int innerSize = avatarSize - 1;
                using var brush = new SolidBrush(respondingAgent.BodyColor);
                g.FillEllipse(brush, 0, 0, innerSize, innerSize);
                using var highlight = new SolidBrush(Color.FromArgb(40, 255, 255, 255));
                g.FillEllipse(highlight, 1, 1, innerSize / 2, innerSize / 3);
                using var font = new Font("Segoe UI", isCompact ? 8f : 9.5f, FontStyle.Bold);
                var letter = respondingAgent.CharacterName.Substring(0, 1).ToUpper();
                var strSz = g.MeasureString(letter, font);
                g.DrawString(letter, font, Brushes.White, (avatarSize - strSz.Width) / 2f, (avatarSize - strSz.Height) / 2f);
            };
            rowPanel.Controls.Add(avatar);

            var nameLbl = new Label
            {
                Name = "senderLabel",
                Text = respondingAgent.CharacterName,
                Font = new Font("Inter", isCompact ? 8f : 9f, FontStyle.Bold),
                ForeColor = ThemeManager.TextSecondary,
                Location = new Point(85, textMarginY),
                AutoSize = true,
                BackColor = ThemeManager.ChatBg
            };
            rowPanel.Controls.Add(nameLbl);

            int targetContentWidth = rowPanel.Width - 120;
            var contentFlow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(0),
                BackColor = ThemeManager.ChatBg,
                Location = new Point(85, flowOffsetY),
                MinimumSize = new Size(targetContentWidth, 0),
                MaximumSize = new Size(targetContentWidth, 0),
                Width = targetContentWidth
            };
            rowPanel.Controls.Add(contentFlow);

            _firstDeltaMap[respondingAgent.CharacterName] = true;
            var activeLabel = new Label
            {
                Text = "●",
                Font = new Font("Segoe UI", 12f),
                ForeColor = respondingAgent.BodyColor,
                MaximumSize = new Size(contentFlow.Width - 10, 0),
                AutoSize = true,
                Margin = new Padding(0),
                BackColor = ThemeManager.ChatBg,
                Tag = "THINKING"
            };
            contentFlow.Controls.Add(activeLabel);

            // Start typing indicator animation
            _typingIndicatorStep = 1;
            if (_typingIndicatorTimer == null)
            {
                _typingIndicatorTimer = new System.Windows.Forms.Timer { Interval = 400 };
                _typingIndicatorTimer.Tick += (s, e) =>
                {
                    _typingIndicatorStep = (_typingIndicatorStep % 3) + 1;
                    string dots = _typingIndicatorStep switch
                    {
                        1 => "●",
                        2 => "● ●",
                        3 => "● ● ●",
                        _ => "●"
                    };

                    foreach (var kvp in _activeThinkingLabels)
                    {
                        var label = kvp.Value;
                        if (label.Tag as string == "THINKING")
                        {
                            label.Text = dots;
                        }
                    }
                };
                _typingIndicatorTimer.Start();
            }
            
            contentFlow.SizeChanged += (s, e) =>
            {
                rowPanel.Height = Math.Max(40, contentFlow.Bottom + 5);
            };
            
            rowPanel.Height = Math.Max(40, contentFlow.Bottom + 5);
            
            _activeThinkingPanels[respondingAgent.CharacterName] = rowPanel;
            _activeThinkingLabels[respondingAgent.CharacterName] = activeLabel;
            _chatPanel.Controls.Add(rowPanel);
            
            if (_scrollSpacer != null)
            {
                if (!_chatPanel.Controls.Contains(_scrollSpacer))
                {
                    _chatPanel.Controls.Add(_scrollSpacer);
                }
                _chatPanel.Controls.SetChildIndex(_scrollSpacer, _chatPanel.Controls.Count - 1);
            }
            ScrollToBottomSmooth();
        }

        private void AddAgentDelta(Agent respondingAgent, string text)
        {
            if (respondingAgent == null || !_activeThinkingLabels.TryGetValue(respondingAgent.CharacterName, out var label)) return;
            
            bool isFirst = false;
            if (!_firstDeltaMap.TryGetValue(respondingAgent.CharacterName, out isFirst) || isFirst)
            {
                _firstDeltaMap[respondingAgent.CharacterName] = false;
                label.Tag = "STREAMING";

                bool hasThinking = false;
                foreach (var kvp in _activeThinkingLabels)
                {
                    if (kvp.Value.Tag as string == "THINKING")
                    {
                        hasThinking = true;
                        break;
                    }
                }

                if (!hasThinking && _typingIndicatorTimer != null)
                {
                    _typingIndicatorTimer.Stop();
                    _typingIndicatorTimer.Dispose();
                    _typingIndicatorTimer = null;
                }

                label.Text = "";
                label.Font = new Font("Inter", 10.5f);
                label.ForeColor = Color.FromArgb(228, 228, 231);
            }
            label.Text += text;
            ScrollToBottomSmooth();
        }

        private void RenderFormattedMessageIntoFlow(FlowLayoutPanel contentFlow, string text, bool isUser, Agent activeAgent)
        {
            string remainingText = text;
            int toolStart = remainingText.IndexOf("[TOOL:", StringComparison.OrdinalIgnoreCase);

            while (toolStart >= 0)
            {
                if (toolStart > 0)
                {
                    string textBefore = remainingText[..toolStart];
                    RenderMarkdownChunksIntoFlow(contentFlow, textBefore, isUser);
                }

                int tagEnd = remainingText.IndexOf(']', toolStart);
                if (tagEnd > toolStart)
                {
                    string toolHeader = remainingText.Substring(toolStart + 6, tagEnd - (toolStart + 6)).Trim();
                    int toolEnd = remainingText.IndexOf("[/TOOL]", tagEnd, StringComparison.OrdinalIgnoreCase);
                    string toolContent = "";
                    int nextStartOffset = tagEnd + 1;

                    if (toolEnd >= 0)
                    {
                        toolContent = remainingText.Substring(tagEnd + 1, toolEnd - (tagEnd + 1)).Trim();
                        nextStartOffset = toolEnd + 7;
                    }

                    var toolCard = CreateToolCardControl(toolHeader, toolContent, contentFlow.MaximumSize.Width, activeAgent);
                    if (toolCard != null)
                    {
                        contentFlow.Controls.Add(toolCard);
                    }

                    remainingText = remainingText[nextStartOffset..];
                }
                else
                {
                    remainingText = remainingText[(toolStart + 6)..];
                }

                toolStart = remainingText.IndexOf("[TOOL:", StringComparison.OrdinalIgnoreCase);
            }

            if (!string.IsNullOrEmpty(remainingText))
            {
                RenderMarkdownChunksIntoFlow(contentFlow, remainingText, isUser);
            }

            AddBubbleFooter(contentFlow, text, isUser);
        }

        private void RenderMarkdownChunksIntoFlow(FlowLayoutPanel contentFlow, string text, bool isUser)
        {
            var chunks = ParseMarkdown(text);
            float fontSize = _config.ChatLayout == "Compact" ? 8.5f : 9.5f;

            foreach (var chunk in chunks)
            {
                if (chunk.IsCode)
                {
                    var codeBlock = CreateCodeBlockControl(chunk.Content, chunk.Language, contentFlow.MaximumSize.Width);
                    contentFlow.Controls.Add(codeBlock);
                }
                else
                {
                    var cleanText = chunk.Content.Trim('\r', '\n');
                    if (string.IsNullOrEmpty(cleanText)) continue;

                    int paddingOffset = isUser ? 24 : 0;
                    var rtb = new RichTextBox
                    {
                        ReadOnly = true,
                        BorderStyle = BorderStyle.None,
                        ScrollBars = RichTextBoxScrollBars.None,
                        BackColor = isUser ? ThemeManager.UserBubbleBg : ThemeManager.ChatBg,
                        ForeColor = ThemeManager.TextPrimary,
                        Width = contentFlow.MaximumSize.Width - paddingOffset - 10,
                        Multiline = true,
                        Margin = new Padding(0, 0, 0, 8),
                        Cursor = Cursors.Arrow
                    };

                    rtb.ContentsResized += (s, e) =>
                    {
                        rtb.Height = e.NewRectangle.Height + 4;
                    };

                    rtb.Clear();
                    string remaining = cleanText;
                    int idx = 0;
                    while (idx < remaining.Length)
                    {
                        int start = remaining.IndexOf("**", idx);
                        if (start == -1)
                        {
                            rtb.SelectionFont = new Font("Segoe UI", fontSize, FontStyle.Regular);
                            rtb.SelectionColor = ThemeManager.TextPrimary;
                            rtb.AppendText(remaining[idx..]);
                            break;
                        }

                        if (start > idx)
                        {
                            rtb.SelectionFont = new Font("Segoe UI", fontSize, FontStyle.Regular);
                            rtb.SelectionColor = ThemeManager.TextPrimary;
                            rtb.AppendText(remaining[idx..start]);
                        }

                        int end = remaining.IndexOf("**", start + 2);
                        if (end == -1)
                        {
                            rtb.SelectionFont = new Font("Segoe UI", fontSize, FontStyle.Regular);
                            rtb.SelectionColor = ThemeManager.TextPrimary;
                            rtb.AppendText(remaining[start..]);
                            break;
                        }

                        rtb.SelectionFont = new Font("Segoe UI", fontSize, FontStyle.Bold);
                        rtb.SelectionColor = ThemeManager.TextPrimary;
                        rtb.AppendText(remaining[(start + 2)..end]);
                        idx = end + 2;
                    }

                    contentFlow.Controls.Add(rtb);
                }
            }
        }

        private Panel? CreateToolCardControl(string header, string content, int width, Agent activeAgent)
        {
            try
            {
                var card = new Panel
                {
                    Width = width - 15,
                    Height = 90,
                    BackColor = Color.FromArgb(47, 47, 47), // slate-800
                    Padding = new Padding(12, 10, 12, 10),
                    Margin = new Padding(0, 4, 0, 10)
                };

                card.Paint += (s, e) =>
                {
                    var g = e.Graphics;
                    UIHelpers.SetHighQuality(g);
                    // Shadow
                    UIHelpers.DrawSoftShadow(g, new Rectangle(2, 2, card.Width - 4, card.Height - 4), 8, 2);
                    // Card background
                    using var bgBrush = new SolidBrush(card.BackColor);
                    UIHelpers.FillRoundedRect(g, bgBrush, new Rectangle(0, 0, card.Width, card.Height), 8);
                    // Accent-colored border
                    using var p = UIHelpers.RoundedRect(new RectangleF(0.5f, 0.5f, card.Width - 1f, card.Height - 1f), 8f);
                    using var borderPen = new Pen(ThemeManager.AccentColor, 1.2f);
                    g.DrawPath(borderPen, p);
                };
                card.SizeChanged += (s, e) =>
                {
                    using var p = RoundedRect(new RectangleF(0, 0, card.Width, card.Height), 8);
                    card.Region = new Region(p);
                };

                var iconLabel = new Label
                {
                    Text = "âš™ System Tool Request",
                    Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(56, 189, 248),
                    Location = new Point(12, 10),
                    AutoSize = true
                };
                card.Controls.Add(iconLabel);

                if (header.StartsWith("WRITE_SCRIPT", StringComparison.OrdinalIgnoreCase))
                {
                    string filename = header[12..].Trim();
                    var descLabel = new Label
                    {
                        Text = $"{activeAgent.CharacterName} requests permission to run '{filename}' script.",
                        Font = new Font("Segoe UI", 8.5f),
                        ForeColor = Color.FromArgb(212, 212, 216),
                        Location = new Point(12, 32),
                        Width = card.Width - 140,
                        Height = 45
                    };
                    card.Controls.Add(descLabel);

                    var runButton = new Button
                    {
                        Text = "Approve & Run",
                        Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold),
                        BackColor = Color.FromArgb(34, 197, 94),
                        ForeColor = Color.White,
                        FlatStyle = FlatStyle.Flat,
                        Size = new Size(110, 28),
                        Location = new Point(card.Width - 125, 45),
                        Cursor = Cursors.Hand
                    };
                    runButton.FlatAppearance.BorderSize = 0;
                    runButton.Click += (s, e) =>
                    {
                        runButton.Enabled = false;
                        runButton.BackColor = Color.Gray;
                        runButton.Text = "Running...";
                        ExecuteScriptFile(filename, content);
                        runButton.Text = "Executed";
                    };
                    card.Controls.Add(runButton);
                }
                else if (header.StartsWith("VOLUME", StringComparison.OrdinalIgnoreCase))
                {
                    string direction = header[6..].Trim().ToUpperInvariant();
                    var descLabel = new Label
                    {
                        Text = $"{activeAgent.CharacterName} requests to change system volume to {direction}.",
                        Font = new Font("Segoe UI", 8.5f),
                        ForeColor = Color.FromArgb(212, 212, 216),
                        Location = new Point(12, 32),
                        Width = card.Width - 140,
                        Height = 45
                    };
                    card.Controls.Add(descLabel);

                    var runButton = new Button
                    {
                        Text = "Approve",
                        Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold),
                        BackColor = Color.FromArgb(34, 197, 94),
                        ForeColor = Color.White,
                        FlatStyle = FlatStyle.Flat,
                        Size = new Size(110, 28),
                        Location = new Point(card.Width - 125, 45),
                        Cursor = Cursors.Hand
                    };
                    runButton.FlatAppearance.BorderSize = 0;
                    runButton.Click += (s, e) =>
                    {
                        runButton.Enabled = false;
                        runButton.BackColor = Color.Gray;
                        runButton.Text = "Approved";
                        AdjustSystemVolume(direction);
                    };
                    card.Controls.Add(runButton);
                }
                else if (header.StartsWith("CHECKLIST", StringComparison.OrdinalIgnoreCase))
                {
                    string tasksRaw = header.Contains(' ') ? header[9..].Trim() : content;
                    string[] tasks = tasksRaw.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    
                    card.Height = 35 + tasks.Length * 28;
                    iconLabel.Text = "â˜‘ Interactive Checklist";
                    
                    int yOffset = 32;
                    foreach (var task in tasks)
                    {
                        var cb = new CheckBox
                        {
                            Text = task,
                            Font = new Font("Segoe UI", 9f),
                            ForeColor = Color.FromArgb(212, 212, 216),
                            Location = new Point(16, yOffset),
                            Size = new Size(card.Width - 40, 24)
                        };
                        card.Controls.Add(cb);
                        yOffset += 26;
                    }
                }
                else
                {
                    return null;
                }

                return card;
            }
            catch
            {
                return null;
            }
        }

        private void ExecuteScriptFile(string filename, string code)
        {
            try
            {
                string path = Path.Combine(AppConfig.ConfigFolder, "scripts");
                Directory.CreateDirectory(path);
                string filePath = Path.Combine(path, filename);
                File.WriteAllText(filePath, code);

                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{filePath}\"",
                    UseShellExecute = true,
                    CreateNoWindow = false
                };
                System.Diagnostics.Process.Start(processInfo);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Execution failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AdjustSystemVolume(string action)
        {
            try
            {
                if (action.Contains("UP"))
                {
                    for (int i = 0; i < 5; i++)
                    {
                        keybd_event(VK_VOLUME_UP, 0, 0, 0);
                        keybd_event(VK_VOLUME_UP, 0, KEYEVENTF_KEYUP, 0);
                    }
                }
                else if (action.Contains("DOWN"))
                {
                    for (int i = 0; i < 5; i++)
                    {
                        keybd_event(VK_VOLUME_DOWN, 0, 0, 0);
                        keybd_event(VK_VOLUME_DOWN, 0, KEYEVENTF_KEYUP, 0);
                    }
                }
                else if (action.Contains("MUTE"))
                {
                    keybd_event(VK_VOLUME_MUTE, 0, 0, 0);
                    keybd_event(VK_VOLUME_MUTE, 0, KEYEVENTF_KEYUP, 0);
                }
            }
            catch { }
        }

        private async void CaptureScreenAsync()
        {
            double oldOpacity = Opacity;
            Opacity = 0;
            
            await Task.Delay(130);

            try
            {
                IntPtr hwnd = GetForegroundWindow();
                Rectangle captureRect = Rectangle.Empty;

                if (hwnd != IntPtr.Zero)
                {
                    RECT r;
                    if (GetWindowRect(hwnd, out r))
                    {
                        captureRect = r.ToRectangle();
                    }
                }

                if (captureRect.Width <= 10 || captureRect.Height <= 10)
                {
                    captureRect = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
                }

                using var bmp = new Bitmap(captureRect.Width, captureRect.Height);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(captureRect.Left, captureRect.Top, 0, 0, captureRect.Size);
                }

                string folder = Path.Combine(AppConfig.ConfigFolder, "screenshots");
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, $"screen_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);

                // Add as image chip immediately
                AddAttachmentFromFile(Path.GetFileName(path), $"[Screenshot Image base64 raw context path: {path}]");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to capture screen: {ex.Message}", "Capture Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Opacity = oldOpacity;
            }
        }

        private void AddBubbleFooter(FlowLayoutPanel contentFlow, string fullText, bool isUser)
        {
            var footer = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                Height = 18,
                Width = contentFlow.MaximumSize.Width - 10,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 2, 0, 0)
            };

            var copyLink = new Label
            {
                Text = "Copy",
                Font = new Font("Segoe UI Semibold", 8f, FontStyle.Bold),
                ForeColor = Color.FromArgb(148, 163, 184), // slate-400
                Cursor = Cursors.Hand,
                AutoSize = true,
                Margin = new Padding(0, 1, 0, 0),
                BackColor = Color.Transparent
            };
            copyLink.MouseEnter += (s, e) => copyLink.ForeColor = Color.White;
            copyLink.MouseLeave += (s, e) => copyLink.ForeColor = Color.FromArgb(148, 163, 184);
            copyLink.Click += (s, e) =>
            {
                Clipboard.SetText(fullText);
                copyLink.Text = "Copied!";
                var t = new System.Windows.Forms.Timer { Interval = 1500 };
                t.Tick += (s2, e2) => { copyLink.Text = "Copy"; t.Stop(); t.Dispose(); };
                t.Start();
            };
            footer.Controls.Add(copyLink);

            contentFlow.Controls.Add(footer);
        }

        private Control CreateCodeBlockControl(string code, string language, int width)
        {
            var maxContentWidth = width - 10;

            var codeContainer = new Panel
            {
                Width = maxContentWidth,
                BackColor = Color.FromArgb(33, 33, 33), // slate-900
                Margin = new Padding(0, 5, 0, 8),
                Padding = new Padding(1)
            };

            var header = new Panel
            {
                Height = 26,
                BackColor = Color.FromArgb(47, 47, 47), // slate-800
                Dock = DockStyle.Top
            };

            var langLabel = new Label
            {
                Text = string.IsNullOrWhiteSpace(language) ? "code" : language.ToLower(),
                Font = new Font("Consolas", 8f, FontStyle.Bold),
                ForeColor = Color.FromArgb(212, 212, 216),
                Location = new Point(10, 5),
                AutoSize = true
            };
            header.Controls.Add(langLabel);

            var copyBtn = new Button
            {
                Text = "Copy",
                Font = new Font("Segoe UI Semibold", 7.5f, FontStyle.Bold),
                Size = new Size(46, 18),
                Location = new Point(maxContentWidth - 55, 4),
                BackColor = Color.FromArgb(82, 82, 82), // slate-600
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            copyBtn.FlatAppearance.BorderSize = 0;

            var cleanCode = code.Trim('\r', '\n');
            copyBtn.Click += (s, e) =>
            {
                Clipboard.SetText(cleanCode);
                copyBtn.Text = "Copied!";
                var t = new System.Windows.Forms.Timer { Interval = 2000 };
                t.Tick += (s2, e2) => { copyBtn.Text = "Copy"; t.Stop(); t.Dispose(); };
                t.Start();
            };
            header.Controls.Add(copyBtn);

            codeContainer.Controls.Add(header);

            int lineCount = cleanCode.Split('\n').Length;
            codeContainer.Height = Math.Clamp(lineCount * 17 + 38, 90, 320);

            var codeBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                Text = cleanCode,
                Font = new Font("Consolas", 9f),
                BackColor = Color.FromArgb(33, 33, 33),
                ForeColor = Color.FromArgb(244, 244, 245),
                BorderStyle = BorderStyle.None,
                Dock = DockStyle.Fill,
                ScrollBars = ScrollBars.Vertical
            };

            var textWrapper = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(8, 4, 8, 4),
                BackColor = Color.FromArgb(33, 33, 33)
            };
            textWrapper.Controls.Add(codeBox);
            codeContainer.Controls.Add(textWrapper);

            codeContainer.Paint += (s, e) =>
            {
                using var p = RoundedRect(new RectangleF(0.5f, 0.5f, codeContainer.Width - 1f, codeContainer.Height - 1f), 8);
                using var pen = new Pen(Color.FromArgb(64, 64, 64), 1f);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.DrawPath(pen, p);
            };
            codeContainer.SizeChanged += (s, e) =>
            {
                using var p = RoundedRect(new RectangleF(0, 0, codeContainer.Width, codeContainer.Height), 8);
                codeContainer.Region = new Region(p);
            };

            return codeContainer;
        }

        private void ScrollToBottomSmooth()
        {
            if (IsDisposed || _chatPanel == null || _chatPanel.IsDisposed) return;

            if (!IsHandleCreated)
            {
                EventHandler? handler = null;
                handler = (s, e) =>
                {
                    HandleCreated -= handler;
                    ScrollToBottomSmooth();
                };
                HandleCreated += handler;
                return;
            }

            if (_isScrollPending) return;

            _isScrollPending = true;
            BeginInvoke(new Action(() =>
            {
                _isScrollPending = false;
                if (IsDisposed || _chatPanel == null || _chatPanel.IsDisposed) return;

                ResizeMessageRows();

                _chatPanel.PerformLayout();
                foreach (Control ctrl in _chatPanel.Controls)
                {
                    ctrl.PerformLayout();
                }
                _chatPanel.PerformLayout();

                if (_chatPanel.Controls.Count > 0)
                {
                    var lastControl = _chatPanel.Controls[_chatPanel.Controls.Count - 1];
                    _chatPanel.ScrollControlIntoView(lastControl);
                    _chatPanel.AutoScrollPosition = new Point(0, _chatPanel.DisplayRectangle.Height);
                }
            }));
        }

        private async Task CheckModelConnectionAsync(bool isReconnecting)
        {
            if (IsDisposed) return;

            if (!IsHandleCreated)
            {
                EventHandler? handler = null;
                handler = (s, e) =>
                {
                    HandleCreated -= handler;
                    _ = CheckModelConnectionAsync(isReconnecting);
                };
                HandleCreated += handler;
                return;
            }

            // Cancel any pending connection check
            _connectionCheckCts?.Cancel();
            _connectionCheckCts = new CancellationTokenSource();
            var token = _connectionCheckCts.Token;

            // Update UI to Connecting / Reconnecting state
            BeginInvoke(new Action(() =>
            {
                if (IsDisposed) return;
                if (_statusIndicator is PulsingDot dot0)
                {
                    dot0.DotColor = ThemeManager.StatusThinking;
                    dot0.StartPulse();
                }
                else if (_statusIndicator != null)
                    _statusIndicator.BackColor = ThemeManager.StatusThinking;
                if (_statusLabel != null)
                {
                    _statusLabel.Text = isReconnecting ? "Reconnecting..." : "Connecting...";
                    _statusLabel.ForeColor = ThemeManager.StatusThinking;
                }
            }));

            bool connected = false;
            try
            {
                if (_chatService != null && _agent != null)
                {
                    connected = await Task.Run(() => _chatService.CheckConnectionAsync(_agent), token);
                }
            }
            catch
            {
                connected = false;
            }

            if (token.IsCancellationRequested) return;

            _isOnline = connected;

            BeginInvoke(new Action(() =>
            {
                if (IsDisposed) return;
                
                // If currently thinking, don't overwrite visual state yet
                var isThinking = _activeThinkingPanels.Count > 0 || _currentCts != null;
                if (isThinking) return;

                if (connected)
                {
                    if (_statusIndicator is PulsingDot dot1) { dot1.DotColor = ThemeManager.StatusOnline; dot1.StopPulse(); }
                    else if (_statusIndicator != null) _statusIndicator.BackColor = ThemeManager.StatusOnline;
                    if (_statusLabel != null)
                    {
                        _statusLabel.Text = "Online";
                        _statusLabel.ForeColor = ThemeManager.TextSecondary;
                    }
                }
                else
                {
                    if (_statusIndicator is PulsingDot dot2) { dot2.DotColor = ThemeManager.StatusOffline; dot2.StopPulse(); }
                    else if (_statusIndicator != null) _statusIndicator.BackColor = ThemeManager.StatusOffline;
                    if (_statusLabel != null)
                    {
                        _statusLabel.Text = "Offline";
                        _statusLabel.ForeColor = ThemeManager.StatusOffline;
                    }
                }
            }));
        }

        private void ResizeMessageRows()
        {
            if (_chatPanel == null) return;

            _chatPanel.SuspendLayout();
            
            int panelWidth = _chatPanel.ClientSize.Width > 100 ? _chatPanel.ClientSize.Width : 455;
            int maxContentWidth = 800;
            int hPadding = 12;

            if (panelWidth > maxContentWidth + 24)
            {
                hPadding = (panelWidth - maxContentWidth) / 2;
            }
            
            // Adjust chat panel padding to center content
            if (_chatPanel.Padding.Left != hPadding || _chatPanel.Padding.Bottom != 20)
            {
                _chatPanel.Padding = new Padding(hPadding, 12, hPadding, 20);
            }

            int targetWidth = panelWidth - (hPadding * 2) - 25;
            
            // Center the input card to match chat width
            if (_inputCard != null)
            {
                _inputContainer.Padding = new Padding(hPadding, 6, hPadding, 12);
                PositionInputControls();
            }

            foreach (Control control in _chatPanel.Controls)
            {
                if (control == _scrollSpacer)
                {
                    control.Width = targetWidth;
                    continue;
                }
                if (control is Panel rowPanel)
                {
                    rowPanel.Width = targetWidth;
                    bool isUserMessage = (rowPanel.Tag as string) == "USER";
                    
                    var avatar = rowPanel.Controls["avatar"];
                    var senderLabel = rowPanel.Controls["senderLabel"] as Label;
                    var timeLabel = rowPanel.Controls["timeLabel"] as Label;
                    var contentFlow = rowPanel.Controls.OfType<FlowLayoutPanel>().FirstOrDefault();

                    if (contentFlow != null)
                    {
                        int w = isUserMessage ? Math.Min(400, targetWidth - 70) : (targetWidth - 120);
                        if (isUserMessage)
                        {
                            contentFlow.MinimumSize = new Size(0, 0);
                            contentFlow.MaximumSize = new Size(w, 0);
                        }
                        else
                        {
                            contentFlow.MinimumSize = new Size(w, 0);
                            contentFlow.MaximumSize = new Size(w, 0);
                            contentFlow.Width = w;
                        }
 
                        int paddingOffset = contentFlow.Padding.Horizontal;
                        int maxChildWidth = w - paddingOffset - 10;

                        foreach (Control child in contentFlow.Controls)
                        {
                            if (child is Label lbl)
                            {
                                lbl.MaximumSize = new Size(maxChildWidth, 9999);
                            }
                            else if (child is Panel codeBlock)
                            {
                                codeBlock.Width = maxChildWidth;
                            }
                            else if (child is FlowLayoutPanel footer)
                            {
                                footer.Width = maxChildWidth;
                            }
                        }

                        // Force layout update on contentFlow to compute the AutoSize size correctly
                        contentFlow.PerformLayout();

                        if (isUserMessage)
                        {
                            if (avatar != null) avatar.Location = new Point(targetWidth - 36, 6);
                            if (timeLabel != null && avatar != null) timeLabel.Location = new Point(avatar.Left - timeLabel.PreferredWidth - 8, 8);
                            
                            if (senderLabel != null)
                            {
                                if (timeLabel != null)
                                {
                                    senderLabel.Location = new Point(timeLabel.Left - senderLabel.PreferredWidth - 8, 6);
                                }
                                else if (avatar != null)
                                {
                                    senderLabel.Location = new Point(avatar.Left - senderLabel.PreferredWidth - 8, 6);
                                }
                            }

                            contentFlow.Location = new Point(targetWidth - contentFlow.Width - 46, 26);
                        }
                        else
                        {
                            contentFlow.Location = new Point(85, 26);
                        }
                        
                        rowPanel.Height = Math.Max(48, contentFlow.Bottom + 8);
                    }
                    rowPanel.PerformLayout();
                }
            }
            _chatPanel.ResumeLayout(true);
        }

        private List<MessageChunk> ParseMarkdown(string text)
        {
            var chunks = new List<MessageChunk>();
            int index = 0;

            while (index < text.Length)
            {
                int codeStart = text.IndexOf("```", index);
                if (codeStart == -1)
                {
                    chunks.Add(new MessageChunk(false, text[index..]));
                    break;
                }

                if (codeStart > index)
                {
                    chunks.Add(new MessageChunk(false, text[index..codeStart]));
                }

                int tagStart = codeStart + 3;
                int newlineIndex = text.IndexOf('\n', tagStart);
                string language = "";
                int codeContentStart = tagStart;

                if (newlineIndex != -1 && newlineIndex < text.IndexOf("```", tagStart))
                {
                    language = text[tagStart..newlineIndex].Trim();
                    codeContentStart = newlineIndex + 1;
                }

                int codeEnd = text.IndexOf("```", codeContentStart);
                if (codeEnd == -1)
                {
                    chunks.Add(new MessageChunk(true, text[codeContentStart..], language));
                    break;
                }

                chunks.Add(new MessageChunk(true, text[codeContentStart..codeEnd], language));
                index = codeEnd + 3;
            }

            return chunks;
        }

                private void LoadInitialMessage()
        {
            if (_history.Count == 0)
            {
                if (_activeGroup != null) 
                {
                    AddMessageBubble($"*Connected to Group: {_activeGroup.GroupName}*", false);
                }
                else 
                {
                    AddMessageBubble($"*Secure connection established. I am {_agent.CharacterName}.*\\n\\n{_agent.Context}", false);
                }
            }
            else
            {
                foreach (var msg in _history)
                {
                    bool isUser = msg.Role == "user";
                    string cleanMsg = msg.Content;
                    Agent bubbleAgent = _agent;
                    
                    if (!isUser && cleanMsg.Contains(':'))
                    {
                        var parts = cleanMsg.Split(':', 2);
                        var name = parts[0].Trim();
                        var def = _config.Agents.FirstOrDefault(a => a.CharacterName == name);
                        if (def != null)
                        {
                            bubbleAgent = DesktopAgentForm.ActiveAgents.FirstOrDefault(a => a.Agent.CharacterName == name)?.Agent ?? new Agent(def);
                            cleanMsg = parts[1].TrimStart();
                        }
                    }
                    
                    if (bubbleAgent == null && _activeGroup != null && _activeGroup.MemberNames.Count > 0)
                    {
                         var def = _config.Agents.FirstOrDefault(a => a.CharacterName == _activeGroup.MemberNames[0]);
                         if (def != null) bubbleAgent = new Agent(def);
                    }
                    
                    AddMessageBubbleWithAgent(cleanMsg, isUser, bubbleAgent ?? _agent);
                }
            }
        }

        private void LoadChatHistory()
        {
            try
            {
                var historyPath = GetHistoryPath();
                if (File.Exists(historyPath))
                {
                    var json = File.ReadAllText(historyPath);
                    _history = JsonSerializer.Deserialize<List<ChatMessage>>(json) ?? new List<ChatMessage>();
                }
            }
            catch { }
        }

        private void SaveChatHistory()
        {
            try
            {
                var historyPath = GetHistoryPath();
                Directory.CreateDirectory(Path.GetDirectoryName(historyPath)!);
                var json = JsonSerializer.Serialize(_history, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(historyPath, json);
            }
            catch { }
        }

        private string GetHistoryPath()
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "LilAgentsWindows",
                "chat_history");
            return Path.Combine(folder, _activeGroup != null ? $"group_{_activeGroup.GroupName}.json" : $"{_agent.CharacterName}.json");
        }

        private void CancelGeneration()
        {
            _currentCts?.Cancel();
            
            // Clean up the pre-emptive bubbles for the queued agents who haven't started yet
            while (_mentionedAgentsQueue.Count > 0)
            {
                var queuedAgent = _mentionedAgentsQueue.Dequeue();
                queuedAgent.SetThinking(false);
                var companion = DesktopAgentForm.ActiveAgents.FirstOrDefault(a => a.Agent.CharacterName == queuedAgent.CharacterName);
                companion?.SetSpeechBubble("", 0);

                if (_activeThinkingPanels.TryGetValue(queuedAgent.CharacterName, out var panel))
                {
                    _chatPanel.Controls.Remove(panel);
                    panel.Dispose();
                    _activeThinkingPanels.Remove(queuedAgent.CharacterName);
                }
                _activeThinkingLabels.Remove(queuedAgent.CharacterName);
                _firstDeltaMap.Remove(queuedAgent.CharacterName);
            }

            _cancelButton.Visible = false;
            _clearButton.Visible = true;
            SetThinking(false);
        }

        private void SetThinking(bool isThinking)
        {
            _agent?.SetThinking(isThinking);
            if (_sendButton != null)
            {
                _sendButton.Enabled = !isThinking;
                _sendButton.Visible = !isThinking;
            }
            if (_clearButton != null) _clearButton.Visible = !isThinking;
            if (_cancelButton != null) _cancelButton.Visible = isThinking;
            if (_messageBox != null) _messageBox.ReadOnly = isThinking;
            PositionInputControls();

            if (isThinking)
            {
                if (_statusIndicator is PulsingDot dot) { dot.DotColor = ThemeManager.StatusThinking; dot.StartPulse(); }
                else if (_statusIndicator != null) _statusIndicator.BackColor = ThemeManager.StatusThinking;
                if (_statusLabel != null)
                {
                    _statusLabel.Text = "Thinking...";
                    _statusLabel.ForeColor = ThemeManager.StatusThinking;
                }
            }
            else
            {
                if (_isOnline)
                {
                    if (_statusIndicator is PulsingDot dot) { dot.DotColor = ThemeManager.StatusOnline; dot.StopPulse(); }
                    else if (_statusIndicator != null) _statusIndicator.BackColor = ThemeManager.StatusOnline;
                    if (_statusLabel != null)
                    {
                        _statusLabel.Text = "Online";
                        _statusLabel.ForeColor = ThemeManager.TextSecondary;
                    }
                }
                else
                {
                    if (_statusIndicator is PulsingDot dot) { dot.DotColor = ThemeManager.StatusOffline; dot.StopPulse(); }
                    else if (_statusIndicator != null) _statusIndicator.BackColor = ThemeManager.StatusOffline;
                    if (_statusLabel != null)
                    {
                        _statusLabel.Text = "Offline";
                        _statusLabel.ForeColor = ThemeManager.StatusOffline;
                    }
                }
            }
        }

        private void ClearChatHistory()
        {
            var result = MessageBox.Show(
                $"Clear all conversation records for {(_activeGroup != null ? _activeGroup.GroupName : _agent.CharacterName)}?",
                "Clear Conversation",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                try
                {
                    _history.Clear();
                    var historyPath = GetHistoryPath();
                    if (File.Exists(historyPath))
                    {
                        File.Delete(historyPath);
                    }
                    _chatPanel.Controls.Clear();
                    LoadInitialMessage();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed: {ex.Message}");
                }
            }
        }

        // MIC recordings stub/typing simulation triggers
        private void MicButton_Click(object? sender, EventArgs e)
        {
            _isRecording = !_isRecording;
            if (_isRecording)
            {
                _micButton.BackColor = Color.FromArgb(239, 68, 68); // Red recording indicator
                _micButton.Text = "ðŸ›‘";
                _waveOverlayPanel.Visible = true;
                _messageBox.Visible = false;
                _waveTickCount = 0;
                _voiceTimer.Start();
            }
            else
            {
                StopMicRecordingAndTranscribe();
            }
        }

        private void VoiceTimer_Tick(object? sender, EventArgs e)
        {
            _waveTickCount++;
            var rand = new Random();
            for (int i = 0; i < 5; i++)
            {
                _waveAmplitudes[i] = rand.Next(4, 24);
            }
            _waveOverlayPanel.Invalidate();

            // Auto timeout transcription after 4.5 seconds to feel responsive
            if (_waveTickCount >= 90)
            {
                StopMicRecordingAndTranscribe();
            }
        }

        private void WaveOverlayPanel_Paint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Draw soundwaves
            int xOffset = 24;
            using var waveBrush = new SolidBrush(Color.FromArgb(56, 189, 248)); // sky-400
            for (int i = 0; i < 5; i++)
            {
                float h = _waveAmplitudes[i];
                float y = (_waveOverlayPanel.Height - h) / 2;
                g.FillRectangle(waveBrush, xOffset, y, 4, h);
                xOffset += 10;
            }

            using var font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold);
            using var textBrush = new SolidBrush(Color.FromArgb(148, 163, 184));
            g.DrawString("Listening... Click to transcribe", font, textBrush, 85, 10);
        }

        private void StopMicRecordingAndTranscribe()
        {
            _voiceTimer.Stop();
            _isRecording = false;
            _micButton.BackColor = Color.FromArgb(82, 82, 82);
            _micButton.Text = "ðŸŽ¤";
            _waveOverlayPanel.Visible = false;
            _messageBox.Visible = true;

            // Inject contextual queries based on active companion role
            string[] contextQueries = (_activeGroup != null ? _activeGroup.GroupName : _agent.CharacterName).ToLowerInvariant() switch
            {
                var n when n.Contains("nova") => new[] {
                    "Nova, review my window crawling script code.",
                    "Nova, check for memory leaks in my GDI graphics code."
                },
                var s when s.Contains("sage") => new[] {
                    "Sage, outline the benefits of local vs cloud AI architectures.",
                    "Sage, summarize the key considerations for screen layout design."
                },
                var b when b.Contains("bolt") => new[] {
                    "Bolt, write a rapid bash automation script to backup configuration JSONs.",
                    "Bolt, compile a quick script to adjust Windows system volume."
                },
                _ => new[] {
                    "Lumi, suggest creative marketing slogans for Lil Agents Desktop Companion.",
                    "Lumi, generate short names for a modern glassmorphic chat widget."
                }
            };

            var query = contextQueries[new Random().Next(contextQueries.Length)];
            SimulateSpeechTyping(query);
        }

        private async void SimulateSpeechTyping(string text)
        {
            _messageBox.Focus();
            _messageBox.Clear();
            
            // Fast typing simulation
            string[] words = text.Split(' ');
            var builder = new StringBuilder();
            foreach (var word in words)
            {
                if (IsDisposed) return;
                builder.Append(word).Append(" ");
                _messageBox.Text = builder.ToString();
                _messageBox.SelectionStart = _messageBox.Text.Length;
                AdjustInputHeight();
                await Task.Delay(100);
            }
        }

        public void SlideIn()
        {
            if (!this.Visible)
            {
                Show();
                BringToFront();
                _messageBox.Focus();
            }
            else
            {
                BringToFront();
            }
        }

        public void SlideOut()
        {
            Hide();
        }

        public void ApplyTheme()
        {
            _config = AppConfig.Load();
            TopMost = _config.KeepChatOnTop;
            
            BackColor = ThemeManager.FormBg;
            _mainPanel.BackColor = ThemeManager.ChatBg;
            _headerPanel.BackColor = ThemeManager.HeaderBg;
            _headerPanel.Invalidate(); // Repaint gradient
            _chatPanel.BackColor = ThemeManager.ChatBg;
            _inputContainer.BackColor = ThemeManager.ChatBg;
            _inputCard.BackColor = ThemeManager.InputBg;
            _inputCard.Invalidate(); // Repaint border
            _messageBox.BackColor = ThemeManager.InputBg;
            _messageBox.ForeColor = ThemeManager.TextPrimary;
            _placeholderLabel.BackColor = ThemeManager.InputBg;
            _placeholderLabel.ForeColor = ThemeManager.TextSecondary;
            
            if (_closeBtn != null) { _closeBtn.ForeColor = ThemeManager.TextSecondary; _closeBtn.BackColor = Color.Transparent; }
            if (_minBtn != null)   { _minBtn.ForeColor   = ThemeManager.TextSecondary; _minBtn.BackColor   = Color.Transparent; }
            
            _agentSelectBtn.BackColor = ThemeManager.InputBg;
            _agentSelectBtn.ForeColor = ThemeManager.TextPrimary;
            _agentSelectBtn.FlatAppearance.BorderColor = ThemeManager.BorderColor;
            _agentSelectBtn.Visible = _config.ShowHeaderSelectors;
            
            _modelSelectBtn.BackColor = ThemeManager.InputBg;
            _modelSelectBtn.ForeColor = ThemeManager.TextSecondary;
            _modelSelectBtn.FlatAppearance.BorderColor = ThemeManager.BorderColor;
            _modelSelectBtn.Visible = _config.ShowHeaderSelectors;
            
            _clearButton.ForeColor = ThemeManager.TextSecondary;
            
            // Refresh pulsing dot for new theme
            if (_statusIndicator is PulsingDot pd) pd.Invalidate();
            
            if (_mentionListBox != null)
            {
                _mentionListBox.BackColor = ThemeManager.InputBg;
                _mentionListBox.ForeColor = ThemeManager.TextPrimary;
            }
            
            if (_waveOverlayPanel != null)
            {
                _waveOverlayPanel.BackColor = ThemeManager.InputBg;
            }
            
            // Re-render the chat conversation bubbles in the new theme!
            _chatPanel.Controls.Clear();
            LoadInitialMessage();
            AdjustInputHeight();
            ScrollToBottomSmooth();
            LayoutHeaderControls();
        }


        // Drag & Drop Highlighting
        private void OnFormDragEnter(object? sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
                _isDragOverLeft = true;
                _dropZoneOverlay.Visible = true;
                _dropZoneOverlay.BringToFront();
            }
        }

        private void OnFormDragLeave(object? sender, EventArgs e)
        {
            Point clientMouse = PointToClient(Cursor.Position);
            if (!ClientRectangle.Contains(clientMouse))
            {
                _dropZoneOverlay.Visible = false;
            }
        }

        private void OnFormDragDrop(object? sender, DragEventArgs e)
        {
            _dropZoneOverlay.Visible = false;
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null && files.Length > 0)
                {
                    foreach (var file in files)
                    {
                        try
                        {
                            string fileName = Path.GetFileName(file);
                            string content = ProcessBinaryFile(file);
                            AddAttachmentFromFile(fileName, content);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"File load issue: {ex.Message}");
                        }
                    }

                    if (!_isDragOverLeft)
                    {
                        _messageBox.Text = "Summarize the attached file(s).";
                        _messageBox.Focus();
                        _messageBox.SelectionStart = _messageBox.Text.Length;
                    }
                }
            }
        }

        private static bool IsTextFile(string path)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
                var buffer = new byte[4096];
                int read = fs.Read(buffer, 0, buffer.Length);
                for (int i = 0; i < read; i++)
                {
                    if (buffer[i] == 0) return false;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private string ProcessBinaryFile(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            
            // Check for images (convert to base64 data URI for LLM vision)
            if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".gif")
            {
                try
                {
                    byte[] bytes = File.ReadAllBytes(path);
                    string base64 = Convert.ToBase64String(bytes);
                    string mime = ext switch
                    {
                        ".jpg" or ".jpeg" => "image/jpeg",
                        ".gif" => "image/gif",
                        _ => "image/png"
                    };
                    return $"data:{mime};base64,{base64}";
                }
                catch (Exception ex)
                {
                    return $"[Error reading image file: {ex.Message}]";
                }
            }
            
            // Check for custom parsers
            if (ext == ".docx")
            {
                try
                {
                    return ParseDocxText(path);
                }
                catch (Exception ex)
                {
                    return $"[Error parsing .docx file: {ex.Message}]";
                }
            }
            
            if (ext == ".pdf")
            {
                try
                {
                    return ParsePdfText(path);
                }
                catch (Exception ex)
                {
                    return $"[Error parsing .pdf file: {ex.Message}]";
                }
            }

            if (ext == ".xlsx")
            {
                try
                {
                    return ParseXlsxText(path);
                }
                catch (Exception ex)
                {
                    return $"[Error parsing .xlsx file: {ex.Message}]";
                }
            }

            // Check if it's any text/code file (via null-byte scanning)
            if (IsTextFile(path))
            {
                try
                {
                    return File.ReadAllText(path);
                }
                catch (Exception ex)
                {
                    return $"[Error reading text file: {ex.Message}]";
                }
            }
            
            var info = new FileInfo(path);
            return $"[Binary File Reference: Name = {info.Name}, Extension = {ext}, Size = {info.Length} bytes - Loaded as active conversation context]";
        }

        private string ParseDocxText(string path)
        {
            using (var archive = System.IO.Compression.ZipFile.OpenRead(path))
            {
                var entry = archive.GetEntry("word/document.xml");
                if (entry == null) return "[Empty or invalid DOCX document]";
                
                using (var stream = entry.Open())
                using (var reader = new StreamReader(stream))
                {
                    string xml = reader.ReadToEnd();
                    var matches = System.Text.RegularExpressions.Regex.Matches(xml, @"<w:t[^>]*>(.*?)</w:t>");
                    var sb = new StringBuilder();
                    foreach (System.Text.RegularExpressions.Match m in matches)
                    {
                        sb.Append(System.Net.WebUtility.HtmlDecode(m.Groups[1].Value));
                    }
                    return sb.ToString();
                }
            }
        }

        private string ParsePdfText(string path)
        {
            using (var document = UglyToad.PdfPig.PdfDocument.Open(path))
            {
                var sb = new StringBuilder();
                foreach (var page in document.GetPages())
                {
                    sb.AppendLine(page.Text);
                }
                return sb.ToString();
            }
        }

        private string ParseXlsxText(string path)
        {
            using (var archive = System.IO.Compression.ZipFile.OpenRead(path))
            {
                var sharedStrings = new List<string>();
                var sstEntry = archive.GetEntry("xl/sharedStrings.xml");
                if (sstEntry != null)
                {
                    using (var stream = sstEntry.Open())
                    using (var reader = new StreamReader(stream))
                    {
                        string sstXml = reader.ReadToEnd();
                        var matches = System.Text.RegularExpressions.Regex.Matches(sstXml, @"<t[^>]*>(.*?)</t>");
                        foreach (System.Text.RegularExpressions.Match m in matches)
                        {
                            sharedStrings.Add(System.Net.WebUtility.HtmlDecode(m.Groups[1].Value));
                        }
                    }
                }

                var sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml");
                if (sheetEntry == null) return "[Empty or invalid Excel workbook]";

                using (var stream = sheetEntry.Open())
                using (var reader = new StreamReader(stream))
                {
                    string xml = reader.ReadToEnd();
                    var rowsMatches = System.Text.RegularExpressions.Regex.Matches(xml, @"<row[^>]*>(.*?)</row>");
                    var sb = new StringBuilder();
                    
                    foreach (System.Text.RegularExpressions.Match rowMatch in rowsMatches)
                    {
                        var rowXml = rowMatch.Groups[1].Value;
                        var cellMatches = System.Text.RegularExpressions.Regex.Matches(rowXml, @"<c[^>]*t=""([^""]+)""[^>]*>(.*?)</c>|<c[^>]*>(.*?)</c>");
                        var rowValues = new List<string>();
                        
                        foreach (System.Text.RegularExpressions.Match cellMatch in cellMatches)
                        {
                            string cellXml = cellMatch.Value;
                            string cellType = System.Text.RegularExpressions.Regex.Match(cellXml, @"t=""([^""]+)""").Groups[1].Value;
                            string val = System.Text.RegularExpressions.Regex.Match(cellXml, @"<v>(.*?)</v>").Groups[1].Value;
                            
                            if (cellType == "s" && int.TryParse(val, out int idx) && idx >= 0 && idx < sharedStrings.Count)
                            {
                                rowValues.Add(sharedStrings[idx]);
                            }
                            else if (!string.IsNullOrEmpty(val))
                            {
                                rowValues.Add(val);
                            }
                            else
                            {
                                rowValues.Add("");
                            }
                        }
                        
                        if (rowValues.Count > 0)
                        {
                            sb.AppendLine(string.Join(" | ", rowValues));
                        }
                    }
                    return sb.ToString();
                }
            }
        }

        private Label CreateTemplatePill(string labelText, string promptTemplate)
        {
            var pill = new Label
            {
                Text = labelText,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(171, 178, 191),
                BackColor = Color.FromArgb(47, 51, 60),
                AutoSize = true,
                Padding = new Padding(8, 3, 8, 3),
                Margin = new Padding(0, 0, 6, 4),
                Cursor = Cursors.Hand
            };
            pill.SizeChanged += (s, e) =>
            {
                using var path = RoundedRect(new RectangleF(0, 0, pill.Width, pill.Height), 8);
                pill.Region = new Region(path);
            };
            pill.MouseEnter += (s, e) =>
            {
                pill.BackColor = Color.FromArgb(97, 175, 239); // Blue hover background
                pill.ForeColor = Color.White;
            };
            pill.MouseLeave += (s, e) =>
            {
                pill.BackColor = Color.FromArgb(47, 51, 60);
                pill.ForeColor = Color.FromArgb(171, 178, 191);
            };
            pill.Click += (s, e) =>
            {
                _messageBox.Text = promptTemplate;
                _messageBox.Focus();
                _messageBox.SelectionStart = _messageBox.Text.Length;
            };
            return pill;
        }

        private void DropZoneOverlay_Paint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int w = _dropZoneOverlay.Width;
            int h = _dropZoneOverlay.Height;
            int margin = 16;
            int midX = w / 2;

            // Define left and right cards
            var leftRect = new Rectangle(margin, margin, midX - (int)(margin * 1.5), h - margin * 2);
            var rightRect = new Rectangle(midX + margin / 2, margin, midX - (int)(margin * 1.5), h - margin * 2);

            // Colors
            var activeBg = Color.FromArgb(50, 56, 189, 248);     // Glowing light blue
            var inactiveBg = Color.FromArgb(15, 255, 255, 255);  // Subtle glass fill
            var activeBorder = Color.FromArgb(56, 189, 248);     // sky-400
            var inactiveBorder = Color.FromArgb(64, 74, 94);     // slate-700

            // Draw Left Card ("Paste")
            using (var path = RoundedRect(new RectangleF(leftRect.X, leftRect.Y, leftRect.Width, leftRect.Height), 12))
            {
                using (var bgBrush = new SolidBrush(_isDragOverLeft ? activeBg : inactiveBg))
                {
                    g.FillPath(bgBrush, path);
                }
                using (var borderPen = new Pen(_isDragOverLeft ? activeBorder : inactiveBorder, _isDragOverLeft ? 2.5f : 1.5f))
                {
                    if (!_isDragOverLeft) borderPen.DashStyle = DashStyle.Dash;
                    g.DrawPath(borderPen, path);
                }
            }

            // Draw Right Card ("Summarize")
            using (var path = RoundedRect(new RectangleF(rightRect.X, rightRect.Y, rightRect.Width, rightRect.Height), 12))
            {
                using (var bgBrush = new SolidBrush(!_isDragOverLeft ? activeBg : inactiveBg))
                {
                    g.FillPath(bgBrush, path);
                }
                using (var borderPen = new Pen(!_isDragOverLeft ? activeBorder : inactiveBorder, !_isDragOverLeft ? 2.5f : 1.5f))
                {
                    if (_isDragOverLeft) borderPen.DashStyle = DashStyle.Dash;
                    g.DrawPath(borderPen, path);
                }
            }

            // Draw content inside Left Card
            using (var titleFont = new Font("Segoe UI Semibold", 13f, FontStyle.Bold))
            using (var descFont = new Font("Segoe UI", 9f))
            {
                var pasteTitle = "📋 Paste File";
                var pasteDesc = "Drop here to attach file\nwithout prompting.";
                var tSz = g.MeasureString(pasteTitle, titleFont);
                var dSz = g.MeasureString(pasteDesc, descFont);

                g.DrawString(pasteTitle, titleFont, Brushes.White, leftRect.X + (leftRect.Width - tSz.Width) / 2, leftRect.Y + (leftRect.Height - 35) / 2);
                using (var descBrush = new SolidBrush(Color.FromArgb(171, 178, 191)))
                {
                    g.DrawString(pasteDesc, descFont, descBrush, leftRect.X + (leftRect.Width - dSz.Width) / 2, leftRect.Y + (leftRect.Height + 15) / 2);
                }

                // Draw content inside Right Card
                var sumTitle = "📝 Summarize";
                var sumDesc = "Drop here to attach and\npre-fill template.";
                var tSz2 = g.MeasureString(sumTitle, titleFont);
                var dSz2 = g.MeasureString(sumDesc, descFont);

                g.DrawString(sumTitle, titleFont, Brushes.White, rightRect.X + (rightRect.Width - tSz2.Width) / 2, rightRect.Y + (rightRect.Height - 35) / 2);
                using (var descBrush = new SolidBrush(Color.FromArgb(171, 178, 191)))
                {
                    g.DrawString(sumDesc, descFont, descBrush, rightRect.X + (rightRect.Width - dSz2.Width) / 2, rightRect.Y + (rightRect.Height + 15) / 2);
                }
            }
        }

        private void HeaderPanel_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private async void OnMessageKeyDown(object? sender, KeyEventArgs e)
        {
            if (_mentionListBox != null && _mentionListBox.Visible)
            {
                if (e.KeyCode == Keys.Down)
                {
                    int nextIdx = _mentionListBox.SelectedIndex + 1;
                    if (nextIdx < _mentionListBox.Items.Count)
                    {
                        _mentionListBox.SelectedIndex = nextIdx;
                    }
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    return;
                }
                else if (e.KeyCode == Keys.Up)
                {
                    int prevIdx = _mentionListBox.SelectedIndex - 1;
                    if (prevIdx >= 0)
                    {
                        _mentionListBox.SelectedIndex = prevIdx;
                    }
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    return;
                }
                else if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Tab)
                {
                    InsertSelectedMention();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    return;
                }
                else if (e.KeyCode == Keys.Escape)
                {
                    _mentionListBox.Visible = false;
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    return;
                }
            }

            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.SuppressKeyPress = true;
                await SendCurrentMessageAsync();
            }
        }

        // WinForms UI Clipboard Pasteur
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.V)
            {
                if (Clipboard.ContainsImage())
                {
                    e.SuppressKeyPress = true;
                    var img = Clipboard.GetImage();
                    if (img != null)
                    {
                        // Save image attachment context
                        string folder = Path.Combine(AppConfig.ConfigFolder, "screenshots");
                        Directory.CreateDirectory(folder);
                        string path = Path.Combine(folder, $"pasted_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                        img.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                        AddAttachmentFromFile(Path.GetFileName(path), $"[Pasted Image base64 raw context path: {path}]");
                    }
                }
            }
            base.OnKeyDown(e);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Block closing, just hide panel via SlideOut to preserve instance state
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
            }
            else
            {
                base.OnFormClosing(e);
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            SaveWindowPositionAndSize();
            _voiceTimer?.Dispose();
            SaveChatHistory();
        }

        protected override void OnLocationChanged(EventArgs e)
        {
            base.OnLocationChanged(e);
            if (this.Visible)
            {
                SnapToScreenEdges();
                SaveWindowPositionAndSize();
            }
        }

        private bool _isSnapping = false;
        private void SnapToScreenEdges()
        {
            if (_config == null || !_config.SnapToEdges) return;
            if (_isSnapping) return;
            _isSnapping = true;
            try
            {
                const int snapDistance = 15;
                var screen = Screen.FromControl(this);
                var area = screen.WorkingArea;

                int newLeft = Left;
                int newTop = Top;

                // Snap Left/Right
                if (Math.Abs(Left - area.Left) < snapDistance)
                    newLeft = area.Left;
                else if (Math.Abs(Right - area.Right) < snapDistance)
                    newLeft = area.Right - Width;

                // Snap Top/Bottom
                if (Math.Abs(Top - area.Top) < snapDistance)
                    newTop = area.Top;
                else if (Math.Abs(Bottom - area.Bottom) < snapDistance)
                    newTop = area.Bottom - Height;

                if (newLeft != Left || newTop != Top)
                {
                    this.Location = new Point(newLeft, newTop);
                }
            }
            finally
            {
                _isSnapping = false;
            }
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            LayoutHeaderControls();
            if (this.Visible)
            {
                SaveWindowPositionAndSize();
            }
        }

        private void SaveWindowPositionAndSize()
        {
            if (!this.Visible) return;
            if (this.WindowState != FormWindowState.Normal) return;

            try
            {
                var latestConfig = AppConfig.Load();
                latestConfig.ChatLeft = this.Left;
                latestConfig.ChatTop = this.Top;
                latestConfig.ChatWidth = this.Width;
                latestConfig.ChatHeight = this.Height;
                latestConfig.Save();
                _config = latestConfig;
            }
            catch { }
        }

        private void RegisterDragDrop(Control control)
        {
            control.AllowDrop = true;
            control.DragEnter += OnFormDragEnter;
            control.DragLeave += OnFormDragLeave;
            control.DragDrop += OnFormDragDrop;
            control.DragOver += (s, e) => {
                if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    e.Effect = DragDropEffects.Copy;
                    Point clientPt = _dropZoneOverlay.PointToClient(new Point(e.X, e.Y));
                    bool isLeft = clientPt.X < _dropZoneOverlay.Width / 2;
                    if (isLeft != _isDragOverLeft)
                    {
                        _isDragOverLeft = isLeft;
                        _dropZoneOverlay.Invalidate();
                    }
                }
            };
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                return cp;
            }
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_NCCALCSIZE = 0x0083;
            if (m.Msg == WM_NCCALCSIZE)
            {
                m.Result = IntPtr.Zero;
                return;
            }

            const int WM_NCHITTEST = 0x0084;
            if (m.Msg == WM_NCHITTEST)
            {
                base.WndProc(ref m);
                int val = m.Result.ToInt32();
                if (val == 1) // HTCLIENT
                {
                    Point pos = PointToClient(new Point(m.LParam.ToInt32() & 0xffff, m.LParam.ToInt32() >> 16));
                    const int borderSize = 8;
                    bool resizeWidth = pos.X < borderSize || pos.X >= ClientSize.Width - borderSize;
                    bool resizeHeight = pos.Y < borderSize || pos.Y >= ClientSize.Height - borderSize;

                    if (resizeWidth && resizeHeight)
                    {
                        if (pos.X < borderSize && pos.Y < borderSize) m.Result = (IntPtr)13; // HTTOPLEFT
                        else if (pos.X >= ClientSize.Width - borderSize && pos.Y < borderSize) m.Result = (IntPtr)14; // HTTOPRIGHT
                        else if (pos.X < borderSize && pos.Y >= ClientSize.Height - borderSize) m.Result = (IntPtr)16; // HTBOTTOMLEFT
                        else m.Result = (IntPtr)17; // HTBOTTOMRIGHT
                    }
                    else if (resizeWidth)
                    {
                        if (pos.X < borderSize) m.Result = (IntPtr)10; // HTLEFT
                        else m.Result = (IntPtr)11; // HTRIGHT
                    }
                    else if (resizeHeight)
                    {
                        if (pos.Y < borderSize) m.Result = (IntPtr)12; // HTTOP
                        else m.Result = (IntPtr)15; // HTBOTTOM
                    }
                }
                return;
            }

            base.WndProc(ref m);
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

        private record MessageChunk(bool IsCode, string Content, string Language = "");
    }

    internal class Attachment
    {
        public string FileName { get; set; } = "";
        public string ContentType { get; set; } = "";
        public string TextContent { get; set; } = "";
        public bool IsImage => ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
        public Rectangle ToRectangle() => new Rectangle(Left, Top, Width, Height);
    }
}
