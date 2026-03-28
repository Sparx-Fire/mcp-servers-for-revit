import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerSetElementParametersTool(server: McpServer) {
  server.tool(
    "set_element_parameters",
    "Set parameter values on one or more Revit elements. Supports instance and type parameters. Values are automatically converted to the correct storage type (String, Integer, Double, ElementId).",
    {
      requests: z
        .array(
          z.object({
            elementId: z.number().describe("Revit element ID"),
            parameterName: z
              .string()
              .describe("Name of the parameter to set"),
            value: z
              .union([z.string(), z.number(), z.boolean()])
              .describe(
                "Value to set. String for text, number for numeric/ElementId, boolean for yes/no parameters"
              ),
          })
        )
        .describe("Array of parameter set requests"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("set_element_parameters", {
            requests: args.requests,
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
              text: `Set element parameters failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}
