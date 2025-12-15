-- Drop deprecated ArchivedDate column (EF already removed this for other providers)
IF COL_LENGTH('[dbo].[Cipher]', 'ArchivedDate') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[Cipher] DROP COLUMN [ArchivedDate];
END;
GO
