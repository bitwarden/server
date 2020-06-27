CREATE PROCEDURE [dbo].[CipherDetails_ReadWithoutOrganizationsByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *,
        1 [Edit],
        1 [ViewPassword],
        0 [OrganizationUseTotp]
    FROM
        [dbo].[CipherDetails](@UserId)
    WHERE
        [UserId] = @UserId
END