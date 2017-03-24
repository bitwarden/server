CREATE PROCEDURE [dbo].[CipherFullDetails_ReadByIdUserId]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT DISTINCT
        C.*,
        CASE 
            WHEN C.[OrganizationId] IS NULL THEN 1 
            ELSE [dbo].[UserCanEditCipher](@UserId, @Id)
        END [Edit]
    FROM
        [dbo].[CipherDetails](@UserId) C
    LEFT JOIN
        [dbo].[SubvaultCipher] SC ON SC.[CipherId] = C.[Id]
    LEFT JOIN
        [dbo].[SubvaultUser] SU ON SU.[SubvaultId] = SC.[SubvaultId]
    LEFT JOIN
        [dbo].[OrganizationUser] OU ON OU.[Id] = SU.[OrganizationUserId]
    WHERE
        C.Id = @Id
        AND (
            (C.[UserId] IS NOT NULL AND C.[UserId] = @UserId)
            OR (OU.[UserId] = @UserId AND OU.[Status] = 2) -- 2 = Confirmed
        )
END