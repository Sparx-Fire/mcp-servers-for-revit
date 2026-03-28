import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerGetElementParametersTool(server: McpServer) {
  server.tool(
    "get_element_parameters",
    "Get all parameters (instance and type) of one or more Revit elements by their IDs. Returns parameter names, values, storage types, and whether they are read-only or shared.",
    {
      elementIds: z
        .array(z.number())
        .describe("Array of Revit element IDs to get parameters for"),
      includeTypeParameters: z
        .boolean()
        .optional()
        .describe(
          "Include type parameters in addition to instance parameters (default: true)"
        ),
    },
    async (args, extra) => {
      const params = {
        elementIds: args.elementIds,
        includeTypeParameters: args.includeTypeParameters ?? true,
      };

      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand(
            "get_element_parameters",
            params
          );
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
              text: `Get element parameters failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}
