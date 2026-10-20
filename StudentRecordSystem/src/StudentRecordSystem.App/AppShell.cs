using System.Diagnostics;
using StudentRecordSystem.Core;

namespace StudentRecordSystem.App;

internal sealed partial class AppShell : Form
{
    private readonly SchoolService _service;
    private readonly Panel _root = new() { Dock = DockStyle.Fill };
    private UserSession? _session;
    private SchoolData _data = null!;
    private ComboBox _studentBox = null!, _termBox = null!;
    private DateTimePicker _asOfBox = null!;
    private TabControl _tabs = null!;
    private Label _status = null!;
    private readonly DateOnly _initialAsOf;
    private bool _rendering;
    private bool _busy;
    private string? _subject, _activityId;
    private bool Teacher => _session?.Role == UserRole.Teacher;
    private string StudentId => ((Choice)_studentBox.SelectedItem!).Id;
    private SchoolTerm Term => _data.Terms.Single(t => t.Id == ((Choice)_termBox.SelectedItem!).Id);
    private DateOnly AsOf => DateOnly.FromDateTime(_asOfBox.Value);

    public AppShell(SchoolService service, DateOnly initialAsOf)
    {
        _service = service;
        _initialAsOf = initialAsOf;
        Text = "Student Records · Progress and participation · v1.0.1";
        Font = new Font("Segoe UI", 10);
        BackColor = Ui.Background;
        ForeColor = Ui.Ink;
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1380, 920);
        MinimumSize = new Size(1050, 760);
        Controls.Add(_root);
        _service.RecordsChanged += OnRecordsChanged;
        FormClosing += (_, e) => { if (_busy) { e.Cancel = true; MessageBox.Show(this, "Please wait until the save finishes."); } };
        ShowLogin();
    }

    private void OnRecordsChanged(object? sender, EventArgs e)
    {
        if (_session is not null) _data = _service.Snapshot(_session);
    }

    private void ShowLogin()
    {
        if (_session is not null) _service.Logout(_session);
        _session = null;
        Ui.Clear(_root);
        var columns = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(32) };
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        var intro = new Panel { Dock = DockStyle.Fill, BackColor = Ui.Navy, Padding = new Padding(36) };
        var words = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        var title = Ui.Label("STUDENT\nRECORDS", 30, true); title.ForeColor = Color.White;
        var subtitle = Ui.Label("A clearer picture of school life.", 15); subtitle.ForeColor = Color.FromArgb(177, 216, 246);
        var description = Ui.Label("Explore academic progress, attendance, sports and club participation.\n\n" +
            "Each trend shows its own measurement. Achievements retain their context.\n\n" +
            "CSP3341 · C# application demonstration\nFictional students and school calendar.", 11);
        description.ForeColor = Color.White;
        description.MaximumSize = new Size(360, 0);
        words.Controls.AddRange([title, subtitle, description]);
        intro.Controls.Add(words);
        var login = new TableLayoutPanel { Anchor = AnchorStyles.None, Width = 430, AutoSize = true, ColumnCount = 1, Padding = new Padding(25) };
        login.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var username = EditorDialog.TextInput("teacher");
        var password = EditorDialog.TextInput(); password.UseSystemPasswordChar = true;
        var message = Ui.Label("", 10); message.ForeColor = Color.Firebrick; message.MaximumSize = new Size(370, 0);
        var signIn = Ui.Button("Sign in", (_, _) =>
        {
            try
            {
                var session = _service.Login(username.Text, password.Text);
                if (session is null) { message.Text = "Check your username and password."; return; }
                _session = session;
                ShowWorkspace();
            }
            catch (Exception ex) { message.Text = "Sign-in failed: " + ex.Message; }
        }, true);
        foreach (Control control in new Control[]
        {
            Ui.Label("Welcome back", 23, true), Ui.Label("Choose a demonstration account below."),
            Ui.Label("Username", 10, true), username, Ui.Label("Password", 10, true), password, signIn, message,
            Ui.Label("DEMO ACCOUNTS", 9, true),
            Ui.Label("Teacher: teacher\nStudents: student1, student2, student3\nPassword for all: Demo123!", 10),
            Ui.Label("Demo snapshot: 16 October 2026.\nRecords are saved locally for this Windows user.", 9)
        }) { control.Margin = new Padding(3, 6, 3, 6); login.Controls.Add(control); }
        username.Width = password.Width = 370;
        columns.Controls.Add(intro, 0, 0);
        columns.Controls.Add(login, 1, 0);
        _root.Controls.Add(columns);
        AcceptButton = signIn;
    }

    private void ShowWorkspace()
    {
        _rendering = true;
        AcceptButton = null;
        _data = _service.Snapshot(_session!);
        Ui.Clear(_root);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Ui.Navy, Padding = new Padding(16, 10, 16, 10) };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var brand = Ui.Label("STUDENT RECORDS  /  Progress dashboard", 17, true);
        brand.ForeColor = Color.White; brand.AutoSize = false; brand.Dock = DockStyle.Fill;
        brand.TextAlign = ContentAlignment.MiddleLeft; brand.AutoEllipsis = true;
        var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
        var identity = Ui.Label($"{_session!.DisplayName} · {_session.Role}"); identity.ForeColor = Color.White;
        actions.Controls.Add(identity);
        actions.Controls.Add(Ui.Button("Rules & help", (_, _) => ShowHelp()));
        if (Teacher) actions.Controls.Add(Ui.Button("Data & backup", (_, _) => ShowStorage()));
        actions.Controls.Add(Ui.Button("Sign out", (_, _) => ShowLogin()));
        header.Controls.Add(brand, 0, 0); header.Controls.Add(actions, 1, 0);
        var filters = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18, 5, 10, 3), WrapContents = false, AutoScroll = true };
        _studentBox = Ui.Combo(240);
        _studentBox.Items.AddRange(_data.Students.Select(s => new Choice(s.Id, $"{s.Name} · {s.ClassName}")).ToArray());
        _studentBox.SelectedIndex = 0;
        _studentBox.Enabled = Teacher;
        _termBox = Ui.Combo(180);
        _termBox.Items.AddRange(_data.Terms.OrderBy(t => t.Start).Select(t => new Choice(t.Id, t.Name)).ToArray());
        _termBox.SelectedIndex = _termBox.Items.Count - 1;
        _asOfBox = EditorDialog.DateInput(_initialAsOf);
        _asOfBox.Width = 150; _asOfBox.Margin = new Padding(3, 7, 10, 3);
        filters.Controls.AddRange([Ui.Label("Student", 10, true), _studentBox, Ui.Label("Term", 10, true), _termBox,
            Ui.Label("As of", 10, true), _asOfBox]);
        _tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(18, 9), Margin = new Padding(16, 0, 16, 0) };
        foreach (string name in new[] { "Overview", "Examination marks", "Attendance", "Sports & clubs", "Term comparison", "C# trade-offs" })
            _tabs.TabPages.Add(new TabPage(name) { BackColor = Ui.Background, Padding = new Padding(10) });
        _status = Ui.Label("Fictional demonstration data · saved locally · percentages use recorded data", 9);
        _status.Dock = DockStyle.Fill;
        layout.Controls.Add(header, 0, 0); layout.Controls.Add(filters, 0, 1); layout.Controls.Add(_tabs, 0, 2); layout.Controls.Add(_status, 0, 3);
        _root.Controls.Add(layout);
        _studentBox.SelectedIndexChanged += (_, _) => RenderPage();
        _termBox.SelectedIndexChanged += (_, _) => RenderPage();
        _asOfBox.ValueChanged += (_, _) => RenderPage();
        _tabs.SelectedIndexChanged += (_, _) => RenderPage();
        _rendering = false;
        RenderPage();
    }

    private void RenderPage()
    {
        if (_rendering || _session is null || _tabs.SelectedTab is null) return;
        _rendering = true;
        try
        {
            Ui.Clear(_tabs.SelectedTab);
            Control content = _tabs.SelectedIndex switch
            {
                0 => Overview(), 1 => ExaminationPage(), 2 => AttendancePage(), 3 => ActivityPage(),
                4 => ComparisonPage(), _ => ExperimentsPage()
            };
            content.Dock = DockStyle.Fill;
            _tabs.SelectedTab.Controls.Add(content);
        }
        finally { _rendering = false; }
    }

    private async Task RunChangeAsync(Func<Task> action)
    {
        if (_busy) return;
        _busy = true;
        _root.Enabled = false;
        UseWaitCursor = true;
        try
        {
            await action();
            _status.Text = "Saved successfully · " + DateTime.Now.ToString("HH:mm:ss") + " · previous version retained as backup";
            RenderPage();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message + "\n\nThe requested change was not applied.", "Unable to save", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally { _root.Enabled = true; UseWaitCursor = false; _busy = false; }
    }

    private void ShowHelp() => MessageBox.Show(this,
        "DEMONSTRATION RULES\n\n" +
        "Academics: earned mark / maximum × 100. Assessment percentages are equally weighted within each subject; the overview average gives each marked subject equal weight. Unmarked results are excluded and counted separately.\n\n" +
        "Attendance: present / recorded scheduled days × 100. Excused days count as recorded non-attendance. Missing records are shown separately. Weekends and the fictional calendar's excluded holiday do not break a streak; absence, excused attendance and missing records do.\n\n" +
        "Activities: attended / recorded sessions × 100, for enrolled students. Missing session records remain visible. Awards are listed separately with no numerical weight. Membership applies across the demo dataset.\n\n" +
        "As-of date: charts and summaries exclude later dates. Teacher record lists show the whole selected term so future assessments can be prepared.\n\n" +
        "Charts: each dot represents an assessment or a week; gaps indicate missing results. Hover over dots for counts. Right-click a chart to save a PNG.\n\n" +
        "Permissions: students can view only their own records; the demo teacher manages all three students. This is a local educational prototype, not a shared school deployment.",
        "Rules and help", MessageBoxButtons.OK, MessageBoxIcon.Information);

    private void ShowStorage()
    {
        using var dialog = new Form { Text = "Local records and backup", StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(670, 260), Font = Font, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false };
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), FlowDirection = FlowDirection.TopDown, WrapContents = false };
        var label = Ui.Label("Records are stored here:\n" + _service.DataPath + "\n\nEach successful edit keeps the previous JSON version as .bak.\nExport a dated copy before experimenting with your records.");
        label.MaximumSize = new Size(620, 0);
        panel.Controls.Add(label);
        panel.Controls.Add(Ui.Button("Open data folder", (_, _) => Process.Start(new ProcessStartInfo(Path.GetDirectoryName(_service.DataPath)!) { UseShellExecute = true })));
        panel.Controls.Add(Ui.Button("Export backup…", async (_, _) =>
        {
            using var save = new SaveFileDialog { Filter = "JSON records|*.json", FileName = "student-records-backup-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".json" };
            if (save.ShowDialog(dialog) != DialogResult.OK) return;
            try { await _service.ExportBackupAsync(_session!, save.FileName); MessageBox.Show(dialog, "Backup exported."); }
            catch (Exception ex) { MessageBox.Show(dialog, ex.Message, "Backup failed"); }
        }, true));
        dialog.Controls.Add(panel);
        dialog.ShowDialog(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _service.RecordsChanged -= OnRecordsChanged;
        base.Dispose(disposing);
    }
}
