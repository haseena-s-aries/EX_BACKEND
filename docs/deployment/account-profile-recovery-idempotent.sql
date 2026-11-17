-- Account/profile upgrade for the confirmed 16-migration ExSurveyApp live baseline.
-- Run in SSMS SQLCMD Mode or sqlcmd -b. Take a fresh backup before deployment.
:On Error exit
USE [ExSurveyApp];
GO
SET XACT_ABORT ON;
IF DB_NAME() <> N'ExSurveyApp'
    THROW 51000, 'Wrong database. Expected ExSurveyApp.', 1;
IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
    THROW 51000, 'Migration history is missing. Stop and review.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = N'20261112045739_ProjectMultipleClients')
    THROW 51000, 'The ProjectMultipleClients baseline must be applied first.', 1;
GO
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113044054_AccountProfileAndRecovery'
)
BEGIN
    ALTER TABLE [Users] ADD [AuthVersion] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113044054_AccountProfileAndRecovery'
)
BEGIN
    ALTER TABLE [Users] ADD [Designation] nvarchar(150) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113044054_AccountProfileAndRecovery'
)
BEGIN
    ALTER TABLE [Users] ADD [PhoneNumber] nvarchar(40) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113044054_AccountProfileAndRecovery'
)
BEGIN
    ALTER TABLE [Users] ADD [ProfilePhotoPath] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113044054_AccountProfileAndRecovery'
)
BEGIN
    CREATE TABLE [AccountActionTokens] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Purpose] nvarchar(30) NOT NULL,
        [TokenHash] nchar(64) NOT NULL,
        [NewEmail] nvarchar(320) NULL,
        [AuthVersion] int NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ExpiresAt] datetimeoffset NOT NULL,
        [ConsumedAt] datetimeoffset NULL,
        CONSTRAINT [PK_AccountActionTokens] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AccountActionTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113044054_AccountProfileAndRecovery'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AccountActionTokens_TokenHash] ON [AccountActionTokens] ([TokenHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113044054_AccountProfileAndRecovery'
)
BEGIN
    CREATE INDEX [IX_AccountActionTokens_UserId_Purpose_CreatedAt] ON [AccountActionTokens] ([UserId], [Purpose], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113044054_AccountProfileAndRecovery'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261113044054_AccountProfileAndRecovery', N'10.0.13');
END;

COMMIT;
GO


SELECT MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;
