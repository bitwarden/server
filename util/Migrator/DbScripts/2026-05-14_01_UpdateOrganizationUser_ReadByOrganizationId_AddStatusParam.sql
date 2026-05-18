CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_ReadByPendingAutoConfirm]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationUserView]
    WHERE
        [OrganizationId] = @OrganizationId
        AND [Status] = 1  -- Accepted
        AND [Type] = 0    -- User
        AND [UserId] IS NOT NULL
END
GO
