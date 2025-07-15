IF COL_LENGTH('[dbo].[Event]', 'ProjectId') IS NULL
 BEGIN
     ALTER TABLE [dbo].[Event]
     ADD [ProjectId] UNIQUEIDENTIFIER NULL
 END
 GO