IF OBJECT_ID('[dbo].[CipherOrganizationDetailsWithCollections_ReadByOrganizationId]', 'P') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[CipherOrganizationDetailsWithCollections_ReadByOrganizationId];
END
GO

CREATE PROCEDURE [dbo].[CipherOrgDetailsWithCollections_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        c.*,
        od.OrganizationUseTotp
    FROM dbo.Cipher AS c
    JOIN dbo.OrganizationDetail AS od
      ON od.CipherId = c.Id
     AND od.OrganizationId = @OrganizationId
    WHERE c.OrganizationId = @OrganizationId;

    SELECT
        cc.CipherId,
        cc.CollectionId
    FROM dbo.CollectionCipher AS cc
    JOIN dbo.Collection AS col
      ON col.Id = cc.CollectionId
     AND col.OrganizationId = @OrganizationId;
END
GO
