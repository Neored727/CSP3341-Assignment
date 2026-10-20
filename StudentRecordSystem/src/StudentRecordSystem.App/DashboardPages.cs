using StudentRecordSystem.Core;

namespace StudentRecordSystem.App;

internal sealed partial class AppShell
{
    private Control Overview()
    {
        var viewport = new Panel { AutoScroll = true };
        var root = new TableLayoutPanel { Dock = DockStyle.Top, Height = 670, ColumnCount = 1, RowCount = 3 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        viewport.Controls.Add(root);
        viewport.Resize += (_, _) => root.Height = Math.Max(670, viewport.ClientSize.Height);
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 97));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var attendance = Analytics.Attendance(_data, StudentId, Term, AsOf);
        var subjects = Analytics.Subjects(_data, StudentId, Term, AsOf);
        var sports = Analytics.Activities(_data, StudentId, Term, AsOf, ActivityKind.Sport);
        int sportsRecorded = sports.Sum(s => s.Recorded);
        double? sportsRate = sportsRecorded == 0 ? null : 100.0 * sports.Sum(s => s.Attended) / sportsRecorded;
        var stats = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4 };
        for (int i = 0; i < 4; i++) stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        stats.Controls.Add(Stat("ACADEMIC AVERAGE", Ui.Percent(Analytics.AcademicAverage(_data, StudentId, Term, AsOf)),
            $"{subjects.Sum(s => s.Marked)} of {subjects.Sum(s => s.Assessments)} assessments marked"), 0, 0);
        stats.Controls.Add(Stat("RECORDED ATTENDANCE", Ui.Percent(attendance.Percentage),
            $"{attendance.Recorded} days recorded · {attendance.Missing} missing"), 1, 0);
        stats.Controls.Add(Stat("CURRENT STREAK", attendance.CurrentStreak + " days",
            $"Longest verified streak: {attendance.LongestStreak} days"), 2, 0);
        stats.Controls.Add(Stat("SPORTS PARTICIPATION", Ui.Percent(sportsRate),
            $"{sportsRecorded} recorded / {sports.Sum(s => s.Scheduled)} scheduled sessions"), 3, 0);
        var filters = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true };
        var subject = Ui.Combo(200);
        var names = _data.Assessments.Where(a => a.TermId == Term.Id).Select(a => a.Subject).Distinct().Order().ToList();
        subject.Items.AddRange(names.Cast<object>().ToArray());
        if (!names.Contains(_subject ?? "")) _subject = names.FirstOrDefault();
        subject.SelectedItem = _subject;
        var activity = Ui.Combo(200);
        var enrolled = _data.Activities.Where(a => _data.Memberships.Any(m => m.StudentId == StudentId && m.ActivityId == a.Id)).ToList();
        activity.Items.AddRange(enrolled.Select(a => new Choice(a.Id, a.Name)).ToArray());
        if (!enrolled.Any(a => a.Id == _activityId)) _activityId = enrolled.FirstOrDefault()?.Id;
        activity.SelectedItem = activity.Items.Cast<Choice>().FirstOrDefault(c => c.Id == _activityId);
        filters.Controls.AddRange([Ui.Label("Academic subject", 9, true), subject, Ui.Label("Participation activity", 9, true), activity,
            Ui.Label("Hover over dots · right-click to save charts", 9)]);
        subject.SelectedIndexChanged += (_, _) => { _subject = subject.SelectedItem?.ToString(); RenderPage(); };
        activity.SelectedIndexChanged += (_, _) => { _activityId = (activity.SelectedItem as Choice)?.Id; RenderPage(); };
        var charts = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        charts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); charts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        charts.RowStyles.Add(new RowStyle(SizeType.Percent, 50)); charts.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        string context = _data.Students.Single(s => s.Id == StudentId).Name + " · " + Term.Name;
        charts.Controls.Add(new TrendChart
        {
            Title = (_subject ?? "Subject") + " · assessment marks (%)", Subtitle = context + " · assessment dates; unmarked results leave gaps",
            Points = _subject is null ? [] : Analytics.AcademicTrend(_data, StudentId, Term, AsOf, _subject), SeriesColor = Ui.Blue
        }, 0, 0);
        charts.Controls.Add(new TrendChart
        {
            Title = "Attendance · weekly percentage", Subtitle = context + " · present / recorded days; missing days excluded",
            Points = Analytics.WeeklyAttendance(_data, StudentId, Term, AsOf), SeriesColor = Ui.Teal
        }, 1, 0);
        charts.Controls.Add(new TrendChart
        {
            Title = (enrolled.FirstOrDefault(a => a.Id == _activityId)?.Name ?? "Activity") + " · weekly participation (%)",
            Subtitle = context + " · attended / recorded sessions",
            Points = _activityId is null ? [] : Analytics.WeeklyParticipation(_data, StudentId, Term, AsOf, _activityId),
            SeriesColor = Color.FromArgb(134, 86, 181)
        }, 0, 1);
        var insights = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(5), Padding = new Padding(16), AutoScroll = true };
        var list = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        var heading = Ui.Label("What the records show", 12, true);
        list.Controls.Add(heading);
        foreach (var text in Analytics.Insights(_data, StudentId, Term, AsOf))
        {
            var label = Ui.Label(text, 9);
            label.MaximumSize = new Size(480, 0);
            list.Controls.Add(label);
        }
        insights.Resize += (_, _) =>
        {
            foreach (Label label in list.Controls.OfType<Label>()) label.MaximumSize = new Size(Math.Max(150, insights.ClientSize.Width - 50), 0);
        };
        insights.Controls.Add(list);
        charts.Controls.Add(insights, 1, 1);
        root.Controls.Add(stats, 0, 0); root.Controls.Add(filters, 0, 1); root.Controls.Add(charts, 0, 2);
        return viewport;
    }

    private static Control Stat(string heading, string value, string detail)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.White,
            Margin = new Padding(5), Padding = new Padding(12, 5, 8, 4), ColumnCount = 1, RowCount = 3 };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(Ui.Label(heading, 8, true), 0, 0);
        var big = Ui.Label(value, 20, true); big.ForeColor = Ui.Blue; big.Margin = Padding.Empty;
        panel.Controls.Add(big, 0, 1);
        var small = Ui.Label(detail, 8); small.Dock = DockStyle.Fill; small.AutoSize = false; small.Margin = Padding.Empty;
        panel.Controls.Add(small, 0, 2);
        return panel;
    }

    private Control ComparisonPage()
    {
        var root = PageLayout("Term comparison", "All figures use the selected as-of date. The current term may be incomplete; check coverage before comparing.", out var tools);
        tools.Controls.Add(Ui.Label("Academic means give equal weight to each marked subject. Awards are not converted into scores.", 10));
        var rows = _data.Terms.OrderBy(t => t.Start).Select(term =>
        {
            var attendance = Analytics.Attendance(_data, StudentId, term, AsOf);
            var subjects = Analytics.Subjects(_data, StudentId, term, AsOf);
            var activities = Analytics.Activities(_data, StudentId, term, AsOf, ActivityKind.Sport);
            int recorded = activities.Sum(a => a.Recorded);
            return new
            {
                Term = term.Name, Academic_average = Ui.Percent(Analytics.AcademicAverage(_data, StudentId, term, AsOf)),
                Marked_assessments = $"{subjects.Sum(s => s.Marked)} / {subjects.Sum(s => s.Assessments)}",
                Attendance = Ui.Percent(attendance.Percentage), School_days_recorded = attendance.Recorded,
                Missing_attendance = attendance.Missing,
                Sports_participation = Ui.Percent(recorded == 0 ? null : 100.0 * activities.Sum(a => a.Attended) / recorded),
                Sports_sessions_recorded = $"{recorded} / {activities.Sum(a => a.Scheduled)}",
                Achievements = _data.Achievements.Count(a => a.StudentId == StudentId && Analytics.InPeriod(a.Date, term, AsOf))
            };
        }).ToList();
        root.Controls.Add(Ui.Grid(rows), 0, 2);
        return root;
    }

    private Control ExperimentsPage()
    {
        var root = PageLayout("C# and .NET trade-offs", "Controlled examples for your evaluation. These experiments are isolated from student records; inspect LanguageExperiments.cs.", out var tools);
        var body = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = Color.White, Padding = new Padding(18) };
        void Run()
        {
            Ui.Clear(body);
            foreach (var result in new[] { LanguageExperiments.IntegerDivision(), LanguageExperiments.DeferredQuery() })
            {
                body.Controls.Add(Ui.Label(result.Title, 15, true));
                body.Controls.Add(Ui.Label(result.Problem, 11));
                var improved = Ui.Label(result.Improved, 11, true); improved.ForeColor = Ui.Teal;
                body.Controls.Add(improved);
                var explanation = Ui.Label(result.Explanation, 10); explanation.MaximumSize = new Size(850, 0); explanation.Margin = new Padding(3, 8, 3, 28);
                body.Controls.Add(explanation);
            }
            var note = Ui.Label("Positive features to inspect: typed records and nullable marks preserve meaning; reusable LINQ queries summarize the same data for charts and tables. " +
                "Use your own observations when writing Part B. The experiments demonstrate reproducible behaviour, not a claim that you personally encountered these bugs.", 10);
            note.MaximumSize = new Size(850, 0); body.Controls.Add(note);
        }
        tools.Controls.Add(Ui.Button("Run both examples", (_, _) => Run(), true));
        root.Controls.Add(body, 0, 2);
        Run();
        return root;
    }

    private static TableLayoutPanel PageLayout(string title, string description, out FlowLayoutPanel tools)
    {
        var root = new TableLayoutPanel { ColumnCount = 1, RowCount = 3 };
        // Explicit sizing prevents a one-column table from staying at its preferred content width.
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 95));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var words = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        words.Controls.Add(Ui.Label(title, 17, true));
        words.Controls.Add(Ui.Label(description, 9));
        words.Resize += (_, _) =>
        {
            foreach (Label label in words.Controls.OfType<Label>()) label.MaximumSize = new Size(Math.Max(200, words.ClientSize.Width - 15), 0);
        };
        tools = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true };
        root.Controls.Add(words, 0, 0); root.Controls.Add(tools, 0, 1);
        return root;
    }
}
