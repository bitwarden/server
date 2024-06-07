IF TYPE_ID(N'[dbo].[WebauthnCredentialRotationDataType]') IS NULL
BEGIN
    CREATE TYPE [dbo].[WebauthnCredentialRotationDataType] AS TABLE(
        [Id] UNIQUEIDENTIFIER,
        [EncryptedPublicKey] NVARCHAR(MAX),
        [EncryptedUserKey] NVARCHAR(MAX)
    )
END