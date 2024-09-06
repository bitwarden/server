CREATE PROCEDURE [dbo].[Notification_ReadByUserId]
    @UserId UNIQUEIDENTIFIER,
    @ClientType TINYINT
AS
BEGIN
    SET NOCOUNT ON

    SELECT [Notification].*
    FROM [dbo].[Notification]
             LEFT JOIN
         [dbo].[OrganizationUser] ON [Notification].[OrganizationId] = [OrganizationUser].[OrganizationId]
             AND [OrganizationUser].[UserId] = @UserId
    WHERE [ClientType] IN (0, CASE WHEN @ClientType != 0 THEN @ClientType END)
      AND ([Global] = 1
        OR ([Notification].[UserId] = @UserId
            AND ([Notification].[OrganizationId] IS NULL
                OR [OrganizationUser].[OrganizationId] IS NOT NULL))
        OR ([Notification].[UserId] IS NULL
            AND [OrganizationUser].[OrganizationId] IS NOT NULL))
    ORDER BY [Priority] DESC, [Notification].[CreationDate] DESC
END
