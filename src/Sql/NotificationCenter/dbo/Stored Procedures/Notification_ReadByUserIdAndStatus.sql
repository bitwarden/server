CREATE PROCEDURE [dbo].[Notification_ReadByUserIdAndStatus]
    @UserId UNIQUEIDENTIFIER,
    @ClientType TINYINT,
    @Read BIT,
    @Deleted BIT
AS
BEGIN
    SET NOCOUNT ON

    SELECT [Notification].*
    FROM [dbo].[Notification]
             LEFT JOIN [dbo].[OrganizationUser] ON [Notification].[OrganizationId] = [OrganizationUser].[OrganizationId]
        AND [OrganizationUser].[UserId] = @UserId
             JOIN [dbo].[NotificationStatus] ON [Notification].[Id] = [NotificationStatus].[NotificationId]
        AND [NotificationStatus].[UserId] = @UserId
    WHERE [ClientType] IN (0, CASE WHEN @ClientType != 0 THEN @ClientType END)
      AND ([Global] = 1
        OR ([Notification].[UserId] = @UserId
            AND ([Notification].[OrganizationId] IS NULL
                OR [OrganizationUser].[OrganizationId] IS NOT NULL))
        OR ([Notification].[UserId] IS NULL
            AND [OrganizationUser].[OrganizationId] IS NOT NULL))
      AND (@Read IS NULL
        OR IIF((@Read = 1 AND [NotificationStatus].[ReadDate] IS NOT NULL) OR
               (@Read = 0 AND [NotificationStatus].[ReadDate] IS NULL),
               1, 0) = 1
        OR @Deleted IS NULL
        OR IIF((@Deleted = 1 AND [NotificationStatus].[DeletedDate] IS NOT NULL) OR
               (@Deleted = 0 AND [NotificationStatus].[DeletedDate] IS NULL),
               1, 0) = 1)
    ORDER BY [Priority], [Notification].[CreationDate] DESC
END
