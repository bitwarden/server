CREATE OR REPLACE PROCEDURE cipherdetails_create(par_id uuid, par_userid uuid, par_organizationid uuid, par_type numeric, par_data text, par_favorites text, par_folders text, par_attachments text, par_creationdate timestamp without time zone, par_revisiondate timestamp without time zone, par_folderid uuid, par_favorite numeric, par_edit numeric, par_organizationusetotp numeric)
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
    INSERT INTO cipher (id, userid, organizationid, type, data, favorites, folders, creationdate, revisiondate)
    VALUES (par_Id,
    CASE
        WHEN par_OrganizationId IS NULL THEN par_UserId
        ELSE NULL
    END, par_OrganizationId, par_Type, par_Data,
    CASE
        WHEN par_Favorite = 1 THEN CONCAT('{', var_UserIdKey, ':true}')
        ELSE NULL
    END,
    CASE
        WHEN par_FolderId IS NOT NULL THEN CONCAT('{', var_UserIdKey, ':"', par_FolderId, '"', '}')
        ELSE NULL
    END, par_CreationDate, par_RevisionDate);

    IF par_OrganizationId IS NOT NULL THEN
        CALL user_bumpaccountrevisiondatebycipherid(par_Id, par_OrganizationId);
    ELSE
        IF par_UserId IS NOT NULL THEN
            CALL user_bumpaccountrevisiondate(par_UserId);
        END IF;
    END IF;
END;
$procedure$
;
