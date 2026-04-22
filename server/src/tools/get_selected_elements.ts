import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerGetSelectedElementsTool(server: McpServer) {
  server.tool(
    "get_selected_elements",
    "Get elements currently selected in Revit. You can limit the number of returned elements.",
    {
      limit: z
        .number()
        .optional()
        .describe(
          "Maximum number of elements to return. Defaults to 100000 so a large user selection is not silently truncated; pass a smaller value if you only need a sample."
        ),
    },
    async (args, extra) => {
      const params = {
        limit: args.limit || 100000,
      };

      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("get_selected_elements", params);
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
              text: `get selected elements failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}
