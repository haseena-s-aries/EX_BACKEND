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
GO

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
GO

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
GO

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
GO

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
GO

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
GO

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
GO

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
GO

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
GO

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
GO

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
GO

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
GO

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
GO

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
GO

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
GO

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
GO

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
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE INDEX [IX_Areas_JobId] ON [Areas] ([JobId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_ActorId] ON [AuditLogs] ([ActorId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE INDEX [IX_ChecklistQuestions_ChecklistTemplateId] ON [ChecklistQuestions] ([ChecklistTemplateId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Equipment_AreaId_TagNumber] ON [Equipment] ([AreaId], [TagNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE INDEX [IX_Files_AreaId] ON [Files] ([AreaId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE INDEX [IX_Files_EquipmentId] ON [Files] ([EquipmentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE INDEX [IX_Files_FindingId] ON [Files] ([FindingId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE INDEX [IX_Files_JobId] ON [Files] ([JobId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE INDEX [IX_Files_UploadedById] ON [Files] ([UploadedById]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE INDEX [IX_Findings_SurveySessionId] ON [Findings] ([SurveySessionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE UNIQUE INDEX [IX_JobAssignments_JobId_UserId_Role] ON [JobAssignments] ([JobId], [UserId], [Role]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE INDEX [IX_JobAssignments_UserId] ON [JobAssignments] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE INDEX [IX_Jobs_ClientId] ON [Jobs] ([ClientId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Jobs_JobNumber] ON [Jobs] ([JobNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE INDEX [IX_QcReviews_ReviewerId] ON [QcReviews] ([ReviewerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE INDEX [IX_QcReviews_SurveySessionId] ON [QcReviews] ([SurveySessionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE INDEX [IX_ReportRevisions_FileId] ON [ReportRevisions] ([FileId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ReportRevisions_JobId_Revision] ON [ReportRevisions] ([JobId], [Revision]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE INDEX [IX_SurveyAnswers_ChecklistQuestionId] ON [SurveyAnswers] ([ChecklistQuestionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SurveyAnswers_SurveySessionId_ChecklistQuestionId] ON [SurveyAnswers] ([SurveySessionId], [ChecklistQuestionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE INDEX [IX_SurveySessions_ChecklistTemplateId] ON [SurveySessions] ([ChecklistTemplateId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE INDEX [IX_SurveySessions_EquipmentId] ON [SurveySessions] ([EquipmentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE INDEX [IX_SurveySessions_JobId] ON [SurveySessions] ([JobId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE INDEX [IX_SurveySessions_SurveyorId] ON [SurveySessions] ([SurveyorId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SyncBatches_SurveyorId_ClientBatchId] ON [SyncBatches] ([SurveyorId], [ClientBatchId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923042729_ex-survey'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260923042729_ex-survey', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923055732_RegistrationOtpAndApproval'
)
BEGIN
    ALTER TABLE [Users] ADD [ApprovalReviewedAt] datetimeoffset NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923055732_RegistrationOtpAndApproval'
)
BEGIN
    ALTER TABLE [Users] ADD [ApprovalReviewedByUserId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923055732_RegistrationOtpAndApproval'
)
BEGIN
    ALTER TABLE [Users] ADD [ApprovalStatus] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923055732_RegistrationOtpAndApproval'
)
BEGIN
    UPDATE [Users] SET [ApprovalStatus] = 1;
END;
GO

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
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923055732_RegistrationOtpAndApproval'
)
BEGIN
    CREATE INDEX [IX_Users_ApprovalReviewedByUserId] ON [Users] ([ApprovalReviewedByUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923055732_RegistrationOtpAndApproval'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RegistrationOtps_Email] ON [RegistrationOtps] ([Email]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923055732_RegistrationOtpAndApproval'
)
BEGIN
    ALTER TABLE [Users] ADD CONSTRAINT [FK_Users_Users_ApprovalReviewedByUserId] FOREIGN KEY ([ApprovalReviewedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923055732_RegistrationOtpAndApproval'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260923055732_RegistrationOtpAndApproval', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923064248_RequestedRegistrationRole'
)
BEGIN
    ALTER TABLE [RegistrationOtps] ADD [RequestedRole] int NOT NULL DEFAULT 4;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923064248_RequestedRegistrationRole'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260923064248_RequestedRegistrationRole', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

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
GO

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
GO

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
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923093731_ProjectModule'
)
BEGIN
    CREATE INDEX [IX_ProjectAreaPhotos_ProjectAreaId] ON [ProjectAreaPhotos] ([ProjectAreaId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923093731_ProjectModule'
)
BEGIN
    CREATE INDEX [IX_ProjectAreaPhotos_UploadedByUserId] ON [ProjectAreaPhotos] ([UploadedByUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923093731_ProjectModule'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProjectAreas_ProjectId_AreaName] ON [ProjectAreas] ([ProjectId], [AreaName]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923093731_ProjectModule'
)
BEGIN
    CREATE INDEX [IX_Projects_AssignedClientId] ON [Projects] ([AssignedClientId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923093731_ProjectModule'
)
BEGIN
    CREATE INDEX [IX_Projects_AssignedQcId] ON [Projects] ([AssignedQcId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923093731_ProjectModule'
)
BEGIN
    CREATE INDEX [IX_Projects_AssignedSurveyorId] ON [Projects] ([AssignedSurveyorId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923093731_ProjectModule'
)
BEGIN
    CREATE INDEX [IX_Projects_CreatedByUserId] ON [Projects] ([CreatedByUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923093731_ProjectModule'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Projects_JobNo] ON [Projects] ([JobNo]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923093731_ProjectModule'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260923093731_ProjectModule', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924085532_SurveyorEquipmentPins'
)
BEGIN
    ALTER TABLE [ProjectAreas] ADD [LayoutImagePath] nvarchar(500) NULL;
END;
GO

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
GO

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
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924085532_SurveyorEquipmentPins'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AreaEquipment_AreaId_TagNumber] ON [AreaEquipment] ([AreaId], [TagNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924085532_SurveyorEquipmentPins'
)
BEGIN
    CREATE INDEX [IX_AreaEquipment_CreatedByUserId] ON [AreaEquipment] ([CreatedByUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924085532_SurveyorEquipmentPins'
)
BEGIN
    CREATE INDEX [IX_AreaEquipment_ProjectId] ON [AreaEquipment] ([ProjectId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924085532_SurveyorEquipmentPins'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260924085532_SurveyorEquipmentPins', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926105350_AreaEquipmentSequenceNo'
)
BEGIN
    ALTER TABLE [AreaEquipment] ADD [SequenceNo] int NULL;
END;
GO

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
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926105350_AreaEquipmentSequenceNo'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AreaEquipment]') AND [c].[name] = N'SequenceNo');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [AreaEquipment] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [AreaEquipment] ALTER COLUMN [SequenceNo] int NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926105350_AreaEquipmentSequenceNo'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AreaEquipment_AreaId_SequenceNo] ON [AreaEquipment] ([AreaId], [SequenceNo]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926105350_AreaEquipmentSequenceNo'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260926105350_AreaEquipmentSequenceNo', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926115821_ProjectStatus'
)
BEGIN
    ALTER TABLE [Projects] ADD [Status] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926115821_ProjectStatus'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260926115821_ProjectStatus', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    ALTER TABLE [QcReviews] DROP CONSTRAINT [FK_QcReviews_Users_ReviewerId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    DROP INDEX [IX_QcReviews_SurveySessionId] ON [QcReviews];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    EXEC sp_rename N'[QcReviews].[ReviewerId]', N'ReviewedByUserId', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    EXEC sp_rename N'[QcReviews].[Comment]', N'Comments', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    EXEC sp_rename N'[QcReviews].[IX_QcReviews_ReviewerId]', N'IX_QcReviews_ReviewedByUserId', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SurveySessions]') AND [c].[name] = N'JobId');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [SurveySessions] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [SurveySessions] ALTER COLUMN [JobId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SurveySessions]') AND [c].[name] = N'EquipmentId');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [SurveySessions] DROP CONSTRAINT [' + @var2 + '];');
    ALTER TABLE [SurveySessions] ALTER COLUMN [EquipmentId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    ALTER TABLE [SurveySessions] ADD [AreaEquipmentId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    ALTER TABLE [SurveySessions] ADD [DueDate] datetimeoffset NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    ALTER TABLE [SurveySessions] ADD [InspectionTimestamp] datetimeoffset NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    ALTER TABLE [SurveySessions] ADD [SurveyorRemarks] nvarchar(2000) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    DECLARE @var3 sysname;
    SELECT @var3 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[QcReviews]') AND [c].[name] = N'ReviewedAt');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [QcReviews] DROP CONSTRAINT [' + @var3 + '];');
    ALTER TABLE [QcReviews] ALTER COLUMN [ReviewedAt] datetimeoffset NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    DECLARE @var4 sysname;
    SELECT @var4 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[QcReviews]') AND [c].[name] = N'Comments');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [QcReviews] DROP CONSTRAINT [' + @var4 + '];');
    ALTER TABLE [QcReviews] ALTER COLUMN [Comments] nvarchar(4000) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    ALTER TABLE [QcReviews] ADD [Severity] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    ALTER TABLE [QcReviews] ADD [StartedAt] datetimeoffset NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    ALTER TABLE [QcReviews] ADD [Status] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    UPDATE [QcReviews] SET [Status] = CASE WHEN [Approved] = 1 THEN 3 ELSE 2 END, [StartedAt] = [ReviewedAt]
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    DECLARE @var5 sysname;
    SELECT @var5 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[QcReviews]') AND [c].[name] = N'Approved');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [QcReviews] DROP CONSTRAINT [' + @var5 + '];');
    ALTER TABLE [QcReviews] DROP COLUMN [Approved];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    DECLARE @var6 sysname;
    SELECT @var6 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Files]') AND [c].[name] = N'JobId');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [Files] DROP CONSTRAINT [' + @var6 + '];');
    ALTER TABLE [Files] ALTER COLUMN [JobId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    ALTER TABLE [Files] ADD [SurveySessionId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_SurveySessions_AreaEquipmentId] ON [SurveySessions] ([AreaEquipmentId]) WHERE [AreaEquipmentId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    CREATE UNIQUE INDEX [IX_QcReviews_SurveySessionId] ON [QcReviews] ([SurveySessionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    CREATE INDEX [IX_Files_SurveySessionId] ON [Files] ([SurveySessionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    ALTER TABLE [Files] ADD CONSTRAINT [FK_Files_SurveySessions_SurveySessionId] FOREIGN KEY ([SurveySessionId]) REFERENCES [SurveySessions] ([Id]) ON DELETE CASCADE;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    ALTER TABLE [QcReviews] ADD CONSTRAINT [FK_QcReviews_Users_ReviewedByUserId] FOREIGN KEY ([ReviewedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    ALTER TABLE [SurveySessions] ADD CONSTRAINT [FK_SurveySessions_AreaEquipment_AreaEquipmentId] FOREIGN KEY ([AreaEquipmentId]) REFERENCES [AreaEquipment] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929070220_QcReviewWorkflow'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260929070220_QcReviewWorkflow', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929085735_EquipmentInspectionWorkflow'
)
BEGIN
    DECLARE @var7 sysname;
    SELECT @var7 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SurveySessions]') AND [c].[name] = N'ChecklistTemplateId');
    IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [SurveySessions] DROP CONSTRAINT [' + @var7 + '];');
    ALTER TABLE [SurveySessions] ALTER COLUMN [ChecklistTemplateId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929085735_EquipmentInspectionWorkflow'
)
BEGIN
    ALTER TABLE [SurveySessions] ADD [CreatedAt] datetimeoffset NOT NULL DEFAULT (SYSUTCDATETIME());
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929085735_EquipmentInspectionWorkflow'
)
BEGIN
    ALTER TABLE [SurveySessions] ADD [InspectorName] nvarchar(200) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929085735_EquipmentInspectionWorkflow'
)
BEGIN
    ALTER TABLE [SurveySessions] ADD [UpdatedAt] datetimeoffset NOT NULL DEFAULT (SYSUTCDATETIME());
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929085735_EquipmentInspectionWorkflow'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260929085735_EquipmentInspectionWorkflow', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006104409_ManagerQcMonitoring'
)
BEGIN
    ALTER TABLE [Users] ADD [ManagerId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006104409_ManagerQcMonitoring'
)
BEGIN
    ALTER TABLE [AuditLogs] ADD [Comments] nvarchar(4000) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006104409_ManagerQcMonitoring'
)
BEGIN
    ALTER TABLE [AuditLogs] ADD [QcStatus] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006104409_ManagerQcMonitoring'
)
BEGIN
    ALTER TABLE [AuditLogs] ADD [ReviewStartedAt] datetimeoffset NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006104409_ManagerQcMonitoring'
)
BEGIN
    ALTER TABLE [AuditLogs] ADD [ReviewedAt] datetimeoffset NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006104409_ManagerQcMonitoring'
)
BEGIN
    ALTER TABLE [AuditLogs] ADD [Severity] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006104409_ManagerQcMonitoring'
)
BEGIN
    CREATE INDEX [IX_Users_ManagerId] ON [Users] ([ManagerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006104409_ManagerQcMonitoring'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_EntityId_OccurredAt] ON [AuditLogs] ([EntityId], [OccurredAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006104409_ManagerQcMonitoring'
)
BEGIN
    ALTER TABLE [Users] ADD CONSTRAINT [FK_Users_Users_ManagerId] FOREIGN KEY ([ManagerId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION;
END;
GO

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
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006104409_ManagerQcMonitoring'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261006104409_ManagerQcMonitoring', N'8.0.0');
END;
GO

COMMIT;
GO

