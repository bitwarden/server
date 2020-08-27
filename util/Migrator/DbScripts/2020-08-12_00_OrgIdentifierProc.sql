IF OBJECT_ID('[dbo].[Organization_ReadByIdentifier]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Organization_ReadByIdentifier]
END
GO

CREATE PROCEDURE [dbo].[Organization_ReadByIdentifier]
    @Identifier NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationView]
    WHERE
        [Identifier] = @Identifier
END
GO
