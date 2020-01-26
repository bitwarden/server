CREATE OR REPLACE PROCEDURE vault_dbo."grant_readbysubjectid$tmp"(par_subjectid character varying)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    DROP TABLE IF EXISTS Grant_ReadBySubjectId$TMPTBL;
    CREATE TEMP TABLE Grant_ReadBySubjectId$TMPTBL
    AS
    SELECT
        *
        FROM vault_dbo.grantview
        WHERE LOWER(subjectid) = LOWER(par_SubjectId);
END;
$procedure$
;
