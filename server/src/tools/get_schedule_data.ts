import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerGetScheduleDataTool(server: McpServer) {
  server.tool(
    "get_schedule_data",
    "Read data from a Revit schedule. Pass a schedule ID to get its content, or omit to list all schedules in the project.",
    {
      scheduleId: z
        .number()
        .optional()
        .describe(
          "Schedule element ID. If omitted or 0, lists all schedules in the project."
        ),
      maxRows: z
        .number()
        .optional()
        .describe("Maximum rows to return (default: 500)"),
    },
    async (args, extra) => {
      const params = {
        scheduleId: args.scheduleId ?? 0,
        maxRows: args.maxRows ?? 500,
      };

      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("get_schedule_data", params);
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
              text: `Get schedule data failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}
