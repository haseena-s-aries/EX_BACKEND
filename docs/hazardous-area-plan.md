# Hazardous Area Plan

Create/Edit Project now accepts an optional original JPG, JPEG, PNG or PDF after Team Assignment. Existing mandatory area creation is retained. Project Details offers authenticated viewing, with an explicitly loaded image preview to avoid fetching large files on page load.

## API and storage

- Existing `POST /api/projects` and `PUT /api/projects/{id}` JSON requests remain valid and preserve the existing file on edits.
- For an upload, send the same project DTO fields as multipart form fields and one optional `hazardousAreaPlan` file. File storage and the database save share the existing project action; no separate partially committed upload is required.
- Responses include nullable `hazardousAreaPlanPath`, `hazardousAreaPlanFileName`, and `hazardousAreaPlanContentType`. The path is an API URL, never a physical storage path.
- `GET /api/projects/{id}/hazardous-area-plan` uses existing JWT/project access rules, streams the original file, supports HTTP ranges, and disables browser caching. No public/static storage endpoint is added.
- Storage reuses `IProjectPhotoStorage` and the configured private root, under `plans/{projectId}/{random-file-name}`. Existing configured file-size limits apply. Extension, MIME and JPEG/PNG/PDF signatures are validated server-side.
- The original bytes remain unchanged. A failed upload/database save removes the new file and preserves the old plan; only a successful explicit replacement removes the old file.

## Database

`20261006134251_HazardousAreaPlan` adds nullable `Projects.HazardousAreaPlanPath` (500 characters) and `HazardousAreaPlanFileName` (255 characters). MIME is derived from the validated extension, so no extra content-type column is required.

The migration was applied successfully to the configured development database on 2026-10-06. Do not run `Update-Database` again for that database. For a different database, apply this migration before running the updated API, using Visual Studio Package Manager Console:

```powershell
Update-Database -Migration HazardousAreaPlan
```

An idempotent SQL script is provided in `hazardous-area-plan-migrations.sql` for databases already on the previous `ManagerQcMonitoring` migration. No existing project data or assignment/status fields are modified.

## Viewing

Web images use original authenticated blobs with fit-to-screen, zoom, 100% original-size display and scrolling. PDFs use the browser's original PDF viewer with its native zoom and page controls. Blob URLs are revoked when the viewer closes.

Android/iOS use a document picker and `react-native-pdf` for original PDF rendering, plus the existing authenticated image approach with zoom/scroll controls. Native libraries added: `@react-native-documents/picker`, `react-native-pdf`, `react-native-blob-util`. Rebuild native apps after installing; iOS also needs its normal CocoaPods install on macOS. Native device builds were not run in this Windows session.

## Validation performed

Swagger follow-up: JSON and multipart actions on the same project URLs are combined into one OpenAPI operation via `ProjectUploadOpenApi`, with both content types documented. Unexpected duplicate routes still fail validation. CORS uses the configured `AllowFrontend` policy once, before authentication/authorization, so the browser receives authentication errors correctly.

Live checks after rebuild/restart: HTTP and HTTPS `/swagger/v1/swagger.json` both returned 200; upload preflight returned 204; protected project requests without a token returned 401 with the expected allowed-origin header. Swagger generation/content-type and CORS regression checks are included in the local HTTP test host.

- Frontend validation/API payload/viewer/lazy-loading tests, plus existing Surveyor/QC/Manager regression suites.
- Backend storage byte-preservation, invalid-file/path rejection, permissions, optional create and replacement checks.
- Actual local HTTP tests with an isolated in-memory database: JSON and multipart model binding, uploads, authenticated streaming, and anonymous-access rejection. Test authentication exists only in the test host; production JWT logic is unchanged.
- Backend build and production web build. EF reports no pending model changes.
- Full TypeScript checking still reports pre-existing DOM/OTP typing errors outside the new plan components.

Source backup made and hash-verified before edits:
`C:\Users\AL414\source\repos\backups\hazardous-plan-20261006-174058`.

PNG support uses the existing nullable file columns; no additional migration is needed. Image plans use the same large A3 landscape fitting area as equipment area layouts in generated reports, preserving aspect ratio and the uploaded original. Uploaded PDF plans retain original vector pages and their existing orientation-aware fitting.
