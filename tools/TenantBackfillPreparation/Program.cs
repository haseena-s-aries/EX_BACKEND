using Ex_SurveyApp_api.Services;

var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
for (var i = 0; i < args.Length; i++)
{
    if (args[i] == "--dry-run") { values["dry-run"] = "true"; continue; }
    if (!args[i].StartsWith("--", StringComparison.Ordinal) || i + 1 >= args.Length)
    {
        Console.Error.WriteLine("Usage: TenantBackfillPreparation --dry-run --input <snapshot.json> --report <report.json>");
        return 2;
    }
    values[args[i][2..]] = args[++i];
}

if (!values.ContainsKey("dry-run") || !values.TryGetValue("input", out var inputPath) || !values.TryGetValue("report", out var reportPath))
{
    Console.Error.WriteLine("Dry-run is mandatory. This utility does not support writes or migration application.");
    return 2;
}

try
{
    var input = await TenantOwnershipBackfillPlanner.ReadJsonAsync(inputPath);
    var plan = TenantOwnershipBackfillPlanner.Create(input);
    await TenantOwnershipBackfillPlanner.WriteReportAsync(plan, reportPath);
    Console.WriteLine($"Dry-run complete. Projects={plan.ProjectsRead}; Areas={plan.AreasRead}; Equipment={plan.EquipmentRead}; proposed={plan.ProposedBindings}; alreadyMapped={plan.AlreadyApplied}; unresolved={plan.Issues.Count}; canApply={plan.CanApply}.");
    Console.WriteLine($"Report: {Path.GetFullPath(reportPath)}");
    Console.WriteLine("No database connection or ownership writes were performed.");
    return plan.CanApply ? 0 : 1;
}
catch (Exception error) when (error is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException)
{
    Console.Error.WriteLine($"Dry-run failed: {error.GetType().Name}: {error.Message}");
    return 2;
}
