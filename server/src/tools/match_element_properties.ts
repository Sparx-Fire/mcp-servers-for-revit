import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerMatchElementPropertiesTool(server: McpServer) {
  server.tool(
    "match_element_properties",
    "Copy parameter values from a source element to one or more target elements. Like Revit's 'Match Type Properties' but for instance parameters too. Specify which parameters to copy or copy all matching writable parameters.",
    {
      sourceElementId: z
        .number()
        .describe("Source element ID to copy parameter values from"),
      targetElementIds: z
        .array(z.number())
        .describe("Target element IDs to copy parameter values to"),
      parameterNames: z
        .array(z.string())
        .optional()
        .describe("Specific parameter names to copy (default: copy all matching writable parameters)"),
      includeTypeParameters: z
        .boolean()
        .optional()
        .describe("Also copy type parameters (default: false, instance only)"),
    },
    async (args, extra) => {
      const params = {
        sourceElementId: args.sourceElementId,
        targetElementIds: args.targetElementIds,
        parameterNames: args.parameterNames ?? [],
        includeTypeParameters: args.includeTypeParameters ?? false,
      };

      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("match_element_properties", params);
        });
        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        return { content: [{ type: "text", text: `Match properties failed: ${error instanceof Error ? error.message : String(error)}` }] };
      }
    }
  );
}
