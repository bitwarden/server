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
