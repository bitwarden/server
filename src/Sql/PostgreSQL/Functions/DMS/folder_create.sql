CREATE OR REPLACE PROCEDURE vault_dbo.folder_create(par_id uuid, par_userid uuid, par_name text, par_creationdate timestamp without time zone, par_revisiondate timestamp without time zone)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    INSERT INTO vault_dbo.folder (id, userid, name, creationdate, revisiondate)
    VALUES (par_Id, par_UserId, par_Name, par_CreationDate, par_RevisionDate);
    CALL vault_dbo.user_bumpaccountrevisiondate(par_UserId);
END;
$procedure$
;
