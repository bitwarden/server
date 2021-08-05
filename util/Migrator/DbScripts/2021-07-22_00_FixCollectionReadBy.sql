IF OBJECT_ID('[dbo].[Collection_ReadByUserId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Collection_ReadByUserId]
END
GO

CREATE PROCEDURE [dbo].[Collection_ReadByUserId]
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
        MIN([HidePasswords]) AS [HidePasswords]
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
GO

IF OBJECT_ID('[dbo].[Collection_ReadByIdUserId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Collection_ReadByIdUserId]
END
GO

CREATE PROCEDURE [dbo].[Collection_ReadByIdUserId]
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
        MIN([HidePasswords]) AS [HidePasswords]
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
