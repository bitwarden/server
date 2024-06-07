SET ANSI_NULLS, ANSI_PADDING, ANSI_WARNINGS, ARITHABORT, CONCAT_NULL_YIELDS_NULL, QUOTED_IDENTIFIER ON;

SET NUMERIC_ROUNDABORT OFF;

GO
PRINT N'Creating [dbo].[WebauthnCredentialRotationDataType]...';
IF TYPE_ID(N'[dbo].[WebauthnCredentialRotationDataType]') IS NULL
BEGIN
    CREATE TYPE [dbo].[WebauthnCredentialRotationDataType] AS TABLE(
        [Id] UNIQUEIDENTIFIER,
        [EncryptedPublicKey] NVARCHAR(MAX),
        [EncryptedUserKey] NVARCHAR(MAX)
    )
END
GO
