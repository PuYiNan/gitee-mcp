import { isToolCallEventType, type ExtensionAPI } from "@earendil-works/pi-coding-agent";
import { spawnSync } from "node:child_process";
import { existsSync } from "node:fs";
import { join } from "node:path";

export default function (pi: ExtensionAPI) {
  pi.on("tool_call", async (event, ctx) => {
    if (!isToolCallEventType("write", event) && !isToolCallEventType("edit", event)) return;

    const input = event.input as { path?: string };
    const targetPath = String(input.path ?? "");
    const guard = join(ctx.cwd, ".ai-quality", "scripts", "Assert-AiEditAllowed.ps1");
    if (!existsSync(guard)) return;

    const result = spawnSync(
      "pwsh",
      ["-NoProfile", "-File", guard, "-Path", targetPath],
      { cwd: ctx.cwd, encoding: "utf8" },
    );

    if (result.status !== 0) {
      const reason = (result.stderr || result.stdout || "AI quality edit guard rejected the write").trim();
      return { block: true, reason };
    }
  });
}
