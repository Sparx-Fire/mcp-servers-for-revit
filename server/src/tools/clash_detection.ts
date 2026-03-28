import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerClashDetectionTool(server: McpServer) {
  server.tool(
    "clash_detection",
    "Detect geometric intersections (clashes) between two sets of elements. Specify by category names (e.g., 'Ducts' vs 'StructuralFraming') or specific element IDs. Returns pairs of clashing elements.",
    {
      categoryA: z
        .string()
        .optional()
        .describe("Category name for set A (e.g., Walls, Ducts, Pipes, StructuralFraming, Columns, Floors)"),
      categoryB: z
        .string()
        .optional()
        .describe("Category name for set B"),
      elementIdsA: z
        .array(z.number())
        .optional()
        .describe("Specific element IDs for set A (overrides categoryA)"),
      elementIdsB: z
        .array(z.number())
        .optional()
        .describe("Specific element IDs for set B (overrides categoryB)"),
      tolerance: z
        .number()
        .optional()
        .describe("Tolerance in mm (default: 0, exact intersection)"),
      maxResults: z
        .number()
        .optional()
        .describe("Maximum number of clashes to return (default: 100)"),
    },
    async (args, extra) => {
      const params = {
        categoryA: args.categoryA ?? "",
        categoryB: args.categoryB ?? "",
        elementIdsA: args.elementIdsA ?? [],
        elementIdsB: args.elementIdsB ?? [],
        tolerance: args.tolerance ?? 0,
        maxResults: args.maxResults ?? 100,
      };

      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("clash_detection", params);
        });
        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        return { content: [{ type: "text", text: `Clash detection failed: ${error instanceof Error ? error.message : String(error)}` }] };
      }
    }
  );
}
