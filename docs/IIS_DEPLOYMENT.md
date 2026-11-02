# Ex Survey API on IIS

## Publish

From the repository root:

```powershell
dotnet publish .\Ex-SurveyApp_api\Ex-SurveyApp_api.csproj -c Release /p:PublishProfile=IISFolder
```

Deploy the contents of `artifacts/iis-publish` to the IIS site's physical folder. Keep the generated `web.config`, DLLs, dependency files and runtime configuration together. Do not point IIS at the source directory or build output. The profile uses .NET 10, framework-dependent, in-process hosting and Production environment.

## Server settings

1. Enable IIS and install the **.NET 10 ASP.NET Core Hosting Bundle** on the server. The SDK alone is insufficient. If IIS was installed after the bundle, repair the bundle. Restart IIS/WAS after installation during an appropriate maintenance window.
2. Create a dedicated application pool: **.NET CLR Version = No Managed Code**, **Managed pipeline = Integrated**, **Enable 32-Bit Applications = False** for a 64-bit runtime.
3. Assign the pool to the site and set its physical path to the deployed publish folder. Configure an HTTPS binding and a valid certificate; production redirects HTTP to HTTPS.
4. Grant `IIS AppPool\YOUR_POOL_NAME` Read & Execute on the publish folder. Grant Modify only on the configured upload storage directory and any diagnostic logs directory.
5. For hosting beneath another site, convert the folder to an IIS Application and give it a separate application pool. Swagger uses a relative document URL to support this layout.

## Required application configuration

`launchSettings.json` and Development User Secrets do not configure deployed IIS applications. The project copies the local `appsettings.Production.json` into build and publish output. The IIS profile sets the environment to Production, so ASP.NET Core loads this file automatically. Configure production values in that file or through application-pool environment variables, which override JSON settings. The file remains ignored by Git. Treat the publish folder as private if it contains credentials, and restrict access to deployed configuration.

| Environment variable | Required value |
| --- | --- |
| `Jwt__SigningKey` | A persistent, randomly generated secret of at least 32 UTF-8 bytes. Missing/short keys prevent startup. |
| `ConnectionStrings__SurveyDatabase` | A real SQL Server connection string accessible from the IIS server. |
| `Cors__AllowedOrigins__0` | The exact frontend origin, e.g. `https://survey.example.com`, without a trailing slash or path. Use indexes 1, 2, etc. for additional origins. |
| `ProjectPhotoStorage__RootPath` | A persistent absolute upload directory, e.g. `D:\ExSurveyData\uploads`, with Modify permission for the pool identity. |
| `EmailSettings__SmtpUsername` / `EmailSettings__SmtpPassword` | SMTP credentials where required by your mail server. Configure the remaining mail options for your service. |

Recycle the application pool after changing configuration. Do not use Development to bypass production validation. In-memory database mode is only supported in Development.

For Windows integrated SQL authentication, IIS uses the application-pool identity rather than the interactive developer's account. Configure an appropriate service identity and SQL permissions. For SQL authentication, supply the server-side connection string privately. Confirm SQL network/firewall access and certificate trust.

The application queries and seeds checklist tables during startup. Apply the existing EF migrations through your normal reviewed database deployment process **before starting this version**. The application does not automatically apply migrations. Do not set bootstrap account variables unless you intend to provision those accounts; email/password pairs must both be supplied.

## Verify and diagnose

Browse `https://YOUR_HOST/` to open Swagger automatically. The root redirects to `swagger/index.html`, including the application prefix when hosted as an IIS sub-application. The direct Swagger URL remains available. Test a protected API endpoint: a 401 response without a token confirms the request reached authentication.

If startup fails, inspect **Event Viewer > Windows Logs > Application**. To capture additional startup output temporarily, set `stdoutLogEnabled="true"` on the generated `aspNetCore` element in deployed `web.config`; use `stdoutLogFile=".\logs\stdout"`, create that directory and grant the pool Modify permission. Reproduce once, inspect the log privately, then turn stdout logging off.

| Error | Check |
| --- | --- |
| 500.19 | Detailed IIS configuration error, Hosting Bundle/module installation, malformed web.config, locked IIS sections. |
| 500.30 | Startup exception: JWT key, SQL connection/login, missing tables/migrations, bootstrap configuration. |
| 500.31 / 500.32 | Installed .NET 10 runtime and application-pool/runtime architecture. |
| Swagger 404 | Application path and `/swagger/index.html`; ensure IIS points to published output. |
| Browser CORS error | Exact frontend origin in production configuration, then recycle the pool. |

Official guidance: https://learn.microsoft.com/aspnet/core/host-and-deploy/iis/?view=aspnetcore-10.0
