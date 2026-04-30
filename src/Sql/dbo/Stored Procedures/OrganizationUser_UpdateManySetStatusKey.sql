CREATE PROCEDURE [dbo].[OrganizationUser_UpdateManySetStatusKey]
    @UsersJson             NVARCHAR(MAX),
    @NewStatus             TINYINT,
    @RequiredCurrentStatus TINYINT,
    @RevisionDate          DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @UsersToUpdate AS TABLE (
        [Id]     UNIQUEIDENTIFIER NOT NULL,
        [UserId] UNIQUEIDENTIFIER NOT NULL,
        [Key]    NVARCHAR(MAX)    NULL
    )

    INSERT INTO @UsersToUpdate
    SELECT
        [Id],
        [UserId],
        [Key]
    FROM OPENJSON(@UsersJson)
    WITH (
        [Id]     UNIQUEIDENTIFIER '$.Id',
        [UserId] UNIQUEIDENTIFIER '$.UserId',
        [Key]    NVARCHAR(MAX)    '$.Key'
    )

    DECLARE @UpdatedIds [dbo].[GuidIdArray]

    UPDATE OU
    SET
        OU.[Status]       = @NewStatus,
        OU.[Key]          = U.[Key],
        OU.[RevisionDate] = @RevisionDate
    OUTPUT
        INSERTED.[Id] INTO @UpdatedIds
    FROM
        [dbo].[OrganizationUser] OU
    INNER JOIN
        @UsersToUpdate U ON U.[Id] = OU.[Id]
    WHERE
        OU.[Status] = @RequiredCurrentStatus

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationUserIds] @UpdatedIds

    -- Return the IDs that were actually updated so the caller can track idempotency
    SELECT [Id] FROM @UpdatedIds
END
