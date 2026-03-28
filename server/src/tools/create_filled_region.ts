import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerCreateFilledRegionTool(server: McpServer) {
  server.tool(
    "create_filled_region",
    "Create a 2D filled region (hatched area) in a view with specified boundary points and fill pattern. Useful for area shading, detail views, and presentation drawings.",
    {
      boundaryPoints: z
        .array(z.object({ x: z.number(), y: z.number() }))
        .describe("Boundary points in mm defining the filled region outline (minimum 3 points)"),
      viewId: z
        .number()
        .optional()
        .describe("View ID to create the region in (default: active view)"),
      filledRegionTypeName: z
        .string()
        .optional()
        .describe("Filled region type name (default: first available)"),
    },
    async (args, extra) => {
      const params = {
        boundaryPoints: args.boundaryPoints,
        viewId: args.viewId ?? 0,
        filledRegionTypeName: args.filledRegionTypeName ?? "",
      };

      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("create_filled_region", params);
        });
        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        return { content: [{ type: "text", text: `Create filled region failed: ${error instanceof Error ? error.message : String(error)}` }] };
      }
    }
  );
}
