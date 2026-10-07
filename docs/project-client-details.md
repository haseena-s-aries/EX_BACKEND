# Optional project metadata and client details

Create and Edit Project now expose UID Number, Document Number, Revision and multiline Description in Project Information. CLIENT DETAILS follows Project Information, before Team Assignment, with independent optional Client Name and Client Logo/Image. Assign Client remains optional and continues to store AssignedClientId.

Project information text and client name are converted to uppercase in input handlers and API payloads. The backend independently trims and uses ToUpperInvariant. Empty optional values are stored as null. User names, assignment IDs, emails, sector options and original upload filenames are not modified.

The existing JSON and multipart POST/PUT project endpoints accept uidNumber, documentNumber, revision, description, clientName, and removeClientLogo. The multipart endpoints also accept clientLogo alongside hazardousAreaPlan. JPG/JPEG/PNG logos use existing private project photo storage with generated filenames, size limits, extension/MIME/signature validation, and path confinement. Original bytes are preserved. Failed writes clean up new uploads. Saving without an upload preserves the existing logo; removeClientLogo=true explicitly removes it. Choosing and then clearing a replacement before save retains the previous image.

Project Details exposes clientLogoPath as `/api/projects/{id}/client-logo`, never a physical storage path. The authenticated image component displays the logo. Downloads use existing project access rules and private/no-store/nosniff headers. Deleted/replaced project logos are cleaned up after the database save succeeds.

The report aggregate exposes these metadata fields plus a protected clientLogo asset. Cover UID, Document Number, Revision and client logo/name use actual Project data. Revision history uses the project revision and description when there are no legacy job revision records. For backward compatibility the report's existing assigned-client display name remains a fallback if ClientName is absent; project client details and assignment still remain independent.

## Database

Migration `20261007120651_ProjectReportMetadataAndClientDetails` adds six nullable columns to Projects only: UidNumber, DocumentNumber, Revision, Description, ClientName and ClientLogoPath. Existing rows remain valid. This migration has been applied to the configured local development SQL database.

For another database, apply the normal EF migration with `Update-Database` in Visual Studio, or:

```powershell
dotnet ef database update --project Ex-SurveyApp_api
```

The workspace-local tool, when needed, is `../tmp/tools/dotnet-ef.exe`; `docs/project-client-details-migration.sql` contains the additive SQL from OptionalProjectClient to this migration. Take the normal database backup before deploying migrations to another environment. Do not run the Down migration to preserve new metadata.

## Verification

HTTP regression checks cover nullable fields, JSON/multipart creation and updates, uppercase trimming, independent Client assignment, protected original-image downloads, preservation/replacement/removal, and rejected MIME/signature/extension mismatches. Report checks verify saved metadata and branding, along with existing QC and survey workflows. Frontend ProjectMetadata tests cover input handling, payloads, filenames, logo validation and combined multipart uploads; the existing Reports/QC/equipment tests also pass. Web production build succeeds. Native UI code is connected, but device uploads/downloads need testing on the target device.
