CREATE PROCEDURE [dbo].[Notification_ReadByUserId]
    @UserId UNIQUEIDENTIFIER,
    @ClientType TINYINT
AS
BEGIN
    SET NOCOUNT ON

    SELECT n.*
    FROM [dbo].[NotificationView] n
             LEFT JOIN
         [dbo].[OrganizationUserView] ou ON n.[OrganizationId] = ou.[OrganizationId]
             AND ou.[UserId] = @UserId
    WHERE [ClientType] IN (0, CASE WHEN @ClientType != 0 THEN @ClientType END)
      AND ([Global] = 1
        OR (n.[UserId] = @UserId
            AND (n.[OrganizationId] IS NULL
                OR ou.[OrganizationId] IS NOT NULL))
        OR (n.[UserId] IS NULL
            AND ou.[OrganizationId] IS NOT NULL))
    ORDER BY [Priority] DESC, n.[CreationDate] DESC
END
