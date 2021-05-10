CREATE PROCEDURE [dbo].[ProviderUser_ReadByUnitId]
    @ProviderId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ProviderUserView]
    WHERE
        [ProviderId] = @ProviderId
END
