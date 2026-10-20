namespace StudentRecordSystem.Core;

// Pure calculations: no controls, files, or changes to the source records.
public static class Analytics
{
    public static AttendanceSummary Attendance(SchoolData data, string studentId, SchoolTerm term, DateOnly asOf)
    {
        var dates = term.SchoolDays.Where(d => d <= asOf).Order().ToList();
        var records = data.Attendance.Where(a => a.StudentId == studentId).ToDictionary(a => a.Date, a => a.Status);
        int present = 0, absent = 0, excused = 0, missing = 0, streak = 0, longest = 0;
        foreach (var date in dates)
        {
            if (!records.TryGetValue(date, out var status)) { missing++; streak = 0; continue; }
            if (status == AttendanceStatus.Present) { present++; streak++; longest = Math.Max(longest, streak); }
            else { streak = 0; if (status == AttendanceStatus.Absent) absent++; else excused++; }
        }
        return new(present, absent, excused, missing, streak, longest);
    }

    public static List<SubjectSummary> Subjects(SchoolData data, string studentId, SchoolTerm term, DateOnly asOf) =>
        data.Assessments.Where(a => a.TermId == term.Id && a.Date <= asOf)
            .GroupBy(a => a.Subject).OrderBy(g => g.Key).Select(group =>
            {
                var marks = group.Select(a => (Assessment: a, Result: data.Marks.SingleOrDefault(m =>
                    m.StudentId == studentId && m.AssessmentId == a.Id)?.Mark))
                    .Where(x => x.Result.HasValue).ToList();
                return new SubjectSummary(group.Key, marks.Count == 0 ? null :
                    marks.Average(x => (double)(100m * x.Result!.Value / x.Assessment.MaximumMark)), marks.Count, group.Count());
            }).ToList();

    // Each subject gets equal influence; assessments within a subject are equally weighted percentages.
    public static double? AcademicAverage(SchoolData data, string studentId, SchoolTerm term, DateOnly asOf)
    {
        var values = Subjects(data, studentId, term, asOf).Where(s => s.Average.HasValue).ToList();
        return values.Count == 0 ? null : values.Average(s => s.Average!.Value);
    }

    public static List<TrendPoint> AcademicTrend(SchoolData data, string studentId, SchoolTerm term, DateOnly asOf, string? subject = null) =>
        data.Assessments.Where(a => a.TermId == term.Id && a.Date <= asOf &&
            (subject is null || a.Subject == subject)).OrderBy(a => a.Date).ThenBy(a => a.Subject)
            .Select(a =>
            {
                var mark = data.Marks.SingleOrDefault(m => m.StudentId == studentId && m.AssessmentId == a.Id)?.Mark;
                return new TrendPoint(a.Date.ToString("dd MMM"), mark.HasValue ? (double)(100m * mark.Value / a.MaximumMark) : null,
                    $"{a.Subject} · {a.Title}\n{a.Date:dd MMM yyyy}\n" +
                    (mark.HasValue ? $"{mark:0.##} / {a.MaximumMark:0.##}" : "Not marked"));
            }).ToList();

    public static List<TrendPoint> WeeklyAttendance(SchoolData data, string studentId, SchoolTerm term, DateOnly asOf) =>
        term.SchoolDays.Where(d => d <= asOf).GroupBy(WeekStart).OrderBy(g => g.Key).Select(week =>
        {
            var dates = week.ToHashSet();
            var records = data.Attendance.Where(a => a.StudentId == studentId && dates.Contains(a.Date)).ToList();
            var present = records.Count(a => a.Status == AttendanceStatus.Present);
            return new TrendPoint(week.Key.ToString("dd MMM"), records.Count == 0 ? null : 100.0 * present / records.Count,
                $"Week of {week.Key:dd MMM}\n{present} present / {records.Count} recorded\n{dates.Count - records.Count} missing school-day records");
        }).ToList();

    public static List<ActivitySummary> Activities(SchoolData data, string studentId, SchoolTerm term, DateOnly asOf, ActivityKind? kind = null) =>
        data.Activities.Where(a => (kind is null || a.Kind == kind) &&
            data.Memberships.Any(m => m.StudentId == studentId && m.ActivityId == a.Id)).Select(activity =>
        {
            var sessionIds = data.Sessions.Where(s => s.ActivityId == activity.Id && InPeriod(s.Date, term, asOf)).Select(s => s.Id).ToHashSet();
            var records = data.Participation.Where(p => p.StudentId == studentId && sessionIds.Contains(p.SessionId)).ToList();
            return new ActivitySummary(activity.Name, records.Count(p => p.Attended), records.Count, sessionIds.Count);
        }).OrderBy(x => x.Activity).ToList();

    public static List<TrendPoint> WeeklyParticipation(SchoolData data, string studentId, SchoolTerm term, DateOnly asOf, string activityId)
    {
        var member = data.Memberships.Any(m => m.StudentId == studentId && m.ActivityId == activityId);
        if (!member) return [];
        return data.Sessions.Where(s => s.ActivityId == activityId && InPeriod(s.Date, term, asOf))
            .GroupBy(s => WeekStart(s.Date)).OrderBy(g => g.Key).Select(week =>
            {
                var ids = week.Select(s => s.Id).ToHashSet();
                var records = data.Participation.Where(p => p.StudentId == studentId && ids.Contains(p.SessionId)).ToList();
                var attended = records.Count(p => p.Attended);
                return new TrendPoint(week.Key.ToString("dd MMM"), records.Count == 0 ? null : 100.0 * attended / records.Count,
                    $"Week of {week.Key:dd MMM}\n{attended} attended / {records.Count} recorded\n{ids.Count - records.Count} sessions awaiting entry");
            }).ToList();
    }

    public static List<string> Insights(SchoolData data, string studentId, SchoolTerm term, DateOnly asOf)
    {
        var lines = new List<string>();
        var attendance = Attendance(data, studentId, term, asOf);
        lines.Add(attendance.Recorded == 0 ? "Attendance: no recorded school days yet." :
            $"Attendance: {attendance.Percentage:0.#}% across {attendance.Recorded} recorded days; {attendance.Missing} missing records.");
        lines.Add($"Current recorded attendance streak: {attendance.CurrentStreak} scheduled school days. Missing records interrupt a verified streak.");
        var previous = data.Terms.Where(t => t.End < term.Start).OrderByDescending(t => t.End).FirstOrDefault();
        var currentAverage = AcademicAverage(data, studentId, term, asOf);
        if (previous is not null && currentAverage.HasValue)
        {
            var before = AcademicAverage(data, studentId, previous, asOf);
            if (before.HasValue) lines.Add($"Recorded academic average: {currentAverage:0.#}% versus {before:0.#}% in {previous.Name} " +
                $"({currentAverage - before:+0.#;-0.#;0} percentage points). Assessment coverage and difficulty may differ.");
        }
        var sports = Activities(data, studentId, term, asOf, ActivityKind.Sport).Where(a => a.Recorded > 0).ToList();
        if (sports.Count > 0 && currentAverage.HasValue)
        {
            double rate = sports.Sum(a => a.Attended) * 100.0 / sports.Sum(a => a.Recorded);
            lines.Add($"Academics and sport: {currentAverage:0.#}% academic average alongside {rate:0.#}% recorded sports participation. These describe different measures; no causal claim or overall balance score is made.");
        }
        return lines;
    }

    public static DateOnly WeekStart(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
    public static bool InPeriod(DateOnly date, SchoolTerm term, DateOnly asOf) => date >= term.Start && date <= term.End && date <= asOf;
}
