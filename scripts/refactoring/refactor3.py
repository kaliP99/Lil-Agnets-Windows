import sys

with open("AgentChatForm_WebView2.cs", "r", encoding="utf-8") as f:
    code = f.read()

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
                        await _chatService.PostMessageAsync(_activeGroup, text, null);
                    }
                }
                else if (type == "upload_click")
                {
                    using var ofd = new OpenFileDialog { Multiselect = true, Title = "Attach Files" };
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        // Add files logic
                    }
                }
            }
            catch { }
        }
"""

# Insert right before HandleChatServiceEvents()
code = code.replace("private void HandleChatServiceEvents()", webview_init + "\n        private void HandleChatServiceEvents()")

with open("AgentChatForm_WebView2.cs", "w", encoding="utf-8") as f:
    f.write(code)
