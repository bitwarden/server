IF COL_LENGTH('[dbo].[User]', 'VerifyDevices') IS NULL
BEGIN
    ALTER TABLE
        [dbo].[User]
    ADD
        [VerifyDevices] BIT NOT NULL DEFAULT 1
END
GO