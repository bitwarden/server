# Automatic User Confirmation

Owned by: admin-console

Automatic confirmation requests are server driven events that are sent to the admin's client where via a background service the confirmation will occur. The basic model
for the workflow is as follows:

- The Api server sends an invite email to a user.
- The user accepts the invite request, which is sent back to the Api server
- The Api server sends a push-notification with the OrganizationId and UserId to a client admin session.
- The Client performs the key exchange in the background and POSTs the ConfirmRequest back to the Api server
- The Api server runs the OrgUser_Confirm sproc to confirm the user in the DB

This Feature has the following security measures in place in order to achieve our security goals:

- The single organization exemption for admins/owners is removed for this policy.
  - This is enforced by preventing enabling the policy and organization plan feature if there are non-compliant users
- Emergency access is removed for all organization users
- Automatic confirmation will only apply to the User role (You cannot auto confirm admins/owners to an organization)
- The organization has no members with the Provider user type.
  - This will also prevent the policy and organization plan feature from being enabled
  - This will prevent sending organization invites to provider users
