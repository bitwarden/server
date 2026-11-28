//! Organization invite-link crypto for the Seeder.
//!
//! Builds the sealed `Invite` blob an organization invite link stores on the server, and the
//! matching invite secret that goes into the link's `?key=` part. Both come from the same
//! `bitwarden_organization_crypto::invite::Invite` code the clients use, so a seeded link can be
//! opened by a real client. The server only stores the sealed invite; the secret is returned to the
//! caller and is never stored.

use std::ffi::{c_char, CStr, CString};

use bitwarden_crypto::{key_slot_ids, EncString, KeyStore};
use bitwarden_organization_crypto::invite::Invite;

use crate::crypto_util::{error_response, parse_key};

key_slot_ids! {
    #[symmetric]
    pub enum InviteSymmKey {
        #[local]
        Local(LocalId),
    }

    #[private]
    pub enum InvitePrivateKey {
        #[local]
        Local(LocalId),
    }

    #[signing]
    pub enum InviteSigningKey {
        #[local]
        Local(LocalId),
    }

    pub InviteIds => InviteSymmKey, InvitePrivateKey, InviteSigningKey;
}

/// Create a sealed organization invite and its invite secret.
///
/// Mirrors the clients' `InviteLinkAdminClient::make_invite`: the invite is made for the
/// organization's private key and, when `supports_confirmation` is false, confirmation is disabled.
///
/// # Arguments
/// * `organization_key_b64` - Base64-encoded organization symmetric key
/// * `wrapped_private_key` - The organization's private key wrapped with the organization key
///   (the `Organization.PrivateKey` EncString)
/// * `supports_confirmation` - Whether the invite should let an invitee recover the organization key
///
/// # Returns
/// JSON `{"invite": "<sealed invite>", "secret": "<base64url invite secret>"}`, or
/// `{"error": "..."}` on failure.
///
/// # Safety
/// Both pointers must be valid null-terminated strings.
#[no_mangle]
pub unsafe extern "C" fn make_organization_invite(
    organization_key_b64: *const c_char,
    wrapped_private_key: *const c_char,
    supports_confirmation: bool,
) -> *const c_char {
    let Ok(organization_key_str) = CStr::from_ptr(organization_key_b64).to_str() else {
        return error_response("Invalid UTF-8 in organization_key_b64");
    };

    let Ok(wrapped_private_key_str) = CStr::from_ptr(wrapped_private_key).to_str() else {
        return error_response("Invalid UTF-8 in wrapped_private_key");
    };

    match make_invite(
        organization_key_str,
        wrapped_private_key_str,
        supports_confirmation,
    ) {
        Ok((invite, secret)) => {
            let json = serde_json::json!({ "invite": invite, "secret": secret }).to_string();
            CString::new(json).unwrap().into_raw()
        }
        Err(msg) => error_response(&msg),
    }
}

fn make_invite(
    organization_key_b64: &str,
    wrapped_private_key: &str,
    supports_confirmation: bool,
) -> Result<(String, String), String> {
    let organization_key = parse_key(organization_key_b64)?;
    let wrapped_private_key: EncString = wrapped_private_key
        .parse()
        .map_err(|_| "Failed to parse wrapped private key EncString".to_string())?;

    let store: KeyStore<InviteIds> = KeyStore::default();
    let mut ctx = store.context();
    let organization_key_id = ctx.add_local_symmetric_key(organization_key);

    let (secret, mut invite) =
        Invite::make_for_private_key(organization_key_id, &wrapped_private_key, &mut ctx)
            .map_err(|e| format!("Failed to make invite: {e}"))?;

    // Invites support confirmation by default; disable it when not requested.
    if !supports_confirmation {
        invite.disable_confirmation();
    }

    Ok((String::from(&invite), String::from(&secret)))
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::free_c_string;
    use bitwarden_crypto::{SymmetricCryptoKey, SymmetricKeyAlgorithm, UserKey};
    use bitwarden_organization_crypto::invite::InviteSecret;

    struct OrgKeys {
        key: SymmetricCryptoKey,
        key_b64: String,
        wrapped_private_key: String,
    }

    fn make_org_keys() -> OrgKeys {
        let key = SymmetricCryptoKey::make(SymmetricKeyAlgorithm::Aes256CbcHmac);
        let user_key = UserKey::new(key.clone());
        let keypair = user_key.make_key_pair().unwrap();
        OrgKeys {
            key_b64: key.to_base64().to_string(),
            key,
            wrapped_private_key: keypair.private.to_string(),
        }
    }

    fn call_ffi(key_b64: &str, wrapped: &str, supports_confirmation: bool) -> serde_json::Value {
        let key = CString::new(key_b64).unwrap();
        let wrapped = CString::new(wrapped).unwrap();
        let ptr = unsafe {
            make_organization_invite(key.as_ptr(), wrapped.as_ptr(), supports_confirmation)
        };
        let result = unsafe { CStr::from_ptr(ptr) }.to_str().unwrap().to_owned();
        unsafe { free_c_string(ptr as *mut c_char) };
        serde_json::from_str(&result).unwrap()
    }

    #[test]
    fn invite_secret_opens_invite_and_matches_org_key_copy() {
        let org = make_org_keys();
        let json = call_ffi(&org.key_b64, &org.wrapped_private_key, false);

        let invite: Invite = json["invite"].as_str().unwrap().parse().unwrap();
        let secret: InviteSecret = json["secret"].as_str().unwrap().parse().unwrap();

        let store: KeyStore<InviteIds> = KeyStore::default();
        let mut ctx = store.context();

        // The invitee path: the secret from the link recovers the invite key.
        let invite_key_from_secret = invite
            .unseal_invite_key_with_invite_secret(&secret, &mut ctx)
            .expect("secret should unseal the invite key");
        assert!(invite
            .get_invite_secret(invite_key_from_secret, &mut ctx)
            .unwrap()
            .eq(&secret));

        // The admin path: the organization key recovers the same secret, as the web vault does to
        // show the link.
        let org_key_id = ctx.add_local_symmetric_key(org.key);
        let invite_key_from_org = invite
            .unseal_invite_key_with_organization_key(org_key_id, &mut ctx)
            .expect("organization key should unseal the invite key");
        assert!(invite
            .get_invite_secret(invite_key_from_org, &mut ctx)
            .unwrap()
            .eq(&secret));
    }

    #[test]
    fn supports_confirmation_controls_sealed_org_key() {
        let org = make_org_keys();

        let without: Invite = call_ffi(&org.key_b64, &org.wrapped_private_key, false)["invite"]
            .as_str()
            .unwrap()
            .parse()
            .unwrap();
        assert!(!without.supports_confirmation());

        let with: Invite = call_ffi(&org.key_b64, &org.wrapped_private_key, true)["invite"]
            .as_str()
            .unwrap()
            .parse()
            .unwrap();
        assert!(with.supports_confirmation());
    }

    #[test]
    fn each_call_makes_a_new_secret() {
        let org = make_org_keys();
        let a = call_ffi(&org.key_b64, &org.wrapped_private_key, false);
        let b = call_ffi(&org.key_b64, &org.wrapped_private_key, false);
        assert_ne!(a["secret"], b["secret"]);
    }

    #[test]
    fn private_key_wrapped_with_another_key_is_rejected() {
        let org = make_org_keys();
        let other = make_org_keys();
        let json = call_ffi(&org.key_b64, &other.wrapped_private_key, false);
        assert!(json["error"]
            .as_str()
            .unwrap()
            .contains("Failed to make invite"));
    }

    #[test]
    fn invalid_inputs_return_errors() {
        let org = make_org_keys();
        assert!(call_ffi("not-base64!", &org.wrapped_private_key, false)["error"].is_string());
        assert!(call_ffi(&org.key_b64, "not-an-encstring", false)["error"].is_string());
    }

    #[test]
    fn secret_is_url_safe() {
        let org = make_org_keys();
        let json = call_ffi(&org.key_b64, &org.wrapped_private_key, false);
        let secret = json["secret"].as_str().unwrap();
        assert!(secret
            .chars()
            .all(|c| c.is_ascii_alphanumeric() || c == '-' || c == '_' || c == '='));
    }
}
