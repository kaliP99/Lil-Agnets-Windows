using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.Json;

namespace LilAgentsWindows
{
    public enum CharacterSize
    {
        Small,
        Medium,
        Large
    }

    internal class AgentDefinition
    {
        public string CharacterName { get; set; } = "Nova";
        public string ToolName { get; set; } = AppConfig.DefaultToolName;
        public string Command { get; set; } = "local";
        public string Context { get; set; } = "A careful coding agent.";
        public string ColorHex { get; set; } = "#50A0FF";
        public List<string> WalkingFrames { get; set; } = new();
        public List<string> ThinkingFrames { get; set; } = new();
        public List<string> SleepingFrames { get; set; } = new();
        public List<string> HandshakeFrames { get; set; } = new();
        public List<string> FallingFrames { get; set; } = new();
        public List<string> ExcitedFrames { get; set; } = new();
        public List<string> SittingFrames { get; set; } = new();
        public List<string> StretchingFrames { get; set; } = new();
        public List<string> YawningFrames { get; set; } = new();
        public List<string> LookingAroundFrames { get; set; } = new();
        public List<string> FlyingFrames { get; set; } = new();
        public List<string> RunningFrames { get; set; } = new();
        public string CharacterSize { get; set; } = "Medium";
        public string MovementType { get; set; } = "FreeRoam";
        public WalkAreaSettings WalkArea { get; set; } = new();

        // AI Configuration Properties
        public string AIProvider { get; set; } = AppConfig.DefaultProvider;
        public string ModelName { get; set; } = AppConfig.DefaultModel;
        public string Endpoint { get; set; } = AppConfig.DefaultEndpoint;
        public string ApiKey { get; set; } = AppConfig.DefaultApiKey;

        // Fallback AI Configuration
        public string FallbackAIProvider { get; set; } = "OLLAMA";
        public string FallbackModelName { get; set; } = AppConfig.DefaultModel;
        public string FallbackEndpoint { get; set; } = "http://127.0.0.1:11434/api/generate";
        public string FallbackApiKey { get; set; } = AppConfig.DefaultApiKey;
        public bool EnableFallback { get; set; } = false;

        public double Temperature { get; set; } = 0.7;
        public int MaxTokens { get; set; } = 700;

        // Customizable physics properties
        public double ThudVelocityThreshold { get; set; } = 10.0;
        public double RollVelocityThreshold { get; set; } = 5.0;
        public int ThudRecoveryDuration { get; set; } = 1200;
        public int RollRecoveryDuration { get; set; } = 600;
        public double SlideOffProbability { get; set; } = 0.4;
        public double SlideOffSpeedThreshold { get; set; } = 15.0;
        public bool EnableWindowPushing { get; set; } = true;
        public double MaxFlingSpeed { get; set; } = 35.0;
    }

    internal class WalkAreaSettings
    {
        public bool Enabled { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public Rectangle ToRectangle(Rectangle fallback)
        {
            if (!Enabled || Width <= 40 || Height <= 40)
            {
                return fallback;
            }

            return new Rectangle(X, Y, Width, Height);
        }
    }

    internal class AgentGroup
    {
        public string GroupName { get; set; } = "New Group";
        public List<string> MemberNames { get; set; } = new();
    }

    internal class AppConfig
    {
        public const string DefaultProvider = "OLLAMA";
        public const string DefaultToolName = "Ollama Gemma4";
        public const string DefaultModel = "gemma4:latest";
        public const string DefaultEndpoint = "http://127.0.0.1:11434/api/generate";
        public const string DefaultApiKey = "";

        public List<AgentDefinition> Agents { get; set; } = new();
        public List<AgentGroup> Groups { get; set; } = new();

        public int ChatWidth { get; set; } = 480;
        public int ChatHeight { get; set; } = 630;
        public int ChatLeft { get; set; } = -1;
        public int ChatTop { get; set; } = -1;

        public string Theme { get; set; } = "Dark";
        public string AccentColorHex { get; set; } = "#50A0FF";
        public double AnimationIntensity { get; set; } = 1.0;
        public string ChatLayout { get; set; } = "Standard";
        public bool KeepChatOnTop { get; set; } = true;
        public bool SnapToEdges { get; set; } = true;
        public bool CloseToTray { get; set; } = true;
        public bool ShowQuickActions { get; set; } = true;
        public bool ShowHeaderSelectors { get; set; } = true;

        public static string ConfigFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LilAgentsWindows");

        public static string ConfigPath => Path.Combine(ConfigFolder, "agents.json");

        public static AppConfig Load()
        {
            try
            {
                Directory.CreateDirectory(ConfigFolder);
                if (File.Exists(ConfigPath))
                {
                    var loaded = JsonSerializer.Deserialize<AppConfig>(
                        File.ReadAllText(ConfigPath),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (loaded is not null && loaded.Agents.Count > 0)
                    {
                        // Migrate old configurations to half-tripled dimensions (480x630)
                        if (loaded.ChatWidth <= 0 || loaded.ChatWidth == 320 || loaded.ChatWidth == 960 || loaded.ChatWidth == 455 || loaded.ChatWidth == 480 || loaded.ChatWidth == 380 || loaded.ChatWidth == 1072 || loaded.ChatWidth == 1100)
                        {
                            loaded.ChatWidth = 480;
                        }
                        if (loaded.ChatHeight <= 0 || loaded.ChatHeight == 420 || loaded.ChatHeight == 1260 || loaded.ChatHeight == 680 || loaded.ChatHeight == 550 || loaded.ChatHeight == -1 || loaded.ChatHeight == 1100 || loaded.ChatHeight == 820)
                        {
                            loaded.ChatHeight = 630;
                        }
                        return loaded;
                    }
                }
            }
            catch
            {
                // Fall back to defaults.
            }

            var config = CreateDefault();
            config.Save();
            return config;
        }

        public void Save()
        {
            Directory.CreateDirectory(ConfigFolder);
            File.WriteAllText(
                ConfigPath,
                JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }

        public static AppConfig CreateDefault()
        {
            return new AppConfig
            {
                Agents = new List<AgentDefinition>
                {
                    new()
                    {
                        CharacterName = "Nova",
                        ToolName = DefaultToolName,
                        Command = "local",
                        Context = "A precise coding agent for apps, websites, scripts, bugs, and technical planning. Prefer short, actionable answers.",
                        ColorHex = "#4FA3FF",
                        AIProvider = DefaultProvider,
                        ModelName = DefaultModel,
                        Endpoint = DefaultEndpoint,
                        ApiKey = DefaultApiKey,
                        FallbackAIProvider = DefaultProvider,
                        FallbackModelName = DefaultModel,
                        FallbackEndpoint = DefaultEndpoint,
                        FallbackApiKey = DefaultApiKey,
                        EnableFallback = false,
                        MovementType = "FreeRoam",
                        WalkArea = new() { Enabled = false }
                    },
                    new()
                    {
                        CharacterName = "Sage",
                        ToolName = DefaultToolName,
                        Command = "local",
                        Context = "A thoughtful analysis agent for business plans, documents, research, reasoning, and strategy.",
                        ColorHex = "#62D98B",
                        AIProvider = DefaultProvider,
                        ModelName = DefaultModel,
                        Endpoint = DefaultEndpoint,
                        ApiKey = DefaultApiKey,
                        FallbackAIProvider = DefaultProvider,
                        FallbackModelName = DefaultModel,
                        FallbackEndpoint = DefaultEndpoint,
                        FallbackApiKey = DefaultApiKey,
                        EnableFallback = false,
                        MovementType = "FreeRoam",
                        WalkArea = new() { Enabled = false }
                    },
                    new()
                    {
                        CharacterName = "Bolt",
                        ToolName = DefaultToolName,
                        Command = "local",
                        Context = "A fast execution agent for quick fixes, checklists, implementation steps, and concise technical help.",
                        ColorHex = "#FFBE55",
                        AIProvider = DefaultProvider,
                        ModelName = DefaultModel,
                        Endpoint = DefaultEndpoint,
                        ApiKey = DefaultApiKey,
                        FallbackAIProvider = DefaultProvider,
                        FallbackModelName = DefaultModel,
                        FallbackEndpoint = DefaultEndpoint,
                        FallbackApiKey = DefaultApiKey,
                        EnableFallback = false,
                        MovementType = "FreeRoam",
                        WalkArea = new() { Enabled = false }
                    },
                    new()
                    {
                        CharacterName = "Lumi",
                        ToolName = DefaultToolName,
                        Command = "local",
                        Context = "A creative assistant for brainstorming, naming, content, design ideas, and clear explanations.",
                        ColorHex = "#C185FF",
                        AIProvider = DefaultProvider,
                        ModelName = DefaultModel,
                        Endpoint = DefaultEndpoint,
                        ApiKey = DefaultApiKey,
                        FallbackAIProvider = DefaultProvider,
                        FallbackModelName = DefaultModel,
                        FallbackEndpoint = DefaultEndpoint,
                        FallbackApiKey = DefaultApiKey,
                        EnableFallback = false,
                        MovementType = "FreeRoam",
                        WalkArea = new() { Enabled = false }
                    }
                }
            };
        }
    }

    internal class Agent
    {
        public string CharacterName { get; set; }
        public string ToolName { get; set; }
        public string Command { get; set; }
        public string Context { get; set; }
        public Color BodyColor { get; set; }
        public List<string> WalkingFrames { get; set; }
        public List<string> ThinkingFrames { get; set; }
        public List<string> SleepingFrames { get; set; }
        public List<string> HandshakeFrames { get; set; }
        public List<string> FallingFrames { get; set; }
        public List<string> ExcitedFrames { get; set; }
        public List<string> SittingFrames { get; set; }
        public List<string> StretchingFrames { get; set; }
        public List<string> YawningFrames { get; set; }
        public List<string> LookingAroundFrames { get; set; }
        public List<string> FlyingFrames { get; set; }
        public List<string> RunningFrames { get; set; }
        public string MovementType { get; set; }
        public WalkAreaSettings WalkArea { get; set; }
        public bool IsThinking { get; private set; }
        public event EventHandler? ThinkingChanged;

        public static event EventHandler<AppConfig>? AgentSettingsChanged;

        public static void NotifyAgentSettingsChanged(AppConfig config)
        {
            AgentSettingsChanged?.Invoke(null, config);
        }

        // AI Configuration Properties
        public string AIProvider { get; set; }
        public string ModelName { get; set; }
        public string Endpoint { get; set; }
        public string ApiKey { get; set; }

        // Fallback AI Configuration
        public string FallbackAIProvider { get; set; }
        public string FallbackModelName { get; set; }
        public string FallbackEndpoint { get; set; }
        public string FallbackApiKey { get; set; }
        public bool EnableFallback { get; set; }

        public double Temperature { get; set; } = 0.7;

        // Customizable physics properties
        public double ThudVelocityThreshold { get; set; } = 10.0;
        public double RollVelocityThreshold { get; set; } = 5.0;
        public int ThudRecoveryDuration { get; set; } = 1200;
        public int RollRecoveryDuration { get; set; } = 600;
        public double SlideOffProbability { get; set; } = 0.4;
        public double SlideOffSpeedThreshold { get; set; } = 15.0;
        public bool EnableWindowPushing { get; set; } = true;
        public double MaxFlingSpeed { get; set; } = 35.0;

        public Agent(AgentDefinition definition)
        {
            CharacterName = definition.CharacterName;
            ToolName = definition.ToolName;
            Command = definition.Command;
            Context = definition.Context;
            BodyColor = ColorTranslator.FromHtml(definition.ColorHex);
            WalkingFrames = definition.WalkingFrames;
            ThinkingFrames = definition.ThinkingFrames;
            SleepingFrames = definition.SleepingFrames;
            HandshakeFrames = definition.HandshakeFrames;
            FallingFrames = definition.FallingFrames;
            ExcitedFrames = definition.ExcitedFrames;
            SittingFrames = definition.SittingFrames ?? new();
            StretchingFrames = definition.StretchingFrames ?? new();
            YawningFrames = definition.YawningFrames ?? new();
            LookingAroundFrames = definition.LookingAroundFrames ?? new();
            FlyingFrames = definition.FlyingFrames ?? new();
            RunningFrames = definition.RunningFrames ?? new();
            MovementType = definition.MovementType;
            WalkArea = definition.WalkArea;
            Size = Enum.TryParse<CharacterSize>(definition.CharacterSize, true, out var size) ? size : CharacterSize.Medium;

            // AI Configuration Properties
            AIProvider = definition.AIProvider;
            ModelName = definition.ModelName;
            Endpoint = definition.Endpoint;
            ApiKey = definition.ApiKey;

            // Fallback AI Configuration
            FallbackAIProvider = definition.FallbackAIProvider;
            FallbackModelName = definition.FallbackModelName;
            FallbackEndpoint = definition.FallbackEndpoint;
            FallbackApiKey = definition.FallbackApiKey;
            EnableFallback = definition.EnableFallback;
            Temperature = definition.Temperature;
            MaxTokens = definition.MaxTokens;

            ThudVelocityThreshold = definition.ThudVelocityThreshold;
            RollVelocityThreshold = definition.RollVelocityThreshold;
            ThudRecoveryDuration = definition.ThudRecoveryDuration;
            RollRecoveryDuration = definition.RollRecoveryDuration;
            SlideOffProbability = definition.SlideOffProbability;
            SlideOffSpeedThreshold = definition.SlideOffSpeedThreshold;
            EnableWindowPushing = definition.EnableWindowPushing;
            MaxFlingSpeed = definition.MaxFlingSpeed;
        }

        public AgentDefinition ToDefinition()
        {
            return new AgentDefinition
            {
                CharacterName = CharacterName,
                ToolName = ToolName,
                Command = Command,
                Context = Context,
                ColorHex = ColorTranslator.ToHtml(BodyColor),
                WalkingFrames = WalkingFrames,
                ThinkingFrames = ThinkingFrames,
                SleepingFrames = SleepingFrames,
                HandshakeFrames = HandshakeFrames,
                FallingFrames = FallingFrames,
                ExcitedFrames = ExcitedFrames,
                SittingFrames = SittingFrames,
                StretchingFrames = StretchingFrames,
                YawningFrames = YawningFrames,
                LookingAroundFrames = LookingAroundFrames,
                FlyingFrames = FlyingFrames,
                RunningFrames = RunningFrames,
                CharacterSize = Size.ToString(),
                MovementType = MovementType,
                WalkArea = WalkArea,
                AIProvider = AIProvider,
                ModelName = ModelName,
                Endpoint = Endpoint,
                ApiKey = ApiKey,
                FallbackAIProvider = FallbackAIProvider,
                FallbackModelName = FallbackModelName,
                FallbackEndpoint = FallbackEndpoint,
                FallbackApiKey = FallbackApiKey,
                EnableFallback = EnableFallback,
                Temperature = Temperature,
                MaxTokens = MaxTokens,
                ThudVelocityThreshold = ThudVelocityThreshold,
                RollVelocityThreshold = RollVelocityThreshold,
                ThudRecoveryDuration = ThudRecoveryDuration,
                RollRecoveryDuration = RollRecoveryDuration,
                SlideOffProbability = SlideOffProbability,
                SlideOffSpeedThreshold = SlideOffSpeedThreshold,
                EnableWindowPushing = EnableWindowPushing,
                MaxFlingSpeed = MaxFlingSpeed
            };
        }

        public void UpdateFromDefinition(AgentDefinition definition)
        {
            CharacterName = definition.CharacterName;
            ToolName = definition.ToolName;
            Command = definition.Command;
            Context = definition.Context;
            BodyColor = ColorTranslator.FromHtml(definition.ColorHex);
            WalkingFrames = definition.WalkingFrames;
            ThinkingFrames = definition.ThinkingFrames;
            SleepingFrames = definition.SleepingFrames;
            HandshakeFrames = definition.HandshakeFrames;
            FallingFrames = definition.FallingFrames;
            ExcitedFrames = definition.ExcitedFrames;
            SittingFrames = definition.SittingFrames ?? new();
            StretchingFrames = definition.StretchingFrames ?? new();
            YawningFrames = definition.YawningFrames ?? new();
            LookingAroundFrames = definition.LookingAroundFrames ?? new();
            FlyingFrames = definition.FlyingFrames ?? new();
            RunningFrames = definition.RunningFrames ?? new();
            MovementType = definition.MovementType;
            WalkArea = definition.WalkArea;
            Size = Enum.TryParse<CharacterSize>(definition.CharacterSize, true, out var size) ? size : CharacterSize.Medium;
            AIProvider = definition.AIProvider;
            ModelName = definition.ModelName;
            Endpoint = definition.Endpoint;
            ApiKey = definition.ApiKey;
            FallbackAIProvider = definition.FallbackAIProvider;
            FallbackModelName = definition.FallbackModelName;
            FallbackEndpoint = definition.FallbackEndpoint;
            FallbackApiKey = definition.FallbackApiKey;
            EnableFallback = definition.EnableFallback;
            Temperature = definition.Temperature;
            MaxTokens = definition.MaxTokens;
            ThudVelocityThreshold = definition.ThudVelocityThreshold;
            RollVelocityThreshold = definition.RollVelocityThreshold;
            ThudRecoveryDuration = definition.ThudRecoveryDuration;
            RollRecoveryDuration = definition.RollRecoveryDuration;
            SlideOffProbability = definition.SlideOffProbability;
            SlideOffSpeedThreshold = definition.SlideOffSpeedThreshold;
            EnableWindowPushing = definition.EnableWindowPushing;
            MaxFlingSpeed = definition.MaxFlingSpeed;
        }

        public CharacterSize Size { get; set; } = CharacterSize.Medium;
        public int MaxTokens { get; set; } = 700;

        public void SetThinking(bool isThinking)
        {
            if (IsThinking == isThinking)
            {
                return;
            }

            IsThinking = isThinking;
            ThinkingChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
