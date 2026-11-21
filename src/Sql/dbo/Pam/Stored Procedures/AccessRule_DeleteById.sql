CREATE PROCEDURE [dbo].[AccessRule_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
    SET XACT_ABORT ON

    DECLARE @OrganizationId UNIQUEIDENTIFIER
    DECLARE @Enabled BIT
    DECLARE @ClearedCollectionIds AS [dbo].[GuidIdArray]

    BEGIN TRY
        BEGIN TRANSACTION

        -- Lock the rule before its collections, the same order as AccessRule_Update, so a concurrent toggle waits for
        -- this delete and Enabled cannot change underneath it. A rule that is already gone matches nothing below.
        SELECT @OrganizationId = [OrganizationId],
            @Enabled = [Enabled]
        FROM [dbo].[AccessRule] WITH (UPDLOCK, HOLDLOCK)
        WHERE [Id] = @Id

        -- Clear the collection links first: the FK Collection.AccessRuleId -> AccessRule is ON DELETE NO ACTION, so
        -- the referencing rows must be detached before the rule can be removed. A cleared collection is simply
        -- ungoverned.
        UPDATE [dbo].[Collection]
        SET [AccessRuleId] = NULL,
            [RevisionDate] = SYSUTCDATETIME()
        OUTPUT INSERTED.[Id] INTO @ClearedCollectionIds
        WHERE [AccessRuleId] = @Id

        -- Detach the requests that pinned this rule, for the same reason: FK_AccessRequest_AccessRule is ON DELETE
        -- NO ACTION, so any request that recorded this rule as its governing rule would block the delete outright.
        -- RuleId is provenance rather than authority -- the request's own window and decision log are what was
        -- actually granted, and the column is already nullable for requests that were never gated through a stored
        -- rule.
        UPDATE [dbo].[AccessRequest]
        SET [RuleId] = NULL
        WHERE [RuleId] = @Id

        DELETE FROM [dbo].[AccessRule]
        WHERE [Id] = @Id

        COMMIT TRANSACTION
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
        BEGIN
            ROLLBACK TRANSACTION
        END;

        THROW
    END CATCH

    -- Only an enabled rule gated its collections, so only then do their members see a change.
    IF @Enabled = 1 AND EXISTS (SELECT 1 FROM @ClearedCollectionIds)
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDateByCollectionIds] @ClearedCollectionIds, @OrganizationId
    END
END
