import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

const pointSchema = z.object({
  x: z.number().describe("X coordinate in mm"),
  y: z.number().describe("Y coordinate in mm"),
  z: z.number().describe("Z coordinate in mm"),
});

export function registerModifyElementTool(server: McpServer) {
  server.tool(
    "modify_element",
    "Modify Revit elements by moving, rotating, mirroring, or copying them. Coordinates are in millimeters.",
    {
      data: z.object({
        elementIds: z
          .array(z.number())
          .describe("Array of element IDs to modify"),
        action: z
          .enum(["move", "rotate", "mirror", "copy"])
          .describe("The modification action to perform"),
        translation: pointSchema
          .optional()
          .describe(
            "Translation vector in mm (required for 'move' action). E.g. {x: 1000, y: 0, z: 0} moves 1m in X"
          ),
        rotationCenter: pointSchema
          .optional()
          .describe(
            "Center point of rotation in mm (required for 'rotate' action)"
          ),
        rotationAngle: z
          .number()
          .optional()
          .describe("Rotation angle in degrees (required for 'rotate' action)"),
        mirrorPlaneOrigin: pointSchema
          .optional()
          .describe(
            "Origin point of the mirror plane in mm (required for 'mirror' action)"
          ),
        mirrorPlaneNormal: pointSchema
          .optional()
          .describe(
            "Normal vector of the mirror plane (required for 'mirror' action). E.g. {x: 1, y: 0, z: 0} for YZ plane"
          ),
        copyOffset: pointSchema
          .optional()
          .describe(
            "Offset vector for copy in mm (required for 'copy' action)"
          ),
      }),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("modify_element", {
            data: args.data,
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
              text: `Modify element failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}
