# TODO: This should probably be replaced with a node script building every file in `emails/`

npx mjml emails/invite.mjml -o out/invite.html
npx mjml emails/two-factor.mjml -o out/two-factor.html
