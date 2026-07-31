# AI engineering policy

## Source of truth

The active work item, approved acceptance criteria, repository code, and executable evidence are authoritative. Chat history and agent confidence are not.

## Mandatory sequence

1. Discovery: inspect only; complete `spec.md`.
2. Requirements approval: manual mode uses a human; trusted mode lets the Agent proceed only after deterministic readiness checks.
3. Planning: complete `plan.md` and `test-matrix.md`.
4. Plan and Tests approvals: manual mode uses a human; trusted mode lets the Agent self-approve completed artifacts.
5. Implementation: make small mapped changes and run narrow checks.
6. Verification: run the Full quality gate, including configured UI hooks.
7. Delivery: complete and validate `delivery.md`; manual mode requests human/PR acceptance, while trusted mode may self-accept and must disclose the missing independent review.

## Modification rules

- Product-code edits are forbidden in `discovery`, `requirements-approved`, and `plan-approved`.
- In `implementation-authorized`, edit only approved scope.
- In `verification-failed`, edit only to resolve recorded failures; scope expansion requires renewed approval.
- Preserve unrelated work and inspect the diff after every slice.
- Never delete, skip, loosen, or replace an approved check to obtain green status.
- Never invent command results, screenshots, approvals, or environment coverage.

## Requirement readiness

Requirements are ready only when the objective, current and desired behavior, non-goals, constraints, user journeys, failure cases, and observable `AC-###` criteria are present. A material ambiguity blocks approval.

## Completion definition

Completion requires all of the following:

- every acceptance criterion is mapped to evidence;
- Release build passes with warnings treated as errors;
- all configured tests and Full hooks pass;
- the application or affected UI journey is actually executed when UI is in scope;
- the Full gate evidence exists and is current for the approved specification;
- `delivery.md` contains no placeholder or unsupported claim;
- residual risks, skipped checks, and manual checks are explicit;
- the Delivery approval is recorded under the active approval mode; protected repositories still require their independent PR/CI policy.

If any item is unavailable, failed, or unverified, report `INCOMPLETE`.

## Trust boundary

`approvalMode` is stored in `.ai-quality/config.json`:

- `manual` is the default and keeps approval authority outside the implementing Agent.
- `trusted` is an explicit repository-level opt-in. It removes repeated human stage prompts but does not remove readiness checks, artifact hashes, edit-state rules, Full verification, UI checks, or delivery validation.
- Trusted approval records must identify `approvalAuthority: implementing-agent`; never present them as independent review.

Local instruction files are behavioral controls, not a security boundary. Protected repositories still need clean CI, branch protection, and reviewer credentials unavailable to the Agent.
