namespace Ex_SurveyApp_api.Attributes;

/// <summary>
/// Passive architecture inventory for the future tenant authorization work.
/// This metadata does not enforce access or change current endpoint behavior.
/// </summary>
public enum EndpointScopeKind
{
    TenantResource,
    IdentityLifecycle,
    SharedReferenceData
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
public sealed class EndpointScopeInventoryAttribute(EndpointScopeKind scope) : Attribute
{
    public EndpointScopeKind Scope { get; } = scope;
}
