-- Registration approvals, durable notifications and Admin-only role change requests.
-- Back up ExSurveyApp first. Run in SSMS SQLCMD Mode or sqlcmd -b.
:On Error exit
USE [ExSurveyApp];
GO
SET XACT_ABORT ON;
IF DB_NAME() <> N'ExSurveyApp' THROW 51000, 'Wrong database. Expected ExSurveyApp.', 1;
IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL THROW 51000, 'Migration history is missing.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = N'20261113044054_AccountProfileAndRecovery')
    THROW 51000, 'Apply AccountProfileAndRecovery before this upgrade.', 1;
GO
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113072459_UserRegistrationAndRoleRequests'
)
BEGIN
    ALTER TABLE [Users] ADD [ApprovalRejectionReason] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113072459_UserRegistrationAndRoleRequests'
)
BEGIN
    ALTER TABLE [Users] ADD [RegisteredAt] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113072459_UserRegistrationAndRoleRequests'
)
BEGIN
    CREATE TABLE [RoleChangeRequests] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [CurrentRole] int NOT NULL,
        [RequestedRole] int NOT NULL,
        [Reason] nvarchar(2000) NOT NULL,
        [Status] int NOT NULL,
        [RequestedAt] datetimeoffset NOT NULL,
        [ReviewedBy] uniqueidentifier NULL,
        [ReviewedAt] datetimeoffset NULL,
        [RejectionReason] nvarchar(2000) NULL,
        CONSTRAINT [PK_RoleChangeRequests] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RoleChangeRequests_Users_ReviewedBy] FOREIGN KEY ([ReviewedBy]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_RoleChangeRequests_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113072459_UserRegistrationAndRoleRequests'
)
BEGIN
    CREATE TABLE [UserAccessNotifications] (
        [Id] uniqueidentifier NOT NULL,
        [RecipientId] uniqueidentifier NOT NULL,
        [EventKey] nvarchar(150) NOT NULL,
        [Kind] nvarchar(30) NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Message] nvarchar(2500) NOT NULL,
        [TargetUserId] uniqueidentifier NULL,
        [RoleRequestId] uniqueidentifier NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ReadAt] datetimeoffset NULL,
        [DismissedAt] datetimeoffset NULL,
        CONSTRAINT [PK_UserAccessNotifications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserAccessNotifications_Users_RecipientId] FOREIGN KEY ([RecipientId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113072459_UserRegistrationAndRoleRequests'
)
BEGIN
    CREATE INDEX [IX_RoleChangeRequests_ReviewedBy] ON [RoleChangeRequests] ([ReviewedBy]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113072459_UserRegistrationAndRoleRequests'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_RoleChangeRequests_UserId] ON [RoleChangeRequests] ([UserId]) WHERE [Status] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113072459_UserRegistrationAndRoleRequests'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UserAccessNotifications_RecipientId_EventKey] ON [UserAccessNotifications] ([RecipientId], [EventKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113072459_UserRegistrationAndRoleRequests'
)
BEGIN
    INSERT INTO [UserAccessNotifications] ([Id],[RecipientId],[EventKey],[Kind],[Title],[Message],[TargetUserId],[CreatedAt])
    SELECT NEWID(), approver.[Id], N'registration:' + LOWER(CONVERT(nvarchar(36), pending.[Id])), N'Registration', N'New User Registration', N'New user registration is awaiting approval.', pending.[Id], SYSUTCDATETIME()
    FROM [Users] pending CROSS JOIN [Users] approver
    WHERE pending.[IsActive] = 1 AND pending.[ApprovalStatus] = 0
    AND approver.[IsActive] = 1 AND approver.[ApprovalStatus] = 1
    AND (approver.[Role] = 0 OR (approver.[Role] = 1 AND pending.[Role] NOT IN (0,1)));
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113072459_UserRegistrationAndRoleRequests'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261113072459_UserRegistrationAndRoleRequests', N'10.0.13');
END;

COMMIT;
GO


SELECT MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;
