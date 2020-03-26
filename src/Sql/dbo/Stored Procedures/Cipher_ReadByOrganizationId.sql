CREATE PROCEDURE [dbo].[Cipher_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER,
    @Deleted BIT = 0
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[CipherView]
    WHERE
        [UserId] IS NULL
        AND [OrganizationId] = @OrganizationId
        AND
        (
            (@Deleted = 1 AND [DeletedDate] IS NOT NULL)
            OR (@Deleted = 0 AND [DeletedDate] IS NULL)
        )
END