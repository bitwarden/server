CREATE PROCEDURE [dbo].[User_UpdateRenewalReminderDate]
    @Id UNIQUEIDENTIFIER,
    @RenewalReminderDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[User]
    SET
        [RenewalReminderDate] = @RenewalReminderDate
    WHERE
        [Id] = @Id
END