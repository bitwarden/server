CREATE PROCEDURE [dbo].[AuthRequest_ReadPendingByUserId]
    @UserId UNIQUEIDENTIFIER,
    @ExpirationMinutes INT
AS
BEGIN
    SET NOCOUNT ON

    SELECT *
    FROM [dbo].[AuthRequestPendingDetailsView]
    WHERE [UserId] = @UserId
        AND [CreationDate] >= DATEADD(MINUTE, -@ExpirationMinutes, GETUTCDATE())
END
