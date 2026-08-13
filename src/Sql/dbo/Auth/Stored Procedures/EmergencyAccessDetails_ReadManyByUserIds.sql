CREATE PROCEDURE [dbo].[EmergencyAccessDetails_ReadManyByUserIds]
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
