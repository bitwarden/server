CREATE PROCEDURE [dbo].[AuthRequest_DeleteIfExpired]
AS
BEGIN
    SET NOCOUNT OFF
    DELETE FROM [dbo].[AuthRequest] WHERE [CreationDate] < DATEADD(minute, -15, GETUTCDATE());
END
