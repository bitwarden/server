CREATE PROCEDURE [dbo].[Notification_ReadByUserIdAndOrganizations]
    @UserId UNIQUEIDENTIFIER,
    @ClientType TINYINT,
    @OrganizationIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    SELECT *
    FROM [dbo].[Notification]
    WHERE (
            [ClientType] = @ClientType
            AND [UserId] = @UserId
            )
        OR [Global] = 1
        OR (
            [OrganizationId] IS NOT NULL
            AND [UserId] IS NULL
            AND [OrganizationId] IN (
                SELECT [Id]
                FROM @OrganizationIds
                )
            )
    ORDER BY [CreationDate] DESC
END
