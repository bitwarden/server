CREATE OR REPLACE PROCEDURE vault_dbo.grant_deletebysubjectidclientid(par_subjectid character varying, par_clientid character varying)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    DELETE FROM vault_dbo."Grant"
        WHERE LOWER(subjectid) = LOWER(par_SubjectId) AND LOWER(clientid) = LOWER(par_ClientId);
END;
$procedure$
;
