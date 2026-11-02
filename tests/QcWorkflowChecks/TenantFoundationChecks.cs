using System.Security.Claims;
using Ex_SurveyApp_api.Data;
using Ex_SurveyApp_api.Models;
using Ex_SurveyApp_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Routing;

internal static class TenantFoundationChecks
{
    public static async Task Run()
    {
        var db = new SurveyDbContext(new DbContextOptionsBuilder<SurveyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var tenantA = new Tenant { TenantKey = "A", DisplayName = "Tenant A", Status = TenantLifecycleStatus.Active };
        var tenantB = new Tenant { TenantKey = "B", DisplayName = "Tenant B", Status = TenantLifecycleStatus.Active };
        var user = new User { Email = "tenant@test.invalid", DisplayName = "Tenant User", PasswordHash = "test", Role = UserRole.Manager };
        var userB = new User { Email = "tenant-b@test.invalid", DisplayName = "Tenant B User", PasswordHash = "test", Role = UserRole.Manager };
        var inactiveUser = new User { Email = "inactive@test.invalid", DisplayName = "Inactive", PasswordHash = "test", Role = UserRole.Manager };
        var switcher = new User { Email = "switcher@test.invalid", DisplayName = "Switcher", PasswordHash = "test", Role = UserRole.Manager };
        var membershipA = new TenantMembership { Tenant = tenantA, TenantId = tenantA.Id, UserId = user.Id, Status = TenantMembershipStatus.Active };
        var membershipB = new TenantMembership { Tenant = tenantB, TenantId = tenantB.Id, UserId = userB.Id, Status = TenantMembershipStatus.Active };
        var inactiveMembership = new TenantMembership { Tenant = tenantA, TenantId = tenantA.Id, UserId = inactiveUser.Id, Status = TenantMembershipStatus.Suspended };
        var switcherA = new TenantMembership { Tenant = tenantA, TenantId = tenantA.Id, UserId = switcher.Id, Status = TenantMembershipStatus.Active };
        var switcherB = new TenantMembership { Tenant = tenantB, TenantId = tenantB.Id, UserId = switcher.Id, Status = TenantMembershipStatus.Active };
        var project = new Project { ProjectName = "Unassigned", JobNo = "T-1", Sector = "Marine", Location = "Dubai", VesselOrSiteName = "Legacy site", CreatedByUserId = user.Id, AssignedClientId = user.Id };
        var contract = new Contract { Tenant = tenantA, TenantId = tenantA.Id, ContractKey = "C-1", Name = "Contract" };
        var binding = new TenantProjectBinding { TenantId = tenantA.Id, ContractId = contract.Id, ProjectId = project.Id };
        var site = new Site { TenantId = tenantA.Id, TenantProjectBindingId = binding.Id, Name = "Site" };
        var role = new TenantRoleGrant { TenantId = tenantA.Id, TenantMembershipId = membershipA.Id, Membership = membershipA, RoleCode = "Surveyor", ScopeType = TenantRoleScopeType.Project, ScopeId = project.Id };
        var area = new ProjectArea { ProjectId = project.Id, AreaName = "Deck" };
        var areaPhoto = new ProjectAreaPhoto { ProjectAreaId = area.Id, FileName = "deck.jpg", FilePath = "test/deck.jpg", UploadedByUserId = user.Id };
        var equipment = new AreaEquipment { ProjectId = project.Id, AreaId = area.Id, TagNumber = "EQ-1", Type = "Motor", CreatedByUserId = user.Id };
        var session = new SurveySession { AreaEquipmentId = equipment.Id, SurveyorId = user.Id };
        var answer = new SurveyAnswer { SurveySessionId = session.Id, ChecklistQuestionId = Guid.NewGuid(), Value = "pass" };
        var finding = new Finding { SurveySessionId = session.Id, Observation = "Test observation" };
        var qcReview = new QcReview { SurveySessionId = session.Id, ReviewedByUserId = user.Id };
        var file = new SurveyFile { SurveySessionId = session.Id, FindingId = finding.Id, FileName = "photo.jpg", StorageKey = "test/photo.jpg", ContentType = "image/jpeg", UploadedById = user.Id, Category = FileCategory.FindingPhoto };
        var missingOwnerProject = new Project { ProjectName = "Unmapped", JobNo = "T-0", Sector = "Marine", Location = "Dubai", VesselOrSiteName = "Unknown", CreatedByUserId = user.Id };
        db.AddRange(tenantA, tenantB, user, userB, inactiveUser, switcher, membershipA, membershipB, inactiveMembership,
            switcherA, switcherB, project, missingOwnerProject, contract, binding, site, role, area, areaPhoto, equipment, session, answer, finding, qcReview, file);
        await db.SaveChangesAsync();

        Check(db.Model.FindEntityType(typeof(Tenant)) is not null && db.Model.FindEntityType(typeof(Contract)) is not null, "Tenant and Contract are mapped");
        Check(db.Model.FindEntityType(typeof(TenantMembership))!.GetIndexes().Any(x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(new[] { "TenantId", "UserId" })), "Membership is unique per tenant and existing user");
        using var modelDb = new SurveyDbContext(new DbContextOptionsBuilder<SurveyDbContext>()
            .UseSqlServer("Server=localhost;Database=TenantModelOnly;TrustServerCertificate=True").Options);
        Check(modelDb.Database.GenerateCreateScript().Contains("CK_TenantRoleGrants_ScopeShape", StringComparison.Ordinal), "Scoped role grant shape is constrained");
        Check(!db.Model.FindEntityType(typeof(Project))!.GetProperties().Any(x => x.Name == "TenantId"), "Existing Project table receives no tenant column");
        Check(db.TenantProjectBindings.Count() == 1 && db.TenantSites.Single().TenantProjectBindingId == binding.Id, "Contract, project binding and site relationship is represented additively");

        var resolver = new TenantScopeResolver(db);
        Check((await resolver.ResolveMembershipAsync(user.Id, tenantA.Id, default)).Succeeded, "Active member can resolve a tenant selected by ID");
        Check(!(await resolver.ResolveMembershipAsync(user.Id, null, default)).Succeeded, "Missing tenant context is denied");
        Check(!(await resolver.ResolveMembershipAsync(user.Id, tenantB.Id, default)).Succeeded, "Cross-tenant membership is denied");
        Check(!(await resolver.ResolveMembershipAsync(inactiveUser.Id, tenantA.Id, default)).Succeeded, "Suspended membership is denied");
        Check((await resolver.ResolveMembershipAsync(switcher.Id, tenantA.Id, default)).Succeeded &&
              (await resolver.ResolveMembershipAsync(switcher.Id, tenantB.Id, default)).Succeeded,
            "Tenant switching resolves each selected tenant only through a separate active membership");
        var owner = await resolver.ResolveResourceAsync(TenantResourceKind.Project, project.Id, default);
        Check(owner.Ownership?.TenantId == tenantA.Id, "Project ownership resolves from the server-side binding");
        Check(await resolver.HasRoleGrantAsync(membershipA.Id, "Surveyor", owner.Ownership!, default), "Scoped project role grant resolves");
        Check((await resolver.ResolveResourceAsync(TenantResourceKind.Project, missingOwnerProject.Id, default)).Ownership is null, "Missing project ownership fails closed");
        foreach (var child in new[]
                 {
                     (TenantResourceKind.ProjectArea, area.Id), (TenantResourceKind.AreaEquipment, equipment.Id),
                     (TenantResourceKind.ProjectAreaPhoto, areaPhoto.Id),
                     (TenantResourceKind.SurveySession, session.Id), (TenantResourceKind.ChecklistAnswer, answer.Id),
                     (TenantResourceKind.Finding, finding.Id), (TenantResourceKind.Defect, finding.Id),
                     (TenantResourceKind.QcReview, qcReview.Id), (TenantResourceKind.SurveyFile, file.Id),
                     (TenantResourceKind.Report, project.Id), (TenantResourceKind.Export, project.Id),
                     (TenantResourceKind.ClientDashboard, user.Id)
                 })
        {
            var resolved = await resolver.ResolveResourceAsync(child.Item1, child.Item2, default);
            Check(resolved.Ownership?.TenantId == tenantA.Id, $"{child.Item1} ownership resolves through its server-side parent");
            Check(!await AuthorizeResourceAsync(resolver, userB.Id, tenantB.Id, child.Item1, child.Item2, "GET"),
                $"Cross-tenant {child.Item1} read denied");
            Check(!await AuthorizeResourceAsync(resolver, userB.Id, tenantB.Id, child.Item1, child.Item2, "PUT"),
                $"Cross-tenant {child.Item1} mutation denied");
        }
        Check(!await AuthorizeResourceAsync(resolver, userB.Id, tenantB.Id, TenantResourceKind.SurveyFile, file.Id, "GET"), "Cross-tenant photo/file access denied");
        Check(!await AuthorizeResourceAsync(resolver, userB.Id, tenantB.Id, TenantResourceKind.Report, project.Id, "GET"), "Cross-tenant report read denied");
        Check(!await AuthorizeResourceAsync(resolver, userB.Id, tenantB.Id, TenantResourceKind.Export, project.Id, "GET"), "Cross-tenant report/export denied");
        var authorizedHttp = await AuthorizeMemberAsync(resolver, user.Id, tenantA.Id);
        var serverContext = new HttpTenantContextAccessor(new HttpContextAccessor { HttpContext = authorizedHttp });
        Check(serverContext.Current?.TenantId == tenantA.Id, "Validated membership installs server-owned tenant context");
        var projectQueryScope = new TenantQueryScope(db, serverContext);
        Check(projectQueryScope.Projects().Any(x => x.Id == project.Id) && !projectQueryScope.Projects().Any(x => x.Id == missingOwnerProject.Id),
            "Tenant list query scope includes only projects with server-side tenant bindings");

        var secondContract = new Contract { Tenant = tenantB, TenantId = tenantB.Id, ContractKey = "C-2", Name = "Second" };
        db.Add(secondContract);
        db.Add(new TenantProjectBinding { TenantId = tenantB.Id, ContractId = secondContract.Id, ProjectId = project.Id });
        await db.SaveChangesAsync();
        var conflict = await resolver.ResolveResourceAsync(TenantResourceKind.Project, project.Id, default);
        Check(conflict.Ownership is null && conflict.FailureCode == "ConflictingResourceOwnership", "Conflicting project tenant ownership fails closed");
        Check(!projectQueryScope.Projects().Any(x => x.Id == project.Id), "Tenant collection query excludes a project with conflicting tenant bindings");
        var otherProject = new Project { ProjectName = "Other", JobNo = "T-2", Sector = "Marine", Location = "Dubai", VesselOrSiteName = "Other site", CreatedByUserId = user.Id };
        var areaConflict = new ProjectArea { ProjectId = project.Id, AreaName = "Area on first project" };
        var inconsistentEquipment = new AreaEquipment { ProjectId = otherProject.Id, AreaId = areaConflict.Id, TagNumber = "CONFLICT", Type = "Motor" };
        db.AddRange(otherProject, areaConflict, inconsistentEquipment);
        await db.SaveChangesAsync();
        var equipmentConflict = await resolver.ResolveResourceAsync(TenantResourceKind.AreaEquipment, inconsistentEquipment.Id, default);
        Check(equipmentConflict.Ownership is null && equipmentConflict.FailureCode == "ConflictingResourceOwnership", "Equipment with conflicting project/area parents fails closed");

        var disabledHandler = new TenantScopeAuthorizationHandler(new HttpContextAccessor { HttpContext = new DefaultHttpContext() }, resolver,
            Options.Create(new TenantFoundationOptions { Enabled = false }));
        var requirement = new TenantScopeRequirement(false);
        var context = new AuthorizationHandlerContext([requirement], new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())], "test")), null);
        await disabledHandler.HandleAsync(context);
        Check(!context.HasSucceeded, "Tenant authorization stays inactive while feature flag is false");

        var activeHttp = new DefaultHttpContext();
        activeHttp.Request.Headers[TenantAuthorizationPolicyNames.TenantSelectorHeader] = tenantB.Id.ToString();
        var activeHandler = new TenantScopeAuthorizationHandler(new HttpContextAccessor { HttpContext = activeHttp }, resolver,
            Options.Create(new TenantFoundationOptions { Enabled = true }));
        var activeRequirement = new TenantScopeRequirement(false);
        var crossTenantContext = new AuthorizationHandlerContext([activeRequirement], new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())], "test")), null);
        await activeHandler.HandleAsync(crossTenantContext);
        Check(!crossTenantContext.HasSucceeded, "Active tenant policy denies a tenant selected without membership");
        activeHttp.Request.Headers.Remove(TenantAuthorizationPolicyNames.TenantSelectorHeader);
        var missingContext = new AuthorizationHandlerContext([activeRequirement], new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())], "test")), null);
        await activeHandler.HandleAsync(missingContext);
        Check(!missingContext.HasSucceeded, "Active tenant policy denies missing tenant context");
        Console.WriteLine("All tenant foundation checks passed.");
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
        Console.WriteLine("PASS: " + label);
    }

    private static async Task<bool> AuthorizeResourceAsync(ITenantScopeResolver resolver, Guid userId, Guid tenantId,
        TenantResourceKind kind, Guid resourceId, string method)
    {
        var http = CreateHttpContext(userId, tenantId, method, resourceId);
        http.SetEndpoint(new Endpoint(_ => Task.CompletedTask,
            new EndpointMetadataCollection(new TenantResourceScopeAttribute(kind, "resourceId")), "tenant-test"));
        var handler = new TenantScopeAuthorizationHandler(new HttpContextAccessor { HttpContext = http }, resolver,
            Options.Create(new TenantFoundationOptions { Enabled = true }));
        var requirement = new TenantScopeRequirement(true);
        var context = new AuthorizationHandlerContext([requirement], http.User, http);
        await handler.HandleAsync(context);
        return context.HasSucceeded;
    }

    private static async Task<DefaultHttpContext> AuthorizeMemberAsync(ITenantScopeResolver resolver, Guid userId, Guid tenantId)
    {
        var http = CreateHttpContext(userId, tenantId, "GET", Guid.Empty);
        var handler = new TenantScopeAuthorizationHandler(new HttpContextAccessor { HttpContext = http }, resolver,
            Options.Create(new TenantFoundationOptions { Enabled = true }));
        var requirement = new TenantScopeRequirement(false);
        var context = new AuthorizationHandlerContext([requirement], http.User, http);
        await handler.HandleAsync(context);
        if (!context.HasSucceeded) throw new InvalidOperationException("Expected fixture membership to authorize.");
        return http;
    }

    private static DefaultHttpContext CreateHttpContext(Guid userId, Guid tenantId, string method, Guid resourceId)
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "test"))
        };
        http.Request.Method = method;
        http.Request.Headers[TenantAuthorizationPolicyNames.TenantSelectorHeader] = tenantId.ToString();
        http.Request.RouteValues["resourceId"] = resourceId.ToString();
        return http;
    }
}
