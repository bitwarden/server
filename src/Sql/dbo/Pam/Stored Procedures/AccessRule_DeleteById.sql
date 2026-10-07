CREATE PROCEDURE [dbo].[AccessRule_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @OrganizationId UNIQUEIDENTIFIER

    SELECT @OrganizationId = [OrganizationId]
    FROM [dbo].[AccessRule]
    WHERE [Id] = @Id

    IF @OrganizationId IS NULL
    BEGIN
        -- Already gone: idempotent no-op.
        RETURN
    END

    -- Detach collections first, since FK Collection.AccessRuleId is ON DELETE NO ACTION. A detached collection is
    -- ungoverned.
    UPDATE [dbo].[Collection]
    SET [AccessRuleId] = NULL,
        [RevisionDate] = SYSUTCDATETIME()
    WHERE [AccessRuleId] = @Id

    -- Detach the requests that pinned this rule, since FK_AccessRequest_AccessRule is ON DELETE NO ACTION too. RuleId
    -- is provenance, not authority, so clearing it changes no grant.
    UPDATE [dbo].[AccessRequest]
    SET [RuleId] = NULL
    WHERE [RuleId] = @Id

    DELETE FROM [dbo].[AccessRule]
    WHERE [Id] = @Id

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
END
