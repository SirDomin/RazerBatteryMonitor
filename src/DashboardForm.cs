using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace RazerBatteryMonitor
{
    internal sealed class DashboardForm : Form
    {
        private readonly FlowLayoutPanel list;
        private readonly Label updated;
        private readonly ComboBox monitorSelector;
        private readonly CheckBox fullscreenOverlay;
        private bool loadingSettings;

        internal event Action<string, bool> OverlaySettingsChanged;

        internal DashboardForm()
        {
            Text = "Razer Battery Monitor";
            Width = 620;
            Height = 520;
            MinimumSize = new Size(560, 450);
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96f, 96f);
            BackColor = Color.FromArgb(24, 24, 27);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 10f);

            Label title = new Label { Text = "Bateria urządzeń bezprzewodowych", AutoSize = true, Font = new Font("Segoe UI Semibold", 16f), Location = new Point(18, 16) };
            updated = new Label { Text = "Oczekiwanie na odczyt…", AutoSize = true, ForeColor = Color.FromArgb(160, 160, 165), Location = new Point(21, 50) };
            list = new FlowLayoutPanel { Location = new Point(16, 82), Size = new Size(570, 300), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
            Panel settingsPanel = new Panel { Location = new Point(16, 394), Size = new Size(570, 80), Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right, BackColor = Color.FromArgb(31, 31, 35) };
            Label monitorLabel = new Label { Text = "Monitor paska:", AutoSize = true, Location = new Point(12, 14), ForeColor = Color.FromArgb(205, 205, 210) };
            monitorSelector = new ComboBox { Location = new Point(132, 10), Width = 270, DropDownStyle = ComboBoxStyle.DropDownList };
            fullscreenOverlay = new CheckBox { Text = "Pokazuj wskaźniki nad grami pełnoekranowymi", AutoSize = true, Location = new Point(12, 48), ForeColor = Color.White };
            monitorSelector.SelectedIndexChanged += delegate { RaiseSettingsChanged(); };
            fullscreenOverlay.CheckedChanged += delegate { RaiseSettingsChanged(); };
            settingsPanel.Controls.Add(monitorLabel);
            settingsPanel.Controls.Add(monitorSelector);
            settingsPanel.Controls.Add(fullscreenOverlay);
            Controls.Add(title);
            Controls.Add(updated);
            Controls.Add(list);
            Controls.Add(settingsPanel);
        }

        internal void ConfigureSettings(string monitorDeviceName, bool showOverFullscreen)
        {
            loadingSettings = true;
            monitorSelector.Items.Clear();
            Screen[] screens = Screen.AllScreens;
            int selectedIndex = -1;
            for (int index = 0; index < screens.Length; index++)
            {
                Screen screen = screens[index];
                int slash = screen.DeviceName.LastIndexOf('\\');
                string identifier = slash >= 0 ? screen.DeviceName.Substring(slash + 1) : screen.DeviceName;
                string label = (screen.Primary ? "Główny" : "Dodatkowy") + " — " + identifier;
                MonitorChoice choice = new MonitorChoice(screen.DeviceName,
                    string.Format("{0} ({1}×{2})", label, screen.Bounds.Width, screen.Bounds.Height));
                monitorSelector.Items.Add(choice);
                if (string.Equals(screen.DeviceName, monitorDeviceName, StringComparison.OrdinalIgnoreCase)) selectedIndex = index;
            }
            if (monitorSelector.Items.Count > 0) monitorSelector.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;
            fullscreenOverlay.Checked = showOverFullscreen;
            loadingSettings = false;
        }

        private void RaiseSettingsChanged()
        {
            if (loadingSettings) return;
            MonitorChoice choice = monitorSelector.SelectedItem as MonitorChoice;
            Action<string, bool> handler = OverlaySettingsChanged;
            if (choice != null && handler != null) handler(choice.DeviceName, fullscreenOverlay.Checked);
        }

        internal void UpdateStates(IList<DeviceState> states)
        {
            if (InvokeRequired) { BeginInvoke(new Action<IList<DeviceState>>(UpdateStates), states); return; }
            list.SuspendLayout();
            list.Controls.Clear();
            foreach (DeviceState state in states) list.Controls.Add(CreateCard(state));
            list.ResumeLayout();
            updated.Text = string.Format("Ostatnie odświeżenie: {0:HH:mm:ss}", DateTime.Now);
        }

        private Control CreateCard(DeviceState state)
        {
            int scale = Math.Max(1, DeviceDpi);
            int cardHeight = (int)Math.Round(70.0 * scale / 96.0);
            int glyphWidth = (int)Math.Round(42.0 * scale / 96.0);
            int levelWidth = (int)Math.Round(96.0 * scale / 96.0);
            int gap = (int)Math.Round(8.0 * scale / 96.0);
            Panel panel = new Panel
            {
                Width = Math.Max(360, list.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 8),
                Height = cardHeight,
                BackColor = Color.FromArgb(37, 37, 42),
                Margin = new Padding(0, 0, 0, gap)
            };
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = new Padding(gap, 4, gap, 4)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, glyphWidth));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, levelWidth));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 52f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 48f));

            Label glyph = new Label { Text = state.Kind == DeviceKind.Mouse ? "M" : "H", Dock = DockStyle.Fill, Margin = Padding.Empty, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI Semibold", 13f), ForeColor = Color.FromArgb(57, 255, 20) };
            Label name = new Label { Text = state.Name, Dock = DockStyle.Fill, Margin = Padding.Empty, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI Semibold", 10f) };
            string detail = state.IsOnline ? (state.IsCharging ? "Ładowanie" : "Nie ładuje") : "Offline / brak świeżych danych";
            Label status = new Label { Text = detail + " · " + state.Source, Dock = DockStyle.Fill, Margin = Padding.Empty, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(165, 165, 170), Font = new Font("Segoe UI", 8.5f) };
            Label level = new Label { Text = state.Level.HasValue ? string.Format("{0}%{1}", state.Level.Value, state.IsCharging ? " ⚡" : "") : "--%", Dock = DockStyle.Fill, Margin = Padding.Empty, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI Semibold", 15f), ForeColor = StateColor(state) };
            layout.Controls.Add(glyph, 0, 0);
            layout.SetRowSpan(glyph, 2);
            layout.Controls.Add(name, 1, 0);
            layout.Controls.Add(status, 1, 1);
            layout.Controls.Add(level, 2, 0);
            layout.SetRowSpan(level, 2);
            panel.Controls.Add(layout);
            return panel;
        }

        private static Color StateColor(DeviceState state)
        {
            if (!state.IsOnline || !state.Level.HasValue) return Color.Gray;
            if (state.IsCharging) return Color.FromArgb(57, 255, 20);
            if (state.Level.Value <= 10) return Color.FromArgb(255, 70, 70);
            if (state.Level.Value <= 25) return Color.FromArgb(255, 177, 46);
            return Color.White;
        }

        private sealed class MonitorChoice
        {
            internal readonly string DeviceName;
            private readonly string label;

            internal MonitorChoice(string deviceName, string label)
            {
                DeviceName = deviceName;
                this.label = label;
            }

            public override string ToString() { return label; }
        }
    }
}

