using System.ComponentModel;

namespace StudentRecordSystem.App;

internal sealed class EditorDialog : Form
{
    private readonly TableLayoutPanel _fields;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<string?>? ValidateInput { get; set; }

    public EditorDialog(string title)
    {
        Text = title;
        Font = new Font("Segoe UI", 10);
        BackColor = Color.White;
        ForeColor = Ui.Ink;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(570, 230);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 1, RowCount = 2 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _fields = new TableLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, ColumnCount = 2 };
        _fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155));
        _fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
        var save = Ui.Button("Save", (_, _) =>
        {
            var error = ValidateInput?.Invoke();
            if (error is not null) { MessageBox.Show(this, error, "Check your entry", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            DialogResult = DialogResult.OK;
        }, true);
        var cancel = Ui.Button("Cancel", (_, _) => DialogResult = DialogResult.Cancel);
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        AcceptButton = save;
        CancelButton = cancel;
        layout.Controls.Add(_fields, 0, 0);
        layout.Controls.Add(buttons, 0, 1);
        Controls.Add(layout);
    }

    public void Add(string caption, Control input)
    {
        int row = _fields.RowCount++;
        _fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var label = Ui.Label(caption);
        label.MaximumSize = new Size(145, 0);
        input.Dock = DockStyle.Top;
        input.Margin = new Padding(4, 5, 4, 10);
        _fields.Controls.Add(label, 0, row);
        _fields.Controls.Add(input, 1, row);
        ClientSize = new Size(570, Math.Min(650, 120 + _fields.RowCount * 58));
    }

    public static TextBox TextInput(string value = "", bool multiline = false) => new()
    {
        Text = value, Multiline = multiline, Height = multiline ? 72 : 30,
        MaxLength = multiline ? 1000 : 120, ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None
    };
    public static DateTimePicker DateInput(DateOnly value) => new()
    {
        Format = DateTimePickerFormat.Custom, CustomFormat = "dd MMM yyyy", Value = value.ToDateTime(TimeOnly.MinValue)
    };
    public static NumericUpDown Number(decimal value, decimal maximum = 10000) => new()
    {
        Minimum = 0, Maximum = maximum, DecimalPlaces = 2, Value = Math.Clamp(value, 0, maximum)
    };
}
