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
    -- referencing rows must be detached before the rule can be removed. A cleared collection is simply ungoverned; the
    -- RuleDeleted audit event already carries the rule's name (written by the command), so the row need not survive.
    UPDATE [dbo].[Collection]
    SET [AccessRuleId] = NULL,
        [RevisionDate] = SYSUTCDATETIME()
    WHERE [AccessRuleId] = @Id

    DELETE FROM [dbo].[AccessRule]
    WHERE [Id] = @Id

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
END
