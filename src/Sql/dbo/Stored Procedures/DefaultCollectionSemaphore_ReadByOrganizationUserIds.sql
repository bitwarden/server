CREATE PROCEDURE [dbo].[DefaultCollectionSemaphore_ReadByOrganizationUserIds]
    @OrganizationUserIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [OrganizationUserId]
    FROM
        [dbo].[DefaultCollectionSemaphore] DCS
    INNER JOIN
        @OrganizationUserIds OU ON [OU].[Id] = [DCS].[OrganizationUserId]
END
