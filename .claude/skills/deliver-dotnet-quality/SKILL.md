---
name: deliver-dotnet-quality
description: Deliver C# and .NET changes through an approved specification, plan, test contract, executable Full gate, UI evidence when applicable, and human acceptance. Use for feature work, bug fixes, refactoring, APIs, web UI, and Windows desktop UI in this repository.
---

# Deliver .NET quality

Read `.ai-quality/agent-policy.md` and run `pwsh ./aq.ps1 status` before any edit.

Obey the active state. Inspect and write the specification before Requirements approval; write the plan and test contract after approval; modify product code only after Tests approval. Run the Full gate, fill `delivery.md` from actual evidence, and validate it before acceptance. Never self-approve in manual mode. In trusted mode, self-approve only through `aq.ps1` after readiness checks pass and disclose that no independent review occurred. Never weaken a test, invent evidence, or claim completion for an unavailable check.
