-- TODO! Handle existing data in EF if releasing before this. This assumes the column is populated, if it isn't then it'll create at least 1 duplicate.
-- Add DefaultCollectionOwnerId column to Collection table for Type=1 collections
-- This enables a filtered unique constraint to prevent duplicate default collections

IF NOT EXISTS (
    SELECT *
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'dbo'
      AND TABLE_NAME = 'Collection'
      AND COLUMN_NAME = 'DefaultCollectionOwnerId'
)
BEGIN
    ALTER TABLE [dbo].[Collection]
        ADD [DefaultCollectionOwnerId] UNIQUEIDENTIFIER NULL
END
GO

-- Populate DefaultCollectionOwnerId for existing Type=1 collections
-- Find the OrganizationUser with Manage permissions on each default collection
IF EXISTS (
    SELECT *
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'dbo'
      AND TABLE_NAME = 'Collection'
      AND COLUMN_NAME = 'DefaultCollectionOwnerId'
)
BEGIN
    UPDATE c
    SET c.[DefaultCollectionOwnerId] = cu.[OrganizationUserId]
    FROM [dbo].[Collection] c
    INNER JOIN [dbo].[CollectionUser] cu ON c.[Id] = cu.[CollectionId]
    WHERE c.[Type] = 1
      AND cu.[Manage] = 1
      AND c.[DefaultCollectionOwnerId] IS NULL
END
GO

-- Handle duplicate default collections for the same user/organization
-- Convert duplicates to SharedCollections (Type=0), keeping the oldest one
IF EXISTS (
    SELECT *
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'dbo'
      AND TABLE_NAME = 'Collection'
      AND COLUMN_NAME = 'DefaultCollectionOwnerId'
)
BEGIN
    DECLARE @UtcNow DATETIME2(7) = GETUTCDATE();

    -- Find duplicate default collections (same OrganizationUserId + OrganizationId + Type=1)
    -- Keep the oldest one, convert the rest to SharedCollections
    WITH DuplicateCollections AS (
        SELECT
            c.[Id],
            c.[OrganizationId],
            c.[DefaultCollectionOwnerId],
            c.[CreationDate],
            ROW_NUMBER() OVER (
                PARTITION BY c.[DefaultCollectionOwnerId], c.[OrganizationId]
                ORDER BY c.[CreationDate] ASC
            ) AS RowNum
        FROM [dbo].[Collection] c
        WHERE c.[Type] = 1
          AND c.[DefaultCollectionOwnerId] IS NOT NULL
    )
    UPDATE c
    SET
        c.[Type] = 0,  -- Convert to SharedCollection
        c.[DefaultCollectionOwnerId] = NULL,
        c.[RevisionDate] = @UtcNow
    FROM [dbo].[Collection] c
    INNER JOIN DuplicateCollections dc ON c.[Id] = dc.[Id]
    WHERE dc.RowNum > 1  -- Keep only the first (oldest) collection
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
        FOREIGN KEY ([DefaultCollectionOwnerId])
        REFERENCES [dbo].[OrganizationUser] ([Id])
        ON DELETE NO ACTION
END
GO

-- Create filtered unique index to prevent duplicate default collections per user
IF NOT EXISTS (
    SELECT *
    FROM sys.indexes
    WHERE name = 'IX_Collection_DefaultCollectionOwnerId_OrganizationId_Type'
      AND object_id = OBJECT_ID('[dbo].[Collection]')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_Collection_DefaultCollectionOwnerId_OrganizationId_Type]
        ON [dbo].[Collection]([DefaultCollectionOwnerId], [OrganizationId], [Type])
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
