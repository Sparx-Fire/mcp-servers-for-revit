import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerGetSharedParametersTool(server: McpServer) {
  server.tool(
    "get_shared_parameters",
    "List all project parameters (shared and non-shared) bound to categories in the current Revit project. Can filter by category name.",
    {
      categoryFilter: z
        .string()
        .optional()
        .describe(
          "Optional category name filter. Returns only parameters bound to categories whose name contains this string (case-insensitive)."
        ),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("get_shared_parameters", {
            categoryFilter: args.categoryFilter,
          });
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
              text: `Get shared parameters failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}
