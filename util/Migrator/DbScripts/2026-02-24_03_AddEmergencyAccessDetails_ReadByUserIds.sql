CREATE OR ALTER PROCEDURE [dbo].[EmergencyAccessDetails_ReadByUserIds]
    @UserIds [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[EmergencyAccessDetailsView]
    WHERE
        [GrantorId] IN (SELECT [Id] FROM @UserIds)
        OR [GranteeId] IN (SELECT [Id] FROM @UserIds)
END
GO
