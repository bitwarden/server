CREATE OR REPLACE PROCEDURE vault_dbo.folder_update(par_id uuid, par_userid uuid, par_name text, par_creationdate timestamp without time zone, par_revisiondate timestamp without time zone)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    UPDATE vault_dbo.folder
    SET userid = par_UserId, name = par_Name, creationdate = par_CreationDate, revisiondate = par_RevisionDate
        WHERE id = par_Id;
    CALL vault_dbo.user_bumpaccountrevisiondate(par_UserId);
END;
$procedure$
;
