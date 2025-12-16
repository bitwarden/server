CREATE PROCEDURE [dbo].[User_ReadPremiumAccessByIds]
    @Ids [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        UPA.[Id],
        UPA.[PersonalPremium],
        UPA.[OrganizationPremium]
    FROM
        [dbo].[UserPremiumAccessView] UPA
    WHERE
        UPA.[Id] IN (SELECT [Id] FROM @Ids)
END
