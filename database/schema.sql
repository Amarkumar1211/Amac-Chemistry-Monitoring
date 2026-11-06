/* =====================================================================
   Furnace Monitoring & Control System - SQL Server Schema
   Target: SQL Server 2019+
   ===================================================================== */

CREATE DATABASE FurnaceMonitor;
GO

USE FurnaceMonitor;
GO

/* ---------------------------------------------------------------------
   Roles
   --------------------------------------------------------------------- */
IF OBJECT_ID('dbo.Roles', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Roles (
        Id          INT IDENTITY(1,1) NOT NULL,
        Name        NVARCHAR(50)      NOT NULL,
        Description NVARCHAR(250)     NULL,
        RoleType    INT               NOT NULL,
        CreatedAtUtc DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(7)     NULL,
        CreatedBy   NVARCHAR(100)     NULL,
        UpdatedBy   NVARCHAR(100)     NULL,
        CONSTRAINT PK_Roles PRIMARY KEY (Id),
        CONSTRAINT UQ_Roles_Name UNIQUE (Name)
    );
END
GO

/* ---------------------------------------------------------------------
   Users
   --------------------------------------------------------------------- */
IF OBJECT_ID('dbo.Users', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users (
        Id                 INT IDENTITY(1,1) NOT NULL,
        UserName           NVARCHAR(50)      NOT NULL,
        PasswordHash       NVARCHAR(200)     NOT NULL,
        DisplayName        NVARCHAR(100)     NOT NULL,
        Email              NVARCHAR(150)     NULL,
        IsActive           BIT               NOT NULL DEFAULT 1,
        IsSystem           BIT               NOT NULL DEFAULT 0,
        LastLoginAtUtc     DATETIME2(7)      NULL,
        RoleId             INT               NOT NULL,
        RefreshToken       NVARCHAR(500)     NULL,
        RefreshTokenExpiryUtc DATETIME2(7)   NULL,
        CreatedAtUtc       DATETIME2(7)      NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc       DATETIME2(7)      NULL,
        CreatedBy          NVARCHAR(100)     NULL,
        UpdatedBy          NVARCHAR(100)     NULL,
        CONSTRAINT PK_Users PRIMARY KEY (Id),
        CONSTRAINT UQ_Users_UserName UNIQUE (UserName),
        CONSTRAINT FK_Users_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles(Id)
    );
END
GO

/* ---------------------------------------------------------------------
   Furnaces
   --------------------------------------------------------------------- */
IF OBJECT_ID('dbo.Furnaces', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Furnaces (
        Id           INT IDENTITY(1,1) NOT NULL,
        Number       NVARCHAR(10)      NOT NULL,
        Name         NVARCHAR(100)     NOT NULL,
        Status       INT               NOT NULL DEFAULT 1,
        IsActive     BIT               NOT NULL DEFAULT 1,
        Location     NVARCHAR(150)     NULL,
        SortOrder    INT               NOT NULL DEFAULT 0,
        CreatedAtUtc DATETIME2(7)      NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(7)      NULL,
        CreatedBy    NVARCHAR(100)     NULL,
        UpdatedBy    NVARCHAR(100)     NULL,
        CONSTRAINT PK_Furnaces PRIMARY KEY (Id),
        CONSTRAINT UQ_Furnaces_Number UNIQUE (Number)
    );
END
GO

/* ---------------------------------------------------------------------
   FurnaceBatches
   --------------------------------------------------------------------- */
IF OBJECT_ID('dbo.FurnaceBatches', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.FurnaceBatches (
        Id              INT IDENTITY(1,1) NOT NULL,
        BatchNumber     NVARCHAR(50)      NOT NULL,
        FurnaceId       INT               NOT NULL,
        OperatorId      INT               NULL,
        Status          INT               NOT NULL DEFAULT 1,
        IsActive        BIT               NOT NULL DEFAULT 1,
        StartedAtUtc    DATETIME2(7)      NOT NULL DEFAULT SYSUTCDATETIME(),
        EndedAtUtc      DATETIME2(7)      NULL,
        Indicator       INT               NOT NULL DEFAULT 1,
        Instruction     NVARCHAR(500)     NULL,
        Remarks         NVARCHAR(1000)    NULL,
        ClosedBy        NVARCHAR(100)     NULL,
        ClosedTimeUtc   DATETIME2(7)      NULL,
        CreatedAtUtc    DATETIME2(7)      NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc    DATETIME2(7)      NULL,
        CreatedBy       NVARCHAR(100)     NULL,
        UpdatedBy       NVARCHAR(100)     NULL,
        CONSTRAINT PK_FurnaceBatches PRIMARY KEY (Id),
        CONSTRAINT UQ_FurnaceBatches_BatchNumber UNIQUE (BatchNumber),
        CONSTRAINT FK_FurnaceBatches_Furnaces FOREIGN KEY (FurnaceId) REFERENCES dbo.Furnaces(Id),
        CONSTRAINT FK_FurnaceBatches_Users FOREIGN KEY (OperatorId) REFERENCES dbo.Users(Id)
    );
END
GO

/* Partial unique index: only ONE active batch per furnace
   (only a single row with IsActive=1 allowed per FurnaceId). */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_FurnaceBatches_OneActivePerFurnace')
BEGIN
    CREATE UNIQUE INDEX UX_FurnaceBatches_OneActivePerFurnace
        ON dbo.FurnaceBatches (FurnaceId) WHERE IsActive = 1;
END
GO

/* ---------------------------------------------------------------------
   FurnaceChemicals
   --------------------------------------------------------------------- */
IF OBJECT_ID('dbo.FurnaceChemicals', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.FurnaceChemicals (
        Id              INT IDENTITY(1,1) NOT NULL,
        BatchId         INT               NOT NULL,
        Si              DECIMAL(18,4)     NOT NULL DEFAULT 0,
        Ti              DECIMAL(18,4)     NOT NULL DEFAULT 0,
        Mg              DECIMAL(18,4)     NOT NULL DEFAULT 0,
        FutureChemical1 DECIMAL(18,4)     NULL,
        FutureChemical2 DECIMAL(18,4)     NULL,
        FutureChemical3 DECIMAL(18,4)     NULL,
        Indicator       INT               NOT NULL DEFAULT 1,
        Instruction     NVARCHAR(500)     NULL,
        Remarks         NVARCHAR(1000)    NULL,
        CapturedAtUtc   DATETIME2(7)      NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedAtUtc    DATETIME2(7)      NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc    DATETIME2(7)      NULL,
        CreatedBy       NVARCHAR(100)     NULL,
        UpdatedBy       NVARCHAR(100)     NULL,
        CONSTRAINT PK_FurnaceChemicals PRIMARY KEY (Id),
        CONSTRAINT FK_FurnaceChemicals_Batches FOREIGN KEY (BatchId) REFERENCES dbo.FurnaceBatches(Id) ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IDX_FurnaceChemicals_Batch_Captured' AND object_id = OBJECT_ID('dbo.FurnaceChemicals'))
BEGIN
    CREATE INDEX IDX_FurnaceChemicals_Batch_Captured
        ON dbo.FurnaceChemicals (BatchId, CapturedAtUtc);
END
GO

/* ---------------------------------------------------------------------
   SignalRConnections
   --------------------------------------------------------------------- */
IF OBJECT_ID('dbo.SignalRConnections', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SignalRConnections (
        Id              INT IDENTITY(1,1) NOT NULL,
        ConnectionId    NVARCHAR(100)     NOT NULL,
        UserId          INT               NULL,
        FurnaceNumber   NVARCHAR(10)      NULL,
        ConnectedAtUtc  DATETIME2(7)      NOT NULL DEFAULT SYSUTCDATETIME(),
        DisconnectedAtUtc DATETIME2(7)    NULL,
        IsConnected     BIT               NOT NULL DEFAULT 1,
        UserAgent       NVARCHAR(500)     NULL,
        IpAddress       NVARCHAR(50)      NULL,
        CreatedAtUtc    DATETIME2(7)      NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc    DATETIME2(7)      NULL,
        CreatedBy       NVARCHAR(100)     NULL,
        UpdatedBy       NVARCHAR(100)     NULL,
        CONSTRAINT PK_SignalR PRIMARY KEY (Id),
        CONSTRAINT UQ_SignalR_ConnectionId UNIQUE (ConnectionId)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IDX_SignalR_IsConnected' AND object_id = OBJECT_ID('dbo.SignalRConnections'))
BEGIN
    CREATE INDEX IDX_SignalR_IsConnected ON dbo.SignalRConnections (IsConnected);
END
GO

/* ---------------------------------------------------------------------
   AuditLogs
   --------------------------------------------------------------------- */
IF OBJECT_ID('dbo.AuditLogs', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AuditLogs (
        Id            INT IDENTITY(1,1) NOT NULL,
        UserName      NVARCHAR(100)     NULL,
        UserId        INT               NULL,
        Action        INT               NOT NULL,
        Description   NVARCHAR(1000)    NULL,
        Entity        NVARCHAR(100)     NULL,
        EntityId      NVARCHAR(50)      NULL,
        IpAddress     NVARCHAR(50)      NULL,
        Payload       NVARCHAR(MAX)     NULL,
        OccurredAtUtc DATETIME2(7)      NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedAtUtc  DATETIME2(7)      NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc  DATETIME2(7)      NULL,
        CreatedBy     NVARCHAR(100)     NULL,
        UpdatedBy     NVARCHAR(100)     NULL,
        CONSTRAINT PK_AuditLogs PRIMARY KEY (Id)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IDX_AuditLogs_Occurred' AND object_id = OBJECT_ID('dbo.AuditLogs'))
BEGIN
    CREATE INDEX IDX_AuditLogs_Occurred ON dbo.AuditLogs (OccurredAtUtc);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IDX_AuditLogs_Action' AND object_id = OBJECT_ID('dbo.AuditLogs'))
BEGIN
    CREATE INDEX IDX_AuditLogs_Action ON dbo.AuditLogs (Action, OccurredAtUtc);
END
GO

/* ---------------------------------------------------------------------
   UserSessions
   --------------------------------------------------------------------- */
IF OBJECT_ID('dbo.UserSessions', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserSessions (
        Id           INT IDENTITY(1,1) NOT NULL,
        UserId       INT               NOT NULL,
        JwtId        NVARCHAR(100)     NULL,
        IpAddress    NVARCHAR(50)      NULL,
        UserAgent    NVARCHAR(500)     NULL,
        StartedAtUtc DATETIME2(7)      NOT NULL DEFAULT SYSUTCDATETIME(),
        EndedAtUtc   DATETIME2(7)      NULL,
        IsActive     BIT               NOT NULL DEFAULT 1,
        CreatedAtUtc DATETIME2(7)      NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(7)      NULL,
        CreatedBy    NVARCHAR(100)     NULL,
        UpdatedBy    NVARCHAR(100)     NULL,
        CONSTRAINT PK_UserSessions PRIMARY KEY (Id),
        CONSTRAINT FK_UserSessions_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id) ON DELETE CASCADE
    );
END
GO

