CREATE OR REPLACE PROCEDURE vault_dbo.organizationuser_update(par_id uuid, par_organizationid uuid, par_userid uuid, par_email character varying, par_key text, par_status numeric, par_type numeric, par_accessall numeric, par_externalid character varying, par_creationdate timestamp without time zone, par_revisiondate timestamp without time zone)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    UPDATE vault_dbo.organizationuser
    SET organizationid = par_OrganizationId, userid = par_UserId, email = par_Email, key = par_Key, status = par_Status, type = par_Type, accessall = par_AccessAll, externalid = par_ExternalId, creationdate = par_CreationDate, revisiondate = par_RevisionDate
        WHERE id = par_Id;
    CALL vault_dbo.user_bumpaccountrevisiondate(par_UserId);
END;
$procedure$
;
