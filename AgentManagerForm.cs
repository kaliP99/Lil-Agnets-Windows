using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Windows.Forms;

namespace LilAgentsWindows
{
    internal class AgentManagerForm : Form
    {
        private AppConfig _config;
        private readonly ListBox _agentList = new();
        private readonly TextBox _nameBox = new();
        private readonly TextBox _toolBox = new();
        private readonly TextBox _contextBox = new();
        private readonly TextBox _walkFramesBox = new();
        private readonly TextBox _thinkFramesBox = new();
        private readonly TextBox _sleepFramesBox = new();
        private readonly TextBox _handshakeFramesBox = new();
        private readonly TextBox _fallingFramesBox = new();
        private readonly TextBox _excitedFramesBox = new();
        private readonly TextBox _sittingFramesBox = new();
        private readonly TextBox _stretchingFramesBox = new();
        private readonly TextBox _yawningFramesBox = new();
        private readonly TextBox _lookingAroundFramesBox = new();
        private readonly TextBox _flyingFramesBox = new();
        private readonly TextBox _runningFramesBox = new();
        private readonly Button _colorButton = new();
        private readonly CheckBox _walkAreaEnabled = new();
        private readonly NumericUpDown _x = new();
        private readonly NumericUpDown _y = new();
        private readonly NumericUpDown _w = new();
        private readonly NumericUpDown _h = new();
        private readonly ComboBox _sizeBox = new();
        private readonly ComboBox _movementTypeBox = new();
        private DoubleBufferedPanel _previewPanel = null!;
        private System.Windows.Forms.Timer _previewTimer = null!;
        private Color _selectedColor = Color.DeepSkyBlue;
        private Panel _colorPreview = null!;
        private Button _btnDragSelect = null!;
        private Button _btnFullScreen = null!;

        // AI Configuration Controls
        private readonly ComboBox _aiProviderBox = new();
        private readonly ComboBox _fallbackProviderBox = new();
        private readonly ComboBox _modelNameBox = new();
        private readonly ComboBox _fallbackModelNameBox = new();
        private readonly TextBox _endpointBox = new();
        private readonly TextBox _fallbackEndpointBox = new();
        private readonly TextBox _apiKeyBox = new();
        private readonly TextBox _fallbackApiKeyBox = new();
        private readonly CheckBox _fallbackEnabledBox = new();
        private readonly NumericUpDown _temperatureBox = new();
        private readonly NumericUpDown _maxTokensBox = new();
        private LinkLabel _primaryDetectLink = null!;
        private LinkLabel _primaryPullLink = null!;
        private LinkLabel _fallbackDetectLink = null!;
        private LinkLabel _fallbackPullLink = null!;

        // Custom physics inputs
        private readonly NumericUpDown _thudThresh = new();
        private readonly NumericUpDown _rollThresh = new();
        private readonly NumericUpDown _thudDur = new();
        private readonly NumericUpDown _rollDur = new();
        private readonly NumericUpDown _maxFling = new();
        private readonly NumericUpDown _slideThresh = new();
        private readonly NumericUpDown _slideProb = new();
        private readonly CheckBox _pushEnabled = new();
        private int _lastSelectedIndex = -1;
        private bool _isLoadingSelectedAgent = false;

        // App Customization Controls
        private readonly ComboBox _themeBox = new();
        private readonly FlowLayoutPanel _accentColorsPanel = new();
        private readonly ComboBox _chatLayoutBox = new();
        private readonly TrackBar _animIntensitySlider = new();
        private readonly CheckBox _keepChatOnTopBox = new();
        private readonly CheckBox _snapToEdgesBox = new();
        private readonly CheckBox _closeToTrayBox = new();
        private readonly CheckBox _showQuickActionsBox = new();
        private readonly CheckBox _showHeaderSelectorsBox = new();
        private string _tempAccentColorHex = "#50A0FF";
        private TabControl _tabControl = null!;

        public event EventHandler<AppConfig>? ConfigSaved;
        public event EventHandler<AppConfig>? ConfigChanged;

        public AgentManagerForm(AppConfig config)
        {
            _config = config;
            Text = "Lil Agents - Control Center";
            Width = 1100;
            Height = 900;
            MinimumSize = new Size(1100, 900);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(15, 23, 42); // slate-900

            BuildUi();
            LoadAgents();
            LoadWalkArea();
            LoadGlobalSettings();
            ApplyTheme();
            UpdateOllamaModelsAsync();
            RefreshDetectLinksVisibility();
        }

        private void BuildUi()
        {
            int coordY = 0;
            _modelNameBox.DropDownStyle = ComboBoxStyle.DropDown;
            _fallbackModelNameBox.DropDownStyle = ComboBoxStyle.DropDown;

            // Modern gradient header
            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 75,
                BackColor = Color.FromArgb(30, 41, 59), // slate-800
                Padding = new Padding(25, 15, 25, 15)
            };
            Controls.Add(headerPanel);

            var title = new Label
            {
                Text = "LIL AGENTS CONTROL CENTER",
                Font = new Font("Segoe UI Semibold", 18, FontStyle.Bold),
                ForeColor = Color.FromArgb(248, 250, 252),
                AutoSize = true,
                Location = new Point(25, 12)
            };
            headerPanel.Controls.Add(title);

            var subtitle = new Label
            {
                Text = "Configure identities, walking screens, and AI server API routes",
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = Color.FromArgb(148, 163, 184),
                AutoSize = true,
                Location = new Point(25, 42)
            };
            headerPanel.Controls.Add(subtitle);

            var txtSearchSettings = new TextBox
            {
                BackColor = Color.FromArgb(15, 23, 42),
                ForeColor = Color.FromArgb(148, 163, 184),
                Font = new Font("Segoe UI", 9.5f)
            };
            string placeholder = "Search settings...";
            txtSearchSettings.Text = placeholder;
            txtSearchSettings.Enter += (s, e) =>
            {
                if (txtSearchSettings.Text == placeholder)
                {
                    txtSearchSettings.Text = "";
                    txtSearchSettings.ForeColor = Color.FromArgb(241, 245, 249);
                }
            };
            txtSearchSettings.Leave += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtSearchSettings.Text))
                {
                    txtSearchSettings.Text = placeholder;
                    txtSearchSettings.ForeColor = Color.FromArgb(148, 163, 184);
                }
            };
            txtSearchSettings.TextChanged += (s, e) =>
            {
                PerformSettingsSearch(txtSearchSettings.Text);
            };
            CreateBorderedWrapper(headerPanel, txtSearchSettings, headerPanel.Width > 0 ? headerPanel.Width - 230 : 850, 25, 200, 24);
            if (txtSearchSettings.Parent is Panel wrapperPanel)
            {
                wrapperPanel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            }

            // Left panel - agent list panel
            var listPanel = new Panel
            {
                Location = new Point(20, 95),
                Size = new Size(230, 420),
                BackColor = Color.FromArgb(30, 41, 59), // slate-800
                BorderStyle = BorderStyle.None
            };
            listPanel.Paint += (s, e) =>
            {
                using var pen = new Pen(ThemeManager.BorderColor, 1);
                e.Graphics.DrawRectangle(pen, 0, 0, listPanel.Width - 1, listPanel.Height - 1);
            };
            Controls.Add(listPanel);

            var listHeader = new Label
            {
                Text = "AGENT COMPANIONS",
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(56, 189, 248), // sky-400
                AutoSize = true,
                Location = new Point(12, 10)
            };
            listPanel.Controls.Add(listHeader);

            _agentList.Location = new Point(12, 35);
            _agentList.Size = new Size(204, 370);
            _agentList.BorderStyle = BorderStyle.None;
            _agentList.BackColor = Color.FromArgb(15, 23, 42); // slate-900
            _agentList.ForeColor = Color.FromArgb(241, 245, 249);
            _agentList.Font = new Font("Segoe UI", 10);
            _agentList.SelectionMode = SelectionMode.One;
            _agentList.DrawMode = DrawMode.OwnerDrawFixed;
            _agentList.DrawItem += (_, e) =>
            {
                if (e.Index < 0) return;
                using var bgBrush = new SolidBrush(
                    (e.State & DrawItemState.Selected) == DrawItemState.Selected
                        ? ThemeManager.AccentColor
                        : ThemeManager.FormBg);
                e.Graphics.FillRectangle(bgBrush, e.Bounds);

                using var textBrush = new SolidBrush(ThemeManager.TextPrimary);
                e.Graphics.DrawString(
                    _agentList.Items[e.Index].ToString(),
                    new Font("Segoe UI", 10),
                    textBrush,
                    e.Bounds.X + 8, e.Bounds.Y + 4);
            };
            _agentList.ItemHeight = 32;
            _agentList.SelectedIndexChanged += (_, _) => LoadSelectedAgent();
            listPanel.Controls.Add(_agentList);

            // Agent utility buttons below ListBox
            CreateLeftButton("Add Agent", 20, 525, 70, Color.FromArgb(34, 197, 94), (_, _) => AddAgent());
            CreateLeftButton("Duplicate", 95, 525, 75, Color.FromArgb(71, 85, 105), (_, _) => DuplicateAgent());
            CreateLeftButton("Delete", 175, 525, 75, Color.FromArgb(220, 38, 38), (_, _) => DeleteAgent());

            CreateLeftButton("Manage Groups", 20, 560, 230, Color.FromArgb(99, 102, 241), (_, _) => {
                _config = AppConfig.Load();
                var form = new GroupManagerForm(_config);
                form.ShowDialog(this);
                _config = AppConfig.Load();
                LoadAgents();
                LoadSelectedAgent();
            });

            // Live Preview Panel in left column
            var previewTitle = new Label
            {
                Text = "LIVE SPRITE PREVIEW",
                ForeColor = Color.FromArgb(56, 189, 248),
                Location = new Point(22, 600),
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold)
            };
            Controls.Add(previewTitle);

            _previewPanel = new DoubleBufferedPanel
            {
                Location = new Point(20, 622),
                Size = new Size(230, 130),
                BackColor = Color.FromArgb(30, 41, 59),
                BorderStyle = BorderStyle.None
            };
            _previewPanel.Paint += PreviewPanel_Paint;
            Controls.Add(_previewPanel);

            _previewTimer = new System.Windows.Forms.Timer { Interval = 110 };
            _previewTimer.Tick += (_, _) => { _previewFrame++; _previewPanel?.Invalidate(); };

            // ==========================================
            // TABS LAYOUT: CARD PANELS & MODULAR SECTIONS
            // ==========================================
            var tabControl = new TabControl
            {
                Location = new Point(270, 85),
                Size = new Size(800, 680),
                Alignment = TabAlignment.Top,
                SizeMode = TabSizeMode.Fixed,
                ItemSize = new Size(156, 36),
                DrawMode = TabDrawMode.OwnerDrawFixed,
                BackColor = Color.FromArgb(15, 23, 42) // slate-900
            };
            _tabControl = tabControl;

            var tabIdentity = new TabPage("Identity & Territory") { BackColor = Color.FromArgb(15, 23, 42) };
            var tabAnimations = new TabPage("Animation Frames") { BackColor = Color.FromArgb(15, 23, 42) };
            var tabAi = new TabPage("AI Configuration") { BackColor = Color.FromArgb(15, 23, 42) };
            var tabPhysics = new TabPage("Physics Engine") { BackColor = Color.FromArgb(15, 23, 42) };
            var tabCustomization = new TabPage("App Customization") { BackColor = Color.FromArgb(15, 23, 42) };

            tabControl.TabPages.Add(tabIdentity);
            tabControl.TabPages.Add(tabAnimations);
            tabControl.TabPages.Add(tabAi);
            tabControl.TabPages.Add(tabPhysics);
            tabControl.TabPages.Add(tabCustomization);

            tabControl.DrawItem += (sender, e) =>
            {
                var tc = sender as TabControl;
                if (tc == null) return;
                var g = e.Graphics;
                var tabPage = tc.TabPages[e.Index];
                if (tabPage == null) return;
                var rect = tc.GetTabRect(e.Index);

                bool isSelected = tc.SelectedIndex == e.Index;

                // Tab Header Background
                using var bgBrush = new SolidBrush(isSelected ? ThemeManager.PanelBg : ThemeManager.FormBg);
                g.FillRectangle(bgBrush, rect);

                // Tab Text
                string text = tabPage.Text;
                using var font = new Font("Segoe UI Semibold", 9.5f, isSelected ? FontStyle.Bold : FontStyle.Regular);
                using var textBrush = new SolidBrush(isSelected ? ThemeManager.AccentColor : ThemeManager.TextSecondary);

                var textSize = g.MeasureString(text, font);
                var textRect = new RectangleF(
                    rect.X + (rect.Width - textSize.Width) / 2f,
                    rect.Y + (rect.Height - textSize.Height) / 2f,
                    textSize.Width,
                    textSize.Height
                );
                g.DrawString(text, font, textBrush, textRect);

                // Underline indicator for active tab
                if (isSelected)
                {
                    using var pen = new Pen(ThemeManager.AccentColor, 3f);
                    g.DrawLine(pen, rect.X, rect.Bottom - 2, rect.Right, rect.Bottom - 2);
                }
            };

            Controls.Add(tabControl);

            // ==========================================
            // TAB 1: IDENTITY & WALKING SCREEN AREA
            // ==========================================
            CreateSectionHeader(tabIdentity, "Agent Identity", 20, 25, 340);
            CreateLabeledInput(tabIdentity, "Name", 20, 70, _nameBox, 160, 26);
            CreateLabeledInput(tabIdentity, "Role / Tool System", 200, 70, _toolBox, 160, 26);

            _sizeBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _sizeBox.Items.Clear();
            _sizeBox.Items.AddRange(new[] { "Small", "Medium", "Large" });
            _sizeBox.SelectedIndex = 1;
            CreateLabeledInput(tabIdentity, "Sprite Size", 20, 130, _sizeBox, 160, 26);

            AddLabel(tabIdentity, "Body Color", 200, 110);
            _colorButton.Location = new Point(200, 130);
            _colorButton.Size = new Size(50, 26);
            _colorButton.FlatStyle = FlatStyle.Flat;
            _colorButton.FlatAppearance.BorderSize = 1;
            _colorButton.FlatAppearance.BorderColor = Color.FromArgb(71, 85, 105);
            _colorButton.BackColor = _selectedColor;
            _colorButton.Cursor = Cursors.Hand;
            _colorButton.Click += (_, _) => PickColor();
            tabIdentity.Controls.Add(_colorButton);

            var colorIndicator = new Panel
            {
                Location = new Point(260, 130),
                Size = new Size(100, 26),
                BackColor = Color.FromArgb(15, 23, 42),
                BorderStyle = BorderStyle.None
            };
            colorIndicator.Paint += (s, e) =>
            {
                using var pen = new Pen(ThemeManager.BorderColor, 1);
                e.Graphics.DrawRectangle(pen, 0, 0, colorIndicator.Width - 1, colorIndicator.Height - 1);
            };
            _colorPreview = new Panel
            {
                Location = new Point(3, 3),
                Size = new Size(94, 18),
                BackColor = _selectedColor
            };
            colorIndicator.Controls.Add(_colorPreview);
            tabIdentity.Controls.Add(colorIndicator);

            _movementTypeBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _movementTypeBox.Items.Clear();
            _movementTypeBox.Items.AddRange(new[] { "Free Roam", "Restricted Territory" });
            _movementTypeBox.SelectedIndex = 0;
            CreateLabeledInput(tabIdentity, "Movement Mode", 20, 190, _movementTypeBox, 340, 26);

            AddLabel(tabIdentity, "System Context / Personality Prompts", 20, 230);
            _contextBox.Multiline = true;
            _contextBox.ScrollBars = ScrollBars.Vertical;
            _contextBox.Font = new Font("Segoe UI", 9.5f);
            CreateBorderedWrapper(tabIdentity, _contextBox, 20, 250, 340, 120);

            // Screen territory
            CreateSectionHeader(tabIdentity, "Screen Movement Limits", 420, 25, 340);
            _walkAreaEnabled.Text = "Limit walking coordinates";
            _walkAreaEnabled.ForeColor = Color.FromArgb(241, 245, 249);
            _walkAreaEnabled.Location = new Point(420, 60);
            _walkAreaEnabled.Size = new Size(240, 24);
            _walkAreaEnabled.Font = new Font("Segoe UI", 9.5f);
            tabIdentity.Controls.Add(_walkAreaEnabled);

            coordY = 115;
            AddCoordinateInput(tabIdentity, "Min X", _x, 420, coordY);
            AddCoordinateInput(tabIdentity, "Min Y", _y, 505, coordY);
            AddCoordinateInput(tabIdentity, "Width", _w, 590, coordY);
            AddCoordinateInput(tabIdentity, "Height", _h, 675, coordY);

            _btnDragSelect = CreateUtilityButton(tabIdentity, "Drag Area Select", 420, 165, 165, 32, (_, _) => SelectWalkArea());
            _btnFullScreen = CreateUtilityButton(tabIdentity, "Full Desktop Screen", 595, 165, 165, 32, (_, _) => UseFullScreen());

            // ==========================================
            // TAB 2: ANIMATION SPRITE FRAMES (12 STATES)
            // ==========================================
            CreateSectionHeader(tabAnimations, "Custom Animation Sprite Frames", 20, 25, 740);

            // Left column (Col 1)
            CreateLabeledInput(tabAnimations, "Walking Frames", 20, 85, _walkFramesBox, 320, 26);
            CreateUtilityButton(tabAnimations, "...", 350, 84, 40, 27, (_, _) => BrowseFrames(_walkFramesBox));

            CreateLabeledInput(tabAnimations, "Thinking Frames", 20, 145, _thinkFramesBox, 320, 26);
            CreateUtilityButton(tabAnimations, "...", 350, 144, 40, 27, (_, _) => BrowseFrames(_thinkFramesBox));

            CreateLabeledInput(tabAnimations, "Sleeping Frames", 20, 205, _sleepFramesBox, 320, 26);
            CreateUtilityButton(tabAnimations, "...", 350, 204, 40, 27, (_, _) => BrowseFrames(_sleepFramesBox));

            CreateLabeledInput(tabAnimations, "Handshake Frames", 20, 265, _handshakeFramesBox, 320, 26);
            CreateUtilityButton(tabAnimations, "...", 350, 264, 40, 27, (_, _) => BrowseFrames(_handshakeFramesBox));

            CreateLabeledInput(tabAnimations, "Falling Frames", 20, 325, _fallingFramesBox, 320, 26);
            CreateUtilityButton(tabAnimations, "...", 350, 324, 40, 27, (_, _) => BrowseFrames(_fallingFramesBox));

            CreateLabeledInput(tabAnimations, "LookingAround Frames", 20, 385, _lookingAroundFramesBox, 320, 26);
            CreateUtilityButton(tabAnimations, "...", 350, 384, 40, 27, (_, _) => BrowseFrames(_lookingAroundFramesBox));

            // Right column (Col 2)
            CreateLabeledInput(tabAnimations, "Running Frames", 420, 85, _runningFramesBox, 320, 26);
            CreateUtilityButton(tabAnimations, "...", 750, 84, 40, 27, (_, _) => BrowseFrames(_runningFramesBox));

            CreateLabeledInput(tabAnimations, "Excited Frames", 420, 145, _excitedFramesBox, 320, 26);
            CreateUtilityButton(tabAnimations, "...", 750, 144, 40, 27, (_, _) => BrowseFrames(_excitedFramesBox));

            CreateLabeledInput(tabAnimations, "Sitting Frames", 420, 205, _sittingFramesBox, 320, 26);
            CreateUtilityButton(tabAnimations, "...", 750, 204, 40, 27, (_, _) => BrowseFrames(_sittingFramesBox));

            CreateLabeledInput(tabAnimations, "Stretching Frames", 420, 265, _stretchingFramesBox, 320, 26);
            CreateUtilityButton(tabAnimations, "...", 750, 264, 40, 27, (_, _) => BrowseFrames(_stretchingFramesBox));

            CreateLabeledInput(tabAnimations, "Yawning Frames", 420, 325, _yawningFramesBox, 320, 26);
            CreateUtilityButton(tabAnimations, "...", 750, 324, 40, 27, (_, _) => BrowseFrames(_yawningFramesBox));

            CreateLabeledInput(tabAnimations, "Flying Frames", 420, 385, _flyingFramesBox, 320, 26);
            CreateUtilityButton(tabAnimations, "...", 750, 384, 40, 27, (_, _) => BrowseFrames(_flyingFramesBox));

            // ==========================================
            // TAB 3: AI CONFIGURATION (PRIMARY & FALLBACK)
            // ==========================================
            CreateSectionHeader(tabAi, "Primary AI Endpoint Configuration", 20, 25, 360);
            AddLabel(tabAi, "AI Server Provider", 20, 60);
            _aiProviderBox.DropDownStyle = ComboBoxStyle.DropDownList;
            var providers = Enum.GetNames(typeof(AIProvider));
            _aiProviderBox.Items.Clear();
            _aiProviderBox.Items.AddRange(providers);
            _aiProviderBox.SelectedIndex = 0;
            _aiProviderBox.Location = new Point(20, 80);
            _aiProviderBox.Size = new Size(140, 26);
            _aiProviderBox.BackColor = Color.FromArgb(30, 41, 59);
            _aiProviderBox.ForeColor = Color.White;
            _aiProviderBox.Font = new Font("Segoe UI", 9);
            tabAi.Controls.Add(_aiProviderBox);

            coordY = 80;
            AddCoordinateInput(tabAi, "Temperature", _temperatureBox, 175, coordY);
            _temperatureBox.Location = new Point(175, 80);
            _temperatureBox.Size = new Size(90, 26);
            _temperatureBox.Minimum = 0.1m;
            _temperatureBox.Maximum = 2.0m;
            _temperatureBox.DecimalPlaces = 1;
            _temperatureBox.Value = 0.7m;

            AddCoordinateInput(tabAi, "Max Tokens", _maxTokensBox, 280, coordY = 80);
            _maxTokensBox.Location = new Point(280, 80);
            _maxTokensBox.Size = new Size(100, 26);
            _maxTokensBox.Minimum = 128;
            _maxTokensBox.Maximum = 8000;
            _maxTokensBox.Value = 700;

            CreateLabeledInput(tabAi, "Model ID", 20, 150, _modelNameBox, 360, 26);
            _primaryDetectLink = new LinkLabel
            {
                Text = "Auto-detect",
                LinkColor = Color.FromArgb(56, 189, 248), // sky-400
                ActiveLinkColor = Color.FromArgb(14, 165, 233),
                VisitedLinkColor = Color.FromArgb(56, 189, 248),
                Location = new Point(180, 130),
                AutoSize = true,
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _primaryDetectLink.LinkClicked += (s, e) => UpdateOllamaModelsAsync();
            tabAi.Controls.Add(_primaryDetectLink);

            _primaryPullLink = new LinkLabel
            {
                Text = "Pull / Download",
                LinkColor = Color.FromArgb(34, 197, 94), // green-500
                ActiveLinkColor = Color.FromArgb(22, 163, 74),
                VisitedLinkColor = Color.FromArgb(34, 197, 94),
                Location = new Point(260, 130),
                AutoSize = true,
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _primaryPullLink.LinkClicked += (s, e) => ShowPullModelDialog(_endpointBox.Text);
            tabAi.Controls.Add(_primaryPullLink);

            CreateLabeledInput(tabAi, "Authorization API Key", 20, 210, _apiKeyBox, 360, 26);
            _apiKeyBox.PasswordChar = '*';

            CreateLabeledInput(tabAi, "API Base Request Endpoint URL", 20, 270, _endpointBox, 360, 26);

            // Fallback Section
            CreateSectionHeader(tabAi, "Automatic Fallback Endpoint Router", 420, 25, 360);
            _fallbackEnabledBox.Text = "Enable fallback routing on connection error";
            _fallbackEnabledBox.ForeColor = Color.FromArgb(244, 63, 94); // Rose-500
            _fallbackEnabledBox.Location = new Point(420, 55);
            _fallbackEnabledBox.Size = new Size(320, 24);
            _fallbackEnabledBox.Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold);
            tabAi.Controls.Add(_fallbackEnabledBox);

            AddLabel(tabAi, "Fallback Provider", 420, 95);
            _fallbackProviderBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _fallbackProviderBox.Items.Clear();
            _fallbackProviderBox.Items.AddRange(providers);
            _fallbackProviderBox.SelectedIndex = 0;
            _fallbackProviderBox.Location = new Point(420, 115);
            _fallbackProviderBox.Size = new Size(140, 26);
            _fallbackProviderBox.BackColor = Color.FromArgb(30, 41, 59);
            _fallbackProviderBox.ForeColor = Color.White;
            _fallbackProviderBox.Font = new Font("Segoe UI", 9);
            tabAi.Controls.Add(_fallbackProviderBox);

            CreateLabeledInput(tabAi, "Fallback API Key", 580, 115, _fallbackApiKeyBox, 200, 26);
            _fallbackApiKeyBox.PasswordChar = '*';

            CreateLabeledInput(tabAi, "Fallback Model ID", 420, 185, _fallbackModelNameBox, 360, 26);
            _fallbackDetectLink = new LinkLabel
            {
                Text = "Auto-detect",
                LinkColor = Color.FromArgb(56, 189, 248), // sky-400
                ActiveLinkColor = Color.FromArgb(14, 165, 233),
                VisitedLinkColor = Color.FromArgb(56, 189, 248),
                Location = new Point(580, 165),
                AutoSize = true,
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _fallbackDetectLink.LinkClicked += (s, e) => UpdateOllamaModelsAsync();
            tabAi.Controls.Add(_fallbackDetectLink);

            _fallbackPullLink = new LinkLabel
            {
                Text = "Pull / Download",
                LinkColor = Color.FromArgb(34, 197, 94), // green-500
                ActiveLinkColor = Color.FromArgb(22, 163, 74),
                VisitedLinkColor = Color.FromArgb(34, 197, 94),
                Location = new Point(660, 165),
                AutoSize = true,
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _fallbackPullLink.LinkClicked += (s, e) => ShowPullModelDialog(_fallbackEndpointBox.Text);
            tabAi.Controls.Add(_fallbackPullLink);

            CreateLabeledInput(tabAi, "Fallback Base Request URL", 420, 255, _fallbackEndpointBox, 360, 26);

            // AI Event bindings
            _aiProviderBox.SelectedIndexChanged += (s, e) =>
            {
                RefreshDetectLinksVisibility();
                UpdateOllamaModelsAsync();
                
                if (!_isLoadingSelectedAgent && _aiProviderBox.SelectedItem != null)
                {
                    string selectedProvider = _aiProviderBox.SelectedItem.ToString() ?? "";
                    if (selectedProvider == "OLLAMA")
                    {
                        _endpointBox.Text = "http://127.0.0.1:11434";
                        _modelNameBox.Text = "gemma4:latest";
                    }
                    else if (selectedProvider == "OPENAI")
                    {
                        _endpointBox.Text = "https://api.openai.com/v1/chat/completions";
                        _modelNameBox.Text = "gpt-4o";
                    }
                    else if (selectedProvider == "ANTHROPIC")
                    {
                        _endpointBox.Text = "https://api.anthropic.com/v1/messages";
                        _modelNameBox.Text = "claude-3-5-sonnet-20241022";
                    }
                    else if (selectedProvider == "NVIDIA_NIM")
                    {
                        _endpointBox.Text = "https://integrate.api.nvidia.com/v1/chat/completions";
                        _modelNameBox.Text = "meta/llama-3.1-70b-instruct";
                    }
                }
            };

            _fallbackProviderBox.SelectedIndexChanged += (s, e) =>
            {
                RefreshDetectLinksVisibility();
                UpdateOllamaModelsAsync();
                
                if (!_isLoadingSelectedAgent && _fallbackProviderBox.SelectedItem != null)
                {
                    string selectedProvider = _fallbackProviderBox.SelectedItem.ToString() ?? "";
                    if (selectedProvider == "OLLAMA")
                    {
                        _fallbackEndpointBox.Text = "http://127.0.0.1:11434";
                        _fallbackModelNameBox.Text = "gemma4:latest";
                    }
                    else if (selectedProvider == "OPENAI")
                    {
                        _fallbackEndpointBox.Text = "https://api.openai.com/v1/chat/completions";
                        _fallbackModelNameBox.Text = "gpt-4o";
                    }
                    else if (selectedProvider == "ANTHROPIC")
                    {
                        _fallbackEndpointBox.Text = "https://api.anthropic.com/v1/messages";
                        _fallbackModelNameBox.Text = "claude-3-5-sonnet-20241022";
                    }
                    else if (selectedProvider == "NVIDIA_NIM")
                    {
                        _fallbackEndpointBox.Text = "https://integrate.api.nvidia.com/v1/chat/completions";
                        _fallbackModelNameBox.Text = "meta/llama-3.1-70b-instruct";
                    }
                }
            };

            _endpointBox.Leave += (s, e) => UpdateOllamaModelsAsync();
            _fallbackEndpointBox.Leave += (s, e) => UpdateOllamaModelsAsync();
            _endpointBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    UpdateOllamaModelsAsync();
                }
            };
            _fallbackEndpointBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    UpdateOllamaModelsAsync();
                }
            };

            _walkAreaEnabled.CheckedChanged += (s, e) => { UpdateWalkLimitControlsState(); TriggerInstantSync(); };
            _movementTypeBox.SelectedIndexChanged += (s, e) => { UpdateWalkLimitControlsState(); TriggerInstantSync(); };
            _x.ValueChanged += (s, e) => TriggerInstantSync();
            _y.ValueChanged += (s, e) => TriggerInstantSync();
            _w.ValueChanged += (s, e) => TriggerInstantSync();
            _h.ValueChanged += (s, e) => TriggerInstantSync();
            _sizeBox.SelectedIndexChanged += (s, e) => TriggerInstantSync();

            // ==========================================
            // TAB 4: CUSTOM PHYSICS & ANIMATIONS ENGINE
            // ==========================================
            CreateSectionHeader(tabPhysics, "Impact & Recovery Physics", 20, 25, 360);

            AddCoordinateInput(tabPhysics, "Thud Threshold (y-speed)", _thudThresh, 20, 80);
            _thudThresh.Minimum = 1.0m;
            _thudThresh.Maximum = 100.0m;
            _thudThresh.DecimalPlaces = 1;
            _thudThresh.Size = new Size(165, 26);

            AddCoordinateInput(tabPhysics, "Roll Threshold (y-speed)", _rollThresh, 205, 80);
            _rollThresh.Minimum = 1.0m;
            _rollThresh.Maximum = 100.0m;
            _rollThresh.DecimalPlaces = 1;
            _rollThresh.Size = new Size(165, 26);

            AddCoordinateInput(tabPhysics, "Thud Recovery (ms)", _thudDur, 20, 150);
            _thudDur.Minimum = 100;
            _thudDur.Maximum = 10000;
            _thudDur.Size = new Size(165, 26);

            AddCoordinateInput(tabPhysics, "Roll Recovery (ms)", _rollDur, 205, 150);
            _rollDur.Minimum = 100;
            _rollDur.Maximum = 10000;
            _rollDur.Size = new Size(165, 26);

            CreateSectionHeader(tabPhysics, "Movement & Dragging Physics", 420, 25, 360);

            AddCoordinateInput(tabPhysics, "Max Fling Speed", _maxFling, 420, 80);
            _maxFling.Minimum = 5.0m;
            _maxFling.Maximum = 200.0m;
            _maxFling.DecimalPlaces = 1;
            _maxFling.Size = new Size(165, 26);

            AddCoordinateInput(tabPhysics, "Slide-off Threshold", _slideThresh, 605, 80);
            _slideThresh.Minimum = 2.0m;
            _slideThresh.Maximum = 200.0m;
            _slideThresh.DecimalPlaces = 1;
            _slideThresh.Size = new Size(165, 26);

            AddCoordinateInput(tabPhysics, "Slide-off Prob (0-1)", _slideProb, 420, 150);
            _slideProb.Minimum = 0.0m;
            _slideProb.Maximum = 1.0m;
            _slideProb.Increment = 0.1m;
            _slideProb.DecimalPlaces = 2;
            _slideProb.Size = new Size(165, 26);

            _pushEnabled.Text = "Enable Window Pushing";
            _pushEnabled.ForeColor = Color.FromArgb(241, 245, 249);
            _pushEnabled.Location = new Point(605, 150);
            _pushEnabled.Size = new Size(175, 24);
            _pushEnabled.Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold);
            tabPhysics.Controls.Add(_pushEnabled);

            // Hook value changes to trigger instant real-time synchronization
            _thudThresh.ValueChanged += (s, e) => TriggerInstantSync();
            _rollThresh.ValueChanged += (s, e) => TriggerInstantSync();
            _thudDur.ValueChanged += (s, e) => TriggerInstantSync();
            _rollDur.ValueChanged += (s, e) => TriggerInstantSync();
            _maxFling.ValueChanged += (s, e) => TriggerInstantSync();
            _slideThresh.ValueChanged += (s, e) => TriggerInstantSync();
            _slideProb.ValueChanged += (s, e) => TriggerInstantSync();
            _pushEnabled.CheckedChanged += (s, e) => TriggerInstantSync();

            // ==========================================
            // TAB 5: APP CUSTOMIZATION
            // ==========================================
            CreateSectionHeader(tabCustomization, "Interface Styling", 20, 25, 360);

            // Theme box
            _themeBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _themeBox.Items.Clear();
            _themeBox.Items.AddRange(new[] { "Dark", "Light", "Auto" });
            CreateLabeledInput(tabCustomization, "App Theme", 20, 80, _themeBox, 165, 26);

            // Chat Layout Box
            _chatLayoutBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _chatLayoutBox.Items.Clear();
            _chatLayoutBox.Items.AddRange(new[] { "Standard", "Compact", "Wide" });
            CreateLabeledInput(tabCustomization, "Chat Layout Style", 205, 80, _chatLayoutBox, 165, 26);

            // Accent Color Row
            AddLabel(tabCustomization, "Accent Color", 20, 140);
            _accentColorsPanel.Location = new Point(20, 160);
            _accentColorsPanel.Size = new Size(350, 45);
            _accentColorsPanel.BackColor = Color.Transparent;
            _accentColorsPanel.FlowDirection = FlowDirection.LeftToRight;
            _accentColorsPanel.WrapContents = false;
            tabCustomization.Controls.Add(_accentColorsPanel);

            // Predefined accent colors
            string[] accentColors = { "#50A0FF", "#62D98B", "#FFBE55", "#FF6B8B", "#C185FF" };
            foreach (var hex in accentColors)
            {
                var colorVal = ColorTranslator.FromHtml(hex);
                var colorPill = new Button
                {
                    Size = new Size(28, 28),
                    BackColor = colorVal,
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand,
                    Margin = new Padding(0, 0, 8, 0),
                    Tag = hex
                };
                colorPill.FlatAppearance.BorderSize = 0;
                colorPill.Click += (s, e) =>
                {
                    _tempAccentColorHex = hex;
                    UpdateAccentColorSelectionVisuals();
                };
                _accentColorsPanel.Controls.Add(colorPill);
            }

            // Custom color button
            var customColorBtn = new Button
            {
                Text = "Custom...",
                Size = new Size(80, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(51, 65, 85),
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 8f),
                Cursor = Cursors.Hand,
                Margin = new Padding(4, 0, 0, 0)
            };
            customColorBtn.FlatAppearance.BorderSize = 0;
            customColorBtn.Click += (s, e) =>
            {
                using var cd = new ColorDialog();
                cd.Color = ColorTranslator.FromHtml(_tempAccentColorHex);
                if (cd.ShowDialog(this) == DialogResult.OK)
                {
                    _tempAccentColorHex = ColorTranslator.ToHtml(cd.Color);
                    UpdateAccentColorSelectionVisuals();
                }
            };
            _accentColorsPanel.Controls.Add(customColorBtn);

            // Animation Settings section
            CreateSectionHeader(tabCustomization, "Animation Settings", 20, 240, 360);

            // Animation Intensity Slider
            _animIntensitySlider.Minimum = 0;
            _animIntensitySlider.Maximum = 200;
            _animIntensitySlider.TickFrequency = 25;
            _animIntensitySlider.LargeChange = 25;
            _animIntensitySlider.Size = new Size(350, 45);
            _animIntensitySlider.Location = new Point(20, 290);
            tabCustomization.Controls.Add(_animIntensitySlider);

            var animValLabel = new Label
            {
                Location = new Point(20, 335),
                Size = new Size(350, 20),
                ForeColor = Color.FromArgb(148, 163, 184),
                Font = new Font("Segoe UI Semibold", 8f),
                Text = $"Animation Intensity: {_animIntensitySlider.Value}%"
            };
            tabCustomization.Controls.Add(animValLabel);

            _animIntensitySlider.Scroll += (s, e) =>
            {
                animValLabel.Text = $"Animation Intensity: {_animIntensitySlider.Value}%";
            };

            // Section 2: Window Behavior
            CreateSectionHeader(tabCustomization, "Window & Behavior Settings", 420, 25, 360);

            // checkboxes
            SetupCustomizationCheckbox(tabCustomization, _keepChatOnTopBox, "Always keep chat window on top", 420, 80);
            SetupCustomizationCheckbox(tabCustomization, _snapToEdgesBox, "Enable magnetic edge snapping (15px)", 420, 115);
            SetupCustomizationCheckbox(tabCustomization, _closeToTrayBox, "Minimize / Close chat window to system tray", 420, 150);
            SetupCustomizationCheckbox(tabCustomization, _showQuickActionsBox, "Show Quick Actions templates bar in chat", 420, 185);
            SetupCustomizationCheckbox(tabCustomization, _showHeaderSelectorsBox, "Show Agent/Model dropdowns in chat header", 420, 220);

            // ==========================================
            // BOTTOM UTILITY & ACTION BUTTONS
            // ==========================================
            var bottomY = 800;
            CreateActionButton("Save & Reload Settings", 270, bottomY, 230, Color.FromArgb(34, 197, 94), (_, _) => SaveAll());

            CreateActionButton("Open Chat", 515, bottomY, 150, Color.FromArgb(14, 165, 233), (_, _) => {
                if (_lastSelectedIndex >= 0 && _lastSelectedIndex < _config.Agents.Count)
                {
                    var agent = _config.Agents[_lastSelectedIndex];
                    var runningAgent = DesktopAgentForm.ActiveAgents
                        .FirstOrDefault(a => a.Agent.CharacterName == agent.CharacterName)?.Agent 
                        ?? new Agent(agent);
                        
                    var chatForm = Application.OpenForms.OfType<AgentChatForm>().FirstOrDefault();
                    if (chatForm != null)
                    {
                        chatForm.SwitchToAgent(runningAgent);
                        chatForm.BringToFront();
                    }
                    this.Close();
                }
            });

            CreateActionButton("Explore Config Directory", 680, bottomY, 210, Color.FromArgb(71, 85, 105), (_, _) =>
                System.Diagnostics.Process.Start("explorer.exe", AppConfig.ConfigFolder));

            CreateActionButton("Reset Defaults", 905, bottomY, 165, Color.FromArgb(225, 29, 72), (_, _) =>
            {
                if (MessageBox.Show("Restore default character routes? This wipes custom changes.", "Restore Defaults", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    _config.Agents.Clear();
                    _config = AppConfig.CreateDefault();
                    LoadAgents();
                    LoadSelectedAgent();
                }
            });
        }

        private void CreateSectionHeader(Control parent, string text, int x, int y, int width)
        {
            var header = new Label
            {
                Text = text,
                Font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(56, 189, 248), // Sky-400
                Location = new Point(x, y),
                AutoSize = true
            };
            parent.Controls.Add(header);

            var line = new Panel
            {
                Location = new Point(x, y + 21),
                Size = new Size(width, 1),
                BackColor = Color.FromArgb(51, 65, 85) // Slate-700
            };
            parent.Controls.Add(line);
        }

        private void CreateBorderedWrapper(Control parent, Control input, int x, int y, int width, int height)
        {
            var wrapper = new ThemedWrapperPanel
            {
                Location = new Point(x, y),
                Size = new Size(width, height),
                BackColor = ThemeManager.InputBg
            };

            wrapper.Paint += (s, e) =>
            {
                bool isFocused = input.Focused;
                using var pen = new Pen(isFocused ? ThemeManager.AccentColor : ThemeManager.BorderColor, isFocused ? 1.5f : 1f);
                e.Graphics.DrawRectangle(pen, 0, 0, wrapper.Width - 1, wrapper.Height - 1);
            };

            input.Enter += (s, e) => { wrapper.Invalidate(); };
            input.Leave += (s, e) => { wrapper.Invalidate(); };

            if (input is TextBox tb)
            {
                tb.BorderStyle = BorderStyle.None;
                tb.BackColor = ThemeManager.InputBg;
                tb.ForeColor = ThemeManager.TextPrimary;
                if (tb.Multiline)
                {
                    tb.Location = new Point(4, 4);
                    tb.Size = new Size(width - 8, height - 8);
                }
                else
                {
                    tb.Location = new Point(4, (height - tb.PreferredHeight) / 2);
                    tb.Size = new Size(width - 8, tb.PreferredHeight);
                }
                wrapper.Controls.Add(tb);
            }
            else if (input is NumericUpDown nud)
            {
                nud.BorderStyle = BorderStyle.None;
                nud.BackColor = ThemeManager.InputBg;
                nud.ForeColor = ThemeManager.TextPrimary;
                nud.Location = new Point(4, (height - nud.PreferredHeight) / 2);
                nud.Size = new Size(width - 8, nud.PreferredHeight);
                wrapper.Controls.Add(nud);
            }
            else
            {
                input.Location = new Point(1, 1);
                input.Size = new Size(width - 2, height - 2);
                wrapper.Controls.Add(input);
            }

            parent.Controls.Add(wrapper);
        }

        private void CreateLeftButton(string text, int x, int y, int width, Color backColor, EventHandler onClick)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = backColor,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += onClick;
            Controls.Add(btn);
        }

        private void CreateActionButton(string text, int x, int y, int width, Color backColor, EventHandler onClick)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, 38),
                FlatStyle = FlatStyle.Flat,
                BackColor = backColor,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += onClick;
            Controls.Add(btn);
        }

        private Button CreateUtilityButton(Control parent, string text, int x, int y, int width, int height, EventHandler onClick)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, height),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(51, 65, 85), // slate-700
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += onClick;
            parent.Controls.Add(btn);
            return btn;
        }

        private void CreateLabeledInput(Control parent, string label, int x, int y, Control input, int width, int height)
        {
            if (!string.IsNullOrEmpty(label))
            {
                AddLabel(parent, label, x, y - 20);
            }
            input.BackColor = ThemeManager.InputBg;
            input.ForeColor = ThemeManager.TextPrimary;

            if (input is TextBox tb)
            {
                tb.Font = new Font("Segoe UI", 9.5f);
                CreateBorderedWrapper(parent, tb, x, y, width, height);
            }
            else if (input is NumericUpDown nud)
            {
                nud.Font = new Font("Segoe UI", 9.5f);
                CreateBorderedWrapper(parent, nud, x, y, width, height);
            }
            else if (input is ComboBox cb)
            {
                cb.Font = new Font("Segoe UI", 9.5f);
                cb.Location = new Point(x, y);
                cb.Size = new Size(width, height);
                parent.Controls.Add(cb);
            }
            else
            {
                input.Location = new Point(x, y);
                input.Size = new Size(width, height);
                parent.Controls.Add(input);
            }
        }

        private void AddLabel(Control parent, string text, int x, int y)
        {
            parent.Controls.Add(new Label
            {
                Text = text,
                ForeColor = Color.FromArgb(148, 163, 184), // Slate-400
                Location = new Point(x, y),
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 8, FontStyle.Bold)
            });
        }

        private void AddCoordinateInput(Control parent, string label, NumericUpDown box, int x, int y)
        {
            AddLabel(parent, label, x, y - 20);
            box.Minimum = -10000;
            box.Maximum = 10000;
            box.BackColor = ThemeManager.InputBg;
            box.ForeColor = ThemeManager.TextPrimary;
            box.Font = new Font("Segoe UI", 9);
            CreateBorderedWrapper(parent, box, x, y, 76, 24);
        }

        private int _previewFrame;

        private void PreviewPanel_Paint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(ThemeManager.PanelBg);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var width = _previewPanel.Width;
            var height = _previewPanel.Height;

            DrawAgentInPreview(g, width / 2f, height / 2f + 4, width, height);

            // Draw State Tag Overlay
            if (_agentList.SelectedIndex >= 0)
            {
                string stateName = GetCurrentDemoStateName();
                using var font = new Font("Segoe UI Semibold", 8f, FontStyle.Bold);
                var size = g.MeasureString(stateName, font);

                float tagW = size.Width + 12;
                float tagH = size.Height + 6;
                float tagX = 8;
                float tagY = 8;

                using (var path = RoundedRect(new RectangleF(tagX, tagY, tagW, tagH), 4))
                {
                    using var bgBrush = new SolidBrush(Color.FromArgb(180, 15, 23, 42)); // translucent slate tag bg
                    g.FillPath(bgBrush, path);
                    using var borderPen = new Pen(Color.FromArgb(120, ThemeManager.AccentColor), 1f);
                    g.DrawPath(borderPen, path);
                }

                using var textBrush = new SolidBrush(Color.White);
                g.DrawString(stateName, font, textBrush, tagX + 6, tagY + 3);
            }

            // Draw coordinated border
            using (var pen = new Pen(ThemeManager.BorderColor, 1))
            {
                g.DrawRectangle(pen, 0, 0, width - 1, height - 1);
            }
        }

        private void DrawAgentInPreview(Graphics g, float centerX, float centerY, float width, float height)
        {
            if (_agentList.SelectedIndex < 0) return;

            var agentDef = _config.Agents[_agentList.SelectedIndex];
            var color = _selectedColor;

            int stateCycle = (_previewFrame / 30) % 12; // 0 = walking, 1 = thinking, 2 = sleeping, 3 = excited, 4 = falling, 5 = handshake, 6 = sitting, 7 = stretching, 8 = yawning, 9 = lookingAround, 10 = flying, 11 = running
            bool isThinking = stateCycle == 1;
            bool isSleeping = stateCycle == 2;
            bool isExcited = stateCycle == 3;
            bool isFalling = stateCycle == 4;
            bool isHandshake = stateCycle == 5;
            bool isSitting = stateCycle == 6;
            bool isStretching = stateCycle == 7;
            bool isYawning = stateCycle == 8;
            bool isLookingAround = stateCycle == 9;
            bool isFlying = stateCycle == 10;
            bool isRunning = stateCycle == 11;

            // Check if there are custom frames
            var frames = stateCycle switch
            {
                1 => agentDef.ThinkingFrames,
                2 => agentDef.SleepingFrames,
                3 => agentDef.ExcitedFrames ?? new(),
                4 => agentDef.FallingFrames ?? new(),
                5 => agentDef.HandshakeFrames ?? new(),
                6 => agentDef.SittingFrames ?? new(),
                7 => agentDef.StretchingFrames ?? new(),
                8 => agentDef.YawningFrames ?? new(),
                9 => agentDef.LookingAroundFrames ?? new(),
                10 => agentDef.FlyingFrames ?? new(),
                11 => agentDef.RunningFrames ?? new(),
                _ => agentDef.WalkingFrames
            };

            var validFrames = frames.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
            if (validFrames.Count > 0)
            {
                var framePath = validFrames[_previewFrame % validFrames.Count];
                var img = ImageFrameCache.Get(framePath);
                if (img != null)
                {
                    float imgW = img.Width;
                    float imgH = img.Height;
                    float targetW = width - 8;
                    float targetH = height - 8;

                    float ratio = Math.Min(targetW / imgW, targetH / imgH);
                    float drawW = imgW * ratio;
                    float drawH = imgH * ratio;

                    float drawX = (width - drawW) / 2f;
                    float drawY = (height - drawH) / 2f;

                    g.DrawImage(img, new RectangleF(drawX, drawY, drawW, drawH));
                    return;
                }
            }

            // Otherwise, render the procedural agent in full visual detail!
            var scale = 0.62f; // Scale down for preview panel
            var bob = (isThinking || isSleeping || isHandshake || isFalling) ? 0 : (float)Math.Sin(_previewFrame / 2.5) * 2f;
            var bodyW = 72 * scale;
            var bodyH = 72 * scale;
            var bodyX = centerX - bodyW / 2f;
            var bodyY = centerY - bodyH / 2f - 10 * scale + bob;

            // Soft drop shadow
            using var shadowBrush = new SolidBrush(Color.FromArgb(50, 0, 0, 0));
            g.FillEllipse(shadowBrush, bodyX + 10 * scale, bodyY + bodyH - 6 * scale, bodyW - 20 * scale, 10 * scale);

            // Body (Gradient shading)
            using var bodyBrush = new LinearGradientBrush(
                new PointF(bodyX, bodyY),
                new PointF(bodyX + bodyW, bodyY + bodyH),
                LighterColor(color),
                DarkerColor(color)
            );
            g.FillEllipse(bodyBrush, bodyX, bodyY, bodyW, bodyH);

            // Rosy blush cheeks
            using var blushBrush = new SolidBrush(Color.FromArgb(90, 255, 120, 150));
            g.FillEllipse(blushBrush, bodyX + 14 * scale, bodyY + 36 * scale, 10 * scale, 6 * scale);
            g.FillEllipse(blushBrush, bodyX + 48 * scale, bodyY + 36 * scale, 10 * scale, 6 * scale);

            // Shine crescent highlight
            using var shineBrush = new SolidBrush(Color.FromArgb(120, 255, 255, 255));
            g.FillEllipse(shineBrush, bodyX + 12 * scale, bodyY + 8 * scale, 22 * scale, 12 * scale);

            // Eyes & Mouth
            var eyeW = 14 * scale;
            var eyeH = 16 * scale;
            using var mouthPen = new Pen(Color.FromArgb(140, 20, 20, 20), 1.8f * scale);
            mouthPen.StartCap = LineCap.Round;
            mouthPen.EndCap = LineCap.Round;

            if (isSleeping)
            {
                // Closed sleeping eyes
                g.DrawArc(mouthPen, bodyX + 18 * scale, bodyY + 28 * scale, 15 * scale, 10 * scale, 0, 180);
                g.DrawArc(mouthPen, bodyX + 40 * scale, bodyY + 28 * scale, 15 * scale, 10 * scale, 0, 180);

                // Small sleeping mouth
                g.DrawLine(mouthPen, bodyX + 33 * scale, bodyY + 46 * scale, bodyX + 39 * scale, bodyY + 46 * scale);

                // Little sleeping 'z' particle in preview!
                if (_previewFrame % 10 < 5)
                {
                    using var sleepFont = new Font("Segoe UI", 8.5f * scale, FontStyle.Bold);
                    g.DrawString("zZ", sleepFont, Brushes.LightSlateGray, bodyX + 50 * scale, bodyY - 5 * scale);
                }
            }
            else if (isFalling)
            {
                // Crossed "x" eyes
                g.DrawLine(mouthPen, bodyX + 18 * scale, bodyY + 28 * scale, bodyX + 30 * scale, bodyY + 38 * scale);
                g.DrawLine(mouthPen, bodyX + 30 * scale, bodyY + 28 * scale, bodyX + 18 * scale, bodyY + 38 * scale);

                g.DrawLine(mouthPen, bodyX + 42 * scale, bodyY + 28 * scale, bodyX + 54 * scale, bodyY + 38 * scale);
                g.DrawLine(mouthPen, bodyX + 54 * scale, bodyY + 28 * scale, bodyX + 42 * scale, bodyY + 38 * scale);

                // Surprised open mouth
                g.DrawEllipse(mouthPen, bodyX + 32 * scale, bodyY + 46 * scale, 8 * scale, 9 * scale);
            }
            else if (isHandshake)
            {
                // Squinty eyes
                g.DrawArc(mouthPen, bodyX + 18 * scale, bodyY + 32 * scale, 14 * scale, 8 * scale, 180, 180);
                g.DrawArc(mouthPen, bodyX + 40 * scale, bodyY + 32 * scale, 14 * scale, 8 * scale, 180, 180);

                // Waving hand
                g.DrawLine(mouthPen, bodyX + bodyW - 4 * scale, bodyY + bodyH / 2f, bodyX + bodyW + 12 * scale, bodyY + bodyH / 2f - 6 * scale);

                // Smile mouth
                g.DrawArc(mouthPen, bodyX + 28 * scale, bodyY + 41 * scale, 22 * scale, 12 * scale, 15, 150);
            }
            else if (isExcited)
            {
                // Wide open eyes
                g.FillEllipse(Brushes.White, bodyX + 19 * scale, bodyY + 28 * scale, eyeW, eyeH);
                g.FillEllipse(Brushes.White, bodyX + 41 * scale, bodyY + 28 * scale, eyeW, eyeH);
                g.FillEllipse(Brushes.Black, bodyX + 22.5f * scale, bodyY + 32.5f * scale, 6.5f * scale, 7.5f * scale);
                g.FillEllipse(Brushes.Black, bodyX + 44.5f * scale, bodyY + 32.5f * scale, 6.5f * scale, 7.5f * scale);

                // Excited happy mouth
                using var excitedMouthBrush = new SolidBrush(Color.FromArgb(140, 20, 20, 20));
                g.FillPie(excitedMouthBrush, bodyX + 24 * scale, bodyY + 38 * scale, 24 * scale, 18 * scale, 0, 180);
            }
            else
            {
                // Normal eyes
                g.FillEllipse(Brushes.White, bodyX + 19 * scale, bodyY + 28 * scale, eyeW, eyeH);
                g.FillEllipse(Brushes.White, bodyX + 41 * scale, bodyY + 28 * scale, eyeW, eyeH);
                g.FillEllipse(Brushes.Black, bodyX + 24 * scale + 1, bodyY + 33 * scale, 5.5f * scale, 6.5f * scale);
                g.FillEllipse(Brushes.Black, bodyX + 46 * scale + 1, bodyY + 33 * scale, 5.5f * scale, 6.5f * scale);

                if (isThinking)
                {
                    // Thinking dots
                    using var dotBrush = new SolidBrush(Color.FromArgb(150, 20, 20, 20));
                    g.FillEllipse(dotBrush, bodyX + 26 * scale, bodyY + 44 * scale, 5 * scale, 5 * scale);
                    g.FillEllipse(dotBrush, bodyX + 36 * scale, bodyY + 44 * scale, 5 * scale, 5 * scale);
                    g.FillEllipse(dotBrush, bodyX + 46 * scale, bodyY + 44 * scale, 5 * scale, 5 * scale);
                }
                else
                {
                    // Smile
                    g.DrawArc(mouthPen, bodyX + 27 * scale, bodyY + 40 * scale, 22 * scale, 12 * scale, 15, 150);
                }
            }

            // Accessories
            var name = agentDef.CharacterName.ToLowerInvariant();
            if (name.Contains("nova"))
            {
                using var font = new Font("Consolas", 10 * scale, FontStyle.Bold);
                using var brush = new SolidBrush(Color.FromArgb(200, 50, 180, 255));
                g.DrawString("[  ]", font, brush, bodyX + 21 * scale, bodyY - 14 * scale);
            }
            else if (name.Contains("sage"))
            {
                using var glassesPen = new Pen(Color.FromArgb(160, 220, 220, 220), 1.5f * scale);
                g.DrawEllipse(glassesPen, bodyX + 16 * scale, bodyY + 25 * scale, 18 * scale, 18 * scale);
                g.DrawEllipse(glassesPen, bodyX + 38 * scale, bodyY + 25 * scale, 18 * scale, 18 * scale);
                g.DrawLine(glassesPen, bodyX + 34 * scale, bodyY + 34 * scale, bodyX + 38 * scale, bodyY + 34 * scale);
            }
            else if (name.Contains("bolt"))
            {
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
            else if (name.Contains("lumi"))
            {
                using var starBrush = new SolidBrush(Color.FromArgb(220, 245, 220, 90));
                var points = new[] {
                    new PointF(bodyX + 36 * scale, bodyY - 14 * scale),
                    new PointF(bodyX + 39 * scale, bodyY - 8 * scale),
                    new PointF(bodyX + 46 * scale, bodyY - 8 * scale),
                    new PointF(bodyX + 40 * scale, bodyY - 4 * scale),
                    new PointF(bodyX + 43 * scale, bodyY + 2 * scale),
                    new PointF(bodyX + 36 * scale, bodyY - 2 * scale),
                    new PointF(bodyX + 29 * scale, bodyY + 2 * scale),
                    new PointF(bodyX + 32 * scale, bodyY - 4 * scale),
                    new PointF(bodyX + 26 * scale, bodyY - 8 * scale),
                    new PointF(bodyX + 33 * scale, bodyY - 8 * scale)
                };
                g.FillPolygon(starBrush, points);
            }

            // Feet
            using var footBrush = new SolidBrush(Color.FromArgb(100, 20, 20, 20));
            var footOffset = _previewFrame % 4 < 2 ? 2 * scale : -2 * scale;
            g.FillEllipse(footBrush, bodyX + 14 * scale + footOffset, bodyY + bodyH - 3 * scale, 22 * scale, 11 * scale);
            g.FillEllipse(footBrush, bodyX + 34 * scale - footOffset, bodyY + bodyH - 3 * scale, 22 * scale, 11 * scale);
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

        private void UpdateWalkLimitControlsState()
        {
            int selIdx = _movementTypeBox.SelectedIndex;
            bool isRestricted = selIdx == 1;

            _walkAreaEnabled.Enabled = false;
            _walkAreaEnabled.Checked = isRestricted;

            _x.Enabled = isRestricted;
            _y.Enabled = isRestricted;
            _w.Enabled = isRestricted;
            _h.Enabled = isRestricted;
            
            if (_btnDragSelect != null)
            {
                _btnDragSelect.Enabled = isRestricted;
                _btnDragSelect.Text = "Set Territory";
            }
            if (_btnFullScreen != null) _btnFullScreen.Enabled = isRestricted;
        }

        private void LoadAgents()
        {
            _lastSelectedIndex = -1; // Reset to prevent saving stale data
            _agentList.Items.Clear();
            foreach (var agent in _config.Agents)
            {
                _agentList.Items.Add(agent.CharacterName);
            }
            if (_agentList.Items.Count > 0)
                _agentList.SelectedIndex = 0;
        }

        private void LoadSelectedAgent()
        {
            if (_agentList.SelectedIndex < 0) return;

            // Save the previous selected agent's settings first to prevent losing edits when selecting other list items.
            if (_lastSelectedIndex >= 0 && _lastSelectedIndex < _config.Agents.Count && _lastSelectedIndex != _agentList.SelectedIndex)
            {
                SaveSelectedAgent(_lastSelectedIndex);
                SaveWalkArea(_lastSelectedIndex);
            }
            _lastSelectedIndex = _agentList.SelectedIndex;

            _isLoadingSelectedAgent = true;
            try
            {
                var agent = _config.Agents[_agentList.SelectedIndex];
                _nameBox.Text = agent.CharacterName;
                _toolBox.Text = agent.ToolName;
                _contextBox.Text = agent.Context;
                _walkFramesBox.Text = string.Join(" ; ", agent.WalkingFrames);
                _thinkFramesBox.Text = string.Join(" ; ", agent.ThinkingFrames);
                _sleepFramesBox.Text = string.Join(" ; ", agent.SleepingFrames);
                _handshakeFramesBox.Text = string.Join(" ; ", agent.HandshakeFrames ?? new());
                _fallingFramesBox.Text = string.Join(" ; ", agent.FallingFrames ?? new());
                _excitedFramesBox.Text = string.Join(" ; ", agent.ExcitedFrames ?? new());
                _sittingFramesBox.Text = string.Join(" ; ", agent.SittingFrames ?? new());
                _stretchingFramesBox.Text = string.Join(" ; ", agent.StretchingFrames ?? new());
                _yawningFramesBox.Text = string.Join(" ; ", agent.YawningFrames ?? new());
                _lookingAroundFramesBox.Text = string.Join(" ; ", agent.LookingAroundFrames ?? new());
                _flyingFramesBox.Text = string.Join(" ; ", agent.FlyingFrames ?? new());
                _runningFramesBox.Text = string.Join(" ; ", agent.RunningFrames ?? new());
                _selectedColor = ColorTranslator.FromHtml(agent.ColorHex);
                _colorButton.BackColor = _selectedColor;
                _sizeBox.SelectedIndex = Enum.TryParse<CharacterSize>(agent.CharacterSize, true, out var sz) ? (int)sz : 1;
                int idx = agent.MovementType switch
                {
                    "FreeRoam" => 0,
                    "RestrictedTerritory" => 1,
                    // Backward compatibility mappings:
                    "FreeWindow" => 1,
                    _ => 0
                };
                _movementTypeBox.SelectedIndex = idx;

                _aiProviderBox.SelectedIndex = Enum.TryParse<AIProvider>(agent.AIProvider, out var p) ? (int)p : 0;
                _modelNameBox.Text = agent.ModelName;
                _endpointBox.Text = agent.Endpoint;
                _apiKeyBox.Text = agent.ApiKey;

                _fallbackProviderBox.SelectedIndex = Enum.TryParse<AIProvider>(agent.FallbackAIProvider, out var fp) ? (int)fp : 0;
                _fallbackModelNameBox.Text = agent.FallbackModelName;
                _fallbackEndpointBox.Text = agent.FallbackEndpoint;
                _fallbackApiKeyBox.Text = agent.FallbackApiKey;
                _fallbackEnabledBox.Checked = agent.EnableFallback;
                _temperatureBox.Value = (decimal)Math.Clamp(agent.Temperature, (double)_temperatureBox.Minimum, (double)_temperatureBox.Maximum);
                _maxTokensBox.Value = Math.Clamp(agent.MaxTokens, (int)_maxTokensBox.Minimum, (int)_maxTokensBox.Maximum);

                _thudThresh.Value = (decimal)Math.Clamp(agent.ThudVelocityThreshold, (double)_thudThresh.Minimum, (double)_thudThresh.Maximum);
                _rollThresh.Value = (decimal)Math.Clamp(agent.RollVelocityThreshold, (double)_rollThresh.Minimum, (double)_rollThresh.Maximum);
                _thudDur.Value = Math.Clamp(agent.ThudRecoveryDuration, (int)_thudDur.Minimum, (int)_thudDur.Maximum);
                _rollDur.Value = Math.Clamp(agent.RollRecoveryDuration, (int)_rollDur.Minimum, (int)_rollDur.Maximum);
                _maxFling.Value = (decimal)Math.Clamp(agent.MaxFlingSpeed, (double)_maxFling.Minimum, (double)_maxFling.Maximum);
                _slideThresh.Value = (decimal)Math.Clamp(agent.SlideOffSpeedThreshold, (double)_slideThresh.Minimum, (double)_slideThresh.Maximum);
                _slideProb.Value = (decimal)Math.Clamp(agent.SlideOffProbability, (double)_slideProb.Minimum, (double)_slideProb.Maximum);
                _pushEnabled.Checked = agent.EnableWindowPushing;

                LoadWalkArea();
                UpdateWalkLimitControlsState();
            }
            finally
            {
                _isLoadingSelectedAgent = false;
            }

            _previewTimer?.Start();
        }

        public void ReloadAgentFromDisk(string characterName)
        {
            try
            {
                var diskConfig = AppConfig.Load();
                var diskAgent = diskConfig.Agents.FirstOrDefault(a => a.CharacterName == characterName);
                if (diskAgent != null)
                {
                    var localAgent = _config.Agents.FirstOrDefault(a => a.CharacterName == characterName);
                    if (localAgent != null)
                    {
                        localAgent.MovementType = diskAgent.MovementType;
                        localAgent.WalkArea.Enabled = diskAgent.WalkArea.Enabled;
                        localAgent.WalkArea.X = diskAgent.WalkArea.X;
                        localAgent.WalkArea.Y = diskAgent.WalkArea.Y;
                        localAgent.WalkArea.Width = diskAgent.WalkArea.Width;
                        localAgent.WalkArea.Height = diskAgent.WalkArea.Height;

                        if (_agentList.SelectedIndex >= 0 && _config.Agents[_agentList.SelectedIndex].CharacterName == characterName)
                        {
                            _isLoadingSelectedAgent = true;
                            try
                            {
                                int idx = localAgent.MovementType switch
                                {
                                    "FreeRoam" => 0,
                                    "RestrictedTerritory" => 1,
                                    "FreeWindow" => 1,
                                    _ => 0
                                };
                                _movementTypeBox.SelectedIndex = idx;
                                LoadWalkArea();
                                UpdateWalkLimitControlsState();
                            }
                            finally
                            {
                                _isLoadingSelectedAgent = false;
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private void SaveSelectedAgent(int index)
        {
            if (index < 0 || index >= _config.Agents.Count) return;

            var agent = _config.Agents[index];
            agent.CharacterName = string.IsNullOrWhiteSpace(_nameBox.Text) ? "Agent" : _nameBox.Text.Trim();
            agent.ToolName = string.IsNullOrWhiteSpace(_toolBox.Text) ? "AI Assistant" : _toolBox.Text.Trim();
            agent.Context = string.IsNullOrWhiteSpace(_contextBox.Text) ? "A helpful desktop agent." : _contextBox.Text.Trim();
            agent.ColorHex = ColorTranslator.ToHtml(_selectedColor);
            agent.WalkingFrames = SplitFrames(_walkFramesBox.Text);
            agent.ThinkingFrames = SplitFrames(_thinkFramesBox.Text);
            agent.SleepingFrames = SplitFrames(_sleepFramesBox.Text);
            agent.HandshakeFrames = SplitFrames(_handshakeFramesBox.Text);
            agent.FallingFrames = SplitFrames(_fallingFramesBox.Text);
            agent.ExcitedFrames = SplitFrames(_excitedFramesBox.Text);
            agent.SittingFrames = SplitFrames(_sittingFramesBox.Text);
            agent.StretchingFrames = SplitFrames(_stretchingFramesBox.Text);
            agent.YawningFrames = SplitFrames(_yawningFramesBox.Text);
            agent.LookingAroundFrames = SplitFrames(_lookingAroundFramesBox.Text);
            agent.FlyingFrames = SplitFrames(_flyingFramesBox.Text);
            agent.RunningFrames = SplitFrames(_runningFramesBox.Text);
            agent.CharacterSize = _sizeBox.SelectedItem?.ToString() ?? "Medium";
            agent.MovementType = _movementTypeBox.SelectedIndex switch
            {
                0 => "FreeRoam",
                1 => "RestrictedTerritory",
                _ => "FreeRoam"
            };

            agent.AIProvider = _aiProviderBox.SelectedItem?.ToString() ?? AppConfig.DefaultProvider;
            agent.ModelName = _modelNameBox.Text.Trim();
            agent.Endpoint = _endpointBox.Text.Trim();
            agent.ApiKey = _apiKeyBox.Text.Trim();

            agent.FallbackAIProvider = _fallbackProviderBox.SelectedItem?.ToString() ?? "OLLAMA";
            agent.FallbackModelName = _fallbackModelNameBox.Text.Trim();
            agent.FallbackEndpoint = _fallbackEndpointBox.Text.Trim();
            agent.FallbackApiKey = _fallbackApiKeyBox.Text.Trim();
            agent.EnableFallback = _fallbackEnabledBox.Checked;
            agent.Temperature = (double)_temperatureBox.Value;
            agent.MaxTokens = (int)_maxTokensBox.Value;

            agent.ThudVelocityThreshold = (double)_thudThresh.Value;
            agent.RollVelocityThreshold = (double)_rollThresh.Value;
            agent.ThudRecoveryDuration = (int)_thudDur.Value;
            agent.RollRecoveryDuration = (int)_rollDur.Value;
            agent.MaxFlingSpeed = (double)_maxFling.Value;
            agent.SlideOffSpeedThreshold = (double)_slideThresh.Value;
            agent.SlideOffProbability = (double)_slideProb.Value;
            agent.EnableWindowPushing = _pushEnabled.Checked;

            _agentList.Items[index] = agent.CharacterName;
        }

        private void AddAgent()
        {
            if (_agentList.SelectedIndex >= 0)
            {
                SaveSelectedAgent(_agentList.SelectedIndex);
                SaveWalkArea(_agentList.SelectedIndex);
            }
            _lastSelectedIndex = -1; // Reset to prevent saving stale data

            _config.Agents.Add(new AgentDefinition
            {
                CharacterName = "New Agent",
                ToolName = AppConfig.DefaultToolName,
                Context = "Describe what this agent should do.",
                ColorHex = ColorTranslator.ToHtml(Color.DeepSkyBlue),
                CharacterSize = _sizeBox.SelectedItem?.ToString() ?? "Medium",
                WalkingFrames = new(),
                ThinkingFrames = new(),
                SleepingFrames = new(),
                HandshakeFrames = new(),
                FallingFrames = new(),
                ExcitedFrames = new(),
                SittingFrames = new(),
                StretchingFrames = new(),
                YawningFrames = new(),
                LookingAroundFrames = new(),
                FlyingFrames = new(),
                RunningFrames = new(),
                AIProvider = _aiProviderBox.SelectedItem?.ToString() ?? AppConfig.DefaultProvider,
                ModelName = AppConfig.DefaultModel,
                Endpoint = AppConfig.DefaultEndpoint,
                ApiKey = AppConfig.DefaultApiKey,
                FallbackAIProvider = AppConfig.DefaultProvider,
                FallbackModelName = AppConfig.DefaultModel,
                FallbackEndpoint = AppConfig.DefaultEndpoint,
                FallbackApiKey = AppConfig.DefaultApiKey,
                EnableFallback = false,
                Temperature = 0.7,
                MaxTokens = 700
            });
            LoadAgents();
            _agentList.SelectedIndex = _config.Agents.Count - 1;
        }

        private void DuplicateAgent()
        {
            if (_agentList.SelectedIndex < 0) return;
            SaveSelectedAgent(_agentList.SelectedIndex);
            SaveWalkArea(_agentList.SelectedIndex);
            _lastSelectedIndex = -1; // Reset to prevent saving stale data

            var source = _config.Agents[_agentList.SelectedIndex];
            _config.Agents.Add(new AgentDefinition
            {
                CharacterName = source.CharacterName + " Copy",
                ToolName = source.ToolName,
                Command = source.Command,
                Context = source.Context,
                ColorHex = source.ColorHex,
                WalkingFrames = source.WalkingFrames.ToList(),
                ThinkingFrames = source.ThinkingFrames.ToList(),
                SleepingFrames = source.SleepingFrames.ToList(),
                HandshakeFrames = source.HandshakeFrames?.ToList() ?? new(),
                FallingFrames = source.FallingFrames?.ToList() ?? new(),
                ExcitedFrames = source.ExcitedFrames?.ToList() ?? new(),
                SittingFrames = source.SittingFrames?.ToList() ?? new(),
                StretchingFrames = source.StretchingFrames?.ToList() ?? new(),
                YawningFrames = source.YawningFrames?.ToList() ?? new(),
                LookingAroundFrames = source.LookingAroundFrames?.ToList() ?? new(),
                FlyingFrames = source.FlyingFrames?.ToList() ?? new(),
                RunningFrames = source.RunningFrames?.ToList() ?? new(),
                CharacterSize = source.CharacterSize,
                AIProvider = source.AIProvider,
                ModelName = source.ModelName,
                Endpoint = source.Endpoint,
                ApiKey = source.ApiKey,
                FallbackAIProvider = source.FallbackAIProvider,
                FallbackModelName = source.FallbackModelName,
                FallbackEndpoint = source.FallbackEndpoint,
                FallbackApiKey = source.FallbackApiKey,
                EnableFallback = source.EnableFallback,
                Temperature = source.Temperature,
                MaxTokens = source.MaxTokens,
                WalkArea = new WalkAreaSettings
                {
                    Enabled = source.WalkArea.Enabled,
                    X = source.WalkArea.X,
                    Y = source.WalkArea.Y,
                    Width = source.WalkArea.Width,
                    Height = source.WalkArea.Height
                }
            });
            LoadAgents();
            _agentList.SelectedIndex = _config.Agents.Count - 1;
        }

        private void DeleteAgent()
        {
            if (_agentList.SelectedIndex < 0 || _config.Agents.Count <= 1)
            {
                MessageBox.Show("Cannot delete the last agent.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (MessageBox.Show($"Delete agent '{_config.Agents[_agentList.SelectedIndex].CharacterName}'?", "Confirm Delete",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _lastSelectedIndex = -1; // Reset to prevent saving stale data
                _config.Agents.RemoveAt(_agentList.SelectedIndex);
                LoadAgents();
            }
        }

        private void PickColor()
        {
            using var picker = new ColorDialog { Color = _selectedColor, FullOpen = true };
            if (picker.ShowDialog() == DialogResult.OK)
            {
                _selectedColor = picker.Color;
                _colorButton.BackColor = _selectedColor;
                _colorPreview.BackColor = _selectedColor;
                _previewPanel?.Invalidate();
                
                if (_agentList.SelectedIndex >= 0)
                {
                    var agent = _config.Agents[_agentList.SelectedIndex];
                    agent.ColorHex = ColorTranslator.ToHtml(_selectedColor);
                }
                TriggerInstantSync();
            }
        }

        private void TriggerInstantSync()
        {
            if (_isLoadingSelectedAgent) return;
            if (_agentList.SelectedIndex < 0) return;

            // Save only transient movement properties (type, size, bounds coordinates)
            // to avoid resetting active focus on text input controls via SaveSelectedAgent().
            var agent = _config.Agents[_agentList.SelectedIndex];
            agent.MovementType = _movementTypeBox.SelectedIndex switch
            {
                0 => "FreeRoam",
                1 => "RestrictedTerritory",
                _ => "FreeRoam"
            };
            agent.CharacterSize = _sizeBox.SelectedItem?.ToString() ?? "Medium";

            // Save customizable physics settings for instant real-time synchronization:
            agent.ThudVelocityThreshold = (double)_thudThresh.Value;
            agent.RollVelocityThreshold = (double)_rollThresh.Value;
            agent.ThudRecoveryDuration = (int)_thudDur.Value;
            agent.RollRecoveryDuration = (int)_rollDur.Value;
            agent.MaxFlingSpeed = (double)_maxFling.Value;
            agent.SlideOffSpeedThreshold = (double)_slideThresh.Value;
            agent.SlideOffProbability = (double)_slideProb.Value;
            agent.EnableWindowPushing = _pushEnabled.Checked;
            
            SaveWalkArea(_agentList.SelectedIndex);

            ConfigChanged?.Invoke(this, _config);
            Agent.NotifyAgentSettingsChanged(_config);
        }

        private void BrowseFrames(TextBox target)
        {
            using var dialog = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "Images|*.png;*.jpg;*.jpeg;*.gif;*.bmp|All files|*.*",
                Title = "Select animation frames"
            };
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                target.Text = string.Join(" ; ", dialog.FileNames);
            }
        }

        private void LoadWalkArea()
        {
            if (_agentList.SelectedIndex < 0) return;
            var agent = _config.Agents[_agentList.SelectedIndex];
            _walkAreaEnabled.Checked = agent.WalkArea.Enabled;
            _x.Value = Math.Clamp(agent.WalkArea.X, -10000, 10000);
            _y.Value = Math.Clamp(agent.WalkArea.Y, -10000, 10000);
            _w.Value = Math.Clamp(agent.WalkArea.Width, 10, 10000);
            _h.Value = Math.Clamp(agent.WalkArea.Height, 10, 10000);
        }

        private void SaveWalkArea(int index)
        {
            if (index < 0 || index >= _config.Agents.Count) return;
            var agent = _config.Agents[index];
            agent.WalkArea.Enabled = _walkAreaEnabled.Checked;
            agent.WalkArea.X = (int)_x.Value;
            agent.WalkArea.Y = (int)_y.Value;
            agent.WalkArea.Width = (int)_w.Value;
            agent.WalkArea.Height = (int)_h.Value;
        }

        private void SelectWalkArea()
        {
            using var selector = new AreaSelectionForm();
            if (selector.ShowDialog() == DialogResult.OK)
            {
                _isLoadingSelectedAgent = true;
                try
                {
                    _walkAreaEnabled.Checked = true;
                    _x.Value = selector.SelectedArea.X;
                    _y.Value = selector.SelectedArea.Y;
                    _w.Value = selector.SelectedArea.Width;
                    _h.Value = selector.SelectedArea.Height;
                    UpdateWalkLimitControlsState();
                }
                finally
                {
                    _isLoadingSelectedAgent = false;
                }
                TriggerInstantSync();
            }
        }

        private void UseFullScreen()
        {
            var bounds = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
            _isLoadingSelectedAgent = true;
            try
            {
                _walkAreaEnabled.Checked = false;
                _x.Value = bounds.X;
                _y.Value = bounds.Y;
                _w.Value = bounds.Width;
                _h.Value = bounds.Height;
                UpdateWalkLimitControlsState();
            }
            finally
            {
                _isLoadingSelectedAgent = false;
            }
            TriggerInstantSync();
        }

        private void SaveAll()
        {
            if (_agentList.SelectedIndex >= 0)
            {
                SaveSelectedAgent(_agentList.SelectedIndex);
                SaveWalkArea(_agentList.SelectedIndex);
            }
            SaveGlobalSettings();
            _config.Save();

            ThemeManager.ApplyTheme(_config.Theme, _config.AccentColorHex);
            ThemeManager.RefreshOpenForms();

            ConfigSaved?.Invoke(this, _config);
            Agent.NotifyAgentSettingsChanged(_config);
            MessageBox.Show("Settings saved successfully!\n\nAgents have been reloaded with the new configuration.", "Saved",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static List<string> SplitFrames(string text)
        {
            return text.Split(new[] { ';', ',' }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        }

        private void RefreshDetectLinksVisibility()
        {
            if (IsDisposed) return;
            bool isPrimaryOllama = _aiProviderBox.SelectedItem?.ToString() == "OLLAMA";
            if (_primaryDetectLink != null)
                _primaryDetectLink.Visible = isPrimaryOllama;
            if (_primaryPullLink != null)
                _primaryPullLink.Visible = isPrimaryOllama;

            bool isFallbackOllama = _fallbackProviderBox.SelectedItem?.ToString() == "OLLAMA";
            if (_fallbackDetectLink != null)
                _fallbackDetectLink.Visible = isFallbackOllama;
            if (_fallbackPullLink != null)
                _fallbackPullLink.Visible = isFallbackOllama;
        }

        private async void UpdateOllamaModelsAsync()
        {
            if (IsDisposed) return;

            // Update primary models if Ollama is selected
            if (_aiProviderBox.SelectedItem?.ToString() == "OLLAMA")
            {
                string endpoint = _endpointBox.Text;
                var models = await FetchOllamaModelsForEndpointAsync(endpoint);
                if (IsDisposed) return;

                if (models.Count > 0)
                {
                    string currentModel = _modelNameBox.Text;
                    _modelNameBox.Items.Clear();
                    foreach (var m in models)
                    {
                        _modelNameBox.Items.Add(m);
                    }
                    _modelNameBox.Text = currentModel;
                }
            }

            // Update fallback models if Ollama is selected
            if (_fallbackProviderBox.SelectedItem?.ToString() == "OLLAMA")
            {
                string endpoint = _fallbackEndpointBox.Text;
                var models = await FetchOllamaModelsForEndpointAsync(endpoint);
                if (IsDisposed) return;

                if (models.Count > 0)
                {
                    string currentModel = _fallbackModelNameBox.Text;
                    _fallbackModelNameBox.Items.Clear();
                    foreach (var m in models)
                    {
                        _fallbackModelNameBox.Items.Add(m);
                    }
                    _fallbackModelNameBox.Text = currentModel;
                }
            }
        }

        private static string ResolveEnvironmentVariable(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return input;
            input = input.Trim();
            if ((input.StartsWith("%") && input.EndsWith("%")) ||
                (input.StartsWith("$") && input.Length > 1))
            {
                string varName = input.TrimStart('%', '$').TrimEnd('%');
                return Environment.GetEnvironmentVariable(varName, EnvironmentVariableTarget.User)
                    ?? Environment.GetEnvironmentVariable(varName, EnvironmentVariableTarget.Process)
                    ?? Environment.GetEnvironmentVariable(varName)
                    ?? input;
            }
            return input;
        }

        private async Task<List<string>> FetchOllamaModelsForEndpointAsync(string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                return new List<string>();

            // Resolve environment variable if any
            string resolvedEndpoint = ResolveEnvironmentVariable(endpoint);

            // Parse and format the endpoint to get /api/tags
            string tagsUrl;
            try
            {
                string url = resolvedEndpoint.Trim();
                if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                    !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    url = "http://" + url;
                }

                if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    string baseUrl = $"{uri.Scheme}://{uri.Authority}";
                    tagsUrl = $"{baseUrl}/api/tags";
                }
                else
                {
                    tagsUrl = url.TrimEnd('/') + "/api/tags";
                }
            }
            catch
            {
                tagsUrl = "http://127.0.0.1:11434/api/tags";
            }

            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                var response = await client.GetAsync(tagsUrl);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("models", out var modelsElement) && modelsElement.ValueKind == JsonValueKind.Array)
                    {
                        var list = new List<string>();
                        foreach (var m in modelsElement.EnumerateArray())
                        {
                            if (m.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
                            {
                                list.Add(nameProp.GetString()!);
                            }
                        }
                        return list;
                    }
                }
            }
            catch
            {
                // Silently ignore if offline
            }
            return new List<string>();
        }

        private void ShowPullModelDialog(string endpoint)
        {
            var dialog = new Form
            {
                Text = "Pull Ollama Model",
                Width = 420,
                Height = 220,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Color.FromArgb(30, 41, 59),
                ForeColor = Color.White,
                TopMost = true
            };

            var lblPrompt = new Label
            {
                Text = "Ollama Model Name (e.g. qwen2.5:0.5b, gemma4:latest):",
                Location = new Point(20, 20),
                Size = new Size(380, 20),
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            dialog.Controls.Add(lblPrompt);

            var txtModel = new TextBox
            {
                Text = "qwen2.5:0.5b",
                BackColor = Color.FromArgb(15, 23, 42),
                ForeColor = Color.White
            };
            CreateBorderedWrapper(dialog, txtModel, 20, 45, 360, 25);

            var progressBar = new ProgressBar
            {
                Location = new Point(20, 85),
                Size = new Size(360, 20),
                Style = ProgressBarStyle.Continuous,
                Visible = false
            };
            dialog.Controls.Add(progressBar);

            var lblStatus = new Label
            {
                Text = "Ready to download.",
                Location = new Point(20, 110),
                Size = new Size(360, 35),
                ForeColor = Color.FromArgb(148, 163, 184)
            };
            dialog.Controls.Add(lblStatus);

            var btnPull = new Button
            {
                Text = "Download",
                Location = new Point(280, 140),
                Size = new Size(100, 30),
                BackColor = Color.FromArgb(34, 197, 94),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnPull.FlatAppearance.BorderSize = 0;
            dialog.Controls.Add(btnPull);

            var cts = new CancellationTokenSource();
            dialog.FormClosing += (s, e) => cts.Cancel();

            btnPull.Click += async (s, e) =>
            {
                string modelName = txtModel.Text.Trim();
                if (string.IsNullOrWhiteSpace(modelName)) return;

                btnPull.Enabled = false;
                txtModel.Enabled = false;
                progressBar.Visible = true;
                lblStatus.Text = "Connecting to Ollama...";

                try
                {
                    await PullOllamaModelAsync(endpoint, modelName, pct =>
                    {
                        if (!dialog.IsDisposed)
                        {
                            dialog.BeginInvoke(() =>
                            {
                                progressBar.Value = Math.Clamp(pct, 0, 100);
                            });
                        }
                    }, status =>
                    {
                        if (!dialog.IsDisposed)
                        {
                            dialog.BeginInvoke(() =>
                            {
                                lblStatus.Text = status;
                            });
                        }
                    }, cts.Token);

                    if (!dialog.IsDisposed)
                    {
                        dialog.BeginInvoke(() =>
                        {
                            lblStatus.Text = "Success! Model downloaded.";
                            MessageBox.Show($"Successfully pulled model '{modelName}'", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            UpdateOllamaModelsAsync();
                            dialog.Close();
                        });
                    }
                }
                catch (Exception ex)
                {
                    if (!dialog.IsDisposed)
                    {
                        dialog.BeginInvoke(() =>
                        {
                            lblStatus.Text = "Error.";
                            MessageBox.Show($"Failed to pull model: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            btnPull.Enabled = true;
                            txtModel.Enabled = true;
                        });
                    }
                }
            };

            dialog.ShowDialog(this);
        }

        private async Task PullOllamaModelAsync(string endpoint, string modelName, Action<int> onProgress, Action<string> onStatus, CancellationToken cancellationToken)
        {
            string resolvedEndpoint = ResolveEnvironmentVariable(endpoint);
            string pullUrl;
            try
            {
                string url = resolvedEndpoint.Trim();
                if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                    !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    url = "http://" + url;
                }

                if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    string baseUrl = $"{uri.Scheme}://{uri.Authority}";
                    pullUrl = $"{baseUrl}/api/pull";
                }
                else
                {
                    pullUrl = url.TrimEnd('/') + "/api/pull";
                }
            }
            catch
            {
                pullUrl = "http://127.0.0.1:11434/api/pull";
            }

            var requestBody = new { name = modelName, stream = true };
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(20);

            var content = new System.Net.Http.StringContent(JsonSerializer.Serialize(requestBody), System.Text.Encoding.UTF8, "application/json");
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, pullUrl) { Content = content };
            var response = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Ollama pull returned status code {response.StatusCode}");
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("error", out var errorProp))
                    {
                        string errorMsg = errorProp.GetString() ?? "Unknown Ollama error";
                        throw new InvalidOperationException(errorMsg);
                    }

                    if (root.TryGetProperty("status", out var statusProp))
                    {
                        string status = statusProp.GetString() ?? "";
                        onStatus(status);

                        if (root.TryGetProperty("completed", out var compProp) && 
                            root.TryGetProperty("total", out var totProp))
                        {
                            long completed = compProp.GetInt64();
                            long total = totProp.GetInt64();
                            if (total > 0)
                            {
                                int pct = (int)((completed * 100) / total);
                                onProgress(pct);
                                onStatus($"{status} ({pct}%)");
                            }
                        }
                    }
                }
                catch (JsonException)
                {
                    // Ignore parse errors on chunk boundaries
                }
            }
        }

        private void LoadGlobalSettings()
        {
            _themeBox.SelectedItem = _config.Theme ?? "Dark";
            _tempAccentColorHex = _config.AccentColorHex ?? "#50A0FF";
            _chatLayoutBox.SelectedItem = _config.ChatLayout ?? "Standard";
            _animIntensitySlider.Value = (int)Math.Max(0, Math.Min(200, _config.AnimationIntensity * 100));
            _keepChatOnTopBox.Checked = _config.KeepChatOnTop;
            _snapToEdgesBox.Checked = _config.SnapToEdges;
            _closeToTrayBox.Checked = _config.CloseToTray;
            _showQuickActionsBox.Checked = _config.ShowQuickActions;
            _showHeaderSelectorsBox.Checked = _config.ShowHeaderSelectors;

            UpdateAccentColorSelectionVisuals();
        }

        private void SaveGlobalSettings()
        {
            _config.Theme = _themeBox.SelectedItem?.ToString() ?? "Dark";
            _config.AccentColorHex = _tempAccentColorHex;
            _config.ChatLayout = _chatLayoutBox.SelectedItem?.ToString() ?? "Standard";
            _config.AnimationIntensity = _animIntensitySlider.Value / 100.0;
            _config.KeepChatOnTop = _keepChatOnTopBox.Checked;
            _config.SnapToEdges = _snapToEdgesBox.Checked;
            _config.CloseToTray = _closeToTrayBox.Checked;
            _config.ShowQuickActions = _showQuickActionsBox.Checked;
            _config.ShowHeaderSelectors = _showHeaderSelectorsBox.Checked;
        }

        private void SetupCustomizationCheckbox(Control parent, CheckBox chk, string text, int x, int y)
        {
            chk.Text = text;
            chk.ForeColor = Color.FromArgb(241, 245, 249);
            chk.Location = new Point(x, y);
            chk.Size = new Size(350, 24);
            chk.Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold);
            chk.BackColor = Color.Transparent;
            parent.Controls.Add(chk);
        }

        private void UpdateAccentColorSelectionVisuals()
        {
            foreach (Control ctrl in _accentColorsPanel.Controls)
            {
                if (ctrl is Button btn && btn.Tag is string hex)
                {
                    if (hex.Equals(_tempAccentColorHex, StringComparison.OrdinalIgnoreCase))
                    {
                        btn.FlatAppearance.BorderSize = 2;
                        btn.FlatAppearance.BorderColor = ThemeManager.TextPrimary;
                    }
                    else
                    {
                        btn.FlatAppearance.BorderSize = 0;
                    }
                }
            }
        }

        private void PerformSettingsSearch(string query)
        {
            if (string.IsNullOrWhiteSpace(query) || query == "Search settings...")
            {
                ClearSearchHighlights();
                return;
            }

            query = query.ToLower();
            TabPage? firstMatchingPage = null;

            foreach (TabPage page in _tabControl.TabPages)
            {
                bool pageHasMatch = false;
                SearchAndHighlightControls(page, query, ref pageHasMatch);

                if (pageHasMatch && firstMatchingPage == null)
                {
                    firstMatchingPage = page;
                }
            }

            if (firstMatchingPage != null)
            {
                _tabControl.SelectedTab = firstMatchingPage;
            }
        }

        private void ClearSearchHighlights()
        {
            foreach (TabPage page in _tabControl.TabPages)
            {
                foreach (Control ctrl in page.Controls)
                {
                    if (ctrl is Label lbl)
                    {
                        if (lbl.Font.Bold && lbl.Font.Size > 10)
                        {
                            lbl.ForeColor = ThemeManager.AccentColor;
                        }
                        else
                        {
                            lbl.ForeColor = ThemeManager.TextSecondary;
                        }
                    }
                    else if (ctrl is CheckBox chk)
                    {
                        chk.ForeColor = ThemeManager.TextPrimary;
                    }
                }
            }
        }

        private void SearchAndHighlightControls(Control parent, string query, ref bool pageHasMatch)
        {
            foreach (Control ctrl in parent.Controls)
            {
                if (ctrl is Label lbl)
                {
                    if (!string.IsNullOrEmpty(lbl.Text) && lbl.Text.ToLower().Contains(query))
                    {
                        lbl.ForeColor = ThemeManager.AccentColor;
                        pageHasMatch = true;
                    }
                    else
                    {
                        if (lbl.Font.Bold && lbl.Font.Size > 10)
                        {
                            lbl.ForeColor = ThemeManager.AccentColor;
                        }
                        else
                        {
                            lbl.ForeColor = ThemeManager.TextSecondary;
                        }
                    }
                }
                else if (ctrl is CheckBox chk)
                {
                    if (!string.IsNullOrEmpty(chk.Text) && chk.Text.ToLower().Contains(query))
                    {
                        chk.ForeColor = ThemeManager.AccentColor;
                        pageHasMatch = true;
                    }
                    else
                    {
                        chk.ForeColor = ThemeManager.TextPrimary;
                    }
                }
                else if (ctrl is Button btn && btn.Parent != _accentColorsPanel && btn != _colorButton)
                {
                    if (!string.IsNullOrEmpty(btn.Text) && btn.Text.ToLower().Contains(query))
                    {
                        btn.ForeColor = ThemeManager.AccentColor;
                        pageHasMatch = true;
                    }
                    else
                    {
                        btn.ForeColor = ThemeManager.TextPrimary;
                    }
                }

                if (ctrl.Controls.Count > 0)
                {
                    SearchAndHighlightControls(ctrl, query, ref pageHasMatch);
                }
            }
        }

        public void ApplyTheme()
        {
            BackColor = ThemeManager.FormBg;
            ApplyThemeToControl(this);

            _agentList.BackColor = ThemeManager.FormBg;
            _agentList.ForeColor = ThemeManager.TextPrimary;
            _agentList.Invalidate();

            if (_tabControl != null)
            {
                _tabControl.BackColor = ThemeManager.FormBg;
                foreach (TabPage page in _tabControl.TabPages)
                {
                    page.BackColor = ThemeManager.FormBg;
                }
                _tabControl.Invalidate();
            }

            this.Invalidate();
        }

        private void ApplyThemeToControl(Control parent)
        {
            foreach (Control ctrl in parent.Controls)
            {
                if (ctrl is Panel panel)
                {
                    if (ctrl == _previewPanel)
                    {
                        ctrl.BackColor = ThemeManager.PanelBg;
                    }
                    else if (ctrl.Location.Y == 0 && ctrl.Height == 75) // headerPanel
                    {
                        ctrl.BackColor = ThemeManager.PanelBg;
                    }
                    else if (ctrl.Location.X == 20 && ctrl.Location.Y == 95 && ctrl.Width == 230) // listPanel
                    {
                        ctrl.BackColor = ThemeManager.PanelBg;
                    }
                    else if (ctrl.Parent is TabPage)
                    {
                        if (ctrl.Height == 1) // line panel
                        {
                            ctrl.BackColor = ThemeManager.BorderColor;
                        }
                    }
                    else if (ctrl == _accentColorsPanel)
                    {
                        ctrl.BackColor = Color.Transparent;
                    }
                    else if (ctrl is ThemedWrapperPanel)
                    {
                        ctrl.BackColor = ThemeManager.InputBg;
                    }
                    else
                    {
                        ctrl.BackColor = ThemeManager.PanelBg;
                    }
                }
                else if (ctrl is TabControl tc)
                {
                    tc.BackColor = ThemeManager.FormBg;
                }
                else if (ctrl is TabPage tp)
                {
                    tp.BackColor = ThemeManager.FormBg;
                }
                else if (ctrl is Label lbl)
                {
                    if (lbl.ForeColor == Color.FromArgb(56, 189, 248) || lbl.Text == "LIL AGENTS CONTROL CENTER")
                    {
                        lbl.ForeColor = ThemeManager.AccentColor;
                    }
                    else if (lbl.ForeColor == Color.FromArgb(248, 250, 252) || lbl.ForeColor == Color.White)
                    {
                        lbl.ForeColor = ThemeManager.TextPrimary;
                    }
                    else
                    {
                        lbl.ForeColor = ThemeManager.TextSecondary;
                    }
                }
                else if (ctrl is TextBox tb)
                {
                    tb.BackColor = ThemeManager.InputBg;
                    tb.ForeColor = ThemeManager.TextPrimary;
                }
                else if (ctrl is ComboBox cb)
                {
                    cb.BackColor = ThemeManager.InputBg;
                    cb.ForeColor = ThemeManager.TextPrimary;
                }
                else if (ctrl is NumericUpDown nud)
                {
                    nud.BackColor = ThemeManager.InputBg;
                    nud.ForeColor = ThemeManager.TextPrimary;
                }
                else if (ctrl is CheckBox chk)
                {
                    chk.ForeColor = ThemeManager.TextPrimary;
                }
                else if (ctrl is Button btn)
                {
                    if (btn.Parent == _accentColorsPanel)
                    {
                        // Keep colors
                    }
                    else if (btn == _colorButton)
                    {
                        // Keep colors
                    }
                    else if (btn.BackColor == Color.FromArgb(34, 197, 94) || 
                             btn.BackColor == Color.FromArgb(71, 85, 105) || 
                             btn.BackColor == Color.FromArgb(220, 38, 38) || 
                             btn.BackColor == Color.FromArgb(99, 102, 241))
                    {
                        // Keep these functional colors, they look modern and clean!
                    }
                    else
                    {
                        btn.BackColor = ThemeManager.InputBg;
                        btn.ForeColor = ThemeManager.TextPrimary;
                    }
                }
                else if (ctrl is LinkLabel lnk)
                {
                    lnk.LinkColor = ThemeManager.AccentColor;
                }

                if (ctrl.Controls.Count > 0)
                {
                    ApplyThemeToControl(ctrl);
                }
                ctrl.Invalidate();
            }
        }

        private string GetCurrentDemoStateName()
        {
            int stateCycle = (_previewFrame / 30) % 12;
            return stateCycle switch
            {
                1 => "Thinking",
                2 => "Sleeping",
                3 => "Excited",
                4 => "Falling",
                5 => "Handshake",
                6 => "Sitting",
                7 => "Stretching",
                8 => "Yawning",
                9 => "Looking Around",
                10 => "Flying",
                11 => "Running",
                _ => "Walking"
            };
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

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            _previewTimer?.Dispose();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var pen = new Pen(ThemeManager.BorderColor, 1);
            e.Graphics.DrawRectangle(pen, 269, 84, 801, 681);
        }
    }

    internal class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            UpdateStyles();
        }
    }

    internal class ThemedWrapperPanel : Panel
    {
    }
}
