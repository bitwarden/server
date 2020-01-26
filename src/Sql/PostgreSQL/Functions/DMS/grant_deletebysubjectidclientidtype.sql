CREATE OR REPLACE PROCEDURE vault_dbo.grant_deletebysubjectidclientidtype(par_subjectid character varying, par_clientid character varying, par_type character varying)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    DELETE FROM vault_dbo."Grant"
        WHERE LOWER(subjectid) = LOWER(par_SubjectId) AND LOWER(clientid) = LOWER(par_ClientId) AND LOWER(type) = LOWER(par_Type);
END;
$procedure$
;
