CREATE PROCEDURE [dbo].[EmergencyAccess_SetStatusToAccepted]
    @Ids [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @UtcNow DATETIME2(7) = GETUTCDATE()

    UPDATE EA
    SET EA.[Status] = 1, -- Accepted
        EA.[KeyEncrypted] = NULL,
        EA.[RevisionDate] = @UtcNow
    FROM [dbo].[EmergencyAccess] EA
    INNER JOIN @Ids I ON I.[Id] = EA.[Id]
END
GO
