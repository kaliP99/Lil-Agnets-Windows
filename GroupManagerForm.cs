using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace LilAgentsWindows
{
    internal class GroupManagerForm : Form
    {
        private AppConfig _config;
        private ListBox _groupList;
        private ListBox _agentList;
        private TextBox _groupNameBox;

        public event EventHandler? GroupsChanged;

        public GroupManagerForm(AppConfig config)
        {
            _config = config;
            Text = "Group Manager";
            Size = new Size(480, 360);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(15, 23, 42); // slate-900
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var titleFont = new Font("Segoe UI Semibold", 11f, FontStyle.Bold);
            var regularFont = new Font("Segoe UI", 9.5f);

            var groupTitle = new Label { Text = "Groups", ForeColor = Color.FromArgb(56, 189, 248), Font = titleFont, Location = new Point(20, 20), AutoSize = true };
            Controls.Add(groupTitle);

            _groupList = new ListBox
            {
                Location = new Point(20, 50),
                Size = new Size(180, 200),
                BackColor = Color.FromArgb(30, 41, 59),
                ForeColor = Color.White,
                Font = regularFont,
                BorderStyle = BorderStyle.FixedSingle
            };
            _groupList.SelectedIndexChanged += GroupList_SelectedIndexChanged;
            Controls.Add(_groupList);

            var agentTitle = new Label { Text = "Agents in Group", ForeColor = Color.FromArgb(56, 189, 248), Font = titleFont, Location = new Point(220, 20), AutoSize = true };
            Controls.Add(agentTitle);

            _agentList = new ListBox
            {
                Location = new Point(220, 50),
                Size = new Size(220, 200),
                BackColor = Color.FromArgb(30, 41, 59),
                ForeColor = Color.White,
                Font = regularFont,
                SelectionMode = SelectionMode.MultiSimple,
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(_agentList);

            var nameLabel = new Label { Text = "Group Name:", ForeColor = Color.FromArgb(148, 163, 184), Font = regularFont, Location = new Point(20, 270), AutoSize = true };
            Controls.Add(nameLabel);

            _groupNameBox = new TextBox
            {
                Location = new Point(120, 268),
                Size = new Size(180, 26),
                BackColor = Color.FromArgb(30, 41, 59),
                ForeColor = Color.White,
                Font = regularFont,
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(_groupNameBox);

            var btnAdd = new Button { Text = "Save Group", Location = new Point(310, 267), Size = new Size(130, 28), BackColor = Color.FromArgb(37, 99, 235), ForeColor = Color.White, Font = regularFont, FlatStyle = FlatStyle.Flat };
            btnAdd.FlatAppearance.BorderSize = 0;
            btnAdd.Click += BtnAdd_Click;
            Controls.Add(btnAdd);

            var btnDelete = new Button { Text = "Delete Group", Location = new Point(310, 305), Size = new Size(130, 28), BackColor = Color.FromArgb(220, 38, 38), ForeColor = Color.White, Font = regularFont, FlatStyle = FlatStyle.Flat };
            btnDelete.FlatAppearance.BorderSize = 0;
            btnDelete.Click += BtnDelete_Click;
            Controls.Add(btnDelete);

            RefreshGroupList();
        }

        private void RefreshGroupList()
        {
            _groupList.Items.Clear();
            foreach (var g in _config.Groups) _groupList.Items.Add(g.GroupName);

            _agentList.Items.Clear();
            foreach (var a in _config.Agents) _agentList.Items.Add(a.CharacterName);
        }

        private void GroupList_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_groupList.SelectedIndex < 0) return;
            var group = _config.Groups[_groupList.SelectedIndex];
            _groupNameBox.Text = group.GroupName;

            _agentList.ClearSelected();
            for (int i = 0; i < _agentList.Items.Count; i++)
            {
                if (group.MemberNames.Contains(_agentList.Items[i].ToString() ?? ""))
                {
                    _agentList.SetSelected(i, true);
                }
            }
        }

        private void BtnAdd_Click(object? sender, EventArgs e)
        {
            var name = _groupNameBox.Text.Trim();
            if (string.IsNullOrEmpty(name)) return;

            var group = _config.Groups.FirstOrDefault(g => g.GroupName == name);
            if (group == null)
            {
                group = new AgentGroup { GroupName = name };
                _config.Groups.Add(group);
            }

            group.MemberNames.Clear();
            foreach (var item in _agentList.SelectedItems)
            {
                group.MemberNames.Add(item.ToString()!);
            }

            _config.Save();
            RefreshGroupList();
            _groupList.SelectedItem = name;
            GroupsChanged?.Invoke(this, EventArgs.Empty);
            MessageBox.Show("Group saved! Open Agent Chat to see the group in the sidebar.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BtnDelete_Click(object? sender, EventArgs e)
        {
            if (_groupList.SelectedIndex < 0) return;
            var group = _config.Groups[_groupList.SelectedIndex];
            _config.Groups.Remove(group);
            _config.Save();
            RefreshGroupList();
            _groupNameBox.Clear();
            _agentList.ClearSelected();
            GroupsChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
