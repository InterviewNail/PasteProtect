using System.ComponentModel;

namespace PasteProtect;

/// <summary>Edits the replacement rules and options, with a live preview.</summary>
internal sealed class SettingsForm : Form
{
    private sealed record CaseChoice(CaseMode Value, string Name);

    private static readonly CaseChoice[] CaseChoices =
    [
        new(CaseMode.Preserve, "Preserve case"),
        new(CaseMode.Ignore, "Ignore case"),
        new(CaseMode.Match, "Match case"),
    ];

    private readonly AppSettings initial;
    private readonly bool initialStartup = StartupManager.IsEnabled;
    private readonly Func<AppSettings, string?> apply;
    private readonly Action<bool> suspendHotkey;
    private readonly BindingList<ReplacementRule> rules;

    private readonly Label intro = new();
    private readonly DataGridView grid = new();
    private readonly DataGridViewCheckBoxColumn enabledColumn = new();
    private readonly DataGridViewCheckBoxColumn wholeWordColumn = new();
    private readonly DataGridViewComboBoxColumn caseColumn = new();
    private readonly Button removeAllButton = new();
    private readonly Button moveUpButton = new();
    private readonly Button moveDownButton = new();
    private readonly TextBox sampleBox = new();
    private readonly TextBox resultBox = new();
    private readonly HotkeyTextBox hotkeyBox = new();
    private readonly CheckBox restoreCheck = new();
    private readonly NumericUpDown delayInput = new();
    private readonly CheckBox startupCheck = new();
    private readonly ToolTip toolTip = new();
    private bool closeWithoutPrompt;

    // Dragging a rule by its number in the row header
    private int pressedRow = -1;        // row whose number the mouse went down on
    private Rectangle dragStartBounds;  // the mouse has to leave this before a drag starts
    private int draggedRow = -1;        // row being dragged
    private int dropRow = -1;           // where it would land

    /// <param name="apply">Applies and saves new settings; returns an error message or null.</param>
    /// <param name="suspendHotkey">Pauses the global shortcut while a new one is being recorded.</param>
    public SettingsForm(AppSettings current, Func<AppSettings, string?> apply, Action<bool> suspendHotkey)
    {
        initial = current;
        this.apply = apply;
        this.suspendHotkey = suspendHotkey;
        rules = new BindingList<ReplacementRule>(current.Rules.Select(r => r.Clone()).ToList());

        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = $"{Program.AppName} – Rules & Settings";
        Icon = AppIcons.Load(SystemInformation.IconSize);
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(780, 640);
        MinimumSize = new Size(660, 560);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(10) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        intro.AutoSize = true;
        intro.Margin = new Padding(3, 0, 3, 8);
        root.Controls.Add(intro, 0, 0);
        root.Controls.Add(BuildRulesGroup(), 0, 1);
        root.Controls.Add(BuildPreviewGroup(), 0, 2);
        root.Controls.Add(BuildOptionsGroup(), 0, 3);
        root.Controls.Add(BuildButtons(), 0, 4);
        Controls.Add(root);
        UpdateIntro();

        ResumeLayout(false);
        PerformLayout();
    }

    private Control BuildRulesGroup()
    {
        grid.Dock = DockStyle.Fill;
        grid.AutoGenerateColumns = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.BackgroundColor = SystemColors.Window;
        grid.BorderStyle = BorderStyle.FixedSingle;
        grid.EditMode = DataGridViewEditMode.EditOnEnter;
        grid.AllowUserToResizeRows = false;
        grid.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;

        enabledColumn.DataPropertyName = nameof(ReplacementRule.Enabled);
        enabledColumn.HeaderText = "On";
        enabledColumn.ToolTipText = "Untick to switch this rule off without deleting it.";
        enabledColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;

        var findColumn = new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(ReplacementRule.Find),
            HeaderText = "Find",
            ToolTipText = "Text to look for.",
            FillWeight = 50,
        };
        var replaceColumn = new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(ReplacementRule.Replace),
            HeaderText = "Replace with",
            ToolTipText = "Leave empty to remove the found text.",
            FillWeight = 50,
        };
        // Without this, clearing a cell would try to store DBNull in a string property.
        findColumn.DefaultCellStyle.DataSourceNullValue = "";
        replaceColumn.DefaultCellStyle.DataSourceNullValue = "";

        wholeWordColumn.DataPropertyName = nameof(ReplacementRule.WholeWord);
        wholeWordColumn.HeaderText = "Whole word";
        wholeWordColumn.ToolTipText = "Only match when it isn't part of a longer word (\"acme\" won't match inside \"acmeCorp\").";
        wholeWordColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;

        caseColumn.DataPropertyName = nameof(ReplacementRule.CaseMode);
        caseColumn.HeaderText = "Case";
        caseColumn.DataSource = CaseChoices;
        caseColumn.ValueMember = nameof(CaseChoice.Value);
        caseColumn.DisplayMember = nameof(CaseChoice.Name);
        caseColumn.FlatStyle = FlatStyle.Flat;
        caseColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
        caseColumn.ToolTipText =
            "Preserve case: match any capitalization and copy it onto the replacement (acme → contoso, Acme → Contoso, ACME → CONTOSO).\n" +
            "Ignore case: match any capitalization and insert the replacement exactly as typed.\n" +
            "Match case: only match the exact capitalization typed in Find.";

        grid.Columns.AddRange(enabledColumn, findColumn, replaceColumn, wholeWordColumn, caseColumn);
        foreach (DataGridViewColumn column in grid.Columns)
            column.SortMode = DataGridViewColumnSortMode.NotSortable;
        grid.DataSource = rules;

        grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            // Apply checkbox and dropdown changes immediately rather than when the cell is left.
            if (grid.IsCurrentCellDirty && grid.CurrentCell is DataGridViewCheckBoxCell or DataGridViewComboBoxCell)
                grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        grid.EditingControlShowing += (_, e) =>
        {
            if (e.Control is TextBox editor)
            {
                editor.TextChanged -= OnCellEditorTextChanged;
                editor.TextChanged += OnCellEditorTextChanged;
            }
        };
        grid.CellValueChanged += (_, _) => UpdatePreview();
        grid.RowsRemoved += (_, _) => UpdatePreview();
        grid.DataError += (_, e) => e.ThrowException = false;

        // Priority: rule numbers in the row header, which double as drag handles.
        grid.TopLeftHeaderCell.Value = "#";
        grid.TopLeftHeaderCell.ToolTipText = "Priority: where rules overlap, the higher one wins. Drag a number to move its rule.";
        grid.AllowDrop = true;
        grid.RowPostPaint += OnGridRowPostPaint;
        grid.MouseDown += OnGridMouseDown;
        grid.MouseMove += OnGridMouseMove;
        grid.MouseUp += (_, _) => pressedRow = -1;
        grid.DragOver += OnGridDragOver;
        grid.DragLeave += (_, _) => SetDropRow(-1);
        grid.DragDrop += OnGridDragDrop;

        moveUpButton.Text = "▲";
        moveUpButton.Size = new Size(34, 30);
        moveUpButton.Click += (_, _) => MoveCurrentRule(-1);
        toolTip.SetToolTip(moveUpButton, "Move the rule up, giving it higher priority (Alt+Up)");
        moveDownButton.Text = "▼";
        moveDownButton.Size = new Size(34, 30);
        moveDownButton.Click += (_, _) => MoveCurrentRule(1);
        toolTip.SetToolTip(moveDownButton, "Move the rule down, giving it lower priority (Alt+Down)");
        var moveButtons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Margin = new Padding(6, 0, 0, 0),
        };
        moveButtons.Controls.Add(moveUpButton);
        moveButtons.Controls.Add(moveDownButton);

        var removeButton = new Button { Text = "Remove selected", AutoSize = true, Anchor = AnchorStyles.Left };
        removeButton.Click += (_, _) => RemoveSelectedRules();
        removeAllButton.Text = "Remove all";
        removeAllButton.AutoSize = true;
        removeAllButton.Anchor = AnchorStyles.Left;
        removeAllButton.Click += (_, _) => RemoveAllRules();
        grid.RowsAdded += (_, _) => UpdateRuleButtons();
        grid.RowsRemoved += (_, _) => UpdateRuleButtons();
        grid.DataBindingComplete += (_, _) => UpdateRuleButtons();
        grid.CurrentCellChanged += (_, _) => UpdateRuleButtons();

        var hint = new Label
        {
            Text = "Add a rule in the last row. Drag the numbers to reorder.",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            ForeColor = SystemColors.GrayText,
        };
        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 3, Margin = new Padding(0, 6, 0, 0) };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.Controls.Add(removeButton, 0, 0);
        footer.Controls.Add(removeAllButton, 1, 0);
        footer.Controls.Add(hint, 2, 0);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(grid, 0, 0);
        layout.Controls.Add(moveButtons, 1, 0);
        layout.Controls.Add(footer, 0, 1);
        layout.SetColumnSpan(footer, 2);

        var group = new GroupBox { Text = "Replacement rules (in priority order)", Dock = DockStyle.Fill, Padding = new Padding(8) };
        group.Controls.Add(layout);
        return group;
    }

    private Control BuildPreviewGroup()
    {
        sampleBox.Multiline = true;
        sampleBox.AcceptsReturn = true;
        sampleBox.ScrollBars = ScrollBars.Vertical;
        sampleBox.Dock = DockStyle.Fill;
        sampleBox.PlaceholderText = "Type or paste text to try your rules";
        sampleBox.TextChanged += (_, _) => UpdatePreview();

        resultBox.Multiline = true;
        resultBox.ReadOnly = true;
        resultBox.ScrollBars = ScrollBars.Vertical;
        resultBox.Dock = DockStyle.Fill;

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label { Text = "Sample text", AutoSize = true }, 0, 0);
        layout.Controls.Add(new Label { Text = "Result", AutoSize = true }, 1, 0);
        layout.Controls.Add(sampleBox, 0, 1);
        layout.Controls.Add(resultBox, 1, 1);

        var group = new GroupBox { Text = "Preview", Dock = DockStyle.Fill, Padding = new Padding(8) };
        group.Controls.Add(layout);
        return group;
    }

    private Control BuildOptionsGroup()
    {
        hotkeyBox.Hotkey = initial.Hotkey;
        hotkeyBox.Width = 170;
        hotkeyBox.Anchor = AnchorStyles.Left;
        hotkeyBox.HotkeyChanged += (_, _) => UpdateIntro();
        hotkeyBox.Enter += (_, _) => suspendHotkey(true);
        hotkeyBox.Leave += (_, _) => suspendHotkey(false);
        // Leave doesn't fire when switching to another app, so the shortcut must work again then.
        Deactivate += (_, _) => suspendHotkey(false);
        Activated += (_, _) => suspendHotkey(hotkeyBox.Focused);

        restoreCheck.Text = "Put the original clipboard back after pasting";
        restoreCheck.AutoSize = true;
        restoreCheck.Anchor = AnchorStyles.Left;
        restoreCheck.Checked = initial.RestoreClipboard;
        restoreCheck.CheckedChanged += (_, _) => delayInput.Enabled = restoreCheck.Checked;

        delayInput.Minimum = AppSettings.MinRestoreDelayMs;
        delayInput.Maximum = AppSettings.MaxRestoreDelayMs;
        delayInput.Increment = 50;
        delayInput.Value = Math.Clamp(initial.RestoreDelayMs, AppSettings.MinRestoreDelayMs, AppSettings.MaxRestoreDelayMs);
        delayInput.Width = 80;
        delayInput.Anchor = AnchorStyles.Left;
        delayInput.Enabled = restoreCheck.Checked;
        var delayRow = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Margin = Padding.Empty, Anchor = AnchorStyles.Left };
        delayRow.Controls.Add(delayInput, 0, 0);
        delayRow.Controls.Add(new Label { Text = "ms", AutoSize = true, Anchor = AnchorStyles.Left }, 1, 0);

        startupCheck.Text = $"Start {Program.AppName} when I sign in to Windows";
        startupCheck.AutoSize = true;
        startupCheck.Anchor = AnchorStyles.Left;
        startupCheck.Checked = initialStartup;

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 3 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddOptionRow(layout, 0, "Shortcut:", hotkeyBox, "Click the box, then press the keys you want to use");
        AddOptionRow(layout, 1, "Clipboard:", restoreCheck, null);
        AddOptionRow(layout, 2, "Restore after:", delayRow, "Raise this if an app sometimes pastes the original text");
        AddOptionRow(layout, 3, "Startup:", startupCheck, null);

        var group = new GroupBox
        {
            Text = "Options",
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(8),
        };
        group.Controls.Add(layout);
        return group;
    }

    private static void AddOptionRow(TableLayoutPanel layout, int row, string label, Control input, string? hint)
    {
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        layout.Controls.Add(input, 1, row);
        if (hint is null)
        {
            layout.SetColumnSpan(input, 2);
            return;
        }
        layout.Controls.Add(new Label
        {
            Text = hint,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            ForeColor = SystemColors.GrayText,
        }, 2, row);
    }

    private Control BuildButtons()
    {
        var save = new Button { Text = "Save", AutoSize = true, MinimumSize = new Size(88, 0) };
        var cancel = new Button { Text = "Cancel", AutoSize = true, MinimumSize = new Size(88, 0) };
        save.Click += (_, _) =>
        {
            if (!TrySave())
                return;
            closeWithoutPrompt = true;
            Close();
        };
        cancel.Click += (_, _) =>
        {
            closeWithoutPrompt = true;
            Close();
        };

        var restoreDefaults = new Button { Text = "Restore defaults", AutoSize = true, Anchor = AnchorStyles.Left };
        toolTip.SetToolTip(restoreDefaults, "Reset the shortcut and clipboard options. Your rules aren't changed.");
        restoreDefaults.Click += (_, _) => RestoreDefaultOptions();

        var saveCancel = new FlowLayoutPanel
        {
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            FlowDirection = FlowDirection.RightToLeft,
            Margin = Padding.Empty,
        };
        saveCancel.Controls.Add(cancel);
        saveCancel.Controls.Add(save);

        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            Margin = new Padding(0, 8, 0, 0),
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.Controls.Add(restoreDefaults, 0, 0);
        panel.Controls.Add(saveCancel, 1, 0);
        return panel;
    }

    /// <summary>Resets the options to their defaults; like any other edit here, it takes effect on Save.</summary>
    private void RestoreDefaultOptions()
    {
        var defaults = new AppSettings();
        hotkeyBox.Hotkey = defaults.Hotkey;
        restoreCheck.Checked = defaults.RestoreClipboard;
        delayInput.Value = defaults.RestoreDelayMs;
        UpdateIntro();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        // Fixed-width columns, sized for the monitor's DPI once the window exists.
        grid.RowHeadersWidth = grid.LogicalToDeviceUnits(44);
        enabledColumn.Width = grid.LogicalToDeviceUnits(40);
        wholeWordColumn.Width = grid.LogicalToDeviceUnits(84);
        caseColumn.Width = grid.LogicalToDeviceUnits(124);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (grid.ContainsFocus && keyData is (Keys.Alt | Keys.Up) or (Keys.Alt | Keys.Down))
        {
            MoveCurrentRule((keyData & Keys.KeyCode) == Keys.Up ? -1 : 1);
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!closeWithoutPrompt && e.CloseReason == CloseReason.UserClosing && HasChanges())
        {
            var answer = MessageBox.Show(this, "Save your changes?", Program.AppName,
                MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (answer == DialogResult.Cancel || (answer == DialogResult.Yes && !TrySave()))
                e.Cancel = true;
        }
        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        suspendHotkey(false);
        base.OnFormClosed(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            toolTip.Dispose();
        base.Dispose(disposing);
    }

    private bool TrySave()
    {
        grid.EndEdit();
        var updated = new AppSettings
        {
            Hotkey = hotkeyBox.Hotkey,
            RestoreClipboard = restoreCheck.Checked,
            RestoreDelayMs = (int)delayInput.Value,
            Rules = CurrentRules(),
        };
        if (apply(updated) is { } error)
        {
            MessageBox.Show(this, error, Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        if (startupCheck.Checked != initialStartup)
        {
            try
            {
                StartupManager.SetEnabled(startupCheck.Checked);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                MessageBox.Show(this, $"Your rules were saved, but the startup setting couldn't be changed: {ex.Message}",
                    Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        return true;
    }

    private bool HasChanges() =>
        hotkeyBox.Hotkey != initial.Hotkey
        || restoreCheck.Checked != initial.RestoreClipboard
        || (int)delayInput.Value != initial.RestoreDelayMs
        || startupCheck.Checked != initialStartup
        || !CurrentRules().Select(RuleKey).SequenceEqual(initial.Rules.Select(RuleKey));

    private static (bool, string, string, bool, CaseMode) RuleKey(ReplacementRule r) =>
        (r.Enabled, r.Find, r.Replace, r.WholeWord, r.CaseMode);

    /// <summary>The rules as currently shown, including a cell that's still being typed in. Blank rows are dropped.</summary>
    private List<ReplacementRule> CurrentRules()
    {
        var list = rules.Select(r => r.Clone()).ToList();
        if (grid.IsCurrentCellInEditMode
            && grid.EditingControl is TextBox editor
            && grid.CurrentCell is { RowIndex: var row, ColumnIndex: var column }
            && row < list.Count)
        {
            switch (grid.Columns[column].DataPropertyName)
            {
                case nameof(ReplacementRule.Find): list[row].Find = editor.Text; break;
                case nameof(ReplacementRule.Replace): list[row].Replace = editor.Text; break;
            }
        }
        return list.Where(r => r.Find.Length > 0).ToList();
    }

    private void OnCellEditorTextChanged(object? sender, EventArgs e) => UpdatePreview();

    private void UpdatePreview() => resultBox.Text = new TextReplacer(CurrentRules()).Replace(sampleBox.Text);

    private void UpdateIntro() =>
        intro.Text = $"Press {hotkeyBox.Hotkey} in any app to paste the clipboard as plain text with these replacements applied.\n" +
            "Where rules overlap, the one higher in the list wins.";

    private void RemoveSelectedRules()
    {
        grid.EndEdit();
        var selected = grid.SelectedCells.Cast<DataGridViewCell>().Select(c => c.OwningRow!)
            .Concat(grid.SelectedRows.Cast<DataGridViewRow>())
            .Where(r => !r.IsNewRow)
            .Distinct()
            .OrderByDescending(r => r.Index)
            .ToList();
        foreach (var row in selected)
            grid.Rows.Remove(row);
    }

    private void RemoveAllRules()
    {
        grid.EndEdit();
        int count = RuleRowCount();
        if (count == 0)
            return;

        string question = count == 1 ? "Remove the rule?" : $"Remove all {count} rules?";
        var answer = MessageBox.Show(this, $"{question}\n\nNothing is deleted until you click Save.", Program.AppName,
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes)
            return;

        rules.Clear();
        UpdatePreview();
        UpdateRuleButtons();
    }

    private void MoveCurrentRule(int offset)
    {
        if (grid.CurrentCell is not { RowIndex: var row } || grid.Rows[row].IsNewRow)
            return;
        int target = row + offset;
        if (target >= 0 && target < RuleRowCount())
            MoveRule(row, target);
    }

    private void MoveRule(int from, int to)
    {
        // Removing the row that holds the cell editor, or disabling a focused arrow button, pushes focus
        // on to the next control, which may be "Remove selected". Note where focus was to put it back.
        bool gridHadFocus = grid.ContainsFocus;
        var focusedMoveButton = moveUpButton.Focused ? moveUpButton : moveDownButton.Focused ? moveDownButton : null;

        int column = grid.CurrentCell?.ColumnIndex ?? 0;
        grid.EndEdit();
        grid.BindingContext?[rules].EndCurrentEdit();   // commit a rule that's still being added

        var rule = rules[from];
        rules.RemoveAt(from);
        rules.Insert(to, rule);
        grid.CurrentCell = grid.Rows[to].Cells[column];
        UpdatePreview();
        UpdateRuleButtons();

        if (focusedMoveButton is { Enabled: true })
            focusedMoveButton.Focus();
        else if (gridHadFocus || focusedMoveButton is not null)
            grid.Focus();
    }

    private void UpdateRuleButtons()
    {
        int count = RuleRowCount();
        int row = grid.CurrentCell is { RowIndex: var r } && r < count ? r : -1;
        removeAllButton.Enabled = count > 0;
        moveUpButton.Enabled = row > 0;
        moveDownButton.Enabled = row >= 0 && row < count - 1;
    }

    private void OnGridRowPostPaint(object? sender, DataGridViewRowPostPaintEventArgs e)
    {
        if (!grid.Rows[e.RowIndex].IsNewRow)
        {
            var header = new Rectangle(e.RowBounds.Left, e.RowBounds.Top, grid.RowHeadersWidth - grid.LogicalToDeviceUnits(6), e.RowBounds.Height);
            TextRenderer.DrawText(e.Graphics, (e.RowIndex + 1).ToString(), grid.Font, header, SystemColors.GrayText,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
        }

        // Show where a dragged rule will land: above this row when moving up, below it when moving down.
        if (e.RowIndex == dropRow && draggedRow >= 0 && dropRow != draggedRow)
        {
            int thickness = grid.LogicalToDeviceUnits(2);
            int y = dropRow > draggedRow ? e.RowBounds.Bottom - thickness : e.RowBounds.Top;
            e.Graphics.FillRectangle(SystemBrushes.Highlight, e.RowBounds.Left, y, e.RowBounds.Width, thickness);
        }
    }

    private void OnGridMouseDown(object? sender, MouseEventArgs e)
    {
        pressedRow = -1;
        var hit = grid.HitTest(e.X, e.Y);
        if (e.Button != MouseButtons.Left || hit.Type != DataGridViewHitTestType.RowHeader
            || hit.RowIndex < 0 || grid.Rows[hit.RowIndex].IsNewRow)
            return;

        pressedRow = hit.RowIndex;
        var size = SystemInformation.DragSize;
        dragStartBounds = new Rectangle(e.X - size.Width / 2, e.Y - size.Height / 2, size.Width, size.Height);
    }

    private void OnGridMouseMove(object? sender, MouseEventArgs e)
    {
        if (pressedRow < 0 || (e.Button & MouseButtons.Left) == 0 || dragStartBounds.Contains(e.Location))
            return;

        draggedRow = pressedRow;
        pressedRow = -1;
        try
        {
            // The row index lives in draggedRow; the payload only identifies the drag as ours.
            grid.DoDragDrop(nameof(ReplacementRule), DragDropEffects.Move);
        }
        finally
        {
            draggedRow = -1;
            SetDropRow(-1);
        }
    }

    private void OnGridDragOver(object? sender, DragEventArgs e)
    {
        if (draggedRow < 0)
        {
            e.Effect = DragDropEffects.None;
            return;
        }
        e.Effect = DragDropEffects.Move;
        var point = grid.PointToClient(new Point(e.X, e.Y));
        ScrollWhileDragging(point);
        SetDropRow(DropTargetRow(point));
    }

    private void OnGridDragDrop(object? sender, DragEventArgs e)
    {
        int from = draggedRow, to = dropRow;
        SetDropRow(-1);
        if (from >= 0 && to >= 0 && from != to)
            MoveRule(from, to);
    }

    /// <summary>The rule row under the mouse; past the last rule means the end of the list.</summary>
    private int DropTargetRow(Point point)
    {
        int count = RuleRowCount();
        int row = grid.HitTest(point.X, point.Y).RowIndex;
        if (row >= 0 && row < count)
            return row;
        return point.Y <= grid.ColumnHeadersHeight ? Math.Max(grid.FirstDisplayedScrollingRowIndex, 0) : count - 1;
    }

    private void ScrollWhileDragging(Point point)
    {
        int first = grid.FirstDisplayedScrollingRowIndex;
        if (first < 0)
            return;
        int edge = grid.Rows[first].Height / 2;
        if (point.Y < grid.ColumnHeadersHeight + edge && first > 0)
            grid.FirstDisplayedScrollingRowIndex = first - 1;
        else if (point.Y > grid.ClientSize.Height - edge && first + grid.DisplayedRowCount(false) < grid.RowCount)
            grid.FirstDisplayedScrollingRowIndex = first + 1;
    }

    private void SetDropRow(int row)
    {
        if (row == dropRow)
            return;
        int previous = dropRow;
        dropRow = row;
        if (previous >= 0 && previous < grid.RowCount)
            grid.InvalidateRow(previous);
        if (row >= 0 && row < grid.RowCount)
            grid.InvalidateRow(row);
    }

    /// <summary>Rows holding a rule, not counting the blank row for adding a new one.</summary>
    private int RuleRowCount() => grid.Rows.Cast<DataGridViewRow>().Count(r => !r.IsNewRow);
}
