using System.Text.Json;
using Ex_SurveyApp_api.Models;
namespace Ex_SurveyApp_api.Services;
public static class CompanyAssets {
    public static IReadOnlyList<CompanyMaster> Seed() {
        using var stream = typeof(CompanyAssets).Assembly.GetManifestResourceStream("ExSurvey.Companies")!;
        return JsonSerializer.Deserialize<CompanyMaster[]>(stream)!;
    }
    public static Stream? Logo(string? name) {
        if (string.IsNullOrWhiteSpace(name) || Path.GetFileName(name) != name || name.Contains('\\') || name.Contains('/')) return null;
        return typeof(CompanyAssets).Assembly.GetManifestResourceStream("ExSurvey.CompanyLogos." + name);
    }
    public static CompanyResponse Response(CompanyMaster company) {
        using var logo = Logo(company.LogoPath);
        return new(company.Id, company.CompanyName, company.DisplayName, company.Address, company.Telephone, company.Fax, company.Website, company.Email,
            logo is null ? null : $"/api/companies/{company.Id}/logo");
    }
}
