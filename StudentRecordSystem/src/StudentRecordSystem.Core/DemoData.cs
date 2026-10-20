namespace StudentRecordSystem.Core;

public static class DemoData
{
    public const string Password = "Demo123!";

    public static SchoolData Create()
    {
        var data = new SchoolData();
        data.Students.AddRange([
            new("S001", "Amaya Perera", "10 A"),
            new("S002", "Ravi Fernando", "10 A"),
            new("S003", "Nila Silva", "10 A")]);
        data.Terms.AddRange([
            Term("T2", "2026 · Term 2", new(2026, 5, 4), new(2026, 7, 24)),
            Term("T3", "2026 · Term 3", new(2026, 9, 7), new(2026, 12, 4))]);
        data.Accounts.Add(Authentication.CreateAccount("teacher", "teacher", "Ms. Jayasinghe", UserRole.Teacher, null, Password));
        foreach (var student in data.Students)
            data.Accounts.Add(Authentication.CreateAccount(student.Id, "student" + student.Id[^1], student.Name, UserRole.Student, student.Id, Password));
        data.Activities.AddRange([
            new("cricket", "Cricket", ActivityKind.Sport),
            new("swimming", "Swimming", ActivityKind.Sport),
            new("debate", "Debating Society", ActivityKind.Club),
            new("music", "Music Club", ActivityKind.Club)]);
        data.Memberships.AddRange([
            new("S001", "cricket"), new("S001", "debate"),
            new("S002", "swimming"), new("S002", "music"),
            new("S003", "cricket"), new("S003", "music")]);
        string[] subjects = ["Mathematics", "Science", "English", "History"];
        foreach (var term in data.Terms)
        {
            foreach (var date in term.SchoolDays.Where(d => d <= data.SuggestedAsOf))
            {
                for (int i = 0; i < data.Students.Count; i++)
                {
                    // One intentional missing record demonstrates coverage reporting.
                    if (i == 2 && date == new DateOnly(2026, 10, 14)) continue;
                    int day = date.DayNumber - term.Start.DayNumber;
                    var status = (day + i * 3) % (i == 1 ? 9 : 17) == 0 ? AttendanceStatus.Absent : AttendanceStatus.Present;
                    if (i == 0 && date == new DateOnly(2026, 9, 15)) status = AttendanceStatus.Excused;
                    data.Attendance.Add(new(data.Students[i].Id, date, status));
                }
            }
            for (int round = 0; round < 3; round++)
            {
                for (int subject = 0; subject < subjects.Length; subject++)
                {
                    var date = term.SchoolDays[(term.Id == "T2" ? 14 : 4) + round * (term.Id == "T2" ? 15 : 10) + subject];
                    if (date > data.SuggestedAsOf) continue;
                    string id = $"{term.Id}-{subject}-{round}";
                    decimal max = round == 1 ? 50 : 100;
                    data.Assessments.Add(new(id, term.Id, subjects[subject], round == 2 ? "Progress assessment" : $"Class assessment {round + 1}", date, max));
                    for (int i = 0; i < data.Students.Count; i++)
                    {
                        decimal percent = 59 + subject * 4 + round * (i == 1 ? -2 : 4) + i * 3 + (term.Id == "T3" ? 7 : 0);
                        decimal? mark = i == 2 && round == 2 && subject == 1 ? null : Math.Round(percent * max / 100, 1);
                        if (mark.HasValue) data.Marks.Add(new(data.Students[i].Id, id, mark));
                    }
                }
            }
            foreach (var activity in data.Activities)
            {
                int index = 0;
                foreach (var date in term.SchoolDays.Where(d => d <= data.SuggestedAsOf &&
                             (activity.Kind == ActivityKind.Sport ? d.DayOfWeek is DayOfWeek.Tuesday or DayOfWeek.Thursday : d.DayOfWeek == DayOfWeek.Friday)))
                {
                    var session = new ActivitySession($"{term.Id}-{activity.Id}-{index}", activity.Id, date,
                        activity.Kind == ActivityKind.Sport ? "Practice session" : "Club meeting / contribution");
                    data.Sessions.Add(session);
                    foreach (var member in data.Memberships.Where(m => m.ActivityId == activity.Id))
                    {
                        int offset = member.StudentId[^1] - '0';
                        data.Participation.Add(new(member.StudentId, session.Id, (index + offset) % 6 != 0));
                    }
                    index++;
                }
            }
        }
        data.Achievements.AddRange([
            new("A1", "S001", "cricket", new(2026, 9, 24), "Interschool match participation", "Represented the school team; participation recorded by the teacher."),
            new("A2", "S001", "debate", new(2026, 10, 2), "Debate event contribution", "Helped organise the junior debate workshop."),
            new("A3", "S002", "swimming", new(2026, 10, 8), "50 m freestyle · second place", "Fictional school sports meet result."),
            new("A4", "S003", "music", new(2026, 9, 25), "School concert performance", "Participated in the term concert.")]);
        DataValidation.Validate(data);
        return data;
    }

    private static SchoolTerm Term(string id, string name, DateOnly start, DateOnly end)
    {
        List<DateOnly> dates = [];
        for (var date = start; date <= end; date = date.AddDays(1))
            if (date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday &&
                date != new DateOnly(2026, 9, 23)) dates.Add(date);
        return new(id, name, start, end, dates);
    }
}
