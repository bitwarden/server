IF COL_LENGTH('[dbo].[Event]', 'ProjectId') IS NULL
BEGIN
    EXEC('ALTER TABLE [dbo].[Event] ADD [ProjectId] UNIQUEIDENTIFIER NULL');
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