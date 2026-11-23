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
            Width = 580;
            Height = 430;
            MinimumSize = new Size(520, 360);
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96f, 96f);
            BackColor = Color.FromArgb(24, 24, 27);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 10f);

            Label title = new Label { Text = "Bateria urządzeń bezprzewodowych", AutoSize = true, Font = new Font("Segoe UI Semibold", 16f), Location = new Point(18, 16) };
            updated = new Label { Text = "Oczekiwanie na odczyt…", AutoSize = true, ForeColor = Color.FromArgb(160, 160, 165), Location = new Point(21, 50) };
            list = new FlowLayoutPanel { Location = new Point(16, 82), Size = new Size(530, 300), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
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
    }
}

