CREATE PROCEDURE [dbo].[User_BumpAccountRevisionDateByUnitPOrganizationId]
    @UnitPOrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        U
    SET
        U.[AccountRevisionDate] = GETUTCDATE()
    FROM
        [dbo].[User] U
    INNER JOIN
        [dbo].[OrganizationUser] OU ON OU.[UserId] = U.[Id]
    INNER JOIN
        [dbo].[UnitPOrganization] UO ON UO.[UnitPId] = OU.[UnitPId] AND UO.[OrganizationId] = OU.[OrganizationId]
    WHERE
        UO.[Id] = @UnitPOrganizationId
        AND OU.[Status] = 2 -- Confirmed
END
