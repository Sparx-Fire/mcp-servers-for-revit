#!/usr/bin/env node
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { registerTools } from "./tools/register.js";
import { setRevitPort } from "./utils/ConnectionManager.js";

function parsePort(): number {
  const args = process.argv.slice(2);
  for (let i = 0; i < args.length; i++) {
    if (args[i] === "--port" && args[i + 1]) {
      return parseInt(args[i + 1], 10);
    }
    if (args[i] === "--revit-version" && args[i + 1]) {
      const version = parseInt(args[i + 1], 10);
      return 8020 + (version % 100);
    }
  }
  if (process.env.REVIT_MCP_PORT) {
    return parseInt(process.env.REVIT_MCP_PORT, 10);
  }
  return 8080;
}

// 创建服务器实例
const server = new McpServer({
  name: "mcp-server-for-revit",
  version: "1.0.0",
});

// 启动服务器
async function main() {
  const port = parsePort();
  setRevitPort(port);

  // 注册工具
  await registerTools(server);

  // 连接到传输层
  const transport = new StdioServerTransport();
  await server.connect(transport);
  console.error(`Revit MCP Server started (connecting to Revit on port ${port})`);
}

main().catch((error) => {
  console.error("Error starting Revit MCP Server:", error);
  process.exit(1);
});
