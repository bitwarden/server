﻿CREATE PROCEDURE [dbo].[CipherOrganizationDetails_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        C.*,
        CASE 
            WHEN O.[UseTotp] = 1 THEN 1
            ELSE 0
        END [OrganizationUseTotp]
    FROM
        [dbo].[CipherView] C
    LEFT JOIN
        [dbo].[Organization] O ON O.[Id] = C.[OrganizationId]
    WHERE
        C.[Id] = @Id
END