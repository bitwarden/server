CREATE PROCEDURE [dbo].[User_BumpAccountRevisionDateByProviderOrganizationId]
    @ProviderOrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        U
    SET
        U.[AccountRevisionDate] = GETUTCDATE()
    FROM
        [dbo].[User] U
    INNER JOIN
        [dbo].[OrganizationUser] OU ON OU.[UserId] = U.[Id]
    INNER JOIN
        [dbo].[ProviderOrganization] UO ON UO.[ProviderId] = OU.[ProviderId] AND UO.[OrganizationId] = OU.[OrganizationId]
    WHERE
        UO.[Id] = @ProviderOrganizationId
        AND OU.[Status] = 2 -- Confirmed
END
