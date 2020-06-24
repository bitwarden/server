IF COL_LENGTH('[dbo].[Organization]', 'Identifier') IS NULL
BEGIN
    ALTER TABLE
        [dbo].[Organization]
    ADD
        [Identifier] NVARCHAR (50) NULL
END
GO

IF NOT EXISTS (
    SELECT * FROM sys.indexes  WHERE [Name]='IX_Organization_Identifier'
    AND object_id = OBJECT_ID('[dbo].[Organization]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Organization_Identifier]
        ON [dbo].[Organization]([Identifier] ASC)
END
GO
