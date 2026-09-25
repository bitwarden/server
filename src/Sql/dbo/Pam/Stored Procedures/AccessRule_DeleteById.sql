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

    -- Clear the collection links first: the FK Collection.AccessRuleId -> AccessRule is ON DELETE NO ACTION, so the
    -- referencing rows must be detached before the rule can be removed. A cleared collection is simply ungoverned.
    UPDATE [dbo].[Collection]
    SET [AccessRuleId] = NULL,
        [RevisionDate] = SYSUTCDATETIME()
    WHERE [AccessRuleId] = @Id

    -- Detach the requests that pinned this rule, for the same reason: FK_AccessRequest_AccessRule is ON DELETE
    -- NO ACTION, so any request that recorded this rule as its governing rule would block the delete outright. RuleId
    -- is provenance rather than authority -- the request's own window and decision log are what was actually granted,
    -- and the column is already nullable for requests that were never gated through a stored rule.
    UPDATE [dbo].[AccessRequest]
    SET [RuleId] = NULL
    WHERE [RuleId] = @Id

    DELETE FROM [dbo].[AccessRule]
    WHERE [Id] = @Id

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
END
