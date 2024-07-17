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
        0,  -- AccessAll will be removed shortly
        [ExternalId],
        [CreationDate],
        [RevisionDate],
        [Permissions],
        [ResetPasswordKey],
        [AccessSecretsManager]
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
        OU.[ResetPasswordKey],
        OU.[AccessSecretsManager]
    FROM
        @OrganizationUsersInput OU
END
