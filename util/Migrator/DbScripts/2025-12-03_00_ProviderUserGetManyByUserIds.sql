CREATE OR ALTER PROCEDURE [dbo].[ProviderUser_ReadyManyByManyUserIds]
    @UserIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [pu].*
    FROM
        [dbo].[ProviderUserView] AS [pu]
            JOIN
        @UserIds [u] ON [u].[Id] = [pu].[UserId]
END
