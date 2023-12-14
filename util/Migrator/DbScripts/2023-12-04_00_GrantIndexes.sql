IF COL_LENGTH('[dbo].[Grant]', 'Id') IS NULL
BEGIN
    ALTER TABLE [dbo].[Grant]
        ADD [Id] INT NOT NULL IDENTITY

    ALTER TABLE [dbo].[Grant]
        ALTER COLUMN [Key] NVARCHAR (200) NULL

    ALTER TABLE [dbo].[Grant]
        DROP CONSTRAINT [PK_Grant];

    ALTER TABLE [dbo].[Grant]
        ADD CONSTRAINT [PK_Grant] PRIMARY KEY CLUSTERED ([Id] ASC);

    CREATE UNIQUE INDEX [IX_Grant_Key]
        ON [dbo].[Grant]([Key])
        WHERE [Key] IS NOT NULL;
END
GO

IF EXISTS(SELECT *
FROM sys.views
WHERE [Name] = 'GrantView')
    BEGIN
    DROP VIEW [dbo].[GrantView];
END
GO

CREATE VIEW [dbo].[GrantView]
AS
    SELECT
        *
    FROM
        [dbo].[Grant]
GO

IF EXISTS(SELECT name
FROM sys.indexes
WHERE name = 'IX_Grant_SubjectId_ClientId_Type')
    BEGIN
    DROP INDEX [IX_Grant_SubjectId_ClientId_Type] ON [dbo].[Grant]
END
GO

IF EXISTS(SELECT name
FROM sys.indexes
WHERE name = 'IX_Grant_SubjectId_SessionId_Type')
    BEGIN
    DROP INDEX [IX_Grant_SubjectId_SessionId_Type] ON [dbo].[Grant]
END
GO
