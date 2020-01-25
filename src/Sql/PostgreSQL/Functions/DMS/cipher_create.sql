CREATE OR REPLACE PROCEDURE vault_dbo.cipher_create(par_id uuid, par_userid uuid, par_organizationid uuid, par_type numeric, par_data text, par_favorites text, par_folders text, par_attachments text, par_creationdate timestamp without time zone, par_revisiondate timestamp without time zone)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    INSERT INTO vault_dbo.cipher (id, userid, organizationid, type, data, favorites, folders, attachments, creationdate, revisiondate)
    VALUES (par_Id,
    CASE
        WHEN par_OrganizationId IS NULL THEN par_UserId
        ELSE NULL
    END, par_OrganizationId, par_Type, par_Data, par_Favorites, par_Folders, par_Attachments, par_CreationDate, par_RevisionDate);

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
