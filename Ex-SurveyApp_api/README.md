# Ex-Survey API

.NET 8 Web API skeleton for Ex equipment surveys. Swagger is available at `/swagger` in Development.

## Run locally

1. Run `dotnet run --launch-profile http` and open `http://localhost:5235/swagger`.
2. Development uses an in-memory database by default. Its data disappears when the process stops.
3. To use SQL Server, set `UseInMemoryDatabase` to `false`, configure `ConnectionStrings__SurveyDatabase`, then run `dotnet ef database update`.

## Authentication

- `POST /api/auth/register` accepts `email`, `displayName`, and `password` (at least 12 characters). Public registration creates a Client user and returns a one-hour JWT.
- `POST /api/auth/login` accepts `email` and `password` and returns a JWT.
- `GET /api/auth/me` requires `Authorization: Bearer <token>`.
- `POST /api/auth/users` lets an Admin create a user with a selected `role`: `Admin`, `Manager`, `Qc`, `Surveyor`, or `Client`.
- Client and job reads require Admin, Manager, or Qc. Creating clients and jobs requires Admin or Manager. Surveyor and Client job access will be scoped to assignments in a later module.

To create the first admin locally, set `BootstrapAdmin__Email` and `BootstrapAdmin__Password` in the process environment before starting the API. The password must be kept private. No default admin credentials are stored in the project. For deployment, also provide a stable `Jwt__SigningKey` of at least 32 bytes using secret configuration; Development generates a temporary key when none is supplied, invalidating tokens after restart. Use HTTPS for non-local clients. Email verification and password reset are not implemented yet.

The model and initial migration include job assignments, areas, equipment, checklist templates and questions, survey sessions and answers, findings, files, QC reviews, report revisions, audit logs, and sync batches. Their API workflows are still to be implemented.
