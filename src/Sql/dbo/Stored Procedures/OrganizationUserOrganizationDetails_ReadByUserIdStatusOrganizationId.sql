CREATE PROCEDURE [dbo].[OrganizationUserOrganizationDetails_ReadByUserIdStatusOrganizationId]
    @UserId UNIQUEIDENTIFIER,
    @Status SMALLINT,
    @OrganizationId UNIQUEIDENTIFIER,
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
        AND [OrganizationId] = @OrganizationId
        AND (@Status IS NULL OR [Status] = @Status)
        AND (@ProviderType IS NULL OR [ProviderType] = @ProviderType)
END