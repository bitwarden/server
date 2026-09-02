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

    BEGIN TRANSACTION

    UPDATE
        C
    SET
        C.[AccessRuleId] = NULL,
        C.[RevisionDate] = @RevisionDate
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
    FROM
        [dbo].[Collection] C
    INNER JOIN
        @ToAssign T ON T.[Id] = C.[Id]
    WHERE
        C.[OrganizationId] = @OrganizationId
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

    COMMIT TRANSACTION

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
END
