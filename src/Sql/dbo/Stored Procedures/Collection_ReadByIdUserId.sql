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
        MIN([HidePasswords]) AS [HidePasswords],
        MIN([Manage]) AS [Manage]
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
