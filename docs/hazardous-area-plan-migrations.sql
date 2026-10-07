BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006134251_HazardousAreaPlan'
)
BEGIN
    ALTER TABLE [Projects] ADD [HazardousAreaPlanFileName] nvarchar(255) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006134251_HazardousAreaPlan'
)
BEGIN
    ALTER TABLE [Projects] ADD [HazardousAreaPlanPath] nvarchar(500) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006134251_HazardousAreaPlan'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261006134251_HazardousAreaPlan', N'8.0.0');
END;
GO

COMMIT;
GO

