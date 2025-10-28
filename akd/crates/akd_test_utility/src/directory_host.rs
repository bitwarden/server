use akd::configuration::Configuration;
use akd::ecvrf::VRFKeyStorage;
use akd::storage::Database;
use akd::HistoryParams;
use akd::{AkdLabel, AkdValue};
use akd::{Directory, EpochHash};
use tokio::sync::mpsc::*;
use tokio::time::Instant;
use tracing::{error, info};

pub(crate) struct Rpc(
    pub(crate) DirectoryCommand,
    pub(crate) Option<tokio::sync::oneshot::Sender<Result<String, String>>>,
);

#[derive(Debug)]
pub enum DirectoryCommand {
    Publish(String, String),
    PublishBatch(Vec<(String, String)>),
    Lookup(String),
    KeyHistory(String),
    Audit(u64, u64),
    RootHash,
    Terminate,
}

pub(crate) async fn init_host<TC, S, V>(rx: &mut Receiver<Rpc>, directory: &mut Directory<TC, S, V>)
where
    TC: Configuration,
    S: Database + 'static,
    V: VRFKeyStorage,
{
    info!("Starting the verifiable directory host");

    while let Some(Rpc(message, channel)) = rx.recv().await {
        match (message, channel) {
            (DirectoryCommand::Terminate, _) => {
                break;
            }
            (DirectoryCommand::Publish(a, b), Some(response)) => {
                let tic = Instant::now();
                match directory
                    .publish(vec![(AkdLabel::from(&a), AkdValue::from(&b))])
                    .await
                {
                    Ok(EpochHash(epoch, hash)) => {
                        let toc = Instant::now() - tic;
                        let msg = format!(
                            "PUBLISHED '{}' = '{}' in {} s (epoch: {}, root hash: {})",
                            a,
                            b,
                            toc.as_secs_f64(),
                            epoch,
                            hex::encode(hash)
                        );
                        let _ = response.send(Ok(msg));
                    }
                    Err(error) => {
                        let msg = format!("Failed to publish with error: {error:?}");
                        let _ = response.send(Err(msg));
                    }
                }
            }
            (DirectoryCommand::PublishBatch(batches), Some(response)) => {
                let tic = Instant::now();
                let len = batches.len();
                match directory
                    .publish(
                        batches
                            .into_iter()
                            .map(|(key, value)| (AkdLabel::from(&key), AkdValue::from(&value)))
                            .collect(),
                    )
                    .await
                {
                    Ok(_) => {
                        let toc = Instant::now() - tic;
                        let msg = format!("PUBLISHED {} records in {} s", len, toc.as_secs_f64());
                        let _ = response.send(Ok(msg));
                    }
                    Err(error) => {
                        let msg = format!("Failed to publish with error: {error:?}");
                        let _ = response.send(Err(msg));
                    }
                }
            }
            (DirectoryCommand::Lookup(a), Some(response)) => {
                match directory.lookup(AkdLabel::from(&a)).await {
                    Ok((proof, root_hash)) => {
                        let vrf_pk = match directory.get_public_key().await {
                            Ok(pk) => pk,
                            Err(error) => {
                                let msg = format!("Failed to get public key: {error:?}");
                                let _ = response.send(Err(msg));
                                continue;
                            }
                        };
                        let verification = akd::client::lookup_verify::<TC>(
                            vrf_pk.as_bytes(),
                            root_hash.hash(),
                            root_hash.epoch(),
                            AkdLabel::from(&a),
                            proof,
                        );
                        match verification {
                            Err(error) => {
                                let msg = format!("WARN: Lookup proof failed verification for '{a}': {error:?}");
                                let _ = response.send(Err(msg));
                            }
                            Ok(result) => {
                                let value_hex = hex::encode(result.value.as_slice());
                                let value_str = String::from_utf8(result.value.0.clone())
                                    .unwrap_or_else(|_| format!("<binary: {}>", value_hex));
                                let msg = format!(
                                    "Lookup verified for '{a}'\n  Epoch: {}\n  Version: {}\n  Value: {}",
                                    result.epoch, result.version, value_str
                                );
                                let _ = response.send(Ok(msg));
                            }
                        }
                    }
                    Err(error) => {
                        let msg = format!("Failed to lookup with error {error:?}");
                        let _ = response.send(Err(msg));
                    }
                }
            }
            (DirectoryCommand::KeyHistory(a), Some(response)) => {
                match directory
                    .key_history(&AkdLabel::from(&a), HistoryParams::default())
                    .await
                {
                    Ok((proof, _root_hash)) => {
                        let num_updates = proof.update_proofs.len();
                        if num_updates == 0 {
                            let msg = format!("Key history for '{a}': No updates found");
                            let _ = response.send(Ok(msg));
                        } else {
                            let mut msg = format!("Key history for '{a}': {} update(s)\n", num_updates);
                            for (i, update) in proof.update_proofs.iter().enumerate() {
                                let value_hex = hex::encode(update.value.as_slice());
                                let value_str = String::from_utf8(update.value.0.clone())
                                    .unwrap_or_else(|_| format!("<binary: {}>", value_hex));
                                msg.push_str(&format!(
                                    "  [{}] Epoch: {}, Version: {}, Value: {}\n",
                                    i + 1, update.epoch, update.version, value_str
                                ));
                            }
                            let _ = response.send(Ok(msg.trim_end().to_string()));
                        }
                    }
                    Err(error) => {
                        let msg = format!("Failed to get key history with error {error:?}");
                        let _ = response.send(Err(msg));
                    }
                }
            }
            (DirectoryCommand::Audit(start, end), Some(response)) => {
                match directory.audit(start, end).await {
                    Ok(_proof) => {
                        let msg = format!("GOT AUDIT PROOF BETWEEN ({start}, {end})");
                        let _ = response.send(Ok(msg));
                    }
                    Err(error) => {
                        let msg = format!("Failed to get audit proof with error {error:?}");
                        let _ = response.send(Err(msg));
                    }
                }
            }
            (DirectoryCommand::RootHash, Some(response)) => {
                let hash = directory.get_epoch_hash().await;
                match hash {
                    Ok(EpochHash(_, hash)) => {
                        let msg = format!("Retrieved root hash {}", hex::encode(hash));
                        let _ = response.send(Ok(msg));
                    }
                    Err(error) => {
                        let msg = format!("Failed to retrieve root hash with error {error:?}");
                        let _ = response.send(Err(msg));
                    }
                }
            }
            (_, None) => {
                error!("A channel was not provided to the directory server to process a command!");
            }
        }
    }

    info!("AKD host shutting down");
}
