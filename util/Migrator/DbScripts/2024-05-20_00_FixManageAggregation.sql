-- We were aggregating CollectionGroup permissions using MIN([Manage]) instead of MAX.
-- If the user is a member of multiple groups with overlapping collection permissions, they should get the most
-- generous permissions, not the least. This is consistent with ReadOnly and HidePasswords columns.
-- Updating both current and V2 sprocs out of caution and because they still need to be reviewed/cleaned up.

-- Collection_ReadByIdUserId
CREATE OR ALTER PROCEDURE [dbo].[Collection_ReadByIdUserId]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
    SELECT
        Id,
        OrganizationId,
        [Name],
        CreationDate,
        RevisionDate,
        ExternalId,
        MIN([ReadOnly]) AS [ReadOnly],
        MIN([HidePasswords]) AS [HidePasswords],
        MAX([Manage]) AS [Manage]
    FROM
        [dbo].[UserCollectionDetails](@UserId)
    WHERE
        [Id] = @Id
    GROUP BY
        Id,
        OrganizationId,
        [Name],
        CreationDate,
        RevisionDate,
        ExternalId
END
GO;

-- Collection_ReadByIdUserId_V2
CREATE OR ALTER PROCEDURE [dbo].[Collection_ReadByIdUserId_V2]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
    SELECT
        Id,
        OrganizationId,
        [Name],
        CreationDate,
        RevisionDate,
        ExternalId,
        MIN([ReadOnly]) AS [ReadOnly],
        MIN([HidePasswords]) AS [HidePasswords],
        MAX([Manage]) AS [Manage]
    FROM
        [dbo].[UserCollectionDetails_V2](@UserId)
    WHERE
        [Id] = @Id
    GROUP BY
        Id,
        OrganizationId,
        [Name],
        CreationDate,
        RevisionDate,
        ExternalId
END
GO;

-- Collection_ReadByUserId
CREATE OR ALTER PROCEDURE [dbo].[Collection_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        Id,
        OrganizationId,
        [Name],
        CreationDate,
        RevisionDate,
        ExternalId,
        MIN([ReadOnly]) AS [ReadOnly],
        MIN([HidePasswords]) AS [HidePasswords],
        MAX([Manage]) AS [Manage]
    FROM
        [dbo].[UserCollectionDetails](@UserId)
    GROUP BY
        Id,
        OrganizationId,
        [Name],
        CreationDate,
        RevisionDate,
        ExternalId
END
GO;

-- Collection_ReadByUserId_V2
CREATE OR ALTER PROCEDURE [dbo].[Collection_ReadByUserId_V2]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        Id,
        OrganizationId,
        [Name],
        CreationDate,
        RevisionDate,
        ExternalId,
        MIN([ReadOnly]) AS [ReadOnly],
        MIN([HidePasswords]) AS [HidePasswords],
        MAX([Manage]) AS [Manage]
    FROM
        [dbo].[UserCollectionDetails_V2](@UserId)
    GROUP BY
        Id,
        OrganizationId,
        [Name],
        CreationDate,
        RevisionDate,
        ExternalId
END
GO;
