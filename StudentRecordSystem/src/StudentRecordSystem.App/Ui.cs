using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using StudentRecordSystem.Core;

namespace StudentRecordSystem.App;

internal sealed record Choice(string Id, string Text)
{
    public override string ToString() => Text;
}

internal static class Ui
{
    public static readonly Color Navy = Color.FromArgb(22, 40, 65);
    public static readonly Color Blue = Color.FromArgb(35, 99, 201);
    public static readonly Color Teal = Color.FromArgb(12, 126, 119);
    public static readonly Color Ink = Color.FromArgb(32, 47, 66);
    public static readonly Color Muted = Color.FromArgb(91, 107, 126);
    public static readonly Color Background = Color.FromArgb(243, 246, 250);

    public static Label Label(string text, float size = 10, bool bold = false) => new()
    {
        Text = text, AutoSize = true, ForeColor = Ink,
        Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
        Margin = new Padding(3, 5, 10, 5)
    };

    public static Button Button(string text, EventHandler? click = null, bool primary = false)
    {
        var button = new Button
        {
            Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(12, 6, 12, 6), FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Blue : Color.White, ForeColor = primary ? Color.White : Ink,
            Cursor = Cursors.Hand, Margin = new Padding(4)
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(211, 221, 233);
        if (click is not null) button.Click += click;
        return button;
    }

    public static ComboBox Combo(int width = 190) => new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList, Width = width,
        Margin = new Padding(3, 7, 12, 3), IntegralHeight = false, DropDownHeight = 300
    };

    public static DataGridView Grid(object rows)
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill, DataSource = rows, ReadOnly = true,
            AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
            BackgroundColor = Color.White, BorderStyle = BorderStyle.None,
            RowHeadersVisible = false, EnableHeadersVisualStyles = false,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            GridColor = Color.FromArgb(231, 236, 243), ColumnHeadersHeight = 38,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            RowTemplate = { Height = 36 }
        };
        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(231, 238, 248), ForeColor = Ink,
            Font = new Font("Segoe UI", 10, FontStyle.Bold), Padding = new Padding(5)
        };
        grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            ForeColor = Ink, SelectionBackColor = Color.FromArgb(217, 232, 255),
            SelectionForeColor = Ink, Padding = new Padding(5), WrapMode = DataGridViewTriState.True
        };
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 253);
        grid.DataBindingComplete += (_, _) =>
        {
            foreach (DataGridViewColumn column in grid.Columns)
            {
                column.HeaderText = column.HeaderText.Replace('_', ' ');
                column.SortMode = DataGridViewColumnSortMode.NotSortable;
            }
            if (grid.Columns["Key"] is { } key) key.Visible = false;
            if (grid.Columns["Notes"] is { } notes) notes.FillWeight = 180;
        };
        return grid;
    }

    public static string? SelectedKey(DataGridView grid) =>
        grid.CurrentRow?.DataBoundItem?.GetType().GetProperty("Key")?.GetValue(grid.CurrentRow.DataBoundItem)?.ToString();

    public static string Percent(double? value) => value.HasValue ? $"{value:0.#}%" : "No data";

    public static void Clear(Control parent)
    {
        foreach (Control control in parent.Controls.Cast<Control>().ToArray()) control.Dispose();
    }
}

// A small, dependency-free percentage chart. Null points break the line rather than implying zero.
internal sealed class TrendChart : Control
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Title { get; init; } = "";
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Subtitle { get; init; } = "";
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color SeriesColor { get; init; } = Ui.Blue;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public List<TrendPoint> Points { get; init; } = [];
    private readonly ToolTip _tooltip = new() { AutoPopDelay = 12000 };
    private readonly List<(RectangleF Area, string Text)> _hitAreas = [];
    private string _lastTip = "";

    public TrendChart()
    {
        DoubleBuffered = true;
        Dock = DockStyle.Fill;
        BackColor = Color.White;
        Margin = new Padding(5);
        MinimumSize = new Size(260, 230);
        AccessibleRole = AccessibleRole.Graphic;
        var menu = new ContextMenuStrip();
        menu.Items.Add("Save chart as PNG…", null, (_, _) => SaveImage());
        ContextMenuStrip = menu;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        _hitAreas.Clear();
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var titleFont = new Font("Segoe UI", 11, FontStyle.Bold);
        using var small = new Font("Segoe UI", 8.5f);
        using var ink = new SolidBrush(Ui.Ink);
        using var muted = new SolidBrush(Ui.Muted);
        using var line = new Pen(SeriesColor, 2.5f);
        using var dot = new SolidBrush(SeriesColor);
        using var grid = new Pen(Color.FromArgb(226, 232, 240));
        g.DrawString(Title, titleFont, ink, new RectangleF(18, 12, Width - 36, 26));
        g.DrawString(Subtitle, small, muted, new RectangleF(18, 40, Width - 36, 34));
        var plot = new RectangleF(52, 85, Math.Max(10, Width - 82), Math.Max(20, Height - 126));
        for (int value = 0; value <= 100; value += 25)
        {
            float y = plot.Bottom - value / 100f * plot.Height;
            g.DrawLine(grid, plot.Left, y, plot.Right, y);
            g.DrawString(value.ToString(), small, muted, 13, y - 7);
        }
        if (Points.Count == 0)
        {
            g.DrawString("No scheduled or recorded data in this period.", small, muted,
                new RectangleF(plot.Left + 5, plot.Top + plot.Height / 2 - 20, plot.Width - 10, 50));
            return;
        }
        PointF? previous = null;
        int every = Math.Max(1, (int)Math.Ceiling(Points.Count / 5.0));
        for (int i = 0; i < Points.Count; i++)
        {
            float x = Points.Count == 1 ? plot.Left + plot.Width / 2 : plot.Left + i * plot.Width / (Points.Count - 1);
            var point = Points[i];
            if (point.Value.HasValue)
            {
                float y = plot.Bottom - (float)Math.Clamp(point.Value.Value, 0, 100) / 100f * plot.Height;
                var current = new PointF(x, y);
                if (previous.HasValue) g.DrawLine(line, previous.Value, current);
                g.FillEllipse(dot, x - 4, y - 4, 8, 8);
                _hitAreas.Add((new RectangleF(x - 12, y - 12, 24, 24), point.Detail + $"\n{point.Value:0.#}%"));
                previous = current;
            }
            else
            {
                previous = null;
                g.DrawEllipse(grid, x - 4, plot.Bottom - 4, 8, 8);
                _hitAreas.Add((new RectangleF(x - 12, plot.Bottom - 12, 24, 24), point.Detail + "\nNo percentage available"));
            }
            if (i % every == 0 || i == Points.Count - 1)
            {
                using var format = new StringFormat { Alignment = StringAlignment.Center };
                g.DrawString(point.Label, small, muted, new RectangleF(x - 28, plot.Bottom + 8, 56, 25), format);
            }
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        string text = _hitAreas.FirstOrDefault(h => h.Area.Contains(e.Location)).Text ?? "";
        if (text == _lastTip) return;
        _lastTip = text;
        _tooltip.SetToolTip(this, text);
    }

    private void SaveImage()
    {
        using var dialog = new SaveFileDialog { Filter = "PNG image|*.png", FileName = "student-progress.png" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            using var bitmap = new Bitmap(Width, Height);
            DrawToBitmap(bitmap, ClientRectangle);
            bitmap.Save(dialog.FileName, ImageFormat.Png);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Could not save chart", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _tooltip.Dispose(); ContextMenuStrip?.Dispose(); }
        base.Dispose(disposing);
    }
}
