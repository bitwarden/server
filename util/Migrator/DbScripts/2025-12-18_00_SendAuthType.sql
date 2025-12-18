IF COL_LENGTH('[dbo].[Send]', 'AuthType') IS NULL
BEGIN
ALTER TABLE [dbo].[Send]
    ADD [AuthType] TINYINT NULL;
END
GO
