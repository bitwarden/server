CREATE OR ALTER PROCEDURE [dbo].[Organization_IncrementSeatCount]
    @OrganizationId UNIQUEIDENTIFIER,
    @ExpectedCurrentSeats INT,
    @SeatsToAdd INT,
    @RequestDate DATETIME2
AS
BEGIN
    SET NOCOUNT ON

    UPDATE [dbo].[Organization]
    SET
        [Seats] = [Seats] + @SeatsToAdd,
        [SyncSeats] = 1,
        [RevisionDate] = @RequestDate
    WHERE [Id] = @OrganizationId
        AND [Seats] = @ExpectedCurrentSeats

    IF @@ROWCOUNT = 0
    BEGIN
        RAISERROR('Seat count concurrency conflict: the current seat count does not match the expected value.', 16, 1)
    END
END
