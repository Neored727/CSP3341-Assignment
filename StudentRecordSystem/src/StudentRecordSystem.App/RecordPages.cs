using StudentRecordSystem.Core;

namespace StudentRecordSystem.App;

internal sealed partial class AppShell
{
    private Control ExaminationPage()
    {
        var root = PageLayout("Examination marks", "Select a result to enter or correct a mark. Unmarked results remain separate from zero. This list includes the whole selected term.", out var tools);
        var rows = _data.Assessments.Where(a => a.TermId == Term.Id).OrderBy(a => a.Date).ThenBy(a => a.Subject).Select(a =>
        {
            var mark = _data.Marks.SingleOrDefault(m => m.StudentId == StudentId && m.AssessmentId == a.Id)?.Mark;
            return new { Key = a.Id, Date = a.Date.ToString("dd MMM yyyy"), a.Subject, Assessment = a.Title,
                Maximum = a.MaximumMark.ToString("0.##"), Mark = mark.HasValue ? mark.Value.ToString("0.##") : "Not marked",
                Percentage = Ui.Percent(mark.HasValue ? (double)(mark.Value * 100m / a.MaximumMark) : null),
                Included_as_of_date = a.Date <= AsOf ? "Yes" : "Later assessment" };
        }).ToList();
        var grid = Ui.Grid(rows);
        if (Teacher)
        {
            tools.Controls.Add(Ui.Button("Enter / correct mark", async (_, _) => await EditMarkAsync(grid), true));
            tools.Controls.Add(Ui.Button("Create assessment", async (_, _) => await AddAssessmentAsync()));
            grid.CellDoubleClick += async (_, e) => { if (e.RowIndex >= 0) await EditMarkAsync(grid); };
        }
        tools.Controls.Add(Ui.Label(Teacher ? "Assessments are shared across the class; marks belong to the selected student." : "Student view · records are read-only.", 9));
        root.Controls.Add(grid, 0, 2);
        return root;
    }

    private async Task EditMarkAsync(DataGridView grid)
    {
        string? key = Ui.SelectedKey(grid);
        if (key is null) { MessageBox.Show(this, "Create or select an assessment first."); return; }
        var assessment = _data.Assessments.Single(a => a.Id == key);
        var existing = _data.Marks.SingleOrDefault(m => m.StudentId == StudentId && m.AssessmentId == key)?.Mark;
        using var dialog = new EditorDialog("Mark · " + assessment.Subject);
        var mark = EditorDialog.Number(existing ?? 0, assessment.MaximumMark);
        var unmarked = new CheckBox { Text = "Not marked · exclude from averages", Checked = !existing.HasValue, AutoSize = true };
        mark.Enabled = !unmarked.Checked;
        unmarked.CheckedChanged += (_, _) => mark.Enabled = !unmarked.Checked;
        dialog.Add("Assessment", Ui.Label(assessment.Title));
        dialog.Add("Maximum mark", Ui.Label(assessment.MaximumMark.ToString("0.##")));
        dialog.Add("Status", unmarked);
        dialog.Add("Earned mark", mark);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        decimal? value = unmarked.Checked ? null : mark.Value;
        string student = StudentId;
        await RunChangeAsync(() => _service.SaveMarkAsync(_session!, student, key, value));
    }

    private async Task AddAssessmentAsync()
    {
        using var dialog = new EditorDialog("Create a class assessment");
        var subject = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown };
        subject.Items.AddRange(_data.Assessments.Select(a => a.Subject).Distinct().Order().Cast<object>().ToArray());
        subject.Text = _subject ?? "Mathematics";
        var title = EditorDialog.TextInput();
        var date = EditorDialog.DateInput(WithinTerm());
        date.MinDate = Term.Start.ToDateTime(TimeOnly.MinValue); date.MaxDate = Term.End.ToDateTime(TimeOnly.MinValue);
        var maximum = EditorDialog.Number(100);
        dialog.Add("Subject", subject); dialog.Add("Title", title); dialog.Add("Assessment date", date); dialog.Add("Maximum mark", maximum);
        dialog.ValidateInput = () => string.IsNullOrWhiteSpace(subject.Text) || string.IsNullOrWhiteSpace(title.Text) ? "Enter a subject and assessment title." :
            maximum.Value <= 0 ? "Maximum mark must be greater than zero." : null;
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        string term = Term.Id;
        await RunChangeAsync(() => _service.AddAssessmentAsync(_session!, term, subject.Text, title.Text, DateOnly.FromDateTime(date.Value), maximum.Value));
    }

    private Control AttendancePage()
    {
        var summary = Analytics.Attendance(_data, StudentId, Term, AsOf);
        var root = PageLayout("School attendance", $"As-of summary: {summary.Present} present · {summary.Absent} absent · {summary.Excused} excused · {summary.Missing} missing records. Excused days count as non-attendance.", out var tools);
        // Put the reporting date first without scrolling during grid binding/layout.
        var rows = Term.SchoolDays.OrderBy(d => d > AsOf).ThenByDescending(d => d).Select(date =>
        {
            var record = _data.Attendance.SingleOrDefault(a => a.StudentId == StudentId && a.Date == date);
            return new { Key = date.ToString("yyyy-MM-dd"), Date = date.ToString("dd MMM yyyy"), Day = date.DayOfWeek.ToString(),
                Status = record?.Status.ToString() ?? "Not recorded", Included_as_of_date = date <= AsOf ? "Yes" : "Later school day" };
        }).ToList();
        var grid = Ui.Grid(rows);
        if (Teacher)
        {
            tools.Controls.Add(Ui.Button("Record / correct attendance", async (_, _) => await EditAttendanceAsync(grid), true));
            grid.CellDoubleClick += async (_, e) => { if (e.RowIndex >= 0) await EditAttendanceAsync(grid); };
        }
        tools.Controls.Add(Ui.Label("Only scheduled school days appear. The calendar excludes weekends and the demo holiday on 23 September.", 9));
        root.Controls.Add(grid, 0, 2);
        return root;
    }

    private async Task EditAttendanceAsync(DataGridView grid)
    {
        string? key = Ui.SelectedKey(grid);
        if (key is null) return;
        var date = DateOnly.Parse(key);
        var existing = _data.Attendance.SingleOrDefault(a => a.StudentId == StudentId && a.Date == date);
        using var dialog = new EditorDialog("Attendance · " + date.ToString("dd MMM yyyy"));
        var status = Ui.Combo();
        status.Items.AddRange(new object[] { "Present", "Absent", "Excused", "Not recorded" });
        status.SelectedItem = existing?.Status.ToString() ?? "Not recorded";
        dialog.Add("Attendance status", status);
        dialog.Add("Rule", Ui.Label("Not recorded removes the entry; it does not record an absence.", 9));
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        AttendanceStatus? value = status.SelectedIndex == 3 ? null : Enum.Parse<AttendanceStatus>(status.Text);
        string student = StudentId;
        await RunChangeAsync(() => _service.SaveAttendanceAsync(_session!, student, date, value));
    }

    private Control ActivityPage()
    {
        var root = PageLayout("Sports and extracurricular activities", "Participation and achievements retain separate meanings. Session schedules apply to every enrolled student. This list shows the whole term.", out var tools);
        var activity = Ui.Combo(210);
        var available = _data.Activities.Where(a => Teacher || _data.Memberships.Any(m => m.StudentId == StudentId && m.ActivityId == a.Id)).ToList();
        if (!available.Any(a => a.Id == _activityId)) _activityId = available.FirstOrDefault()?.Id;
        activity.Items.AddRange(available.Select(a => new Choice(a.Id, a.Name + " · " + a.Kind)).ToArray());
        activity.SelectedItem = activity.Items.Cast<Choice>().FirstOrDefault(c => c.Id == _activityId);
        tools.Controls.Add(activity);
        var sessions = _data.Sessions.Where(s => s.ActivityId == _activityId && s.Date >= Term.Start && s.Date <= Term.End).OrderByDescending(s => s.Date).Select(s =>
        {
            var p = _data.Participation.SingleOrDefault(p => p.StudentId == StudentId && p.SessionId == s.Id);
            return new { Key = s.Id, Date = s.Date.ToString("dd MMM yyyy"), s.Description,
                Participation = p is null ? "Not recorded" : p.Attended ? "Attended" : "Missed",
                Included_as_of_date = s.Date <= AsOf ? "Yes" : "Later session" };
        }).ToList();
        var sessionGrid = Ui.Grid(sessions);
        bool member = _data.Memberships.Any(m => m.StudentId == StudentId && m.ActivityId == _activityId);
        if (Teacher)
        {
            tools.Controls.Add(Ui.Button(member ? "Already enrolled" : "Enrol student", async (_, _) =>
            {
                if (_activityId is null || member) return;
                string student = StudentId, id = _activityId;
                await RunChangeAsync(() => _service.EnrolActivityAsync(_session!, student, id));
            }));
            tools.Controls.Add(Ui.Button("Add session", async (_, _) => await AddActivitySessionAsync()));
            tools.Controls.Add(Ui.Button("Record participation", async (_, _) => await EditParticipationAsync(sessionGrid), true));
            tools.Controls.Add(Ui.Button("Add achievement", async (_, _) => await AddAchievementAsync()));
            sessionGrid.CellDoubleClick += async (_, e) => { if (e.RowIndex >= 0) await EditParticipationAsync(sessionGrid); };
        }
        activity.SelectedIndexChanged += (_, _) => { _activityId = (activity.SelectedItem as Choice)?.Id; RenderPage(); };
        var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        var summary = Analytics.Activities(_data, StudentId, Term, AsOf).FirstOrDefault(a => a.Activity == available.FirstOrDefault(x => x.Id == _activityId)?.Name);
        string text = !member ? "This student is not enrolled. Enrolment is required before recording participation or achievements." :
            summary is null || summary.Scheduled == 0 ? "No sessions scheduled in the selected reporting period." :
            $"As of {AsOf:dd MMM}: {Ui.Percent(summary.Percentage)} participation · {summary.Attended} attended / {summary.Recorded} recorded / {summary.Scheduled} scheduled.";
        content.Controls.Add(Ui.Label(text, 10, true), 0, 0);
        content.Controls.Add(sessionGrid, 0, 1);
        content.Controls.Add(Ui.Label("Achievements and contributions · through the as-of date", 11, true), 0, 2);
        var awards = _data.Achievements.Where(a => a.StudentId == StudentId && a.ActivityId == _activityId && Analytics.InPeriod(a.Date, Term, AsOf)).OrderByDescending(a => a.Date)
            .Select(a => new { Date = a.Date.ToString("dd MMM yyyy"), Achievement = a.Title, a.Notes }).ToList();
        content.Controls.Add(Ui.Grid(awards), 0, 3);
        root.Controls.Add(content, 0, 2);
        return root;
    }

    private bool EnsureMembership()
    {
        if (_activityId is not null && _data.Memberships.Any(m => m.StudentId == StudentId && m.ActivityId == _activityId)) return true;
        MessageBox.Show(this, "Enrol this student in the selected activity first.");
        return false;
    }

    private async Task AddActivitySessionAsync()
    {
        if (_activityId is null) return;
        using var dialog = new EditorDialog("Schedule an activity session");
        var date = EditorDialog.DateInput(WithinTerm());
        date.MinDate = Term.Start.ToDateTime(TimeOnly.MinValue); date.MaxDate = Term.End.ToDateTime(TimeOnly.MinValue);
        var description = EditorDialog.TextInput("Practice / meeting");
        dialog.Add("Date", date); dialog.Add("Description", description);
        dialog.Add("Scope", Ui.Label("One session per activity per date. Shared by enrolled students.", 9));
        dialog.ValidateInput = () => string.IsNullOrWhiteSpace(description.Text) ? "Enter a description." :
            _data.Sessions.Any(s => s.ActivityId == _activityId && s.Date == DateOnly.FromDateTime(date.Value)) ? "A session for this activity already exists on that date." : null;
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        string activity = _activityId;
        await RunChangeAsync(() => _service.AddSessionAsync(_session!, activity, DateOnly.FromDateTime(date.Value), description.Text));
    }

    private async Task EditParticipationAsync(DataGridView grid)
    {
        if (!EnsureMembership()) return;
        string? key = Ui.SelectedKey(grid);
        if (key is null) { MessageBox.Show(this, "Schedule or select an activity session first."); return; }
        var current = _data.Participation.SingleOrDefault(p => p.StudentId == StudentId && p.SessionId == key);
        using var dialog = new EditorDialog("Record activity participation");
        var status = Ui.Combo(); status.Items.AddRange(new object[] { "Attended", "Missed", "Not recorded" });
        status.SelectedIndex = current is null ? 2 : current.Attended ? 0 : 1;
        dialog.Add("Participation", status);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        bool? attended = status.SelectedIndex == 2 ? null : status.SelectedIndex == 0;
        string student = StudentId;
        await RunChangeAsync(() => _service.SaveParticipationAsync(_session!, student, key, attended));
    }

    private async Task AddAchievementAsync()
    {
        if (!EnsureMembership()) return;
        using var dialog = new EditorDialog("Record an achievement or contribution");
        var date = EditorDialog.DateInput(WithinTerm());
        date.MinDate = Term.Start.ToDateTime(TimeOnly.MinValue); date.MaxDate = Term.End.ToDateTime(TimeOnly.MinValue);
        var title = EditorDialog.TextInput(); var notes = EditorDialog.TextInput("", multiline: true);
        dialog.Add("Date", date); dialog.Add("Achievement", title); dialog.Add("Notes / contribution", notes);
        dialog.ValidateInput = () => string.IsNullOrWhiteSpace(title.Text) ? "Describe the achievement or contribution." : null;
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        string student = StudentId, activity = _activityId!;
        await RunChangeAsync(() => _service.AddAchievementAsync(_session!, student, activity, DateOnly.FromDateTime(date.Value), title.Text, notes.Text));
    }

    private DateOnly WithinTerm() => AsOf < Term.Start ? Term.Start : AsOf > Term.End ? Term.End : AsOf;
}
