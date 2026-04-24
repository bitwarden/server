-- Create the OrganizationUserToConfirmArray table type if it does not already exist.
-- SQL Server does not support CREATE OR ALTER for user-defined table types, so we check
-- the catalog directly and skip creation when the type is already present.
IF NOT EXISTS (
    SELECT 1
    FROM sys.types
    WHERE name = 'OrganizationUserToConfirmArray'
      AND is_table_type = 1
      AND schema_id = SCHEMA_ID('dbo')
)
BEGIN
    EXEC ('
        CREATE TYPE [dbo].[OrganizationUserToConfirmArray] AS TABLE (
            [Id]     UNIQUEIDENTIFIER NOT NULL,
            [UserId] UNIQUEIDENTIFIER NOT NULL,
            [Key]    NVARCHAR(MAX)    NULL
        )
    ')
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_ConfirmByIds]
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
GO
