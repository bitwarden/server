CREATE PROCEDURE [dbo].[OrganizationPartnership_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationPartnershipView]
    WHERE
        [Id] = @Id
END
