BEGIN TRANSACTION;
GO

ALTER TABLE [Projects] ADD [ClientLogoPath] nvarchar(500) NULL;
GO

ALTER TABLE [Projects] ADD [ClientName] nvarchar(200) NULL;
GO

ALTER TABLE [Projects] ADD [Description] nvarchar(4000) NULL;
GO

ALTER TABLE [Projects] ADD [DocumentNumber] nvarchar(200) NULL;
GO

ALTER TABLE [Projects] ADD [Revision] nvarchar(100) NULL;
GO

ALTER TABLE [Projects] ADD [UidNumber] nvarchar(100) NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261007120651_ProjectReportMetadataAndClientDetails', N'8.0.0');
GO

COMMIT;
GO

