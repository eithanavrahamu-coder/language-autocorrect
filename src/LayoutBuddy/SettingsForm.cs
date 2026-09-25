using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using LayoutBuddy.Engine;

namespace LayoutBuddy;

internal sealed class SettingsForm : Form
{
    private readonly AppSettings _settings;
    private readonly NeverFixList _neverFix;

    private readonly CheckBox _indicator = new() { Text = "Show language indicator next to the cursor", AutoSize = true };
    private readonly CheckBox _voice = new() { Text = "Say the language out loud when it changes", AutoSize = true };
    private readonly CheckBox _autoCorrect = new() { Text = "Auto-correct words typed in the wrong layout", AutoSize = true };
    private readonly CheckBox _startup = new() { Text = "Start with Windows", AutoSize = true };
    private readonly ComboBox _sensitivity = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    private readonly NumericUpDown _undos = new() { Minimum = 1, Maximum = 10, Width = 60 };
    private readonly TextBox _excluded = new() { Multiline = true, ScrollBars = ScrollBars.Vertical, Height = 80, Dock = DockStyle.Fill };
    private readonly ListBox _neverList = new() { Height = 120, Dock = DockStyle.Fill, SelectionMode = SelectionMode.MultiExtended };
    private readonly TextBox _addWord = new() { Width = 160 };

    public SettingsForm(AppSettings settings, NeverFixList neverFix)
    {
        _settings = settings;
        _neverFix = neverFix;

        Text = "LayoutBuddy Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Font = new Font("Segoe UI", 9f);
        Padding = new Padding(12);

        _sensitivity.Items.AddRange(["Low (only obvious mistakes)", "Medium (recommended)", "High (fix more, may be wrong more)"]);

        var root = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, Dock = DockStyle.Fill };
        root.Controls.Add(_indicator);
        root.Controls.Add(_voice);
        root.Controls.Add(_autoCorrect);
        root.Controls.Add(Row(new Label { Text = "Auto-correct sensitivity:", AutoSize = true, Anchor = AnchorStyles.Left }, _sensitivity));
        root.Controls.Add(_startup);

        root.Controls.Add(Header("Words never to fix"));
        root.Controls.Add(Hint("To undo an auto-correction, press Backspace right after it (or Ctrl+Z).\n" +
                               "A word is added to this list only after you undo it this many times:"));
        root.Controls.Add(Row(_undos, new Label { Text = "undos", AutoSize = true, Anchor = AnchorStyles.Left }));
        root.Controls.Add(_neverList);
        var remove = new Button { Text = "Remove selected", AutoSize = true };
        var clear = new Button { Text = "Clear all", AutoSize = true };
        var add = new Button { Text = "Add", AutoSize = true };
        remove.Click += (_, _) =>
        {
            foreach (var w in _neverList.SelectedItems.Cast<string>().ToList()) _neverFix.Remove(w);
            RefreshList();
        };
        clear.Click += (_, _) =>
        {
            if (MessageBox.Show(this, "Remove all words from the list?", Text, MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                _neverFix.Clear();
                RefreshList();
            }
        };
        add.Click += (_, _) =>
        {
            if (_addWord.Text.Trim().Length > 0) _neverFix.Block(_addWord.Text);
            _addWord.Clear();
            RefreshList();
        };
        root.Controls.Add(Row(remove, clear, new Label { Text = "   Add word:", AutoSize = true, Anchor = AnchorStyles.Left }, _addWord, add));

        root.Controls.Add(Header("Apps where auto-correct is off"));
        root.Controls.Add(Hint("One program name per line (e.g. notepad). Auto-correct is also always off in password boxes."));
        root.Controls.Add(_excluded);

        var ok = new Button { Text = "Save", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        var buttons = Row(ok, cancel);
        buttons.Anchor = AnchorStyles.Right;
        root.Controls.Add(buttons);
        AcceptButton = ok;
        CancelButton = cancel;
        Controls.Add(root);

        _indicator.Checked = settings.ShowIndicator;
        _voice.Checked = settings.VoiceEnabled;
        _autoCorrect.Checked = settings.AutoCorrectEnabled;
        _startup.Checked = settings.StartWithWindows;
        _sensitivity.SelectedIndex = (int)settings.Sensitivity;
        _undos.Value = Math.Clamp(settings.UndosToBlock, 1, 10);
        _excluded.Text = string.Join(Environment.NewLine, settings.ExcludedApps);
        _undos.ValueChanged += (_, _) =>
        {
            _neverFix.UndosToBlock = (int)_undos.Value;
            RefreshList();
        };
        RefreshList();
    }

    /// <summary>Copies the form into the settings object (call after OK).</summary>
    public void ApplyTo(AppSettings s)
    {
        s.ShowIndicator = _indicator.Checked;
        s.VoiceEnabled = _voice.Checked;
        s.AutoCorrectEnabled = _autoCorrect.Checked;
        s.StartWithWindows = _startup.Checked;
        s.Sensitivity = (Sensitivity)Math.Max(0, _sensitivity.SelectedIndex);
        s.UndosToBlock = (int)_undos.Value;
        s.ExcludedApps = _excluded.Lines.Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        s.NeverFixUndoCounts = _neverFix.Snapshot();
    }

    private void RefreshList()
    {
        _neverList.BeginUpdate();
        _neverList.Items.Clear();
        foreach (var w in _neverFix.BlockedWords()) _neverList.Items.Add(w);
        _neverList.EndUpdate();
    }

    private static FlowLayoutPanel Row(params Control[] controls)
    {
        var p = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 3, 0, 3) };
        p.Controls.AddRange(controls);
        return p;
    }

    private static Label Header(string text) => new()
    {
        Text = text, AutoSize = true, Font = new Font("Segoe UI", 10f, FontStyle.Bold), Margin = new Padding(0, 14, 0, 2),
    };

    private static Label Hint(string text) => new()
    {
        Text = text, AutoSize = true, ForeColor = SystemColors.GrayText, MaximumSize = new Size(460, 0),
    };
}
