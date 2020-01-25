CREATE OR REPLACE PROCEDURE vault_dbo.cipher_updateattachment(par_id uuid, par_userid uuid, par_organizationid uuid, par_attachmentid character varying, par_attachmentdata text)
 LANGUAGE plpgsql
AS $procedure$
DECLARE
    var_AttachmentIdKey VARCHAR(50) DEFAULT CONCAT('"', par_AttachmentId, '"');
    var_AttachmentIdPath VARCHAR(50) DEFAULT CONCAT('$.', var_AttachmentIdKey);
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    UPDATE vault_dbo.cipher
    SET attachments =
    CASE
        WHEN attachments IS NULL THEN CONCAT('{', var_AttachmentIdKey, ':', par_AttachmentData, '}')
        ELSE JSON_MODIFY(attachments, var_AttachmentIdPath, JSON_QUERY(par_AttachmentData, '$'))
    END
        WHERE id = par_Id;

    IF par_OrganizationId IS NOT NULL THEN
        CALL vault_dbo.organization_updatestorage(par_OrganizationId);
        CALL vault_dbo.user_bumpaccountrevisiondatebycipherid(par_Id, par_OrganizationId);
    ELSE
        IF par_UserId IS NOT NULL THEN
            CALL vault_dbo.user_updatestorage(par_UserId);
            CALL vault_dbo.user_bumpaccountrevisiondate(par_UserId);
        END IF;
    END IF;
END;
$procedure$
;
