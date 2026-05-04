//! Wrappers around `akd::client` verification functions that enforce installation
//! context. All AKD proof verification in this crate should go through this module
//! rather than calling `akd::client` directly.

use akd::hash::Digest;
use akd::AkdLabel;
use bitwarden_akd_configuration::BitwardenV1Configuration;
use uuid::Uuid;

/// Verify a lookup proof against the given root hash and epoch.
pub(crate) fn lookup_verify(
    installation_id: Uuid,
    vrf_public_key: &[u8],
    root_hash: Digest,
    current_epoch: u64,
    akd_label: AkdLabel,
    proof: akd::LookupProof,
) -> Result<akd::VerifyResult, akd::verify::VerificationError> {
    BitwardenV1Configuration::with_installation(installation_id, || {
        akd::client::lookup_verify::<BitwardenV1Configuration>(
            vrf_public_key,
            root_hash,
            current_epoch,
            akd_label,
            proof,
        )
    })
}

/// Verify a key history proof against the given root hash and epoch.
pub(crate) fn key_history_verify(
    installation_id: Uuid,
    vrf_public_key: &[u8],
    root_hash: Digest,
    current_epoch: u64,
    akd_label: AkdLabel,
    proof: akd::HistoryProof,
    params: akd::client::HistoryVerificationParams,
) -> Result<Vec<akd::VerifyResult>, akd::verify::VerificationError> {
    BitwardenV1Configuration::with_installation(installation_id, || {
        akd::client::key_history_verify::<BitwardenV1Configuration>(
            vrf_public_key,
            root_hash,
            current_epoch,
            akd_label,
            proof,
            params,
        )
    })
}
