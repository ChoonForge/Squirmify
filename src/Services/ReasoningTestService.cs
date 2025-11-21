using System.Text.Json;
using ModelEvaluator.Models;
using Spectre.Console;

namespace ModelEvaluator.Services;

/// <summary>
/// Represents a reasoning test that will be judged for quality
/// </summary>
public class ReasoningTest
{
    public string Category { get; set; } = "";
    public string Prompt { get; set; } = "";
    public string CorrectAnswer { get; set; } = ""; // What the right answer is (for judge reference)
    public string Description { get; set; } = "";
}

/// <summary>
/// Result of a single reasoning test
/// </summary>
public class ReasoningTestResponse
{
    public string ModelName { get; set; } = "";
    public string Category { get; set; } = "";
    public string Prompt { get; set; } = "";
    public string Response { get; set; } = "";
    public string CorrectAnswer { get; set; } = "";
    public PerfMetrics Perf { get; set; } = new();
    public ReasoningRating? Rating { get; set; }
}

/// <summary>
/// Judge's rating of a reasoning response
/// </summary>
public class ReasoningRating
{
    public int OverallScore { get; set; } // 1-10
    public int CorrectAnswer { get; set; } // 1-10 (did they get it right?)
    public int LogicalSteps { get; set; } // 1-10 (showed their work?)
    public int Clarity { get; set; } // 1-10 (clear explanation?)
    public string Reasoning { get; set; } = "";
    public string Rater { get; set; } = "";
}

/// <summary>
/// Summary of reasoning test results for a model
/// </summary>
public class ReasoningTestSummary
{
    public string ModelName { get; set; } = "";
    public double AvgOverallScore { get; set; }
    public double AvgCorrectAnswerScore { get; set; }
    public double AvgLogicalStepsScore { get; set; }
    public double AvgClarityScore { get; set; }
    public double AvgTokensPerSec { get; set; }
    public double AvgLatencyMs { get; set; }
    public Dictionary<string, double> CategoryScores { get; set; } = new();
    public int TotalTests { get; set; }
}

public static class ReasoningTests
{
    public static List<ReasoningTest> GetTests()
    {
        return new List<ReasoningTest>
        {
            // === MULTI-STEP PROBLEM SOLVING ===
            
            new()
            {
                Category = "multi-step",
                Description = "Sheep word problem",
                Prompt = "A farmer has 17 sheep. All but 9 die. How many sheep are left? Think through this step by step and explain your reasoning clearly.",
                CorrectAnswer = "9 sheep (because 'all but 9' means 9 survived)"
            },

            new()
            {
                Category = "multi-step",
                Description = "Widget production rate",
                Prompt = "If it takes 5 machines 5 minutes to make 5 widgets, how long would it take 100 machines to make 100 widgets? Explain your reasoning step by step.",
                CorrectAnswer = "5 minutes (each machine makes 1 widget in 5 minutes, so 100 machines make 100 widgets in 5 minutes)"
            },

            new()
            {
                Category = "multi-step",
                Description = "Age calculation puzzle",
                Prompt = "A father is 40 years old and his son is 10. In how many years will the father be twice as old as his son? Show your work.",
                CorrectAnswer = "20 years (father will be 60, son will be 30)"
            },

            // === CONTEXT RETENTION & TRACKING ===
            
            new()
            {
                Category = "context",
                Description = "Multi-number calculation",
                Prompt = "I'm thinking of three numbers. The first number is 7. The second number is twice the first number. The third number is the sum of the first two numbers. What is the third number multiplied by 2? Show each step.",
                CorrectAnswer = "42 (first=7, second=14, third=21, third*2=42)"
            },

            new()
            {
                Category = "context",
                Description = "Relationship ordering",
                Prompt = "John is older than Mary. Mary is older than Susan. Susan is younger than Tom but older than Lisa. Who is the youngest person mentioned? Think through the relationships step by step.",
                CorrectAnswer = "Lisa is the youngest"
            },

            // === LOGICAL REASONING ===
            
            new()
            {
                Category = "logic",
                Description = "Syllogism flaw detection",
                Prompt = "All cats are animals. Some animals are pets. Therefore, are all cats pets? Reason through this logically and identify any flaws in the reasoning.",
                CorrectAnswer = "No, this is flawed. Just because some animals are pets doesn't mean all cats are pets (they could be wild cats)"
            },

            new()
            {
                Category = "logic",
                Description = "Prime number reasoning",
                Prompt = "Is 1 a prime number? Explain your reasoning using the definition of a prime number.",
                CorrectAnswer = "No, 1 is not prime because prime numbers must have exactly two distinct divisors (1 and itself), but 1 only has one divisor"
            },

            // === APPLIED MATH ===
            
            new()
            {
                Category = "math",
                Description = "Shopping calculation",
                Prompt = "A bakery sells cupcakes for $3 each and cookies for $2 each. Sarah bought 5 cupcakes and 8 cookies. What is her total? Show your calculation.",
                CorrectAnswer = "$31 (5×$3 = $15 for cupcakes, 8×$2 = $16 for cookies, total = $31)"
            },

            new()
            {
                Category = "math",
                Description = "Percentage calculation",
                Prompt = "A store has a 20% off sale. If an item originally costs $50, what's the sale price? Explain your calculation.",
                CorrectAnswer = "$40 (20% of $50 = $10 discount, $50 - $10 = $40)"
            },

            // === PATTERN RECOGNITION ===
            
            new()
            {
                Category = "pattern",
                Description = "Number sequence",
                Prompt = "What comes next in this sequence: 2, 4, 8, 16, 32, __? Explain the pattern.",
                CorrectAnswer = "64 (each number doubles: 2×2=4, 4×2=8, etc.)"
            },

            new()
            {
                Category = "pattern",
                Description = "Letter sequence",
                Prompt = "What comes next: A, C, E, G, __? Explain the pattern you observe.",
                CorrectAnswer = "I (every other letter of the alphabet)"
            }
        };
    }
}

public class ReasoningTestService
{
    private readonly ModelService _modelService;

    public ReasoningTestService(ModelService modelService)
    {
        _modelService = modelService;
    }

    /// <summary>
    /// Run reasoning tests on all models
    /// </summary>
    public async Task<List<ReasoningTestResponse>> RunReasoningTestsAsync(List<string> models)
    {
        AnsiConsole.MarkupLine("\n[bold cyan]═══ Running Reasoning Tests ═══[/]\n");

        var results = new List<ReasoningTestResponse>();
        var tests = ReasoningTests.GetTests();
        var totalTests = models.Count * tests.Count;

        await AnsiConsole.Progress()
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[yellow]Testing models[/]", maxValue: totalTests);

                foreach (var model in models)
                {
                    // Warm up
                    var warmupOk = await _modelService.WarmUpModelAsync(model);
                    if (!warmupOk)
                    {
                        AnsiConsole.MarkupLine($"[red]✗ Failed to warm up {model}[/]");
                        task.Increment(tests.Count);
                        continue;
                    }

                    foreach (var test in tests)
                    {
                        task.Description = $"[yellow]{model} → {test.Category}: {test.Description}[/]";

                        var response = await _modelService.CompletionAsync(
                            model,
                            "You are a helpful assistant that thinks through problems step by step.",
                            test.Prompt,
                            0.3, // Low temp for consistent reasoning
                            0.9,
                            600
                        );

                        if (response != null)
                        {
                            var (responseText, perf) = response.Value;
                            
                            results.Add(new ReasoningTestResponse
                            {
                                ModelName = model,
                                Category = test.Category,
                                Prompt = test.Prompt,
                                Response = responseText,
                                CorrectAnswer = test.CorrectAnswer,
                                Perf = perf
                            });
                        }

                        task.Increment(1);
                    }
                }
            });

        AnsiConsole.MarkupLine($"[green]✓ Completed {results.Count} reasoning tests[/]\n");

        return results;
    }

    /// <summary>
    /// Score reasoning responses using a judge model
    /// </summary>
    public async Task ScoreReasoningTestsAsync(
        string judgeModel,
        List<ReasoningTestResponse> responses)
    {
        AnsiConsole.MarkupLine($"\n[bold cyan]═══ Scoring Reasoning Tests with Judge: {judgeModel} ═══[/]\n");

        await AnsiConsole.Progress()
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[yellow]Scoring responses[/]", maxValue: responses.Count);

                foreach (var response in responses)
                {
                    task.Description = $"[yellow]Scoring {response.ModelName} - {response.Category}[/]";

                    var rating = await ScoreReasoningResponseAsync(judgeModel, response);
                    if (rating != null)
                    {
                        response.Rating = rating;
                    }

                    task.Increment(1);
                }
            });

        AnsiConsole.MarkupLine($"[green]✓ Scored {responses.Count} reasoning responses[/]\n");
    }

    /// <summary>
    /// Score a single reasoning response
    /// </summary>
    private async Task<ReasoningRating?> ScoreReasoningResponseAsync(
        string judgeModel,
        ReasoningTestResponse response)
    {
        var judgePrompt = $@"Evaluate this reasoning response:

Question: {response.Prompt}

Correct Answer (for reference): {response.CorrectAnswer}

Model's Response:
{response.Response}

Rate this response from 1-10 on these dimensions:
1. Correct Answer (did they get the right answer?)
2. Logical Steps (did they show their reasoning/work?)
3. Clarity (was the explanation clear and easy to follow?)

Respond in this exact JSON format:
{{
  ""overall_score"": <1-10>,
  ""correct_answer"": <1-10>,
  ""logical_steps"": <1-10>,
  ""clarity"": <1-10>,
  ""reasoning"": ""<brief explanation of overall score>""
}}";

        var judgeResponse = await _modelService.CompletionAsync(
            judgeModel,
            "You are an expert evaluator of reasoning and problem-solving. You score responses objectively based on correctness, logic, and clarity.",
            judgePrompt,
            0.3,
            0.9,
            400
        );

        if (judgeResponse == null)
            return null;

        var (responseText, _) = judgeResponse.Value;

        return ParseReasoningJudgeResponse(responseText, judgeModel);
    }

    private ReasoningRating? ParseReasoningJudgeResponse(string response, string judgeModel)
    {
        try
        {
            // Clean markdown if present
            response = System.Text.RegularExpressions.Regex.Replace(response, @"```json\s*", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            response = System.Text.RegularExpressions.Regex.Replace(response, @"```\s*", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            response = response.Trim();

            var jsonDoc = JsonDocument.Parse(response);
            var root = jsonDoc.RootElement;

            return new ReasoningRating
            {
                OverallScore = root.GetProperty("overall_score").GetInt32(),
                CorrectAnswer = root.GetProperty("correct_answer").GetInt32(),
                LogicalSteps = root.GetProperty("logical_steps").GetInt32(),
                Clarity = root.GetProperty("clarity").GetInt32(),
                Reasoning = root.GetProperty("reasoning").GetString() ?? "",
                Rater = judgeModel
            };
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteLine($"Failed to parse reasoning judge response: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Generate summary report for reasoning tests
    /// </summary>
    public List<ReasoningTestSummary> GenerateReasoningSummaries(List<ReasoningTestResponse> responses)
    {
        var modelGroups = responses.GroupBy(r => r.ModelName);

        var summaries = modelGroups.Select(group =>
        {
            var modelResponses = group.Where(r => r.Rating != null).ToList();
            
            if (!modelResponses.Any())
                return null;

            var categoryScores = modelResponses
                .GroupBy(r => r.Category)
                .ToDictionary(g => g.Key, g => g.Average(r => r.Rating!.OverallScore));

            return new ReasoningTestSummary
            {
                ModelName = group.Key,
                AvgOverallScore = modelResponses.Average(r => r.Rating!.OverallScore),
                AvgCorrectAnswerScore = modelResponses.Average(r => r.Rating!.CorrectAnswer),
                AvgLogicalStepsScore = modelResponses.Average(r => r.Rating!.LogicalSteps),
                AvgClarityScore = modelResponses.Average(r => r.Rating!.Clarity),
                AvgTokensPerSec = modelResponses.Average(r => r.Perf.tokens_per_sec ?? 0),
                AvgLatencyMs = modelResponses.Average(r => r.Perf.total_ms),
                CategoryScores = categoryScores,
                TotalTests = modelResponses.Count
            };
        })
        .Where(s => s != null)
        .OrderByDescending(s => s!.AvgOverallScore)
        .ThenByDescending(s => s!.AvgTokensPerSec)
        .ToList()!;

        return summaries;
    }

    /// <summary>
    /// Display reasoning test results
    /// </summary>
    public void DisplayReasoningResults(List<ReasoningTestSummary> summaries)
    {
        if (!summaries.Any()) return;

        AnsiConsole.MarkupLine("\n[bold cyan]═══ Reasoning Test Summary ═══[/]\n");

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("[bold]Model[/]").LeftAligned())
            .AddColumn(new TableColumn("[bold]Overall[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Correct[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Logic[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Clarity[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Avg t/s[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Status[/]").Centered());

        foreach (var summary in summaries)
        {
            var statusIcon = summary.AvgOverallScore >= 7.0 ? "[green]✓[/]" : "[yellow]~[/]";
            
            table.AddRow(
                summary.ModelName,
                summary.AvgOverallScore.ToString("F1"),
                summary.AvgCorrectAnswerScore.ToString("F1"),
                summary.AvgLogicalStepsScore.ToString("F1"),
                summary.AvgClarityScore.ToString("F1"),
                summary.AvgTokensPerSec.ToString("F1"),
                statusIcon
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Save reasoning results to JSON
    /// </summary>
    public async Task SaveReasoningResultsAsync(List<ReasoningTestResponse> responses, string filePath)
    {
        var json = JsonSerializer.Serialize(responses, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json);
        AnsiConsole.MarkupLine($"[green]✓ Saved reasoning results → {filePath}[/]");
    }
}
