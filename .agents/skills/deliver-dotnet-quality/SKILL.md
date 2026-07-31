---
name: deliver-dotnet-quality
description: Deliver C# and .NET changes through an approved specification, plan, test contract, executable Full gate, UI evidence when applicable, and human acceptance. Use for feature work, bug fixes, refactoring, APIs, web UI, and Windows desktop UI in this repository.
---

# Deliver .NET quality

Read `.ai-quality/agent-policy.md` and run `pwsh ./aq.ps1 status` before any edit.

Follow the state-reported allowed action exactly:

1. In `discovery`, inspect only and complete `spec.md`.
2. After Requirements approval, complete `plan.md` and `test-matrix.md`.
3. After Tests approval, implement small slices mapped to `AC-###` criteria.
4. Run `pwsh ./aq.ps1 verify -WorkItemId <id> -Mode Full`.
5. Complete `delivery.md`, then run `pwsh ./aq.ps1 check-delivery -WorkItemId <id>`.
6. Never approve a stage for the user and never report complete without passing evidence.

If an expected check cannot run, report `INCOMPLETE` with the missing evidence.
