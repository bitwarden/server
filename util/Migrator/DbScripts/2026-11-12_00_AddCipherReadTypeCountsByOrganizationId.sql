CREATE OR ALTER PROCEDURE [dbo].[Cipher_ReadTypeCountsByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [Type],
        COUNT(1) AS [Count]
    FROM
        [dbo].[CipherView]
    WHERE
        [UserId] IS NULL
        AND [OrganizationId] = @OrganizationId
    GROUP BY
        [Type]
END
GO
