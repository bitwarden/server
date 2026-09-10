CREATE PROCEDURE [dbo].[Collection_Update]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name VARCHAR(MAX),
    @ExternalId NVARCHAR(300),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @DefaultUserCollectionEmail NVARCHAR(256) = NULL,
    @Type TINYINT = 0,
    -- Accepted and deliberately ignored. [AccessRuleId] has a single writer,
    -- [dbo].[Collection_SetAccessRuleAssociations] (cleared by [dbo].[AccessRule_DeleteById]), so this
    -- procedure must never assign it: callers pass whole-entity updates that know nothing about PAM, and
    -- assigning it here erases the association. The parameter is retained because Dapper binds every
    -- property on the Collection entity, so dropping it would raise "too many arguments specified".
    @AccessRuleId UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[Collection]
    SET
        [OrganizationId] = @OrganizationId,
        [Name] = @Name,
        [ExternalId] = @ExternalId,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate,
        [DefaultUserCollectionEmail] = @DefaultUserCollectionEmail,
        [Type] = @Type
    WHERE
        [Id] = @Id

    EXEC [dbo].[User_BumpAccountRevisionDateByCollectionId] @Id, @OrganizationId
END
