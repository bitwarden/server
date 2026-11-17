CREATE PROCEDURE [dbo].[Collection_SetAccessRuleAssociations]
    @AccessRuleId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @ToAssign AS [dbo].[GuidIdArray] READONLY,
    @ToClear AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON
    -- The clear and assign passes must apply together. Without this a run-time error in the second
    -- statement aborts only that statement, leaving the transaction open for the COMMIT below to
    -- commit the clear on its own -- detaching collections that were meant to be reassigned.
    SET XACT_ABORT ON

    DECLARE @RevisionDate DATETIME2(7) = SYSUTCDATETIME()

    DECLARE @Changed TABLE (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [PreviousAccessRuleId] UNIQUEIDENTIFIER NULL,
        [AccessRuleId] UNIQUEIDENTIFIER NULL
    )

    DECLARE @FlippedCollectionIds AS [dbo].[GuidIdArray]

    BEGIN TRY
        BEGIN TRANSACTION

        UPDATE
            C
        SET
            C.[AccessRuleId] = NULL,
            C.[RevisionDate] = @RevisionDate
        OUTPUT
            INSERTED.[Id],
            DELETED.[AccessRuleId],
            INSERTED.[AccessRuleId]
        INTO @Changed
        FROM
            [dbo].[Collection] C
        INNER JOIN
            @ToClear T ON T.[Id] = C.[Id]
        WHERE
            C.[OrganizationId] = @OrganizationId
            AND C.[AccessRuleId] = @AccessRuleId

        UPDATE
            C
        SET
            C.[AccessRuleId] = @AccessRuleId,
            C.[RevisionDate] = @RevisionDate
        OUTPUT
            INSERTED.[Id],
            DELETED.[AccessRuleId],
            INSERTED.[AccessRuleId]
        INTO @Changed
        FROM
            [dbo].[Collection] C
        INNER JOIN
            @ToAssign T ON T.[Id] = C.[Id]
        WHERE
            C.[OrganizationId] = @OrganizationId
            AND (C.[AccessRuleId] IS NULL OR C.[AccessRuleId] <> @AccessRuleId)
            -- The foreign key only proves the rule exists, not that it belongs to this organization. Without
            -- this the caller could govern its own collections with another organization's rule, handing that
            -- organization control of the conditions gating access to data it cannot see.
            AND EXISTS (
                SELECT
                    1
                FROM
                    [dbo].[AccessRule] AR
                WHERE
                    AR.[Id] = @AccessRuleId
                    AND AR.[OrganizationId] = @OrganizationId
            )

        -- Members sync a collection's "governed by an enabled rule" state, so only collections where that state
        -- flipped need their members to resync. REPEATABLEREAD holds the rule rows until COMMIT, so a concurrent
        -- AccessRule_Update toggle waits and then reads the new associations.
        INSERT INTO @FlippedCollectionIds
        SELECT DISTINCT
            CH.[Id]
        FROM
            @Changed CH
        LEFT JOIN
            [dbo].[AccessRule] PAR WITH (REPEATABLEREAD) ON PAR.[Id] = CH.[PreviousAccessRuleId]
        LEFT JOIN
            [dbo].[AccessRule] AR WITH (REPEATABLEREAD) ON AR.[Id] = CH.[AccessRuleId]
        WHERE
            ISNULL(PAR.[Enabled], 0) <> ISNULL(AR.[Enabled], 0)

        COMMIT TRANSACTION
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
        BEGIN
            ROLLBACK TRANSACTION
        END;

        THROW
    END CATCH

    IF EXISTS (SELECT 1 FROM @FlippedCollectionIds)
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDateByCollectionIds] @FlippedCollectionIds, @OrganizationId
    END
END
