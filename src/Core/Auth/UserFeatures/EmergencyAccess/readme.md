# Emergency Access System
This system allows users to share their `User.Key` with other users using public key exchange. An emergency contact (a grantee user) can view or takeover (reset the password) of the grantor user.

When an account is taken over all two factor methods are turned off and device verification is disabled.

This system is affected by the Key Rotation feature. The `EmergencyAccess.KeyEncrypted` is the `Grantor.UserKey` encrypted by the `Grantee.PublicKey`. So if the `User.Key` is rotated then all `EmergencyAccess` entities will need to be updated.

## Special Cases
Users who use `KeyConnector` are not able to allow `Takeover` of their accounts. However, they can allow `View`.

When a grantee user _takes over_ a grantor user's account, the grantor user will be removed from all organizations where the grantor user is not the `OrganizationUserType.Owner`. A grantor user will not be removed from organizations if the `EmergencyAccessType` is `View`. The grantee user will only be able to `View` the grantor user's ciphers, and not any of the organization ciphers, if any exist.

## Step 1. Invitation

A grantor user invites another user to be their emergency contact, the grantee. This will create a new `EmergencyAccess` entity in the database with the `EmergencyAccessStatusType` set to `Invited`.
The `EmergencyAccess.KeyEncrypted` field is empty, and the `GranteeId` is `null` since the user being invited via email may not have an account yet.

### code
```csharp
// creates entity.
Task<EmergencyAccess> InviteAsync(User grantorUser, string emergencyContactEmail, EmergencyAccessType accessType, int waitTime);
// resend email to the EmergencyAccess.Email.
Task ResendInviteAsync(User grantorUser, Guid emergencyAccessId);
```

## Step 2. Acceptance

The grantee user receives an email they have been invited to be an emergency contact for a grantor user.

At this point the grantee user can accept the request. This will set the `EmergencyAccess.GranteeId` to the `User.Id` of the grantee user. The `EmergencyAccess.Status` is set to `Accepted`.

If the grantee user does not have an account then they can create an account and accept the invitation.

### Code
```csharp
// accepts the request to be an emergency contact.
Task<EmergencyAccess> AcceptUserAsync(Guid emergencyAccessId, User granteeUser, string token, IUserService userService);
```

## Step 3. Confirmation

Once the grantee user has accepted, the `EmergencyAccess.GranteeId` allows the grantor user the ability to query for the `GranteeUser.PublicKey`. With the `Grantee.PublicKey`, the grantor on the client is able to safely encrypt their `User.Key` and save the encrypted string to the database.

The `EmergencyAccess.Status` is set to `Confirmed`, and the `EmergencyAccess.KeyEncrypted` is set.

### Code
```csharp
Task<EmergencyAccess> ConfirmUserAsync(Guid emergencyAccessId, string key, Guid grantorId);
```

## Step 4. Recovery Approval

The grantee user can now exercise the ability to view or takeover the account. This is done by initiating the recovery. Initiating recovery has a time delay specified by `EmergencyAccess.WaitTime`. `WaitTime` is set in the initial invite. The grantor user can approve the request before the `WaitTime`, but they _cannot_ reject the request _after_ the `WaitTime` has completed. If the recovery request is not rejected then once the `WaitTime` has passed the grantee user will be able to access the emergency access entity.

### Code
```csharp
// Initiates the recovery process; Will set EmergencyAccess.Status to RecoveryInitiated.
Task InitiateAsync(Guid id, User granteeUser);
// Approved the recovery request; Will set EmergencyAccess.Status to RecoveryApproved.
Task ApproveAsync(Guid id, User approvingUser);
// Rejects the recovery request; Will set EmergencyAccess.Status to Confirmed.
Task RejectAsync(Guid id, User rejectingUser);
// Automatically set the EmergencyAccess.Status to RecoveryApproved after WaitTime has passed.
Task HandleTimedOutRequestsAsync();
```

## Step 5. Recovering the account

Once the `EmergencyAccess.Status` is `RecoveryApproved` the grantee user is able to exercise their ability to view or takeover the grantor account. Viewing allows the grantee user to view the vault data of the grantor user. Takeover allows the grantee to change the password of the grantor user.

### Takeover
`TakeoverAsync(Guid, User)` returns the grantor user object along with the `EmergencyAccess` entity. The grantor user object is required since to update the password the client needs access to the grantor kdf configuration. Once the password has been set in the `PasswordAsync(Guid, User, string, string)` the account has been successfully recovered.

Taking over the account will change the password of the grantor user, empty the two factor array on the grantor user, and disable device verification.

```csharp
// Takeover returns the grantor user and the emergency access entity.
Task<(EmergencyAccess, User)> TakeoverAsync(Guid emergencyAccessId, User granteeUser);
// Password sets the password for the grantor user.
Task PasswordAsync(Guid emergencyAccessId, User granteeUser, string newMasterPasswordHash, string key);
// Returns Ciphers the Grantee is allowed to view based on the EmergencyAccess status.
Task<EmergencyAccessViewData> ViewAsync(Guid emergencyAccessId, User granteeUser);
// Returns downloadable cipher attachments based on the EmergencyAccess status.
Task<AttachmentResponseData> GetAttachmentDownloadAsync(Guid emergencyAccessId, Guid cipherId, string attachmentId, User granteeUser);
```

## Optional steps

The grantor user is able to delete an emergency contact at anytime, at any point in the recovery process.

### Code
```csharp
// deletes the associated EmergencyAccess Entity
Task DeleteAsync(Guid emergencyAccessId, Guid grantorId);
```
