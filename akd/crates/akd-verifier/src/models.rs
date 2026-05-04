//! Verifier-only types. Wire types shared with the reader live in
//! `bitwarden_akd_configuration::wire_models`; this module re-exports them
//! for ergonomic access and adds the verifier-specific types
//! ([`AuditSignatureResponse`] for the akd-watch endpoint, [`VerifiedValue`]
//! as the SDK's public per-entry result).

use serde::Deserialize;

pub use bitwarden_akd_configuration::wire_models::{
    BatchLookupData, BatchLookupRequest, BitwardenAkdLabelMaterialRequest,
    BitwardenAkdPairMaterialRequest, EpochData, ErrorResponse, HistoryData, HistoryParams,
    KeyHistoryRequest, Response,
};

/// Response from the akd-watch audit endpoint.
/// `None` (returned as a `null` body or 404) means the epoch has not been
/// audited yet.
#[derive(Debug, Deserialize)]
pub struct AuditSignatureResponse {
    pub epoch: u64,
    pub digest: String,
    pub signature: String,
    pub key_id: String,
    pub timestamp: u64,
}

/// A verified value at a specific (`epoch`, `version`).
/// `value` is the raw bytes the server committed at this label.
#[cfg_attr(feature = "uniffi", derive(uniffi::Record))]
#[cfg_attr(
    feature = "wasm",
    derive(tsify::Tsify, serde::Serialize),
    tsify(into_wasm_abi)
)]
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct VerifiedValue {
    pub epoch: u64,
    pub version: u64,
    #[cfg_attr(feature = "wasm", tsify(type = "Uint8Array"))]
    pub value: Vec<u8>,
}
