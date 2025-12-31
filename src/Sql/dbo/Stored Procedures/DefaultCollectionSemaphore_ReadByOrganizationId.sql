CREATE PROCEDURE [dbo].[DefaultCollectionSemaphore_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [OrganizationUserId]
    FROM
        [dbo].[DefaultCollectionSemaphore]
    WHERE
        [OrganizationId] = @OrganizationId
END
