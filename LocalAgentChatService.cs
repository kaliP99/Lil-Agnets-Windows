using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LilAgentsWindows
{
    internal class LocalAgentChatService
    {
        private static readonly HttpClient Http = new()
        {
            Timeout = TimeSpan.FromMinutes(2) // Reduced from 5 minutes
        };
        private readonly string _baseUrl;
        private readonly string _model;
        private readonly string _apiKey;
        private readonly string _fallbackBaseUrl;
        private readonly string _fallbackModel;
        private readonly string _fallbackApiKey;
        private readonly bool _enableFallback;
        private int _retryCount = 0;
        private const int MaxRetries = 3;

        public LocalAgentChatService(Agent agent)
        {
            // Primary AI configuration
            _baseUrl = GetParameterValue(agent.Endpoint, EnvironmentVariableTarget.User) ??
                      GetParameterValue(agent.Endpoint, EnvironmentVariableTarget.Process) ??
                      (string.IsNullOrWhiteSpace(agent.Endpoint) ? null : agent.Endpoint) ?? 
                      AppConfig.DefaultEndpoint;

            _model = GetParameterValue(agent.ModelName, EnvironmentVariableTarget.User) ??
                    GetParameterValue(agent.ModelName, EnvironmentVariableTarget.Process) ??
                    (string.IsNullOrWhiteSpace(agent.ModelName) ? null : agent.ModelName) ?? 
                    AppConfig.DefaultModel;

            _apiKey = GetParameterValue(agent.ApiKey, EnvironmentVariableTarget.User) ??
                     GetParameterValue(agent.ApiKey, EnvironmentVariableTarget.Process) ??
                     (string.IsNullOrWhiteSpace(agent.ApiKey) ? null : agent.ApiKey) ?? 
                     AppConfig.DefaultApiKey;

            // Fallback AI configuration
            _fallbackBaseUrl = GetParameterValue(agent.FallbackEndpoint, EnvironmentVariableTarget.User) ??
                              GetParameterValue(agent.FallbackEndpoint, EnvironmentVariableTarget.Process) ??
                              (string.IsNullOrWhiteSpace(agent.FallbackEndpoint) ? null : agent.FallbackEndpoint) ?? 
                              AppConfig.DefaultEndpoint;

            _fallbackModel = GetParameterValue(agent.FallbackModelName, EnvironmentVariableTarget.User) ??
                             GetParameterValue(agent.FallbackModelName, EnvironmentVariableTarget.Process) ??
                             (string.IsNullOrWhiteSpace(agent.FallbackModelName) ? null : agent.FallbackModelName) ?? 
                             AppConfig.DefaultModel;

            _fallbackApiKey = GetParameterValue(agent.FallbackApiKey, EnvironmentVariableTarget.User) ??
                              GetParameterValue(agent.FallbackApiKey, EnvironmentVariableTarget.Process) ??
                              (string.IsNullOrWhiteSpace(agent.FallbackApiKey) ? null : agent.FallbackApiKey) ?? 
                              AppConfig.DefaultApiKey;

            _enableFallback = agent.EnableFallback && !IsSameRoute(_baseUrl, _model, _fallbackBaseUrl, _fallbackModel);
        }

        private string? GetParameterValue(string? param, EnvironmentVariableTarget target)
        {
            if (string.IsNullOrWhiteSpace(param))
                return null;

            // Check if it looks like an environment variable reference (e.g., %VAR_NAME% or $VAR_NAME)
            if ((param.StartsWith("%") && param.EndsWith("%")) ||
                (param.StartsWith("$") && param.Length > 1))
            {
                string varName = param.TrimStart('%', '$').TrimEnd('%');
                return Environment.GetEnvironmentVariable(varName, target);
            }

            return param; // Return as-is if not an env var reference
        }

        public string BaseUrl => _baseUrl;
        public string Model => _model;
        public string FallbackBaseUrl => _fallbackBaseUrl;
        public string FallbackModel => _fallbackModel;
        public bool EnableFallback => _enableFallback;

        public async Task<string> SendStreamingAsync(
            Agent agent,
            IReadOnlyList<ChatMessage> history,
            Action<string> onDelta,
            CancellationToken cancellationToken = default)
        {
            _retryCount = 0;
            return await TrySendWithFallback(agent, history, onDelta, cancellationToken, useFallback: false);
        }

        private async Task<string> TrySendWithFallback(
            Agent agent,
            IReadOnlyList<ChatMessage> history,
            Action<string> onDelta,
            CancellationToken cancellationToken,
            bool useFallback)
        {
            try
            {
                // Select configuration based on whether we're using fallback
                string baseUrl = useFallback ? _fallbackBaseUrl : _baseUrl;
                string model = useFallback ? _fallbackModel : _model;
                string apiKey = useFallback ? _fallbackApiKey : _apiKey;
                string providerName = useFallback ? "Fallback" : "Primary";

                return await SendRequestWithRetry(agent, history, onDelta, baseUrl, model, apiKey, providerName, cancellationToken);
            }
            catch
            {
                // If we're not using fallback and fallback is enabled, try fallback
                if (!useFallback && _enableFallback)
                {
                    _retryCount = 0;
                    return await TrySendWithFallback(agent, history, onDelta, cancellationToken, useFallback: true);
                }

                // If we've exhausted retry attempts or fallback didn't work, throw the exception
                throw;
            }
        }

        private async Task<string> SendRequestWithRetry(
            Agent agent,
            IReadOnlyList<ChatMessage> history,
            Action<string> onDelta,
            string baseUrl,
            string model,
            string apiKey,
            string providerName,
            CancellationToken cancellationToken)
        {
            while (_retryCount <= MaxRetries)
            {
                try
                {
                    return await SendSingleRequest(agent, history, onDelta, baseUrl, model, apiKey, providerName, cancellationToken);
                }
                catch when (_retryCount < MaxRetries)
                {
                    _retryCount++;

                    // Exponential backoff with jitter
                    int delayMs = (int)Math.Pow(2, _retryCount) * 100 + new Random().Next(0, 100);
                    await Task.Delay(delayMs, cancellationToken);

                    // Continue to retry
                }
            }

            // If we get here, we've exhausted retries
            throw new InvalidOperationException($"Failed to get response from {providerName} AI after {MaxRetries} attempts");
        }

        private async Task<string> SendSingleRequest(
            Agent agent,
            IReadOnlyList<ChatMessage> history,
            Action<string> onDelta,
            string baseUrl,
            string model,
            string apiKey,
            string providerName,
            CancellationToken cancellationToken)
        {
            bool isFallback = providerName == "Fallback";
            string providerStr = isFallback ? agent.FallbackAIProvider : agent.AIProvider;
            Enum.TryParse<AIProvider>(providerStr, true, out var providerType);

            return providerType switch
            {
                AIProvider.OLLAMA => await SendOllamaRequest(agent, history, onDelta, baseUrl, model, apiKey, providerName, cancellationToken),
                AIProvider.OPENAI => await SendOpenAICompatibleRequest(agent, history, onDelta, baseUrl, model, apiKey, providerName, cancellationToken),
                AIProvider.OPENAI_COMPATIBLE => await SendOpenAICompatibleRequest(agent, history, onDelta, baseUrl, model, apiKey, providerName, cancellationToken),
                AIProvider.ANTHROPIC => await SendAnthropicRequest(agent, history, onDelta, baseUrl, model, apiKey, providerName, cancellationToken),
                AIProvider.CUSTOM => await SendCustomRequest(agent, history, onDelta, baseUrl, model, apiKey, providerName, cancellationToken),
                _ => await SendAnthropicRequest(agent, history, onDelta, baseUrl, model, apiKey, providerName, cancellationToken) // Default to NVIDIA_NIM format
            };
        }

        private async Task<string> SendAnthropicRequest(
            Agent agent,
            IReadOnlyList<ChatMessage> history,
            Action<string> onDelta,
            string baseUrl,
            string model,
            string apiKey,
            string providerName,
            CancellationToken cancellationToken)
        {
            if (agent == null)
                throw new ArgumentNullException(nameof(agent));

            var system = $"""
You are {agent.CharacterName}, a tiny desktop AI companion running through the user's local Claude Code / Free Claude Code proxy.

Backend route:
{model}

Character context:
{agent.Context}

Style:
- Keep responses extremely brief and concise. Avoid unnecessary preamble or conversational fluff. Reply almost immediately.
- Be useful and direct.
- Keep answers friendly and practical.
- Stay in character, but do not overdo roleplay.
- If the user asks for code or steps, give clear actionable steps.

CRITICAL: You are participating in a multi-agent group conversation. You must ONLY speak as yourself ({agent.CharacterName}). Do NOT simulate dialogue for other agents, write replies on behalf of other agents, or list other agent names like '{agent.CharacterName}: ...' or other names. Generate ONLY your own response.
""";

            var messages = history.Select(message => {
                if (message.Images != null && message.Images.Count > 0)
                {
                    var contentList = new List<object>
                    {
                        new { type = "text", text = message.Content }
                    };
                    foreach (var img in message.Images)
                    {
                        string base64Data = img;
                        string mediaType = "image/png";
                        if (img.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                        {
                            int commaIdx = img.IndexOf(',');
                            if (commaIdx != -1)
                            {
                                base64Data = img[(commaIdx + 1)..];
                                int semiIdx = img.IndexOf(';');
                                if (semiIdx != -1 && semiIdx > 5)
                                {
                                    mediaType = img[5..semiIdx];
                                }
                            }
                        }
                        contentList.Add(new
                        {
                            type = "image",
                            source = new
                            {
                                type = "base64",
                                media_type = mediaType,
                                data = base64Data
                            }
                        });
                    }
                    return (object)new
                    {
                        role = message.Role,
                        content = contentList
                    };
                }
                else
                {
                    return (object)new
                    {
                        role = message.Role,
                        content = message.Content
                    };
                }
            }).ToList();

            var request = new
            {
                model = model,
                system,
                messages,
                temperature = agent.Temperature,
                max_tokens = GetMaxTokens(agent),
                stream = true
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, baseUrl);
            bool isFallback = providerName == "Fallback";
            string providerStr = isFallback ? agent.FallbackAIProvider : agent.AIProvider;
            Enum.TryParse<AIProvider>(providerStr, true, out var providerType);

            if (providerType != AIProvider.ANTHROPIC)
            {
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            }
            httpRequest.Headers.Add("anthropic-version", "2023-06-01");
            httpRequest.Headers.Add("x-api-key", apiKey);
            httpRequest.Content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

            using var response = await Http.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"{providerName} proxy error {(int)response.StatusCode}: {ExtractError(body)}");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);
            var builder = new StringBuilder();

            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    break;
                }

                var delta = ExtractSseDelta(line);
                if (string.IsNullOrEmpty(delta))
                {
                    continue;
                }

                builder.Append(delta);
                onDelta(delta);
            }

            var parsed = builder.ToString().Trim();
            return string.IsNullOrWhiteSpace(parsed)
                ? $"I got a response from the {providerName.ToLower()} AI, but could not read the answer text."
                : parsed;
        }

        private async Task<string> SendOllamaRequest(
            Agent agent,
            IReadOnlyList<ChatMessage> history,
            Action<string> onDelta,
            string baseUrl,
            string model,
            string apiKey,
            string providerName,
            CancellationToken cancellationToken)
        {
            // Build Ollama-compatible prompt from conversation history
            var promptBuilder = new StringBuilder();
            promptBuilder.AppendLine($"You are {agent.CharacterName}, a tiny desktop AI companion.");
            promptBuilder.AppendLine($"Character context: {agent.Context}");
            promptBuilder.AppendLine("Be useful, direct, friendly, and practical.");
            promptBuilder.AppendLine("You must answer immediately and keep replies extremely short and direct (typically 1-3 sentences). Avoid long explanations.");
            promptBuilder.AppendLine($"CRITICAL: You are participating in a multi-agent group conversation. You must ONLY speak as yourself ({agent.CharacterName}). Do NOT simulate dialogue for other agents, write replies on behalf of other agents, or list other agent names like '{agent.CharacterName}: ...' or other names. Generate ONLY your own response.");
            promptBuilder.AppendLine();

            // Add conversation history
            foreach (var message in history)
            {
                if (message.Role == "user")
                {
                    promptBuilder.AppendLine($"User: {message.Content}");
                }
                else if (message.Role == "assistant")
                {
                    promptBuilder.AppendLine($"Assistant: {message.Content}");
                }
            }

            // Add current user message if we're building from history
            // (Note: the latest message should already be in history when this is called)

            promptBuilder.AppendLine("Assistant:");

            List<string>? ollamaImages = null;
            var lastUserMsg = history.LastOrDefault(m => m.Role == "user");
            if (lastUserMsg != null && lastUserMsg.Images != null && lastUserMsg.Images.Count > 0)
            {
                ollamaImages = new List<string>();
                foreach (var img in lastUserMsg.Images)
                {
                    string base64Data = img;
                    if (img.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    {
                        int commaIdx = img.IndexOf(',');
                        if (commaIdx != -1)
                        {
                            base64Data = img[(commaIdx + 1)..];
                        }
                    }
                    ollamaImages.Add(base64Data);
                }
            }

            var request = new
            {
                model = model,
                prompt = promptBuilder.ToString().Trim(),
                images = ollamaImages,
                stream = true,
                options = new
                {
                    temperature = agent.Temperature,
                    num_predict = GetMaxTokens(agent)
                }
            };

            // For local Ollama, API key is often not required, but we'll include it if provided
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, NormalizeOllamaEndpoint(baseUrl));
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            }
            httpRequest.Content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

            using var response = await Http.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"{providerName} Ollama error {(int)response.StatusCode}: {ExtractOllamaError(body)}");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);
            var builder = new StringBuilder();

            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    break;
                }

                var delta = ExtractOllamaDelta(line);
                if (string.IsNullOrEmpty(delta))
                {
                    continue;
                }

                builder.Append(delta);
                onDelta(delta);
            }

            var parsed = builder.ToString().Trim();
            return string.IsNullOrWhiteSpace(parsed)
                ? $"I got a response from the {providerName.ToLower()} AI, but could not read the answer text."
                : parsed;
        }

        private async Task<string> SendOpenAICompatibleRequest(
            Agent agent,
            IReadOnlyList<ChatMessage> history,
            Action<string> onDelta,
            string baseUrl,
            string model,
            string apiKey,
            string providerName,
            CancellationToken cancellationToken)
        {
            var messages = new List<object>
            {
                new
                {
                    role = "system",
                    content = $"""
You are {agent.CharacterName}, a tiny desktop AI companion.

Character context:
{agent.Context}

Be useful, direct, friendly, and practical.
Keep your responses extremely brief, concise, and direct (typically 1-3 sentences). Reply immediately. Do not write filler.

CRITICAL: You are participating in a multi-agent group conversation. You must ONLY speak as yourself ({agent.CharacterName}). Do NOT simulate dialogue for other agents, write replies on behalf of other agents, or list other agent names like '{agent.CharacterName}: ...' or other names. Generate ONLY your own response.
"""
                }
            };

            messages.AddRange(history.Select(message => {
                if (message.Images != null && message.Images.Count > 0)
                {
                    var contentList = new List<object>
                    {
                        new { type = "text", text = message.Content }
                    };
                    foreach (var img in message.Images)
                    {
                        contentList.Add(new
                        {
                            type = "image_url",
                            image_url = new { url = img }
                        });
                    }
                    return (object)new
                    {
                        role = message.Role,
                        content = contentList
                    };
                }
                else
                {
                    return (object)new
                    {
                        role = message.Role,
                        content = message.Content
                    };
                }
            }));

            var request = new
            {
                model = model,
                messages,
                temperature = agent.Temperature,
                max_tokens = GetMaxTokens(agent),
                stream = true
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, baseUrl);
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            }
            httpRequest.Content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

            using var response = await Http.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"{providerName} OpenAI-compatible error {(int)response.StatusCode}: {ExtractOpenAIError(body)}");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);
            var builder = new StringBuilder();

            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    break;
                }

                var delta = ExtractOpenAIDelta(line);
                if (string.IsNullOrEmpty(delta))
                {
                    continue;
                }

                builder.Append(delta);
                onDelta(delta);
            }

            var parsed = builder.ToString().Trim();
            return string.IsNullOrWhiteSpace(parsed)
                ? $"I got a response from the {providerName.ToLower()} AI, but could not read the answer text."
                : parsed;
        }

        private async Task<string> SendCustomRequest(
            Agent agent,
            IReadOnlyList<ChatMessage> history,
            Action<string> onDelta,
            string baseUrl,
            string model,
            string apiKey,
            string providerName,
            CancellationToken cancellationToken)
        {
            // For custom providers, default to Anthropic format but allow configuration
            // This could be extended to support different formats based on configuration
            return await SendAnthropicRequest(agent, history, onDelta, baseUrl, model, apiKey, providerName, cancellationToken);
        }

        private static string ExtractOllamaDelta(string line)
        {
            line = line.Trim();
            if (string.IsNullOrEmpty(line))
            {
                return string.Empty;
            }

            try
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;

                if (root.TryGetProperty("response", out var response) &&
                    response.ValueKind == JsonValueKind.String)
                {
                    return response.GetString() ?? string.Empty;
                }

                // Check if done
                if (root.TryGetProperty("done", out var done) &&
                    done.GetBoolean())
                {
                    return string.Empty; // End of stream
                }
            }
            catch
            {
                // If not valid JSON, return empty
                return string.Empty;
            }

            return string.Empty;
        }

        private static string NormalizeOllamaEndpoint(string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                return "http://127.0.0.1:11434/api/generate";
            }

            var trimmed = endpoint.TrimEnd('/');
            if (trimmed.EndsWith("/api/generate", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed;
            }

            if (trimmed.EndsWith("/v1/chat/completions", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed[..^"/v1/chat/completions".Length] + "/api/generate";
            }

            if (trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed[..^"/v1".Length] + "/api/generate";
            }

            return trimmed + "/api/generate";
        }

        private static string ExtractOpenAIDelta(string line)
        {
            line = line.Trim();
            if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            var data = line[5..].Trim();
            if (string.IsNullOrWhiteSpace(data) || data == "[DONE]")
            {
                return string.Empty;
            }

            try
            {
                using var document = JsonDocument.Parse(data);
                var root = document.RootElement;

                if (root.TryGetProperty("choices", out var choices) &&
                    choices.ValueKind == JsonValueKind.Array &&
                    choices.GetArrayLength() > 0)
                {
                    var choice = choices[0];
                    if (choice.TryGetProperty("delta", out var delta) &&
                        delta.TryGetProperty("content", out var content) &&
                        content.ValueKind == JsonValueKind.String)
                    {
                        return content.GetString() ?? string.Empty;
                    }
                }
            }
            catch
            {
                return string.Empty;
            }

            return string.Empty;
        }

        private static string ExtractOllamaError(string json)
        {
            try
            {
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.TryGetProperty("error", out var error))
                {
                    if (error.ValueKind == JsonValueKind.String)
                    {
                        return error.GetString() ?? json;
                    }
                    else if (error.ValueKind == JsonValueKind.Object &&
                             error.TryGetProperty("message", out var message))
                    {
                        return message.GetString() ?? json;
                    }
                }
            }
            catch
            {
                return json;
            }

            return json;
        }

        private static string ExtractOpenAIError(string json)
        {
            try
            {
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.TryGetProperty("error", out var error))
                {
                    if (error.ValueKind == JsonValueKind.Object &&
                        error.TryGetProperty("message", out var message))
                    {
                        return message.GetString() ?? json;
                    }
                    else if (error.ValueKind == JsonValueKind.String)
                    {
                        return error.GetString() ?? json;
                    }
                }
            }
            catch
            {
                return json;
            }

            return json;
        }

        private static string ExtractOutputText(string json)
        {
            if (json.Contains("data:"))
            {
                return ExtractSseOutputText(json);
            }

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.TryGetProperty("content", out var content) &&
                content.ValueKind == JsonValueKind.Array)
            {
                var builder = new StringBuilder();

                foreach (var block in content.EnumerateArray())
                {
                    if (block.TryGetProperty("type", out var type) &&
                        type.GetString() == "text" &&
                        block.TryGetProperty("text", out var text))
                    {
                        builder.Append(text.GetString());
                    }
                }

                var parsed = builder.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(parsed))
                {
                    return parsed;
                }
            }

            return "I got a response from the local proxy, but could not read the answer text.";
        }

        private static string ExtractSseOutputText(string sse)
        {
            var builder = new StringBuilder();
            var lines = sse.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var data = line[5..].Trim();
                if (string.IsNullOrWhiteSpace(data) || data == "[DONE]")
                {
                    continue;
                }

                try
                {
                    using var document = JsonDocument.Parse(data);
                    var root = document.RootElement;

                    if (root.TryGetProperty("type", out var type) &&
                        type.GetString() == "content_block_delta" &&
                        root.TryGetProperty("delta", out var delta) &&
                        delta.TryGetProperty("type", out var deltaType) &&
                        deltaType.GetString() == "text_delta" &&
                        delta.TryGetProperty("text", out var text))
                    {
                        builder.Append(text.GetString());
                    }
                }
                catch
                {
                    // Ignore non-JSON SSE lines.
                }
            }

            var parsed = builder.ToString().Trim();
            return string.IsNullOrWhiteSpace(parsed)
                ? "I got a streaming response from the proxy, but could not read the text."
                : parsed;
        }

        private static string ExtractSseDelta(string line)
        {
            line = line.Trim();
            if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            var data = line[5..].Trim();
            if (string.IsNullOrWhiteSpace(data) || data == "[DONE]")
            {
                return string.Empty;
            }

            try
            {
                using var document = JsonDocument.Parse(data);
                var root = document.RootElement;

                if (root.TryGetProperty("type", out var type) &&
                    type.GetString() == "content_block_delta" &&
                    root.TryGetProperty("delta", out var delta) &&
                    delta.TryGetProperty("type", out var deltaType) &&
                    deltaType.GetString() == "text_delta" &&
                    delta.TryGetProperty("text", out var text))
                {
                    return text.GetString() ?? string.Empty;
                }
            }
            catch
            {
                return string.Empty;
            }

            return string.Empty;
        }

        private static string ExtractError(string json)
        {
            try
            {
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.TryGetProperty("error", out var error))
                {
                    if (error.ValueKind == JsonValueKind.Object &&
                        error.TryGetProperty("message", out var message))
                    {
                        return message.GetString() ?? json;
                    }

                    if (error.ValueKind == JsonValueKind.String)
                    {
                        return error.GetString() ?? json;
                    }
                }
            }
            catch
            {
                return json;
            }

            return json;
        }

        private static string? ReadFccEnvValue(string key)
        {
            try
            {
                var path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".fcc",
                    ".env");

                if (!File.Exists(path))
                {
                    return null;
                }

                foreach (var line in File.ReadLines(path))
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("#") || !trimmed.StartsWith($"{key}=", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var value = trimmed[(key.Length + 1)..].Trim().Trim('"');
                    return string.IsNullOrWhiteSpace(value) ? null : value;
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        private static bool IsSameRoute(string baseUrl, string model, string fallbackBaseUrl, string fallbackModel)
        {
            return string.Equals(baseUrl.TrimEnd('/'), fallbackBaseUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)
                && string.Equals(model.Trim(), fallbackModel.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        public async Task<bool> CheckConnectionAsync(Agent agent)
        {
            try
            {
                if (agent == null) return false;
                Enum.TryParse<AIProvider>(agent.AIProvider, true, out var providerType);
                string url = _baseUrl;
                string modelName = _model;
                string apiKey = _apiKey;

                if (providerType == AIProvider.OLLAMA)
                {
                    string checkUrl = NormalizeOllamaEndpoint(url);
                    var checkRequest = new
                    {
                        model = modelName,
                        prompt = "ping",
                        stream = false,
                        options = new
                        {
                            num_predict = 1
                        }
                    };

                    using var request = new HttpRequestMessage(HttpMethod.Post, checkUrl);
                    if (!string.IsNullOrWhiteSpace(apiKey))
                    {
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                    }
                    request.Content = new StringContent(JsonSerializer.Serialize(checkRequest), Encoding.UTF8, "application/json");

                    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                    using var res = await client.SendAsync(request);
                    return res.IsSuccessStatusCode;
                }
                else if (providerType == AIProvider.ANTHROPIC)
                {
                    var checkRequest = new
                    {
                        model = modelName,
                        messages = new[] { new { role = "user", content = "ping" } },
                        max_tokens = 1,
                        stream = false
                    };

                    using var request = new HttpRequestMessage(HttpMethod.Post, url);
                    if (!string.IsNullOrWhiteSpace(apiKey))
                    {
                        request.Headers.Add("x-api-key", apiKey);
                    }
                    request.Headers.Add("anthropic-version", "2023-06-01");
                    request.Content = new StringContent(JsonSerializer.Serialize(checkRequest), Encoding.UTF8, "application/json");

                    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                    using var res = await client.SendAsync(request);
                    return res.IsSuccessStatusCode;
                }
                else
                {
                    // OpenAI, OpenAI Compatible, NVIDIA_NIM, CUSTOM
                    var checkRequest = new
                    {
                        model = modelName,
                        messages = new[] { new { role = "user", content = "ping" } },
                        max_tokens = 1,
                        stream = false
                    };

                    using var request = new HttpRequestMessage(HttpMethod.Post, url);
                    if (!string.IsNullOrWhiteSpace(apiKey))
                    {
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                    }
                    request.Content = new StringContent(JsonSerializer.Serialize(checkRequest), Encoding.UTF8, "application/json");

                    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                    using var res = await client.SendAsync(request);
                    return res.IsSuccessStatusCode;
                }
            }
            catch
            {
                return false;
            }
        }

        private static int GetMaxTokens(Agent agent)
        {
            var raw = Environment.GetEnvironmentVariable("LIL_AGENTS_MAX_TOKENS", EnvironmentVariableTarget.User)
                ?? Environment.GetEnvironmentVariable("LIL_AGENTS_MAX_TOKENS");

            return int.TryParse(raw, out var parsed)
                ? Math.Clamp(parsed, 128, 4000)
                : Math.Clamp(agent.MaxTokens, 128, 4000);
        }
    }

    public class ChatMessage
    {
        public string Role { get; set; } = "";
        public string Content { get; set; } = "";
        public List<string>? Images { get; set; }

        public ChatMessage() { }
        public ChatMessage(string role, string content)
        {
            Role = role;
            Content = content;
        }
    }
}
