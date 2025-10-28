use super::directory_host::DirectoryCommand;
use owo_colors::OwoColorize;

pub(crate) enum Command {
    Help,
    Exit,
    Clean,
    Info,
    Directory(DirectoryCommand),
    InvalidArgs(String),
    Unknown(String),
}

impl Command {
    pub(crate) fn parse(text: &mut String) -> Command {
        trim_newline(text);
        let parts: Vec<&str> = text.split(' ').collect();

        let mut command = String::new();
        if let Some(head) = parts.first() {
            command = String::from(*head);
        }

        match command.to_lowercase().as_ref() {
            "exit" | "x" => Command::Exit,
            "help" | "?" => Command::Help,
            "clean" => Command::Clean,
            "info" => Command::Info,
            cmd => Command::handle_dir_cmd(cmd, parts, text),
        }
    }

    pub(crate) fn print_help_menu() {
        println!("{}", "*************************** Help menu ***************************".red());
        println!(
            "{} are commands, {} are mandatory args, {} are optional args",
            "green".green(),
            "blue".blue(),
            "magenta".magenta()
        );
        println!("=============================================================");
        println!("  {}|{}:\t\t\tprint this menu", "help".green(), "?".green());
        println!(
            "  {}|{}:\t\t\texit the application",
            "exit".green(),
            "x".green()
        );
        println!(
            "  {}\t\t\t\tclean the database (drop + migrate)",
            "clean".green()
        );
        println!(
            "  {}\t\t\t\tprints information about the running instance",
            "info".green()
        );
        println!(
            "  {} {} {}:\t\tpublish key material (value) for user",
            "publish".green(),
            "user".blue(),
            "value".blue()
        );
        println!(
            "  {} {}:\t\t\tlookup a proof for user",
            "lookup".green(),
            "user".blue()
        );
        println!(
            "  {} {}:\t\t\tlookup key history for user",
            "history".green(),
            "user".blue()
        );
        println!(
            "  {} {} {}:\t\tretrieve audit proof between start and end epochs",
            "audit".green(),
            "start".blue(),
            "end".blue()
        );
        println!(
            "  {}|{}\t\tretrieve the root hash at the latest epoch",
            "root".green(),
            "root_hash".green(),
        );
    }

    // ==== Helpers for managing directory commands ==== //
    fn handle_dir_cmd(command: &str, parts: Vec<&str>, full_text: &str) -> Command {
        let dir_cmd: Option<Option<DirectoryCommand>> = match command {
            "publish" => Some(Command::publish(parts)),
            "lookup" => Some(Command::lookup(parts)),
            "history" => Some(Command::history(parts)),
            "audit" => Some(Command::audit(parts)),
            "root" | "root_hash" => Some(Command::root_hash(parts)),
            _ => None,
        };
        match dir_cmd {
            Some(Some(cmd)) => Command::Directory(cmd),
            Some(None) => {
                let msg = format!(
                    "Command {} received invalid arguments. Check {} for syntax",
                    command,
                    "help".green()
                );
                Command::InvalidArgs(msg)
            }
            _ => Command::Unknown(String::from(full_text)),
        }
    }

    fn publish(parts: Vec<&str>) -> Option<DirectoryCommand> {
        if parts.len() < 3 {
            return None;
        }
        let (a, b) = (parts[1], parts[2]);
        let cmd = DirectoryCommand::Publish(String::from(a), String::from(b));
        Some(cmd)
    }

    fn lookup(parts: Vec<&str>) -> Option<DirectoryCommand> {
        if parts.len() < 2 {
            return None;
        }
        let a = parts[1];
        let cmd = DirectoryCommand::Lookup(String::from(a));
        Some(cmd)
    }

    fn history(parts: Vec<&str>) -> Option<DirectoryCommand> {
        if parts.len() < 2 {
            return None;
        }
        let a = parts[1];
        let cmd = DirectoryCommand::KeyHistory(String::from(a));
        Some(cmd)
    }

    fn audit(parts: Vec<&str>) -> Option<DirectoryCommand> {
        if parts.len() < 3 {
            return None;
        }
        let (a, b) = (parts[1], parts[2]);
        match (a.parse::<u64>(), b.parse::<u64>()) {
            (Ok(u_a), Ok(u_b)) => {
                let cmd = DirectoryCommand::Audit(u_a, u_b);
                Some(cmd)
            }
            _ => None,
        }
    }

    fn root_hash(_parts: Vec<&str>) -> Option<DirectoryCommand> {
        let cmd = DirectoryCommand::RootHash;
        Some(cmd)
    }
}

fn trim_newline(s: &mut String) {
    if s.ends_with('\n') {
        s.pop();
        if s.ends_with('\r') {
            s.pop();
        }
    }
}
