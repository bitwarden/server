use akd::{directory::ReadOnlyDirectory, Directory};
use akd_storage::{AkdDatabase, VrfKeyDatabase};
use bitwarden_akd_configuration::BitwardenV1Configuration;
use serde::{Deserialize, Serialize};

pub type BitAkdDirectory = Directory<BitwardenV1Configuration, AkdDatabase, VrfKeyDatabase>;
pub type ReadOnlyBitAkdDirectory =
    ReadOnlyDirectory<BitwardenV1Configuration, AkdDatabase, VrfKeyDatabase>;

#[derive(Debug, Serialize, Deserialize)]
pub struct AkdLabelB64(pub(crate) bitwarden_encoding::B64);

impl From<AkdLabelB64> for akd::AkdLabel {
    fn from(label_b64: AkdLabelB64) -> Self {
        akd::AkdLabel(label_b64.0.into_bytes())
    }
}

#[derive(Debug, Serialize, Deserialize)]
pub struct AkdValueB64(pub(crate) bitwarden_encoding::B64);

impl From<AkdValueB64> for akd::AkdValue {
    fn from(value_b64: AkdValueB64) -> Self {
        akd::AkdValue(value_b64.0.into_bytes())
    }
}
