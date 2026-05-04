//! HTTP wire types shared between the AKD server and the
//! `akd-verifier` SDK (client).
//!
//! Covers both the request bodies and the response shapes the reader
//! produces. Sharing them prevents server and client from drifting independently.

use akd::{AkdLabel, AkdValue, EpochHash, HistoryProof, LookupProof, SingleAppendOnlyProof};
use bitwarden_encoding::B64;
use serde::{Deserialize, Serialize};
use uuid::Uuid;

use crate::{BitwardenAkdLabelMaterial, BitwardenAkdPairMaterial};

// ============================================================================
// Request material types — the wire shape of label/pair material that the
// reader and publisher accept in request bodies. Also exposed across the FFI
// boundary so foreign callers can construct them directly.
// ============================================================================

#[cfg_attr(feature = "uniffi", derive(uniffi::Enum))]
#[cfg_attr(
    feature = "wasm",
    derive(tsify::Tsify),
    tsify(into_wasm_abi, from_wasm_abi)
)]
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(tag = "type")]
pub enum BitwardenAkdPairMaterialRequest {
    UserRealWorldId {
        real_world_id: String,
        #[cfg_attr(feature = "wasm", tsify(type = "string"))]
        user_id: Uuid,
    },
    UserPublicKey {
        #[cfg_attr(feature = "wasm", tsify(type = "string"))]
        user_id: Uuid,
        #[cfg_attr(feature = "wasm", tsify(type = "string"))]
        public_key_der_b64: B64,
    },
}

impl From<BitwardenAkdPairMaterialRequest> for BitwardenAkdPairMaterial {
    fn from(req: BitwardenAkdPairMaterialRequest) -> Self {
        match req {
            BitwardenAkdPairMaterialRequest::UserRealWorldId {
                real_world_id,
                user_id,
            } => BitwardenAkdPairMaterial::UserRealWorldId {
                real_world_id,
                user_id,
            },
            BitwardenAkdPairMaterialRequest::UserPublicKey {
                user_id,
                public_key_der_b64,
            } => BitwardenAkdPairMaterial::UserPublicKey {
                user_id,
                public_key_der: public_key_der_b64.into_bytes(),
            },
        }
    }
}

impl From<&BitwardenAkdPairMaterialRequest> for BitwardenAkdLabelMaterialRequest {
    fn from(req: &BitwardenAkdPairMaterialRequest) -> Self {
        match req {
            BitwardenAkdPairMaterialRequest::UserRealWorldId {
                real_world_id,
                user_id: _,
            } => BitwardenAkdLabelMaterialRequest::UserRealWorldId {
                real_world_id: real_world_id.clone(),
            },
            BitwardenAkdPairMaterialRequest::UserPublicKey {
                user_id,
                public_key_der_b64: _,
            } => BitwardenAkdLabelMaterialRequest::UserPublicKey { user_id: *user_id },
        }
    }
}

impl From<BitwardenAkdPairMaterialRequest> for AkdLabel {
    fn from(req: BitwardenAkdPairMaterialRequest) -> Self {
        BitwardenAkdPairMaterial::from(req).into()
    }
}

impl From<BitwardenAkdPairMaterialRequest> for AkdValue {
    fn from(req: BitwardenAkdPairMaterialRequest) -> Self {
        BitwardenAkdPairMaterial::from(req).into()
    }
}

#[cfg_attr(feature = "uniffi", derive(uniffi::Enum))]
#[cfg_attr(
    feature = "wasm",
    derive(tsify::Tsify),
    tsify(into_wasm_abi, from_wasm_abi)
)]
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(tag = "type")]
pub enum BitwardenAkdLabelMaterialRequest {
    UserRealWorldId {
        real_world_id: String,
    },
    UserPublicKey {
        #[cfg_attr(feature = "wasm", tsify(type = "string"))]
        user_id: Uuid,
    },
}

impl From<BitwardenAkdLabelMaterialRequest> for BitwardenAkdLabelMaterial {
    fn from(value: BitwardenAkdLabelMaterialRequest) -> Self {
        match value {
            BitwardenAkdLabelMaterialRequest::UserRealWorldId { real_world_id } => {
                BitwardenAkdLabelMaterial::UserRealWorldId { real_world_id }
            }
            BitwardenAkdLabelMaterialRequest::UserPublicKey { user_id } => {
                BitwardenAkdLabelMaterial::UserPublicKey { user_id }
            }
        }
    }
}

impl From<BitwardenAkdLabelMaterialRequest> for AkdLabel {
    fn from(value: BitwardenAkdLabelMaterialRequest) -> Self {
        BitwardenAkdLabelMaterial::from(value).into()
    }
}

// ============================================================================
// Response envelope and per-endpoint request/response shapes.
// ============================================================================

/// Generic envelope wrapping all reader API responses.
///
/// Canonical shapes are `success=true + data=Some + error=None` and
/// `success=false + error=Some`. Clients should treat any other combination
/// as a protocol violation.
///
/// `C` is the type of the error code carried inside [`ErrorResponse`]. The
/// server passes its own typed error enum (which serializes via serde rename
/// to a wire string); deserializing clients can use the default `String` and
/// avoid enumerating every server-side variant.
#[derive(Debug, Serialize, Deserialize)]
pub struct Response<T, C = String> {
    pub success: bool,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub data: Option<T>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub error: Option<ErrorResponse<C>>,
}

impl<T, C> Response<T, C> {
    pub fn success(data: T) -> Self {
        Self {
            success: true,
            data: Some(data),
            error: None,
        }
    }

    pub fn error(err: ErrorResponse<C>) -> Self {
        Self {
            success: false,
            data: None,
            error: Some(err),
        }
    }
}

/// Error payload sent inside [`Response::error`].
///
/// `code` is a machine-readable identifier; `message` is human-readable.
/// `C` defaults to `String` for clients that don't care to enumerate every
/// server-side variant; servers can pass their own typed enum (e.g. with
/// `#[serde(rename_all = "SCREAMING_SNAKE_CASE")]`) and rely on serde to
/// produce the canonical wire form.
#[derive(Debug, Serialize, Deserialize)]
pub struct ErrorResponse<C = String> {
    pub code: C,
    pub message: String,
}

/// Epoch + epoch-hash pair returned alongside proofs.
#[derive(Debug, Serialize, Deserialize)]
pub struct EpochData {
    pub epoch: u64,
    pub epoch_hash_b64: B64,
}

impl From<EpochHash> for EpochData {
    fn from(epoch_hash: EpochHash) -> Self {
        EpochData {
            epoch: epoch_hash.0,
            epoch_hash_b64: B64::from(epoch_hash.1.as_ref()),
        }
    }
}

/// Request body for `POST /lookup`.
#[derive(Debug, Serialize, Deserialize)]
pub struct LookupRequest {
    pub bitwarden_akd_label_material: BitwardenAkdLabelMaterialRequest,
}

/// Response data for `POST /lookup`.
#[derive(Debug, Serialize, Deserialize)]
pub struct LookupData {
    pub lookup_proof: LookupProof,
    pub epoch_data: EpochData,
}

/// Request body for `POST /batch_lookup`.
#[derive(Debug, Serialize, Deserialize)]
pub struct BatchLookupRequest {
    pub bitwarden_akd_labels: Vec<BitwardenAkdLabelMaterialRequest>,
}

/// Response data for `POST /batch_lookup`.
#[derive(Debug, Serialize, Deserialize)]
pub struct BatchLookupData {
    pub lookup_proofs: Vec<LookupProof>,
    /// Epoch and root-hash the proofs are rooted at — i.e. the directory's
    /// current state at response time.
    pub current_epoch_data: EpochData,
}

/// Request body for `POST /key_history`.
#[derive(Debug, Serialize, Deserialize)]
pub struct KeyHistoryRequest {
    pub bitwarden_akd_label_material: BitwardenAkdLabelMaterialRequest,
    pub history_params: HistoryParams,
}

/// Parameters dictating how much of the history proof the server should
/// return. Wire-compatible with [`akd::HistoryParams`].
#[derive(Copy, Clone, Debug, Serialize, Deserialize)]
pub enum HistoryParams {
    /// Full history for a label.
    Complete,
    /// Up to the most recent `N` updates for a label.
    MostRecent(usize),
}

impl From<HistoryParams> for akd::HistoryParams {
    fn from(params: HistoryParams) -> Self {
        match params {
            HistoryParams::Complete => akd::HistoryParams::Complete,
            HistoryParams::MostRecent(n) => akd::HistoryParams::MostRecent(n),
        }
    }
}

/// Response data for `POST /key_history`.
#[derive(Debug, Serialize, Deserialize)]
pub struct HistoryData {
    pub history_proof: HistoryProof,
    pub epoch_data: EpochData,
}

/// Request body for `POST /audit`.
#[derive(Debug, Serialize, Deserialize)]
pub struct AuditRequest {
    /// The epoch to audit. Proves the transition from `epoch-1` to this epoch.
    pub epoch: u64,
}

/// Response data for `POST /audit`.
pub type AuditData = SingleAppendOnlyProof;

/// Response data for `GET /public_key` — the VRF public key, base64 encoded.
pub type PublicKeyData = B64;

/// Response data for `GET /health`.
#[derive(Debug, Serialize, Deserialize)]
pub struct HealthData {
    pub time: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub predicted_next_epoch_datetime: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub predicted_seconds_until_next_epoch: Option<f64>,
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_pair_material_request_user_real_world_id_from_json() {
        let json = r#"{
            "type": "UserRealWorldId",
            "real_world_id": "user@example.com",
            "user_id": "550e8400-e29b-41d4-a716-446655440000"
        }"#;

        let req: BitwardenAkdPairMaterialRequest = serde_json::from_str(json).expect("valid json");
        let pair: BitwardenAkdPairMaterial = req.into();

        let BitwardenAkdPairMaterial::UserRealWorldId {
            real_world_id,
            user_id,
        } = pair
        else {
            panic!("Converted to wrong variant")
        };

        assert_eq!(real_world_id, "user@example.com");
        assert_eq!(
            user_id,
            Uuid::parse_str("550e8400-e29b-41d4-a716-446655440000").expect("valid uuid")
        );
    }

    #[test]
    fn test_pair_material_request_user_public_key_from_json() {
        let json = r#"{
            "type": "UserPublicKey",
            "user_id": "550e8400-e29b-41d4-a716-446655440000",
            "public_key_der_b64": "MIIBIQ=="
        }"#;

        let req: BitwardenAkdPairMaterialRequest = serde_json::from_str(json).expect("valid json");
        let pair: BitwardenAkdPairMaterial = req.into();

        let BitwardenAkdPairMaterial::UserPublicKey {
            user_id,
            public_key_der,
        } = pair
        else {
            panic!("Converted to wrong variant")
        };

        assert_eq!(
            user_id,
            Uuid::parse_str("550e8400-e29b-41d4-a716-446655440000").expect("valid uuid")
        );
        assert_eq!(public_key_der, vec![48, 130, 1, 33]);
    }

    #[test]
    fn test_label_material_request_user_real_world_id_from_json() {
        let json = r#"{
            "type": "UserRealWorldId",
            "real_world_id": "user@example.com"
        }"#;

        let req: BitwardenAkdLabelMaterialRequest = serde_json::from_str(json).expect("valid json");
        let label: BitwardenAkdLabelMaterial = req.into();

        let BitwardenAkdLabelMaterial::UserRealWorldId { real_world_id } = label else {
            panic!("Converted to wrong variant")
        };

        assert_eq!(real_world_id, "user@example.com");
    }

    #[test]
    fn test_label_material_request_user_public_key_from_json() {
        let json = r#"{
            "type": "UserPublicKey",
            "user_id": "550e8400-e29b-41d4-a716-446655440000"
        }"#;

        let req: BitwardenAkdLabelMaterialRequest = serde_json::from_str(json).expect("valid json");
        let label: BitwardenAkdLabelMaterial = req.into();

        let BitwardenAkdLabelMaterial::UserPublicKey { user_id } = label else {
            panic!("Converted to wrong variant")
        };

        assert_eq!(
            user_id,
            Uuid::parse_str("550e8400-e29b-41d4-a716-446655440000").expect("valid uuid")
        );
    }
}
