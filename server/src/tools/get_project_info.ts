import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerGetProjectInfoTool(server: McpServer) {
  server.tool(
    "get_project_info",
    "Get comprehensive project information from the active Revit document, including project metadata, phases, worksets, Revit links, and levels.",
    {
      includePhases: z
        .boolean()
        .optional()
        .describe("Include project phases (default: true)"),
      includeWorksets: z
        .boolean()
        .optional()
        .describe("Include workset information (default: true)"),
      includeLinks: z
        .boolean()
        .optional()
        .describe("Include Revit link information (default: true)"),
      includeLevels: z
        .boolean()
        .optional()
        .describe("Include level information (default: true)"),
    },
    async (args, extra) => {
      const params = {
        includePhases: args.includePhases ?? true,
        includeWorksets: args.includeWorksets ?? true,
        includeLinks: args.includeLinks ?? true,
        includeLevels: args.includeLevels ?? true,
      };

      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("get_project_info", params);
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
              text: `Get project info failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}
