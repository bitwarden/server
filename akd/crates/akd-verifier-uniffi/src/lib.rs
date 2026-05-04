//! cdylib hosting the uniffi FFI surface for `akd-verifier`. Bundles types
//! from `akd-verifier` and `bitwarden-akd-configuration` so foreign-binding
//! generators (Swift, Kotlin) can see everything reachable from the public
//! API in one library.
//!
//! Async core methods are exposed synchronously here via a tokio runtime,
//! matching how Swift/Kotlin callers consume the SDK. uniffi cannot cross
//! generics, so [`BatchVerifyError<I>`](akd_verifier::BatchVerifyError) and
//! [`BatchLookupError<I>`](akd_verifier::BatchLookupError) are concretized
//! into [`BatchPairError`] / [`BatchLabelError`] at this layer.

use std::sync::Arc;

use akd_verifier::verifier::AkdVerifier as Core;
use uuid::Uuid;

pub use akd_verifier::{
    BatchLookupError, BatchVerifyError, LookupError, TransportError, VerifiedValue, VerifyError,
    VerifyItemError,
};
pub use bitwarden_akd_configuration::wire_models::{
    BitwardenAkdLabelMaterialRequest, BitwardenAkdPairMaterialRequest,
};

uniffi::setup_scaffolding!("akd_verifier_uniffi");

akd_verifier::uniffi_reexport_scaffolding!();
bitwarden_akd_configuration::uniffi_reexport_scaffolding!();

// Bridge `Uuid` and `B64` — both registered in `bitwarden-akd-configuration`
// as `custom_type!(... remote ...)` (concrete `FfiConverter<bac::UniFfiTag>`
// impl, not blanket). The local FFI records below contain
// `BitwardenAkd*MaterialRequest`, which transitively contains `Uuid`/`B64`,
// so this crate also needs the bridges.
mod uniffi_remote_bridges {
    use bitwarden_encoding::B64;
    use uuid::Uuid;

    uniffi::use_remote_type!(bitwarden_akd_configuration::Uuid);
    uniffi::use_remote_type!(bitwarden_akd_configuration::B64);
}

/// A successfully verified entry from `verify_pairs`, paired with the input
/// that produced it.
#[derive(::uniffi::Record, Debug, Clone)]
pub struct VerifiedPairItem {
    pub input: BitwardenAkdPairMaterialRequest,
    pub value: VerifiedValue,
}

/// A failed entry from `verify_pairs`, paired with the input that produced it.
#[derive(::uniffi::Record, Debug, Clone)]
pub struct FailedPairItem {
    pub input: BitwardenAkdPairMaterialRequest,
    pub error: VerifyItemError,
}

/// Error returned by `verify_pairs`. Request-level outcomes (`Connection`,
/// `Protocol`, `EpochNotAudited`) affect the whole batch; `PerItem` carries
/// the partial-success ledger with per-item facts.
#[derive(::uniffi::Error, Debug, thiserror::Error)]
pub enum BatchPairError {
    #[error("{message}")]
    Connection { message: String },

    #[error("Unexpected response: {message}")]
    Protocol { message: String },

    #[error("Epoch {epoch} has not been audited yet")]
    EpochNotAudited { epoch: u64 },

    #[error("Batch had per-item failures")]
    PerItem {
        verified: Vec<VerifiedPairItem>,
        failed: Vec<FailedPairItem>,
    },
}

impl From<BatchVerifyError<BitwardenAkdPairMaterialRequest>> for BatchPairError {
    fn from(err: BatchVerifyError<BitwardenAkdPairMaterialRequest>) -> Self {
        match err {
            BatchVerifyError::Connection(s) => BatchPairError::Connection { message: s },
            BatchVerifyError::Protocol(s) => BatchPairError::Protocol { message: s },
            BatchVerifyError::EpochNotAudited { epoch } => {
                BatchPairError::EpochNotAudited { epoch }
            }
            BatchVerifyError::PerItem { verified, failed } => BatchPairError::PerItem {
                verified: verified
                    .into_iter()
                    .map(|v| VerifiedPairItem {
                        input: v.input,
                        value: v.value,
                    })
                    .collect(),
                failed: failed
                    .into_iter()
                    .map(|f| FailedPairItem {
                        input: f.input,
                        error: f.error,
                    })
                    .collect(),
            },
        }
    }
}

/// A successfully verified entry from `lookup_batch`, paired with the input
/// label.
#[derive(::uniffi::Record, Debug, Clone)]
pub struct VerifiedLabelItem {
    pub input: BitwardenAkdLabelMaterialRequest,
    pub value: VerifiedValue,
}

/// A failed entry from `lookup_batch`, paired with the input label. Only
/// proof-verification failure is reachable here, so the failure carries a
/// plain message.
#[derive(::uniffi::Record, Debug, Clone)]
pub struct FailedLabelItem {
    pub input: BitwardenAkdLabelMaterialRequest,
    pub proof_error: String,
}

/// Error returned by `lookup_batch`. See [`BatchPairError`].
#[derive(::uniffi::Error, Debug, thiserror::Error)]
pub enum BatchLabelError {
    #[error("{message}")]
    Connection { message: String },

    #[error("Unexpected response: {message}")]
    Protocol { message: String },

    #[error("Epoch {epoch} has not been audited yet")]
    EpochNotAudited { epoch: u64 },

    #[error("Batch had per-item failures")]
    PerItem {
        verified: Vec<VerifiedLabelItem>,
        failed: Vec<FailedLabelItem>,
    },
}

impl From<BatchLookupError<BitwardenAkdLabelMaterialRequest>> for BatchLabelError {
    fn from(err: BatchLookupError<BitwardenAkdLabelMaterialRequest>) -> Self {
        match err {
            BatchLookupError::Connection(s) => BatchLabelError::Connection { message: s },
            BatchLookupError::Protocol(s) => BatchLabelError::Protocol { message: s },
            BatchLookupError::EpochNotAudited { epoch } => {
                BatchLabelError::EpochNotAudited { epoch }
            }
            BatchLookupError::PerItem { verified, failed } => BatchLabelError::PerItem {
                verified: verified
                    .into_iter()
                    .map(|v| VerifiedLabelItem {
                        input: v.input,
                        value: v.value,
                    })
                    .collect(),
                failed: failed
                    .into_iter()
                    .map(|f| FailedLabelItem {
                        input: f.input,
                        proof_error: f.proof_error,
                    })
                    .collect(),
            },
        }
    }
}

/// AKD verifier exposed to Swift/Kotlin via uniffi. Synchronous wrapper over
/// the async [`Core`]; each method blocks on an internal tokio runtime.
#[derive(::uniffi::Object)]
pub struct AkdVerifier {
    inner: Core,
    runtime: Arc<tokio::runtime::Runtime>,
}

#[::uniffi::export]
impl AkdVerifier {
    /// See [`Core::new`].
    #[uniffi::constructor]
    pub fn new(
        reader_url: String,
        watch_url: String,
        watch_namespace: String,
        installation_id: Uuid,
    ) -> Result<Self, LookupError> {
        let inner = Core::new(reader_url, watch_url, watch_namespace, installation_id)
            .map_err(|e| LookupError::Protocol(e.to_string()))?;
        let runtime = tokio::runtime::Runtime::new()
            .map_err(|e| LookupError::Protocol(format!("Failed to create async runtime: {e}")))?;
        Ok(Self {
            inner,
            runtime: Arc::new(runtime),
        })
    }

    /// See [`Core::verify_pair`].
    pub fn verify_pair(
        &self,
        pair: BitwardenAkdPairMaterialRequest,
    ) -> Result<VerifiedValue, VerifyError> {
        self.runtime.block_on(self.inner.verify_pair(pair))
    }

    /// See [`Core::verify_pairs`].
    pub fn verify_pairs(
        &self,
        pairs: Vec<BitwardenAkdPairMaterialRequest>,
    ) -> Result<Vec<VerifiedValue>, BatchPairError> {
        self.runtime
            .block_on(self.inner.verify_pairs(pairs))
            .map_err(BatchPairError::from)
    }

    /// See [`Core::lookup`].
    pub fn lookup(
        &self,
        label: BitwardenAkdLabelMaterialRequest,
    ) -> Result<VerifiedValue, LookupError> {
        self.runtime.block_on(self.inner.lookup(label))
    }

    /// See [`Core::lookup_batch`].
    pub fn lookup_batch(
        &self,
        labels: Vec<BitwardenAkdLabelMaterialRequest>,
    ) -> Result<Vec<VerifiedValue>, BatchLabelError> {
        self.runtime
            .block_on(self.inner.lookup_batch(labels))
            .map_err(BatchLabelError::from)
    }

    /// See [`Core::lookup_history`].
    pub fn lookup_history(
        &self,
        label: BitwardenAkdLabelMaterialRequest,
    ) -> Result<Vec<VerifiedValue>, LookupError> {
        self.runtime.block_on(self.inner.lookup_history(label))
    }

    /// See [`Core::audited`].
    pub fn audited(&self, epoch: u64) -> Result<bool, TransportError> {
        self.runtime.block_on(self.inner.audited(epoch))
    }
}
