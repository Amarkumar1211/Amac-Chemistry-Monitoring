BEGIN TRANSACTION;
CREATE TABLE [PartSpecifications] (
    [Id] int NOT NULL IDENTITY,
    [PartName] nvarchar(200) NOT NULL,
    [PartNo] nvarchar(100) NOT NULL,
    [GradeName] nvarchar(100) NOT NULL,
    [ColorCode] nvarchar(100) NULL,
    [MinTargetsJson] nvarchar(2000) NULL,
    [MaxTargetsJson] nvarchar(2000) NULL,
    [CustomerMaxTargetsJson] nvarchar(2000) NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_PartSpecifications] PRIMARY KEY ([Id])
);

CREATE UNIQUE INDEX [IX_PartSpecifications_PartNo_PartName_GradeName] ON [PartSpecifications] ([PartNo], [PartName], [GradeName]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260814045451_AddPartSpecifications', N'9.0.0');

COMMIT;
GO

