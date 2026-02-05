# Send Access Request Validation

This feature supports the ability of Tools to require specific claims for access to sends.

In order to access Send data a user must meet the requirements laid out in these request validators.

> [!IMPORTANT]
> The string constants contained herein are used in conjunction with the Auth module in the SDK. Any change to these string values _must_ be intentional and _must_ have a corresponding change in the SDK.

There is snapshot testing that will fail if the strings change to help detect unintended changes to the string constants.

## Custom Claims

Send access tokens contain custom claims specific to the Send the Send grant type.

1. `send_id` - is always included in the issued access token. This is the `GUID` of the request Send.
1. `send_email` - only set when the Send requires `EmailOtp` authentication type.
1. `type` - this will always be `Send`

## Authentication methods

### `NeverAuthenticate`

For a Send to be in this state two things can be true:
1. The Send has been modified and no longer allows access.
2. The Send does not exist.

### `NotAuthenticated`

In this scenario the Send is not protected by any added authentication or authorization and the access token is issued to the requesting user.

### `ResourcePassword`

In this scenario the Send is password protected and a user must supply the correct password hash to be issued an access token.

### `EmailOtp`

In this scenario the Send is only accessible to owners of specific email addresses. The user must submit a correct email. Once the email has been entered then ownership of the email must be established via OTP. The Otp is sent to the aforementioned email and must be supplied, along with the email, to be issued an access token.

## Send Access Request Validation

### Required Parameters

#### All Requests
- `send_id` - Base64 URL-encoded GUID of the send being accessed

#### Password Protected Sends
- `password_hash_b64` - client hashed Base64-encoded password.

#### Email OTP Protected Sends
- `email` - Email address associated with the send
- `otp` - One-time password (optional - if missing, OTP is generated and sent)

### Error Responses

All errors include a custom response field:
```json
{
  "error": "invalid_request|invalid_grant",
  "error_description": "Human readable description",
  "send_access_error_type": "specific_error_code"
}
```