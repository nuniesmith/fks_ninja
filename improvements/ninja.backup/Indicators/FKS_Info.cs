#pragma warning disable 436 // Suppress type conflict with NinjaTrader.Custom
// src/Indicators/FKS_Dashboard.cs - UPDATED with Custom TextPosition and Enhanced Dashboard
#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Xml.Serialization;
using NinjaTrader.Data;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript.AddOns;
using NinjaTrader.NinjaScript.Indicators;
#endregion

namespace NinjaTrader.NinjaScript.Indicators
{
    // Simple memory monitor implementation
    public class LightweightMemoryMonitor : IDisposable
    {
        private bool disposed = false;
        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
            }
        }
    }

    // Simple cached value implementation
    public class CachedValue
    {
        public object Value { get; set; }
        public DateTime LastUpdated { get; set; }
        public bool IsValid { get; set; }

        public CachedValue(object value)
        {
            Value = value;
            LastUpdated = DateTime.Now;
            IsValid = true;
        }
    }

    public class FKS_Dashboard : Indicator, IFKSComponent, IDisposable
    {
        #region FKS Integration Infrastructure

        // Shared calculation state keys
        private const string STATE_KEY_ATR = "INFO_ATR";
        private const string STATE_KEY_EMA_FAST = "INFO_EMA_FAST";
        private const string STATE_KEY_EMA_SLOW = "INFO_EMA_SLOW";

        // Component integration
        private FKS_ComponentManager componentRegistry;
        private FKS_SignalCoordinator signalCoordinator;
#pragma warning disable CS0414 // Field is assigned but its value is never used
        private bool isRegisteredWithCoordinator = false;
#pragma warning restore CS0414

        // Component references - Now reliably obtained through registry
        private FKS_AI fksAI;
        private FKS_AO fksAO;

        #endregion

        #region Custom TextPosition Integration - NEW

        /// <summary>
        /// Converts custom TextPosition enum to NinjaTrader's TextPosition
        /// NinjaTrader only supports: TopLeft, TopRight, BottomLeft, BottomRight, Center
        /// </summary>
        private DrawingTools.TextPosition ConvertToNTTextPosition(AddOns.TextPosition customPosition)
        {
            switch (customPosition)
            {
                case AddOns.TextPosition.TopLeft:
                    return DrawingTools.TextPosition.TopLeft;
                case AddOns.TextPosition.TopCenter:
                    return DrawingTools.TextPosition.TopLeft; // Fallback to TopLeft
                case AddOns.TextPosition.TopRight:
                    return DrawingTools.TextPosition.TopRight;
                case AddOns.TextPosition.MiddleLeft:
                    return DrawingTools.TextPosition.TopLeft; // Fallback to TopLeft
                case AddOns.TextPosition.Center:
                    return DrawingTools.TextPosition.Center;
                case AddOns.TextPosition.MiddleRight:
                    return DrawingTools.TextPosition.TopRight; // Fallback to TopRight
                case AddOns.TextPosition.BottomLeft:
                    return DrawingTools.TextPosition.BottomLeft;
                case AddOns.TextPosition.BottomCenter:
                    return DrawingTools.TextPosition.BottomLeft; // Fallback to BottomLeft
                case AddOns.TextPosition.BottomRight:
                    return DrawingTools.TextPosition.BottomRight;
                default:
                    return DrawingTools.TextPosition.TopRight;
            }
        }

        /// <summary>
        /// Gets additional offset adjustments for positions not natively supported by NinjaTrader
        /// </summary>
        private int GetPositionOffset(AddOns.TextPosition customPosition, int baseOffset)
        {
            switch (customPosition)
            {
                case AddOns.TextPosition.TopCenter:
                case AddOns.TextPosition.BottomCenter:
                    return baseOffset + 100; // Center adjustment
                case AddOns.TextPosition.MiddleLeft:
                case AddOns.TextPosition.MiddleRight:
                    return baseOffset + 200; // Middle vertical adjustment
                default:
                    return baseOffset;
            }
        }

        /// <summary>
        /// Dashboard section configuration for proper positioning
        /// </summary>
        private class DashboardSection
        {
            public string Name { get; set; }
            public AddOns.TextPosition Position { get; set; }
            public int VerticalOffset { get; set; }
            public int HorizontalOffset { get; set; }
            public Brush TextColor { get; set; }
            public Brush BackgroundColor { get; set; }
            public Brush OutlineColor { get; set; }
            public bool IsEnabled { get; set; }
            public int ZOrder { get; set; }
        }

        // Dashboard layout configuration - optimized for NinjaTrader's TextPosition limitations
        private readonly Dictionary<string, DashboardSection> dashboardLayout = new Dictionary<string, DashboardSection>
        {
            ["Header"] = new DashboardSection
            {
                Name = "FKSDashboardHeader",
                Position = AddOns.TextPosition.TopRight,
                VerticalOffset = 5,
                HorizontalOffset = 5,
                TextColor = Brushes.White,
                BackgroundColor = Brushes.Black,
                OutlineColor = Brushes.DarkBlue,
                IsEnabled = true,
                ZOrder = 100
            },
            ["TrendAnalysis"] = new DashboardSection
            {
                Name = "TrendAnalysisSection",
                Position = AddOns.TextPosition.TopRight,
                VerticalOffset = 55,
                HorizontalOffset = 5,
                TextColor = Brushes.White,
                BackgroundColor = Brushes.Black,
                OutlineColor = Brushes.Green,
                IsEnabled = true,
                ZOrder = 90
            },
            ["WaveAnalysis"] = new DashboardSection
            {
                Name = "WaveAnalysisSection",
                Position = AddOns.TextPosition.TopRight,
                VerticalOffset = 105,
                HorizontalOffset = 5,
                TextColor = Brushes.Yellow,
                BackgroundColor = Brushes.Black,
                OutlineColor = Brushes.Orange,
                IsEnabled = true,
                ZOrder = 80
            },
            ["MarketRegime"] = new DashboardSection
            {
                Name = "MarketRegimeSection",
                Position = AddOns.TextPosition.TopRight,
                VerticalOffset = 155,
                HorizontalOffset = 5,
                TextColor = Brushes.LightGreen,
                BackgroundColor = Brushes.Black,
                OutlineColor = Brushes.DarkGreen,
                IsEnabled = true,
                ZOrder = 70
            },
            ["SupportResistance"] = new DashboardSection
            {
                Name = "SupportResistanceSection",
                Position = AddOns.TextPosition.TopRight,
                VerticalOffset = 205,
                HorizontalOffset = 5,
                TextColor = Brushes.Cyan,
                BackgroundColor = Brushes.Black,
                OutlineColor = Brushes.DarkCyan,
                IsEnabled = true,
                ZOrder = 60
            },
            ["VolatilityAnalysis"] = new DashboardSection
            {
                Name = "VolatilityAnalysisSection",
                Position = AddOns.TextPosition.TopRight,
                VerticalOffset = 255,
                HorizontalOffset = 5,
                TextColor = Brushes.LightBlue,
                BackgroundColor = Brushes.Black,
                OutlineColor = Brushes.DarkBlue,
                IsEnabled = true,
                ZOrder = 50
            },
            ["Performance"] = new DashboardSection
            {
                Name = "PerformanceSection",
                Position = AddOns.TextPosition.TopRight,
                VerticalOffset = 305,
                HorizontalOffset = 5,
                TextColor = Brushes.LightGreen,
                BackgroundColor = Brushes.Black,
                OutlineColor = Brushes.DarkGreen,
                IsEnabled = true,
                ZOrder = 40
            },
            ["ComponentIntegration"] = new DashboardSection
            {
                Name = "ComponentIntegrationSection",
                Position = AddOns.TextPosition.TopRight,
                VerticalOffset = 355,
                HorizontalOffset = 5,
                TextColor = Brushes.White,
                BackgroundColor = Brushes.Black,
                OutlineColor = Brushes.DarkRed,
                IsEnabled = true,
                ZOrder = 30
            }
        };

        /// <summary>
        /// Enhanced text drawing method with custom positioning and proper offset handling
        /// </summary>
        private void DrawDashboardSection(string sectionKey, string content, bool isActive = true)
        {
            try
            {
                if (!dashboardLayout.TryGetValue(sectionKey, out var section) || !section.IsEnabled)
                    return;

                if (!isActive)
                    return;

                // Convert custom position to NinjaTrader position
                var ntPosition = ConvertToNTTextPosition(section.Position);

                // Calculate adjusted offset for unsupported positions
                var adjustedOffset = GetPositionOffset(section.Position, section.VerticalOffset + section.HorizontalOffset);

                // Create the text drawing with proper positioning
                DrawingTools.Draw.TextFixed(
                    this,
                    section.Name,
                    content,
                    ntPosition,
                    section.TextColor,
                    new Gui.Tools.SimpleFont("Arial", DASHBOARD_FONT_SIZE),
                    section.BackgroundColor,
                    section.OutlineColor,
                    adjustedOffset);

                LogDebug("DRAW", $"Drew {sectionKey} section at {section.Position} (NT: {ntPosition}, Offset: {adjustedOffset})");
            }
            catch (Exception ex)
            {
                LogDebug("DRAW", $"Error drawing {sectionKey} section: {ex.Message}", "ERROR");
            }
        }

        #endregion

        #region DEBUG MODE INFRASTRUCTURE

        // Debug tracking variables
        private readonly Dictionary<string, DebugMetric> debugMetrics = new Dictionary<string, DebugMetric>();
        private readonly List<DebugEvent> debugEvents = new List<DebugEvent>(1000);
        private readonly StringBuilder debugLog = new StringBuilder();
        private int debugCheckCounter = 0;
        private DateTime lastDebugUpdate = DateTime.MinValue;
        private readonly TimeSpan debugUpdateInterval = TimeSpan.FromSeconds(5);

        // Debug metric tracking
        private class DebugMetric
        {
            public string Name { get; set; }
            public double Value { get; set; }
            public double MinValue { get; set; } = double.MaxValue;
            public double MaxValue { get; set; } = double.MinValue;
            public double SumValue { get; set; }
            public int Count { get; set; }
            public DateTime LastUpdate { get; set; }

            public double Average => Count > 0 ? SumValue / Count : 0;

            public void Update(double newValue)
            {
                Value = newValue;
                MinValue = Math.Min(MinValue, newValue);
                MaxValue = Math.Max(MaxValue, newValue);
                SumValue += newValue;
                Count++;
                LastUpdate = DateTime.Now;
            }
        }

        // Debug event tracking
        private class DebugEvent
        {
            public DateTime Timestamp { get; set; }
            public string Category { get; set; }
            public string Message { get; set; }
            public string Level { get; set; } // INFO, WARN, ERROR
            public int Bar { get; set; }
        }

        // Debug methods
        private void LogDebug(string category, string message, string level = "INFO")
        {
            if (!ENABLE_DEBUG_MODE) return;

            try
            {
                var debugEvent = new DebugEvent
                {
                    Timestamp = DateTime.Now,
                    Category = category,
                    Message = message,
                    Level = level,
                    Bar = CurrentBar
                };

                debugEvents.Add(debugEvent);

                // Keep only last 1000 events
                if (debugEvents.Count > 1000)
                    debugEvents.RemoveAt(0);

                // Print to output if verbose or error/warning
                if (VERBOSE_DEBUG || level == "ERROR" || level == "WARN")
                {
                    Print($"[FKS_Dashboard-{level}] {category}: {message} (Bar: {CurrentBar})");
                }

                // Add to debug log
                debugLog.AppendLine($"{DateTime.Now:HH:mm:ss.fff} [{level}] {category}: {message}");

                // Keep log size manageable
                if (debugLog.Length > 50000)
                {
                    debugLog.Remove(0, debugLog.Length / 2);
                }
            }
            catch (Exception ex)
            {
                Print($"Debug logging error: {ex.Message}");
            }
        }

        private void UpdateDebugMetric(string name, double value)
        {
            if (!ENABLE_DEBUG_MODE) return;

            try
            {
                if (!debugMetrics.TryGetValue(name, out var metric))
                {
                    metric = new DebugMetric { Name = name };
                    debugMetrics[name] = metric;
                }

                metric.Update(value);
            }
            catch (Exception ex)
            {
                Print($"Debug metric update error: {ex.Message}");
            }
        }

        private void PerformDebugCheck()
        {
            if (!ENABLE_DEBUG_MODE) return;
            if (DateTime.Now - lastDebugUpdate < debugUpdateInterval) return;

            try
            {
                debugCheckCounter++;
                lastDebugUpdate = DateTime.Now;

                LogDebug("SYSTEM", $"=== Info Debug Check #{debugCheckCounter} ===");
                LogDebug("BAR", $"Current Bar: {CurrentBar}, Time: {Time[0]:HH:mm:ss}");

                // Component Registry Status
                if (componentRegistry != null)
                {
                    var status = componentRegistry.GetSystemHealth();
                    LogDebug("REGISTRY", $"Components: {status.TotalComponents}, Healthy: {status.HealthyComponents}");
                    UpdateDebugMetric("RegistryComponents", status.TotalComponents);
                    UpdateDebugMetric("HealthyComponents", status.HealthyComponents);
                }

                // Dashboard Layout Debug - NinjaTrader TextPosition Support
                LogDebug("DASHBOARD", $"Active Sections: {GetActiveDashboardSections()}, Layout Count: {dashboardLayout.Count}");
                LogDebug("NT_SUPPORT", "NinjaTrader supports: TopLeft, TopRight, BottomLeft, BottomRight, Center only");
                foreach (var section in dashboardLayout)
                {
                    var ntPos = ConvertToNTTextPosition(section.Value.Position);
                    LogDebug("LAYOUT", $"{section.Key}: Custom={section.Value.Position}, NT={ntPos}, Offset={section.Value.VerticalOffset}, Enabled={section.Value.IsEnabled}");
                }

                // Draw debug info on chart if enabled
                if (SHOW_DEBUG_ON_CHART)
                {
                    DrawDebugInfo();
                }
            }
            catch (Exception ex)
            {
                LogDebug("DEBUG", $"Debug check error: {ex.Message}", "ERROR");
            }
        }

        private void DrawDebugInfo()
        {
            try
            {
                var debugText = new StringBuilder();
                debugText.AppendLine($"=== FKS_Dashboard Debug #{debugCheckCounter} ===");
                debugText.AppendLine($"Registry: {(componentRegistry != null ? componentRegistry.GetSystemHealth().TotalComponents.ToString() + " comp" : "N/A")}");
                debugText.AppendLine($"AI: {(fksAI != null ? "CONN" : "DISC")}");
                debugText.AppendLine($"AO: {(fksAO != null ? "CONN" : "DISC")}");
                debugText.AppendLine($"Dashboard Sections: {GetActiveDashboardSections()}");
                debugText.AppendLine($"Layout Position: Custom TextPosition");

                DrawingTools.Draw.TextFixed(this, "FKSInfoDebug", debugText.ToString(),
                    DrawingTools.TextPosition.BottomLeft,
                    Brushes.White,
                    new Gui.Tools.SimpleFont("Consolas", DASHBOARD_FONT_SIZE),
                    Brushes.Black,
                    Brushes.DarkMagenta,
                    50);
            }
            catch (Exception ex)
            {
                LogDebug("DRAWING", $"Debug drawing error: {ex.Message}", "ERROR");
            }
        }

        #endregion

        #region Performance Infrastructure and Variables
        private System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        private readonly Dictionary<string, CachedValue> calculationCache = new Dictionary<string, CachedValue>();
        private readonly LightweightMemoryMonitor memoryMonitor = new LightweightMemoryMonitor();
        private DateTime lastDashboardUpdate = DateTime.MinValue;
        private readonly TimeSpan dashboardUpdateInterval = TimeSpan.FromSeconds(1);
        private static readonly Dictionary<Color, SolidColorBrush> colorBrushCache = new Dictionary<Color, SolidColorBrush>();

        public string IndicatorName => "FKS_Dashboard";
        string IFKSComponent.Name => IndicatorName;

        [XmlIgnore]
        [Browsable(false)]
        public ComponentStatus Status { get; set; } = ComponentStatus.Healthy;

        private double signalQualityScore = 0;
        private bool disposed = false;
        private int minimumBarsRequired = 60;

        // market analysis variables
        private string marketRegime = "NEUTRAL";
        private string marketPhase = "ACCUMULATION";
        private string marketBias = "Neutral";
        private string volatilityRegime = "MEDIUM";

        // wave analysis
        private double waveRatioAvg = 1.0;
        private double currentRatioAvg = 0.0;

        // support/resistance
        private double nearestResistance1m = 0;
        private double nearestSupport1m = 0;
        private bool longBias1m = false;
        private bool shortBias1m = false;
        private double nearestResistance5m = 0;
        private double nearestSupport5m = 0;
        private bool longBias5m = false;
        private bool shortBias5m = false;

        // FKS_Calculators state variables
        private double currentATR = 0;

        // Performance metrics
        private double winRate = 0;
        private double profitFactor = 1.0;
        private int totalTrades = 0;
        private double dailyPnL = 0;
        private double trendSpeed = 0;
        private string trendStrengthCategory = "Moderate";
        private double volatilityPercentile = 0.5;

        // Component integration status
        private bool componentsLoaded = false;
        private DateTime lastComponentCheck = DateTime.MinValue;
        private Dictionary<string, DateTime> componentConnectionTimes = new Dictionary<string, DateTime>();
        #endregion

        #region Properties - Dashboard Configuration + DEBUG

        #region Production Configuration - Hardcoded per master plan
        
        // Dashboard display settings - hardcoded for production
        private const bool SHOW_DASHBOARD = true;
        private const bool SHOW_TREND_INFO = true;
        private const bool SHOW_WAVE_ANALYSIS = true;
        private const bool SHOW_MARKET_REGIME = true;
        private const bool SHOW_SR_ANALYSIS = true;
        private const bool SHOW_VOLATILITY = true;
        private const bool SHOW_PERFORMANCE_METRICS = true;
        private const bool SHOW_COMPONENT_INTEGRATION = true;
        private const int DASHBOARD_FONT_SIZE = 10;
        private const int TEXT_SIZE = 14;
        
        // Debug settings - hardcoded to production values
        private const bool ENABLE_DEBUG_MODE = false;
        private const bool VERBOSE_DEBUG = false;
        private const bool SHOW_DEBUG_ON_CHART = false;
        private const bool EXPORT_DEBUG_DATA = false;
        
        #endregion

        #region NinjaScript Lifecycle - UPDATED with Enhanced Dashboard
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                // Dashboard overlay settings
                Description = "FKS Information Dashboard Overlay with Custom TextPosition + DEBUG MODE";
                Name = "FKS_Dashboard";
                IsOverlay = true;
                DisplayInDataBox = false;
                DrawOnPricePanel = true;
                PaintPriceMarkers = false;
                ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
                InitializeDefaultsSafely();
            }
            else if (State == State.Configure)
            {
                ConfigureStrategySafely();
            }
            else if (State == State.DataLoaded)
            {
                LogDebug("LIFECYCLE", "DataLoaded state - initializing components with custom TextPosition");
                InitializeComponentsSafely();

                // Register with component registry
                try
                {
                    componentRegistry = FKS_ComponentManager.Instance;
                    componentRegistry.RegisterComponent("FKS_Dashboard", this);

                    if (signalCoordinator == null)
                    {
                        signalCoordinator = new FKS_SignalCoordinator();
                    }

                    LogDebug("REGISTRY", "Registered with component registry successfully");
                }
                catch (Exception ex)
                {
                    LogDebug("REGISTRY", $"Error registering with component registry: {ex.Message}", "ERROR");
                    FKS_ErrorHandler.HandleError(ex, "FKS_Dashboard.RegisterComponent");
                }
            }
            else if (State == State.Historical)
            {
                try
                {
                    LogDebug("LIFECYCLE", "Initialization completed successfully");
                    Status = ComponentStatus.Healthy;
                    LogDebug("LIFECYCLE", "Historical state initialization completed with enhanced dashboard");
                }
                catch (Exception ex)
                {
                    Status = ComponentStatus.Error;
                    LogDebug("LIFECYCLE", $"FKS_Dashboard historical initialization error: {ex.Message}", "ERROR");
                    FKS_ErrorHandler.HandleError(ex, "FKS_Dashboard.HistoricalInit");
                }

                TryConnectToComponents();
            }
            else if (State == State.Terminated)
            {
                LogDebug("LIFECYCLE", "Termination state - cleaning up");
                CleanupSafely();
            }
        }

        protected override void OnBarUpdate()
        {
            if (disposed) return;

            try
            {
                if (ENABLE_DEBUG_MODE)
                {
                    PerformDebugCheck();
                }

                // Retry component connections periodically
                if (SHOW_COMPONENT_INTEGRATION && !componentsLoaded)
                {
                    RetryComponentConnections();
                }

                // Update dashboard if enabled - using new enhanced system
                if (SHOW_DASHBOARD && DateTime.Now - lastDashboardUpdate >= dashboardUpdateInterval)
                {
                    UpdateEnhancedDashboard();
                    lastDashboardUpdate = DateTime.Now;
                    LogDebug("DASHBOARD", "Enhanced dashboard updated with custom TextPosition");
                }

                // Update market analysis periodically
                if (CurrentBar % 20 == 0)
                {
                    UpdateMarketAnalysis();
                    LogDebug("MARKET", "Market analysis updated");
                }

                // Update signal quality score
                signalQualityScore = CalculateOverallSignalQuality();
                UpdateDebugMetric("SignalQualityScore", signalQualityScore);

            }
            catch (Exception ex)
            {
                LogDebug("UPDATE", $"Error in OnBarUpdate: {ex.Message}", "ERROR");
                Status = ComponentStatus.Error;
            }
        }

        #endregion

        #region Enhanced Dashboard Methods - NEW

        /// <summary>
        /// Main dashboard update method using the new layout system
        /// </summary>
        private void UpdateEnhancedDashboard()
        {
            try
            {
                LogDebug("DASHBOARD", "Updating enhanced dashboard with custom positioning");

                // Update cached data first
                UpdateCachedDisplayData();

                // Draw header
                if (SHOW_DASHBOARD)
                {
                    var headerContent = $"🎯 FKS DASHBOARD - {GetInstrumentName()}\n" +
                                       $"📊 System Status: {Status}\n" +
                                       $"⏰ Last Update: {DateTime.Now:HH:mm:ss}";

                    DrawDashboardSection("Header", headerContent, true);
                }

                // Draw each section based on user settings
                if (SHOW_TREND_INFO)
                {
                    var trendContent = $"📈 TREND ANALYSIS\n" +
                                      $"Direction: {marketBias}\n" +
                                      $"Speed: {trendSpeed:F2}\n" +
                                      $"Strength: {trendStrengthCategory}\n" +
                                      $"Quality: {signalQualityScore:P0}";

                    DrawDashboardSection("TrendAnalysis", trendContent, true);
                }

                if (SHOW_WAVE_ANALYSIS)
                {
                    var waveContent = $"🌊 WAVE ANALYSIS\n" +
                                     $"Wave Ratio: {waveRatioAvg:F2}\n" +
                                     $"Current: {currentRatioAvg:F2}\n" +
                                     $"Market Bias: {marketBias}";

                    DrawDashboardSection("WaveAnalysis", waveContent, true);
                }

                if (SHOW_MARKET_REGIME)
                {
                    var regimeContent = $"🏛️ MARKET REGIME\n" +
                                       $"Regime: {marketRegime}\n" +
                                       $"Phase: {marketPhase}\n" +
                                       $"Momentum: {marketBias}";

                    DrawDashboardSection("MarketRegime", regimeContent, true);
                }

                if (SHOW_SR_ANALYSIS)
                {
                    var srContent = $"📊 SUPPORT & RESISTANCE\n" +
                                   $"1m Bias: {(longBias1m ? "LONG" : shortBias1m ? "SHORT" : "NEUTRAL")}\n" +
                                   $"R: {nearestResistance1m:F2} S: {nearestSupport1m:F2}\n" +
                                   $"5m Bias: {(longBias5m ? "LONG" : shortBias5m ? "SHORT" : "NEUTRAL")}\n" +
                                   $"R: {nearestResistance5m:F2} S: {nearestSupport5m:F2}";

                    DrawDashboardSection("SupportResistance", srContent, true);
                }

                if (SHOW_VOLATILITY)
                {
                    var volContent = $"💥 VOLATILITY ANALYSIS\n" +
                                    $"ATR: {currentATR:F4}\n" +
                                    $"Percentile: {volatilityPercentile:P0}\n" +
                                    $"Regime: {volatilityRegime}";

                    DrawDashboardSection("VolatilityAnalysis", volContent, true);
                }

                if (SHOW_PERFORMANCE_METRICS)
                {
                    var perfContent = $"⚡ PERFORMANCE\n" +
                                     $"Win Rate: {winRate:P0}\n" +
                                     $"Profit Factor: {profitFactor:F2}\n" +
                                     $"Total Trades: {totalTrades}\n" +
                                     $"Daily PnL: {dailyPnL:F0}";

                    DrawDashboardSection("Performance", perfContent, true);
                }

                if (SHOW_COMPONENT_INTEGRATION)
                {
                    var compContent = $"🔗 COMPONENT INTEGRATION\n" +
                                     $"AI: {(fksAI != null ? "✅ CONNECTED" : "❌ DISCONNECTED")}\n" +
                                     $"AO: {(fksAO != null ? "✅ CONNECTED" : "❌ DISCONNECTED")}\n" +
                                     $"Registry: {(componentRegistry != null ? "✅ ACTIVE" : "❌ INACTIVE")}\n" +
                                     $"Signal Quality: {CalculateOverallSignalQuality():F2}";

                    DrawDashboardSection("ComponentIntegration", compContent, true);
                }

                LogDebug("DASHBOARD", "Enhanced dashboard update completed successfully");
            }
            catch (Exception ex)
            {
                LogDebug("DASHBOARD", $"Error updating enhanced dashboard: {ex.Message}", "ERROR");
            }
        }

        /// <summary>
        /// Updates cached display data for consistent information
        /// </summary>
        private void UpdateCachedDisplayData()
        {
            try
            {
                // Update all cached values here for consistency
                // This ensures all dashboard sections show the same data snapshot
                LogDebug("CACHE", "Updating cached display data");
            }
            catch (Exception ex)
            {
                LogDebug("CACHE", $"Error updating cached display data: {ex.Message}", "ERROR");
            }
        }

        /// <summary>
        /// Gets the instrument name for the header
        /// </summary>
        private string GetInstrumentName()
        {
            try
            {
                return BarsArray?[0]?.Instrument?.MasterInstrument?.Name ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        #endregion

        #region Support Methods and Component Integration

        private void TryConnectToComponents()
        {
            try
            {
                LogDebug("CONNECTIONS", "Attempting to connect to other FKS components");

                if (!SHOW_COMPONENT_INTEGRATION)
                {
                    LogDebug("CONNECTIONS", "Component integration disabled");
                    return;
                }

                fksAI = componentRegistry?.GetComponent<FKS_AI>("FKS_AI");
                fksAO = componentRegistry?.GetComponent<FKS_AO>("FKS_AO");

                if (fksAI != null)
                {
                    LogDebug("CONNECTIONS", "Connected to FKS_AI component via registry");
                    componentConnectionTimes["FKS_AI"] = DateTime.Now;
                }

                if (fksAO != null)
                {
                    LogDebug("CONNECTIONS", "Connected to FKS_AO component via registry");
                    componentConnectionTimes["FKS_AO"] = DateTime.Now;
                }

                componentsLoaded = fksAI != null || fksAO != null;
            }
            catch (Exception ex)
            {
                LogDebug("CONNECTIONS", $"Component connection failed: {ex.Message}", "ERROR");
                componentsLoaded = false;
            }
        }

        private void RetryComponentConnections()
        {
            if (DateTime.Now - lastComponentCheck < TimeSpan.FromSeconds(10))
                return;

            lastComponentCheck = DateTime.Now;
            TryConnectToComponents();
        }

        private double GetActiveDashboardSections()
        {
            int activeCount = 0;
            if (SHOW_DASHBOARD) activeCount++;
            if (SHOW_TREND_INFO) activeCount++;
            if (SHOW_WAVE_ANALYSIS) activeCount++;
            if (SHOW_MARKET_REGIME) activeCount++;
            if (SHOW_SR_ANALYSIS) activeCount++;
            if (SHOW_VOLATILITY) activeCount++;
            if (SHOW_PERFORMANCE_METRICS) activeCount++;
            if (SHOW_COMPONENT_INTEGRATION) activeCount++;
            return activeCount;
        }

        private double CalculateOverallSignalQuality()
        {
            try
            {
                double totalQuality = 0;
                int componentCount = 0;

                if (fksAI != null)
                {
                    var aiSignal = fksAI.GetSignal();
                    totalQuality += aiSignal.Confidence * aiSignal.Score;
                    componentCount++;
                }

                if (fksAO != null)
                {
                    var aoSignal = fksAO.GetSignal();
                    totalQuality += aoSignal.Confidence * aoSignal.Score;
                    componentCount++;
                }

                double marketQuality = 0.5; // Base quality
                if (marketRegime == "TREND") marketQuality += 0.2;
                if (volatilityRegime == "LOW" || volatilityRegime == "MEDIUM") marketQuality += 0.15;

                totalQuality += marketQuality;
                componentCount++;

                return componentCount > 0 ? totalQuality / componentCount : 0.0;
            }
            catch
            {
                return 0.0;
            }
        }

        #endregion

        #region IFKSComponent Implementation and Lifecycle Management

        public void Initialize()
        {
            try
            {
                LogDebug("LIFECYCLE", "Component Initialize() called");
                LogDebug("INIT", "Basic initialization completed");
                Status = ComponentStatus.Healthy;
                LogDebug("LIFECYCLE", "FKS_Dashboard component initialized successfully with custom TextPosition");
            }
            catch (Exception ex)
            {
                Status = ComponentStatus.Error;
                LogDebug("LIFECYCLE", $"FKS_Dashboard initialization error: {ex.Message}", "ERROR");
                FKS_ErrorHandler.HandleError(ex, "FKS_Dashboard.Initialize");
            }
        }

        void IFKSComponent.Update()
        {
            try
            {
                if (CurrentBar < minimumBarsRequired)
                    return;

                UpdateMarketAnalysis();
                Status = ComponentStatus.Healthy;
            }
            catch (Exception ex)
            {
                Status = ComponentStatus.Error;
                LogDebug("LIFECYCLE", $"FKS_Dashboard update error: {ex.Message}", "ERROR");
                FKS_ErrorHandler.HandleError(ex, "FKS_Dashboard.Update");
            }
        }

        public ComponentHealthReport GetHealthReport()
        {
            try
            {
                var report = new ComponentHealthReport
                {
                    ComponentName = "FKS_Dashboard",
                    Status = Status,
                    LastUpdate = DateTime.Now,
                    ErrorCount = 0,
                    PerformanceMetrics = new Dictionary<string, double>
                    {
                        ["DashboardSections"] = GetActiveDashboardSections(),
                        ["ComponentConnections"] = componentConnectionTimes.Count,
                        ["OverallSignalQuality"] = CalculateOverallSignalQuality(),
                        ["LayoutSections"] = dashboardLayout.Count
                    },
                    DiagnosticInfo = new List<string>
                    {
                        $"Dashboard Layout: Custom TextPosition",
                        $"Active Sections: {GetActiveDashboardSections()}",
                        $"Layout Configuration: {dashboardLayout.Count} sections",
                        $"Debug Mode: {(ENABLE_DEBUG_MODE ? "Enabled" : "Disabled")}"
                    }
                };

                return report;
            }
            catch (Exception ex)
            {
                LogDebug("HEALTH", $"Error generating health report: {ex.Message}", "ERROR");
                return new ComponentHealthReport
                {
                    ComponentName = "FKS_Dashboard",
                    Status = ComponentStatus.Error,
                    LastUpdate = DateTime.Now,
                    ErrorCount = 1,
                    DiagnosticInfo = new List<string> { $"Health report generation failed: {ex.Message}" }
                };
            }
        }

        public AddOns.ComponentSignal GetSignal()
        {
            try
            {
                var signal = new AddOns.ComponentSignal
                {
                    Source = "FKS_Dashboard",
                    Direction = AddOns.SignalDirection.Neutral,
                    Score = 0.5,
                    Confidence = 0.8,
                    IsActive = false,
                    Timestamp = DateTime.Now,
                    Reasons = new List<string>
                    {
                        $"Market regime: {marketRegime}",
                        $"Market phase: {marketPhase}",
                        $"Dashboard sections: {GetActiveDashboardSections()}"
                    },
                    Metrics = new Dictionary<string, double>
                    {
                        ["SignalQuality"] = signalQualityScore,
                        ["DashboardSections"] = GetActiveDashboardSections(),
                        ["ComponentHealth"] = Status == ComponentStatus.Healthy ? 1.0 : 0.0
                    }
                };

                return signal;
            }
            catch (Exception ex)
            {
                LogDebug("SIGNAL", $"Error generating signal: {ex.Message}", "ERROR");
                return new AddOns.ComponentSignal
                {
                    Source = "FKS_Dashboard",
                    Direction = AddOns.SignalDirection.Neutral,
                    Score = 0.0,
                    Confidence = 0.0,
                    IsActive = false,
                    Timestamp = DateTime.Now,
                    Reasons = new List<string> { "Error generating Info signal" }
                };
            }
        }

        public void Cleanup()
        {
            try
            {
                LogDebug("CLEANUP", "FKS_Dashboard component cleanup initiated");
                CleanupSafely();
                Status = ComponentStatus.Disposed;
            }
            catch (Exception ex)
            {
                LogDebug("CLEANUP", $"FKS_Dashboard cleanup error: {ex.Message}", "ERROR");
            }
        }

        private void UpdateMarketAnalysis()
        {
            // Placeholder for market analysis updates
            LogDebug("ANALYSIS", "Market analysis updated");
        }

        private void InitializeDefaultsSafely()
        {
            try
            {
                SHOW_DASHBOARD = true;
                SHOW_TREND_INFO = true;
                SHOW_WAVE_ANALYSIS = true;
                SHOW_MARKET_REGIME = true;
                SHOW_SR_ANALYSIS = true;
                SHOW_VOLATILITY = true;
                SHOW_PERFORMANCE_METRICS = true;
                SHOW_COMPONENT_INTEGRATION = true;
                DASHBOARD_FONT_SIZE = 10;
                TEXT_SIZE = 14;
                ENABLE_DEBUG_MODE = false;
                VERBOSE_DEBUG = false;
                SHOW_DEBUG_ON_CHART = false;
                EXPORT_DEBUG_DATA = false;
            }
            catch (Exception ex)
            {
                LogDebug("DEFAULTS", $"Error in defaults initialization: {ex.Message}", "ERROR");
            }
        }

        private void ConfigureStrategySafely()
        {
            try
            {
                LogDebug("CONFIG", "Strategy configured safely with enhanced dashboard");
            }
            catch (Exception ex)
            {
                LogDebug("CONFIG", $"Strategy configuration error: {ex.Message}", "ERROR");
            }
        }

        private void InitializeComponentsSafely()
        {
            try
            {
                LogDebug("INIT", "Components initialized safely with custom TextPosition system");
                Status = ComponentStatus.Initializing;
            }
            catch (Exception ex)
            {
                Status = ComponentStatus.Error;
                LogDebug("INIT", $"Component initialization error: {ex.Message}", "ERROR");
            }
        }

        private void CleanupSafely()
        {
            try
            {
                if (disposed) return;

                componentRegistry?.UnregisterComponent("FKS_Dashboard");
                fksAI = null;
                fksAO = null;
                calculationCache?.Clear();
                colorBrushCache?.Clear();
                componentConnectionTimes?.Clear();
                stopwatch?.Stop();
                memoryMonitor?.Dispose();

                disposed = true;
                Status = ComponentStatus.Disposed;
            }
            catch (Exception ex)
            {
                LogDebug("CLEANUP", $"Error in cleanup: {ex.Message}", "ERROR");
            }
        }

        public void Dispose()
        {
            try
            {
                CleanupSafely();
            }
            catch (Exception ex)
            {
                Print($"Error disposing FKS_Dashboard: {ex.Message}");
            }
        }

        #endregion

        #endregion
    }
}