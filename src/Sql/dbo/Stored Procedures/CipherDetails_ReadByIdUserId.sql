CREATE PROCEDURE [dbo].[CipherDetails_ReadByIdUserId]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT DISTINCT
        C.*
    FROM
        [dbo].[CipherDetails](@UserId) C
    LEFT JOIN
        [dbo].[SubvaultCipher] SC ON SC.[CipherId] = C.[Id]
    LEFT JOIN
        [dbo].[SubvaultUser] SU ON SU.[SubvaultId] = SC.[SubvaultId]
    LEFT JOIN
        [dbo].[OrganizationUser] OU ON OU.[Id] = SU.[OrganizationUserId]
    LEFT JOIN
        [dbo].[Organization] O ON O.[Id] = C.[OrganizationId]
    WHERE
        C.Id = @Id
        AND (
            C.[UserId] = @UserId
            OR (
                C.[UserId] IS NULL
                AND OU.[UserId] = @UserId
                AND OU.[Status] = 2 -- 2 = Confirmed
                AND O.[Enabled] = 1
            )
        )
END