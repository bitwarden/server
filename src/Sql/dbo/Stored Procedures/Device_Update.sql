CREATE PROCEDURE [dbo].[Device_Update]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @Name NVARCHAR(50),
    @Type TINYINT,
    @Identifier NVARCHAR(50),
    @PushToken NVARCHAR(255),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @EncryptedUserKey VARCHAR(MAX) = NULL,
    @EncryptedPublicKey VARCHAR(MAX) = NULL,
    @EncryptedPrivateKey VARCHAR(MAX) = NULL,
    @Active BIT = 1,
    @LastActivityDate DATETIME2(7) = NULL,
    @ClientVersion NVARCHAR(43) = NULL
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[Device]
    SET
        [UserId] = @UserId,
        [Name] = @Name,
        [Type] = @Type,
        [Identifier] = @Identifier,
        [PushToken] = @PushToken,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate,
        [EncryptedUserKey] = @EncryptedUserKey,
        [EncryptedPublicKey] = @EncryptedPublicKey,
        [EncryptedPrivateKey] = @EncryptedPrivateKey,
        [Active] = @Active,
        -- LastActivityDate only moves forward. Two scenarios could silently clobber a valid bump:
        --   1. NULL passthrough: a general save that does not intend to touch LastActivityDate passes NULL
        --      (the default); we must not overwrite an existing value with NULL.
        --   2. Stale non-null overwrite: a thread that loaded the device before a concurrent bump fires
        --      may call SaveAsync with an older date; we must not clobber the fresher DB value.
        -- The CASE expression handles both: LastActivityDate is updated only when the incoming value is
        -- strictly greater than the current DB value (ISNULL baseline of '1900-01-01' handles NULL DB values).
        [LastActivityDate] = CASE
            WHEN @LastActivityDate > ISNULL([LastActivityDate], '1900-01-01') THEN @LastActivityDate
            ELSE [LastActivityDate]
        END,
        -- ClientVersion is value-equality based, not forward-only — downgrades are valid (e.g. a user
        -- reverts a desktop install). We only need NULL passthrough so unrelated SaveAsync calls (that
        -- don't intend to touch ClientVersion) don't clobber the stored value with NULL.
        [ClientVersion] = ISNULL(@ClientVersion, [ClientVersion])
    WHERE
        [Id] = @Id
END
