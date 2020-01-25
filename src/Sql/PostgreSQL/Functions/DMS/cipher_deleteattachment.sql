CREATE OR REPLACE PROCEDURE vault_dbo.cipher_deleteattachment(par_id uuid, par_attachmentid character varying)
 LANGUAGE plpgsql
AS $procedure$
DECLARE
    var_AttachmentIdKey VARCHAR(50) DEFAULT CONCAT('"', par_AttachmentId, '"');
    var_AttachmentIdPath VARCHAR(50) DEFAULT CONCAT('$.', var_AttachmentIdKey);
    var_UserId UUID;
    var_OrganizationId UUID;
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    SELECT
        userid, organizationid
        INTO var_UserId, var_OrganizationId
        FROM vault_dbo.cipher
        WHERE id = par_Id;
    UPDATE vault_dbo.cipher
    SET attachments = JSON_MODIFY(attachments, var_AttachmentIdPath, NULL)
        WHERE id = par_Id;

    IF var_OrganizationId IS NOT NULL THEN
        CALL vault_dbo.organization_updatestorage(var_OrganizationId);
        CALL vault_dbo.user_bumpaccountrevisiondatebycipherid(par_Id, var_OrganizationId);
    ELSE
        IF var_UserId IS NOT NULL THEN
            CALL vault_dbo.user_updatestorage(var_UserId);
            CALL vault_dbo.user_bumpaccountrevisiondate(var_UserId);
        END IF;
    END IF;
END;
$procedure$
;
