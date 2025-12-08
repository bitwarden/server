CREATE PROCEDURE [dbo].[User_UpdateAccountCryptographicState]
    @Id UNIQUEIDENTIFIER,
    @PublicKey NVARCHAR(MAX),
    @PrivateKey NVARCHAR(MAX),
    @SignedPublicKey NVARCHAR(MAX) = NULL,
    @SecurityState NVARCHAR(MAX) = NULL,
    @SecurityVersion INT = NULL,
    @SignatureAlgorithm TINYINT = NULL,
    @SigningKey VARCHAR(MAX) = NULL,
    @VerifyingKey VARCHAR(MAX) = NULL,
    @RevisionDate DATETIME2(7),
    @AccountRevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON
    
    BEGIN TRANSACTION

    BEGIN TRY
        UPDATE
            [dbo].[User]
        SET
            [PublicKey] = @PublicKey,
            [PrivateKey] = @PrivateKey,
            [SignedPublicKey] = @SignedPublicKey,
            [SecurityState] = @SecurityState,
            [SecurityVersion] = @SecurityVersion,
            [RevisionDate] = @RevisionDate,
            [AccountRevisionDate] = @AccountRevisionDate
        WHERE
            [Id] = @Id

        -- Update or insert signature key pair if provided
        IF @SignatureAlgorithm IS NOT NULL AND @SigningKey IS NOT NULL AND @VerifyingKey IS NOT NULL
        BEGIN
            IF EXISTS (SELECT 1 FROM [dbo].[UserSignatureKeyPair] WHERE [UserId] = @Id)
            BEGIN
                UPDATE [dbo].[UserSignatureKeyPair]
                SET
                    [SignatureAlgorithm] = @SignatureAlgorithm,
                    [SigningKey] = @SigningKey,
                    [VerifyingKey] = @VerifyingKey,
                    [RevisionDate] = @RevisionDate
                WHERE
                    [UserId] = @Id
            END
            ELSE
            BEGIN
                INSERT INTO [dbo].[UserSignatureKeyPair]
                (
                    [Id],
                    [UserId],
                    [SignatureAlgorithm],
                    [SigningKey],
                    [VerifyingKey],
                    [CreationDate],
                    [RevisionDate]
                )
                VALUES
                (
                    NEWID(),
                    @Id,
                    @SignatureAlgorithm,
                    @SigningKey,
                    @VerifyingKey,
                    @RevisionDate,
                    @RevisionDate
                )
            END
        END
        
        COMMIT TRANSACTION
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION
        THROW
    END CATCH
END
