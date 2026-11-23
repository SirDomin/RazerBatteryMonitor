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

        internal DashboardForm()
        {
            Text = "Razer Battery Monitor";
            Width = 460;
            Height = 330;
            MinimumSize = new Size(430, 280);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(24, 24, 27);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 10f);

            Label title = new Label { Text = "Bateria urządzeń bezprzewodowych", AutoSize = true, Font = new Font("Segoe UI Semibold", 16f), Location = new Point(18, 16) };
            updated = new Label { Text = "Oczekiwanie na odczyt…", AutoSize = true, ForeColor = Color.FromArgb(160, 160, 165), Location = new Point(21, 50) };
            list = new FlowLayoutPanel { Location = new Point(16, 78), Size = new Size(410, 190), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
            Controls.Add(title);
            Controls.Add(updated);
            Controls.Add(list);
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
            Panel panel = new Panel { Width = Math.Max(360, list.ClientSize.Width - 24), Height = 54, BackColor = Color.FromArgb(37, 37, 42), Margin = new Padding(0, 0, 0, 8) };
            Label glyph = new Label { Text = state.Kind == DeviceKind.Mouse ? "M" : "H", TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI Semibold", 13f), ForeColor = Color.FromArgb(57, 255, 20), Location = new Point(10, 11), Size = new Size(30, 30) };
            Label name = new Label { Text = state.Name, AutoEllipsis = true, Location = new Point(48, 7), Size = new Size(235, 21), Font = new Font("Segoe UI Semibold", 10f) };
            string detail = state.IsOnline ? (state.IsCharging ? "Ładowanie" : "Nie ładuje") : "Offline / brak świeżych danych";
            Label status = new Label { Text = detail + " · " + state.Source, AutoEllipsis = true, ForeColor = Color.FromArgb(165, 165, 170), Location = new Point(48, 29), Size = new Size(270, 18), Font = new Font("Segoe UI", 8.5f) };
            Label level = new Label { Text = state.Level.HasValue ? string.Format("{0}%{1}", state.Level.Value, state.IsCharging ? " ⚡" : "") : "--%", TextAlign = ContentAlignment.MiddleRight, Location = new Point(panel.Width - 98, 9), Size = new Size(86, 34), Anchor = AnchorStyles.Top | AnchorStyles.Right, Font = new Font("Segoe UI Semibold", 15f), ForeColor = StateColor(state) };
            panel.Controls.Add(glyph);
            panel.Controls.Add(name);
            panel.Controls.Add(status);
            panel.Controls.Add(level);
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
    }
}

