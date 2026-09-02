CREATE PROCEDURE [dbo].[OrganizationApplication_ReadById]
    @Id UNIQUEIDENTIFIER
AS
    SET NOCOUNT ON;

    SELECT
        *
    FROM [dbo].[OrganizationApplicationView]
    WHERE [Id] = @Id;
