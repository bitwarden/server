CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_ConfirmById]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @RevisionDate DATETIME2(7),
    @Key NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @RowCount INT;

    UPDATE
        [dbo].[OrganizationUser]
    SET
        [Status] = 2, -- Set to Confirmed
        [RevisionDate] = @RevisionDate,
        [Key] = @Key
    WHERE
        [Id] = @Id
      AND [Status] = 1 -- Only update if status is Accepted

    SET @RowCount = @@ROWCOUNT;

    IF @RowCount > 0
        BEGIN
            EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
        END

    SELECT @RowCount;
END
