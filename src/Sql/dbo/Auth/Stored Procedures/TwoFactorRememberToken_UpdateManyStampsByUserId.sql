CREATE PROCEDURE [dbo].[TwoFactorRememberToken_UpdateManyStampsByUserId]
    @UserId       UNIQUEIDENTIFIER,
    @Stamp        NVARCHAR(50),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Revokes every remember token this user holds by replacing the value each of their tokens is
    -- compared against. The rows are kept, so CreationDate still records when each device was first
    -- remembered and RevisionDate now records when it was cut off.
    --
    -- One stamp is shared across the user's rows, and the caller supplies it. Rows are located by
    -- (UserId, DeviceId), so the stamp never selects a row and a token naming one device can never
    -- match another's. Generating it in application code also keeps this procedure and the Entity
    -- Framework implementations writing identical values.
    UPDATE
        [dbo].[TwoFactorRememberToken]
    SET
        [Stamp] = @Stamp,
        [RevisionDate] = @RevisionDate
    WHERE
        [UserId] = @UserId
END
