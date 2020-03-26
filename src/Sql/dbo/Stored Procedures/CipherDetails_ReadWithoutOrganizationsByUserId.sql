CREATE PROCEDURE [dbo].[CipherDetails_ReadWithoutOrganizationsByUserId]
    @UserId UNIQUEIDENTIFIER,
    @Deleted BIT = 0
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *,
        1 [Edit],
        0 [OrganizationUseTotp]
    FROM
        [dbo].[CipherDetails](@UserId)
    WHERE
        [UserId] = @UserId
        AND
        (
            (@Deleted = 1 AND [DeletedDate] IS NOT NULL)
            OR (@Deleted = 0 AND [DeletedDate] IS NULL)
        )
END