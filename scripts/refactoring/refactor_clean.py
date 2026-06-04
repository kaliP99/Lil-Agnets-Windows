import re

with open("AgentChatForm.cs", "r", encoding="utf-8") as f:
    code = f.read()

# Add using
code = code.replace("using System.Windows.Forms;", "using System.Windows.Forms;\nusing Microsoft.Web.WebView2.Core;\nusing Microsoft.Web.WebView2.WinForms;")

# Add webview field
code = code.replace("private Splitter _sidebarSplitter = null!;", "private Splitter _sidebarSplitter = null!;\n        private WebView2 _webView = null!;")

# Find the end of SetupUI
# Let's insert the WebView2 instantiation right before PositionInputControls();
code = code.replace("PositionInputControls();", """
            // --- WEBVIEW2 INJECTION ---
            _webView = new WebView2
            {
                Dock = DockStyle.Fill,
                DefaultBackgroundColor = Color.FromArgb(33, 33, 33)
            };
            _mainPanel.Controls.Add(_webView);
            _webView.BringToFront(); // Ensure it sits above the hidden panels
            
            // Hide all the old native UI
            if (_chatPanel != null) _chatPanel.Visible = false;
            if (_inputContainer != null) _inputContainer.Visible = false;
            if (_dropZoneOverlay != null) _dropZoneOverlay.Visible = false;
            
            InitializeWebViewAsync();

            PositionInputControls();
""")

# Insert InitializeWebViewAsync and hooks before HandleChatServiceEvents
webview_init = """
        private async void InitializeWebViewAsync()
        {
            await _webView.EnsureCoreWebView2Async(null);
            _webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
            
            string htmlPath = System.IO.Path.Combine(Application.StartupPath, "chat_ui.html");
            if (System.IO.File.Exists(htmlPath))
            {
                _webView.Source = new Uri(htmlPath);
            }
            
            // Sync initial state
            _webView.NavigationCompleted += (s, e) => {
                LoadChatHistory(); // Push history to JS
            };
        }

        private async void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var msg = JsonSerializer.Deserialize<JsonElement>(e.WebMessageAsJson);
                string type = msg.GetProperty("type").GetString() ?? "";
                
                if (type == "user_message")
                {
                    string text = msg.GetProperty("text").GetString() ?? "";
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        // Set text in the invisible message box to reuse existing logic
                        _messageBox.Text = text;
                        await SendCurrentMessageAsync();
                    }
                }
                else if (type == "upload_click")
                {
                    using var ofd = new OpenFileDialog { Multiselect = true, Title = "Attach Files" };
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        foreach(var file in ofd.FileNames) {
                            ProcessBinaryFile(file);
                        }
                    }
                }
            }
            catch { }
        }
"""

code = code.replace("private void HandleChatServiceEvents()", webview_init + "\n        private void HandleChatServiceEvents()")

# Hook into AddAgentDelta to stream to JS
code = code.replace("private void AddAgentDelta(string id, string delta)", """private void AddAgentDelta(string id, string delta)
        {
            if (_webView != null && _webView.CoreWebView2 != null && _agentMessages.ContainsKey(id))
            {
                string safeText = JsonSerializer.Serialize(_agentMessages[id].Content);
                _webView.CoreWebView2.ExecuteScriptAsync($"streamAgentDelta('{id}', {safeText}, false);");
            }
            // keep old logic alive just in case
""")

# Hook into StartAgentResponseBubble
code = code.replace("private void StartAgentResponseBubble(string id, string agentName)", """private void StartAgentResponseBubble(string id, string agentName)
        {
            if (_webView != null && _webView.CoreWebView2 != null)
            {
                string safeName = JsonSerializer.Serialize(agentName);
                _webView.CoreWebView2.ExecuteScriptAsync($"startAgentMessage('{id}', {safeName}, null);");
            }
            // keep old logic alive
""")

with open("AgentChatForm.cs", "w", encoding="utf-8") as f:
    f.write(code)

