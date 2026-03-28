import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerBatchRenameTool(server: McpServer) {
  server.tool(
    "batch_rename",
    "Batch rename views, sheets, or elements using find/replace, prefix/suffix, or parameter-based naming rules. Useful for enforcing naming conventions across the project.",
    {
      elementIds: z
        .array(z.number())
        .optional()
        .describe(
          "Specific element IDs to rename. If omitted, uses targetCategory to find elements."
        ),
      targetCategory: z
        .enum(["Views", "Sheets", "Levels", "Grids", "Rooms"])
        .optional()
        .describe(
          "Category of elements to rename (used when elementIds is not provided)"
        ),
      findText: z
        .string()
        .optional()
        .describe("Text to find in existing names (for find/replace mode)"),
      replaceText: z
        .string()
        .optional()
        .describe("Replacement text (for find/replace mode)"),
      prefix: z
        .string()
        .optional()
        .describe("Prefix to add to names"),
      suffix: z
        .string()
        .optional()
        .describe("Suffix to add to names"),
      dryRun: z
        .boolean()
        .optional()
        .describe(
          "If true (default), only previews changes without applying. Set to false to apply."
        ),
    },
    async (args, extra) => {
      const params = {
        elementIds: args.elementIds ?? [],
        targetCategory: args.targetCategory ?? "",
        findText: args.findText ?? "",
        replaceText: args.replaceText ?? "",
        prefix: args.prefix ?? "",
        suffix: args.suffix ?? "",
        dryRun: args.dryRun ?? true,
      };

      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("batch_rename", params);
        });

        return {
          content: [
            {
              type: "text",
              text: JSON.stringify(response, null, 2),
            },
          ],
        };
      } catch (error) {
        return {
          content: [
            {
              type: "text",
              text: `Batch rename failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}
