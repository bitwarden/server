CREATE VIEW [dbo].[NotificationStatusDetailsView]
AS
SELECT
    N.[Id],
    N.[Priority],
    N.[Global],
    N.[ClientType],
    N.[UserId],
    N.[OrganizationId],
    N.[Title],
    N.[Body],
    N.[CreationDate],
    N.[RevisionDate],
    N.[TaskId],
    NS.[UserId] AS [NotificationStatusUserId],
    NS.[ReadDate],
    NS.[DeletedDate]
FROM
    [dbo].[Notification] AS N
LEFT JOIN
    [dbo].[NotificationStatus] as NS
ON
    N.[Id] = NS.[NotificationId]
