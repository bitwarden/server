CREATE PROCEDURE [dbo].[SubvaultUser_ReadPermissionsBySubvaultUserId]
    @UserId UNIQUEIDENTIFIER,
    @SubvaultIds AS [dbo].[GuidIdArray] READONLY,
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        SU.[SubvaultId],
        CASE WHEN OU.[Type] = 2 THEN SU.[Admin] ELSE 1 END AS [Admin], -- 2 = Regular User
        CASE WHEN OU.[Type] = 2 THEN SU.[ReadOnly] ELSE 0 END AS [ReadOnly] -- 2 = Regular User
    FROM
        [dbo].[SubvaultUser] SU
    INNER JOIN
        [dbo].[OrganizationUser] OU ON OU.Id = SU.OrganizationUserId
    WHERE
        OU.[UserId] = @UserId
        AND OU.[OrganizationId] = @OrganizationId
        AND OU.[Status] = 2 -- 2 = Confirmed
        AND SU.[SubvaultId] IN (SELECT [Id] FROM @SubvaultIds)
END