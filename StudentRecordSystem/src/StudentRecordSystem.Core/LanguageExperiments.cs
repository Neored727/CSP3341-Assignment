namespace StudentRecordSystem.Core;

public sealed record ExperimentResult(string Title, string Problem, string Improved, string Explanation);

// Controlled examples for Part B: these do not modify real student records.
public static class LanguageExperiments
{
    public static ExperimentResult IntegerDivision()
    {
        int earned = 43, maximum = 50;
        int truncated = earned / maximum * 100;
        decimal percentage = (decimal)earned / maximum * 100m;
        return new("Integer division in percentage calculations",
            $"int / int: 43 / 50 * 100 = {truncated}%",
            $"decimal / int: (decimal)43 / 50 * 100m = {percentage:0.#}%",
            "C# chooses integer division when both operands are integers, discarding the fractional part. " +
            "The resulting value has a valid type but the wrong meaning for this calculation. " +
            "The application uses decimal or floating-point division before multiplying by 100.");
    }

    public static ExperimentResult DeferredQuery()
    {
        var marks = new List<int> { 45, 72, 88 };
        var deferred = marks.Where(mark => mark >= 80);
        var snapshot = marks.Where(mark => mark >= 80).ToList();
        marks.Add(95); // The source changes after the query and snapshot were created.
        return new("Deferred LINQ query versus a snapshot",
            $"Deferred query after adding 95: {string.Join(", ", deferred)}",
            $"Earlier ToList() snapshot: {string.Join(", ", snapshot)}",
            "Where returns a query that is evaluated when enumerated. If code expects a fixed report, later changes " +
            "to its source may produce surprising results. ToList materializes a snapshot, with an allocation cost. " +
            "Deferred execution is useful for composing queries; this is a .NET LINQ trade-off, not a defect in every C# query.");
    }
}
