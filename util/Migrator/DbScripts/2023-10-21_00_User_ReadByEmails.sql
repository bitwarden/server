SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER PROCEDURE [dbo].[User_ReadByEmails]
    @Emails AS [dbo].[EmailArray] READONLY
AS
BEGIN
    SET NOCOUNT ON;

    IF (SELECT COUNT(1) FROM @Emails) < 1
    BEGIN
        RETURN(-1)
    END

    SELECT
        *
    FROM
        [dbo].[UserView]
    WHERE
        [Email] IN (SELECT [Email] FROM @Emails)
END
GO
