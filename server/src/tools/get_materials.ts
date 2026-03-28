import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerGetMaterialsTool(server: McpServer) {
  server.tool(
    "get_materials",
    "List all materials in the current Revit project with their basic properties including color, transparency, and asset availability. Can filter by material class or name.",
    {
      materialClass: z
        .string()
        .optional()
        .describe(
          "Filter materials by class (case-insensitive exact match, e.g. 'Metal', 'Concrete', 'Wood')"
        ),
      nameFilter: z
        .string()
        .optional()
        .describe(
          "Filter materials whose name contains this substring (case-insensitive)"
        ),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("get_materials", { ...args });
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
              text: `Get materials failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}
