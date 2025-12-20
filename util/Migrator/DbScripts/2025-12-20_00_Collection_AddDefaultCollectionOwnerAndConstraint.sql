-- Add DefaultCollectionOwner column to Collection table for Type=1 collections
-- This enables a filtered unique constraint to prevent duplicate default collections

IF NOT EXISTS (
    SELECT *
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'dbo'
      AND TABLE_NAME = 'Collection'
      AND COLUMN_NAME = 'DefaultCollectionOwner'
)
BEGIN
    ALTER TABLE [dbo].[Collection]
        ADD [DefaultCollectionOwner] UNIQUEIDENTIFIER NULL
END
GO

-- Add foreign key constraint to OrganizationUser
IF NOT EXISTS (
    SELECT *
    FROM sys.foreign_keys
    WHERE name = 'FK_Collection_OrganizationUser'
      AND parent_object_id = OBJECT_ID('[dbo].[Collection]')
)
BEGIN
    ALTER TABLE [dbo].[Collection]
        ADD CONSTRAINT [FK_Collection_OrganizationUser]
        FOREIGN KEY ([DefaultCollectionOwner])
        REFERENCES [dbo].[OrganizationUser] ([Id])
        ON DELETE NO ACTION
END
GO

-- Create filtered unique index to prevent duplicate default collections per user
IF NOT EXISTS (
    SELECT *
    FROM sys.indexes
    WHERE name = 'IX_Collection_DefaultCollectionOwner_OrganizationId_Type'
      AND object_id = OBJECT_ID('[dbo].[Collection]')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_Collection_DefaultCollectionOwner_OrganizationId_Type]
        ON [dbo].[Collection]([DefaultCollectionOwner], [OrganizationId], [Type])
        WHERE [Type] = 1;
END
GO

-- Refresh dependent views to include new column
IF OBJECT_ID('[dbo].[CollectionView]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[CollectionView]';
END
GO

IF OBJECT_ID('[dbo].[Collection_ReadById]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[Collection_ReadById]';
END
GO

IF OBJECT_ID('[dbo].[Collection_ReadByIds]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[Collection_ReadByIds]';
END
GO

IF OBJECT_ID('[dbo].[Collection_ReadByOrganizationId]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[Collection_ReadByOrganizationId]';
END
GO

IF OBJECT_ID('[dbo].[UserCollectionDetails]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[UserCollectionDetails]';
END
GO
