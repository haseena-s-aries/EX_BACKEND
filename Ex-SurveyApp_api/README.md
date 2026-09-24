# Ex-Survey API

.NET 8 Web API skeleton for Ex equipment surveys. Swagger is available at `/swagger` in Development.

## Run locally

1. Run `dotnet run --launch-profile http` and open `http://localhost:5235/swagger`.
2. Set `EmailSettings__SmtpPassword` through environment variables or another secret provider.
3. Development can use an in-memory database by setting `UseInMemoryDatabase` to `true`. Its data disappears when the process stops.
4. To use SQL Server, set `UseInMemoryDatabase` to `false`, configure `ConnectionStrings__SurveyDatabase`, then run `dotnet ef database update`.

## Authentication

- `POST /api/auth/register/send-otp` accepts `name`, `email`, and `role`, stores them with a four-digit OTP, and sends it by SMTP. Valid roles are `Admin`, `Manager`, `Qc`, `Surveyor`, and `Client`. The OTP expires after 10 minutes.
- `POST /api/auth/register/verify-otp` accepts `email` and `otp`.
- `POST /api/auth/register/resend-otp` accepts `email`. Resending is limited to once every 60 seconds.
- `POST /api/auth/register/complete` accepts the verified `email` and any non-empty password. It creates a user with the role requested at the OTP step, sets Pending approval, and does not issue a JWT.
- `POST /api/auth/login` accepts `email` and `password` and returns a JWT.
- `GET /api/auth/me` requires `Authorization: Bearer <token>`.
- `POST /api/auth/users` lets an Admin create a user with a selected `role`: `Admin`, `Manager`, `Qc`, `Surveyor`, or `Client`.
- `GET /api/auth/approvals/pending` lets Admin and Manager users list pending registrations.
- `PUT /api/auth/approvals/{userId}/approve` and `/reject` review a pending registration. Admin can review every role; Manager can review QC, Surveyor, and Client registrations.
- Client and job reads require Admin, Manager, or Qc. Creating clients and jobs requires Admin or Manager. Surveyor and Client job access will be scoped to assignments in a later module.

Pending and rejected users receive HTTP 403 from login. JWTs are issued only after an approved user logs in successfully. Existing users are marked Approved by the approval migration.

Development creates and maintains this approved admin account for testing approvals:

- Email: `admin@arieslocal.com`
- Password: `Admin@123456`

These credentials are defined only in `appsettings.Development.json`. Override `BootstrapAdmin__Email`, `BootstrapAdmin__Password`, and `BootstrapAdmin__DisplayName` for another local account. Production should supply separate admin credentials through secret configuration. For deployment, also provide a stable `Jwt__SigningKey` of at least 32 bytes; Development generates a temporary key when none is supplied, invalidating tokens after restart. Use HTTPS for non-local clients.

The model and initial migration include job assignments, areas, equipment, checklist templates and questions, survey sessions and answers, findings, files, QC reviews, report revisions, audit logs, and sync batches. Their API workflows are still to be implemented.

## Projects

The old `/api/jobs` controller has been replaced by `/api/projects`. Admin and Manager can create and update projects and manage areas. Only Admin can delete a project. Admin and Manager can view all projects; Surveyor, QC, and Client users see only projects assigned to their role-specific user ID.

- `POST /api/projects`, `GET /api/projects`, `GET /api/projects/{id}`, `PUT /api/projects/{id}`, `DELETE /api/projects/{id}`
- `POST /api/projects/{projectId}/areas`, `PUT /api/projects/{projectId}/areas/{areaId}`, `DELETE /api/projects/{projectId}/areas/{areaId}`
- `POST /api/projects/{projectId}/areas/{areaId}/photos` accepts multiple `files` parts as `multipart/form-data`.
- `GET /api/projects/{projectId}/areas/{areaId}/photos` lists photo metadata.
- `DELETE /api/projects/{projectId}/areas/{areaId}/photos/{photoId}` removes the database record and stored file.

Project assignments must reference active, approved Surveyor, QC, and Client users. Assigned Surveyors can upload and delete area photos. QC and Client users have read-only access to their assigned projects and photo metadata. Photos are stored privately under `ProjectPhotoStorage:RootPath`; JPG, PNG, and WebP are accepted, with limits controlled by `MaxFileSizeBytes` and `MaxFilesPerRequest`.

Frontend integration can load eligible assignment users from `GET /api/projects/assignment-options`. Photo responses include `downloadUrl`; fetch that URL with the JWT `Authorization` header and display the returned Blob. Development accepts browser requests from any `localhost` or `127.0.0.1` port. Production must configure allowed frontend origins through `Cors:AllowedOrigins` or environment keys such as `Cors__AllowedOrigins__0`.
