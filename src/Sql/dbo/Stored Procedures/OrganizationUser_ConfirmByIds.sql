CREATE PROCEDURE [dbo].[OrganizationUser_ConfirmByIds]
    @UsersToConfirm [dbo].[OrganizationUserToConfirmArray] READONLY,
    @RevisionDate   DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @ConfirmedIds [dbo].[GuidIdArray]

    UPDATE OU
    SET
        OU.[Status]       = 2, -- Confirmed
        OU.[Key]          = UTC.[Key],
        OU.[RevisionDate] = @RevisionDate
    OUTPUT
        INSERTED.[Id] INTO @ConfirmedIds
    FROM
        [dbo].[OrganizationUser] OU
    INNER JOIN
        @UsersToConfirm UTC ON UTC.[Id] = OU.[Id]
    WHERE
        OU.[Status] = 1 -- Only update rows that are still Accepted

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationUserIds] @ConfirmedIds

    -- Return the IDs that were actually updated so the caller can track idempotency
    SELECT [Id] FROM @ConfirmedIds
END
