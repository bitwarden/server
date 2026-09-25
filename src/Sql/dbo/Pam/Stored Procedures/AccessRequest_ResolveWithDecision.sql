CREATE PROCEDURE [dbo].[AccessRequest_ResolveWithDecision]
    @AccessRequestId UNIQUEIDENTIFIER,
    @Action TINYINT,
    @AccessDecisionId UNIQUEIDENTIFIER,
    @ApproverId UNIQUEIDENTIFIER,
    @Verdict TINYINT,
    @Comment NVARCHAR(MAX) = NULL,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON
    -- XACT_ABORT rolls back both writes together on any failure.
    SET XACT_ABORT ON

    -- Records an approver's decision; WHERE guard makes it idempotent (first CAS wins).
    BEGIN TRANSACTION AccessRequest_Resolve

    UPDATE [dbo].[AccessRequest]
    SET [Action] = @Action,
        [ActionDate] = @Now
    WHERE [Id] = @AccessRequestId
        AND [Action] = 0 -- None (open)
        AND [NotAfter] > @Now

    DECLARE @Rows INT = @@ROWCOUNT

    IF @Rows > 0
    BEGIN
        INSERT INTO [dbo].[AccessDecision]
        (
            [Id], [AccessRequestId], [DeciderKind], [ApproverId], [ConditionKind],
            [Verdict], [Comment], [EvaluationContext], [CreationDate]
        )
        VALUES
        (
            @AccessDecisionId, @AccessRequestId, 1 /* Human */, @ApproverId, NULL,
            @Verdict, @Comment, NULL, @Now
        )
    END

    COMMIT TRANSACTION AccessRequest_Resolve

    -- 1 when this call resolved the request, 0 when it was no longer open.
    SELECT CAST(CASE WHEN @Rows > 0 THEN 1 ELSE 0 END AS BIT)
END
