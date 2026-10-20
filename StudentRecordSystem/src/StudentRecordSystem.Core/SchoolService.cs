namespace StudentRecordSystem.Core;

// This service is the permission boundary. Editing a control is never sufficient authorization.
public sealed class SchoolService
{
    private SchoolData _data;
    private readonly JsonSchoolStore _store;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly Dictionary<string, UserSession> _sessions = [];
    public event EventHandler? RecordsChanged;
    public string DataPath => _store.FilePath;

    public SchoolService(SchoolData data, JsonSchoolStore store)
    {
        DataValidation.Validate(data);
        _data = JsonSchoolStore.Clone(data);
        _store = store;
    }

    public UserSession? Login(string username, string password)
    {
        var session = Authentication.Login(_data, username, password);
        if (session is not null) _sessions[session.Token] = session;
        return session;
    }

    public void Logout(UserSession session) => _sessions.Remove(session.Token);

    public SchoolData Snapshot(UserSession session)
    {
        RequireSession(session);
        var copy = JsonSchoolStore.Clone(_data);
        copy.Accounts.Clear(); // Password hashes are not needed by the presentation layer.
        if (session.Role == UserRole.Student)
        {
            var id = session.StudentId!;
            copy.Students.RemoveAll(s => s.Id != id);
            copy.Marks.RemoveAll(m => m.StudentId != id);
            copy.Attendance.RemoveAll(a => a.StudentId != id);
            copy.Memberships.RemoveAll(m => m.StudentId != id);
            copy.Participation.RemoveAll(p => p.StudentId != id);
            copy.Achievements.RemoveAll(a => a.StudentId != id);
        }
        return copy;
    }

    private void RequireSession(UserSession session)
    {
        if (!_sessions.TryGetValue(session.Token, out var registered) || registered != session ||
            !_data.Accounts.Any(a => a.Id == session.AccountId && a.Role == session.Role &&
            a.StudentId == session.StudentId && a.DisplayName == session.DisplayName))
            throw new UnauthorizedAccessException("This session is invalid. Sign in again.");
    }

    private async Task ChangeAsync(UserSession session, Action<SchoolData> change)
    {
        RequireSession(session);
        if (session.Role != UserRole.Teacher) throw new UnauthorizedAccessException("Only teachers can change records.");
        await _writeLock.WaitAsync();
        try
        {
            // A failed validation or disk write leaves the live records unchanged.
            var candidate = JsonSchoolStore.Clone(_data);
            change(candidate);
            DataValidation.Validate(candidate);
            await _store.SaveAsync(candidate);
            _data = candidate;
        }
        finally { _writeLock.Release(); }
        RecordsChanged?.Invoke(this, EventArgs.Empty);
    }

    public Task SaveMarkAsync(UserSession session, string studentId, string assessmentId, decimal? mark) => ChangeAsync(session, data =>
    {
        data.Marks.RemoveAll(m => m.StudentId == studentId && m.AssessmentId == assessmentId);
        if (mark.HasValue) data.Marks.Add(new(studentId, assessmentId, mark));
    });

    public Task SaveAttendanceAsync(UserSession session, string studentId, DateOnly date, AttendanceStatus? status) => ChangeAsync(session, data =>
    {
        data.Attendance.RemoveAll(a => a.StudentId == studentId && a.Date == date);
        if (status.HasValue) data.Attendance.Add(new(studentId, date, status.Value));
    });

    public Task AddAssessmentAsync(UserSession session, string termId, string subject, string title, DateOnly date, decimal maximum) => ChangeAsync(session, data =>
        data.Assessments.Add(new(Guid.NewGuid().ToString("N"), termId, subject.Trim(), title.Trim(), date, maximum)));

    public Task EnrolActivityAsync(UserSession session, string studentId, string activityId) => ChangeAsync(session, data =>
    {
        if (data.Memberships.Any(m => m.StudentId == studentId && m.ActivityId == activityId))
            throw new ArgumentException("This student is already enrolled in that activity.");
        data.Memberships.Add(new(studentId, activityId));
    });

    public Task AddSessionAsync(UserSession session, string activityId, DateOnly date, string description) => ChangeAsync(session, data =>
    {
        if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Enter a session description.");
        data.Sessions.Add(new(Guid.NewGuid().ToString("N"), activityId, date, description.Trim()));
    });

    public Task SaveParticipationAsync(UserSession session, string studentId, string sessionId, bool? attended) => ChangeAsync(session, data =>
    {
        data.Participation.RemoveAll(p => p.StudentId == studentId && p.SessionId == sessionId);
        if (attended.HasValue) data.Participation.Add(new(studentId, sessionId, attended.Value));
    });

    public Task AddAchievementAsync(UserSession session, string studentId, string activityId, DateOnly date, string title, string notes) => ChangeAsync(session, data =>
        data.Achievements.Add(new(Guid.NewGuid().ToString("N"), studentId, activityId, date, title.Trim(), notes.Trim())));

    public async Task ExportBackupAsync(UserSession session, string destination)
    {
        RequireSession(session);
        if (session.Role != UserRole.Teacher) throw new UnauthorizedAccessException("Only teachers can export the data file.");
        if (Path.GetFullPath(destination).Equals(Path.GetFullPath(DataPath), StringComparison.OrdinalIgnoreCase) ||
            Path.GetFullPath(destination).Equals(Path.GetFullPath(DataPath + ".bak"), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Choose a different location for the exported copy.");
        await _writeLock.WaitAsync();
        try { await new JsonSchoolStore(destination).SaveAsync(JsonSchoolStore.Clone(_data)); }
        finally { _writeLock.Release(); }
    }
}
