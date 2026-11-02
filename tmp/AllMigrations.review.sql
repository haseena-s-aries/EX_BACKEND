IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE TABLE [ChecklistTemplates] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [InspectionType] int NOT NULL,
        [Version] int NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_ChecklistTemplates] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE TABLE [Clients] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [ContactEmail] nvarchar(max) NULL,
        [LogoFileId] uniqueidentifier NULL,
        CONSTRAINT [PK_Clients] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] uniqueidentifier NOT NULL,
        [Email] nvarchar(450) NOT NULL,
        [DisplayName] nvarchar(max) NOT NULL,
        [PasswordHash] nvarchar(max) NOT NULL,
        [Role] int NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE TABLE [ChecklistQuestions] (
        [Id] uniqueidentifier NOT NULL,
        [ChecklistTemplateId] uniqueidentifier NOT NULL,
        [Text] nvarchar(max) NOT NULL,
        [SortOrder] int NOT NULL,
        [IsRequired] bit NOT NULL,
        CONSTRAINT [PK_ChecklistQuestions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ChecklistQuestions_ChecklistTemplates_ChecklistTemplateId] FOREIGN KEY ([ChecklistTemplateId]) REFERENCES [ChecklistTemplates] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE TABLE [Jobs] (
        [Id] uniqueidentifier NOT NULL,
        [ClientId] uniqueidentifier NOT NULL,
        [JobNumber] nvarchar(450) NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [Uid] nvarchar(max) NULL,
        [Revision] int NOT NULL,
        [Status] int NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_Jobs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Jobs_Clients_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [Clients] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE TABLE [AuditLogs] (
        [Id] uniqueidentifier NOT NULL,
        [ActorId] uniqueidentifier NULL,
        [EntityType] nvarchar(max) NOT NULL,
        [EntityId] uniqueidentifier NOT NULL,
        [Action] nvarchar(max) NOT NULL,
        [OccurredAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AuditLogs_Users_ActorId] FOREIGN KEY ([ActorId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE TABLE [SyncBatches] (
        [Id] uniqueidentifier NOT NULL,
        [SurveyorId] uniqueidentifier NOT NULL,
        [ClientBatchId] nvarchar(450) NOT NULL,
        [ReceivedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_SyncBatches] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SyncBatches_Users_SurveyorId] FOREIGN KEY ([SurveyorId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE TABLE [Areas] (
        [Id] uniqueidentifier NOT NULL,
        [JobId] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        CONSTRAINT [PK_Areas] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Areas_Jobs_JobId] FOREIGN KEY ([JobId]) REFERENCES [Jobs] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE TABLE [JobAssignments] (
        [Id] uniqueidentifier NOT NULL,
        [JobId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Role] int NOT NULL,
        CONSTRAINT [PK_JobAssignments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_JobAssignments_Jobs_JobId] FOREIGN KEY ([JobId]) REFERENCES [Jobs] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_JobAssignments_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE TABLE [Equipment] (
        [Id] uniqueidentifier NOT NULL,
        [AreaId] uniqueidentifier NOT NULL,
        [TagNumber] nvarchar(450) NOT NULL,
        [Description] nvarchar(max) NULL,
        [Manufacturer] nvarchar(max) NULL,
        [Model] nvarchar(max) NULL,
        [SerialNumber] nvarchar(max) NULL,
        [ExMarking] nvarchar(max) NULL,
        [RfidTag] nvarchar(max) NULL,
        CONSTRAINT [PK_Equipment] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Equipment_Areas_AreaId] FOREIGN KEY ([AreaId]) REFERENCES [Areas] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE TABLE [SurveySessions] (
        [Id] uniqueidentifier NOT NULL,
        [JobId] uniqueidentifier NOT NULL,
        [EquipmentId] uniqueidentifier NOT NULL,
        [SurveyorId] uniqueidentifier NOT NULL,
        [ChecklistTemplateId] uniqueidentifier NOT NULL,
        [Status] int NOT NULL,
        [SubmittedAt] datetimeoffset NULL,
        CONSTRAINT [PK_SurveySessions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SurveySessions_ChecklistTemplates_ChecklistTemplateId] FOREIGN KEY ([ChecklistTemplateId]) REFERENCES [ChecklistTemplates] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SurveySessions_Equipment_EquipmentId] FOREIGN KEY ([EquipmentId]) REFERENCES [Equipment] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SurveySessions_Jobs_JobId] FOREIGN KEY ([JobId]) REFERENCES [Jobs] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SurveySessions_Users_SurveyorId] FOREIGN KEY ([SurveyorId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE TABLE [Findings] (
        [Id] uniqueidentifier NOT NULL,
        [SurveySessionId] uniqueidentifier NOT NULL,
        [Observation] nvarchar(max) NOT NULL,
        [Recommendation] nvarchar(max) NULL,
        [Severity] nvarchar(max) NULL,
        [Status] int NOT NULL,
        CONSTRAINT [PK_Findings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Findings_SurveySessions_SurveySessionId] FOREIGN KEY ([SurveySessionId]) REFERENCES [SurveySessions] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE TABLE [QcReviews] (
        [Id] uniqueidentifier NOT NULL,
        [SurveySessionId] uniqueidentifier NOT NULL,
        [ReviewerId] uniqueidentifier NOT NULL,
        [Approved] bit NOT NULL,
        [Comment] nvarchar(max) NULL,
        [ReviewedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_QcReviews] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_QcReviews_SurveySessions_SurveySessionId] FOREIGN KEY ([SurveySessionId]) REFERENCES [SurveySessions] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_QcReviews_Users_ReviewerId] FOREIGN KEY ([ReviewerId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE TABLE [SurveyAnswers] (
        [Id] uniqueidentifier NOT NULL,
        [SurveySessionId] uniqueidentifier NOT NULL,
        [ChecklistQuestionId] uniqueidentifier NOT NULL,
        [Value] nvarchar(max) NULL,
        [Comment] nvarchar(max) NULL,
        CONSTRAINT [PK_SurveyAnswers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SurveyAnswers_ChecklistQuestions_ChecklistQuestionId] FOREIGN KEY ([ChecklistQuestionId]) REFERENCES [ChecklistQuestions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SurveyAnswers_SurveySessions_SurveySessionId] FOREIGN KEY ([SurveySessionId]) REFERENCES [SurveySessions] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE TABLE [Files] (
        [Id] uniqueidentifier NOT NULL,
        [JobId] uniqueidentifier NOT NULL,
        [AreaId] uniqueidentifier NULL,
        [EquipmentId] uniqueidentifier NULL,
        [FindingId] uniqueidentifier NULL,
        [Category] int NOT NULL,
        [FileName] nvarchar(max) NOT NULL,
        [StorageKey] nvarchar(max) NOT NULL,
        [ContentType] nvarchar(max) NOT NULL,
        [UploadedById] uniqueidentifier NOT NULL,
        [UploadedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_Files] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Files_Areas_AreaId] FOREIGN KEY ([AreaId]) REFERENCES [Areas] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Files_Equipment_EquipmentId] FOREIGN KEY ([EquipmentId]) REFERENCES [Equipment] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Files_Findings_FindingId] FOREIGN KEY ([FindingId]) REFERENCES [Findings] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Files_Jobs_JobId] FOREIGN KEY ([JobId]) REFERENCES [Jobs] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Files_Users_UploadedById] FOREIGN KEY ([UploadedById]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE TABLE [ReportRevisions] (
        [Id] uniqueidentifier NOT NULL,
        [JobId] uniqueidentifier NOT NULL,
        [Revision] int NOT NULL,
        [FileId] uniqueidentifier NULL,
        [GeneratedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_ReportRevisions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ReportRevisions_Files_FileId] FOREIGN KEY ([FileId]) REFERENCES [Files] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ReportRevisions_Jobs_JobId] FOREIGN KEY ([JobId]) REFERENCES [Jobs] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE INDEX [IX_Areas_JobId] ON [Areas] ([JobId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_ActorId] ON [AuditLogs] ([ActorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE INDEX [IX_ChecklistQuestions_ChecklistTemplateId] ON [ChecklistQuestions] ([ChecklistTemplateId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Equipment_AreaId_TagNumber] ON [Equipment] ([AreaId], [TagNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE INDEX [IX_Files_AreaId] ON [Files] ([AreaId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE INDEX [IX_Files_EquipmentId] ON [Files] ([EquipmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE INDEX [IX_Files_FindingId] ON [Files] ([FindingId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE INDEX [IX_Files_JobId] ON [Files] ([JobId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE INDEX [IX_Files_UploadedById] ON [Files] ([UploadedById]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE INDEX [IX_Findings_SurveySessionId] ON [Findings] ([SurveySessionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE UNIQUE INDEX [IX_JobAssignments_JobId_UserId_Role] ON [JobAssignments] ([JobId], [UserId], [Role]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE INDEX [IX_JobAssignments_UserId] ON [JobAssignments] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE INDEX [IX_Jobs_ClientId] ON [Jobs] ([ClientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Jobs_JobNumber] ON [Jobs] ([JobNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE INDEX [IX_QcReviews_ReviewerId] ON [QcReviews] ([ReviewerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE INDEX [IX_QcReviews_SurveySessionId] ON [QcReviews] ([SurveySessionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE INDEX [IX_ReportRevisions_FileId] ON [ReportRevisions] ([FileId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ReportRevisions_JobId_Revision] ON [ReportRevisions] ([JobId], [Revision]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE INDEX [IX_SurveyAnswers_ChecklistQuestionId] ON [SurveyAnswers] ([ChecklistQuestionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SurveyAnswers_SurveySessionId_ChecklistQuestionId] ON [SurveyAnswers] ([SurveySessionId], [ChecklistQuestionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE INDEX [IX_SurveySessions_ChecklistTemplateId] ON [SurveySessions] ([ChecklistTemplateId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE INDEX [IX_SurveySessions_EquipmentId] ON [SurveySessions] ([EquipmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE INDEX [IX_SurveySessions_JobId] ON [SurveySessions] ([JobId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE INDEX [IX_SurveySessions_SurveyorId] ON [SurveySessions] ([SurveyorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SyncBatches_SurveyorId_ClientBatchId] ON [SyncBatches] ([SurveyorId], [ClientBatchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260923042729_ex-survey', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923055732_RegistrationOtpAndApproval'
)
BEGIN
    ALTER TABLE [Users] ADD [ApprovalReviewedAt] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923055732_RegistrationOtpAndApproval'
)
BEGIN
    ALTER TABLE [Users] ADD [ApprovalReviewedByUserId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923055732_RegistrationOtpAndApproval'
)
BEGIN
    ALTER TABLE [Users] ADD [ApprovalStatus] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923055732_RegistrationOtpAndApproval'
)
BEGIN
    UPDATE [Users] SET [ApprovalStatus] = 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923055732_RegistrationOtpAndApproval'
)
BEGIN
    CREATE TABLE [RegistrationOtps] (
        [Id] uniqueidentifier NOT NULL,
        [Email] nvarchar(320) NOT NULL,
        [DisplayName] nvarchar(200) NOT NULL,
        [Otp] nchar(4) NOT NULL,
        [ExpiresAt] datetimeoffset NOT NULL,
        [LastSentAt] datetimeoffset NOT NULL,
        [IsVerified] bit NOT NULL,
        [VerifiedAt] datetimeoffset NULL,
        CONSTRAINT [PK_RegistrationOtps] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923055732_RegistrationOtpAndApproval'
)
BEGIN
    CREATE INDEX [IX_Users_ApprovalReviewedByUserId] ON [Users] ([ApprovalReviewedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923055732_RegistrationOtpAndApproval'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RegistrationOtps_Email] ON [RegistrationOtps] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923055732_RegistrationOtpAndApproval'
)
BEGIN
    ALTER TABLE [Users] ADD CONSTRAINT [FK_Users_Users_ApprovalReviewedByUserId] FOREIGN KEY ([ApprovalReviewedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923055732_RegistrationOtpAndApproval'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260923055732_RegistrationOtpAndApproval', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923064248_RequestedRegistrationRole'
)
BEGIN
    ALTER TABLE [RegistrationOtps] ADD [RequestedRole] int NOT NULL DEFAULT 4;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923064248_RequestedRegistrationRole'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260923064248_RequestedRegistrationRole', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923093731_ProjectModule'
)
BEGIN
    CREATE TABLE [Projects] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectName] nvarchar(200) NOT NULL,
        [Sector] nvarchar(150) NOT NULL,
        [JobNo] nvarchar(100) NOT NULL,
        [Location] nvarchar(300) NOT NULL,
        [VesselOrSiteName] nvarchar(200) NOT NULL,
        [AssignedSurveyorId] uniqueidentifier NOT NULL,
        [AssignedQcId] uniqueidentifier NOT NULL,
        [AssignedClientId] uniqueidentifier NOT NULL,
        [CreatedByUserId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_Projects] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Projects_Users_AssignedClientId] FOREIGN KEY ([AssignedClientId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Projects_Users_AssignedQcId] FOREIGN KEY ([AssignedQcId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Projects_Users_AssignedSurveyorId] FOREIGN KEY ([AssignedSurveyorId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Projects_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923093731_ProjectModule'
)
BEGIN
    CREATE TABLE [ProjectAreas] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [AreaName] nvarchar(200) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_ProjectAreas] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProjectAreas_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923093731_ProjectModule'
)
BEGIN
    CREATE TABLE [ProjectAreaPhotos] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectAreaId] uniqueidentifier NOT NULL,
        [FileName] nvarchar(255) NOT NULL,
        [FilePath] nvarchar(500) NOT NULL,
        [UploadedByUserId] uniqueidentifier NOT NULL,
        [UploadedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_ProjectAreaPhotos] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProjectAreaPhotos_ProjectAreas_ProjectAreaId] FOREIGN KEY ([ProjectAreaId]) REFERENCES [ProjectAreas] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ProjectAreaPhotos_Users_UploadedByUserId] FOREIGN KEY ([UploadedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923093731_ProjectModule'
)
BEGIN
    CREATE INDEX [IX_ProjectAreaPhotos_ProjectAreaId] ON [ProjectAreaPhotos] ([ProjectAreaId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923093731_ProjectModule'
)
BEGIN
    CREATE INDEX [IX_ProjectAreaPhotos_UploadedByUserId] ON [ProjectAreaPhotos] ([UploadedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923093731_ProjectModule'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProjectAreas_ProjectId_AreaName] ON [ProjectAreas] ([ProjectId], [AreaName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923093731_ProjectModule'
)
BEGIN
    CREATE INDEX [IX_Projects_AssignedClientId] ON [Projects] ([AssignedClientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923093731_ProjectModule'
)
BEGIN
    CREATE INDEX [IX_Projects_AssignedQcId] ON [Projects] ([AssignedQcId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923093731_ProjectModule'
)
BEGIN
    CREATE INDEX [IX_Projects_AssignedSurveyorId] ON [Projects] ([AssignedSurveyorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923093731_ProjectModule'
)
BEGIN
    CREATE INDEX [IX_Projects_CreatedByUserId] ON [Projects] ([CreatedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923093731_ProjectModule'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Projects_JobNo] ON [Projects] ([JobNo]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923093731_ProjectModule'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260923093731_ProjectModule', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924085532_SurveyorEquipmentPins'
)
BEGIN
    ALTER TABLE [ProjectAreas] ADD [LayoutImagePath] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924085532_SurveyorEquipmentPins'
)
BEGIN
    UPDATE pa
    SET LayoutImagePath = (
        SELECT TOP (1) pap.FilePath
        FROM ProjectAreaPhotos AS pap
        WHERE pap.ProjectAreaId = pa.Id
        ORDER BY pap.UploadedAt, pap.Id)
    FROM ProjectAreas AS pa
    WHERE pa.LayoutImagePath IS NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924085532_SurveyorEquipmentPins'
)
BEGIN
    CREATE TABLE [AreaEquipment] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [AreaId] uniqueidentifier NOT NULL,
        [XPercent] decimal(5,2) NOT NULL,
        [YPercent] decimal(5,2) NOT NULL,
        [TagNumber] nvarchar(100) NOT NULL,
        [EquipmentCode] nvarchar(100) NULL,
        [Type] nvarchar(100) NOT NULL,
        [CircuitId] nvarchar(100) NULL,
        [Location] nvarchar(300) NULL,
        [Manufacturer] nvarchar(200) NULL,
        [ProtectionDetails] nvarchar(500) NULL,
        [CertificateNumber] nvarchar(150) NULL,
        [CreatedByUserId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_AreaEquipment] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AreaEquipment_ProjectAreas_AreaId] FOREIGN KEY ([AreaId]) REFERENCES [ProjectAreas] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AreaEquipment_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AreaEquipment_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924085532_SurveyorEquipmentPins'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AreaEquipment_AreaId_TagNumber] ON [AreaEquipment] ([AreaId], [TagNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924085532_SurveyorEquipmentPins'
)
BEGIN
    CREATE INDEX [IX_AreaEquipment_CreatedByUserId] ON [AreaEquipment] ([CreatedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924085532_SurveyorEquipmentPins'
)
BEGIN
    CREATE INDEX [IX_AreaEquipment_ProjectId] ON [AreaEquipment] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924085532_SurveyorEquipmentPins'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260924085532_SurveyorEquipmentPins', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926105350_AreaEquipmentSequenceNo'
)
BEGIN
    ALTER TABLE [AreaEquipment] ADD [SequenceNo] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926105350_AreaEquipmentSequenceNo'
)
BEGIN
    WITH NumberedEquipment AS (
        SELECT Id,
               ROW_NUMBER() OVER (
                   PARTITION BY AreaId
                   ORDER BY CreatedAt, Id) AS NewSequenceNo
        FROM AreaEquipment
    )
    UPDATE equipment
    SET SequenceNo = numbered.NewSequenceNo
    FROM AreaEquipment AS equipment
    INNER JOIN NumberedEquipment AS numbered ON numbered.Id = equipment.Id;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926105350_AreaEquipmentSequenceNo'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AreaEquipment]') AND [c].[name] = N'SequenceNo');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [AreaEquipment] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [AreaEquipment] ALTER COLUMN [SequenceNo] int NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926105350_AreaEquipmentSequenceNo'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AreaEquipment_AreaId_SequenceNo] ON [AreaEquipment] ([AreaId], [SequenceNo]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926105350_AreaEquipmentSequenceNo'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260926105350_AreaEquipmentSequenceNo', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926115821_ProjectStatus'
)
BEGIN
    ALTER TABLE [Projects] ADD [Status] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926115821_ProjectStatus'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260926115821_ProjectStatus', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    ALTER TABLE [QcReviews] DROP CONSTRAINT [FK_QcReviews_Users_ReviewerId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    DROP INDEX [IX_QcReviews_SurveySessionId] ON [QcReviews];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    EXEC sp_rename N'[QcReviews].[ReviewerId]', N'ReviewedByUserId', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    EXEC sp_rename N'[QcReviews].[Comment]', N'Comments', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    EXEC sp_rename N'[QcReviews].[IX_QcReviews_ReviewerId]', N'IX_QcReviews_ReviewedByUserId', 'INDEX';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    DECLARE @var1 nvarchar(max);
    SELECT @var1 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SurveySessions]') AND [c].[name] = N'JobId');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [SurveySessions] DROP CONSTRAINT ' + @var1 + ';');
    ALTER TABLE [SurveySessions] ALTER COLUMN [JobId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    DECLARE @var2 nvarchar(max);
    SELECT @var2 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SurveySessions]') AND [c].[name] = N'EquipmentId');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [SurveySessions] DROP CONSTRAINT ' + @var2 + ';');
    ALTER TABLE [SurveySessions] ALTER COLUMN [EquipmentId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    ALTER TABLE [SurveySessions] ADD [AreaEquipmentId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    ALTER TABLE [SurveySessions] ADD [DueDate] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    ALTER TABLE [SurveySessions] ADD [InspectionTimestamp] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    ALTER TABLE [SurveySessions] ADD [SurveyorRemarks] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    DECLARE @var3 nvarchar(max);
    SELECT @var3 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[QcReviews]') AND [c].[name] = N'ReviewedAt');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [QcReviews] DROP CONSTRAINT ' + @var3 + ';');
    ALTER TABLE [QcReviews] ALTER COLUMN [ReviewedAt] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    DECLARE @var4 nvarchar(max);
    SELECT @var4 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[QcReviews]') AND [c].[name] = N'Comments');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [QcReviews] DROP CONSTRAINT ' + @var4 + ';');
    ALTER TABLE [QcReviews] ALTER COLUMN [Comments] nvarchar(4000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    ALTER TABLE [QcReviews] ADD [Severity] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    ALTER TABLE [QcReviews] ADD [StartedAt] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    ALTER TABLE [QcReviews] ADD [Status] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    UPDATE [QcReviews] SET [Status] = CASE WHEN [Approved] = 1 THEN 3 ELSE 2 END, [StartedAt] = [ReviewedAt]
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    DECLARE @var5 nvarchar(max);
    SELECT @var5 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[QcReviews]') AND [c].[name] = N'Approved');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [QcReviews] DROP CONSTRAINT ' + @var5 + ';');
    ALTER TABLE [QcReviews] DROP COLUMN [Approved];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    DECLARE @var6 nvarchar(max);
    SELECT @var6 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Files]') AND [c].[name] = N'JobId');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [Files] DROP CONSTRAINT ' + @var6 + ';');
    ALTER TABLE [Files] ALTER COLUMN [JobId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    ALTER TABLE [Files] ADD [SurveySessionId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_SurveySessions_AreaEquipmentId] ON [SurveySessions] ([AreaEquipmentId]) WHERE [AreaEquipmentId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    CREATE UNIQUE INDEX [IX_QcReviews_SurveySessionId] ON [QcReviews] ([SurveySessionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    CREATE INDEX [IX_Files_SurveySessionId] ON [Files] ([SurveySessionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    ALTER TABLE [Files] ADD CONSTRAINT [FK_Files_SurveySessions_SurveySessionId] FOREIGN KEY ([SurveySessionId]) REFERENCES [SurveySessions] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    ALTER TABLE [QcReviews] ADD CONSTRAINT [FK_QcReviews_Users_ReviewedByUserId] FOREIGN KEY ([ReviewedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    ALTER TABLE [SurveySessions] ADD CONSTRAINT [FK_SurveySessions_AreaEquipment_AreaEquipmentId] FOREIGN KEY ([AreaEquipmentId]) REFERENCES [AreaEquipment] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260929070220_QcReviewWorkflow', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929085735_EquipmentInspectionWorkflow'
)
BEGIN
    DECLARE @var7 nvarchar(max);
    SELECT @var7 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SurveySessions]') AND [c].[name] = N'ChecklistTemplateId');
    IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [SurveySessions] DROP CONSTRAINT ' + @var7 + ';');
    ALTER TABLE [SurveySessions] ALTER COLUMN [ChecklistTemplateId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929085735_EquipmentInspectionWorkflow'
)
BEGIN
    ALTER TABLE [SurveySessions] ADD [CreatedAt] datetimeoffset NOT NULL DEFAULT (SYSUTCDATETIME());
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929085735_EquipmentInspectionWorkflow'
)
BEGIN
    ALTER TABLE [SurveySessions] ADD [InspectorName] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929085735_EquipmentInspectionWorkflow'
)
BEGIN
    ALTER TABLE [SurveySessions] ADD [UpdatedAt] datetimeoffset NOT NULL DEFAULT (SYSUTCDATETIME());
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929085735_EquipmentInspectionWorkflow'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260929085735_EquipmentInspectionWorkflow', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006104409_ManagerQcMonitoring'
)
BEGIN
    ALTER TABLE [Users] ADD [ManagerId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006104409_ManagerQcMonitoring'
)
BEGIN
    ALTER TABLE [AuditLogs] ADD [Comments] nvarchar(4000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006104409_ManagerQcMonitoring'
)
BEGIN
    ALTER TABLE [AuditLogs] ADD [QcStatus] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006104409_ManagerQcMonitoring'
)
BEGIN
    ALTER TABLE [AuditLogs] ADD [ReviewStartedAt] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006104409_ManagerQcMonitoring'
)
BEGIN
    ALTER TABLE [AuditLogs] ADD [ReviewedAt] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006104409_ManagerQcMonitoring'
)
BEGIN
    ALTER TABLE [AuditLogs] ADD [Severity] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006104409_ManagerQcMonitoring'
)
BEGIN
    CREATE INDEX [IX_Users_ManagerId] ON [Users] ([ManagerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006104409_ManagerQcMonitoring'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_EntityId_OccurredAt] ON [AuditLogs] ([EntityId], [OccurredAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006104409_ManagerQcMonitoring'
)
BEGIN
    ALTER TABLE [Users] ADD CONSTRAINT [FK_Users_Users_ManagerId] FOREIGN KEY ([ManagerId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006104409_ManagerQcMonitoring'
)
BEGIN
    INSERT INTO AuditLogs (Id, ActorId, EntityType, EntityId, Action, OccurredAt,
        Comments, Severity, QcStatus, ReviewStartedAt, ReviewedAt)
    SELECT NEWID(), ReviewedByUserId, 'QcWorkflow', SurveySessionId,
        'Existing QC Snapshot', COALESCE(ReviewedAt, StartedAt, SYSDATETIMEOFFSET()),
        Comments, Severity, Status, StartedAt, ReviewedAt
    FROM QcReviews;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006104409_ManagerQcMonitoring'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261006104409_ManagerQcMonitoring', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006134251_HazardousAreaPlan'
)
BEGIN
    ALTER TABLE [Projects] ADD [HazardousAreaPlanFileName] nvarchar(255) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006134251_HazardousAreaPlan'
)
BEGIN
    ALTER TABLE [Projects] ADD [HazardousAreaPlanPath] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006134251_HazardousAreaPlan'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261006134251_HazardousAreaPlan', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007050119_OptionalProjectClient'
)
BEGIN
    DECLARE @var8 nvarchar(max);
    SELECT @var8 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Projects]') AND [c].[name] = N'AssignedClientId');
    IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [Projects] DROP CONSTRAINT ' + @var8 + ';');
    ALTER TABLE [Projects] ALTER COLUMN [AssignedClientId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007050119_OptionalProjectClient'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261007050119_OptionalProjectClient', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007120651_ProjectReportMetadataAndClientDetails'
)
BEGIN
    ALTER TABLE [Projects] ADD [ClientLogoPath] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007120651_ProjectReportMetadataAndClientDetails'
)
BEGIN
    ALTER TABLE [Projects] ADD [ClientName] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007120651_ProjectReportMetadataAndClientDetails'
)
BEGIN
    ALTER TABLE [Projects] ADD [Description] nvarchar(4000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007120651_ProjectReportMetadataAndClientDetails'
)
BEGIN
    ALTER TABLE [Projects] ADD [DocumentNumber] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007120651_ProjectReportMetadataAndClientDetails'
)
BEGIN
    ALTER TABLE [Projects] ADD [Revision] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007120651_ProjectReportMetadataAndClientDetails'
)
BEGIN
    ALTER TABLE [Projects] ADD [UidNumber] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007120651_ProjectReportMetadataAndClientDetails'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261007120651_ProjectReportMetadataAndClientDetails', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007131230_OurCompanyMaster'
)
BEGIN
    ALTER TABLE [Projects] ADD [OurCompanyId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007131230_OurCompanyMaster'
)
BEGIN
    CREATE TABLE [CompanyMasters] (
        [Id] uniqueidentifier NOT NULL,
        [CompanyName] nvarchar(300) NOT NULL,
        [DisplayName] nvarchar(300) NOT NULL,
        [Address] nvarchar(2000) NULL,
        [Telephone] nvarchar(150) NULL,
        [Fax] nvarchar(150) NULL,
        [Website] nvarchar(300) NULL,
        [Email] nvarchar(500) NULL,
        [LogoPath] nvarchar(255) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_CompanyMasters] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007131230_OurCompanyMaster'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Address', N'CompanyName', N'CreatedAt', N'DisplayName', N'Email', N'Fax', N'IsActive', N'LogoPath', N'Telephone', N'UpdatedAt', N'Website') AND [object_id] = OBJECT_ID(N'[CompanyMasters]'))
        SET IDENTITY_INSERT [CompanyMasters] ON;
    EXEC(N'INSERT INTO [CompanyMasters] ([Id], [Address], [CompanyName], [CreatedAt], [DisplayName], [Email], [Fax], [IsActive], [LogoPath], [Telephone], [UpdatedAt], [Website])
    VALUES (''235b2bcf-183f-5be0-86ab-0cdb29195920'', N''P.O Box: 24496, 20th Floor, Tower 400  Mina Road, Sharjah, UAE'', N''ARIES MARINE & ENGG. SERVICES'', ''2026-10-07T00:00:00.0000000+00:00'', N''Aries Marine & ENGG .SERVICES - DF'', N''design@ariesgroup.ae, offshoredesign@ariesgroup.ae'', N''+971 (6) 550 3100'', CAST(1 AS bit), N''aries_mar_engg.png'', N''+971 (6) 550 3300'', ''2026-10-07T00:00:00.0000000+00:00'', N''www.ariesmar.com''),
    (''31bdf1cc-fb23-547f-9b85-fbd048a24705'', N''PO. Box: 15368, 3rd Floor, 303-B, Jaidah Square Umm Ghuwailina, 63 Airport Road - Zone 27, Doha, Qatar'', N''ARIES MARINE CO. W.L.L'', ''2026-10-07T00:00:00.0000000+00:00'', N''Aries Marine CO. WLL - QD'', N''designqa@ariesgroup.ae'', NULL, CAST(1 AS bit), N''aries_mar_engg.png'', NULL, ''2026-10-07T00:00:00.0000000+00:00'', N''www.ariesmar.com''),
    (''50875e90-8c8e-57c5-8903-3810fb65ffba'', N''P.O Box: 3965, Warehouse 40 Mussafah Ind.Area, Abu Dhabi, UAE'', N''ARIES OIL FIELD SERVICES L.L.C'', ''2026-10-07T00:00:00.0000000+00:00'', N''Aries Oil Field Services L L .C - DU'', N''designauh@ariesofs.ae'', N''+971 (2) 550 8996'', CAST(1 AS bit), N''aries_mar_engg.png'', N''+971 (2) 550 8893'', ''2026-10-07T00:00:00.0000000+00:00'', N''www.ariesmar.com''),
    (''54601c1b-c9fd-5199-a72e-a169a05970ae'', N''P.O Box: 24496, 20th Floor, Tower 400  Mina Road, Sharjah, UAE'', N''ARIES MARINE & ENGG. SERVICES'', ''2026-10-07T00:00:00.0000000+00:00'', N''Aries Marine & ENGG .SERVICES - DI'', N''ideas@ariesofs.ae'', N''+971 (6) 550 3100'', CAST(1 AS bit), N''aries_mar_engg.png'', N''+971 (6) 550 3300'', ''2026-10-07T00:00:00.0000000+00:00'', N''www.ariesmar.com''),
    (''651862ed-98e7-5a95-9c6f-dce2dbacd941'', N''P.O Box: 94354, AI Qayadah Bldg. Abu Hail, Dubai, UAE'', N''ARIES MARINE L.L.C'', ''2026-10-07T00:00:00.0000000+00:00'', N''Aries Marine L L .C - DS'', N''ariesmarine@ariesgroup.ae'', N''+971 (4) 299 7940'', CAST(1 AS bit), N''aries_mar_engg.png'', N''+971 (4) 299 7939'', ''2026-10-07T00:00:00.0000000+00:00'', N''www.ariesmar.com''),
    (''9003d8ae-58d2-577d-a9b3-d145e9bb738f'', N''P.O Box: 94354, Al Qayadah Bldg. Abu Hail, Dubai, UAE'', N''ARIES MARINE L.L.C'', ''2026-10-07T00:00:00.0000000+00:00'', N''Aries Marine L L .C - DM'', N''dams@ariesgroup.ae'', N''+971 (4) 299 7940'', CAST(1 AS bit), N''aries_mar_engg.png'', N''+971 (4) 299 7939'', ''2026-10-07T00:00:00.0000000+00:00'', N''www.ariesmar.com''),
    (''986c73ab-be16-508b-a484-e2260d267ee5'', N''Business Bay, Office #601, Buiding No. 1260, Road No.  2421, Block No.  324, P.O. Box. 11614, Juffair, Manama, Kingdom of Bahrain'', N''ARIES MARINE WLL'', ''2026-10-07T00:00:00.0000000+00:00'', N''Aries Marine WLL - DB'', N''ariesbahrain@ariesgroup.ae'', NULL, CAST(1 AS bit), N''aries_mar_engg.png'', NULL, ''2026-10-07T00:00:00.0000000+00:00'', N''www.ariesmar.com''),
    (''9caaffe9-ed24-587e-91ea-764cb5876674'', N''#22-03A, International Plaza 10 Anson Road, Singapore 079903'', N''ARIES NETWORK PTE. LTD.'', ''2026-10-07T00:00:00.0000000+00:00'', N''Aries Network PTE .LTD - DG'', N''designsg@ariesgroup.ae'', N''+65 6220 9058'', CAST(1 AS bit), N''aries_mar_engg.png'', N''+65 6220 9056'', ''2026-10-07T00:00:00.0000000+00:00'', N''www.ariesmar.com''),
    (''cc0f18d3-8516-5c7b-99ad-54148151b25f'', N''P.O Box: 24496, 20th Floor, Tower 400  Mina Road, Sharjah, UAE'', N''ARIES MARINE & ENGG. SERVICES'', ''2026-10-07T00:00:00.0000000+00:00'', N''Aries Marine & ENGG .SERVICES - DE'', N''eit@ariesgroup.ae'', N''+971 (6) 550 3100'', CAST(1 AS bit), N''aries_mar_engg.png'', N''+971 (6) 550 3300'', ''2026-10-07T00:00:00.0000000+00:00'', N''www.ariesmar.com''),
    (''d31b47a1-91a5-5acf-b7bf-98610b0fcc2a'', N''P.O Box: 24496, 20th Floor, Tower 400  Mina Road, Sharjah, UAE'', N''ARIES MARINE & ENGG. SERVICES'', ''2026-10-07T00:00:00.0000000+00:00'', N''Aries Marine & ENGG .SERVICES - DN'', N''design@ariesgroup.ae'', N''+971 (6) 550 3100'', CAST(1 AS bit), N''aries_mar_engg.png'', N''+971 (6) 550 3300'', ''2026-10-07T00:00:00.0000000+00:00'', N''www.ariesmar.com''),
    (''fb2c0295-cc98-5af3-90e8-c78d8aee925d'', N''Juffali Building (Fluor Arabia Tower) Office #504, King Fahd Road, Al Khobar 34423 Kingdom of Saudi Arabia'', N''ARIES MARINE SERVICES CO. LTD.'', ''2026-10-07T00:00:00.0000000+00:00'', N''Aries Marine Services CO .LTD - KD'', N''ariesksa@ariesgroup.ae'', NULL, CAST(1 AS bit), N''aries_mar_engg.png'', NULL, ''2026-10-07T00:00:00.0000000+00:00'', N''www.ariesmar.com'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Address', N'CompanyName', N'CreatedAt', N'DisplayName', N'Email', N'Fax', N'IsActive', N'LogoPath', N'Telephone', N'UpdatedAt', N'Website') AND [object_id] = OBJECT_ID(N'[CompanyMasters]'))
        SET IDENTITY_INSERT [CompanyMasters] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007131230_OurCompanyMaster'
)
BEGIN
    CREATE INDEX [IX_Projects_OurCompanyId] ON [Projects] ([OurCompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007131230_OurCompanyMaster'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CompanyMasters_DisplayName] ON [CompanyMasters] ([DisplayName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007131230_OurCompanyMaster'
)
BEGIN
    ALTER TABLE [Projects] ADD CONSTRAINT [FK_Projects_CompanyMasters_OurCompanyId] FOREIGN KEY ([OurCompanyId]) REFERENCES [CompanyMasters] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007131230_OurCompanyMaster'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261007131230_OurCompanyMaster', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009050035_ClientRemarks'
)
BEGIN
    CREATE TABLE [ReportClientRemarks] (
        [Id] uniqueidentifier NOT NULL,
        [ClientUserId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [EquipmentId] uniqueidentifier NULL,
        [FindingId] uniqueidentifier NULL,
        [ReportId] uniqueidentifier NULL,
        [AgainstReport] bit NOT NULL,
        [Remark] nvarchar(4000) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [IsRead] bit NOT NULL,
        [ReadAt] datetimeoffset NULL,
        CONSTRAINT [PK_ReportClientRemarks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ReportClientRemarks_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ReportClientRemarks_Users_ClientUserId] FOREIGN KEY ([ClientUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009050035_ClientRemarks'
)
BEGIN
    CREATE INDEX [IX_ReportClientRemarks_ClientUserId] ON [ReportClientRemarks] ([ClientUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009050035_ClientRemarks'
)
BEGIN
    CREATE INDEX [IX_ReportClientRemarks_ProjectId_CreatedAt] ON [ReportClientRemarks] ([ProjectId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009050035_ClientRemarks'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261009050035_ClientRemarks', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027090000_AddEquipmentRfidTagUid'
)
BEGIN
    ALTER TABLE [AreaEquipment] ADD [RfidTagUid] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027090000_AddEquipmentRfidTagUid'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_AreaEquipment_RfidTagUid] ON [AreaEquipment] ([RfidTagUid]) WHERE [RfidTagUid] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027090000_AddEquipmentRfidTagUid'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261027090000_AddEquipmentRfidTagUid', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029110000_AddOtpVerificationSecurity'
)
BEGIN
    ALTER TABLE [RegistrationOtps] ADD [VerificationAttempts] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029110000_AddOtpVerificationSecurity'
)
BEGIN
    ALTER TABLE [RegistrationOtps] ADD [LockedUntil] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029110000_AddOtpVerificationSecurity'
)
BEGIN
    ALTER TABLE [RegistrationOtps] ADD [ConcurrencyVersion] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029110000_AddOtpVerificationSecurity'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261029110000_AddOtpVerificationSecurity', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029123545_Phase2aTenantFoundation'
)
BEGIN
    CREATE TABLE [Tenants] (
        [Id] uniqueidentifier NOT NULL,
        [TenantKey] nvarchar(100) NOT NULL,
        [DisplayName] nvarchar(200) NOT NULL,
        [Status] nvarchar(32) NOT NULL DEFAULT N'Provisioning',
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_Tenants] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029123545_Phase2aTenantFoundation'
)
BEGIN
    CREATE TABLE [LegacyEntityMaps] (
        [Id] uniqueidentifier NOT NULL,
        [SourceSystem] nvarchar(100) NOT NULL,
        [SourceEntityType] nvarchar(100) NOT NULL,
        [SourceRecordKey] nvarchar(200) NOT NULL,
        [TargetEntityType] nvarchar(100) NULL,
        [TargetId] uniqueidentifier NULL,
        [TenantId] uniqueidentifier NULL,
        [Status] nvarchar(24) NOT NULL DEFAULT N'Proposed',
        [EvidenceReference] nvarchar(500) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_LegacyEntityMaps] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_LegacyEntityMaps_VerifiedTarget] CHECK ([Status] <> N'Verified' OR ([TargetEntityType] IS NOT NULL AND [TargetId] IS NOT NULL)),
        CONSTRAINT [FK_LegacyEntityMaps_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029123545_Phase2aTenantFoundation'
)
BEGIN
    CREATE TABLE [TenantBackfillExceptions] (
        [Id] uniqueidentifier NOT NULL,
        [MigrationBatchId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NULL,
        [SourceSystem] nvarchar(100) NOT NULL,
        [SourceEntityType] nvarchar(100) NOT NULL,
        [SourceRecordKey] nvarchar(200) NOT NULL,
        [ExceptionCode] nvarchar(80) NOT NULL,
        [Description] nvarchar(2000) NOT NULL,
        [Status] nvarchar(24) NOT NULL DEFAULT N'Open',
        [EvidenceReference] nvarchar(500) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ResolvedAt] datetimeoffset NULL,
        CONSTRAINT [PK_TenantBackfillExceptions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TenantBackfillExceptions_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029123545_Phase2aTenantFoundation'
)
BEGIN
    CREATE TABLE [TenantContracts] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ContractKey] nvarchar(120) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [StatusCode] nvarchar(40) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_TenantContracts] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_TenantContracts_TenantId_Id] UNIQUE ([TenantId], [Id]),
        CONSTRAINT [FK_TenantContracts_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029123545_Phase2aTenantFoundation'
)
BEGIN
    CREATE TABLE [TenantMemberships] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Status] nvarchar(32) NOT NULL DEFAULT N'Invited',
        [InvitedByUserId] uniqueidentifier NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ActivatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_TenantMemberships] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_TenantMemberships_TenantId_Id] UNIQUE ([TenantId], [Id]),
        CONSTRAINT [FK_TenantMemberships_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TenantMemberships_Users_InvitedByUserId] FOREIGN KEY ([InvitedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TenantMemberships_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029123545_Phase2aTenantFoundation'
)
BEGIN
    CREATE TABLE [TenantProjectBindings] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ContractId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_TenantProjectBindings] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_TenantProjectBindings_TenantId_Id] UNIQUE ([TenantId], [Id]),
        CONSTRAINT [FK_TenantProjectBindings_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TenantProjectBindings_TenantContracts_TenantId_ContractId] FOREIGN KEY ([TenantId], [ContractId]) REFERENCES [TenantContracts] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029123545_Phase2aTenantFoundation'
)
BEGIN
    CREATE TABLE [TenantRoleGrants] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [TenantMembershipId] uniqueidentifier NOT NULL,
        [RoleCode] nvarchar(80) NOT NULL,
        [ScopeType] nvarchar(24) NOT NULL,
        [ScopeId] uniqueidentifier NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_TenantRoleGrants] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_TenantRoleGrants_ScopeShape] CHECK (([ScopeType] = N'Tenant' AND [ScopeId] IS NULL) OR ([ScopeType] <> N'Tenant' AND [ScopeId] IS NOT NULL)),
        CONSTRAINT [FK_TenantRoleGrants_TenantMemberships_TenantId_TenantMembershipId] FOREIGN KEY ([TenantId], [TenantMembershipId]) REFERENCES [TenantMemberships] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029123545_Phase2aTenantFoundation'
)
BEGIN
    CREATE TABLE [TenantSites] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [TenantProjectBindingId] uniqueidentifier NOT NULL,
        [SiteKey] nvarchar(120) NULL,
        [Name] nvarchar(200) NOT NULL,
        [SiteType] nvarchar(40) NULL,
        [Location] nvarchar(300) NULL,
        [Status] nvarchar(32) NOT NULL DEFAULT N'Provisioning',
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_TenantSites] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_TenantSites_TenantId_Id] UNIQUE ([TenantId], [Id]),
        CONSTRAINT [FK_TenantSites_TenantProjectBindings_TenantId_TenantProjectBindingId] FOREIGN KEY ([TenantId], [TenantProjectBindingId]) REFERENCES [TenantProjectBindings] ([TenantId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TenantSites_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029123545_Phase2aTenantFoundation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_LegacyEntityMaps_SourceSystem_SourceEntityType_SourceRecordKey] ON [LegacyEntityMaps] ([SourceSystem], [SourceEntityType], [SourceRecordKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029123545_Phase2aTenantFoundation'
)
BEGIN
    CREATE INDEX [IX_LegacyEntityMaps_TenantId_TargetEntityType_TargetId] ON [LegacyEntityMaps] ([TenantId], [TargetEntityType], [TargetId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029123545_Phase2aTenantFoundation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TenantBackfillExceptions_MigrationBatchId_SourceSystem_SourceEntityType_SourceRecordKey_ExceptionCode] ON [TenantBackfillExceptions] ([MigrationBatchId], [SourceSystem], [SourceEntityType], [SourceRecordKey], [ExceptionCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029123545_Phase2aTenantFoundation'
)
BEGIN
    CREATE INDEX [IX_TenantBackfillExceptions_TenantId_Status_CreatedAt] ON [TenantBackfillExceptions] ([TenantId], [Status], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029123545_Phase2aTenantFoundation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TenantContracts_TenantId_ContractKey] ON [TenantContracts] ([TenantId], [ContractKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029123545_Phase2aTenantFoundation'
)
BEGIN
    CREATE INDEX [IX_TenantMemberships_InvitedByUserId] ON [TenantMemberships] ([InvitedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029123545_Phase2aTenantFoundation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TenantMemberships_TenantId_UserId] ON [TenantMemberships] ([TenantId], [UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029123545_Phase2aTenantFoundation'
)
BEGIN
    CREATE INDEX [IX_TenantMemberships_UserId] ON [TenantMemberships] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029123545_Phase2aTenantFoundation'
)
BEGIN
    CREATE INDEX [IX_TenantProjectBindings_ProjectId] ON [TenantProjectBindings] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029123545_Phase2aTenantFoundation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TenantProjectBindings_TenantId_ContractId_ProjectId] ON [TenantProjectBindings] ([TenantId], [ContractId], [ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029123545_Phase2aTenantFoundation'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_TenantRoleGrants_TenantId_TenantMembershipId_RoleCode_ScopeType_ScopeId] ON [TenantRoleGrants] ([TenantId], [TenantMembershipId], [RoleCode], [ScopeType], [ScopeId]) WHERE [ScopeId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029123545_Phase2aTenantFoundation'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_TenantRoleGrants_TenantId_TenantMembershipId_RoleCode_ScopeType] ON [TenantRoleGrants] ([TenantId], [TenantMembershipId], [RoleCode], [ScopeType]) WHERE [ScopeType] = N''Tenant''');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029123545_Phase2aTenantFoundation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Tenants_TenantKey] ON [Tenants] ([TenantKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029123545_Phase2aTenantFoundation'
)
BEGIN
    CREATE INDEX [IX_TenantSites_TenantId_TenantProjectBindingId] ON [TenantSites] ([TenantId], [TenantProjectBindingId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029123545_Phase2aTenantFoundation'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261029123545_Phase2aTenantFoundation', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030104113_InspectionActivityFoundation'
)
BEGIN
    CREATE TABLE [ChecklistVersionSnapshots] (
        [Id] uniqueidentifier NOT NULL,
        [SourceChecklistTemplateId] uniqueidentifier NOT NULL,
        [VersionNumber] int NOT NULL,
        [InspectionType] int NOT NULL,
        [SnapshotHash] nvarchar(128) NOT NULL,
        [PublishedAt] datetimeoffset NOT NULL,
        [PublishedByUserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_ChecklistVersionSnapshots] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ChecklistVersionSnapshots_ChecklistTemplates_SourceChecklistTemplateId] FOREIGN KEY ([SourceChecklistTemplateId]) REFERENCES [ChecklistTemplates] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ChecklistVersionSnapshots_Users_PublishedByUserId] FOREIGN KEY ([PublishedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030104113_InspectionActivityFoundation'
)
BEGIN
    CREATE TABLE [InspectionPlans] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NULL,
        [Name] nvarchar(200) NOT NULL,
        [SourceReference] nvarchar(200) NULL,
        [StartsAt] datetimeoffset NULL,
        [EndsAt] datetimeoffset NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_InspectionPlans] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InspectionPlans_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030104113_InspectionActivityFoundation'
)
BEGIN
    CREATE TABLE [CheckpointDefinitionSnapshots] (
        [Id] uniqueidentifier NOT NULL,
        [ChecklistVersionSnapshotId] uniqueidentifier NOT NULL,
        [QuestionCode] nvarchar(100) NOT NULL,
        [QuestionVersion] int NOT NULL,
        [QuestionText] nvarchar(4000) NOT NULL,
        [SortOrder] int NOT NULL,
        [ApplicabilityCode] nvarchar(100) NULL,
        [ResponseScaleCode] nvarchar(100) NULL,
        [IsRequired] bit NOT NULL,
        CONSTRAINT [PK_CheckpointDefinitionSnapshots] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CheckpointDefinitionSnapshots_ChecklistVersionSnapshots_ChecklistVersionSnapshotId] FOREIGN KEY ([ChecklistVersionSnapshotId]) REFERENCES [ChecklistVersionSnapshots] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030104113_InspectionActivityFoundation'
)
BEGIN
    CREATE TABLE [InspectionTasks] (
        [Id] uniqueidentifier NOT NULL,
        [InspectionPlanId] uniqueidentifier NOT NULL,
        [AreaEquipmentId] uniqueidentifier NULL,
        [LegacyEquipmentId] uniqueidentifier NULL,
        [Kind] int NOT NULL,
        [ScheduleReference] nvarchar(200) NULL,
        [WorkPackageReference] nvarchar(200) NULL,
        [AssignedUserId] uniqueidentifier NULL,
        [ScheduledAt] datetimeoffset NULL,
        [StatusCode] nvarchar(40) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_InspectionTasks] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_InspectionTasks_OneEquipment] CHECK (([AreaEquipmentId] IS NOT NULL AND [LegacyEquipmentId] IS NULL) OR ([AreaEquipmentId] IS NULL AND [LegacyEquipmentId] IS NOT NULL)),
        CONSTRAINT [FK_InspectionTasks_AreaEquipment_AreaEquipmentId] FOREIGN KEY ([AreaEquipmentId]) REFERENCES [AreaEquipment] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_InspectionTasks_Equipment_LegacyEquipmentId] FOREIGN KEY ([LegacyEquipmentId]) REFERENCES [Equipment] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_InspectionTasks_InspectionPlans_InspectionPlanId] FOREIGN KEY ([InspectionPlanId]) REFERENCES [InspectionPlans] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_InspectionTasks_Users_AssignedUserId] FOREIGN KEY ([AssignedUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030104113_InspectionActivityFoundation'
)
BEGIN
    CREATE TABLE [InspectionActivities] (
        [Id] uniqueidentifier NOT NULL,
        [InspectionTaskId] uniqueidentifier NOT NULL,
        [LegacySurveySessionId] uniqueidentifier NULL,
        [Grade] int NOT NULL,
        [InspectorUserId] uniqueidentifier NOT NULL,
        [InspectorNameSnapshot] nvarchar(200) NULL,
        [StartedAt] datetimeoffset NOT NULL,
        [CompletedAt] datetimeoffset NULL,
        [ChecklistVersionId] uniqueidentifier NOT NULL,
        [ReviewStatus] int NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_InspectionActivities] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InspectionActivities_ChecklistVersionSnapshots_ChecklistVersionId] FOREIGN KEY ([ChecklistVersionId]) REFERENCES [ChecklistVersionSnapshots] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_InspectionActivities_InspectionTasks_InspectionTaskId] FOREIGN KEY ([InspectionTaskId]) REFERENCES [InspectionTasks] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_InspectionActivities_SurveySessions_LegacySurveySessionId] FOREIGN KEY ([LegacySurveySessionId]) REFERENCES [SurveySessions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_InspectionActivities_Users_InspectorUserId] FOREIGN KEY ([InspectorUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030104113_InspectionActivityFoundation'
)
BEGIN
    CREATE TABLE [ActivityReviewEvents] (
        [Id] uniqueidentifier NOT NULL,
        [InspectionActivityId] uniqueidentifier NOT NULL,
        [Decision] int NOT NULL,
        [Comments] nvarchar(4000) NULL,
        [ActorUserId] uniqueidentifier NOT NULL,
        [DecidedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_ActivityReviewEvents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ActivityReviewEvents_InspectionActivities_InspectionActivityId] FOREIGN KEY ([InspectionActivityId]) REFERENCES [InspectionActivities] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ActivityReviewEvents_Users_ActorUserId] FOREIGN KEY ([ActorUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030104113_InspectionActivityFoundation'
)
BEGIN
    CREATE TABLE [InspectionActivityRevisions] (
        [Id] uniqueidentifier NOT NULL,
        [InspectionActivityId] uniqueidentifier NOT NULL,
        [RevisionNumber] int NOT NULL,
        [ChangeReason] nvarchar(1000) NULL,
        [ContentHash] nvarchar(128) NOT NULL,
        [CreatedByUserId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [InspectionActivityId1] uniqueidentifier NULL,
        CONSTRAINT [PK_InspectionActivityRevisions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InspectionActivityRevisions_InspectionActivities_InspectionActivityId] FOREIGN KEY ([InspectionActivityId]) REFERENCES [InspectionActivities] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_InspectionActivityRevisions_InspectionActivities_InspectionActivityId1] FOREIGN KEY ([InspectionActivityId1]) REFERENCES [InspectionActivities] ([Id]),
        CONSTRAINT [FK_InspectionActivityRevisions_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030104113_InspectionActivityFoundation'
)
BEGIN
    CREATE TABLE [CheckpointResponseRevisions] (
        [Id] uniqueidentifier NOT NULL,
        [ActivityRevisionId] uniqueidentifier NOT NULL,
        [CheckpointDefinitionSnapshotId] uniqueidentifier NOT NULL,
        [RevisionNumber] int NOT NULL,
        [ApplicabilityResultCode] nvarchar(100) NULL,
        [ResponseCode] nvarchar(100) NULL,
        [TechnicalConditionCode] nvarchar(100) NULL,
        [WorkflowState] int NOT NULL,
        [Observation] nvarchar(4000) NULL,
        [RecordedAt] datetimeoffset NOT NULL,
        [RecordedByUserId] uniqueidentifier NOT NULL,
        [ReviewedByUserId] uniqueidentifier NULL,
        [ReviewedAt] datetimeoffset NULL,
        [ReviewComment] nvarchar(4000) NULL,
        CONSTRAINT [PK_CheckpointResponseRevisions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CheckpointResponseRevisions_CheckpointDefinitionSnapshots_CheckpointDefinitionSnapshotId] FOREIGN KEY ([CheckpointDefinitionSnapshotId]) REFERENCES [CheckpointDefinitionSnapshots] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CheckpointResponseRevisions_InspectionActivityRevisions_ActivityRevisionId] FOREIGN KEY ([ActivityRevisionId]) REFERENCES [InspectionActivityRevisions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CheckpointResponseRevisions_Users_RecordedByUserId] FOREIGN KEY ([RecordedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CheckpointResponseRevisions_Users_ReviewedByUserId] FOREIGN KEY ([ReviewedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030104113_InspectionActivityFoundation'
)
BEGIN
    CREATE TABLE [ActivityFindings] (
        [Id] uniqueidentifier NOT NULL,
        [InspectionActivityId] uniqueidentifier NOT NULL,
        [CheckpointResponseRevisionId] uniqueidentifier NOT NULL,
        [LegacyFindingId] uniqueidentifier NULL,
        [Observation] nvarchar(4000) NOT NULL,
        [ProposedAction] nvarchar(4000) NULL,
        [SeverityCode] nvarchar(100) NULL,
        [StatusCode] nvarchar(40) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_ActivityFindings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ActivityFindings_CheckpointResponseRevisions_CheckpointResponseRevisionId] FOREIGN KEY ([CheckpointResponseRevisionId]) REFERENCES [CheckpointResponseRevisions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ActivityFindings_Findings_LegacyFindingId] FOREIGN KEY ([LegacyFindingId]) REFERENCES [Findings] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ActivityFindings_InspectionActivities_InspectionActivityId] FOREIGN KEY ([InspectionActivityId]) REFERENCES [InspectionActivities] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030104113_InspectionActivityFoundation'
)
BEGIN
    CREATE TABLE [CorrectiveActions] (
        [Id] uniqueidentifier NOT NULL,
        [ActivityFindingId] uniqueidentifier NOT NULL,
        [ActionReference] nvarchar(100) NOT NULL,
        [SupplementaryInformation] nvarchar(4000) NULL,
        [ProposedAction] nvarchar(4000) NULL,
        [ExternalWorkOrderReference] nvarchar(200) NULL,
        [ResponsibleUserId] uniqueidentifier NULL,
        [Status] int NOT NULL,
        [AriesVerifierUserId] uniqueidentifier NULL,
        [AriesVerifiedAt] datetimeoffset NULL,
        [ClientDecisionUserId] uniqueidentifier NULL,
        [ClientDecisionAt] datetimeoffset NULL,
        [LastDecisionComment] nvarchar(4000) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_CorrectiveActions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CorrectiveActions_ActivityFindings_ActivityFindingId] FOREIGN KEY ([ActivityFindingId]) REFERENCES [ActivityFindings] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CorrectiveActions_Users_AriesVerifierUserId] FOREIGN KEY ([AriesVerifierUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CorrectiveActions_Users_ClientDecisionUserId] FOREIGN KEY ([ClientDecisionUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CorrectiveActions_Users_ResponsibleUserId] FOREIGN KEY ([ResponsibleUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030104113_InspectionActivityFoundation'
)
BEGIN
    CREATE TABLE [CorrectiveActionEvents] (
        [Id] uniqueidentifier NOT NULL,
        [CorrectiveActionId] uniqueidentifier NOT NULL,
        [Kind] int NOT NULL,
        [ResultingStatus] int NOT NULL,
        [ActorUserId] uniqueidentifier NOT NULL,
        [Comment] nvarchar(4000) NULL,
        [OccurredAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_CorrectiveActionEvents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CorrectiveActionEvents_CorrectiveActions_CorrectiveActionId] FOREIGN KEY ([CorrectiveActionId]) REFERENCES [CorrectiveActions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CorrectiveActionEvents_Users_ActorUserId] FOREIGN KEY ([ActorUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030104113_InspectionActivityFoundation'
)
BEGIN
    CREATE TABLE [ActivityEvidence] (
        [Id] uniqueidentifier NOT NULL,
        [SurveyFileId] uniqueidentifier NOT NULL,
        [CheckpointResponseRevisionId] uniqueidentifier NULL,
        [CorrectiveActionEventId] uniqueidentifier NULL,
        [Role] int NOT NULL,
        [Caption] nvarchar(1000) NULL,
        [IntegrityHash] nvarchar(128) NULL,
        [AddedByUserId] uniqueidentifier NOT NULL,
        [AddedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_ActivityEvidence] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ActivityEvidence_CheckpointResponseRevisions_CheckpointResponseRevisionId] FOREIGN KEY ([CheckpointResponseRevisionId]) REFERENCES [CheckpointResponseRevisions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ActivityEvidence_CorrectiveActionEvents_CorrectiveActionEventId] FOREIGN KEY ([CorrectiveActionEventId]) REFERENCES [CorrectiveActionEvents] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ActivityEvidence_Files_SurveyFileId] FOREIGN KEY ([SurveyFileId]) REFERENCES [Files] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ActivityEvidence_Users_AddedByUserId] FOREIGN KEY ([AddedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030104113_InspectionActivityFoundation'
)
BEGIN
    CREATE INDEX [IX_ActivityEvidence_AddedByUserId] ON [ActivityEvidence] ([AddedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030104113_InspectionActivityFoundation'
)
BEGIN
    CREATE INDEX [IX_ActivityEvidence_CheckpointResponseRevisionId] ON [ActivityEvidence] ([CheckpointResponseRevisionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030104113_InspectionActivityFoundation'
)
BEGIN
    CREATE INDEX [IX_ActivityEvidence_CorrectiveActionEventId] ON [ActivityEvidence] ([CorrectiveActionEventId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030104113_InspectionActivityFoundation'
)
BEGIN
    CREATE INDEX [IX_ActivityEvidence_SurveyFileId] ON [ActivityEvidence] ([SurveyFileId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030104113_InspectionActivityFoundation'
)
BEGIN
    CREATE INDEX [IX_ActivityFindings_CheckpointResponseRevisionId] ON [ActivityFindings] ([CheckpointResponseRevisionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030104113_InspectionActivityFoundation'
)
BEGIN
    CREATE INDEX [IX_ActivityFindings_InspectionActivityId] ON [ActivityFindings] ([InspectionActivityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030104113_InspectionActivityFoundation'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ActivityFindings_LegacyFindingId] ON [ActivityFindings] ([LegacyFindingId]) WHERE [LegacyFindingId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030104113_InspectionActivityFoundation'
)
BEGIN
    CREATE INDEX [IX_ActivityReviewEvents_InspectionActivityId_DecidedAt] ON [ActivityReviewEvents] ([InspectionActivityId], [DecidedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030104113_InspectionActivityFoundation'
)
BEGIN
    CREATE INDEX [IX_ActivityReviewEvents_ActorUserId] ON [ActivityReviewEvents] ([ActorUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030104113_InspectionActivityFoundation'
)
BEGIN
    CREATE INDEX [IX_ChecklistVersionSnapshots_PublishedByUserId] ON [ChecklistVersionSnapshots] ([PublishedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030104113_InspectionActivityFoundation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ChecklistVersionSnapshots_SourceChecklistTemplateId_VersionNumber] ON [ChecklistVersionSnapshots] ([SourceChecklistTemplateId], [VersionNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030104113_InspectionActivityFoundation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CheckpointDefinitionSnapshots_ChecklistVersionSnapshotId_QuestionCode_QuestionVersion] ON [CheckpointDefinitionSnapshots] ([ChecklistVersionSnapshotId], [QuestionCode], [QuestionVersion]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030104113_InspectionActivityFoundation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CheckpointResponseRevisions_ActivityRevisionId_CheckpointDefinitionSnapshotId_RevisionNumber] ON [CheckpointResponseRevisions] ([ActivityRevisionId], [CheckpointDefinitionSnapshotId], [RevisionNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030104113_InspectionActivityFoundation'
)
BEGIN
    CREATE INDEX [IX_CheckpointResponseRevisions_CheckpointDefinitionSnapshotId] ON [CheckpointResponseRevisions] ([CheckpointDefinitionSnapshotId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030104113_InspectionActivityFoundation'
)
BEGIN
    CREATE INDEX [IX_CheckpointResponseRevisions_RecordedByUserId] ON [CheckpointResponseRevisions] ([RecordedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030104113_InspectionActivityFoundation'
)
BEGIN
    CREATE INDEX [IX_CheckpointResponseRevisions_ReviewedByUserId] ON [CheckpointResponseRevisions] ([ReviewedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030104113_InspectionActivityFoundation'
)
BEGIN
    CREATE INDEX [IX_CorrectiveActionEvents_ActorUserId] ON [CorrectiveActionEvents] ([ActorUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030104113_InspectionActivityFoundation'
)
BEGIN
    CREATE INDEX [IX_CorrectiveActionEvents_CorrectiveActionId_OccurredAt] ON [CorrectiveActionEvents] ([CorrectiveActionId], [OccurredAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030104113_InspectionActivityFoundation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CorrectiveActions_ActionReference] ON [CorrectiveActions] ([ActionReference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030104113_InspectionActivityFoundation'
)
BEGIN
    CREATE INDEX [IX_CorrectiveActions_ActivityFindingId_CreatedAt] ON [CorrectiveActions] ([ActivityFindingId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030104113_InspectionActivityFoundation'
)
BEGIN
    CREATE INDEX [IX_CorrectiveActions_AriesVerifierUserId] ON [CorrectiveActions] ([AriesVerifierUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030104113_InspectionActivityFoundation'
)
BEGIN
    CREATE INDEX [IX_CorrectiveActions_ClientDecisionUserId] ON [CorrectiveActions] ([ClientDecisionUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030104113_InspectionActivityFoundation'
)
BEGIN
    CREATE INDEX [IX_CorrectiveActions_ResponsibleUserId] ON [CorrectiveActions] ([ResponsibleUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030104113_InspectionActivityFoundation'
)
BEGIN
    CREATE INDEX [IX_InspectionActivities_ChecklistVersionId] ON [InspectionActivities] ([ChecklistVersionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030104113_InspectionActivityFoundation'
)
BEGIN
    CREATE INDEX [IX_InspectionActivities_InspectionTaskId] ON [InspectionActivities] ([InspectionTaskId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030104113_InspectionActivityFoundation'
)
BEGIN
    CREATE INDEX [IX_InspectionActivities_InspectorUserId] ON [InspectionActivities] ([InspectorUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030104113_InspectionActivityFoundation'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_InspectionActivities_LegacySurveySessionId] ON [InspectionActivities] ([LegacySurveySessionId]) WHERE [LegacySurveySessionId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030104113_InspectionActivityFoundation'
)
BEGIN
    CREATE INDEX [IX_InspectionActivityRevisions_CreatedByUserId] ON [InspectionActivityRevisions] ([CreatedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030104113_InspectionActivityFoundation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_InspectionActivityRevisions_InspectionActivityId_RevisionNumber] ON [InspectionActivityRevisions] ([InspectionActivityId], [RevisionNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030104113_InspectionActivityFoundation'
)
BEGIN
    CREATE INDEX [IX_InspectionActivityRevisions_InspectionActivityId1] ON [InspectionActivityRevisions] ([InspectionActivityId1]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030104113_InspectionActivityFoundation'
)
BEGIN
    CREATE INDEX [IX_InspectionPlans_ProjectId] ON [InspectionPlans] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030104113_InspectionActivityFoundation'
)
BEGIN
    CREATE INDEX [IX_InspectionTasks_AreaEquipmentId] ON [InspectionTasks] ([AreaEquipmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030104113_InspectionActivityFoundation'
)
BEGIN
    CREATE INDEX [IX_InspectionTasks_AssignedUserId] ON [InspectionTasks] ([AssignedUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030104113_InspectionActivityFoundation'
)
BEGIN
    CREATE INDEX [IX_InspectionTasks_InspectionPlanId_ScheduledAt] ON [InspectionTasks] ([InspectionPlanId], [ScheduledAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030104113_InspectionActivityFoundation'
)
BEGIN
    CREATE INDEX [IX_InspectionTasks_LegacyEquipmentId] ON [InspectionTasks] ([LegacyEquipmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030104113_InspectionActivityFoundation'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261030104113_InspectionActivityFoundation', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030113002_Sprint2ActivityMeasurementsEvidenceAndCheckpointReviews'
)
BEGIN
    ALTER TABLE [CheckpointResponseRevisions] ADD [MeasurementUnitSnapshot] nvarchar(40) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030113002_Sprint2ActivityMeasurementsEvidenceAndCheckpointReviews'
)
BEGIN
    ALTER TABLE [CheckpointResponseRevisions] ADD [NumericValue] decimal(18,4) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030113002_Sprint2ActivityMeasurementsEvidenceAndCheckpointReviews'
)
BEGIN
    ALTER TABLE [CheckpointDefinitionSnapshots] ADD [MaximumReading] decimal(18,4) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030113002_Sprint2ActivityMeasurementsEvidenceAndCheckpointReviews'
)
BEGIN
    ALTER TABLE [CheckpointDefinitionSnapshots] ADD [MeasurementUnit] nvarchar(40) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030113002_Sprint2ActivityMeasurementsEvidenceAndCheckpointReviews'
)
BEGIN
    ALTER TABLE [CheckpointDefinitionSnapshots] ADD [MinimumReading] decimal(18,4) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030113002_Sprint2ActivityMeasurementsEvidenceAndCheckpointReviews'
)
BEGIN
    ALTER TABLE [CheckpointDefinitionSnapshots] ADD [NumericReadingEnabled] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030113002_Sprint2ActivityMeasurementsEvidenceAndCheckpointReviews'
)
BEGIN
    ALTER TABLE [CheckpointDefinitionSnapshots] ADD [NumericReadingRequired] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030113002_Sprint2ActivityMeasurementsEvidenceAndCheckpointReviews'
)
BEGIN
    ALTER TABLE [ChecklistQuestions] ADD [MaximumReading] decimal(18,4) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030113002_Sprint2ActivityMeasurementsEvidenceAndCheckpointReviews'
)
BEGIN
    ALTER TABLE [ChecklistQuestions] ADD [MeasurementUnit] nvarchar(40) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030113002_Sprint2ActivityMeasurementsEvidenceAndCheckpointReviews'
)
BEGIN
    ALTER TABLE [ChecklistQuestions] ADD [MinimumReading] decimal(18,4) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030113002_Sprint2ActivityMeasurementsEvidenceAndCheckpointReviews'
)
BEGIN
    ALTER TABLE [ChecklistQuestions] ADD [NumericReadingEnabled] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030113002_Sprint2ActivityMeasurementsEvidenceAndCheckpointReviews'
)
BEGIN
    ALTER TABLE [ChecklistQuestions] ADD [NumericReadingRequired] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030113002_Sprint2ActivityMeasurementsEvidenceAndCheckpointReviews'
)
BEGIN
    ALTER TABLE [ActivityEvidence] ADD [AreaEquipmentId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030113002_Sprint2ActivityMeasurementsEvidenceAndCheckpointReviews'
)
BEGIN
    ALTER TABLE [ActivityEvidence] ADD [InspectionActivityId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030113002_Sprint2ActivityMeasurementsEvidenceAndCheckpointReviews'
)
BEGIN
    ALTER TABLE [ActivityEvidence] ADD [LegacyEquipmentId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030113002_Sprint2ActivityMeasurementsEvidenceAndCheckpointReviews'
)
BEGIN
    CREATE TABLE [CheckpointReviewEvents] (
        [Id] uniqueidentifier NOT NULL,
        [InspectionActivityId] uniqueidentifier NOT NULL,
        [CheckpointResponseRevisionId] uniqueidentifier NOT NULL,
        [ReviewerUserId] uniqueidentifier NOT NULL,
        [Decision] int NOT NULL,
        [Comments] nvarchar(4000) NULL,
        [ReviewedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_CheckpointReviewEvents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CheckpointReviewEvents_CheckpointResponseRevisions_CheckpointResponseRevisionId] FOREIGN KEY ([CheckpointResponseRevisionId]) REFERENCES [CheckpointResponseRevisions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CheckpointReviewEvents_InspectionActivities_InspectionActivityId] FOREIGN KEY ([InspectionActivityId]) REFERENCES [InspectionActivities] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CheckpointReviewEvents_Users_ReviewerUserId] FOREIGN KEY ([ReviewerUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030113002_Sprint2ActivityMeasurementsEvidenceAndCheckpointReviews'
)
BEGIN
    CREATE INDEX [IX_ActivityEvidence_AreaEquipmentId] ON [ActivityEvidence] ([AreaEquipmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030113002_Sprint2ActivityMeasurementsEvidenceAndCheckpointReviews'
)
BEGIN
    CREATE INDEX [IX_ActivityEvidence_InspectionActivityId_AddedAt] ON [ActivityEvidence] ([InspectionActivityId], [AddedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030113002_Sprint2ActivityMeasurementsEvidenceAndCheckpointReviews'
)
BEGIN
    CREATE INDEX [IX_ActivityEvidence_LegacyEquipmentId] ON [ActivityEvidence] ([LegacyEquipmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030113002_Sprint2ActivityMeasurementsEvidenceAndCheckpointReviews'
)
BEGIN
    CREATE INDEX [IX_CheckpointReviewEvents_CheckpointResponseRevisionId] ON [CheckpointReviewEvents] ([CheckpointResponseRevisionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030113002_Sprint2ActivityMeasurementsEvidenceAndCheckpointReviews'
)
BEGIN
    CREATE INDEX [IX_CheckpointReviewEvents_InspectionActivityId_ReviewedAt] ON [CheckpointReviewEvents] ([InspectionActivityId], [ReviewedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030113002_Sprint2ActivityMeasurementsEvidenceAndCheckpointReviews'
)
BEGIN
    CREATE INDEX [IX_CheckpointReviewEvents_ReviewerUserId] ON [CheckpointReviewEvents] ([ReviewerUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030113002_Sprint2ActivityMeasurementsEvidenceAndCheckpointReviews'
)
BEGIN
    ALTER TABLE [ActivityEvidence] ADD CONSTRAINT [FK_ActivityEvidence_AreaEquipment_AreaEquipmentId] FOREIGN KEY ([AreaEquipmentId]) REFERENCES [AreaEquipment] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030113002_Sprint2ActivityMeasurementsEvidenceAndCheckpointReviews'
)
BEGIN
    ALTER TABLE [ActivityEvidence] ADD CONSTRAINT [FK_ActivityEvidence_Equipment_LegacyEquipmentId] FOREIGN KEY ([LegacyEquipmentId]) REFERENCES [Equipment] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030113002_Sprint2ActivityMeasurementsEvidenceAndCheckpointReviews'
)
BEGIN
    ALTER TABLE [ActivityEvidence] ADD CONSTRAINT [FK_ActivityEvidence_InspectionActivities_InspectionActivityId] FOREIGN KEY ([InspectionActivityId]) REFERENCES [InspectionActivities] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030113002_Sprint2ActivityMeasurementsEvidenceAndCheckpointReviews'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261030113002_Sprint2ActivityMeasurementsEvidenceAndCheckpointReviews', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030113833_FixUnusedInspectionRevisionShadowForeignKey'
)
BEGIN
    ALTER TABLE [InspectionActivityRevisions] DROP CONSTRAINT [FK_InspectionActivityRevisions_InspectionActivities_InspectionActivityId1];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030113833_FixUnusedInspectionRevisionShadowForeignKey'
)
BEGIN
    DROP INDEX [IX_InspectionActivityRevisions_InspectionActivityId1] ON [InspectionActivityRevisions];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030113833_FixUnusedInspectionRevisionShadowForeignKey'
)
BEGIN
    DECLARE @var9 nvarchar(max);
    SELECT @var9 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InspectionActivityRevisions]') AND [c].[name] = N'InspectionActivityId1');
    IF @var9 IS NOT NULL EXEC(N'ALTER TABLE [InspectionActivityRevisions] DROP CONSTRAINT ' + @var9 + ';');
    ALTER TABLE [InspectionActivityRevisions] DROP COLUMN [InspectionActivityId1];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030113833_FixUnusedInspectionRevisionShadowForeignKey'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261030113833_FixUnusedInspectionRevisionShadowForeignKey', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030121649_Sprint3DefectWorkflow'
)
BEGIN
    ALTER TABLE [CorrectiveActions] ADD [IsOutstanding] bit NOT NULL DEFAULT CAST(1 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030121649_Sprint3DefectWorkflow'
)
BEGIN
    ALTER TABLE [CorrectiveActions] ADD [RectificationAt] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030121649_Sprint3DefectWorkflow'
)
BEGIN
    ALTER TABLE [ActivityFindings] ADD [DueAt] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030121649_Sprint3DefectWorkflow'
)
BEGIN
    ALTER TABLE [ActivityFindings] ADD [FaultCode] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030121649_Sprint3DefectWorkflow'
)
BEGIN
    ALTER TABLE [ActivityFindings] ADD [ReportedAt] datetimeoffset NOT NULL DEFAULT (TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00'));
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030121649_Sprint3DefectWorkflow'
)
BEGIN
    ALTER TABLE [ActivityFindings] ADD [ReportedByUserId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030121649_Sprint3DefectWorkflow'
)
BEGIN
    ALTER TABLE [ActivityFindings] ADD [RequiredMaterialsOrSupport] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030121649_Sprint3DefectWorkflow'
)
BEGIN
    ALTER TABLE [ActivityFindings] ADD [ResponsibleUserId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030121649_Sprint3DefectWorkflow'
)
BEGIN
    CREATE INDEX [IX_ActivityFindings_ReportedByUserId] ON [ActivityFindings] ([ReportedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030121649_Sprint3DefectWorkflow'
)
BEGIN
    CREATE INDEX [IX_ActivityFindings_ResponsibleUserId] ON [ActivityFindings] ([ResponsibleUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030121649_Sprint3DefectWorkflow'
)
BEGIN
    ALTER TABLE [ActivityFindings] ADD CONSTRAINT [FK_ActivityFindings_Users_ReportedByUserId] FOREIGN KEY ([ReportedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030121649_Sprint3DefectWorkflow'
)
BEGIN
    ALTER TABLE [ActivityFindings] ADD CONSTRAINT [FK_ActivityFindings_Users_ResponsibleUserId] FOREIGN KEY ([ResponsibleUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030121649_Sprint3DefectWorkflow'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261030121649_Sprint3DefectWorkflow', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030122212_Sprint3DefectResponsibleParty'
)
BEGIN
    ALTER TABLE [CorrectiveActions] ADD [ResponsibleParty] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030122212_Sprint3DefectResponsibleParty'
)
BEGIN
    ALTER TABLE [ActivityFindings] ADD [ResponsibleParty] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030122212_Sprint3DefectResponsibleParty'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261030122212_Sprint3DefectResponsibleParty', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030123924_Sprint3RectificationDescription'
)
BEGIN
    ALTER TABLE [CorrectiveActions] ADD [RectificationDescription] nvarchar(4000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030123924_Sprint3RectificationDescription'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261030123924_Sprint3RectificationDescription', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030124148_Sprint3RectificationAuditHistory'
)
BEGIN
    ALTER TABLE [CorrectiveActionEvents] ADD [RectificationAt] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030124148_Sprint3RectificationAuditHistory'
)
BEGIN
    ALTER TABLE [CorrectiveActionEvents] ADD [RectificationDescription] nvarchar(4000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030124148_Sprint3RectificationAuditHistory'
)
BEGIN
    ALTER TABLE [CorrectiveActionEvents] ADD [ResponsibleParty] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030124148_Sprint3RectificationAuditHistory'
)
BEGIN
    ALTER TABLE [CorrectiveActionEvents] ADD [WorkOrderReference] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030124148_Sprint3RectificationAuditHistory'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261030124148_Sprint3RectificationAuditHistory', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    DROP INDEX [IX_ChecklistVersionSnapshots_SourceChecklistTemplateId_VersionNumber] ON [ChecklistVersionSnapshots];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [CheckpointDefinitionSnapshots] ADD [RequireRemarkOnFail] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [CheckpointDefinitionSnapshots] ADD [SectionCode] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [ChecklistVersionSnapshots] ADD [ControlledSourceReference] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [ChecklistVersionSnapshots] ADD [ProtectionTypeSnapshot] nvarchar(150) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [ChecklistVersionSnapshots] ADD [StandardEdition] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [ChecklistVersionSnapshots] ADD [StandardReference] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [ChecklistTemplates] ADD [ApplicableProtectionTypes] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [ChecklistTemplates] ADD [ApprovedAt] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [ChecklistTemplates] ADD [ApprovedByUserId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [ChecklistTemplates] ADD [ControlledSourceReference] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [ChecklistTemplates] ADD [CreatedByUserId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [ChecklistTemplates] ADD [DraftRevision] int NOT NULL DEFAULT 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [ChecklistTemplates] ADD [FamilyId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [ChecklistTemplates] ADD [LifecycleStatus] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [ChecklistTemplates] ADD [StandardEdition] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [ChecklistTemplates] ADD [StandardReference] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [ChecklistTemplates] ADD [SubmittedAt] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [ChecklistTemplates] ADD [SubmittedByUserId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [ChecklistQuestions] ADD [ApplicableProtectionTypes] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [ChecklistQuestions] ADD [RequireRemarkOnFail] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [ChecklistQuestions] ADD [ResponseScaleCode] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [ChecklistQuestions] ADD [SectionCode] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [AreaEquipment] ADD [AmbientTemperatureRange] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [AreaEquipment] ADD [Description] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [AreaEquipment] ADD [ExProtectionType] nvarchar(150) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [AreaEquipment] ADD [GasGroup] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [AreaEquipment] ADD [HazardousAreaZone] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [AreaEquipment] ADD [IpRating] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [AreaEquipment] ADD [Model] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [AreaEquipment] ADD [NotApplicableFieldsJson] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [AreaEquipment] ADD [NotifiedBody] nvarchar(250) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [AreaEquipment] ADD [ReconciliationStatus] nvarchar(50) NOT NULL DEFAULT N'Unreviewed';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [AreaEquipment] ADD [RfidIdentificationMethod] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [AreaEquipment] ADD [SerialNumber] nvarchar(150) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [AreaEquipment] ADD [SpecialConditionsOfUse] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [AreaEquipment] ADD [TemperatureClass] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ChecklistVersionSnapshots_SourceChecklistTemplateId_VersionNumber_ProtectionTypeSnapshot] ON [ChecklistVersionSnapshots] ([SourceChecklistTemplateId], [VersionNumber], [ProtectionTypeSnapshot]) WHERE [ProtectionTypeSnapshot] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    CREATE INDEX [IX_ChecklistTemplates_ApprovedByUserId] ON [ChecklistTemplates] ([ApprovedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    CREATE INDEX [IX_ChecklistTemplates_CreatedByUserId] ON [ChecklistTemplates] ([CreatedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ChecklistTemplates_FamilyId_Version] ON [ChecklistTemplates] ([FamilyId], [Version]) WHERE [FamilyId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    CREATE INDEX [IX_ChecklistTemplates_SubmittedByUserId] ON [ChecklistTemplates] ([SubmittedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [ChecklistTemplates] ADD CONSTRAINT [FK_ChecklistTemplates_Users_ApprovedByUserId] FOREIGN KEY ([ApprovedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [ChecklistTemplates] ADD CONSTRAINT [FK_ChecklistTemplates_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    ALTER TABLE [ChecklistTemplates] ADD CONSTRAINT [FK_ChecklistTemplates_Users_SubmittedByUserId] FOREIGN KEY ([SubmittedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030131820_ChecklistAdminAndCompleteHaer'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261030131820_ChecklistAdminAndCompleteHaer', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031043123_InspectionPlanningScheduling'
)
BEGIN
    ALTER TABLE [InspectionTasks] ADD [CarryForwardReason] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031043123_InspectionPlanningScheduling'
)
BEGIN
    ALTER TABLE [InspectionTasks] ADD [ChecklistVersionId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031043123_InspectionPlanningScheduling'
)
BEGIN
    ALTER TABLE [InspectionTasks] ADD [DueAt] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031043123_InspectionPlanningScheduling'
)
BEGIN
    ALTER TABLE [InspectionTasks] ADD [IsInaccessible] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031043123_InspectionPlanningScheduling'
)
BEGIN
    ALTER TABLE [InspectionTasks] ADD [IsolationPrerequisites] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031043123_InspectionPlanningScheduling'
)
BEGIN
    ALTER TABLE [InspectionTasks] ADD [PlannedGrade] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031043123_InspectionPlanningScheduling'
)
BEGIN
    ALTER TABLE [InspectionTasks] ADD [PriorityCode] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031043123_InspectionPlanningScheduling'
)
BEGIN
    ALTER TABLE [InspectionTasks] ADD [RequiredPermits] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031043123_InspectionPlanningScheduling'
)
BEGIN
    ALTER TABLE [InspectionTasks] ADD [Rotation] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031043123_InspectionPlanningScheduling'
)
BEGIN
    ALTER TABLE [InspectionTasks] ADD [RouteOrder] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031043123_InspectionPlanningScheduling'
)
BEGIN
    ALTER TABLE [InspectionTasks] ADD [WorkPackageId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031043123_InspectionPlanningScheduling'
)
BEGIN
    CREATE TABLE [InspectionCampaigns] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Reference] nvarchar(100) NOT NULL,
        [StartsOn] date NOT NULL,
        [EndsOn] date NOT NULL,
        [Status] int NOT NULL,
        [ClosePercent] decimal(5,2) NULL,
        [DetailedPercent] decimal(5,2) NULL,
        [MinimumDetailedCreditPercent] decimal(5,2) NULL,
        [RoundingRule] int NULL,
        [Revision] int NOT NULL,
        [AllocationApproved] bit NOT NULL,
        [AllocationApprovedByUserId] uniqueidentifier NULL,
        [AllocationApprovedAt] datetimeoffset NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedByUserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_InspectionCampaigns] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InspectionCampaigns_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031043123_InspectionPlanningScheduling'
)
BEGIN
    CREATE TABLE [InspectionTaskAssignmentEvents] (
        [Id] uniqueidentifier NOT NULL,
        [InspectionTaskId] uniqueidentifier NOT NULL,
        [PreviousUserId] uniqueidentifier NULL,
        [NewUserId] uniqueidentifier NULL,
        [ActorUserId] uniqueidentifier NOT NULL,
        [Reason] nvarchar(1000) NULL,
        [ChangedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_InspectionTaskAssignmentEvents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InspectionTaskAssignmentEvents_InspectionTasks_InspectionTaskId] FOREIGN KEY ([InspectionTaskId]) REFERENCES [InspectionTasks] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031043123_InspectionPlanningScheduling'
)
BEGIN
    CREATE TABLE [CampaignAnnualLists] (
        [Id] uniqueidentifier NOT NULL,
        [CampaignId] uniqueidentifier NOT NULL,
        [Year] int NOT NULL,
        [EquipmentCount] int NOT NULL,
        [PlannedCloseCount] int NULL,
        [PlannedDetailedCount] int NULL,
        [ActualCloseCount] int NULL,
        [ActualDetailedCount] int NULL,
        [Revision] int NOT NULL,
        CONSTRAINT [PK_CampaignAnnualLists] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CampaignAnnualLists_InspectionCampaigns_CampaignId] FOREIGN KEY ([CampaignId]) REFERENCES [InspectionCampaigns] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031043123_InspectionPlanningScheduling'
)
BEGIN
    CREATE TABLE [InspectionWorkPackages] (
        [Id] uniqueidentifier NOT NULL,
        [CampaignId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [Reference] nvarchar(100) NOT NULL,
        [Site] nvarchar(200) NULL,
        [Area] nvarchar(200) NULL,
        [Rotation] nvarchar(100) NULL,
        [Status] nvarchar(40) NOT NULL,
        [PlannedStart] date NULL,
        [PlannedEnd] date NULL,
        [CreatedByUserId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_InspectionWorkPackages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InspectionWorkPackages_InspectionCampaigns_CampaignId] FOREIGN KEY ([CampaignId]) REFERENCES [InspectionCampaigns] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_InspectionWorkPackages_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031043123_InspectionPlanningScheduling'
)
BEGIN
    CREATE TABLE [PlanningApprovalEvents] (
        [Id] uniqueidentifier NOT NULL,
        [CampaignId] uniqueidentifier NOT NULL,
        [Kind] int NOT NULL,
        [Revision] int NOT NULL,
        [ActorUserId] uniqueidentifier NOT NULL,
        [Decision] nvarchar(40) NOT NULL,
        [Comments] nvarchar(4000) NULL,
        [OccurredAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_PlanningApprovalEvents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PlanningApprovalEvents_InspectionCampaigns_CampaignId] FOREIGN KEY ([CampaignId]) REFERENCES [InspectionCampaigns] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031043123_InspectionPlanningScheduling'
)
BEGIN
    CREATE TABLE [CampaignEquipment] (
        [Id] uniqueidentifier NOT NULL,
        [CampaignId] uniqueidentifier NOT NULL,
        [AnnualListId] uniqueidentifier NOT NULL,
        [AreaEquipmentId] uniqueidentifier NOT NULL,
        [RiskCode] nvarchar(max) NULL,
        [RiskScore] decimal(12,4) NULL,
        [EquipmentTypeSnapshot] nvarchar(max) NULL,
        CONSTRAINT [PK_CampaignEquipment] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CampaignEquipment_AreaEquipment_AreaEquipmentId] FOREIGN KEY ([AreaEquipmentId]) REFERENCES [AreaEquipment] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CampaignEquipment_CampaignAnnualLists_AnnualListId] FOREIGN KEY ([AnnualListId]) REFERENCES [CampaignAnnualLists] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CampaignEquipment_InspectionCampaigns_CampaignId] FOREIGN KEY ([CampaignId]) REFERENCES [InspectionCampaigns] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031043123_InspectionPlanningScheduling'
)
BEGIN
    CREATE TABLE [InspectionRecurrences] (
        [Id] uniqueidentifier NOT NULL,
        [CampaignId] uniqueidentifier NOT NULL,
        [AnnualListId] uniqueidentifier NOT NULL,
        [Grade] int NOT NULL,
        [EveryMonths] int NOT NULL,
        [NextOccurrenceOn] date NOT NULL,
        [Enabled] bit NOT NULL,
        [LastGeneratedAt] datetimeoffset NULL,
        [CreatedByUserId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_InspectionRecurrences] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InspectionRecurrences_CampaignAnnualLists_AnnualListId] FOREIGN KEY ([AnnualListId]) REFERENCES [CampaignAnnualLists] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_InspectionRecurrences_InspectionCampaigns_CampaignId] FOREIGN KEY ([CampaignId]) REFERENCES [InspectionCampaigns] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031043123_InspectionPlanningScheduling'
)
BEGIN
    CREATE TABLE [SamplingApprovals] (
        [Id] uniqueidentifier NOT NULL,
        [CampaignId] uniqueidentifier NOT NULL,
        [AnnualListId] uniqueidentifier NOT NULL,
        [Method] int NOT NULL,
        [RuleVersion] int NOT NULL,
        [RandomSeed] int NULL,
        [Justification] nvarchar(max) NULL,
        [SelectedEquipmentIdsJson] nvarchar(max) NOT NULL,
        [OverridesJson] nvarchar(max) NULL,
        [ApprovedByUserId] uniqueidentifier NULL,
        [SelectedAt] datetimeoffset NOT NULL,
        [ApprovedAt] datetimeoffset NULL,
        [IsFrozen] bit NOT NULL,
        CONSTRAINT [PK_SamplingApprovals] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SamplingApprovals_CampaignAnnualLists_AnnualListId] FOREIGN KEY ([AnnualListId]) REFERENCES [CampaignAnnualLists] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SamplingApprovals_InspectionCampaigns_CampaignId] FOREIGN KEY ([CampaignId]) REFERENCES [InspectionCampaigns] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031043123_InspectionPlanningScheduling'
)
BEGIN
    CREATE INDEX [IX_InspectionTasks_ChecklistVersionId] ON [InspectionTasks] ([ChecklistVersionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031043123_InspectionPlanningScheduling'
)
BEGIN
    CREATE INDEX [IX_InspectionTasks_WorkPackageId_RouteOrder] ON [InspectionTasks] ([WorkPackageId], [RouteOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031043123_InspectionPlanningScheduling'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CampaignAnnualLists_CampaignId_Year] ON [CampaignAnnualLists] ([CampaignId], [Year]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031043123_InspectionPlanningScheduling'
)
BEGIN
    CREATE INDEX [IX_CampaignEquipment_AnnualListId] ON [CampaignEquipment] ([AnnualListId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031043123_InspectionPlanningScheduling'
)
BEGIN
    CREATE INDEX [IX_CampaignEquipment_AreaEquipmentId] ON [CampaignEquipment] ([AreaEquipmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031043123_InspectionPlanningScheduling'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CampaignEquipment_CampaignId_AreaEquipmentId] ON [CampaignEquipment] ([CampaignId], [AreaEquipmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031043123_InspectionPlanningScheduling'
)
BEGIN
    CREATE UNIQUE INDEX [IX_InspectionCampaigns_ProjectId_Reference] ON [InspectionCampaigns] ([ProjectId], [Reference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031043123_InspectionPlanningScheduling'
)
BEGIN
    CREATE INDEX [IX_InspectionRecurrences_AnnualListId] ON [InspectionRecurrences] ([AnnualListId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031043123_InspectionPlanningScheduling'
)
BEGIN
    CREATE INDEX [IX_InspectionRecurrences_CampaignId_AnnualListId_Grade_NextOccurrenceOn] ON [InspectionRecurrences] ([CampaignId], [AnnualListId], [Grade], [NextOccurrenceOn]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031043123_InspectionPlanningScheduling'
)
BEGIN
    CREATE INDEX [IX_InspectionTaskAssignmentEvents_InspectionTaskId_ChangedAt] ON [InspectionTaskAssignmentEvents] ([InspectionTaskId], [ChangedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031043123_InspectionPlanningScheduling'
)
BEGIN
    CREATE UNIQUE INDEX [IX_InspectionWorkPackages_CampaignId_Reference] ON [InspectionWorkPackages] ([CampaignId], [Reference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031043123_InspectionPlanningScheduling'
)
BEGIN
    CREATE INDEX [IX_InspectionWorkPackages_ProjectId] ON [InspectionWorkPackages] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031043123_InspectionPlanningScheduling'
)
BEGIN
    CREATE INDEX [IX_PlanningApprovalEvents_CampaignId_Revision_OccurredAt] ON [PlanningApprovalEvents] ([CampaignId], [Revision], [OccurredAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031043123_InspectionPlanningScheduling'
)
BEGIN
    CREATE INDEX [IX_SamplingApprovals_AnnualListId] ON [SamplingApprovals] ([AnnualListId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031043123_InspectionPlanningScheduling'
)
BEGIN
    CREATE INDEX [IX_SamplingApprovals_CampaignId] ON [SamplingApprovals] ([CampaignId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031043123_InspectionPlanningScheduling'
)
BEGIN
    ALTER TABLE [InspectionTasks] ADD CONSTRAINT [FK_InspectionTasks_ChecklistVersionSnapshots_ChecklistVersionId] FOREIGN KEY ([ChecklistVersionId]) REFERENCES [ChecklistVersionSnapshots] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031043123_InspectionPlanningScheduling'
)
BEGIN
    ALTER TABLE [InspectionTasks] ADD CONSTRAINT [FK_InspectionTasks_InspectionWorkPackages_WorkPackageId] FOREIGN KEY ([WorkPackageId]) REFERENCES [InspectionWorkPackages] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031043123_InspectionPlanningScheduling'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261031043123_InspectionPlanningScheduling', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031044320_InspectionPlanningLeadAssignment'
)
BEGIN
    ALTER TABLE [InspectionCampaigns] ADD [InspectionLeadUserId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031044320_InspectionPlanningLeadAssignment'
)
BEGIN
    CREATE INDEX [IX_InspectionCampaigns_InspectionLeadUserId] ON [InspectionCampaigns] ([InspectionLeadUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031044320_InspectionPlanningLeadAssignment'
)
BEGIN
    ALTER TABLE [InspectionCampaigns] ADD CONSTRAINT [FK_InspectionCampaigns_Users_InspectionLeadUserId] FOREIGN KEY ([InspectionLeadUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031044320_InspectionPlanningLeadAssignment'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261031044320_InspectionPlanningLeadAssignment', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031044847_InspectionSamplingCampaignRevision'
)
BEGIN
    ALTER TABLE [SamplingApprovals] ADD [CampaignRevision] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031044847_InspectionSamplingCampaignRevision'
)
BEGIN
    CREATE INDEX [IX_SamplingApprovals_CampaignId_AnnualListId_CampaignRevision] ON [SamplingApprovals] ([CampaignId], [AnnualListId], [CampaignRevision]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031044847_InspectionSamplingCampaignRevision'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261031044847_InspectionSamplingCampaignRevision', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031052351_OfflineInspectionSyncReceipts'
)
BEGIN
    CREATE TABLE [OfflinePhotoSyncReceipts] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [IdempotencyKey] nvarchar(100) NOT NULL,
        [ContentHash] nvarchar(64) NOT NULL,
        [ActivityId] uniqueidentifier NOT NULL,
        [CheckpointResponseRevisionId] uniqueidentifier NULL,
        [EvidenceId] uniqueidentifier NOT NULL,
        [AcceptedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_OfflinePhotoSyncReceipts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OfflinePhotoSyncReceipts_ActivityEvidence_EvidenceId] FOREIGN KEY ([EvidenceId]) REFERENCES [ActivityEvidence] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OfflinePhotoSyncReceipts_CheckpointResponseRevisions_CheckpointResponseRevisionId] FOREIGN KEY ([CheckpointResponseRevisionId]) REFERENCES [CheckpointResponseRevisions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OfflinePhotoSyncReceipts_InspectionActivities_ActivityId] FOREIGN KEY ([ActivityId]) REFERENCES [InspectionActivities] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OfflinePhotoSyncReceipts_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031052351_OfflineInspectionSyncReceipts'
)
BEGIN
    CREATE TABLE [OfflineSyncReceipts] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [ClientActivityId] uniqueidentifier NOT NULL,
        [IdempotencyKey] nvarchar(100) NOT NULL,
        [PayloadHash] nvarchar(64) NOT NULL,
        [ServerActivityId] uniqueidentifier NOT NULL,
        [ResponseMapJson] nvarchar(max) NOT NULL,
        [AcceptedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_OfflineSyncReceipts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OfflineSyncReceipts_InspectionActivities_ServerActivityId] FOREIGN KEY ([ServerActivityId]) REFERENCES [InspectionActivities] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OfflineSyncReceipts_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031052351_OfflineInspectionSyncReceipts'
)
BEGIN
    CREATE INDEX [IX_OfflinePhotoSyncReceipts_ActivityId] ON [OfflinePhotoSyncReceipts] ([ActivityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031052351_OfflineInspectionSyncReceipts'
)
BEGIN
    CREATE INDEX [IX_OfflinePhotoSyncReceipts_CheckpointResponseRevisionId] ON [OfflinePhotoSyncReceipts] ([CheckpointResponseRevisionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031052351_OfflineInspectionSyncReceipts'
)
BEGIN
    CREATE INDEX [IX_OfflinePhotoSyncReceipts_EvidenceId] ON [OfflinePhotoSyncReceipts] ([EvidenceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031052351_OfflineInspectionSyncReceipts'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OfflinePhotoSyncReceipts_UserId_IdempotencyKey] ON [OfflinePhotoSyncReceipts] ([UserId], [IdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031052351_OfflineInspectionSyncReceipts'
)
BEGIN
    CREATE INDEX [IX_OfflineSyncReceipts_ServerActivityId] ON [OfflineSyncReceipts] ([ServerActivityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031052351_OfflineInspectionSyncReceipts'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OfflineSyncReceipts_UserId_ClientActivityId] ON [OfflineSyncReceipts] ([UserId], [ClientActivityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031052351_OfflineInspectionSyncReceipts'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OfflineSyncReceipts_UserId_IdempotencyKey] ON [OfflineSyncReceipts] ([UserId], [IdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031052351_OfflineInspectionSyncReceipts'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261031052351_OfflineInspectionSyncReceipts', N'10.0.13');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031063837_EquipmentTagAssignmentsAndReportArchive'
)
BEGIN
    CREATE TABLE [EquipmentTagAssignments] (
        [Id] uniqueidentifier NOT NULL,
        [EquipmentId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [Technology] nvarchar(30) NOT NULL,
        [NormalizedIdentifier] nvarchar(200) NOT NULL,
        [IsActive] bit NOT NULL,
        [AssignedByUserId] uniqueidentifier NOT NULL,
        [AssignedAt] datetimeoffset NOT NULL,
        [AssignmentReason] nvarchar(1000) NOT NULL,
        [DeactivatedAt] datetimeoffset NULL,
        [DeactivatedByUserId] uniqueidentifier NULL,
        [DeactivationReason] nvarchar(1000) NULL,
        CONSTRAINT [PK_EquipmentTagAssignments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EquipmentTagAssignments_AreaEquipment_EquipmentId] FOREIGN KEY ([EquipmentId]) REFERENCES [AreaEquipment] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EquipmentTagAssignments_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EquipmentTagAssignments_Users_AssignedByUserId] FOREIGN KEY ([AssignedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EquipmentTagAssignments_Users_DeactivatedByUserId] FOREIGN KEY ([DeactivatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031063837_EquipmentTagAssignmentsAndReportArchive'
)
BEGIN
    CREATE TABLE [EquipmentTagHistory] (
        [Id] uniqueidentifier NOT NULL,
        [EquipmentId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [AssignmentId] uniqueidentifier NULL,
        [Technology] nvarchar(30) NOT NULL,
        [NormalizedIdentifier] nvarchar(200) NOT NULL,
        [ActionCode] nvarchar(40) NOT NULL,
        [Reason] nvarchar(1000) NOT NULL,
        [ActorUserId] uniqueidentifier NOT NULL,
        [OccurredAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_EquipmentTagHistory] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EquipmentTagHistory_AreaEquipment_EquipmentId] FOREIGN KEY ([EquipmentId]) REFERENCES [AreaEquipment] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EquipmentTagHistory_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EquipmentTagHistory_Users_ActorUserId] FOREIGN KEY ([ActorUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031063837_EquipmentTagAssignmentsAndReportArchive'
)
BEGIN
    CREATE TABLE [GeneratedReportArtifacts] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [LinkedEntityId] uniqueidentifier NOT NULL,
        [ReportType] nvarchar(40) NOT NULL,
        [ReportNumber] nvarchar(160) NOT NULL,
        [Revision] int NOT NULL,
        [FileName] nvarchar(255) NOT NULL,
        [ContentType] nvarchar(150) NOT NULL,
        [StorageKey] nvarchar(1000) NOT NULL,
        [Sha256] nvarchar(64) NOT NULL,
        [GeneratedAt] datetimeoffset NOT NULL,
        [GeneratedByUserId] uniqueidentifier NOT NULL,
        [ApprovalState] nvarchar(30) NOT NULL,
        CONSTRAINT [PK_GeneratedReportArtifacts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_GeneratedReportArtifacts_Users_GeneratedByUserId] FOREIGN KEY ([GeneratedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031063837_EquipmentTagAssignmentsAndReportArchive'
)
BEGIN
    CREATE INDEX [IX_EquipmentTagAssignments_AssignedByUserId] ON [EquipmentTagAssignments] ([AssignedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031063837_EquipmentTagAssignmentsAndReportArchive'
)
BEGIN
    CREATE INDEX [IX_EquipmentTagAssignments_DeactivatedByUserId] ON [EquipmentTagAssignments] ([DeactivatedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031063837_EquipmentTagAssignmentsAndReportArchive'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_EquipmentTagAssignments_EquipmentId_Technology] ON [EquipmentTagAssignments] ([EquipmentId], [Technology]) WHERE [IsActive] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031063837_EquipmentTagAssignmentsAndReportArchive'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_EquipmentTagAssignments_NormalizedIdentifier] ON [EquipmentTagAssignments] ([NormalizedIdentifier]) WHERE [IsActive] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031063837_EquipmentTagAssignmentsAndReportArchive'
)
BEGIN
    CREATE INDEX [IX_EquipmentTagAssignments_ProjectId] ON [EquipmentTagAssignments] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031063837_EquipmentTagAssignmentsAndReportArchive'
)
BEGIN
    CREATE INDEX [IX_EquipmentTagHistory_ActorUserId] ON [EquipmentTagHistory] ([ActorUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031063837_EquipmentTagAssignmentsAndReportArchive'
)
BEGIN
    CREATE INDEX [IX_EquipmentTagHistory_AssignmentId] ON [EquipmentTagHistory] ([AssignmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031063837_EquipmentTagAssignmentsAndReportArchive'
)
BEGIN
    CREATE INDEX [IX_EquipmentTagHistory_EquipmentId_OccurredAt] ON [EquipmentTagHistory] ([EquipmentId], [OccurredAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031063837_EquipmentTagAssignmentsAndReportArchive'
)
BEGIN
    CREATE INDEX [IX_EquipmentTagHistory_ProjectId] ON [EquipmentTagHistory] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031063837_EquipmentTagAssignmentsAndReportArchive'
)
BEGIN
    CREATE INDEX [IX_GeneratedReportArtifacts_GeneratedByUserId] ON [GeneratedReportArtifacts] ([GeneratedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031063837_EquipmentTagAssignmentsAndReportArchive'
)
BEGIN
    CREATE INDEX [IX_GeneratedReportArtifacts_ProjectId_GeneratedAt] ON [GeneratedReportArtifacts] ([ProjectId], [GeneratedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031063837_EquipmentTagAssignmentsAndReportArchive'
)
BEGIN
    CREATE UNIQUE INDEX [IX_GeneratedReportArtifacts_ProjectId_ReportType_LinkedEntityId_Revision] ON [GeneratedReportArtifacts] ([ProjectId], [ReportType], [LinkedEntityId], [Revision]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031063837_EquipmentTagAssignmentsAndReportArchive'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261031063837_EquipmentTagAssignmentsAndReportArchive', N'10.0.13');
END;

COMMIT;
GO

