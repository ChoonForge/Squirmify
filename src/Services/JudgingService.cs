using System.Text.Json;
using System.Text.RegularExpressions;
using ModelEvaluator.Models;
using Spectre.Console;

namespace ModelEvaluator.Services;

public class JudgingService
{
    private readonly ModelService _modelService;

    public JudgingService(ModelService modelService)
    {
        _modelService = modelService;
    }

    /// <summary>
    /// Score all results using the base judge
    /// </summary>
    public async Task ScoreResultsAsync(
        string judgeModel,
        List<GenerationResult> results,
        string outputFile)
    {
        AnsiConsole.MarkupLine($"\n[bold cyan]═══ Scoring with Base Judge: {judgeModel} ═══[/]\n");

        await AnsiConsole.Progress()
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[yellow]Scoring responses[/]", maxValue: results.Count);

                foreach (var result in results)
                {
                    task.Description = $"[yellow]Scoring ID {result.id}[/]";
                    
                    var rating = await ScoreResponseAsync(judgeModel, result);
                    if (rating != null)
                    {
                        result.ratings.Add(rating);
                    }
                    
                    task.Increment(1);
                }
            });

        // Save scored results
        await SaveResultsAsync(results, outputFile);
        
        AnsiConsole.MarkupLine($"[green]✓ Scored {results.Count} results with base judge[/]\n");
    }

    /// <summary>
    /// Score a single response
    /// </summary>
    private async Task<Rating?> ScoreResponseAsync(string judgeModel, GenerationResult result)
    {
        var judgePrompt = BuildJudgePrompt(result);
        
        var response = await _modelService.CompletionAsync(
            judgeModel,
            "You are an expert evaluator of AI responses. You score responses objectively based on quality, accuracy, and usefulness.",
            judgePrompt,
            0.3, // Low temperature for consistent judging
            0.9,
            300
        );

        if (response == null)
            return null;

        var (responseText, _) = response.Value;
        
        return ParseJudgeResponse(responseText, judgeModel);
    }

    private string BuildJudgePrompt(GenerationResult result)
    {
        return $@"Evaluate this AI response:

Category: {result.category}
Prompt: {result.seed}

Response:
{result.response}

Performance:
- Tokens/sec: {result.perf.tokens_per_sec:F1}
- Latency: {result.perf.total_ms:F0}ms

Rate this response from 1-10 considering:
- Accuracy and correctness
- Code quality (if applicable)
- Clarity of reasoning
- Response speed and efficiency

Respond in this exact JSON format:
{{
  ""score"": <1-10>,
  ""reasoning"": ""<brief explanation>""
}}";
    }

    private Rating? ParseJudgeResponse(string response, string judgeModel)
    {
        try
        {
            // Clean markdown if present
            response = Regex.Replace(response, @"```json\s*", "", RegexOptions.IgnoreCase);
            response = Regex.Replace(response, @"```\s*", "", RegexOptions.IgnoreCase);
            response = response.Trim();

            var jsonDoc = JsonDocument.Parse(response);
            var root = jsonDoc.RootElement;

            return new Rating
            {
                score = root.GetProperty("score").GetInt32(),
                reasoning = root.GetProperty("reasoning").GetString() ?? "",
                rater = judgeModel
            };
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteLine($"Failed to parse judge response: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Select top judges from scored results
    /// </summary>
    public List<string> SelectAutoJudges(List<GenerationResult> results, List<string> allModels)
    {
        AnsiConsole.MarkupLine("\n[bold cyan]═══ Selecting AutoJudges ═══[/]\n");

        // Calculate model summaries
        var summaries = allModels.Select(model =>
        {
            var modelResults = results.Where(r => r.generator == model).ToList();
            if (!modelResults.Any()) return null;

            return new ModelSummary
            {
                ModelName = model,
                AvgScore = modelResults.Average(r => r.avg_score),
                AvgTokensPerSec = modelResults.Average(r => r.perf.tokens_per_sec ?? 0),
                AvgLatencyMs = modelResults.Average(r => r.perf.total_ms),
                HighQualityCount = modelResults.Count(r => r.high_quality)
            };
        })
        .Where(s => s != null)
        .ToList()!;

        // Select top judges based on score and speed
        var topJudges = summaries
            .OrderByDescending(s => s.AvgScore)
            .ThenByDescending(s => s.AvgTokensPerSec)
            .Take(Config.TopJudgeCount)
            .Select(s => s.ModelName)
            .ToList();

        // Display selected judges
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("[bold]Rank[/]").Centered())
            .AddColumn(new TableColumn("[bold]Model[/]").LeftAligned())
            .AddColumn(new TableColumn("[bold]Avg Score[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Avg t/s[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]HQ Count[/]").RightAligned());

        for (int i = 0; i < topJudges.Count; i++)
        {
            var judge = summaries.First(s => s.ModelName == topJudges[i]);
            table.AddRow(
                $"{i + 1}",
                judge.ModelName,
                judge.AvgScore.ToString("F2"),
                judge.AvgTokensPerSec.ToString("F1"),
                judge.HighQualityCount.ToString()
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        return topJudges;
    }

    /// <summary>
    /// Re-score all results with auto judges
    /// </summary>
    public async Task AutoJudgeResultsAsync(
        List<string> autoJudges,
        List<GenerationResult> results,
        string outputFile)
    {
        AnsiConsole.MarkupLine($"\n[bold cyan]═══ Re-scoring with {autoJudges.Count} AutoJudges ═══[/]\n");

        foreach (var judge in autoJudges)
        {
            AnsiConsole.MarkupLine($"[yellow]Using AutoJudge: {judge}[/]");

            await AnsiConsole.Progress()
                .Columns(
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new SpinnerColumn())
                .StartAsync(async ctx =>
                {
                    var task = ctx.AddTask("[yellow]Scoring responses[/]", maxValue: results.Count);

                    foreach (var result in results)
                    {
                        task.Description = $"[yellow]Scoring ID {result.id}[/]";
                        
                        var rating = await ScoreResponseAsync(judge, result);
                        if (rating != null)
                        {
                            result.ratings.Add(rating);
                        }
                        
                        task.Increment(1);
                    }
                });

            AnsiConsole.MarkupLine($"[green]✓ Completed scoring with {judge}[/]\n");
        }

        // Save final scored results
        await SaveResultsAsync(results, outputFile);
    }

    /// <summary>
    /// Save results to JSON file
    /// </summary>
    private async Task SaveResultsAsync(List<GenerationResult> results, string filePath)
    {
        var json = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>
    /// Extract high quality results
    /// </summary>
    public async Task ExtractHighQualityDatasetAsync(List<GenerationResult> results, string outputFile)
    {
        var highQuality = results.Where(r => r.high_quality).ToList();
        
        if (!highQuality.Any())
        {
            AnsiConsole.MarkupLine("[yellow]⚠ No high-quality results found[/]");
            return;
        }

        // Save as JSONL format
        var lines = highQuality.Select(r => JsonSerializer.Serialize(new
        {
            prompt = r.seed,
            response = r.response,
            category = r.category,
            model = r.generator,
            avg_score = r.avg_score,
            perf = r.perf
        }));

        await File.WriteAllLinesAsync(outputFile, lines);
        
        AnsiConsole.MarkupLine($"[green]✓ Saved {highQuality.Count} high-quality entries → {outputFile}[/]");
    }
}
