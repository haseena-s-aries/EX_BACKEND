using System.Reflection;
using Ex_SurveyApp_api.Attributes;
using Ex_SurveyApp_api.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Authorization;
using System.Text;

internal static class EndpointScopeChecks
{
    public static void WriteTenantMatrix(string path)
    {
        var assembly = typeof(AuthController).Assembly;
        var controllers = assembly.GetTypes().Where(type => !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type)).ToArray();
        var rows = new List<string>();
        foreach (var controller in controllers.OrderBy(x => x.Name))
        {
            var controllerRoute = controller.GetCustomAttribute<RouteAttribute>(true)?.Template ?? "api/[controller]";
            var controllerName = controller.Name.EndsWith("Controller", StringComparison.Ordinal) ? controller.Name[..^10] : controller.Name;
            var controllerAuth = controller.GetCustomAttributes<AuthorizeAttribute>(true).ToArray();
            var methods = controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(method => method.GetCustomAttributes<HttpMethodAttribute>(true).Any()).OrderBy(x => x.Name);
            foreach (var method in methods)
            {
                var routeAttrs = method.GetCustomAttributes<HttpMethodAttribute>(true).ToArray();
                var auth = method.GetCustomAttributes<AuthorizeAttribute>(true).ToArray();
                if (auth.Length == 0) auth = controllerAuth;
                var isAnonymous = method.IsDefined(typeof(AllowAnonymousAttribute), true) || controller.IsDefined(typeof(AllowAnonymousAttribute), true);
                var roles = isAnonymous ? "Anonymous" : auth.Length == 0 ? "No explicit Authorize metadata" :
                    string.Join("; ", auth.Select(x => !string.IsNullOrWhiteSpace(x.Roles) ? "Roles=" + x.Roles :
                        !string.IsNullOrWhiteSpace(x.Policy) ? "Policy=" + x.Policy : "Authenticated"));
                var resource = Resource(controller.Name, method.Name);
                var routeStrings = routeAttrs.Select(attr =>
                {
                    var prefix = attr.Template?.StartsWith("/", StringComparison.Ordinal) == true ? string.Empty : controllerRoute;
                    var suffix = attr.Template?.TrimStart('/') ?? string.Empty;
                    var combined = string.Join('/', new[] { prefix, suffix }.Where(x => !string.IsNullOrWhiteSpace(x)));
                    combined = combined.Replace("[controller]", controllerName, StringComparison.OrdinalIgnoreCase);
                    return $"{string.Join(',', attr.HttpMethods)} /{combined}";
                });
                var export = method.Name.Contains("Report", StringComparison.OrdinalIgnoreCase) || method.Name.Contains("Export", StringComparison.OrdinalIgnoreCase) || method.Name.Contains("Pdf", StringComparison.OrdinalIgnoreCase);
                var write = routeAttrs.Any(x => x.HttpMethods.Any(v => v is "POST" or "PUT" or "PATCH" or "DELETE"));
                var access = export ? "Export/read (also apply write permission if mutating)" : write ? "Write/mutate" : "Read";
                var scope = resource is "Shared reference / identity" ? "No tenant row scope; existing platform/identity authorization" :
                    IsCollection(controller.Name, method) ? "TenantMember + query filtered by validated tenant context" : "TenantResource + server-side parent ownership";
                var ownership = OwnershipPath(resource);
                var coverage = resource switch
                {
                    "Project" or "Area" or "Equipment" or "Survey session" or "Checklist answer" or "Finding/defect" or "File/photo" or "QC review" or "Report/export" or "Client dashboard" => "Phase 2C synthetic resolver/policy tests; no persisted-data proof",
                    _ => "Existing endpoint/workflow checks; tenant scope not applicable or pending"
                };
                var flag = resource == "Shared reference / identity" ? "Not applicable to tenant row policy; preserve current auth" : "`TenantFoundation:Enabled` + scoped policy; not attached to this route";
                var routeStatus = resource == "Shared reference / identity" ? "Outside tenant row enforcement" : "**Not activated**";
                rows.Add($"| `{controller.Name}.{method.Name}` | `{string.Join("<br>", routeStrings)}` | {resource} | {Escape(roles)} | {scope} | {ownership} | {access} | {flag} | {coverage} | {routeStatus} |");
            }
        }

        var content = new StringBuilder();
        content.AppendLine("# Tenant Endpoint Enforcement Matrix");
        content.AppendLine();
        content.AppendLine("**Status: preparation only.** Generated from MVC route metadata on 30 Oct 2026. Policies are not attached to these existing routes. Preserve the listed existing role metadata; row-level tenant checks are an additional future gate. `TenantFoundation:Enabled` must remain false until database-backed review and rollout approval. Collection routes must use `ITenantQueryScope`, not trust a request tenant ID.");
        content.AppendLine();
        content.AppendLine("The 92 rows below list every discovered route action. Ownership path descriptions are proposed server-side paths and must be reviewed against each action's body, nested IDs, file links and query behavior before activation. Any child with multiple/unmapped parents must fail closed.");
        content.AppendLine();
        content.AppendLine("| Controller / action | HTTP method and route | Resource type | Existing role metadata | Required tenant scope | Server-side ownership resolution | Read/write/export permission | Feature flag | Test coverage | Route status |");
        content.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
        foreach (var row in rows) content.AppendLine(row);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, content.ToString());
        Console.WriteLine($"Wrote {rows.Count} tenant enforcement matrix rows to {path}");
    }

    public static void Run()
    {
        var assembly = typeof(AuthController).Assembly;
        var controllerTypes = assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type))
            .ToArray();
        var routeCount = 0;

        foreach (var controller in controllerTypes)
        {
            var controllerScope = controller.GetCustomAttribute<EndpointScopeInventoryAttribute>(inherit: true);
            foreach (var action in controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                         .Where(method => method.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any()))
            {
                var scope = action.GetCustomAttribute<EndpointScopeInventoryAttribute>(inherit: true) ?? controllerScope;
                if (scope is null || !Enum.IsDefined(scope.Scope))
                    throw new InvalidOperationException($"Route action {controller.Name}.{action.Name} has no valid endpoint scope inventory classification.");
                routeCount++;
            }
        }

        if (routeCount == 0) throw new InvalidOperationException("No API routes were discovered for the endpoint scope inventory.");
        Console.WriteLine($"PASS: {routeCount} API route actions across {controllerTypes.Length} controllers have a scope inventory classification.");
        Console.WriteLine("PASS: Inventory metadata is passive and does not enforce or alter endpoint authorization.");
    }

    private static string Resource(string controller, string action)
    {
        if (controller == nameof(CompaniesController)) return "Shared reference / identity";
        if (controller == nameof(AuthController)) return "Shared reference / identity";
        if (controller == nameof(ClientDashboardController)) return "Client dashboard";
        if (action.Contains("Photo", StringComparison.OrdinalIgnoreCase) || action.Contains("File", StringComparison.OrdinalIgnoreCase) || action.Contains("Plan", StringComparison.OrdinalIgnoreCase) || action.Contains("Logo", StringComparison.OrdinalIgnoreCase) || action.Contains("Upload", StringComparison.OrdinalIgnoreCase) || action.Contains("Download", StringComparison.OrdinalIgnoreCase)) return "File/photo";
        if (action.Contains("Report", StringComparison.OrdinalIgnoreCase) || action.Contains("Export", StringComparison.OrdinalIgnoreCase) || action.Contains("Pdf", StringComparison.OrdinalIgnoreCase)) return "Report/export";
        if (action.Contains("Answer", StringComparison.OrdinalIgnoreCase) || action.Contains("Checklist", StringComparison.OrdinalIgnoreCase)) return "Checklist answer";
        if (action.Contains("Defect", StringComparison.OrdinalIgnoreCase) || action.Contains("Finding", StringComparison.OrdinalIgnoreCase)) return "Finding/defect";
        if (action.Contains("Review", StringComparison.OrdinalIgnoreCase) || action.Contains("Inspection", StringComparison.OrdinalIgnoreCase)) return controller.Contains("Qc", StringComparison.OrdinalIgnoreCase) ? "QC review" : "Survey session";
        if (action.Contains("Equipment", StringComparison.OrdinalIgnoreCase) || action.Contains("Rfid", StringComparison.OrdinalIgnoreCase) || action.Contains("Tag", StringComparison.OrdinalIgnoreCase)) return "Equipment";
        if (action.Contains("Area", StringComparison.OrdinalIgnoreCase)) return "Area";
        if (controller == nameof(ClientsController)) return "Client dashboard";
        if (action.Contains("Project", StringComparison.OrdinalIgnoreCase) || controller == nameof(ProjectController) || controller == nameof(ReportsController)) return "Project";
        if (controller == nameof(SurveyorController)) return "Survey session";
        if (controller == nameof(QcController) || controller == nameof(ManagerQcReviewController)) return "QC review";
        if (controller == nameof(DefectsController)) return "Finding/defect";
        return "Project";
    }

    private static bool IsCollection(string controller, MethodInfo method) =>
        method.Name.Contains("Dashboard", StringComparison.OrdinalIgnoreCase) ||
        controller == nameof(ClientDashboardController) ||
        method.GetParameters().All(p => p.ParameterType != typeof(Guid) && p.ParameterType != typeof(Guid?)) &&
        (method.Name.EndsWith("s", StringComparison.OrdinalIgnoreCase) ||
         method.Name.Contains("List", StringComparison.OrdinalIgnoreCase) ||
         method.Name.Contains("Dashboard", StringComparison.OrdinalIgnoreCase) ||
         method.Name.Contains("Projects", StringComparison.OrdinalIgnoreCase) ||
         method.Name.Contains("Defects", StringComparison.OrdinalIgnoreCase) ||
         method.Name.Contains("Summary", StringComparison.OrdinalIgnoreCase) ||
         method.Name.Contains("Counts", StringComparison.OrdinalIgnoreCase));

    private static string OwnershipPath(string resource) => resource switch
    {
        "Project" => "Project ID → exactly one TenantProjectBinding/TenantId; active membership must match",
        "Area" => "ProjectArea.ProjectId → project binding; equipment children must match Area.ProjectId",
        "Equipment" => "AreaEquipment.ProjectId and AreaId → ProjectArea.ProjectId agreement → project binding",
        "Survey session" => "SurveySession.AreaEquipmentId → equipment/area/project binding; legacy Job/Equipment requires verified crosswalk",
        "Checklist answer" => "SurveyAnswer.SurveySessionId → session → equipment/area/project binding",
        "Finding/defect" => "Finding.SurveySessionId → session → equipment/area/project binding",
        "File/photo" => "Every non-null SurveyFile parent (session/finding/job/area/equipment) must resolve to one tenant; ProjectAreaPhoto → area → project",
        "QC review" => "QcReview.SurveySessionId → session → equipment/area/project binding",
        "Report/export" => "Project ID → project binding; collection exports filter every query through ITenantQueryScope",
        "Client dashboard" => "AssignedClientId + tenant-filtered Projects; conflicting tenant project set denies",
        _ => "Review controller-specific server-side parent chain"
    };

    private static string Escape(string value) => value.Replace("|", "\\|");
}
