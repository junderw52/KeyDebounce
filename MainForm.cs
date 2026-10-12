using System;
using System.Drawing;
using System.Windows.Forms;

namespace KeyDebounce
{
    internal sealed class MainForm : Form
    {
        const int DefaultBounceMs = 40;

        readonly Label status = new Label { AutoSize = true, Margin = new Padding(0, 0, 0, 10) };
        readonly CheckBox enable = new CheckBox { Text = "Enable key debounce", AutoSize = true };
        readonly NumericUpDown bounce = new NumericUpDown { Minimum = 5, Maximum = 1000, Value = DefaultBounceMs, Width = 80 };
        readonly CheckBox matchRepeat = new CheckBox { Text = "Use my Windows key repeat settings", AutoSize = true, Checked = true };
        readonly NumericUpDown delay = new NumericUpDown { Minimum = 100, Maximum = 2000, Increment = 50, Value = 500, Width = 80 };
        readonly NumericUpDown repeat = new NumericUpDown { Minimum = 10, Maximum = 1000, Value = 33, Width = 80 };
        readonly Button apply = new Button { Text = "Apply", AutoSize = true };
        readonly Button refresh = new Button { Text = "Refresh", AutoSize = true };

        public MainForm()
        {
            Text = "KeyDebounce";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Font = new Font("Segoe UI", 9f);
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            status.Font = new Font(Font, FontStyle.Bold);

            var grid = new TableLayoutPanel
            {
                ColumnCount = 2,
                AutoSize = true,
                Padding = new Padding(14),
                Dock = DockStyle.Fill,
            };

            grid.Controls.Add(status, 0, 0);
            grid.SetColumnSpan(status, 2);

            grid.Controls.Add(enable, 0, 1);
            grid.SetColumnSpan(enable, 2);

            grid.Controls.Add(Caption("Ignore repeat presses within (ms):"), 0, 2);
            grid.Controls.Add(bounce, 1, 2);

            grid.Controls.Add(matchRepeat, 0, 3);
            grid.SetColumnSpan(matchRepeat, 2);

            grid.Controls.Add(Caption("Repeat delay (ms):"), 0, 4);
            grid.Controls.Add(delay, 1, 4);
            grid.Controls.Add(Caption("Repeat rate (ms per repeat):"), 0, 5);
            grid.Controls.Add(repeat, 1, 5);

            var buttons = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 10, 0, 0) };
            buttons.Controls.Add(apply);
            buttons.Controls.Add(refresh);
            grid.Controls.Add(buttons, 0, 6);
            grid.SetColumnSpan(buttons, 2);

            var note = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(340, 0),
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(0, 10, 0, 0),
                Text = "Settings are saved by Windows and survive restarts, so this app does not need to keep running. " +
                       "Avoid changing Settings > Accessibility > Keyboard > Filter keys afterwards, since it can overwrite these values.",
            };
            grid.Controls.Add(note, 0, 7);
            grid.SetColumnSpan(note, 2);

            Controls.Add(grid);

            enable.CheckedChanged += (s, e) => UpdateEnabledState();
            matchRepeat.CheckedChanged += (s, e) => { FillWindowsRepeat(); UpdateEnabledState(); };
            apply.Click += (s, e) => Apply();
            refresh.Click += (s, e) => LoadCurrent();
            AcceptButton = apply;

            LoadCurrent();
        }

        static Label Caption(string text) =>
            new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 10, 0) };

        void LoadCurrent()
        {
            var fk = FilterKeys.Read();
            FilterKeys.GetWindowsRepeat(out int winDelay, out int winRepeat);

            enable.Checked = fk.Enabled && fk.BounceMs > 0;
            bounce.Value = Clamp(fk.BounceMs > 0 ? fk.BounceMs : DefaultBounceMs, bounce);

            bool customRepeat = fk.Enabled && fk.DelayMs > 0 && (fk.DelayMs != winDelay || fk.RepeatMs != winRepeat);
            matchRepeat.Checked = !customRepeat;
            delay.Value = Clamp(customRepeat ? fk.DelayMs : winDelay, delay);
            repeat.Value = Clamp(customRepeat ? fk.RepeatMs : winRepeat, repeat);

            if (!fk.Enabled)
                status.Text = "Debounce is off.";
            else if (fk.BounceMs == 0)
                status.Text = "Filter Keys is on, but bounce filtering is off.";
            else if (fk.DelayMs == 0 || fk.RepeatMs == 0)
                status.Text = $"Debounce is on ({fk.BounceMs} ms), but key repeat is disabled. Click Apply to fix.";
            else
                status.Text = $"Debounce is on: {fk.BounceMs} ms.";

            UpdateEnabledState();
        }

        void FillWindowsRepeat()
        {
            if (!matchRepeat.Checked) return;
            FilterKeys.GetWindowsRepeat(out int winDelay, out int winRepeat);
            delay.Value = Clamp(winDelay, delay);
            repeat.Value = Clamp(winRepeat, repeat);
        }

        void UpdateEnabledState()
        {
            bounce.Enabled = matchRepeat.Enabled = enable.Checked;
            delay.Enabled = repeat.Enabled = enable.Checked && !matchRepeat.Checked;
        }

        void Apply()
        {
            try
            {
                if (enable.Checked)
                {
                    FillWindowsRepeat();
                    FilterKeys.Enable((int)bounce.Value, (int)delay.Value, (int)repeat.Value);
                }
                else
                {
                    FilterKeys.Disable();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "KeyDebounce", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            LoadCurrent();
        }

        static decimal Clamp(int value, NumericUpDown box) =>
            Math.Max(box.Minimum, Math.Min(box.Maximum, value));
    }
}
