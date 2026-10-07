namespace Ex_SurveyApp_api.Models;
public class CompanyMaster {
    public Guid Id {get; set;}
    public required string CompanyName {get; set;}
    public required string DisplayName {get; set;}
    public string? Address {get; set;}
    public string? Telephone {get; set;}
    public string? Fax {get; set;}
    public string? Website {get; set;}
    public string? Email {get; set;}
    public string? LogoPath {get; set;}
    public bool IsActive {get; set;} = true;
    public DateTimeOffset CreatedAt {get; set;}
    public DateTimeOffset UpdatedAt {get; set;}
}
public record CompanyResponse(Guid Id, string CompanyName, string DisplayName, string? Address, string? Telephone, string? Fax, string? Website, string? Email, string? LogoUrl);
