CREATE PROCEDURE [dbo].[OrganizationUserOrganizationDetails_ReadAcceptedConfirmedByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationUserOrganizationDetailsView]
    WHERE
        [UserId] = @UserId
        AND [Status] IN (1, 2) -- Accepted = 1, Confirmed = 2
END