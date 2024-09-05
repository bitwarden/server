-- Finalise removal of Group.AccessAll column
-- Add default column value
-- Sprocs already have default value for rollback purposes, this just supports dropping the column itself

IF OBJECT_ID('[dbo].[DF_Group_AccessAll]', 'D') IS NULL
BEGIN
    ALTER TABLE [dbo].[Group]
    ADD CONSTRAINT [DF_Group_AccessAll] DEFAULT (0) FOR [AccessAll];
END
GO
