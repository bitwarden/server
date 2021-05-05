CREATE PROCEDURE [dbo].[OrganizationUser_UpdateMany]
    @OrganizationUsersInput [dbo].[OrganizationUserType] READONLY
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        OU
    SET
        [OrganizationId] = OUI.[OrganizationId],
        [UserId] = OUI.[UserId],
        [Email] = OUI.[Email],
        [Key] = OUI.[Key],
        [Status] = OUI.[Status],
        [Type] = OUI.[Type],
        [AccessAll] = OUI.[AccessAll],
        [ExternalId] = OUI.[ExternalId],
        [CreationDate] = OUI.[CreationDate],
        [RevisionDate] = OUI.[RevisionDate],
        [Permissions] = OUI.[Permissions],
        [ResetPasswordKey] = OUI.[ResetPasswordKey]
    FROM
        [dbo].[OrganizationUser] OU
    INNER JOIN
        @OrganizationUsersInput OUI ON OU.Id = OUI.Id

    EXEC [dbo].[User_BumpManyAccountRevisionDates]
    (
        SELECT UserId
        FROM @OrganizationUsersInput
    )
END
GO
