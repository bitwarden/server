use bitwarden_akd_configuration::wire_models::{
    BitwardenAkdLabelMaterialRequest, BitwardenAkdPairMaterialRequest,
};

use crate::models::VerifiedValue;

/// Error constructing an [`AkdVerifier`](crate::verifier::AkdVerifier).
#[derive(Debug, thiserror::Error)]
#[error("Invalid {field}: {reason}")]
pub struct InvalidUrl {
    pub field: &'static str,
    pub reason: String,
}

/// Reader / akd-watch transport failures. Lifted into the `Connection` /
/// `Protocol` variants of the public per-method error enums; surfaced
/// directly by [`AkdVerifier::audited`](crate::verifier::AkdVerifier::audited).
#[cfg_attr(feature = "uniffi", derive(uniffi::Error))]
#[derive(Debug, thiserror::Error)]
pub enum TransportError {
    /// Network failure or non-2xx response without a recognizable envelope.
    #[error("{0}")]
    Connection(String),

    /// Server response did not match the expected envelope or shape.
    #[error("Unexpected response: {0}")]
    Protocol(String),
}

impl From<reqwest::Error> for TransportError {
    fn from(err: reqwest::Error) -> Self {
        TransportError::Connection(err.to_string())
    }
}

impl From<crate::models::ErrorResponse> for TransportError {
    fn from(err: crate::models::ErrorResponse) -> Self {
        TransportError::Connection(format!("[{}] {}", err.code, err.message))
    }
}

/// Outcome from `lookup` / `lookup_history`.
#[cfg_attr(feature = "uniffi", derive(uniffi::Error))]
#[derive(Debug, thiserror::Error)]
pub enum LookupError {
    /// The proof did not verify against its root hash.
    #[error("Proof verification failed: {0}")]
    ProofInvalid(String),

    /// The proof's epoch has not yet been signed by the configured auditor.
    #[error("Epoch {epoch} has not been audited yet")]
    EpochNotAudited { epoch: u64 },

    /// Transport-level failure (see [`TransportError::Connection`]).
    #[error("{0}")]
    Connection(String),

    /// Server response did not match the expected shape (see
    /// [`TransportError::Protocol`]).
    #[error("Unexpected response: {0}")]
    Protocol(String),
}

impl From<TransportError> for LookupError {
    fn from(err: TransportError) -> Self {
        match err {
            TransportError::Connection(s) => LookupError::Connection(s),
            TransportError::Protocol(s) => LookupError::Protocol(s),
        }
    }
}

/// Outcome from `verify_pair`. Strict superset of [`LookupError`] —
/// `verify_pair` performs a lookup plus a value comparison, so it adds
/// [`ValueMismatch`](VerifyError::ValueMismatch).
#[cfg_attr(feature = "uniffi", derive(uniffi::Error))]
#[derive(Debug, thiserror::Error)]
pub enum VerifyError {
    /// The proof did not verify against its root hash.
    #[error("Proof verification failed: {0}")]
    ProofInvalid(String),

    /// The server-committed value differs from the caller-supplied value.
    /// `server_*` fields describe what the server returned.
    #[error("Value mismatch at epoch {server_epoch}, version {server_version}")]
    ValueMismatch {
        server_epoch: u64,
        server_version: u64,
        server_value: Vec<u8>,
    },

    /// The proof's epoch has not yet been signed by the configured auditor.
    #[error("Epoch {epoch} has not been audited yet")]
    EpochNotAudited { epoch: u64 },

    /// Transport-level failure (see [`TransportError::Connection`]).
    #[error("{0}")]
    Connection(String),

    /// Server response did not match the expected shape (see
    /// [`TransportError::Protocol`]).
    #[error("Unexpected response: {0}")]
    Protocol(String),
}

impl From<TransportError> for VerifyError {
    fn from(err: TransportError) -> Self {
        match err {
            TransportError::Connection(s) => VerifyError::Connection(s),
            TransportError::Protocol(s) => VerifyError::Protocol(s),
        }
    }
}

impl From<LookupError> for VerifyError {
    fn from(err: LookupError) -> Self {
        match err {
            LookupError::ProofInvalid(msg) => VerifyError::ProofInvalid(msg),
            LookupError::EpochNotAudited { epoch } => VerifyError::EpochNotAudited { epoch },
            LookupError::Connection(s) => VerifyError::Connection(s),
            LookupError::Protocol(s) => VerifyError::Protocol(s),
        }
    }
}

impl From<VerifyItemError> for VerifyError {
    fn from(err: VerifyItemError) -> Self {
        match err {
            VerifyItemError::ProofInvalid(msg) => VerifyError::ProofInvalid(msg),
            VerifyItemError::ValueMismatch {
                server_epoch,
                server_version,
                server_value,
            } => VerifyError::ValueMismatch {
                server_epoch,
                server_version,
                server_value,
            },
        }
    }
}

/// Per-item failure within a `verify_pairs` batch.
#[cfg_attr(feature = "uniffi", derive(uniffi::Error))]
#[derive(Debug, Clone, thiserror::Error)]
pub enum VerifyItemError {
    /// The proof did not verify.
    #[error("Proof verification failed: {0}")]
    ProofInvalid(String),

    /// The server-committed value differs from the caller-supplied value.
    #[error("Value mismatch at epoch {server_epoch}, version {server_version}")]
    ValueMismatch {
        server_epoch: u64,
        server_version: u64,
        server_value: Vec<u8>,
    },
}

/// A verified batch entry paired with its input.
#[derive(Debug, Clone)]
pub struct VerifiedItem<I> {
    pub input: I,
    pub value: VerifiedValue,
}

/// A failed `verify_pairs` entry paired with its input.
#[derive(Debug, Clone)]
pub struct FailedVerifyItem<I> {
    pub input: I,
    pub error: VerifyItemError,
}

/// A failed `lookup_batch` entry paired with its input. Only proof-verification
/// failure is reachable here, so the failure carries a plain message.
#[derive(Debug, Clone)]
pub struct FailedLookupItem<I> {
    pub input: I,
    pub proof_error: String,
}

/// Outcome from `verify_pairs`.
///
/// `Connection` / `Protocol` / `EpochNotAudited` are request-wide failures.
/// `PerItem` carries a partial-success ledger when some items verified and
/// others didn't.
#[derive(Debug, thiserror::Error)]
pub enum BatchVerifyError<I> {
    /// Transport-level failure (see [`TransportError::Connection`]).
    #[error("{0}")]
    Connection(String),

    /// Server response did not match the expected shape (see
    /// [`TransportError::Protocol`]).
    #[error("Unexpected response: {0}")]
    Protocol(String),

    /// The proof's epoch has not yet been signed by the configured auditor.
    #[error("Epoch {epoch} has not been audited yet")]
    EpochNotAudited { epoch: u64 },

    /// Some items verified, some did not. `verified` and `failed` together
    /// cover every input.
    #[error("Batch had {} per-item failures", failed.len())]
    PerItem {
        verified: Vec<VerifiedItem<I>>,
        failed: Vec<FailedVerifyItem<I>>,
    },
}

impl<I> From<TransportError> for BatchVerifyError<I> {
    fn from(err: TransportError) -> Self {
        match err {
            TransportError::Connection(s) => BatchVerifyError::Connection(s),
            TransportError::Protocol(s) => BatchVerifyError::Protocol(s),
        }
    }
}

/// Outcome from `lookup_batch`.
///
/// Same request-level shape as [`BatchVerifyError`], but `PerItem.failed`
/// can only carry proof-verification failures (no `ValueMismatch`, since
/// `lookup_batch` doesn't compare against caller-supplied values).
#[derive(Debug, thiserror::Error)]
pub enum BatchLookupError<I> {
    /// Transport-level failure (see [`TransportError::Connection`]).
    #[error("{0}")]
    Connection(String),

    /// Server response did not match the expected shape (see
    /// [`TransportError::Protocol`]).
    #[error("Unexpected response: {0}")]
    Protocol(String),

    /// The proof's epoch has not yet been signed by the configured auditor.
    #[error("Epoch {epoch} has not been audited yet")]
    EpochNotAudited { epoch: u64 },

    /// Some items verified, some did not. `verified` and `failed` together
    /// cover every input.
    #[error("Batch had {} per-item failures", failed.len())]
    PerItem {
        verified: Vec<VerifiedItem<I>>,
        failed: Vec<FailedLookupItem<I>>,
    },
}

impl<I> From<TransportError> for BatchLookupError<I> {
    fn from(err: TransportError) -> Self {
        match err {
            TransportError::Connection(s) => BatchLookupError::Connection(s),
            TransportError::Protocol(s) => BatchLookupError::Protocol(s),
        }
    }
}

/// Ergonomic alias for `BatchVerifyError<BitwardenAkdPairMaterialRequest>`.
pub type BatchPairError = BatchVerifyError<BitwardenAkdPairMaterialRequest>;

/// Ergonomic alias for `BatchLookupError<BitwardenAkdLabelMaterialRequest>`.
pub type BatchLabelError = BatchLookupError<BitwardenAkdLabelMaterialRequest>;
