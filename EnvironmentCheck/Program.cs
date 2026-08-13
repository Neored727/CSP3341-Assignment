
//  Author: <Neo Red>   Student ID: <10695907>
// =====================================================================
//  CSP3341 - Programming Languages and Paradigms
//  Assignment 1, Part B: Programming Environment Verification
//
//  Purpose: demonstrate that the C# 14 / .NET 10 toolchain is correctly
//  installed and configured, and illustrate several language features
//  discussed in the accompanying report.
//

//  Built with: .NET 10 SDK, C# 14, Visual Studio 2026 Community
// =====================================================================

using System.Runtime.InteropServices;
using System.Text.Json;

// ---------------------------------------------------------------------
// 1. ENVIRONMENT REPORT
//    Proves at runtime which framework and OS the code is executing on.
//    This output is what makes the console screenshot self-documenting.
// ---------------------------------------------------------------------
Console.WriteLine("========================================================");
Console.WriteLine("  CSP3341 Environment Verification");
Console.WriteLine("========================================================");
Console.WriteLine($"  Framework      : {RuntimeInformation.FrameworkDescription}");
Console.WriteLine($"  CLR version    : {Environment.Version}");
Console.WriteLine($"  OS             : {RuntimeInformation.OSDescription}");
Console.WriteLine($"  Architecture   : {RuntimeInformation.OSArchitecture}");
Console.WriteLine($"  Process arch   : {RuntimeInformation.ProcessArchitecture}");
Console.WriteLine($"  Machine name   : {Environment.MachineName}");
Console.WriteLine($"  Timestamp      : {DateTime.Now:dddd, d MMMM yyyy HH:mm:ss}");
Console.WriteLine();

// ---------------------------------------------------------------------
// 2. C# 14 FEATURE: the 'field' keyword
//    A property accessor can now refer to its compiler-generated backing
//    field directly as 'field', removing the need to declare one by hand.
//    This WILL NOT COMPILE on a pre-C#-14 compiler, so a successful build
//    is itself proof of the language version.
// ---------------------------------------------------------------------
Console.WriteLine("--- C# 14: 'field' keyword ---------------------------");

var unit = new Unit { Code = "  csp3341  ", Title = "Programming Languages and Paradigms" };
Console.WriteLine($"  Raw input was '  csp3341  ' -> stored as '{unit.Code}'");
Console.WriteLine($"  Title          : {unit.Title}");

// C# 14: null-conditional assignment. The right-hand side is only
// evaluated, and the assignment only performed, if 'maybeUnit' is non-null.
Unit? maybeUnit = null;
maybeUnit?.Title = "This assignment never happens - no exception thrown.";
Console.WriteLine($"  Null-conditional assignment on a null reference: no exception.");
Console.WriteLine();

// ---------------------------------------------------------------------
// 3. VALUE vs REFERENCE SEMANTICS  (Module 6 - Data Types)
//    A struct is copied on assignment; a class is aliased. This is the
//    single most important semantic distinction in C#'s type system and
//    is worth reusing in the Syntax & Semantics section of Part A.
// ---------------------------------------------------------------------
Console.WriteLine("--- Value vs reference semantics ---------------------");

var markA = new MarkStruct(75);          // struct  -> value type
var markB = markA;                        // COPY
markB.Score = 40;

var recordA = new MarkClass(75);          // class   -> reference type
var recordB = recordA;                    // ALIAS
recordB.Score = 40;

Console.WriteLine($"  struct : original = {markA.Score}, copy modified to {markB.Score}  (independent)");
Console.WriteLine($"  class  : original = {recordA.Score}, alias modified to {recordB.Score}  (shared)");
Console.WriteLine();

// ---------------------------------------------------------------------
// 4. DATA FILE INTEGRATION
//    Serialises objects to JSON, writes to disk, reads back and
//    deserialises. Demonstrates the data-file integration the assignment
//    brief lists as a required feature, and proves the BCL is available.
// ---------------------------------------------------------------------
Console.WriteLine("--- Data file integration (System.Text.Json) ---------");

List<Student> students =
[
    new("S001", "A. Student",  82.5),
    new("S002", "B. Learner",  67.0),
    new("S003", "C. Scholar",  91.25)
];

string path = Path.Combine(AppContext.BaseDirectory, "students.json");
var options = new JsonSerializerOptions { WriteIndented = true };

File.WriteAllText(path, JsonSerializer.Serialize(students, options));
Console.WriteLine($"  Wrote {students.Count} records to:");
Console.WriteLine($"    {path}");

var loaded = JsonSerializer.Deserialize<List<Student>>(File.ReadAllText(path)) ?? [];
Console.WriteLine($"  Read back {loaded.Count} records.");
Console.WriteLine();

// ---------------------------------------------------------------------
// 5. LINQ  (Module 12 - functional features in an imperative language)
//    Declarative querying over in-memory collections.
// ---------------------------------------------------------------------
Console.WriteLine("--- LINQ query ---------------------------------------");

var distinctions = loaded
    .Where(s => s.Wam >= 80)
    .OrderByDescending(s => s.Wam)
    .Select(s => $"{s.Name} ({s.Wam:F2})");

Console.WriteLine($"  Students with WAM >= 80: {string.Join(", ", distinctions)}");
Console.WriteLine($"  Cohort average WAM     : {loaded.Average(s => s.Wam):F2}");
Console.WriteLine();

// ---------------------------------------------------------------------
// 6. EXCEPTION HANDLING  (Module 11)
//    Demonstrates a deliberately triggered, caught exception. The 'when'
//    clause is an exception filter - a C# feature not present in Java.
// ---------------------------------------------------------------------
Console.WriteLine("--- Exception handling with a filter -----------------");

try
{
    // The Code setter rejects blank input by throwing.
    _ = new Unit { Code = "   ", Title = "Invalid" };
}
catch (ArgumentException ex) when (ex.ParamName == "value")
{
    Console.WriteLine($"  Caught by filter -> {ex.Message}");
}

Console.WriteLine();
Console.WriteLine("========================================================");
Console.WriteLine("  All checks passed. Environment is correctly configured.");
Console.WriteLine("========================================================");


// =====================================================================
//  TYPE DECLARATIONS
// =====================================================================

/// <summary>
/// Demonstrates the C# 14 'field' keyword: the accessor body refers to the
/// compiler-generated backing field without the developer declaring one.
/// </summary>
public class Unit
{
    public string Code
    {
        get => field;
        set => field = string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Unit code cannot be blank.", nameof(value))
            : value.Trim().ToUpperInvariant();
    } = string.Empty;

    public string Title
    {
        get => field;
        set => field = value?.Trim() ?? string.Empty;
    } = string.Empty;
}

/// <summary>Value type - copied on assignment.</summary>
public struct MarkStruct(int score)
{
    public int Score { get; set; } = score;
}

/// <summary>Reference type - aliased on assignment.</summary>
public class MarkClass(int score)
{
    public int Score { get; set; } = score;
}

/// <summary>
/// A positional record: immutable by default, with value-based equality
/// and a compiler-generated ToString. Contrast with the class above.
/// </summary>
public record Student(string Id, string Name, double Wam);
