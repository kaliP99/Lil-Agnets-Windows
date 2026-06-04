import re
import sys

def replace_setupui(code):
    pattern = r'(private void SetupUI\(\)\s*\{)'
    match = re.search(pattern, code)
    if not match: return code
    start_idx = match.end()
    
    brace_count = 1
    end_idx = start_idx
    for i in range(start_idx, len(code)):
        if code[i] == '{': brace_count += 1
        elif code[i] == '}':
            brace_count -= 1
            if brace_count == 0:
                end_idx = i
                break
                
    new_setupui = """
            Controls.Clear();

            // 1. Switcher Sidebar Panel (Left, 55px wide)
            _sidebarPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 55,
                BackColor = Color.FromArgb(47, 47, 47) // slate-800
            };
            _sidebarPanel.Paint += SidebarPanel_Paint;
            _sidebarPanel.MouseClick += SidebarPanel_MouseClick;
            Controls.Add(_sidebarPanel);

            // 2. Collapsible Context Sidebar Panel (Left-Docked)
            _contextSidebar = new Panel
            {
                Dock = DockStyle.Left,
                Width = 0,
                BackColor = Color.FromArgb(30, 30, 30),
                Visible = false
            };
            Controls.Add(_contextSidebar);

            // 3. Splitter
            _sidebarSplitter = new Splitter
            {
                Dock = DockStyle.Left,
                Width = 4,
                BackColor = Color.FromArgb(64, 64, 64),
                Visible = false,
                MinExtra = 300,
                MinSize = 250
            };
            Controls.Add(_sidebarSplitter);

            // 4. Main Panel (Fills the rest of the window)
            _mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(33, 33, 33) // neutral-800
            };
            Controls.Add(_mainPanel);

            // Header Panel
            _headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.FromArgb(33, 33, 33)
            };
            _mainPanel.Controls.Add(_headerPanel);
            
            // WebView2 for Chat UI
            _webView = new Microsoft.Web.WebView2.WinForms.WebView2
            {
                Dock = DockStyle.Fill,
                DefaultBackgroundColor = Color.FromArgb(33, 33, 33)
            };
            _mainPanel.Controls.Add(_webView);
            
            // Add Header Controls (simplified)
            _modelSelectBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 140,
                Location = new Point(60, 18),
                BackColor = Color.FromArgb(47, 47, 47),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f)
            };
            _modelSelectBox.Items.AddRange(new object[] { "gemini-2.5-flash", "gemini-2.5-pro", "gemini-2.0-flash-thinking-exp" });
            _modelSelectBox.SelectedIndex = 1;
            _modelSelectBox.SelectedIndexChanged += (s, e) => { if (_chatService != null) _chatService.CurrentModel = _modelSelectBox.Text; };
            _headerPanel.Controls.Add(_modelSelectBox);

            _statusIndicator = new Panel { Size = new Size(8, 8), Location = new Point(220, 26), BackColor = Color.FromArgb(34, 197, 94) };
            _statusIndicator.Paint += (s, e) => { using var brush = new SolidBrush(_statusIndicator.BackColor); e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias; e.Graphics.FillEllipse(brush, 0, 0, 8, 8); };
            _headerPanel.Controls.Add(_statusIndicator);

            _statusLabel = new Label { Text = "Ready", Location = new Point(234, 21), AutoSize = true, ForeColor = Color.FromArgb(156, 163, 175), Font = new Font("Segoe UI", 9f) };
            _headerPanel.Controls.Add(_statusLabel);

            _sidebarToggleBtn = new Button { Text = "\xE17C", Font = new Font("Segoe Fluent Icons", 12f), Size = new Size(32, 32), Location = new Point(16, 14), FlatStyle = FlatStyle.Flat, ForeColor = Color.FromArgb(209, 213, 219), Cursor = Cursors.Hand };
            _sidebarToggleBtn.FlatAppearance.BorderSize = 0;
            _sidebarToggleBtn.Click += ToggleSidebar;
            _headerPanel.Controls.Add(_sidebarToggleBtn);
            
            _clearButton = new Button { Text = "Clear Chat", Location = new Point(_headerPanel.Width - 100, 16), Size = new Size(80, 28), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _clearButton.Click += (s, e) => _chatService?.ClearHistory();
            _headerPanel.Controls.Add(_clearButton);

            SetupContextSidebar();
            
            InitializeWebViewAsync();
        """
    return code[:start_idx] + new_setupui + code[end_idx:]

with open("AgentChatForm_WebView2.cs", "r", encoding="utf-8") as f:
    code = f.read()

code = replace_setupui(code)

with open("AgentChatForm_WebView2.cs", "w", encoding="utf-8") as f:
    f.write(code)

