/* =====================================================================
   Furnace Monitoring & Control System - Seed Data
   Roles, default admin user and furnaces F1-F20
   ===================================================================== */

USE FurnaceMonitor;
GO

/* ---------------------------------------------------------------------
   Roles
   --------------------------------------------------------------------- */
SET IDENTITY_INSERT dbo.Roles ON;
IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE Name = 'Admin')
    INSERT INTO dbo.Roles (Id, Name, Description, RoleType, CreatedAtUtc)
    VALUES (1, N'Admin', N'Full management rights', 1, SYSUTCDATETIME());
IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE Name = 'Operator')
    INSERT INTO dbo.Roles (Id, Name, Description, RoleType, CreatedAtUtc)
    VALUES (2, N'Operator', N'Operates furnaces and batches', 2, SYSUTCDATETIME());
IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE Name = 'Viewer')
    INSERT INTO dbo.Roles (Id, Name, Description, RoleType, CreatedAtUtc)
    VALUES (3, N'Viewer', N'Read-only display access', 3, SYSUTCDATETIME());
SET IDENTITY_INSERT dbo.Roles OFF;
GO

/* ---------------------------------------------------------------------
   Default Admin user
   Username : admin
   Password : Admin@123
   NOTE    : The PasswordHash below is a BCrypt hash of "Admin@123".
             The application recomputes/verifies with BCrypt on login.
   --------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE UserName = 'admin')
    INSERT INTO dbo.Users (UserName, PasswordHash, DisplayName, Email, IsActive, IsSystem, RoleId, CreatedAtUtc)
    VALUES (
        N'admin',
        N'$2a$11$7D5mXy8kQeR0u1vWzY6tUe6FhXqZ9Yk2VlNcObQpTgCmIwLs2oNnK',
        N'System Administrator',
        N'admin@furnace.local',
        1,
        1,
        1,
        SYSUTCDATETIME()
    );
GO

/* ---------------------------------------------------------------------
   Furnaces F1 - F20
   --------------------------------------------------------------------- */
DECLARE @i INT = 1;
WHILE @i <= 20
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.Furnaces WHERE Number = CONCAT('F', @i))
        INSERT INTO dbo.Furnaces (Number, Name, Status, IsActive, SortOrder, CreatedAtUtc)
        VALUES (
            CONCAT('F', @i),
            CONCAT('Furnace ', @i),
            1,
            1,
            @i,
            SYSUTCDATETIME()
        );
    SET @i = @i + 1;
END
GO

