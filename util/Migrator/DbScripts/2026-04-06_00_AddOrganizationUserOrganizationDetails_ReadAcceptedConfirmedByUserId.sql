CREATE OR ALTER PROCEDURE [dbo].[OrganizationUserOrganizationDetails_ReadAcceptedConfirmedByUserId]
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
        AND [Status] IN (1,2) -- 1 = Accepted, 2 = Confirmed
END
GO
