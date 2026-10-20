using System.Text.Json;
using StudentRecordSystem.Core;

if (args.Length == 2 && args[0] == "--export-demo")
{
    await new JsonSchoolStore(args[1]).SaveAsync(DemoData.Create());
    Console.WriteLine("Fictional sample data exported.");
    return;
}

int passed = 0, failed = 0;
async Task Check(string name, Func<Task> test)
{
    try { await test(); passed++; Console.WriteLine("PASS  " + name); }
    catch (Exception ex) { failed++; Console.WriteLine("FAIL  " + name + ": " + ex.Message); }
}
void Assert(bool condition, string message = "Unexpected result")
{
    if (!condition) throw new Exception(message);
}
void Near(double? value, double expected)
{
    Assert(value.HasValue && Math.Abs(value.Value - expected) < 0.001, $"Expected {expected}, got {value}");
}
async Task Throws<T>(Func<Task> action) where T : Exception
{
    try { await action(); }
    catch (T) { return; }
    throw new Exception("Expected " + typeof(T).Name);
}

string folder = Path.Combine(Path.GetTempPath(), "StudentRecordChecks-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(folder);
var demo = DemoData.Create();
var term = new SchoolTerm("test", "Test term", new(2026, 10, 1), new(2026, 10, 12),
    [new(2026, 10, 1), new(2026, 10, 2), new(2026, 10, 5), new(2026, 10, 6), new(2026, 10, 7), new(2026, 10, 8), new(2026, 10, 9), new(2026, 10, 12)]);
SchoolData Fixture()
{
    var data = JsonSchoolStore.Clone(demo);
    data.Terms = [term]; data.Assessments.Clear(); data.Marks.Clear(); data.Attendance.Clear();
    data.Sessions.Clear(); data.Participation.Clear(); data.Achievements.Clear();
    return data;
}
var teacher = Authentication.Login(demo, "teacher", DemoData.Password)!;
var student = Authentication.Login(demo, "student1", DemoData.Password)!;
SchoolService Service(SchoolData data, string name)
{
    var service = new SchoolService(data, new JsonSchoolStore(Path.Combine(folder, name + ".json")));
    teacher = service.Login("teacher", DemoData.Password)!;
    student = service.Login("student1", DemoData.Password)!;
    return service;
}

try
{
    await Check("Sample data has valid relationships and two terms", () =>
    { DataValidation.Validate(demo); Assert(demo.Terms.Count == 2 && demo.Students.Count == 3); return Task.CompletedTask; });
    await Check("Integer-division experiment exposes the error and its correction", () =>
    { var result = LanguageExperiments.IntegerDivision(); Assert(result.Problem.EndsWith("= 0%") && result.Improved.EndsWith("= 86%")); return Task.CompletedTask; });
    await Check("Deferred-query experiment distinguishes a query from a snapshot", () =>
    { var result = LanguageExperiments.DeferredQuery(); Assert(result.Problem.EndsWith("88, 95") && result.Improved.EndsWith("88")); return Task.CompletedTask; });
    await Check("Correct credentials produce the linked student session", () =>
    { Assert(student.Role == UserRole.Student && student.StudentId == "S001"); return Task.CompletedTask; });
    await Check("Wrong password and unknown account fail", () =>
    { Assert(Authentication.Login(demo, "teacher", "wrong") is null && Authentication.Login(demo, "unknown", "wrong") is null); return Task.CompletedTask; });
    await Check("Login trims username and ignores username case", () =>
    { Assert(Authentication.Login(demo, " TEACHER ", DemoData.Password)?.Role == UserRole.Teacher); return Task.CompletedTask; });
    await Check("Passwords are salted hashes, not plaintext records", () =>
    { Assert(!JsonSerializer.Serialize(demo.Accounts).Contains(DemoData.Password)); Assert(demo.Accounts.Select(a => a.PasswordHash).Distinct().Count() == demo.Accounts.Count); return Task.CompletedTask; });
    await Check("Student snapshots exclude other student records and accounts", () =>
    { var copy = Service(demo, "scope").Snapshot(student); Assert(copy.Students.Count == 1 && copy.Marks.All(m => m.StudentId == "S001") && copy.Accounts.Count == 0); return Task.CompletedTask; });
    await Check("Snapshot mutation cannot change live records", () =>
    { var service = Service(demo, "copy"); var copy = service.Snapshot(teacher); copy.Students.Clear(); Assert(service.Snapshot(teacher).Students.Count == 3); return Task.CompletedTask; });
    await Check("A forged teacher session is rejected", async () =>
    { var service = Service(demo, "forged"); await Throws<UnauthorizedAccessException>(() => service.SaveMarkAsync(student with { Role = UserRole.Teacher }, "S001", demo.Assessments[0].Id, 40)); });
    await Check("Known teacher details without a registered session are rejected", async () =>
    { var service = Service(demo, "unregistered"); var guessed = new UserSession("teacher", "Ms. Jayasinghe", UserRole.Teacher, null); await Throws<UnauthorizedAccessException>(() => service.SaveMarkAsync(guessed, "S001", demo.Assessments[0].Id, 0)); });
    await Check("Signing out invalidates the session", async () =>
    { var service = Service(demo, "logout"); service.Logout(teacher); await Throws<UnauthorizedAccessException>(() => service.SaveMarkAsync(teacher, "S001", demo.Assessments[0].Id, 0)); });
    await Check("Student writes are denied at the service boundary", async () =>
    { await Throws<UnauthorizedAccessException>(() => Service(demo, "deny").SaveMarkAsync(student, "S001", demo.Assessments[0].Id, 40)); });
    await Check("Unequal assessment maxima are normalized before averaging", () =>
    {
        var data = Fixture();
        data.Assessments = [new("a", term.Id, "Maths", "One", term.Start, 50), new("b", term.Id, "Maths", "Two", term.Start, 100)];
        data.Marks = [new("S001", "a", 40), new("S001", "b", 60)];
        Near(Analytics.AcademicAverage(data, "S001", term, term.End), 70); return Task.CompletedTask;
    });
    await Check("Academic average weights subjects equally", () =>
    {
        var data = Fixture();
        data.Assessments = [new("a", term.Id, "Maths", "One", term.Start, 100), new("b", term.Id, "Maths", "Two", term.Start, 100), new("c", term.Id, "English", "One", term.Start, 100)];
        data.Marks = [new("S001", "a", 100), new("S001", "b", 100), new("S001", "c", 0)];
        Near(Analytics.AcademicAverage(data, "S001", term, term.End), 50); return Task.CompletedTask;
    });
    await Check("Zero is included; unmarked results are excluded and counted", () =>
    {
        var data = Fixture(); data.Assessments = [new("a", term.Id, "Maths", "One", term.Start, 100), new("b", term.Id, "Maths", "Two", term.Start, 100)];
        data.Marks = [new("S001", "a", 0)];
        var result = Analytics.Subjects(data, "S001", term, term.End).Single(); Near(result.Average, 0); Assert(result.Marked == 1 && result.Assessments == 2); return Task.CompletedTask;
    });
    await Check("Empty analytics return no percentage rather than zero", () =>
    { var data = Fixture(); Assert(Analytics.AcademicAverage(data, "S001", term, term.End) is null && Analytics.Attendance(data, "S001", term, term.End).Percentage is null); return Task.CompletedTask; });
    await Check("As-of filtering excludes later marks", () =>
    {
        var data = Fixture(); data.Assessments = [new("a", term.Id, "Maths", "One", term.Start, 100), new("b", term.Id, "Maths", "Two", term.End, 100)];
        data.Marks = [new("S001", "a", 40), new("S001", "b", 100)]; Near(Analytics.AcademicAverage(data, "S001", term, term.Start), 40); return Task.CompletedTask;
    });
    await Check("Unmarked chart points remain gaps", () =>
    { var data = Fixture(); data.Assessments = [new("a", term.Id, "Maths", "One", term.Start, 100)]; Assert(Analytics.AcademicTrend(data, "S001", term, term.End).Single().Value is null); return Task.CompletedTask; });
    await Check("Attendance excludes missing records but reports coverage", () =>
    {
        var data = Fixture(); data.Attendance = [new("S001", term.SchoolDays[0], AttendanceStatus.Present), new("S001", term.SchoolDays[1], AttendanceStatus.Absent), new("S001", term.SchoolDays[2], AttendanceStatus.Excused)];
        var result = Analytics.Attendance(data, "S001", term, term.SchoolDays[3]); Near(result.Percentage, 100.0 / 3); Assert(result.Missing == 1 && result.Excused == 1); return Task.CompletedTask;
    });
    await Check("A weekend does not break an attendance streak", () =>
    {
        var data = Fixture(); data.Attendance = term.SchoolDays.Take(3).Select(d => new AttendanceRecord("S001", d, AttendanceStatus.Present)).ToList();
        var result = Analytics.Attendance(data, "S001", term, term.SchoolDays[2]); Assert(result.CurrentStreak == 3 && result.LongestStreak == 3); return Task.CompletedTask;
    });
    await Check("Missing latest school day interrupts a verified streak", () =>
    { var data = Fixture(); data.Attendance = [new("S001", term.Start, AttendanceStatus.Present)]; Assert(Analytics.Attendance(data, "S001", term, term.SchoolDays[1]).CurrentStreak == 0); return Task.CompletedTask; });
    await Check("Holiday is excluded from the sample school calendar", () =>
    { Assert(!demo.Terms.SelectMany(t => t.SchoolDays).Contains(new DateOnly(2026, 9, 23))); return Task.CompletedTask; });
    await Check("Attendance does not count future scheduled days as missing", () =>
    { Assert(Analytics.Attendance(Fixture(), "S001", term, term.Start).Missing == 1); return Task.CompletedTask; });
    await Check("Activity coverage distinguishes missed from unrecorded sessions", () =>
    {
        var data = Fixture(); data.Sessions = [new("a", "cricket", term.Start, "Practice"), new("b", "cricket", term.Start.AddDays(1), "Practice")];
        data.Participation = [new("S001", "a", true)];
        var result = Analytics.Activities(data, "S001", term, term.End, ActivityKind.Sport).Single(); Near(result.Percentage, 100); Assert(result.Scheduled == 2 && result.Recorded == 1); return Task.CompletedTask;
    });
    await Check("Non-member has no participation percentage", () =>
    { Assert(Analytics.WeeklyParticipation(demo, "S001", demo.Terms[1], demo.SuggestedAsOf, "swimming").Count == 0); return Task.CompletedTask; });
    await Check("Invalid mark is rejected without changing memory or disk", async () =>
    {
        var service = Service(demo, "range"); var a = demo.Assessments.First();
        var before = service.Snapshot(teacher).Marks.First(m => m.StudentId == "S001" && m.AssessmentId == a.Id).Mark;
        await Throws<InvalidDataException>(() => service.SaveMarkAsync(teacher, "S001", a.Id, a.MaximumMark + 1));
        Assert(service.Snapshot(teacher).Marks.First(m => m.StudentId == "S001" && m.AssessmentId == a.Id).Mark == before);
        Assert(!File.Exists(Path.Combine(folder, "range.json")));
    });
    await Check("Negative marks are rejected", async () =>
    { await Throws<InvalidDataException>(() => Service(demo, "negative").SaveMarkAsync(teacher, "S001", demo.Assessments[0].Id, -1)); });
    await Check("Teacher can replace a mark and clear it back to unmarked", async () =>
    {
        var service = Service(demo, "markedit"); string id = demo.Assessments[0].Id;
        await service.SaveMarkAsync(teacher, "S001", id, 0);
        Assert(service.Snapshot(teacher).Marks.Single(m => m.StudentId == "S001" && m.AssessmentId == id).Mark == 0);
        await service.SaveMarkAsync(teacher, "S001", id, null);
        Assert(!service.Snapshot(teacher).Marks.Any(m => m.StudentId == "S001" && m.AssessmentId == id));
    });
    await Check("Attendance on a non-school date is rejected", async () =>
    { await Throws<InvalidDataException>(() => Service(demo, "weekend").SaveAttendanceAsync(teacher, "S001", new(2026, 10, 17), AttendanceStatus.Present)); });
    await Check("Participation requires activity membership", async () =>
    { var session = demo.Sessions.First(s => s.ActivityId == "swimming"); await Throws<InvalidDataException>(() => Service(demo, "member").SaveParticipationAsync(teacher, "S001", session.Id, true)); });
    await Check("Duplicate session dates are rejected", async () =>
    { var session = demo.Sessions.First(); await Throws<InvalidDataException>(() => Service(demo, "duplicate").AddSessionAsync(teacher, session.ActivityId, session.Date, "Duplicate")); });
    await Check("An assessment outside its term is rejected", async () =>
    { await Throws<InvalidDataException>(() => Service(demo, "term").AddAssessmentAsync(teacher, "T3", "Maths", "Bad date", new(2025, 1, 1), 100)); });
    await Check("Successful saves round-trip dates, enums and data", async () =>
    { var store = new JsonSchoolStore(Path.Combine(folder, "roundtrip.json")); await store.SaveAsync(demo); var loaded = await store.LoadAsync(); Assert(loaded!.Attendance.SequenceEqual(demo.Attendance) && loaded.Assessments.SequenceEqual(demo.Assessments)); });
    await Check("Second save retains the previous version as backup", async () =>
    {
        var store = new JsonSchoolStore(Path.Combine(folder, "backup.json")); await store.SaveAsync(demo);
        var changed = JsonSchoolStore.Clone(demo); changed.SuggestedAsOf = new(2026, 10, 15); await store.SaveAsync(changed);
        Assert((await new JsonSchoolStore(store.BackupPath).LoadAsync())!.SuggestedAsOf == demo.SuggestedAsOf);
    });
    await Check("Corrupt JSON produces a clear error and is not replaced", async () =>
    { string path = Path.Combine(folder, "broken.json"); await File.WriteAllTextAsync(path, "{broken"); await Throws<DataFileException>(async () => { await new JsonSchoolStore(path).LoadAsync(); }); Assert(await File.ReadAllTextAsync(path) == "{broken"); });
    await Check("Valid JSON with invalid references is rejected", async () =>
    { var bad = JsonSchoolStore.Clone(demo); bad.Marks.Add(new("unknown", "unknown", 20)); string path = Path.Combine(folder, "badrefs.json"); await File.WriteAllTextAsync(path, JsonSerializer.Serialize(bad, JsonSchoolStore.Options)); await Throws<DataFileException>(async () => { await new JsonSchoolStore(path).LoadAsync(); }); });
    await Check("A failed disk save does not alter live records", async () =>
    {
        var store = new JsonSchoolStore(Path.Combine(folder, "blocked", "data.json")); await File.WriteAllTextAsync(Path.Combine(folder, "blocked"), "a file occupies the directory");
        var service = new SchoolService(demo, store); teacher = service.Login("teacher", DemoData.Password)!; var a = demo.Assessments[0];
        decimal? before = service.Snapshot(teacher).Marks.First(m => m.StudentId == "S001" && m.AssessmentId == a.Id).Mark;
        await Throws<IOException>(() => service.SaveMarkAsync(teacher, "S001", a.Id, 0));
        Assert(service.Snapshot(teacher).Marks.First(m => m.StudentId == "S001" && m.AssessmentId == a.Id).Mark == before);
    });
    await Check("Students cannot export all records", async () =>
    { await Throws<UnauthorizedAccessException>(() => Service(demo, "export").ExportBackupAsync(student, Path.Combine(folder, "exported.json"))); });
    await Check("Record change event fires after a successful save", async () =>
    { var service = Service(demo, "event"); int events = 0; service.RecordsChanged += (_, _) => events++; await service.SaveMarkAsync(teacher, "S001", demo.Assessments[0].Id, 0); Assert(events == 1); });
    await Check("Concurrent saves retain both students' updates", async () =>
    {
        var service = Service(demo, "concurrent"); string id = demo.Assessments[0].Id;
        await Task.WhenAll(service.SaveMarkAsync(teacher, "S001", id, 11), service.SaveMarkAsync(teacher, "S002", id, 22));
        var copy = service.Snapshot(teacher); Assert(copy.Marks.Single(m => m.StudentId == "S001" && m.AssessmentId == id).Mark == 11 && copy.Marks.Single(m => m.StudentId == "S002" && m.AssessmentId == id).Mark == 22);
    });
}
finally { Directory.Delete(folder, recursive: true); }
Console.WriteLine($"\n{passed} passed; {failed} failed.");
Environment.ExitCode = failed == 0 ? 0 : 1;
