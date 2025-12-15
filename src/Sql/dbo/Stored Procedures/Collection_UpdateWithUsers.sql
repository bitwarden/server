CREATE PROCEDURE [dbo].[Collection_UpdateWithUsers]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name VARCHAR(MAX),
    @ExternalId NVARCHAR(300),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @Users AS [dbo].[CollectionAccessSelectionType] READONLY,
    @DefaultUserCollectionEmail NVARCHAR(256) = NULL,
    @Type TINYINT = 0
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[Collection_Update] @Id, @OrganizationId, @Name, @ExternalId, @CreationDate, @RevisionDate, @DefaultUserCollectionEmail, @Type

    -- Users
    -- Delete users that are no longer in source
    DELETE
        cu
    FROM
        [dbo].[CollectionUser] cu
    LEFT JOIN
        @Users u ON cu.OrganizationUserId = u.Id
    WHERE
        cu.CollectionId = @Id
            AND u.Id IS NULL;

    -- Update existing users
    UPDATE
        cu
    SET
        cu.ReadOnly = u.ReadOnly,
        cu.HidePasswords = u.HidePasswords,
        cu.Manage = u.Manage
    FROM
        [dbo].[CollectionUser] cu
    INNER JOIN
        @Users u ON cu.OrganizationUserId = u.Id
    WHERE
        cu.CollectionId = @Id
            AND (
                cu.ReadOnly != u.ReadOnly
                    OR cu.HidePasswords != u.HidePasswords
                    OR cu.Manage != u.Manage
            );

    -- Insert new users
    INSERT INTO [dbo].[CollectionUser]
    (
        [CollectionId],
        [OrganizationUserId],
        [ReadOnly],
        [HidePasswords],
        [Manage]
    )
    SELECT
        @Id,
        u.Id,
        u.ReadOnly,
        u.HidePasswords,
        u.Manage
    FROM
        @Users u
    INNER JOIN
        [dbo].[OrganizationUser] ou ON ou.Id = u.Id
    LEFT JOIN
        [dbo].[CollectionUser] cu ON cu.CollectionId = @Id AND cu.OrganizationUserId = u.Id
    WHERE
        ou.OrganizationId = @OrganizationId
            AND cu.CollectionId IS NULL;

    EXEC [dbo].[User_BumpAccountRevisionDateByCollectionId] @Id, @OrganizationId
END
