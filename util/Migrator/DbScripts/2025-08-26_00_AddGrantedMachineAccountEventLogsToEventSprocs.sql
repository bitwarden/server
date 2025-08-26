IF COL_LENGTH('[dbo].[Event]', 'GrantedServiceAccountId') IS NULL
BEGIN
    EXEC('ALTER TABLE [dbo].[Event] ADD [GrantedServiceAccountId] UNIQUEIDENTIFIER NULL');
END
GO

IF OBJECT_ID('[dbo].[EventView]', 'V') IS NOT NULL
BEGIN
    DROP VIEW [dbo].[EventView];
END
GO

CREATE VIEW [dbo].[EventView]
AS
SELECT * FROM [dbo].[Event];
GO
