using System.Text.Json;
using System.Text.Json.Serialization;

namespace StudentRecordSystem.Core;

public sealed class DataFileException(string message, Exception? inner = null) : Exception(message, inner);

public sealed class JsonSchoolStore(string path)
{
    public string FilePath { get; } = Path.GetFullPath(path);
    public string BackupPath => FilePath + ".bak";
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static SchoolData Clone(SchoolData data) =>
        JsonSerializer.Deserialize<SchoolData>(JsonSerializer.Serialize(data, Options), Options)!;

    public async Task<SchoolData?> LoadAsync(CancellationToken cancellation = default)
    {
        if (!File.Exists(FilePath)) return null;
        try
        {
            await using var stream = File.OpenRead(FilePath);
            var data = await JsonSerializer.DeserializeAsync<SchoolData>(stream, Options, cancellation)
                ?? throw new InvalidDataException("The data file is empty.");
            DataValidation.Validate(data);
            return data;
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or ArgumentException
                                  or FormatException or InvalidOperationException or NullReferenceException)
        {
            throw new DataFileException("The student data file is invalid. It has not been overwritten. " +
                "Check the file or restore a known-good backup. Details: " + ex.Message, ex);
        }
    }

    public async Task SaveAsync(SchoolData data, CancellationToken cancellation = default)
    {
        DataValidation.Validate(data);
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            // Serialize completely before replacing the working file. Dispose closes the file handle.
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                             FileShare.None, 4096, FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(stream, data, Options, cancellation);
                await stream.FlushAsync(cancellation);
                stream.Flush(flushToDisk: true);
            }
            cancellation.ThrowIfCancellationRequested();
            if (File.Exists(FilePath)) File.Copy(FilePath, BackupPath, overwrite: true);
            File.Move(temporary, FilePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}

public static class DataValidation
{
    public static void Validate(SchoolData data)
    {
        Check(data.SchemaVersion == 1, "Unsupported data format version.");
        Unique(data.Students.Select(x => x.Id), "student ID");
        Unique(data.Terms.Select(x => x.Id), "term ID");
        Unique(data.Accounts.Select(x => x.Id), "account ID");
        Unique(data.Accounts.Select(x => x.Username.ToLowerInvariant()), "username");
        Unique(data.Assessments.Select(x => x.Id), "assessment ID");
        Unique(data.Activities.Select(x => x.Id), "activity ID");
        Unique(data.Sessions.Select(x => x.Id), "session ID");
        Unique(data.Achievements.Select(x => x.Id), "achievement ID");
        Unique(data.Marks.Select(x => x.StudentId + ":" + x.AssessmentId), "student assessment result");
        Unique(data.Attendance.Select(x => x.StudentId + ":" + x.Date), "student attendance date");
        Unique(data.Memberships.Select(x => x.StudentId + ":" + x.ActivityId), "membership");
        Unique(data.Participation.Select(x => x.StudentId + ":" + x.SessionId), "participation record");
        Unique(data.Sessions.Select(x => x.ActivityId + ":" + x.Date), "activity session date");
        Check(data.Terms.Count > 0 && data.Students.Count > 0, "At least one term and student are required.");
        foreach (var student in data.Students)
            Check(!string.IsNullOrWhiteSpace(student.Name) && !string.IsNullOrWhiteSpace(student.ClassName), "A student needs a name and class.");
        var sortedTerms = data.Terms.OrderBy(t => t.Start).ToList();
        for (int i = 1; i < sortedTerms.Count; i++)
            Check(sortedTerms[i].Start > sortedTerms[i - 1].End, "Term dates must not overlap.");
        foreach (var term in data.Terms)
        {
            Check(term.End >= term.Start && !string.IsNullOrWhiteSpace(term.Name), "Invalid term.");
            Check(term.SchoolDays.Distinct().Count() == term.SchoolDays.Count, "Duplicate school dates.");
            Check(term.SchoolDays.All(d => d >= term.Start && d <= term.End), "A school date is outside its term.");
        }
        var students = data.Students.Select(s => s.Id).ToHashSet();
        var activities = data.Activities.Select(a => a.Id).ToHashSet();
        var schoolDays = data.Terms.SelectMany(t => t.SchoolDays).ToHashSet();
        Check(data.Accounts.Any(a => a.Role == UserRole.Teacher), "A teacher account is required.");
        foreach (var account in data.Accounts)
        {
            Check(Enum.IsDefined(account.Role), "Invalid role.");
            Check(!string.IsNullOrWhiteSpace(account.Username), "Missing username.");
            Check(account.Role != UserRole.Student || students.Contains(account.StudentId ?? ""), "Invalid student account.");
            Check(account.Role != UserRole.Teacher || account.StudentId is null, "A teacher account cannot be linked to one student.");
            Check(Convert.FromBase64String(account.Salt).Length == 16 &&
                  Convert.FromBase64String(account.PasswordHash).Length == 32, "Invalid password encoding.");
        }
        foreach (var assessment in data.Assessments)
        {
            var term = data.Terms.SingleOrDefault(t => t.Id == assessment.TermId);
            Check(term is not null && assessment.Date >= term.Start && assessment.Date <= term.End, "Assessment date is outside its term.");
            Check(assessment.MaximumMark > 0 && assessment.MaximumMark <= 10000, "Maximum mark must be between 0 and 10,000 (exclusive of 0).");
            Check(!string.IsNullOrWhiteSpace(assessment.Subject) && !string.IsNullOrWhiteSpace(assessment.Title), "Assessment title and subject are required.");
        }
        foreach (var mark in data.Marks)
        {
            var assessment = data.Assessments.SingleOrDefault(a => a.Id == mark.AssessmentId);
            Check(students.Contains(mark.StudentId) && assessment is not null, "Result has an unknown student or assessment.");
            Check(mark.Mark is null || mark.Mark >= 0 && mark.Mark <= assessment!.MaximumMark, "Mark is outside the assessment range.");
        }
        foreach (var attendance in data.Attendance)
            Check(students.Contains(attendance.StudentId) && schoolDays.Contains(attendance.Date) && Enum.IsDefined(attendance.Status), "Invalid attendance record or non-school date.");
        foreach (var activity in data.Activities)
            Check(!string.IsNullOrWhiteSpace(activity.Name) && Enum.IsDefined(activity.Kind), "Invalid activity.");
        foreach (var member in data.Memberships)
            Check(students.Contains(member.StudentId) && activities.Contains(member.ActivityId), "Invalid activity membership.");
        foreach (var session in data.Sessions)
            Check(activities.Contains(session.ActivityId) && data.Terms.Any(t => session.Date >= t.Start && session.Date <= t.End), "Invalid activity session.");
        foreach (var participation in data.Participation)
        {
            var session = data.Sessions.SingleOrDefault(s => s.Id == participation.SessionId);
            Check(session is not null && data.Memberships.Any(m => m.StudentId == participation.StudentId && m.ActivityId == session.ActivityId), "Participation requires an existing session and membership.");
        }
        foreach (var award in data.Achievements)
            Check(students.Contains(award.StudentId) && activities.Contains(award.ActivityId) &&
                  data.Memberships.Any(m => m.StudentId == award.StudentId && m.ActivityId == award.ActivityId) &&
                  data.Terms.Any(t => award.Date >= t.Start && award.Date <= t.End) &&
                  !string.IsNullOrWhiteSpace(award.Title), "Invalid achievement.");
    }

    private static void Unique(IEnumerable<string> values, string label)
    {
        var list = values.ToList();
        Check(list.All(x => !string.IsNullOrWhiteSpace(x)) && list.Distinct().Count() == list.Count, "Missing or duplicate " + label + ".");
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
