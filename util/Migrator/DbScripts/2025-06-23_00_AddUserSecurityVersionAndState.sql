IF COL_LENGTH('[dbo].[User]', 'SecurityVersion') IS NULL
BEGIN
    ALTER TABLE [dbo].[User]
    ADD [SecurityVersion] INT NULL;
END
GO

IF COL_LENGTH('[dbo].[User]', 'SecurityState') IS NULL
BEGIN
    ALTER TABLE [dbo].[User]
    ADD [SecurityState] NVARCHAR(MAX) NULL;
END
GO
