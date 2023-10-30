BEGIN TRY
    BEGIN TRANSACTION;

    UPDATE
        [dbo].[Organization]
    SET
        [UsersGetPremium] = 1,
        [Seats] = 6,
        [MaxAutoscaleSeats] = 6
    WHERE
        [PlanType] = 1 -- Families 2019 Annual

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    THROW;
END CATCH
