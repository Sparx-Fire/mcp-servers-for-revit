# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Primary Purpose

This repo is an MCP (Model Context Protocol) server + Revit plugin system. As Claude Code, your primary job is to **use the MCP tools to answer questions and perform actions in Revit**, not to develop the codebase itself. The repo is here for reference.

## Architecture

```
MCP Client (Claude) ←stdio→ MCP Server (TypeScript) ←TCP/JSON-RPC:8080→ Revit Plugin (C#) → Revit API
```

- **server/** — TypeScript MCP server. Exposes 25+ tools via stdio. Tools are in `server/src/tools/` (each file exports a `register*` function, auto-discovered by `register.ts`). Connection to Revit is mutex-protected via `ConnectionManager.ts`.
- **plugin/** — C# Revit add-in. `SocketService.cs` listens on TCP port 8080, dispatches JSON-RPC commands to the command registry.
- **commandset/** — C# command implementations. Each command runs on Revit's main thread via `IExternalEventHandler`.
- **command.json** — Manifest mapping command names to their C# implementations.

## send_code_to_revit: Code Execution Template

When using `send_code_to_revit`, your C# code is wrapped into this template:

```csharp
using System;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;

namespace AIGeneratedCode
{
    public static class CodeExecutor
    {
        public static object Execute(Document document, object[] parameters)
        {
            // YOUR CODE HERE — must return an object
        }
    }
}
```

**Critical rules for send_code_to_revit:**
- Use `document` (lowercase) — not `Document` (that's the type)
- Construct UIDocument as `new UIDocument(document)` when needed
- The code runs inside a transaction automatically — all changes are undoable with Ctrl+Z
- Must return something (use `return "done";` if nothing meaningful to return)
- Has access to all loaded Revit assemblies via Roslyn compilation
- Timeout: 60 seconds
- Line numbers in compilation errors are offset due to the wrapper

**Common gotchas (from experience):**
- Dimension curves may be unbound — don't call `curve.GetEndPoint()`, use `line.Direction`
- `DimensionSegment.LeaderCount` and `IsTextPositionAdjusted()` don't exist in Revit 2025
- `BuiltInParameter.DIM_TEXT_POSITION` doesn't exist
- Per-reference witness line length is not accessible via the Revit API
- When querying by known element IDs, use `new ElementId(intId)` — avoids selection issues
- `ElementTransformUtils.MoveElement` moves the entire element — re-set text position after moving

## Build Commands

### Server (TypeScript)
```bash
cd server && npm install
cd server && npm run build          # Compiles to build/
npx tsx src/index.ts                # Run directly during dev
```

### Plugin & CommandSet (C#)
```bash
# Revit 2025+ (.NET 8)
dotnet build mcp-servers-for-revit.sln -c "Release R25"
dotnet build mcp-servers-for-revit.sln -c "Release R26"

# Revit 2020-2024 (.NET Framework 4.8)
msbuild mcp-servers-for-revit.sln -p:Configuration="Release R24" -restore
```

### Tests (require running Revit instance)
```bash
dotnet test -c Debug.R26 -r win-x64 tests/commandset
```

### Release
```powershell
./scripts/release.ps1 -Version X.Y.Z
git push origin main --tags   # Triggers CI/CD
```
