import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerGetPhasesTool(server: McpServer) {
  server.tool(
    "get_phases",
    "Get all phases and phase filters from the current Revit project. Phases define the timeline of construction, and phase filters control element visibility based on phase status.",
    {
      includePhaseFilters: z
        .boolean()
        .optional()
        .describe(
          "Include phase filters in addition to phases (default: true)"
        ),
    },
    async (args, extra) => {
      const params = {
        includePhaseFilters: args.includePhaseFilters ?? true,
      };

      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("get_phases", params);
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
              text: `Get phases failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}
