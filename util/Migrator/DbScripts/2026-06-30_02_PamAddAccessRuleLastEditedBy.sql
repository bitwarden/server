IF COL_LENGTH('[dbo].[AccessRule]', 'LastEditedBy') IS NULL
BEGIN
    ALTER TABLE [dbo].[AccessRule]
        ADD [LastEditedBy] UNIQUEIDENTIFIER NULL;
END
GO
