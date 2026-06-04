import re

with open("AgentChatForm.cs", "r", encoding="utf-8") as f:
    code = f.read()

# Replace PopulateModelsDropdown
old_populate = """        private void PopulateModelsDropdown()
        {
            _modelSelectBox.Items.Clear();
            if (_agent.AIProvider == "OLLAMA")
            {
                _modelSelectBox.Items.Add(_agent.ModelName);
                _modelSelectBox.Text = _agent.ModelName;
                LoadEndpointOllamaModelsAsync();
            }
            else
            {
                // General default model options
                string[] defaults = { "gpt-4o", "claude-3-5-sonnet", "gemini-1.5-pro", "deepseek-chat" };
                _modelSelectBox.Items.AddRange(defaults);
                if (!_modelSelectBox.Items.Contains(_agent.ModelName))
                {
                    _modelSelectBox.Items.Add(_agent.ModelName);
                }
                _modelSelectBox.Text = _agent.ModelName;
            }
        }"""

new_populate = """        private ContextMenuStrip _modelMenu = new ContextMenuStrip { BackColor = Color.FromArgb(33, 33, 33), ForeColor = Color.White, ShowImageMargin = false, Font = new Font("Segoe UI", 9f) };

        private void PopulateModelsDropdown()
        {
            _modelMenu.Items.Clear();
            _modelSelectBtn.Text = _agent.ModelName + " \u23F7"; // down arrow
            
            if (_agent.AIProvider == "OLLAMA")
            {
                AddModelMenuItem(_agent.ModelName);
                LoadEndpointOllamaModelsAsync();
            }
            else
            {
                string[] defaults = { "gpt-4o", "claude-3-5-sonnet", "gemini-1.5-pro", "gemini-2.5-flash", "deepseek-chat" };
                foreach(var m in defaults) AddModelMenuItem(m);
                
                if (Array.IndexOf(defaults, _agent.ModelName) == -1)
                {
                    AddModelMenuItem(_agent.ModelName);
                }
            }
            
            // Re-bind the click event properly
            _modelSelectBtn.Click -= ModelSelectBtn_Click;
            _modelSelectBtn.Click += ModelSelectBtn_Click;
        }

        private void ModelSelectBtn_Click(object? sender, EventArgs e)
        {
            _modelMenu.Show(_modelSelectBtn, new Point(0, _modelSelectBtn.Height));
        }

        private void AddModelMenuItem(string modelName)
        {
            var item = new ToolStripMenuItem(modelName);
            item.Click += (s, e) => {
                _agent.ModelName = modelName;
                _modelSelectBtn.Text = modelName + " \u23F7";
                _chatService = new LocalAgentChatService(_agent);
                // We keep history, just the provider changes contextually for next message
            };
            _modelMenu.Items.Add(item);
        }"""
code = code.replace(old_populate, new_populate)

# Replace LoadEndpointOllamaModelsAsync updates
old_ollama = """                        if (modelsList.Count > 0 && !IsDisposed)
                        {
                            string activeModel = _modelSelectBox.Text;
                            _modelSelectBox.Items.Clear();
                            foreach (var m in modelsList)
                            {
                                _modelSelectBox.Items.Add(m);
                            }
                            _modelSelectBox.Text = activeModel;
                        }"""

new_ollama = """                        if (modelsList.Count > 0 && !IsDisposed)
                        {
                            _modelMenu.Items.Clear();
                            foreach (var m in modelsList)
                            {
                                AddModelMenuItem(m);
                            }
                        }"""
code = code.replace(old_ollama, new_ollama)

# Delete the ModelSelectBox_SelectedIndexChanged hook in SetupUI
code = code.replace("_modelSelectBox.SelectedIndexChanged += ModelSelectBox_SelectedIndexChanged;", "")
# Replace the ContextMenuStrip creation in SetupUI since we moved it to class level
code = code.replace("""            var modelMenu = new ContextMenuStrip
            {
                BackColor = Color.FromArgb(33, 33, 33),
                ForeColor = Color.White,
                ShowImageMargin = false,
                Font = new Font("Segoe UI", 9f)
            };""", "")
code = code.replace("modelMenu.Show", "_modelMenu.Show")

with open("AgentChatForm.cs", "w", encoding="utf-8") as f:
    f.write(code)
