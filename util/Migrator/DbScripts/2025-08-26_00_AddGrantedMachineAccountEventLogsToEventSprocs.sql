IF COL_LENGTH('[dbo].[Event]', 'GrantedServiceAccountId') IS NULL
BEGIN
    ALTER TABLE [dbo].[Event]
        ADD [GrantedServiceAccountId] UNIQUEIDENTIFIER NULL;
END
GO

IF OBJECT_ID('[dbo].[EventView]', 'V') IS NOT NULL
BEGIN
    EXECUTE sp_refreshview N'[dbo].[EventView]'
END
GO

CREATE VIEW [dbo].[EventView]
AS
SELECT * FROM [dbo].[Event];
GO
