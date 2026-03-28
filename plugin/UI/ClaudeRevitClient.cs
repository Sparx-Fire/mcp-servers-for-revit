using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace revit_mcp_plugin.UI
{
    public class ClaudeRevitClient
    {
        private readonly List<JObject> _conversationHistory = new List<JObject>();
        private string _apiKey;
        private const string API_URL = "https://api.anthropic.com/v1/messages";
        private string _model = "claude-sonnet-4-20250514";
        private const int MCP_PORT = 8080;

        private const string SYSTEM_PROMPT = @"Sei Claude, un assistente AI integrato direttamente in Autodesk Revit. Hai accesso a tool che eseguono comandi sul modello Revit attivo in tempo reale.

Quando l'utente ti chiede di fare qualcosa sul modello, USA I TOOL disponibili per eseguirlo. Non limitarti a descrivere — esegui l'azione.

Rispondi in italiano, sii conciso. Dopo aver eseguito un tool, descrivi brevemente il risultato.

Coordinate: tutti i valori sono in millimetri (mm).
Categorie comuni: OST_Walls, OST_Floors, OST_Doors, OST_Windows, OST_StructuralColumns, OST_StructuralFraming, OST_Rooms.";

        private bool _thinkingEnabled;
        private int _thinkingBudget = 10000;

        public string Model
        {
            get => _model;
            set => _model = value;
        }

        public bool ThinkingEnabled
        {
            get => _thinkingEnabled;
            set => _thinkingEnabled = value;
        }

        public int ThinkingBudget
        {
            get => _thinkingBudget;
            set => _thinkingBudget = value;
        }

        public ClaudeRevitClient()
        {
            LoadApiKey();
        }

        private void LoadApiKey()
        {
            _apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
            if (string.IsNullOrEmpty(_apiKey))
            {
                try
                {
                    string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "api_key.txt");
                    if (File.Exists(path)) _apiKey = File.ReadAllText(path).Trim();
                }
                catch { }
            }
        }

        public async Task<string> SendMessage(string userMessage)
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                return "Chiave API non configurata.\n\n" +
                       "Per abilitare la chat con tool Revit:\n" +
                       "1. Variabile d'ambiente: ANTHROPIC_API_KEY=sk-ant-...\n" +
                       "2. Oppure file: %USERPROFILE%\\.claude\\api_key.txt\n\n" +
                       "Ottieni una chiave su console.anthropic.com";
            }

            _conversationHistory.Add(new JObject { ["role"] = "user", ["content"] = userMessage });

            while (_conversationHistory.Count > 30)
                _conversationHistory.RemoveAt(0);

            try
            {
                return await ProcessConversation();
            }
            catch (Exception ex)
            {
                return $"Errore: {ex.Message}";
            }
        }

        private async Task<string> ProcessConversation()
        {
            int maxToolRounds = 5;

            for (int round = 0; round < maxToolRounds; round++)
            {
                var response = await CallClaudeApi();

                if (response == null)
                    return "Nessuna risposta dall'API.";

                var content = response["content"] as JArray;
                if (content == null) return "Risposta vuota.";

                // Check stop reason
                string stopReason = response["stop_reason"]?.ToString() ?? "end_turn";

                // Collect thinking, text and tool_use blocks
                var thinkingParts = new List<string>();
                var textParts = new List<string>();
                var toolUses = new List<JObject>();

                foreach (var block in content)
                {
                    string blockType = block["type"]?.ToString();
                    if (blockType == "thinking")
                        thinkingParts.Add(block["thinking"]?.ToString() ?? "");
                    else if (blockType == "text")
                        textParts.Add(block["text"]?.ToString() ?? "");
                    else if (blockType == "tool_use")
                        toolUses.Add((JObject)block);
                }

                // Notify panel about thinking content
                if (thinkingParts.Count > 0)
                    MCPDockablePanel.Instance?.OnThinkingReceived(string.Join("\n", thinkingParts));

                // Add assistant message to history
                _conversationHistory.Add(new JObject { ["role"] = "assistant", ["content"] = content });

                if (stopReason == "tool_use" && toolUses.Count > 0)
                {
                    // Execute tools and add results
                    var toolResults = new JArray();
                    foreach (var toolUse in toolUses)
                    {
                        string toolName = toolUse["name"]?.ToString();
                        string toolId = toolUse["id"]?.ToString();
                        JObject toolInput = toolUse["input"] as JObject ?? new JObject();

                        // Notify panel about tool execution
                        MCPDockablePanel.Instance?.OnToolExecuting(toolName);

                        string result = await ExecuteMcpCommand(toolName, toolInput);

                        toolResults.Add(new JObject
                        {
                            ["type"] = "tool_result",
                            ["tool_use_id"] = toolId,
                            ["content"] = result
                        });
                    }

                    _conversationHistory.Add(new JObject { ["role"] = "user", ["content"] = toolResults });
                    continue; // Loop to get Claude's response after tool results
                }

                // No more tool calls — return text
                return string.Join("\n", textParts);
            }

            return "Troppe iterazioni di tool. Riprova con una richiesta più semplice.";
        }

        private async Task<JObject> CallClaudeApi()
        {
            var requestBody = new JObject
            {
                ["model"] = _model,
                ["max_tokens"] = _thinkingEnabled ? 16000 : 2048,
                ["system"] = SYSTEM_PROMPT,
                ["tools"] = GetToolDefinitions(),
                ["messages"] = JArray.FromObject(_conversationHistory)
            };

            if (_thinkingEnabled)
            {
                requestBody["thinking"] = new JObject
                {
                    ["type"] = "enabled",
                    ["budget_tokens"] = _thinkingBudget
                };
            }

            var request = (HttpWebRequest)WebRequest.Create(API_URL);
            request.Method = "POST";
            request.ContentType = "application/json";
            request.Headers.Add("x-api-key", _apiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");
            request.Timeout = _thinkingEnabled ? 180000 : 60000;

            byte[] data = Encoding.UTF8.GetBytes(requestBody.ToString());
            using (var stream = await Task.Factory.FromAsync(request.BeginGetRequestStream, request.EndGetRequestStream, null))
            {
                await stream.WriteAsync(data, 0, data.Length);
            }

            try
            {
                using (var response = (HttpWebResponse)await Task.Factory.FromAsync(request.BeginGetResponse, request.EndGetResponse, null))
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    string responseText = await reader.ReadToEndAsync();
                    return JObject.Parse(responseText);
                }
            }
            catch (WebException ex)
            {
                if (ex.Response != null)
                {
                    using (var reader = new StreamReader(ex.Response.GetResponseStream()))
                    {
                        string errorText = await reader.ReadToEndAsync();
                        var errorJson = JObject.Parse(errorText);
                        throw new Exception($"API Error: {errorJson["error"]?["message"]}");
                    }
                }
                throw;
            }
        }

        private async Task<string> ExecuteMcpCommand(string commandName, JObject parameters)
        {
            try
            {
                var jsonRpc = new JObject
                {
                    ["jsonrpc"] = "2.0",
                    ["id"] = Guid.NewGuid().ToString(),
                    ["method"] = commandName,
                    ["params"] = parameters
                };

                string request = jsonRpc.ToString(Formatting.None);

                using (var client = new TcpClient())
                {
                    // Connect with timeout
                    var connectTask = client.ConnectAsync("127.0.0.1", MCP_PORT);
                    if (await Task.WhenAny(connectTask, Task.Delay(10000)) != connectTask)
                        return "MCP command failed: Connection timeout (server not responding)";
                    await connectTask; // propagate any connection exception

                    var stream = client.GetStream();

                    byte[] requestData = Encoding.UTF8.GetBytes(request);
                    await stream.WriteAsync(requestData, 0, requestData.Length);

                    // Read response with streaming buffer (no 65KB limit)
                    byte[] buffer = new byte[8192];
                    var responseBuilder = new StringBuilder();
                    int bytesRead;

                    client.ReceiveTimeout = 120000;

                    // Read first chunk (blocks until data arrives)
                    bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    responseBuilder.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));

                    // Continue reading if more data is available
                    while (stream.DataAvailable)
                    {
                        bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                        if (bytesRead == 0) break;
                        responseBuilder.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
                    }

                    string responseStr = responseBuilder.ToString();
                    var responseJson = JObject.Parse(responseStr);

                    if (responseJson["result"] != null)
                        return responseJson["result"].ToString(Formatting.Indented);
                    if (responseJson["error"] != null)
                        return $"Error: {responseJson["error"]?["message"]}";

                    return responseStr;
                }
            }
            catch (Exception ex)
            {
                return $"MCP command failed: {ex.Message}";
            }
        }

        public void ClearHistory()
        {
            _conversationHistory.Clear();
        }

        private JArray _cachedToolDefinitions;

        private JArray GetToolDefinitions()
        {
            if (_cachedToolDefinitions != null)
                return _cachedToolDefinitions;

            _cachedToolDefinitions = LoadToolsFromCommandJson() ?? BuildFallbackTools();
            return _cachedToolDefinitions;
        }

        private JArray LoadToolsFromCommandJson()
        {
            try
            {
                // Find command.json relative to plugin DLL: Commands/RevitMCPCommandSet/command.json
                string dllDir = Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";
                string commandJsonPath = Path.Combine(dllDir, "Commands", "RevitMCPCommandSet", "command.json");

                if (!File.Exists(commandJsonPath))
                    return null;

                var json = JObject.Parse(File.ReadAllText(commandJsonPath));
                var commands = json["commands"] as JArray;
                if (commands == null || commands.Count == 0)
                    return null;

                var tools = new JArray();
                foreach (var cmd in commands)
                {
                    string name = cmd["commandName"]?.ToString();
                    string desc = cmd["description"]?.ToString();
                    if (string.IsNullOrEmpty(name)) continue;

                    tools.Add(new JObject
                    {
                        ["name"] = name,
                        ["description"] = desc ?? name,
                        ["input_schema"] = new JObject
                        {
                            ["type"] = "object",
                            ["properties"] = new JObject()
                        }
                    });
                }

                return tools.Count > 0 ? tools : null;
            }
            catch
            {
                return null;
            }
        }

        private JArray BuildFallbackTools()
        {
            // Minimal fallback if command.json is not found
            var tools = new JArray();
            var fallbackCommands = new[]
            {
                ("get_project_info", "Get project info: name, address, author, levels, phases, links"),
                ("analyze_model_statistics", "Analyze model: element counts by category, types, families, levels"),
                ("get_warnings", "Get all warnings/errors from the Revit model"),
                ("create_level", "Create levels at specified elevations (mm)"),
                ("create_line_based_element", "Create walls or other line-based elements (mm)"),
                ("create_room", "Create rooms at specified positions (mm)"),
                ("create_grid", "Create grid system with automatic spacing (mm)"),
                ("delete_element", "Delete elements by ID"),
                ("export_room_data", "Export all room data: name, number, level, area, volume"),
                ("get_materials", "List all project materials with color and properties"),
                ("purge_unused", "Identify and optionally remove unused families, types, materials"),
                ("say_hello", "Show a dialog in Revit with a message (connection test)")
            };

            foreach (var (name, desc) in fallbackCommands)
            {
                tools.Add(new JObject
                {
                    ["name"] = name,
                    ["description"] = desc,
                    ["input_schema"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject()
                    }
                });
            }

            return tools;
        }
    }
}
