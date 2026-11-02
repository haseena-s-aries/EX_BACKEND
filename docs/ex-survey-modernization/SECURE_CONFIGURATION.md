# Secure Configuration

Credential values have been removed from the committed base and Development settings files. ASP.NET Core receives secrets from environment variables or the .NET Secret Manager in Development. Environment variable names use double underscores for nested configuration keys.

| Setting | Environment variable / user-secret key |
|---|---|
| JWT signing key | `Jwt__SigningKey` / `Jwt:SigningKey` |
| SQL Server connection | `ConnectionStrings__SurveyDatabase` / `ConnectionStrings:SurveyDatabase` |
| SMTP username | `EmailSettings__SmtpUsername` / `EmailSettings:SmtpUsername` |
| SMTP password | `EmailSettings__SmtpPassword` / `EmailSettings:SmtpPassword` |
| Bootstrap Admin email/password | `BootstrapAdmin__Email`, `BootstrapAdmin__Password` |
| Bootstrap Manager email/password | `BootstrapManager__Email`, `BootstrapManager__Password` |

For Development, use `dotnet user-secrets set` from the API project directory; do not put secret values in source files, command history, screenshots, logs, CI output or this document. CI should use ephemeral test secrets only when a test needs them. Production should use the hosting platform’s approved secret store/environment injection. A missing JWT signing key is intentionally a startup error outside Development. Development may create an ephemeral process-local signing key when none is configured.

The existing `appsettings.Production.json` is a local, untracked file from earlier work. It remains ignored by Git and its contents were not rewritten. At the owner's request, it is included in build and publish output for IIS deployment. Keep deployment artifacts private when they contain credentials; environment variables can override JSON values. The rotation checklist identifies the credential classes without exposing values.

The environment variables are not resolved by `${VAR}` string substitution inside JSON. Supply the keys directly through the ASP.NET Core environment-variable provider or Secret Manager.
