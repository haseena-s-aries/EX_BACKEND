using Ex_SurveyApp_api.Controllers;
using Ex_SurveyApp_api.Models;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Ex_SurveyApp_api.Services;

// MVC selects these actions by Content-Type. OpenAPI represents both as one
// operation with JSON and multipart request bodies on the existing URL.
public static class ProjectUploadOpenApi
{
    public static void Configure(SwaggerGenOptions options)
    {
        options.ResolveConflictingActions(ResolveProjectContentTypes);
        options.OperationFilter<ProjectPlanRequestBodyFilter>();
    }

    private static ApiDescription ResolveProjectContentTypes(IEnumerable<ApiDescription> descriptions)
    {
        var actions = descriptions.ToArray();
        var expectedPair = actions.Length == 2 && actions.All(action =>
            action.ActionDescriptor is ControllerActionDescriptor descriptor &&
            descriptor.ControllerTypeInfo.AsType() == typeof(ProjectController) &&
            descriptor.ActionName is nameof(ProjectController.CreateProject) or nameof(ProjectController.CreateProjectWithPlan)
                or nameof(ProjectController.UpdateProject) or nameof(ProjectController.UpdateProjectWithPlan));
        if (!expectedPair)
            throw new InvalidOperationException("Unexpected duplicate API operation. Only project JSON/multipart action pairs may be combined.");
        return actions.Single(action => action.SupportedRequestFormats.Any(format => format.MediaType == "application/json"));
    }
}

public sealed class ProjectPlanRequestBodyFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType != typeof(ProjectController) ||
            context.MethodInfo.Name is not (nameof(ProjectController.CreateProject) or nameof(ProjectController.UpdateProject)))
            return;

        var reference = context.SchemaGenerator.GenerateSchema(typeof(UpsertProjectRequest), context.SchemaRepository);
        var fields = reference.Reference is null ? reference : context.SchemaRepository.Schemas[reference.Reference.Id];
        var schema = new OpenApiSchema
        {
            Type = "object",
            Properties = new Dictionary<string, OpenApiSchema>(fields.Properties),
            Required = new HashSet<string>(fields.Required)
        };
        schema.Properties["hazardousAreaPlan"] = new OpenApiSchema
        {
            Type = "string", Format = "binary",
            Description = "Optional original JPG, JPEG or PDF. Omit to preserve the existing plan on edits."
        };
        operation.RequestBody ??= new OpenApiRequestBody();
        schema.Properties["clientLogo"] = new OpenApiSchema { Type = "string", Format = "binary", Description = "Optional JPG, JPEG or PNG client logo. Omit to preserve the existing logo." };
        operation.RequestBody.Content["multipart/form-data"] = new OpenApiMediaType {Schema = schema};
    }
}
