CREATE PROCEDURE [dbo].[AgentFillApprovalRequest_Answer]
    @Id               UNIQUEIDENTIFIER,
    @UserId           UNIQUEIDENTIFIER,
    @SealedResponse   VARCHAR(MAX),
    @ResponseDeviceId UNIQUEIDENTIFIER,
    @ResponseDate     DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- First answer wins: the conditional update is atomic, so concurrent answers store exactly one response.
    UPDATE
        [dbo].[AgentFillApprovalRequest]
    SET
        [SealedResponse] = @SealedResponse,
        [ResponseDeviceId] = @ResponseDeviceId,
        [ResponseDate] = @ResponseDate
    WHERE
        [Id] = @Id
        AND [UserId] = @UserId
        AND [SealedResponse] IS NULL
        AND [ExpirationDate] > @ResponseDate

    SELECT @@ROWCOUNT
END
