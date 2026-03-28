import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerExportScheduleTool(server: McpServer) {
  server.tool(
    "export_schedule",
    "Export a Revit schedule to a text/CSV file. Returns the export file path on success.",
    {
      scheduleId: z
        .number()
        .describe("The element ID of the schedule to export"),
      exportPath: z
        .string()
        .optional()
        .describe(
          "File path for the exported schedule. If omitted, exports to the user's temp folder"
        ),
      delimiter: z
        .enum(["Tab", "Comma", "Space", "Semicolon"])
        .optional()
        .describe("Field delimiter for the exported file (default: Tab)"),
      includeHeaders: z
        .boolean()
        .optional()
        .describe("Whether to include column headers in the export (default: true)"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("export_schedule", args);
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
              text: `Export schedule failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}
