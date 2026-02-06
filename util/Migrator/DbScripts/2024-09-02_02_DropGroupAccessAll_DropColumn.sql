-- Finalise removal of Group.AccessAll column
-- Drop the column

IF OBJECT_ID('[dbo].[DF_Group_AccessAll]', 'D') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[Group]
    DROP CONSTRAINT [DF_Group_AccessAll];
END
GO

IF COL_LENGTH('[dbo].[Group]', 'AccessAll') IS NOT NULL
BEGIN
    ALTER TABLE
        [dbo].[Group]
    DROP COLUMN
        [AccessAll]
END
GO

-- Refresh views
IF OBJECT_ID('[dbo].[GroupView]') IS NOT NULL
    BEGIN
        EXECUTE sp_refreshsqlmodule N'[dbo].[GroupView]';
    END
GO
