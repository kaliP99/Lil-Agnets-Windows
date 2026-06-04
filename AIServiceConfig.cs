using System;

namespace LilAgentsWindows
{
    internal class AIServiceConfig
    {
        public string Provider { get; set; } = AppConfig.DefaultProvider;
        public string ModelName { get; set; } = AppConfig.DefaultModel;
        public string Endpoint { get; set; } = AppConfig.DefaultEndpoint;
        public string ApiKey { get; set; } = AppConfig.DefaultApiKey;
        public int TimeoutSeconds { get; set; } = 30;
        public bool Enabled { get; set; } = true;

        // Ollama specific
        public bool IsOllama => Provider == "OLLAMA";
        public bool IsNvidiaNim => Provider == "NVIDIA_NIM";
        public bool IsOpenAICompatible => Provider == "OPENAI_COMPATIBLE";
        public bool IsCustom => Provider == "CUSTOM";

        public string GetFormattedEndpoint()
        {
            if (IsOllama && !Endpoint.EndsWith("/api/generate", StringComparison.OrdinalIgnoreCase))
            {
                // Ensure Ollama endpoint ends with /api/generate
                var baseUrl = Endpoint.TrimEnd('/');
                return $"{baseUrl}/api/generate";
            }

            return Endpoint;
        }
    }
}
