-- Recreate the NotificationStatusView to include the Notification.TaskId column
CREATE OR ALTER VIEW [dbo].[NotificationStatusDetailsView]
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
END
GO
