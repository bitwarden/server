CREATE PROCEDURE [dbo].[CipherDetails_ReadByIdUserId]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT DISTINCT
        C.*
    FROM
        [dbo].[CipherDetailsView] C
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