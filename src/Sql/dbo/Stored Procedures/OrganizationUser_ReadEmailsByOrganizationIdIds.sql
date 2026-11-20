CREATE PROCEDURE [dbo].[OrganizationUser_ReadEmailsByOrganizationIdIds]
    @OrganizationId UNIQUEIDENTIFIER,
    @Ids [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        OU.[Id],
        COALESCE(OU.[Email], U.[Email]) AS [Email]
    FROM
        [dbo].[OrganizationUser] OU
        INNER JOIN @Ids I ON I.[Id] = OU.[Id]
        LEFT JOIN [dbo].[User] U ON U.[Id] = OU.[UserId]
    WHERE
        OU.[OrganizationId] = @OrganizationId
END
