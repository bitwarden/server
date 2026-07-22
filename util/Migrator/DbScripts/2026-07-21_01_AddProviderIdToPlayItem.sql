-- Add ProviderId column to PlayItem
IF COL_LENGTH('[dbo].[PlayItem]', 'ProviderId') IS NULL
BEGIN
    ALTER TABLE [dbo].[PlayItem]
        ADD [ProviderId] UNIQUEIDENTIFIER NULL;
END
GO

-- Add foreign key to Provider
IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE [name] = 'FK_PlayItem_Provider'
      AND [parent_object_id] = OBJECT_ID('[dbo].[PlayItem]')
)
BEGIN
    ALTER TABLE [dbo].[PlayItem]
        ADD CONSTRAINT [FK_PlayItem_Provider] FOREIGN KEY ([ProviderId])
            REFERENCES [dbo].[Provider] ([Id]) ON DELETE CASCADE;
END
GO

-- Add index on ProviderId
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_PlayItem_ProviderId'
      AND [object_id] = OBJECT_ID('[dbo].[PlayItem]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PlayItem_ProviderId]
        ON [dbo].[PlayItem]([ProviderId] ASC);
END
GO

-- Relax the check constraint to require exactly one of UserId / OrganizationId / ProviderId
IF EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE [name] = 'CK_PlayItem_UserOrOrganization'
      AND [parent_object_id] = OBJECT_ID('[dbo].[PlayItem]')
)
BEGIN
    ALTER TABLE [dbo].[PlayItem]
        DROP CONSTRAINT [CK_PlayItem_UserOrOrganization];
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE [name] = 'CK_PlayItem_UserOrOrganizationOrProvider'
      AND [parent_object_id] = OBJECT_ID('[dbo].[PlayItem]')
)
BEGIN
    ALTER TABLE [dbo].[PlayItem]
        ADD CONSTRAINT [CK_PlayItem_UserOrOrganizationOrProvider] CHECK (
            (CASE WHEN [UserId] IS NOT NULL THEN 1 ELSE 0 END
           + CASE WHEN [OrganizationId] IS NOT NULL THEN 1 ELSE 0 END
           + CASE WHEN [ProviderId] IS NOT NULL THEN 1 ELSE 0 END) = 1);
END
GO

-- Update PlayItem_Create stored procedure
CREATE OR ALTER PROCEDURE [dbo].[PlayItem_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @PlayId NVARCHAR(256),
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @CreationDate DATETIME2(7),
    @ProviderId UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[PlayItem]
    (
        [Id],
        [PlayId],
        [UserId],
        [OrganizationId],
        [CreationDate],
        [ProviderId]
    )
    VALUES
    (
        @Id,
        @PlayId,
        @UserId,
        @OrganizationId,
        @CreationDate,
        @ProviderId
    )
END
GO

-- Update PlayItem_ReadByPlayId stored procedure
CREATE OR ALTER PROCEDURE [dbo].[PlayItem_ReadByPlayId]
    @PlayId NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [Id],
        [PlayId],
        [UserId],
        [OrganizationId],
        [CreationDate],
        [ProviderId]
    FROM
        [dbo].[PlayItem]
    WHERE
        [PlayId] = @PlayId
END
GO
