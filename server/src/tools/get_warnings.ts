import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerGetWarningsTool(server: McpServer) {
  server.tool(
    "get_warnings",
    "Get all warnings/errors in the current Revit model. Warnings indicate issues like duplicate elements, overlapping geometry, room separation problems, etc. Useful for model health auditing and quality control.",
    {
      severityFilter: z
        .enum(["All", "Warning", "Error"])
        .optional()
        .describe("Filter by severity level (default: All)"),
      maxWarnings: z
        .number()
        .optional()
        .describe("Maximum number of warnings to return (default: 500)"),
      categoryFilter: z
        .string()
        .optional()
        .describe(
          "Filter warnings containing this text in the description (case-insensitive)"
        ),
    },
    async (args, extra) => {
      const params = {
        severityFilter: args.severityFilter ?? "All",
        maxWarnings: args.maxWarnings ?? 500,
        categoryFilter: args.categoryFilter ?? "",
      };

      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("get_warnings", params);
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
              text: `Get warnings failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}
