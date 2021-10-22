IF NOT EXISTS (SELECT TOP(1) 1 FROM [dbo].[Organization] WHERE [MaxAutoscaleSeats] IS NOT NULL) 
    AND NOT EXISTS ( SELECT TOP(1) 1 FROM [dbo].[Organization] WHERE [OwnersNotifiedOfAutoscaling] IS NOT NULL)
BEGIN
UPDATE [dbo].[Organization]
    SET MaxAutoscaleSeats = Seats
END
