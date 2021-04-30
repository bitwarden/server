CREATE PROCEDURE [dbo].[OrganizationUser_CreateMany]
    @OrganizationUsersInput [dbo].[OrganizationUserType] READONLY
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationUser]
        (
        [Id],
        [OrganizationId],
        [UserId],
        [Email],
        [Key],
        [Status],
        [Type],
        [AccessAll],
        [ExternalId],
        [CreationDate],
        [RevisionDate],
        [Permissions],
        [ResetPasswordKey]
        )
    SELECT
        OU.[Id],
        OU.[OrganizationId],
        OU.[UserId],
        OU.[Email],
        OU.[Key],
        OU.[Status],
        OU.[Type],
        OU.[AccessAll],
        OU.[ExternalId],
        OU.[CreationDate],
        OU.[RevisionDate],
        OU.[Permissions],
        OU.[ResetPasswordKey]
    FROM
        @OrganizationUsersInput OU
END
GO
