using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LIS.Data;

public static class DatabaseSchemaUpdater
{
    public static async Task EnsureCurrentSchemaAsync(ApplicationDbContext context)
    {
        await context.Database.EnsureCreatedAsync();

        var connection = (SqlConnection)context.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            if (await TableExistsAsync(connection, "Reports"))
            {
                await AddColumnIfMissingAsync(connection, "Reports", "SubmittedAt", "datetime2 NULL");
                await AddColumnIfMissingAsync(connection, "Reports", "ApprovedAt", "datetime2 NULL");
                await AddColumnIfMissingAsync(connection, "Reports", "ArchivedAt", "datetime2 NULL");
                await AddColumnIfMissingAsync(connection, "Reports", "ReportOriginalFileName", "nvarchar(512) NULL");

                await ExecuteNonQueryAsync(connection, """
                    UPDATE Reports
                    SET Status = 2,
                        ApprovedAt = COALESCE(ApprovedAt, ReportingDate, UpdatedAt, CreatedAt),
                        SubmittedAt = COALESCE(SubmittedAt, UpdatedAt, CreatedAt)
                    WHERE Status = 1
                """);
            }

            if (await TableExistsAsync(connection, "Hospitals"))
            {
                await AddColumnIfMissingAsync(connection, "Hospitals", "ContactNumber", "nvarchar(50) NULL");
                await AddColumnIfMissingAsync(connection, "Hospitals", "ContactEmail", "nvarchar(256) NULL");
                await AddColumnIfMissingAsync(connection, "Hospitals", "LogoPath", "nvarchar(512) NULL");
            }

            await EnsureUserHospitalsTableAsync(connection);
            await EnsurePermissionsTableAsync(connection);
            await EnsureRolePermissionsTableAsync(connection);
            await EnsureSystemSettingsTableAsync(connection);
            await EnsureAuditLogsTableAsync(connection);
            await DropUniqueConstraintOnReferenceNumberAsync(connection);
            await EnsureStaffRegistrationRequestsTableAsync(connection);

            if (await TableExistsAsync(connection, "StaffRegistrationRequests"))
            {
                await AddColumnIfMissingAsync(connection, "StaffRegistrationRequests", "Nric", "nvarchar(20) NOT NULL DEFAULT ''");
                await AddColumnIfMissingAsync(connection, "StaffRegistrationRequests", "PhoneNumber", "nvarchar(30) NOT NULL DEFAULT ''");
                await AddColumnIfMissingAsync(connection, "StaffRegistrationRequests", "MmcNumber", "nvarchar(50) NULL");
            }

            if (await TableExistsAsync(connection, "Doctors"))
                await EnsureUniqueDoctorEmailIndexAsync(connection);
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private static async Task EnsureUserHospitalsTableAsync(SqlConnection connection)
    {
        await ExecuteNonQueryAsync(connection, """
            IF OBJECT_ID(N'[UserHospitals]', N'U') IS NULL
            BEGIN
                CREATE TABLE [UserHospitals] (
                    [UserId] nvarchar(450) NOT NULL,
                    [HospitalId] int NOT NULL,
                    CONSTRAINT [PK_UserHospitals] PRIMARY KEY ([UserId], [HospitalId]),
                    CONSTRAINT [FK_UserHospitals_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers]([Id]) ON DELETE CASCADE,
                    CONSTRAINT [FK_UserHospitals_Hospitals_HospitalId] FOREIGN KEY ([HospitalId]) REFERENCES [Hospitals]([Id]) ON DELETE CASCADE
                );
                CREATE INDEX [IX_UserHospitals_HospitalId] ON [UserHospitals]([HospitalId]);
            END
        """);

        await ExecuteNonQueryAsync(connection, """
            INSERT INTO [UserHospitals] ([UserId], [HospitalId])
            SELECT u.[Id], u.[HospitalId]
            FROM [AspNetUsers] u
            WHERE u.[HospitalId] IS NOT NULL
              AND NOT EXISTS (
                  SELECT 1
                  FROM [UserHospitals] uh
                  WHERE uh.[UserId] = u.[Id]
                    AND uh.[HospitalId] = u.[HospitalId]
              )
        """);
    }

    private static async Task EnsurePermissionsTableAsync(SqlConnection connection)
    {
        await ExecuteNonQueryAsync(connection, """
            IF OBJECT_ID(N'[Permissions]', N'U') IS NULL
            BEGIN
                CREATE TABLE [Permissions] (
                    [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    [Code] nvarchar(128) NOT NULL,
                    [Description] nvarchar(256) NOT NULL,
                    [IsActive] bit NOT NULL DEFAULT(1)
                );
                CREATE UNIQUE INDEX [IX_Permissions_Code] ON [Permissions]([Code]);
            END
        """);
    }

    private static async Task EnsureRolePermissionsTableAsync(SqlConnection connection)
    {
        await ExecuteNonQueryAsync(connection, """
            IF OBJECT_ID(N'[RolePermissions]', N'U') IS NULL
            BEGIN
                CREATE TABLE [RolePermissions] (
                    [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    [RoleId] nvarchar(450) NOT NULL,
                    [PermissionId] int NOT NULL,
                    CONSTRAINT [FK_RolePermissions_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles]([Id]) ON DELETE CASCADE,
                    CONSTRAINT [FK_RolePermissions_Permissions_PermissionId] FOREIGN KEY ([PermissionId]) REFERENCES [Permissions]([Id]) ON DELETE CASCADE
                );
                CREATE UNIQUE INDEX [IX_RolePermissions_RoleId_PermissionId] ON [RolePermissions]([RoleId], [PermissionId]);
            END
        """);
    }

    private static async Task EnsureSystemSettingsTableAsync(SqlConnection connection)
    {
        await ExecuteNonQueryAsync(connection, """
            IF OBJECT_ID(N'[SystemSettings]', N'U') IS NULL
            BEGIN
                CREATE TABLE [SystemSettings] (
                    [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    [Category] nvarchar(100) NOT NULL,
                    [Key] nvarchar(100) NOT NULL,
                    [Value] nvarchar(max) NOT NULL,
                    [ValueType] nvarchar(50) NOT NULL,
                    [UpdatedAt] datetime2 NOT NULL,
                    [UpdatedByUserId] nvarchar(450) NOT NULL
                );
                CREATE UNIQUE INDEX [IX_SystemSettings_Category_Key] ON [SystemSettings]([Category], [Key]);
            END
        """);
    }

    private static async Task EnsureAuditLogsTableAsync(SqlConnection connection)
    {
        await ExecuteNonQueryAsync(connection, """
            IF OBJECT_ID(N'[AuditLogs]', N'U') IS NULL
            BEGIN
                CREATE TABLE [AuditLogs] (
                    [Id] bigint IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    [Action] nvarchar(150) NOT NULL,
                    [EntityType] nvarchar(100) NOT NULL,
                    [EntityId] nvarchar(100) NOT NULL,
                    [PerformedByUserId] nvarchar(450) NOT NULL,
                    [PerformedByName] nvarchar(200) NOT NULL,
                    [PerformedAt] datetime2 NOT NULL,
                    [BeforeJson] nvarchar(max) NULL,
                    [AfterJson] nvarchar(max) NULL,
                    [MetadataJson] nvarchar(max) NULL
                );
                CREATE INDEX [IX_AuditLogs_PerformedAt] ON [AuditLogs]([PerformedAt]);
                CREATE INDEX [IX_AuditLogs_EntityType_EntityId] ON [AuditLogs]([EntityType], [EntityId]);
                CREATE INDEX [IX_AuditLogs_PerformedByUserId] ON [AuditLogs]([PerformedByUserId]);
            END
        """);
    }

    private static async Task EnsureStaffRegistrationRequestsTableAsync(SqlConnection connection)
    {
        await ExecuteNonQueryAsync(connection, """
            IF OBJECT_ID(N'[StaffRegistrationRequests]', N'U') IS NULL
            BEGIN
                CREATE TABLE [StaffRegistrationRequests] (
                    [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    [Email] nvarchar(256) NOT NULL,
                    [FullName] nvarchar(256) NOT NULL,
                    [HospitalId] int NOT NULL,
                    [RequestedRole] int NOT NULL,
                    [ProtectedPassword] nvarchar(max) NOT NULL,
                    [Status] int NOT NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [ProcessedAt] datetime2 NULL,
                    [ProcessedByUserId] nvarchar(450) NULL,
                    [RejectionReason] nvarchar(1024) NULL,
                    CONSTRAINT [FK_StaffRegistrationRequests_Hospitals_HospitalId] FOREIGN KEY ([HospitalId]) REFERENCES [Hospitals]([Id]),
                    CONSTRAINT [FK_StaffRegistrationRequests_AspNetUsers_ProcessedByUserId] FOREIGN KEY ([ProcessedByUserId]) REFERENCES [AspNetUsers]([Id]) ON DELETE SET NULL
                );
                CREATE INDEX [IX_StaffRegistrationRequests_Status] ON [StaffRegistrationRequests]([Status]);
                CREATE INDEX [IX_StaffRegistrationRequests_Email] ON [StaffRegistrationRequests]([Email]);
            END
        """);
    }

    /// <summary>
    /// Merges duplicate <c>Doctors</c> rows that share the same non-empty email (case-insensitive), then adds a filtered unique index.
    /// </summary>
    private static async Task EnsureUniqueDoctorEmailIndexAsync(SqlConnection connection)
    {
        await ExecuteNonQueryAsync(connection, """
            IF OBJECT_ID('tempdb..#DoctorEmailDedupe') IS NOT NULL DROP TABLE #DoctorEmailDedupe;

            ;WITH ranked AS (
                SELECT Id,
                    FIRST_VALUE(Id) OVER (
                        PARTITION BY LOWER(LTRIM(RTRIM(Email)))
                        ORDER BY CASE WHEN UserId IS NOT NULL THEN 0 ELSE 1 END, Id
                    ) AS KeepId
                FROM Doctors
                WHERE Email IS NOT NULL AND LTRIM(RTRIM(Email)) <> N''
            )
            SELECT Id AS DupId, KeepId
            INTO #DoctorEmailDedupe
            FROM ranked
            WHERE Id <> KeepId;

            IF EXISTS (SELECT 1 FROM #DoctorEmailDedupe)
            BEGIN
                IF OBJECT_ID(N'Reports', N'U') IS NOT NULL
                BEGIN
                    UPDATE r
                    SET r.DoctorId = m.KeepId
                    FROM Reports r
                    INNER JOIN #DoctorEmailDedupe m ON m.DupId = r.DoctorId;
                END

                UPDATE u
                SET u.DoctorId = m.KeepId
                FROM AspNetUsers u
                INNER JOIN #DoctorEmailDedupe m ON m.DupId = u.DoctorId;

                UPDATE dKeep
                SET dKeep.UserId = dDup.UserId
                FROM Doctors dKeep
                INNER JOIN #DoctorEmailDedupe m ON m.KeepId = dKeep.Id
                INNER JOIN Doctors dDup ON dDup.Id = m.DupId
                WHERE dKeep.UserId IS NULL AND dDup.UserId IS NOT NULL;

                UPDATE d
                SET d.UserId = NULL
                FROM Doctors d
                INNER JOIN #DoctorEmailDedupe m ON m.DupId = d.Id;

                DELETE d
                FROM Doctors d
                INNER JOIN #DoctorEmailDedupe m ON m.DupId = d.Id;
            END

            IF OBJECT_ID('tempdb..#DoctorEmailDedupe') IS NOT NULL DROP TABLE #DoctorEmailDedupe;
        """);

        // nvarchar(max) cannot be an index key (error 1919). Shrink to nvarchar(256) to match Identity email size.
        await ExecuteNonQueryAsync(connection, """
            IF EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'Doctors')
                  AND name = N'IX_Doctors_Email'
            )
                DROP INDEX [IX_Doctors_Email] ON [Doctors];
            IF COL_LENGTH(N'Doctors', N'Email') = -1
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE object_id = OBJECT_ID(N'Doctors')
                      AND name = N'IX_Doctors_Email_UQ'
                )
                    DROP INDEX [IX_Doctors_Email_UQ] ON [Doctors];

                UPDATE [Doctors]
                SET [Email] = LEFT([Email], 256)
                WHERE [Email] IS NOT NULL AND LEN([Email]) > 256;

                ALTER TABLE [Doctors] ALTER COLUMN [Email] nvarchar(256) NULL;
            END
        """);

        await ExecuteNonQueryAsync(connection, """
            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'Doctors')
                  AND name = N'IX_Doctors_Email_UQ'
            )
            BEGIN
                CREATE UNIQUE INDEX [IX_Doctors_Email_UQ] ON [Doctors]([Email])
                WHERE [Email] IS NOT NULL AND [Email] <> N'';
            END
        """);
    }

    private static async Task DropUniqueConstraintOnReferenceNumberAsync(SqlConnection connection)
    {
        // ReferenceNumber is an external lab label, not a system-unique key.
        // Drop the old UNIQUE index if it exists, then ensure a plain non-unique index exists.
        await ExecuteNonQueryAsync(connection, """
            IF EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = 'IX_Reports_ReferenceNumber'
                  AND object_id = OBJECT_ID(N'[Reports]')
                  AND is_unique = 1
            )
            BEGIN
                DROP INDEX [IX_Reports_ReferenceNumber] ON [Reports];
                CREATE INDEX [IX_Reports_ReferenceNumber] ON [Reports]([ReferenceNumber]);
            END
        """);
    }

    private static async Task<bool> TableExistsAsync(SqlConnection connection, string tableName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT CASE WHEN EXISTS (
                SELECT 1
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_NAME = @tableName
            ) THEN 1 ELSE 0 END
        """;
        command.Parameters.AddWithValue("@tableName", tableName);

        var result = (int)(await command.ExecuteScalarAsync() ?? 0);
        return result == 1;
    }

    private static async Task AddColumnIfMissingAsync(SqlConnection connection, string tableName, string columnName, string definition)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            IF COL_LENGTH('{tableName}', '{columnName}') IS NULL
            BEGIN
                ALTER TABLE [{tableName}] ADD [{columnName}] {definition};
            END
        """;

        await command.ExecuteNonQueryAsync();
    }

    private static async Task ExecuteNonQueryAsync(SqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
