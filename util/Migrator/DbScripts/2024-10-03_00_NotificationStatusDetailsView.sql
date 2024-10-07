-- View NotificationStatusDetailsView

IF EXISTS(SELECT *
          FROM sys.views
          WHERE [Name] = 'NotificationStatusDetailsView')
BEGIN
DROP VIEW [dbo].[NotificationStatusDetailsView]
END
GO

CREATE VIEW [dbo].[NotificationStatusDetailsView]
AS
SELECT
    N.*,
    NS.UserId AS NotificationStatusUserId,
    NS.ReadDate,
    NS.DeletedDate
FROM
    [dbo].[Notification] AS N
LEFT JOIN
    [dbo].[NotificationStatus] as NS
ON
    N.[Id] = NS.[NotificationId]
GO

-- Stored Procedure Notification_ReadByUserIdAndStatus

CREATE OR ALTER PROCEDURE [dbo].[Notification_ReadByUserIdAndStatus]
    @UserId UNIQUEIDENTIFIER,
    @ClientType TINYINT,
    @Read BIT,
    @Deleted BIT
AS
BEGIN
    SET NOCOUNT ON

    SELECT n.*
    FROM [dbo].[NotificationStatusDetailsView] n
             LEFT JOIN [dbo].[OrganizationUserView] ou ON n.[OrganizationId] = ou.[OrganizationId]
        AND ou.[UserId] = @UserId
    WHERE (n.[NotificationStatusUserId] IS NULL OR n.[NotificationStatusUserId] = @UserId)
      AND [ClientType] IN (0, CASE WHEN @ClientType != 0 THEN @ClientType END)
      AND ([Global] = 1
        OR (n.[UserId] = @UserId
            AND (n.[OrganizationId] IS NULL
                OR ou.[OrganizationId] IS NOT NULL))
        OR (n.[UserId] IS NULL
            AND ou.[OrganizationId] IS NOT NULL))
      AND ((@Read IS NULL AND @Deleted IS NULL)
        OR (n.[NotificationStatusUserId] IS NOT NULL
            AND ((@Read IS NULL
                OR IIF((@Read = 1 AND n.[ReadDate] IS NOT NULL) OR
                       (@Read = 0 AND n.[ReadDate] IS NULL),
                       1, 0) = 1)
                OR (@Deleted IS NULL
                    OR IIF((@Deleted = 1 AND n.[DeletedDate] IS NOT NULL) OR
                           (@Deleted = 0 AND n.[DeletedDate] IS NULL),
                           1, 0) = 1))))
    ORDER BY [Priority] DESC, n.[CreationDate] DESC
END
GO
