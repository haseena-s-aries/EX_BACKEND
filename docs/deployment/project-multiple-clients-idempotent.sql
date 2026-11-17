BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112045739_ProjectMultipleClients'
)
BEGIN
    CREATE TABLE [ProjectClientAssignments] (
        [ProjectId] uniqueidentifier NOT NULL,
        [ClientUserId] uniqueidentifier NOT NULL,
        [IsActive] bit NOT NULL,
        [AssignedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_ProjectClientAssignments] PRIMARY KEY ([ProjectId], [ClientUserId]),
        CONSTRAINT [FK_ProjectClientAssignments_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ProjectClientAssignments_Users_ClientUserId] FOREIGN KEY ([ClientUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112045739_ProjectMultipleClients'
)
BEGIN
    CREATE INDEX [IX_ProjectClientAssignments_ClientUserId] ON [ProjectClientAssignments] ([ClientUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112045739_ProjectMultipleClients'
)
BEGIN
    INSERT INTO [ProjectClientAssignments] ([ProjectId], [ClientUserId], [IsActive], [AssignedAt], [UpdatedAt])
                    SELECT p.[Id], p.[AssignedClientId], CAST(1 AS bit), p.[CreatedAt], p.[UpdatedAt]
                    FROM [Projects] p
                    WHERE p.[AssignedClientId] IS NOT NULL
                      AND NOT EXISTS (SELECT 1 FROM [ProjectClientAssignments] a WHERE a.[ProjectId] = p.[Id] AND a.[ClientUserId] = p.[AssignedClientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112045739_ProjectMultipleClients'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261112045739_ProjectMultipleClients', N'10.0.13');
END;

COMMIT;
GO

