IF COL_LENGTH('[dbo].[AccessRequest]', 'RejectedDate') IS NULL
BEGIN
    ALTER TABLE [dbo].[AccessRequest]
        ADD [RejectedDate] DATETIME2 (7) NULL;
END
GO
