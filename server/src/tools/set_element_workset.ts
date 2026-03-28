import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerSetElementWorksetTool(server: McpServer) {
  server.tool(
    "set_element_workset",
    "Move elements to a different workset by setting their workset parameter. Project must be workshared.",
    {
      requests: z
        .array(
          z.object({
            elementId: z.number().describe("Revit element ID"),
            worksetName: z
              .string()
              .describe("Name of the target workset to move the element to"),
          })
        )
        .describe("Array of workset assignment requests"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("set_element_workset", {
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
              text: `Set element workset failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}
