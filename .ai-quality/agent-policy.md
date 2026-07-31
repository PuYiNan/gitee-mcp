# AI engineering policy

## Source of truth

The active work item, approved acceptance criteria, repository code, and executable evidence are authoritative. Chat history and agent confidence are not.

## Mandatory sequence

1. Discovery: inspect only; complete `spec.md`.
2. Requirements approval: human confirms intent and acceptance criteria.
3. Planning: complete `plan.md` and `test-matrix.md`.
4. Plan and Tests approvals: human authorizes implementation.
5. Implementation: make small mapped changes and run narrow checks.
6. Verification: run the Full quality gate, including configured UI hooks.
7. Delivery: complete `delivery.md`; validate it; request human/PR acceptance.

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
- a human reviewer or protected PR process accepts the delivery.

If any item is unavailable, failed, or unverified, report `INCOMPLETE`.

## Trust boundary

Local instruction files are behavioral controls, not a security boundary. Hard enforcement requires the agent credential to lack permission to approve protected pull requests or bypass required CI checks. Keep approval authority outside the implementing agent.
