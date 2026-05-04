use std::sync::OnceLock;

use akd::hash::DIGEST_BYTES;
use bitwarden_akd_configuration::wire_models::{
    BitwardenAkdLabelMaterialRequest, BitwardenAkdPairMaterialRequest,
};
use uuid::Uuid;

use crate::error::{
    BatchLabelError, BatchLookupError, BatchPairError, BatchVerifyError, FailedLookupItem,
    FailedVerifyItem, InvalidUrl, LookupError, TransportError, VerifiedItem, VerifyError,
    VerifyItemError,
};
use crate::models::{
    AuditSignatureResponse, BatchLookupData, BatchLookupRequest, EpochData, HistoryData,
    HistoryParams, KeyHistoryRequest, Response, VerifiedValue,
};
use crate::verify;

/// Client-side AKD verifier. See the [crate-level docs](crate) for an
/// overview.
///
/// One instance is bound to a single (reader, akd-watch namespace,
/// installation) tuple; create separate instances for separate directories.
pub struct AkdVerifier {
    http: reqwest::Client,
    reader_url: String,
    watch_url: String,
    watch_namespace: String,
    installation_id: Uuid,
    vrf_public_key: OnceLock<Vec<u8>>,
}

impl AkdVerifier {
    /// Create a new AKD client.
    ///
    /// `reader_url`: Base URL of the AKD reader API (e.g. `http://localhost:3001`)
    /// `watch_url`: Base URL of the akd-watch API (e.g. `http://localhost:3000`)
    /// `watch_namespace`: The akd-watch namespace to query for audit status
    /// `installation_id`: Bitwarden installation ID for configuration context binding
    ///
    /// # Errors
    /// Returns [`InvalidUrl`] if either URL is malformed, uses a non-http(s)
    /// scheme, or if `watch_namespace` contains characters outside
    /// `[A-Za-z0-9_-]`.
    pub fn new(
        reader_url: String,
        watch_url: String,
        watch_namespace: String,
        installation_id: Uuid,
    ) -> Result<Self, InvalidUrl> {
        Ok(Self {
            http: reqwest::Client::new(),
            reader_url: validate_base_url(reader_url, "reader_url")?,
            watch_url: validate_base_url(watch_url, "watch_url")?,
            watch_namespace: validate_namespace(watch_namespace)?,
            installation_id,
            vrf_public_key: OnceLock::new(),
        })
    }

    /// Verify a known (label, value) pair. See [verify_pairs](`AkdVerifier::verify_pairs`).
    pub async fn verify_pair(
        &self,
        pair: BitwardenAkdPairMaterialRequest,
    ) -> Result<VerifiedValue, VerifyError> {
        match self.verify_pairs(vec![pair]).await {
            Ok(mut v) => v
                .pop()
                .ok_or_else(|| VerifyError::Protocol("Empty batch result".into())),
            Err(BatchVerifyError::Connection(s)) => Err(VerifyError::Connection(s)),
            Err(BatchVerifyError::Protocol(s)) => Err(VerifyError::Protocol(s)),
            Err(BatchVerifyError::EpochNotAudited { epoch }) => {
                Err(VerifyError::EpochNotAudited { epoch })
            }
            Err(BatchVerifyError::PerItem { mut failed, .. }) => {
                let one = failed.pop().ok_or_else(|| {
                    VerifyError::Protocol("PerItem with no failures from single-item batch".into())
                })?;
                Err(one.error.into())
            }
        }
    }

    /// Verify a batch of `(label, value)` pairs in one round trip.
    ///
    /// On full success, returns the verified values in input order.
    /// Otherwise [`BatchVerifyError`] distinguishes request-wide failures
    /// from a partial-success [`BatchVerifyError::PerItem`] ledger that pairs
    /// each input with its outcome.
    ///
    /// If the proof's epoch is not yet signed by the configured auditor,
    /// returns [`BatchVerifyError::EpochNotAudited`] for the entire batch —
    /// poll [`AkdVerifier::audited`] to wait.
    pub async fn verify_pairs(
        &self,
        pairs: Vec<BitwardenAkdPairMaterialRequest>,
    ) -> Result<Vec<VerifiedValue>, BatchPairError> {
        let mut labels: Vec<BitwardenAkdLabelMaterialRequest> = Vec::with_capacity(pairs.len());
        let mut expected_values: Vec<akd::AkdValue> = Vec::with_capacity(pairs.len());
        for pair in &pairs {
            labels.push(pair.into());
            expected_values.push(pair.clone().into());
        }
        let run = self.run_batch_proofs(pairs, labels).await?;
        if !run.audited {
            return Err(BatchVerifyError::EpochNotAudited {
                epoch: run.current_epoch,
            });
        }

        let mut verified: Vec<VerifiedItem<BitwardenAkdPairMaterialRequest>> = Vec::new();
        let mut failed: Vec<FailedVerifyItem<BitwardenAkdPairMaterialRequest>> = Vec::new();
        for (i, (input, r)) in run.items.into_iter().enumerate() {
            match r {
                Ok(vr) => {
                    let server_value = vr.value.0;
                    if expected_values[i].0 != server_value {
                        failed.push(FailedVerifyItem {
                            input,
                            error: VerifyItemError::ValueMismatch {
                                server_epoch: vr.epoch,
                                server_version: vr.version,
                                server_value,
                            },
                        });
                    } else {
                        verified.push(VerifiedItem {
                            input,
                            value: VerifiedValue {
                                epoch: vr.epoch,
                                version: vr.version,
                                value: server_value,
                            },
                        });
                    }
                }
                Err(e) => {
                    failed.push(FailedVerifyItem {
                        input,
                        error: VerifyItemError::ProofInvalid(e.to_string()),
                    });
                }
            }
        }

        if failed.is_empty() {
            Ok(verified.into_iter().map(|v| v.value).collect())
        } else {
            Err(BatchVerifyError::PerItem { verified, failed })
        }
    }

    /// Look up the current value for a label. See [lookup_batch](`AkdVerifier::lookup_batch`).
    pub async fn lookup(
        &self,
        label: BitwardenAkdLabelMaterialRequest,
    ) -> Result<VerifiedValue, LookupError> {
        match self.lookup_batch(vec![label]).await {
            Ok(mut v) => v
                .pop()
                .ok_or_else(|| LookupError::Protocol("Empty batch result".into())),
            Err(BatchLookupError::Connection(s)) => Err(LookupError::Connection(s)),
            Err(BatchLookupError::Protocol(s)) => Err(LookupError::Protocol(s)),
            Err(BatchLookupError::EpochNotAudited { epoch }) => {
                Err(LookupError::EpochNotAudited { epoch })
            }
            Err(BatchLookupError::PerItem { mut failed, .. }) => {
                let one = failed.pop().ok_or_else(|| {
                    LookupError::Protocol("PerItem with no failures from single-item batch".into())
                })?;
                Err(LookupError::ProofInvalid(one.proof_error))
            }
        }
    }

    /// Look up the current values for a batch of labels in one round trip.
    ///
    /// Each returned `version` is the most recent committed for that label at
    /// the proof's epoch — the AKD lookup proof additionally proves no
    /// `version+1` exists.
    ///
    /// Same audit semantics as [`verify_pairs`](AkdVerifier::verify_pairs).
    pub async fn lookup_batch(
        &self,
        labels: Vec<BitwardenAkdLabelMaterialRequest>,
    ) -> Result<Vec<VerifiedValue>, BatchLabelError> {
        let run = self.run_batch_proofs(labels.clone(), labels).await?;
        if !run.audited {
            return Err(BatchLookupError::EpochNotAudited {
                epoch: run.current_epoch,
            });
        }

        let mut verified: Vec<VerifiedItem<BitwardenAkdLabelMaterialRequest>> = Vec::new();
        let mut failed: Vec<FailedLookupItem<BitwardenAkdLabelMaterialRequest>> = Vec::new();
        for (input, r) in run.items {
            match r {
                Ok(vr) => verified.push(VerifiedItem {
                    input,
                    value: VerifiedValue {
                        epoch: vr.epoch,
                        version: vr.version,
                        value: vr.value.0,
                    },
                }),
                Err(e) => failed.push(FailedLookupItem {
                    input,
                    proof_error: e.to_string(),
                }),
            }
        }

        if failed.is_empty() {
            Ok(verified.into_iter().map(|v| v.value).collect())
        } else {
            Err(BatchLookupError::PerItem { verified, failed })
        }
    }

    /// Return the full version history for a label.
    ///
    /// Versions are ordered most-recent first. The proof additionally shows
    /// no further versions exist beyond the highest returned one.
    ///
    /// Returns [`LookupError::EpochNotAudited`] if the proof's epoch is not
    /// yet signed by the auditor — poll [`AkdVerifier::audited`] to wait.
    pub async fn lookup_history(
        &self,
        label: BitwardenAkdLabelMaterialRequest,
    ) -> Result<Vec<VerifiedValue>, LookupError> {
        let vrf_pk = self.vrf_public_key().await?;
        let url = format!("{}/key_history", self.reader_url);
        let body = KeyHistoryRequest {
            bitwarden_akd_label_material: label.clone(),
            history_params: HistoryParams::Complete,
        };
        let history_data: HistoryData = http_post_json(&self.http, &url, &body).await?;
        let root_hash = epoch_data_to_digest(&history_data.epoch_data)?;
        let current_epoch = history_data.epoch_data.epoch;

        let akd_label: akd::AkdLabel = label.into();
        let installation_id = self.installation_id;
        let history_proof = history_data.history_proof;

        // Overlap the audit HTTP call with local proof verification CPU work.
        let audit_fut = self.audited(current_epoch);
        let verify_fut = async {
            verify::key_history_verify(
                installation_id,
                vrf_pk,
                root_hash,
                current_epoch,
                akd_label,
                history_proof,
                akd::client::HistoryVerificationParams::Default {
                    history_params: akd::HistoryParams::Complete,
                },
            )
        };
        let (audit_result, verify_result) = futures::join!(audit_fut, verify_fut);
        if !audit_result? {
            return Err(LookupError::EpochNotAudited {
                epoch: current_epoch,
            });
        }
        let verify_results = verify_result.map_err(|e| LookupError::ProofInvalid(e.to_string()))?;

        Ok(verify_results
            .into_iter()
            .map(|vr| VerifiedValue {
                epoch: vr.epoch,
                version: vr.version,
                value: vr.value.0,
            })
            .collect())
    }

    /// Lazily fetch and cache the VRF public key from the reader.
    async fn vrf_public_key(&self) -> Result<&[u8], TransportError> {
        if let Some(pk) = self.vrf_public_key.get() {
            return Ok(pk.as_slice());
        }

        let url = format!("{}/public_key", self.reader_url);
        let pk_b64: bitwarden_encoding::B64 = http_get_json(&self.http, &url).await?;
        let pk_bytes = pk_b64.into_bytes();

        // OnceLock::set may fail if another task raced us, but that's fine —
        // both would have fetched the same key.
        let _ = self.vrf_public_key.set(pk_bytes);
        Ok(self.vrf_public_key.get().expect("just set").as_slice())
    }

    /// Check whether an epoch has been signed by akd-watch.
    ///
    /// Poll this after any `EpochNotAudited` outcome until `Ok(true)`, then retry.
    ///
    /// Existence-only in v1: the signature is not yet verified
    ///
    /// TODO:
    ///  - signature verification
    ///  - root hash comparison
    pub async fn audited(&self, epoch: u64) -> Result<bool, TransportError> {
        let url = format!(
            "{}/namespaces/{}/audits/{}",
            self.watch_url, self.watch_namespace, epoch
        );
        let resp = self.http.get(&url).send().await?;

        if !resp.status().is_success() {
            let status = resp.status();
            let body = resp.text().await.unwrap_or_default();
            return Err(TransportError::Connection(format!("[{status}] {body}")));
        }

        let audit: Option<AuditSignatureResponse> = resp.json().await?;
        Ok(audit.is_some())
    }

    /// Fetch batch proofs, run the audit gate, and verify each proof. Shared
    /// by `verify_pairs` and `lookup_batch`; each public method consumes the
    /// returned [`BatchProofRun`] to construct its own per-item failure shape.
    ///
    /// Errors during proof fetching surface as [`TransportError`] (which the
    /// callers' return types absorb via their `From<TransportError>` impls).
    async fn run_batch_proofs<I>(
        &self,
        inputs: Vec<I>,
        labels: Vec<BitwardenAkdLabelMaterialRequest>,
    ) -> Result<BatchProofRun<I>, TransportError> {
        let vrf_pk = self.vrf_public_key().await?;
        let url = format!("{}/batch_lookup", self.reader_url);
        let body = BatchLookupRequest {
            bitwarden_akd_labels: labels.clone(),
        };
        let batch_data: BatchLookupData = http_post_json(&self.http, &url, &body).await?;
        let root_hash = epoch_data_to_digest(&batch_data.current_epoch_data)?;
        let current_epoch = batch_data.current_epoch_data.epoch;

        if labels.len() != batch_data.lookup_proofs.len() {
            return Err(TransportError::Protocol(format!(
                "Expected {} proofs, got {}",
                labels.len(),
                batch_data.lookup_proofs.len()
            )));
        }

        let installation_id = self.installation_id;
        let lookup_proofs = batch_data.lookup_proofs;

        // Overlap the audit HTTP call with local proof verification CPU work.
        let audit_fut = self.audited(current_epoch);
        let verify_fut = async {
            let akd_labels: Vec<akd::AkdLabel> = labels.into_iter().map(Into::into).collect();
            let mut results = Vec::with_capacity(akd_labels.len());
            for (akd_label, proof) in akd_labels.into_iter().zip(lookup_proofs.into_iter()) {
                results.push(verify::lookup_verify(
                    installation_id,
                    vrf_pk,
                    root_hash,
                    current_epoch,
                    akd_label,
                    proof,
                ));
            }
            results
        };

        let (audit_result, verify_results) = futures::join!(audit_fut, verify_fut);
        Ok(BatchProofRun {
            items: inputs.into_iter().zip(verify_results.into_iter()).collect(),
            audited: audit_result?,
            current_epoch,
        })
    }
}

/// per-input verification result, plus the audit verdict for the proofs' anchor epoch.
struct BatchProofRun<I> {
    items: Vec<(I, Result<akd::VerifyResult, akd::verify::VerificationError>)>,
    audited: bool,
    current_epoch: u64,
}

/// POST a JSON body and parse the reader's `Response<R>` envelope, returning
/// the unwrapped data or a [`TransportError`].
async fn http_post_json<B, R>(
    client: &reqwest::Client,
    url: &str,
    body: &B,
) -> Result<R, TransportError>
where
    B: serde::Serialize + ?Sized,
    R: serde::de::DeserializeOwned,
{
    let resp: Response<R> = client.post(url).json(body).send().await?.json().await?;
    unwrap_envelope(resp)
}

/// GET a JSON response wrapped in the reader's `Response<R>` envelope.
async fn http_get_json<R>(client: &reqwest::Client, url: &str) -> Result<R, TransportError>
where
    R: serde::de::DeserializeOwned,
{
    let resp: Response<R> = client.get(url).send().await?.json().await?;
    unwrap_envelope(resp)
}

/// Unwrap the reader's `Response<T>` envelope.
///
/// The envelope has three fields (`success`, `data`, `error`) and the canonical
/// shapes are `success=true + data=Some + error=None` or `success=false +
/// error=Some`. An explicit `error` payload is honored regardless of the
/// `success` flag — if the server provided one, the operation didn't return
/// usable data. Other inconsistent combinations surface as [`Protocol`] errors.
///
/// [`Protocol`]: TransportError::Protocol
fn unwrap_envelope<T>(resp: Response<T>) -> Result<T, TransportError> {
    if let Some(err) = resp.error {
        return Err(err.into());
    }
    if !resp.success {
        return Err(TransportError::Protocol(
            "Response success=false but no error payload".into(),
        ));
    }
    resp.data
        .ok_or_else(|| TransportError::Protocol("Response success=true but no data".into()))
}

/// Validate a base URL: must parse, must be http or https, trailing slash stripped.
fn validate_base_url(url: String, field: &'static str) -> Result<String, InvalidUrl> {
    let parsed = reqwest::Url::parse(&url).map_err(|e| InvalidUrl {
        field,
        reason: e.to_string(),
    })?;
    match parsed.scheme() {
        "http" | "https" => {}
        scheme => {
            return Err(InvalidUrl {
                field,
                reason: format!("must use http or https, got {scheme}"),
            });
        }
    }
    Ok(url.trim_end_matches('/').to_owned())
}

/// Validate a watch namespace: non-empty, ASCII alphanumeric plus `-` / `_`.
/// Rejects path separators and `..` so the namespace cannot traverse the URL
/// path.
fn validate_namespace(ns: String) -> Result<String, InvalidUrl> {
    if ns.is_empty() {
        return Err(InvalidUrl {
            field: "watch_namespace",
            reason: "must not be empty".into(),
        });
    }
    if !ns
        .chars()
        .all(|c| c.is_ascii_alphanumeric() || c == '_' || c == '-')
    {
        return Err(InvalidUrl {
            field: "watch_namespace",
            reason: "must contain only ASCII alphanumeric characters, '-', or '_'".into(),
        });
    }
    Ok(ns)
}

/// Convert the B64-encoded epoch hash to a fixed-size `Digest`.
fn epoch_data_to_digest(epoch_data: &EpochData) -> Result<akd::Digest, TransportError> {
    let bytes = epoch_data.epoch_hash_b64.as_bytes();
    let digest: [u8; DIGEST_BYTES] = bytes.try_into().map_err(|_| {
        TransportError::Protocol(format!(
            "Epoch hash has wrong length: expected {DIGEST_BYTES}, got {}",
            bytes.len()
        ))
    })?;
    Ok(digest)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn validate_base_url_strips_trailing_slash() {
        let result = validate_base_url("http://localhost:3001/".to_string(), "reader_url")
            .expect("valid url");
        assert_eq!(result, "http://localhost:3001");
    }

    #[test]
    fn validate_base_url_rejects_non_http() {
        let err =
            validate_base_url("ftp://example.com".to_string(), "reader_url").expect_err("rejects");
        assert!(err.reason.contains("http or https"));
    }

    #[test]
    fn validate_namespace_accepts_alphanumeric() {
        assert_eq!(
            validate_namespace("prod-v1_2".to_string()).expect("valid namespace"),
            "prod-v1_2"
        );
    }

    #[test]
    fn validate_namespace_rejects_empty() {
        let err = validate_namespace(String::new()).expect_err("rejects empty");
        assert!(err.reason.contains("empty"));
    }

    #[test]
    fn validate_namespace_rejects_path_separator() {
        let err = validate_namespace("foo/bar".to_string()).expect_err("rejects /");
        assert_eq!(err.field, "watch_namespace");
    }

    #[test]
    fn validate_namespace_rejects_dot_dot() {
        let err = validate_namespace("..".to_string()).expect_err("rejects ..");
        assert_eq!(err.field, "watch_namespace");
    }

    #[test]
    fn unwrap_envelope_returns_data_on_success() {
        let resp = Response::<u32> {
            success: true,
            data: Some(42),
            error: None,
        };
        assert_eq!(unwrap_envelope(resp).expect("data present"), 42);
    }

    #[test]
    fn unwrap_envelope_surfaces_error_payload() {
        let resp = Response::<u32> {
            success: false,
            data: None,
            error: Some(crate::models::ErrorResponse {
                code: "500".into(),
                message: "nope".into(),
            }),
        };
        let err = unwrap_envelope(resp).expect_err("returns Err");
        assert!(matches!(err, TransportError::Connection(_)));
    }

    #[test]
    fn unwrap_envelope_success_true_with_no_data_is_protocol_error() {
        let resp = Response::<u32> {
            success: true,
            data: None,
            error: None,
        };
        let err = unwrap_envelope(resp).expect_err("returns Err");
        assert!(matches!(err, TransportError::Protocol(_)));
    }

    #[test]
    fn unwrap_envelope_success_false_with_no_error_is_protocol_error() {
        let resp = Response::<u32> {
            success: false,
            data: None,
            error: None,
        };
        let err = unwrap_envelope(resp).expect_err("returns Err");
        let TransportError::Protocol(msg) = err else {
            panic!("wrong variant");
        };
        assert!(msg.contains("no error payload"), "got: {msg}");
    }

    #[test]
    fn unwrap_envelope_error_payload_wins_over_success_flag() {
        // Server contradicts itself: success=true but error present. Trust the
        // explicit error.
        let resp = Response::<u32> {
            success: true,
            data: Some(42),
            error: Some(crate::models::ErrorResponse {
                code: "500".into(),
                message: "nope".into(),
            }),
        };
        let err = unwrap_envelope(resp).expect_err("returns Err");
        assert!(matches!(err, TransportError::Connection(_)));
    }

    #[test]
    fn epoch_data_to_digest_rejects_wrong_length() {
        let epoch_data = EpochData {
            epoch: 1,
            epoch_hash_b64: "AA==".parse().expect("valid b64"), // 1 byte, not 32
        };
        let err = epoch_data_to_digest(&epoch_data).expect_err("returns Err");
        let TransportError::Protocol(msg) = err else {
            panic!("wrong variant");
        };
        assert!(msg.contains("wrong length"), "got: {msg}");
    }
}
