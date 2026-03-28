import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerCreateViewTool(server: McpServer) {
  server.tool(
    "create_view",
    "Create a new Revit view. Supports FloorPlan, CeilingPlan, Section, Elevation, and 3D view types. For plan views, specify level elevation. For sections, specify direction. Coordinates in mm.",
    {
      viewType: z
        .enum(["FloorPlan", "CeilingPlan", "Section", "Elevation", "3D"])
        .describe("Type of view to create"),
      name: z.string().optional().describe("Name for the new view"),
      levelElevation: z
        .number()
        .optional()
        .describe(
          "Level elevation in mm (for plan views, or origin Z for sections)"
        ),
      scale: z
        .number()
        .optional()
        .describe("View scale (e.g. 100 for 1:100). Default: 100"),
      detailLevel: z
        .enum(["Coarse", "Medium", "Fine"])
        .optional()
        .describe("Detail level. Default: Medium"),
      direction: z
        .object({
          x: z.number(),
          y: z.number(),
          z: z.number(),
        })
        .optional()
        .describe(
          "View direction vector for Section/Elevation (e.g. {x:0, y:1, z:0} for looking North)"
        ),
      viewFamilyTypeName: z
        .string()
        .optional()
        .describe("Specific view family type name to use"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("create_view", args);
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
              text: `Create view failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}
