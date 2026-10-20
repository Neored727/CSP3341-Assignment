namespace StudentRecordSystem.Core;

public enum UserRole { Student, Teacher }
public enum AttendanceStatus { Present, Absent, Excused }
public enum ActivityKind { Sport, Club }

// The UI receives a session after authentication, rather than choosing its own role.
public sealed record UserSession(string AccountId, string DisplayName, UserRole Role, string? StudentId)
{
    // Opaque token registered by SchoolService; knowing a teacher's account details is insufficient.
    public string Token { get; init; } = Guid.NewGuid().ToString("N");
}
public sealed record Student(string Id, string Name, string ClassName);
public sealed record SchoolTerm(string Id, string Name, DateOnly Start, DateOnly End,
    List<DateOnly> SchoolDays);
public sealed record Account(string Id, string Username, string DisplayName, UserRole Role,
    string? StudentId, string Salt, string PasswordHash);
public sealed record Assessment(string Id, string TermId, string Subject, string Title,
    DateOnly Date, decimal MaximumMark);
public sealed record MarkRecord(string StudentId, string AssessmentId, decimal? Mark);
public sealed record AttendanceRecord(string StudentId, DateOnly Date, AttendanceStatus Status);
public sealed record Activity(string Id, string Name, ActivityKind Kind);
public sealed record Membership(string StudentId, string ActivityId);
public sealed record ActivitySession(string Id, string ActivityId, DateOnly Date, string Description);
public sealed record Participation(string StudentId, string SessionId, bool Attended);
public sealed record Achievement(string Id, string StudentId, string ActivityId,
    DateOnly Date, string Title, string Notes);

public sealed class SchoolData
{
    public int SchemaVersion { get; set; } = 1;
    public DateOnly SuggestedAsOf { get; set; } = new(2026, 10, 16);
    public List<Student> Students { get; set; } = [];
    public List<SchoolTerm> Terms { get; set; } = [];
    public List<Account> Accounts { get; set; } = [];
    public List<Assessment> Assessments { get; set; } = [];
    public List<MarkRecord> Marks { get; set; } = [];
    public List<AttendanceRecord> Attendance { get; set; } = [];
    public List<Activity> Activities { get; set; } = [];
    public List<Membership> Memberships { get; set; } = [];
    public List<ActivitySession> Sessions { get; set; } = [];
    public List<Participation> Participation { get; set; } = [];
    public List<Achievement> Achievements { get; set; } = [];
}

public sealed record TrendPoint(string Label, double? Value, string Detail);
public sealed record AttendanceSummary(int Present, int Absent, int Excused, int Missing,
    int CurrentStreak, int LongestStreak)
{
    public int Recorded => Present + Absent + Excused;
    // Excused days remain non-attendance under this explicitly stated demo rule.
    public double? Percentage => Recorded == 0 ? null : 100.0 * Present / Recorded;
}
public sealed record SubjectSummary(string Subject, double? Average, int Marked, int Assessments);
public sealed record ActivitySummary(string Activity, int Attended, int Recorded, int Scheduled)
{
    public double? Percentage => Recorded == 0 ? null : Attended * 100.0 / Recorded;
}
