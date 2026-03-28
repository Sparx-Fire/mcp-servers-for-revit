using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace revit_mcp_plugin.UI
{
    public class ClaudeApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly List<JObject> _conversationHistory = new List<JObject>();
        private string _apiKey;

        private const string API_URL = "https://api.anthropic.com/v1/messages";
        private const string MODEL = "claude-sonnet-4-20250514";
        private const string SYSTEM_PROMPT = @"Sei un assistente AI integrato in Autodesk Revit. Puoi eseguire comandi sul modello Revit attivo tramite il server MCP.

Quando l'utente ti chiede di fare qualcosa su Revit, spiega brevemente cosa farai e poi esegui il comando appropriato.
Rispondi in italiano in modo conciso. Se non puoi eseguire un'operazione, suggerisci come farlo.

Comandi disponibili (eseguibili tramite MCP):
- Creare livelli, viste (piante, sezioni, 3D), sheets, griglie
- Creare muri, pavimenti, stanze, elementi strutturali
- Modificare elementi (muovere, ruotare, copiare, eliminare)
- Leggere/scrivere parametri degli elementi
- Analizzare statistiche del modello
- Gestire revisioni, view templates, filtri vista
- Rinumerare elementi, batch rename
- Clash detection, purge unused, gestione CAD links
- Esportare dati (schedule, room data, material quantities)

Non hai accesso diretto ai comandi MCP da qui - descrivi cosa faresti e l'utente può copiare i comandi nel terminale MCP.";

        public ClaudeApiClient()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(60);
            LoadApiKey();
        }

        private void LoadApiKey()
        {
            // Try environment variable first
            _apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");

            if (string.IsNullOrEmpty(_apiKey))
            {
                // Try reading from config file
                try
                {
                    string configPath = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        ".claude", "api_key.txt");
                    if (System.IO.File.Exists(configPath))
                        _apiKey = System.IO.File.ReadAllText(configPath).Trim();
                }
                catch { }
            }
        }

        public async Task<string> SendMessage(string userMessage)
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                return "API key non configurata.\n\n" +
                       "Per usare la chat, imposta la chiave API in uno di questi modi:\n" +
                       "1. Variabile d'ambiente: ANTHROPIC_API_KEY\n" +
                       "2. File: %USERPROFILE%\\.claude\\api_key.txt\n\n" +
                       "Puoi ottenere una chiave su console.anthropic.com";
            }

            // Add user message to history
            _conversationHistory.Add(JObject.FromObject(new { role = "user", content = userMessage }));

            // Keep last 20 messages to avoid context overflow
            while (_conversationHistory.Count > 20)
                _conversationHistory.RemoveAt(0);

            try
            {
                var requestBody = new JObject
                {
                    ["model"] = MODEL,
                    ["max_tokens"] = 1024,
                    ["system"] = SYSTEM_PROMPT,
                    ["messages"] = JArray.FromObject(_conversationHistory)
                };

                var request = new HttpRequestMessage(HttpMethod.Post, API_URL);
                request.Headers.Add("x-api-key", _apiKey);
                request.Headers.Add("anthropic-version", "2023-06-01");
                request.Content = new StringContent(requestBody.ToString(), Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    var error = JObject.Parse(responseBody);
                    string errorMsg = error["error"]?["message"]?.ToString() ?? response.StatusCode.ToString();
                    return $"Errore API: {errorMsg}";
                }

                var result = JObject.Parse(responseBody);
                var content = result["content"] as JArray;
                if (content == null || content.Count == 0)
                    return "Nessuna risposta.";

                string assistantText = "";
                foreach (var block in content)
                {
                    if (block["type"]?.ToString() == "text")
                        assistantText += block["text"]?.ToString();
                }

                // Add assistant response to history
                _conversationHistory.Add(JObject.FromObject(new { role = "assistant", content = assistantText }));

                return assistantText;
            }
            catch (TaskCanceledException)
            {
                return "Timeout - la richiesta ha impiegato troppo tempo. Riprova.";
            }
            catch (HttpRequestException ex)
            {
                return $"Errore di connessione: {ex.Message}\n\nVerifica la connessione internet.";
            }
            catch (Exception ex)
            {
                return $"Errore: {ex.Message}";
            }
        }

        public void ClearHistory()
        {
            _conversationHistory.Clear();
        }
    }
}
