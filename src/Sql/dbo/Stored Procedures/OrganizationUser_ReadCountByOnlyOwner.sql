CREATE PROCEDURE [dbo].[OrganizationUser_ReadCountByOnlyOwner]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    ;WITH [OwnerCountCTE] AS
    (
        SELECT
            OU.[UserId],
            COUNT(1) OVER (PARTITION BY OU.[OrganizationId]) [ConfirmedOwnerCount]
        FROM
            [dbo].[OrganizationUser] OU
        WHERE
            OU.[Type] = 0 -- 0 = Owner
            AND OU.[Status] = 2 -- 2 = Confirmed
    )
    SELECT
        COUNT(1)
    FROM
        [OwnerCountCTE] OC
    WHERE
        OC.[UserId] = @UserId
        AND OC.[ConfirmedOwnerCount] = 1
END
