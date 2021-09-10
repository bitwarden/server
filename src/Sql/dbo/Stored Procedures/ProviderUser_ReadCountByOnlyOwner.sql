CREATE PROCEDURE [dbo].[ProviderUser_ReadCountByOnlyOwner]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    ;WITH [OwnerCountCTE] AS
    (
        SELECT
            PU.[UserId],
            COUNT(1) OVER (PARTITION BY PU.[ProviderId]) [ConfirmedOwnerCount]
        FROM
            [dbo].[ProviderUser] PU
        WHERE
            PU.[Type] = 0 -- 0 = ProviderAdmin
            AND PU.[Status] = 2 -- 2 = Confirmed
    )
    SELECT
        COUNT(1)
    FROM
        [OwnerCountCTE] OC
    WHERE
        OC.[UserId] = @UserId
        AND OC.[ConfirmedOwnerCount] = 1
END
