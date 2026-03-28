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
                    await client.ConnectAsync("127.0.0.1", MCP_PORT);
                    var stream = client.GetStream();

                    byte[] requestData = Encoding.UTF8.GetBytes(request);
                    await stream.WriteAsync(requestData, 0, requestData.Length);

                    // Read response
                    byte[] buffer = new byte[65536];
                    var responseBuilder = new StringBuilder();
                    int bytesRead;

                    client.ReceiveTimeout = 30000;
                    do
                    {
                        bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                        responseBuilder.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
                    }
                    while (stream.DataAvailable);

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

        private JArray GetToolDefinitions()
        {
            return JArray.Parse(@"[
  {
    ""name"": ""get_project_info"",
    ""description"": ""Ottieni info sul progetto Revit attivo: nome, indirizzo, autore, livelli, fasi, link."",
    ""input_schema"": {""type"": ""object"", ""properties"": {}}
  },
  {
    ""name"": ""analyze_model_statistics"",
    ""description"": ""Analizza il modello: conteggio elementi per categoria, tipi, famiglie, livelli."",
    ""input_schema"": {""type"": ""object"", ""properties"": {""includeDetailedTypes"": {""type"": ""boolean"", ""description"": ""Include dettaglio tipi (default: false)""}}}
  },
  {
    ""name"": ""get_warnings"",
    ""description"": ""Ottieni tutti i warning/errori del modello Revit."",
    ""input_schema"": {""type"": ""object"", ""properties"": {""maxWarnings"": {""type"": ""integer"", ""description"": ""Max warnings (default: 50)""}}}
  },
  {
    ""name"": ""create_level"",
    ""description"": ""Crea livelli a elevazioni specificate (mm). Ogni livello puo generare floor plan e ceiling plan."",
    ""input_schema"": {""type"": ""object"", ""required"": [""data""], ""properties"": {""data"": {""type"": ""array"", ""items"": {""type"": ""object"", ""required"": [""name"", ""elevation""], ""properties"": {""name"": {""type"": ""string""}, ""elevation"": {""type"": ""number"", ""description"": ""Elevazione in mm""}, ""createFloorPlan"": {""type"": ""boolean""}, ""createCeilingPlan"": {""type"": ""boolean""}}}}}}
  },
  {
    ""name"": ""create_view"",
    ""description"": ""Crea viste: FloorPlan, CeilingPlan, Section, 3D. Coordinate in mm."",
    ""input_schema"": {""type"": ""object"", ""required"": [""viewType""], ""properties"": {""viewType"": {""type"": ""string"", ""enum"": [""FloorPlan"", ""CeilingPlan"", ""Section"", ""3D""]}, ""name"": {""type"": ""string""}, ""scale"": {""type"": ""integer""}, ""detailLevel"": {""type"": ""string"", ""enum"": [""Coarse"", ""Medium"", ""Fine""]}, ""levelElevation"": {""type"": ""number""}, ""direction"": {""type"": ""object"", ""properties"": {""x"": {""type"": ""number""}, ""y"": {""type"": ""number""}, ""z"": {""type"": ""number""}}}}}
  },
  {
    ""name"": ""create_sheet"",
    ""description"": ""Crea sheet (tavole) con title block."",
    ""input_schema"": {""type"": ""object"", ""properties"": {""sheetNumber"": {""type"": ""string""}, ""sheetName"": {""type"": ""string""}}}
  },
  {
    ""name"": ""create_line_based_element"",
    ""description"": ""Crea muri o altri elementi lineari. Coordinate in mm."",
    ""input_schema"": {""type"": ""object"", ""required"": [""data""], ""properties"": {""data"": {""type"": ""array"", ""items"": {""type"": ""object"", ""required"": [""category"", ""locationLine"", ""thickness"", ""height"", ""baseLevel"", ""baseOffset""], ""properties"": {""category"": {""type"": ""string""}, ""locationLine"": {""type"": ""object"", ""properties"": {""p0"": {""type"": ""object"", ""properties"": {""x"": {""type"": ""number""}, ""y"": {""type"": ""number""}, ""z"": {""type"": ""number""}}}, ""p1"": {""type"": ""object"", ""properties"": {""x"": {""type"": ""number""}, ""y"": {""type"": ""number""}, ""z"": {""type"": ""number""}}}}}, ""thickness"": {""type"": ""number""}, ""height"": {""type"": ""number""}, ""baseLevel"": {""type"": ""number""}, ""baseOffset"": {""type"": ""number""}}}}}}
  },
  {
    ""name"": ""create_room"",
    ""description"": ""Crea stanze in posizioni specificate (mm). Devono essere dentro muri chiusi."",
    ""input_schema"": {""type"": ""object"", ""required"": [""data""], ""properties"": {""data"": {""type"": ""array"", ""items"": {""type"": ""object"", ""required"": [""name"", ""location""], ""properties"": {""name"": {""type"": ""string""}, ""number"": {""type"": ""string""}, ""location"": {""type"": ""object"", ""properties"": {""x"": {""type"": ""number""}, ""y"": {""type"": ""number""}, ""z"": {""type"": ""number""}}}}}}}}
  },
  {
    ""name"": ""create_grid"",
    ""description"": ""Crea sistema di griglie con spaziatura automatica (mm)."",
    ""input_schema"": {""type"": ""object"", ""required"": [""xCount"", ""xSpacing"", ""yCount"", ""ySpacing""], ""properties"": {""xCount"": {""type"": ""integer""}, ""xSpacing"": {""type"": ""number""}, ""yCount"": {""type"": ""integer""}, ""ySpacing"": {""type"": ""number""}, ""xStartLabel"": {""type"": ""string""}, ""yStartLabel"": {""type"": ""string""}, ""xStartPosition"": {""type"": ""number""}, ""yStartPosition"": {""type"": ""number""}}}
  },
  {
    ""name"": ""create_schedule"",
    ""description"": ""Crea schedule (abachi) per categoria di elementi."",
    ""input_schema"": {""type"": ""object"", ""required"": [""categoryName""], ""properties"": {""categoryName"": {""type"": ""string"", ""description"": ""Es: OST_Walls, OST_Doors, OST_StructuralColumns""}, ""name"": {""type"": ""string""}, ""fields"": {""type"": ""array"", ""items"": {""type"": ""object"", ""properties"": {""parameterName"": {""type"": ""string""}}}}}}
  },
  {
    ""name"": ""batch_rename"",
    ""description"": ""Rinomina in batch viste, sheet, livelli, griglie o stanze."",
    ""input_schema"": {""type"": ""object"", ""properties"": {""targetCategory"": {""type"": ""string"", ""enum"": [""Views"", ""Sheets"", ""Levels"", ""Grids"", ""Rooms""]}, ""findText"": {""type"": ""string""}, ""replaceText"": {""type"": ""string""}, ""prefix"": {""type"": ""string""}, ""suffix"": {""type"": ""string""}, ""dryRun"": {""type"": ""boolean""}}}
  },
  {
    ""name"": ""delete_element"",
    ""description"": ""Elimina elementi per ID."",
    ""input_schema"": {""type"": ""object"", ""required"": [""elementIds""], ""properties"": {""elementIds"": {""type"": ""array"", ""items"": {""type"": ""integer""}}}}
  },
  {
    ""name"": ""export_room_data"",
    ""description"": ""Esporta dati di tutte le stanze: nome, numero, livello, area, volume, perimetro."",
    ""input_schema"": {""type"": ""object"", ""properties"": {}}
  },
  {
    ""name"": ""get_materials"",
    ""description"": ""Lista tutti i materiali del progetto con colore e proprieta."",
    ""input_schema"": {""type"": ""object"", ""properties"": {}}
  },
  {
    ""name"": ""purge_unused"",
    ""description"": ""Identifica e opzionalmente rimuovi famiglie, tipi e materiali non usati."",
    ""input_schema"": {""type"": ""object"", ""properties"": {""dryRun"": {""type"": ""boolean"", ""description"": ""true = solo preview, false = elimina""}}}
  },
  {
    ""name"": ""say_hello"",
    ""description"": ""Mostra un dialog in Revit con un messaggio. Utile per test di connessione."",
    ""input_schema"": {""type"": ""object"", ""properties"": {""message"": {""type"": ""string""}}}
  }
]");
        }
    }
}
