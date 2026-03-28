import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerDuplicateViewTool(server: McpServer) {
  server.tool(
    "duplicate_view",
    "Duplicate one or more views with options: independent copy, dependent (linked to parent), or with detailing (copies annotations). Supports custom name prefix/suffix.",
    {
      viewIds: z
        .array(z.number())
        .describe("View IDs to duplicate"),
      duplicateOption: z
        .enum(["duplicate", "dependent", "withDetailing"])
        .optional()
        .describe("Duplication mode: duplicate (independent), dependent (linked), withDetailing (copies annotations)"),
      newNamePrefix: z
        .string()
        .optional()
        .describe("Prefix for the new view name"),
      newNameSuffix: z
        .string()
        .optional()
        .describe("Suffix for the new view name"),
    },
    async (args, extra) => {
      const params = {
        viewIds: args.viewIds,
        duplicateOption: args.duplicateOption ?? "duplicate",
        newNamePrefix: args.newNamePrefix ?? "",
        newNameSuffix: args.newNameSuffix ?? "",
      };

      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("duplicate_view", params);
        });
        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        return { content: [{ type: "text", text: `Duplicate view failed: ${error instanceof Error ? error.message : String(error)}` }] };
      }
    }
  );
}
