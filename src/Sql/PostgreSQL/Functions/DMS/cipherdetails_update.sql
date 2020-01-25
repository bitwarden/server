CREATE OR REPLACE PROCEDURE vault_dbo.cipherdetails_update(par_id uuid, par_userid uuid, par_organizationid uuid, par_type numeric, par_data text, par_favorites text, par_folders text, par_attachments text, par_creationdate timestamp without time zone, par_revisiondate timestamp without time zone, par_folderid uuid, par_favorite numeric, par_edit numeric, par_organizationusetotp numeric)
 LANGUAGE plpgsql
AS $procedure$
/* not used */
/* not used */
/* not used */
/* not used */
/* not used */
DECLARE
    var_UserIdKey VARCHAR(50) DEFAULT CONCAT('"', par_UserId, '"');
    var_UserIdPath VARCHAR(50) DEFAULT CONCAT('$.', var_UserIdKey);
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    UPDATE vault_dbo.cipher
    SET userid =
    CASE
        WHEN par_OrganizationId IS NULL THEN par_UserId
        ELSE NULL
    END, organizationid = par_OrganizationId, type = par_Type, data = par_Data, folders =
    CASE
        WHEN par_FolderId IS NOT NULL AND folders IS NULL THEN CONCAT('{', var_UserIdKey, ':"', par_FolderId, '"', '}')
        WHEN par_FolderId IS NOT NULL THEN JSON_MODIFY(folders, var_UserIdPath, CAST (par_FolderId AS VARCHAR(50)))
        ELSE JSON_MODIFY(folders, var_UserIdPath, NULL)
    END, favorites =
    CASE
        WHEN par_Favorite = 1 AND favorites IS NULL THEN CONCAT('{', var_UserIdKey, ':true}')
        WHEN par_Favorite = 1 THEN JSON_MODIFY(favorites, var_UserIdPath, aws_sqlserver_ext.ToMsBit(1))
        ELSE JSON_MODIFY(favorites, var_UserIdPath, NULL)
    END, creationdate = par_CreationDate, revisiondate = par_RevisionDate
        WHERE id = par_Id;

    IF par_OrganizationId IS NOT NULL THEN
        CALL vault_dbo.user_bumpaccountrevisiondatebycipherid(par_Id, par_OrganizationId);
    ELSE
        IF par_UserId IS NOT NULL THEN
            CALL vault_dbo.user_bumpaccountrevisiondate(par_UserId);
        END IF;
    END IF;
END;
$procedure$
;
