using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace LilAgentsWindows
{
    internal class TrayApplicationContext : ApplicationContext
    {
        private readonly NotifyIcon _trayIcon;
        private readonly ContextMenuStrip _trayMenu = new();
        private readonly List<DesktopAgentForm> _agentForms = new();
        private AppConfig _config;

        public TrayApplicationContext()
        {
            _config = AppConfig.Load();
            ThemeManager.ApplyTheme(_config.Theme ?? "Dark", _config.AccentColorHex ?? "#50A0FF");

            Agent.AgentSettingsChanged += (_, updatedConfig) =>
            {
                _config = updatedConfig;
                SyncAgents();
                RebuildTrayMenu();
            };

            _trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Visible = true,
                Text = "Lil Agents Windows"
            };

            _trayIcon.ContextMenuStrip = _trayMenu;
            SyncAgents();
            RebuildTrayMenu();
            ShowAllAgents();
        }

        private void SyncAgents()
        {
            var updatedDefinitions = _config.Agents;

            // Remove forms for agents that no longer exist in the config
            var formsToRemove = new List<DesktopAgentForm>();
            foreach (var form in _agentForms)
            {
                var matchingDef = updatedDefinitions.FirstOrDefault(d => d.CharacterName == form.Agent.CharacterName);
                if (matchingDef == null)
                {
                    formsToRemove.Add(form);
                }
            }

            foreach (var form in formsToRemove)
            {
                form.Close();
                form.Dispose();
                _agentForms.Remove(form);
            }

            // Update existing or add new agents
            foreach (var definition in updatedDefinitions)
            {
                var existingForm = _agentForms.FirstOrDefault(f => f.Agent.CharacterName == definition.CharacterName);
                if (existingForm != null)
                {
                    existingForm.Agent.UpdateFromDefinition(definition);
                    existingForm.UpdateFromAgent();
                }
                else
                {
                    var agent = new Agent(definition);
                    var newForm = new DesktopAgentForm(agent, () => CurrentWalkBounds(agent));
                    _agentForms.Add(newForm);
                    
                    var bounds = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1200, 800);
                    newForm.StartPosition = FormStartPosition.Manual;
                    newForm.Location = new Point(bounds.Right - 150, bounds.Bottom - 150);
                    newForm.Show();
                }
            }
        }

        private void RebuildTrayMenu()
        {
            _trayMenu.Items.Clear();

            var controlCenterItem = new ToolStripMenuItem("Agent Control Center");
            controlCenterItem.Click += (_, _) => OpenControlCenter();
            _trayMenu.Items.Add(controlCenterItem);

            var showAllItem = new ToolStripMenuItem("Show all agents");
            showAllItem.Click += (_, _) => ShowAllAgents();
            _trayMenu.Items.Add(showAllItem);

            var hideAllItem = new ToolStripMenuItem("Hide all agents");
            hideAllItem.Click += (_, _) => HideAllAgents();
            _trayMenu.Items.Add(hideAllItem);

            _trayMenu.Items.Add(new ToolStripSeparator());

            foreach (var form in _agentForms)
            {
                var item = new ToolStripMenuItem($"Ask {form.Agent.CharacterName}");
                item.Click += (_, _) => AgentChatForm.ShowForAgent(form.Agent);
                _trayMenu.Items.Add(item);
            }

            _trayMenu.Items.Add(new ToolStripSeparator());

            var exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += (_, _) => ExitThread();
            _trayMenu.Items.Add(exitItem);
        }

        private Rectangle CurrentWalkBounds(Agent agent)
        {
            var fallback = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1200, 800);
            return agent.WalkArea.ToRectangle(fallback);
        }

        private void OpenControlCenter()
        {
            _config = AppConfig.Load();
            var manager = new AgentManagerForm(_config);
            manager.ConfigSaved += (_, updated) =>
            {
                _config = updated;
                SyncAgents();
                RebuildTrayMenu();
                ShowAllAgents();
            };
            manager.ConfigChanged += (_, updated) =>
            {
                _config = updated;
                SyncAgents();
            };
            manager.Show();
        }

        private void ShowAllAgents()
        {
            var bounds = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1200, 800);
            var spacing = 130;
            var startX = Math.Max(bounds.Left + 20, bounds.Right - (_agentForms.Count * spacing) - 20);
            var y = Math.Max(bounds.Top + 20, bounds.Bottom - 150);

            for (var i = 0; i < _agentForms.Count; i++)
            {
                var form = _agentForms[i];
                if (!form.Visible)
                {
                    form.StartPosition = FormStartPosition.Manual;
                    form.Location = new Point(startX + (i * spacing), y);
                    form.Show();
                }
                else
                {
                    form.Activate();
                }
            }
        }

        private void HideAllAgents()
        {
            foreach (var form in _agentForms.Where(form => form.Visible))
            {
                form.Hide();
            }
        }

        protected override void ExitThreadCore()
        {
            foreach (var form in _agentForms)
            {
                form.Close();
                form.Dispose();
            }

            ImageFrameCache.Clear();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            base.ExitThreadCore();
        }
    }
}
