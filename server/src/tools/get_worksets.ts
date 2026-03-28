import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerGetWorksetsTool(server: McpServer) {
  server.tool(
    "get_worksets",
    "List all worksets in the current Revit project with their properties. Returns workset name, kind, open/editable status, and owner.",
    {
      includeSystemWorksets: z
        .boolean()
        .optional()
        .describe(
          "Include system worksets such as Family Workset, Project Standards, and Views workset (default: false)"
        ),
    },
    async (args, extra) => {
      const params = {
        includeSystemWorksets: args.includeSystemWorksets ?? false,
      };

      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("get_worksets", params);
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
              text: `Get worksets failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}
