CREATE PROCEDURE [dbo].[Notification_ReadByUserIdAndOrganizationsAndStatus]
    @UserId UNIQUEIDENTIFIER,
    @ClientType TINYINT,
    @OrganizationIds AS [dbo].[GuidIdArray] READONLY,
    @Read BIT,
    @Deleted BIT
AS
BEGIN
    SET NOCOUNT ON

    SELECT [Notification].*
    FROM [dbo].[Notification]
    LEFT JOIN [NotificationStatus] ON [Notification].[Id] = [NotificationStatus].[NotificationId]
    WHERE (
            (
                [ClientType] = @ClientType
                AND [Notification].UserId = @UserId
                )
            OR [Global] = 1
            OR (
                [OrganizationId] IS NOT NULL
                AND [Notification].[UserId] IS NULL
                AND [OrganizationId] IN (
                    SELECT [Id]
                    FROM @OrganizationIds
                    )
                )
            )
        AND (
            [NotificationStatus].UserId IS NULL
            OR [NotificationStatus].UserId = @UserId
            )
        AND (
            (
                @Read = 0
                AND [NotificationStatus].[ReadDate] IS NULL
                )
            OR (
                @Read = 1
                AND [NotificationStatus].[ReadDate] IS NOT NULL
                )
            )
        AND (
            (
                @Deleted = 0
                AND [NotificationStatus].[DeletedDate] IS NULL
                )
            OR (
                @Deleted = 1
                AND [NotificationStatus].[DeletedDate] IS NOT NULL
                )
            )
    ORDER BY [CreationDate] DESC
END
