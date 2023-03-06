CREATE PROCEDURE [dbo].[OrganizationUserOrganizationDetails_ReadByUserIdStatus]
    @UserId UNIQUEIDENTIFIER,
    @Status SMALLINT,
    @ProviderType TINYINT = 0
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationUserOrganizationDetailsView]
    WHERE
        [UserId] = @UserId
        AND (@Status IS NULL OR [Status] = @Status)
        AND (@ProviderType IS NULL OR [ProviderType] = @ProviderType)
END