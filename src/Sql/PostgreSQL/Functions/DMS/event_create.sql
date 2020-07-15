CREATE OR REPLACE PROCEDURE event_create(par_id uuid, par_type numeric, par_userid uuid, par_organizationid uuid, par_cipherid uuid, par_collection_id uuid, par_groupid uuid, par_organization_userid uuid, par_actinguserid uuid, par_devicetype numeric, par_ipaddress character varying, par_date timestamp without time zone)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    INSERT INTO event (id, type, userid, organizationid, cipherid, collection_id, groupid, organization_userid, actinguserid, devicetype, ipaddress, date)
    VALUES (par_Id, par_Type, par_UserId, par_OrganizationId, par_CipherId, par_CollectionId, par_GroupId, par_OrganizationUserId, par_ActingUserId, par_DeviceType, par_IpAddress, par_Date);
END;
$procedure$
;
