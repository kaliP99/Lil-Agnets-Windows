import re

with open("AgentChatForm.cs", "r", encoding="utf-8") as f:
    code = f.read()

# 1. Change header to span full width
code = code.replace("_mainPanel.Controls.Add(_headerPanel);", """Controls.Add(_headerPanel);
            _headerPanel.SendToBack(); // Docks to top across full width""")

# 2. Modern Model Dropdown
# Instead of ComboBox, use a Button with ContextMenuStrip
dropdown_old = """            _modelSelectBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(33, 33, 33),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8.5f),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(100, 24),
                Location = new Point(176, 20),
                Cursor = Cursors.Hand
            };
            _headerPanel.Controls.Add(_modelSelectBox);"""

dropdown_new = """            var modelMenu = new ContextMenuStrip
            {
                BackColor = Color.FromArgb(33, 33, 33),
                ForeColor = Color.White,
                ShowImageMargin = false,
                Font = new Font("Segoe UI", 9f)
            };
            
            _modelSelectBtn = new Button
            {
                Text = "Loading...",
                BackColor = Color.FromArgb(47, 47, 47),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(130, 28),
                Location = new Point(176, 18),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleLeft
            };
            _modelSelectBtn.FlatAppearance.BorderSize = 1;
            _modelSelectBtn.FlatAppearance.BorderColor = Color.FromArgb(64, 64, 64);
            _modelSelectBtn.Click += (s, e) => modelMenu.Show(_modelSelectBtn, new Point(0, _modelSelectBtn.Height));
            _headerPanel.Controls.Add(_modelSelectBtn);
            
            // Wait, I need a class-level field for _modelSelectBtn so we can update it, and I should hook up the items.
            // I will do that in the LoadModels logic.
"""

code = code.replace(dropdown_old, dropdown_new)

# Add _modelSelectBtn field to class
code = code.replace("private ComboBox _modelSelectBox = null!;", "private ComboBox _modelSelectBox = null!; // deprecated\n        private Button _modelSelectBtn = null!;")

with open("AgentChatForm.cs", "w", encoding="utf-8") as f:
    f.write(code)

