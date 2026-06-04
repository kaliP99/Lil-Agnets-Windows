import re

with open("AgentChatForm.cs", "r", encoding="utf-8") as f:
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
            
            // Sync initial state
            _webView.NavigationCompleted += (s, e) => {
                LoadChatHistory(); // Push history to JS
            };
        }

        private async void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var msg = JsonSerializer.Deserialize<System.Text.Json.JsonElement>(e.WebMessageAsJson);
                string type = msg.GetProperty("type").GetString() ?? "";
                
                if (type == "user_message")
                {
                    string text = msg.GetProperty("text").GetString() ?? "";
                    if (!string.IsNullOrWhiteSpace(text))
                    {
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
        
        private async Task SendCurrentMessageAsync()
"""

code = code.replace("private async Task SendCurrentMessageAsync()", webview_init)

with open("AgentChatForm.cs", "w", encoding="utf-8") as f:
    f.write(code)

