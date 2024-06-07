CREATE TYPE [dbo].[WebauthnCredentialRotationDataType] AS TABLE(
    [Id] UNIQUEIDENTIFIER,
    [EncryptedPublicKey] NVARCHAR(MAX),
    [EncryptedUserKey] NVARCHAR(MAX)
)
