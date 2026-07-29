# bump-rust-sdk evals

Behavior test cases for the `bump-rust-sdk` skill, in the `skill-creator` schema.

`evals.json` holds five cases covering the skill's substantive decisions: the NPM→git-SHA mapping, resisting the deprecated run-number method, the MSRV/toolchain bump, targeted `cargo update -p`, and a breaking-change fix. Each case's `expectations` are the pass criteria; cases 4 and 5 carry `notes` recording their ablation outcomes (earned vs. borderline).

Run with `/skill-creator:skill-creator` in Benchmark mode (with-skill vs. without-skill).
