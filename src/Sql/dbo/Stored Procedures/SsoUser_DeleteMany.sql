CREATE PROCEDURE [dbo].[SsoUser_DeleteMany]
    @UserAndOrganizationIds [dbo].[TwoGuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        Id
    INTO
        #SSOIds
    FROM
        [dbo].[SsoUser] SU
    INNER JOIN
        @UserAndOrganizationIds UOI ON UOI.Id1 = SU.UserId AND UOI.Id2 = SU.OrganizationId

    DECLARE @BatchSize INT = 100

    -- Delete SSO Users
    WHILE @BatchSize > 0
    BEGIN
        BEGIN TRANSACTION SsoUser_DeleteMany_SsoUsers

        DELETE TOP(@BatchSize) SU
        FROM
            [dbo].[SsoUser] SU
        INNER JOIN
            #SSOIds ON #SSOIds.Id = SU.Id

        SET @BatchSize = @@ROWCOUNT

        COMMIT TRANSACTION SsoUser_DeleteMany_SsoUsers
    END
END
GO
