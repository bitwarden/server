CREATE OR ALTER PROCEDURE [dbo].[Cipher_UpdateData]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Data NVARCHAR(MAX),
    @RevisionDate DATETIME2(7),
    @LastKnownRevisionDate DATETIME2(7),
    @Outcome INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON

    -- Opens no transaction, so it joins the caller's; a rollback here would undo the caller's work too.
    -- The revision check sits in the UPDATE, so the row lock covers check and write. A separate read would take no
    -- lock under RCSI, letting a user's save land in between and be overwritten. The 1-second tolerance matches
    -- CipherService.
    UPDATE
        [dbo].[Cipher]
    SET
        [Data] = @Data,
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
GO
