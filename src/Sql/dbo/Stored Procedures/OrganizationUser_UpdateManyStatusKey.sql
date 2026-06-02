CREATE PROCEDURE [dbo].[OrganizationUser_UpdateManyStatusKey]
    @UsersJson    NVARCHAR(MAX),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @RowCount INT

    DECLARE @UsersToUpdate AS TABLE (
        [Id]  UNIQUEIDENTIFIER NOT NULL,
        [Key] VARCHAR(MAX)     NULL
    )

    INSERT INTO @UsersToUpdate
    SELECT
        [Id],
        [Key]
    FROM OPENJSON(@UsersJson)
    WITH (
        [Id]  UNIQUEIDENTIFIER '$.Id',
        [Key] VARCHAR(MAX)     '$.Key'
    )

    DECLARE @UpdatedIds [dbo].[GuidIdArray]

    UPDATE OU
    SET
        OU.[Status]       = 2, -- Confirmed
        OU.[Key]          = U.[Key],
        OU.[RevisionDate] = @RevisionDate
    OUTPUT
        INSERTED.[Id] INTO @UpdatedIds
    FROM
        [dbo].[OrganizationUser] OU
    INNER JOIN
        @UsersToUpdate U ON U.[Id] = OU.[Id]
    WHERE
        OU.[Status] = 1 -- Accepted

    SET @RowCount = @@ROWCOUNT;
    IF @RowCount > 0
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationUserIds] @UpdatedIds
    END

    -- Return the IDs that were actually updated so the caller can track idempotency
    SELECT [Id] FROM @UpdatedIds
END
