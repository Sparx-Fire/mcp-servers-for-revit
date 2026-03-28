import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerRenumberElementsTool(server: McpServer) {
  server.tool(
    "renumber_elements",
    "Sequentially renumber rooms, doors, windows, or parking elements by spatial order or custom sequence. Supports prefix/suffix, custom start number, and dry-run preview.",
    {
      elementIds: z
        .array(z.number())
        .optional()
        .describe("Specific element IDs to renumber. If omitted, uses targetCategory."),
      targetCategory: z
        .enum(["Rooms", "Doors", "Windows", "Parking"])
        .optional()
        .describe("Category of elements to renumber"),
      parameterName: z
        .string()
        .optional()
        .describe("Custom parameter name to set the number on (default: uses built-in number parameter)"),
      startNumber: z
        .number()
        .optional()
        .describe("Starting number (default: 1)"),
      prefix: z.string().optional().describe("Prefix before the number (e.g., 'R-')"),
      suffix: z.string().optional().describe("Suffix after the number"),
      increment: z.number().optional().describe("Increment between numbers (default: 1)"),
      sortBy: z
        .enum(["location", "name", "none"])
        .optional()
        .describe("Sort order before numbering: location (spatial), name (alphabetical), or none (as-is)"),
      dryRun: z
        .boolean()
        .optional()
        .describe("If true (default), only preview changes. Set to false to apply."),
    },
    async (args, extra) => {
      const params = {
        elementIds: args.elementIds ?? [],
        targetCategory: args.targetCategory ?? "",
        parameterName: args.parameterName ?? "",
        startNumber: args.startNumber ?? 1,
        prefix: args.prefix ?? "",
        suffix: args.suffix ?? "",
        increment: args.increment ?? 1,
        sortBy: args.sortBy ?? "location",
        dryRun: args.dryRun ?? true,
      };

      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("renumber_elements", params);
        });
        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        return { content: [{ type: "text", text: `Renumber elements failed: ${error instanceof Error ? error.message : String(error)}` }] };
      }
    }
  );
}
