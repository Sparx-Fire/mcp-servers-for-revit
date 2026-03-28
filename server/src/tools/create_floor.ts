import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

const pointSchema = z.object({
  x: z.number().describe("X coordinate in mm"),
  y: z.number().describe("Y coordinate in mm"),
});

export function registerCreateFloorTool(server: McpServer) {
  server.tool(
    "create_floor",
    "Create a floor element from boundary points or from room boundaries. Coordinates in mm. Specify either boundary points or a roomId to derive the boundary from a room.",
    {
      boundaryPoints: z
        .array(pointSchema)
        .optional()
        .describe(
          "Array of 2D points defining the floor boundary in mm (minimum 3 points). Points are connected in order, last connects to first."
        ),
      roomId: z
        .number()
        .optional()
        .describe(
          "Room element ID to derive floor boundary from (alternative to boundaryPoints)"
        ),
      floorTypeName: z
        .string()
        .optional()
        .describe("Floor type name to use. If omitted, uses the first available floor type."),
      levelElevation: z
        .number()
        .optional()
        .describe("Level elevation in mm for the floor. If omitted, uses the room's level or the closest level."),
      isStructural: z
        .boolean()
        .optional()
        .describe("Whether the floor is structural (default: false)"),
    },
    async (args, extra) => {
      const params = {
        boundaryPoints: args.boundaryPoints ?? [],
        roomId: args.roomId ?? 0,
        floorTypeName: args.floorTypeName ?? "",
        levelElevation: args.levelElevation ?? 0,
        isStructural: args.isStructural ?? false,
      };

      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("create_floor", params);
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
              text: `Create floor failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}
