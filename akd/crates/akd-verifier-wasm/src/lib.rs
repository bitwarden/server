//! cdylib hosting the wasm-bindgen FFI surface for `akd-verifier`.
//!
//! `tsify` generates TypeScript types so JS callers see proper discriminated
//! unions instead of `any`. wasm-bindgen can't cross generics, so
//! [`BatchVerifyError<I>`](akd_verifier::BatchVerifyError) and
//! [`BatchLookupError<I>`](akd_verifier::BatchLookupError) are concretized
//! into the wasm-flavored [`WasmBatchPairError`] / [`WasmBatchLabelError`]
//! here.

use akd_verifier::verifier::AkdVerifier as Core;
use tsify::Tsify;
use wasm_bindgen::prelude::*;

pub use akd_verifier::{
    BatchLookupError, BatchVerifyError, LookupError, VerifiedValue, VerifyError, VerifyItemError,
};
pub use bitwarden_akd_configuration::wire_models::{
    BitwardenAkdLabelMaterialRequest, BitwardenAkdPairMaterialRequest,
};

/// Per-item failure surfaced to JS as a `kind`-tagged discriminated union.
/// Reachable from `verifyPairs`'s `PerItem.failed`.
#[derive(Tsify, serde::Serialize, Debug, Clone)]
#[tsify(into_wasm_abi)]
#[serde(tag = "kind")]
pub enum WasmVerifyItemError {
    ProofInvalid {
        message: String,
    },
    ValueMismatch {
        server_epoch: u64,
        server_version: u64,
        #[tsify(type = "Uint8Array")]
        server_value: Vec<u8>,
    },
}

impl From<VerifyItemError> for WasmVerifyItemError {
    fn from(err: VerifyItemError) -> Self {
        match err {
            VerifyItemError::ProofInvalid(message) => WasmVerifyItemError::ProofInvalid { message },
            VerifyItemError::ValueMismatch {
                server_epoch,
                server_version,
                server_value,
            } => WasmVerifyItemError::ValueMismatch {
                server_epoch,
                server_version,
                server_value,
            },
        }
    }
}

/// Verified entry from `verifyPairs`, paired with its input.
#[derive(Tsify, serde::Serialize, Debug, Clone)]
#[tsify(into_wasm_abi)]
pub struct WasmVerifiedPairItem {
    pub input: BitwardenAkdPairMaterialRequest,
    pub value: VerifiedValue,
}

/// Failed entry from `verifyPairs`, paired with its input.
#[derive(Tsify, serde::Serialize, Debug, Clone)]
#[tsify(into_wasm_abi)]
pub struct WasmFailedPairItem {
    pub input: BitwardenAkdPairMaterialRequest,
    pub error: WasmVerifyItemError,
}

/// Error thrown by `verifyPairs`. Request-level outcomes (`Connection`,
/// `Protocol`, `EpochNotAudited`) affect the whole batch; `PerItem` carries
/// the partial-success ledger with per-item facts.
#[derive(Tsify, serde::Serialize, Debug, Clone)]
#[tsify(into_wasm_abi)]
#[serde(tag = "kind")]
pub enum WasmBatchPairError {
    Connection {
        message: String,
    },
    Protocol {
        message: String,
    },
    EpochNotAudited {
        epoch: u64,
    },
    PerItem {
        verified: Vec<WasmVerifiedPairItem>,
        failed: Vec<WasmFailedPairItem>,
    },
}

impl From<BatchVerifyError<BitwardenAkdPairMaterialRequest>> for WasmBatchPairError {
    fn from(err: BatchVerifyError<BitwardenAkdPairMaterialRequest>) -> Self {
        match err {
            BatchVerifyError::Connection(message) => WasmBatchPairError::Connection { message },
            BatchVerifyError::Protocol(message) => WasmBatchPairError::Protocol { message },
            BatchVerifyError::EpochNotAudited { epoch } => {
                WasmBatchPairError::EpochNotAudited { epoch }
            }
            BatchVerifyError::PerItem { verified, failed } => WasmBatchPairError::PerItem {
                verified: verified
                    .into_iter()
                    .map(|v| WasmVerifiedPairItem {
                        input: v.input,
                        value: v.value,
                    })
                    .collect(),
                failed: failed
                    .into_iter()
                    .map(|f| WasmFailedPairItem {
                        input: f.input,
                        error: f.error.into(),
                    })
                    .collect(),
            },
        }
    }
}

/// Verified entry from `lookupBatch`, paired with its input label.
#[derive(Tsify, serde::Serialize, Debug, Clone)]
#[tsify(into_wasm_abi)]
pub struct WasmVerifiedLabelItem {
    pub input: BitwardenAkdLabelMaterialRequest,
    pub value: VerifiedValue,
}

/// Failed entry from `lookupBatch`, paired with its input label. Only
/// proof-verification failure is reachable here, so the failure carries a
/// plain message.
#[derive(Tsify, serde::Serialize, Debug, Clone)]
#[tsify(into_wasm_abi)]
pub struct WasmFailedLabelItem {
    pub input: BitwardenAkdLabelMaterialRequest,
    pub proof_error: String,
}

/// Error thrown by `lookupBatch`. See [`WasmBatchPairError`].
#[derive(Tsify, serde::Serialize, Debug, Clone)]
#[tsify(into_wasm_abi)]
#[serde(tag = "kind")]
pub enum WasmBatchLabelError {
    Connection {
        message: String,
    },
    Protocol {
        message: String,
    },
    EpochNotAudited {
        epoch: u64,
    },
    PerItem {
        verified: Vec<WasmVerifiedLabelItem>,
        failed: Vec<WasmFailedLabelItem>,
    },
}

impl From<BatchLookupError<BitwardenAkdLabelMaterialRequest>> for WasmBatchLabelError {
    fn from(err: BatchLookupError<BitwardenAkdLabelMaterialRequest>) -> Self {
        match err {
            BatchLookupError::Connection(message) => WasmBatchLabelError::Connection { message },
            BatchLookupError::Protocol(message) => WasmBatchLabelError::Protocol { message },
            BatchLookupError::EpochNotAudited { epoch } => {
                WasmBatchLabelError::EpochNotAudited { epoch }
            }
            BatchLookupError::PerItem { verified, failed } => WasmBatchLabelError::PerItem {
                verified: verified
                    .into_iter()
                    .map(|v| WasmVerifiedLabelItem {
                        input: v.input,
                        value: v.value,
                    })
                    .collect(),
                failed: failed
                    .into_iter()
                    .map(|f| WasmFailedLabelItem {
                        input: f.input,
                        proof_error: f.proof_error,
                    })
                    .collect(),
            },
        }
    }
}

/// AKD verifier exposed to JavaScript via wasm-bindgen.
#[wasm_bindgen]
pub struct AkdVerifier {
    inner: Core,
}

#[wasm_bindgen]
impl AkdVerifier {
    /// See [`Core::new`]. `installation_id` must be a valid UUID string.
    #[wasm_bindgen(constructor)]
    pub fn new(
        reader_url: String,
        watch_url: String,
        watch_namespace: String,
        installation_id: String,
    ) -> Result<AkdVerifier, JsError> {
        let installation_id: uuid::Uuid = installation_id
            .parse()
            .map_err(|e: uuid::Error| JsError::new(&format!("Invalid installation_id: {e}")))?;
        let inner = Core::new(reader_url, watch_url, watch_namespace, installation_id)
            .map_err(|e| JsError::new(&e.to_string()))?;
        Ok(Self { inner })
    }

    /// See [`Core::verify_pair`].
    #[wasm_bindgen(js_name = "verifyPair")]
    pub async fn verify_pair(
        &self,
        pair: BitwardenAkdPairMaterialRequest,
    ) -> Result<VerifiedValue, JsError> {
        self.inner
            .verify_pair(pair)
            .await
            .map_err(|e| JsError::new(&e.to_string()))
    }

    /// See [`Core::verify_pairs`].
    #[wasm_bindgen(js_name = "verifyPairs")]
    pub async fn verify_pairs(
        &self,
        pairs: Vec<BitwardenAkdPairMaterialRequest>,
    ) -> Result<Vec<VerifiedValue>, WasmBatchPairError> {
        self.inner
            .verify_pairs(pairs)
            .await
            .map_err(WasmBatchPairError::from)
    }

    /// See [`Core::lookup`].
    #[wasm_bindgen(js_name = "lookup")]
    pub async fn lookup(
        &self,
        label: BitwardenAkdLabelMaterialRequest,
    ) -> Result<VerifiedValue, JsError> {
        self.inner
            .lookup(label)
            .await
            .map_err(|e| JsError::new(&e.to_string()))
    }

    /// See [`Core::lookup_batch`].
    #[wasm_bindgen(js_name = "lookupBatch")]
    pub async fn lookup_batch(
        &self,
        labels: Vec<BitwardenAkdLabelMaterialRequest>,
    ) -> Result<Vec<VerifiedValue>, WasmBatchLabelError> {
        self.inner
            .lookup_batch(labels)
            .await
            .map_err(WasmBatchLabelError::from)
    }

    /// See [`Core::lookup_history`].
    #[wasm_bindgen(js_name = "lookupHistory")]
    pub async fn lookup_history(
        &self,
        label: BitwardenAkdLabelMaterialRequest,
    ) -> Result<Vec<VerifiedValue>, JsError> {
        self.inner
            .lookup_history(label)
            .await
            .map_err(|e| JsError::new(&e.to_string()))
    }

    /// See [`Core::audited`].
    #[wasm_bindgen(js_name = "audited")]
    pub async fn audited(&self, epoch: u64) -> Result<bool, JsError> {
        self.inner
            .audited(epoch)
            .await
            .map_err(|e| JsError::new(&e.to_string()))
    }
}
