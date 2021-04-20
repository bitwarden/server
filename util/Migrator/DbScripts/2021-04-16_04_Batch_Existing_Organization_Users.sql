SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- Create EmailArray type
IF NOT EXISTS (
    SELECT *
FROM sys.types
WHERE [Name] = 'EmailArray'
    AND is_user_defined = 1
)
CREATE TYPE [dbo].[EmailArray] AS TABLE (
    [Email] NVARCHAR(256) NOT NULL);
GO

-- Create OrganizationUser Email index
IF NOT EXISTS (
    SELECT *
FROM sys.indexes
WHERE [Name] = 'IX_OrganizationUser_Email'
    AND object_id = OBJECT_ID('[dbo].[OrganizationUser]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_OrganizationUser_Email]
        ON [dbo].[OrganizationUser]([Email] ASC)
END
GO

-- Create sproc to return existing users
CREATE PROCEDURE [dbo].[OrganizationUser_ReadExistingByOrganizationIdEmail]
    @OrganizationId UNIQUEIDENTIFIER,
    @Emails [dbo].[EmailArray] READONLY,
    @OnlyUsers BIT
AS
BEGIN
    SET NOCOUNT ON

    IF @OnlyUsers = 0
        SELECT
        E.Email
    FROM
        @Emails E
        INNER JOIN
        [dbo].[User] U ON U.Email = E.Email
    ELSE
        SELECT
        E.Email
    FROM
        @Emails E
        LEFT JOIN
        [dbo].[OrganizationUser] OU on OU.[Email] = E.Email
        LEFT JOIN
        [dbo].[User] U ON U.Email = E.Email
    WHERE
            OU.Email IS NOT NULL OR
        U.Email IS NOT NULL
END
GO
