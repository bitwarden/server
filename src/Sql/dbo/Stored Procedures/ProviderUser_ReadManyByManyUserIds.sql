CREATE PROCEDURE [dbo].[ProviderUser_ReadManyByManyUserIds]
    @UserIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [pu].*
    FROM
        [dbo].[ProviderUserView] AS [pu]
    INNER JOIN
        @UserIds [u] ON [u].[Id] = [pu].[UserId]
END
