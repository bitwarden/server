CREATE PROCEDURE [dbo].[EmergencyAccess_ReadCountByGrantorIdEmail]
    @GrantorId UNIQUEIDENTIFIER,
    @Email NVARCHAR(256),
    @OnlyUsers BIT
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        COUNT(1)
    FROM
        [dbo].[EmergencyAccess] EA
    LEFT JOIN
        [dbo].[User] U ON EA.[GranteeId] = U.[Id]
    WHERE
        EA.[GrantorId] = @GrantorId
        AND (
            (@OnlyUsers = 0 AND (EA.[Email] = @Email OR U.[Email] = @Email))
            OR (@OnlyUsers = 1 AND U.[Email] = @Email)
        )
END