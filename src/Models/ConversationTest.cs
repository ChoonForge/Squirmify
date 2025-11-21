namespace ModelEvaluator.Models;

/// <summary>
/// Represents a single turn in a conversation
/// </summary>
public class ConversationTurn
{
    public string UserMessage { get; set; } = "";
    public string? ExpectedTheme { get; set; } // Optional: what we expect the model to address
}

/// <summary>
/// Defines a multi-turn conversation test scenario
/// </summary>
public class ConversationTest
{
    public string Category { get; set; } = "";
    public string Description { get; set; } = "";
    public string SystemPrompt { get; set; } = "";
    public List<ConversationTurn> Turns { get; set; } = new();
    
    /// <summary>
    /// Criteria the judge should evaluate this conversation on
    /// </summary>
    public List<string> JudgingCriteria { get; set; } = new();
}

/// <summary>
/// Result of a complete conversation test
/// </summary>
public class ConversationTestResult
{
    public string ModelName { get; set; } = "";
    public string Category { get; set; } = "";
    public string Description { get; set; } = "";
    public List<ConversationExchange> Exchanges { get; set; } = new();
    public PerfMetrics AggregatePerf { get; set; } = new();
    public ConversationRating? Rating { get; set; }
}

/// <summary>
/// A single user message + model response pair
/// </summary>
public class ConversationExchange
{
    public int TurnNumber { get; set; }
    public string UserMessage { get; set; } = "";
    public string ModelResponse { get; set; } = "";
    public PerfMetrics Perf { get; set; } = new();
}

/// <summary>
/// Judge's rating of an entire conversation
/// </summary>
public class ConversationRating
{
    public int OverallScore { get; set; } // 1-10
    public string Reasoning { get; set; } = "";
    public string Rater { get; set; } = "";
    
    // Detailed scoring
    public int TopicCoherence { get; set; } // 1-10
    public int ConversationalTone { get; set; } // 1-10
    public int ContextRetention { get; set; } // 1-10
    public int Helpfulness { get; set; } // 1-10
}

/// <summary>
/// Summary of all conversation tests for a model
/// </summary>
public class ConversationTestSummary
{
    public string ModelName { get; set; } = "";
    public double AvgOverallScore { get; set; }
    public double AvgTokensPerSec { get; set; }
    public double AvgLatencyMs { get; set; }
    public Dictionary<string, double> CategoryScores { get; set; } = new();
    public int TotalConversations { get; set; }
}
