CREATE PROCEDURE [dbo].[Cipher_UpdateData]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Data NVARCHAR(MAX),
    @RevisionDate DATETIME2(7),
    @LastKnownRevisionDate DATETIME2(7),
    @Outcome INT OUTPUT,
    @UpdateKey BIT = 0,
    @Key VARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON

    -- Opens no transaction, so it joins the caller's; a rollback here would undo the caller's work too.
    -- The revision check sits in the UPDATE, so the row lock covers check and write. A separate read would take no
    -- lock under RCSI, letting a user's save land in between and be overwritten. The 1-second tolerance matches
    -- CipherService.
    -- The key is written with the data because the data is encrypted under it: a data-only write would leave a key
    -- that no longer matches. @UpdateKey defaults to 0 so a caller that predates it keeps the stored key, since a
    -- NULL @Key is also a valid new value (an item encrypted directly under the organization key).
    UPDATE
        [dbo].[Cipher]
    SET
        [Data] = @Data,
        [Key] = CASE WHEN @UpdateKey = 1 THEN @Key ELSE [Key] END,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
        AND ABS(DATEDIFF_BIG(MILLISECOND, [RevisionDate], @LastKnownRevisionDate)) <= 1000

    IF @@ROWCOUNT = 0
    BEGIN
        -- 0 = deleted, so retrying can't help; -1 = changed by someone else, so retry from a fresh revision date.
        SET @Outcome = CASE
            WHEN EXISTS (
                SELECT
                    1
                FROM
                    [dbo].[Cipher]
                WHERE
                    [Id] = @Id
            ) THEN -1
            ELSE 0
        END
        RETURN
    END

    EXEC [dbo].[User_BumpAccountRevisionDateByCipherId] @Id, @OrganizationId

    SET @Outcome = 1 -- Updated
END
