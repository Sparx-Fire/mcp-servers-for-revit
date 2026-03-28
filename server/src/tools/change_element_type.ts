import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerChangeElementTypeTool(server: McpServer) {
  server.tool(
    "change_element_type",
    "Batch swap family/element types on multiple elements at once. Useful for changing all doors from one type to another, swapping wall types, etc.",
    {
      elementIds: z
        .array(z.number())
        .describe("Element IDs to change type for"),
      targetTypeId: z
        .number()
        .optional()
        .describe("Target type element ID to change to"),
      targetTypeName: z
        .string()
        .optional()
        .describe("Target type name to search for (used if targetTypeId not provided)"),
      targetFamilyName: z
        .string()
        .optional()
        .describe("Target family name to narrow type search"),
    },
    async (args, extra) => {
      const params = {
        elementIds: args.elementIds,
        targetTypeId: args.targetTypeId ?? 0,
        targetTypeName: args.targetTypeName ?? "",
        targetFamilyName: args.targetFamilyName ?? "",
      };

      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("change_element_type", params);
        });
        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        return { content: [{ type: "text", text: `Change element type failed: ${error instanceof Error ? error.message : String(error)}` }] };
      }
    }
  );
}
