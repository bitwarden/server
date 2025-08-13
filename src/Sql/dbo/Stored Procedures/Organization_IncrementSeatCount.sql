CREATE PROCEDURE [dbo].[Organization_IncrementSeatCount]
    @OrganizationId UNIQUEIDENTIFIER,
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
END
