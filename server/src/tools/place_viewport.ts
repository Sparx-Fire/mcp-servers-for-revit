import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerPlaceViewportTool(server: McpServer) {
  server.tool(
    "place_viewport",
    "Place a view onto a sheet as a viewport. The view must not already be placed on another sheet. Position coordinates are in millimeters relative to the sheet origin.",
    {
      sheetId: z.number().describe("ID of the sheet to place the viewport on"),
      viewId: z.number().describe("ID of the view to place"),
      positionX: z
        .number()
        .describe("X position on the sheet in mm (from sheet origin)"),
      positionY: z
        .number()
        .describe("Y position on the sheet in mm (from sheet origin)"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("place_viewport", args);
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
              text: `Place viewport failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}
