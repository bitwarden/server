CREATE PROCEDURE [dbo].[AgentFillApprovalRequest_Create]
    @Id               UNIQUEIDENTIFIER,
    @UserId           UNIQUEIDENTIFIER,
    @RequestDeviceId  UNIQUEIDENTIFIER,
    @SealedRequest    VARCHAR(MAX),
    @CreationDate     DATETIME2(7),
    @ExpirationDate   DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[AgentFillApprovalRequest]
    (
        [Id],
        [UserId],
        [RequestDeviceId],
        [SealedRequest],
        [SealedResponse],
        [ResponseDeviceId],
        [CreationDate],
        [ExpirationDate],
        [ResponseDate]
    )
    VALUES
    (
        @Id,
        @UserId,
        @RequestDeviceId,
        @SealedRequest,
        NULL,
        NULL,
        @CreationDate,
        @ExpirationDate,
        NULL
    )
END
