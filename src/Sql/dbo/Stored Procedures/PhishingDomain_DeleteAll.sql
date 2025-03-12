CREATE PROCEDURE [dbo].[PhishingDomain_DeleteAll]
AS
BEGIN
    SET NOCOUNT ON

    DELETE FROM
        [dbo].[PhishingDomain]
END 