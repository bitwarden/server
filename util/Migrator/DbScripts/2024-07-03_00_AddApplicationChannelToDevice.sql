-- Add ApplicationChannel column to Device table
IF COL_LENGTH('[dbo].[Device]', 'ApplicationChannel') IS NULL
    BEGIN
        ALTER TABLE
            [dbo].[Device]
        ADD
            [ApplicationChannel] TINYINT DEFAULT 0 NOT NULL; -- Default to production channel
    END
GO
