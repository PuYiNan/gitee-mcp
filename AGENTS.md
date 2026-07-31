# Mandatory AI delivery policy

Before changing product code, read `.ai-quality/agent-policy.md` and the active work item's `state.json`.

- Run `pwsh ./aq.ps1 status` before every task or resumed session.
- Create work items with `pwsh ./aq.ps1 new -Title <title> [-UiScope]`.
- Do not edit product code unless state is `implementation-authorized` or `verification-failed`.
- Never approve Requirements, Plan, Tests, or Delivery on behalf of the human reviewer.
- Never weaken an approved test to make an implementation pass.
- Run the Full quality gate and delivery validator before claiming completion.
- A task is incomplete whenever required evidence is missing, failed, skipped, or unavailable.

These rules override convenience and speed. Repository-specific instructions may add stricter constraints but may not weaken this policy.
