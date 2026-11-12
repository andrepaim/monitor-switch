# Project instructions

- Read `docs/DESIGN-v1.1.md` before implementation. User authorization determines scope; scaffolding is not authorization for physical switching or button setup.
- Keep the device profile fixed to VCP 0x60 and values 5/6. Do not use standard HDMI 17/18 substitutes.
- Never use primary/first monitor or enumeration order as persistent identity. Refuse ambiguous targeting.
- No hardware writes in automated tests or CI. Test native behavior only in explicitly supervised Windows integration sessions.
- No Options+ private-database edits, Deskflow changes, hooks, services, network listeners, drivers, elevation, security-setting changes, or publication.
- Keep unsupported scaffold behavior explicit. Do not replace exit 21 with success before implementing and validating the command.
- Preserve pending operations when state is uncertain. Exactly one native write per operation; never auto-rollback.
- Update the acceptance ledger with measured evidence and clear pending items. Never infer visual success from API acceptance.
- Run restore/build/test from the root. Keep artifacts, machine config, state, serials and local evidence out of Git.
