import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerAddSharedParameterTool(server: McpServer) {
  server.tool(
    "add_shared_parameter",
    "Add a shared parameter from the shared parameter file to specified categories. Requires a shared parameter file to be set in Revit.",
    {
      parameterName: z
        .string()
        .describe(
          "Name of the shared parameter to add. Must exist in the specified group of the shared parameter file, or will be created."
        ),
      groupName: z
        .string()
        .describe(
          "Name of the parameter group in the shared parameter file where the parameter is defined (e.g. 'Identity Data', 'My Group')."
        ),
      categories: z
        .array(z.string())
        .describe(
          "List of Revit category names to bind the parameter to (e.g. ['Walls', 'Floors', 'Doors'])."
        ),
      isInstance: z
        .boolean()
        .optional()
        .default(true)
        .describe(
          "If true, binds as an instance parameter. If false, binds as a type parameter. Defaults to true."
        ),
      parameterGroup: z
        .string()
        .optional()
        .describe(
          "Display group for the parameter in the element properties panel. For Revit 2022+, use GroupTypeId property names (e.g. 'Data', 'IdentityData'). For older versions, use BuiltInParameterGroup enum names (e.g. 'PG_DATA'). Defaults to 'Data' / 'PG_DATA'."
        ),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("add_shared_parameter", {
            parameterName: args.parameterName,
            groupName: args.groupName,
            categories: args.categories,
            isInstance: args.isInstance,
            parameterGroup: args.parameterGroup,
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
              text: `Add shared parameter failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}
