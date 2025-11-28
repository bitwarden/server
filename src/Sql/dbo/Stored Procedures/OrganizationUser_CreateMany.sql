CREATE PROCEDURE [dbo].[OrganizationUser_CreateMany]
    @jsonData NVARCHAR(MAX)
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
        [ExternalId],
        [CreationDate],
        [RevisionDate],
        [Permissions],
        [ResetPasswordKey],
        [AccessSecretsManager]
        )
    SELECT
        OUI.[Id],
        OUI.[OrganizationId],
        OUI.[UserId],
        OUI.[Email],
        OUI.[Key],
        OUI.[Status],
        OUI.[Type],
        OUI.[ExternalId],
        OUI.[CreationDate],
        OUI.[RevisionDate],
        OUI.[Permissions],
        OUI.[ResetPasswordKey],
        OUI.[AccessSecretsManager]
    FROM
        OPENJSON(@jsonData)
        WITH (
            [Id] UNIQUEIDENTIFIER '$.Id',
            [OrganizationId] UNIQUEIDENTIFIER '$.OrganizationId',
            [UserId] UNIQUEIDENTIFIER '$.UserId',
            [Email] NVARCHAR(256) '$.Email',
            [Key] VARCHAR(MAX) '$.Key',
            [Status] SMALLINT '$.Status',
            [Type] TINYINT '$.Type',
            [ExternalId] NVARCHAR(300) '$.ExternalId',
            [CreationDate] DATETIME2(7) '$.CreationDate',
            [RevisionDate] DATETIME2(7) '$.RevisionDate',
            [Permissions] NVARCHAR (MAX) '$.Permissions',
            [ResetPasswordKey] VARCHAR (MAX) '$.ResetPasswordKey',
            [AccessSecretsManager] BIT '$.AccessSecretsManager'
        ) OUI
END
