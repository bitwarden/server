CREATE PROCEDURE [dbo].[ProviderUserProviderOrganizationDetails_ReadByUserIdStatus]
    @UserId UNIQUEIDENTIFIER,
    @Status TINYINT,
    @ProviderType TINYINT = 0
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ProviderUserProviderOrganizationDetailsView]
    WHERE
        [UserId] = @UserId
        AND (@Status IS NULL OR [Status] = @Status)
        AND (@ProviderType IS NULL OR [ProviderType] = @ProviderType)
END
