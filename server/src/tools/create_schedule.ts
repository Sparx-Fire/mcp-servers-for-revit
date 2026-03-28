import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerCreateScheduleTool(server: McpServer) {
  server.tool(
    "create_schedule",
    "Create a schedule view in Revit. Supports Regular, KeySchedule, and MaterialTakeoff types. Specify a BuiltInCategory name (e.g. OST_Walls) or leave empty for multi-category.",
    {
      categoryName: z
        .string()
        .describe(
          "BuiltInCategory name like 'OST_Walls', 'OST_Doors', 'OST_Rooms'. Use empty string for multi-category schedule."
        ),
      name: z.string().optional().describe("Schedule name"),
      type: z
        .enum(["Regular", "KeySchedule", "MaterialTakeoff"])
        .optional()
        .describe("Schedule type. Default: Regular"),
      fields: z
        .array(
          z.object({
            parameterName: z
              .string()
              .describe("Parameter name to add as a schedule field"),
            fieldType: z
              .string()
              .optional()
              .describe("Field type (Instance, Type, Count, Formula, Phasing)"),
            heading: z
              .string()
              .optional()
              .describe("Custom column heading"),
            isHidden: z
              .boolean()
              .optional()
              .describe("Whether the field is hidden"),
            horizontalAlignment: z
              .enum(["Left", "Center", "Right"])
              .optional()
              .describe("Horizontal alignment. Default: Left"),
          })
        )
        .optional()
        .describe("Fields to include in the schedule"),
      filters: z
        .array(
          z.object({
            fieldName: z.string().describe("Field name to filter by"),
            filterType: z
              .string()
              .describe(
                "Filter type (Equal, NotEqual, GreaterThan, GreaterThanOrEqual, LessThan, LessThanOrEqual, Contains, NotContains, BeginsWith, NotBeginsWith, EndsWith, NotEndsWith, HasValue, HasNoValue)"
              ),
            filterValue: z.string().describe("Filter value"),
          })
        )
        .optional()
        .describe("Filters to apply to the schedule"),
      sortFields: z
        .array(
          z.object({
            fieldName: z.string().describe("Field name to sort by"),
            sortOrder: z
              .enum(["Ascending", "Descending"])
              .optional()
              .describe("Sort order. Default: Ascending"),
          })
        )
        .optional()
        .describe("Sort/group fields for the schedule"),
      showTitle: z
        .boolean()
        .optional()
        .describe("Show schedule title. Default: true"),
      showHeaders: z
        .boolean()
        .optional()
        .describe("Show column headers. Default: true"),
      showGridLines: z
        .boolean()
        .optional()
        .describe("Show grid lines. Default: true"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("create_schedule", args);
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
              text: `Create schedule failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}
