use akd::{directory::ReadOnlyDirectory, Directory};
use akd_storage::{AkdDatabase, VrfKeyDatabase};
use bitwarden_akd_configuration::BitwardenV1Configuration;

pub type BitAkdDirectory = Directory<BitwardenV1Configuration, AkdDatabase, VrfKeyDatabase>;
pub type ReadOnlyBitAkdDirectory =
    ReadOnlyDirectory<BitwardenV1Configuration, AkdDatabase, VrfKeyDatabase>;
