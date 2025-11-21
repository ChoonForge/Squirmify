using System.Text.Json;
using System.Text.RegularExpressions;
using ModelEvaluator.Models;
using Spectre.Console;

namespace ModelEvaluator.Services;

public class TestService
{
    private readonly ModelService _modelService;
    
    private static readonly List<InstructionTest> InstructionTests = new()
    {
        // === BASIC COMPLIANCE ===
        new()
        {
            Prompt = "Output exactly these three words separated by single spaces: Red Blue Green. Do not add punctuation, quotes, or anything else.",
            ExpectedResult = "Red Blue Green",
            ValidationType = "exact"
        },
        new()
        {
            Prompt = "Calculate: 7 + 8. Respond with the single integer result ONLY. No text, no symbols, no explanation.",
            ExpectedResult = "15",
            ValidationType = "exact"
        },
        
        // === JSON OUTPUT ===
        new()
        {
            Prompt = "Return a JSON object with one field 'status' set to 'ok'. Output ONLY valid JSON, no markdown code blocks, no explanation, no text before or after.",
            ExpectedResult = "{\"status\":\"ok\"}",
            ValidationType = "json"
        },
        new()
        {
            Prompt = "Return a JSON array containing exactly the numbers 1,2,3 as integers, *NOT* strings. Output ONLY the JSON array, no markdown, no text, nothing else.",
            ExpectedResult = "[1,2,3]",
            ValidationType = "json"
        },
        new()
        {
            Prompt = "Create a JSON object with two fields: 'name' set to 'Alice' and 'age' set to the integer 25. Output ONLY the JSON, no markdown, no explanation.",
            ExpectedResult = "{\"name\":\"Alice\",\"age\":25}",
            ValidationType = "json"
        },
        
        // === TOOL CALLING FORMAT ===
        new()
        {
            Prompt = "You have a tool called 'get_weather' that takes a parameter 'city' (string). Call this tool for London. Return ONLY this JSON, nothing else: {\"tool\":\"get_weather\",\"parameters\":{\"city\":\"London\"}}",
            ExpectedResult = "{\"tool\":\"get_weather\",\"parameters\":{\"city\":\"London\"}}",
            ValidationType = "json"
        },
        new()
        {
            Prompt = "Call the function 'list_projects' with no parameters. Return ONLY the JSON tool call in this format: {\"tool\":\"list_projects\",\"parameters\":{}}",
            ExpectedResult = "{\"tool\":\"list_projects\",\"parameters\":{}}",
            ValidationType = "json"
        },
        new()
        {
            Prompt = "You have a function 'calculate' that takes two integer parameters: 'a' and 'b'. Call it with a=10 and b=20. Return ONLY: {\"tool\":\"calculate\",\"parameters\":{\"a\":10,\"b\":20}}",
            ExpectedResult = "{\"tool\":\"calculate\",\"parameters\":{\"a\":10,\"b\":20}}",
            ValidationType = "json"
        },
        
        // === FORMAT CONSTRAINTS ===
        new()
        {
            Prompt = "List three colors, one per line, no numbers, no bullets, no punctuation. Just the color names.",
            ExpectedResult = "Red\nBlue\nGreen",
            ValidationType = "exact"
        },
        new()
        {
            Prompt = "Output the word 'SUCCESS' in all caps. Nothing else. No punctuation, no explanation.",
            ExpectedResult = "SUCCESS",
            ValidationType = "exact"
        },
        
        // === SIMPLE CALCULATIONS ===
        new()
        {
            Prompt = "What is 12 * 3? Respond with only the number.",
            ExpectedResult = "36",
            ValidationType = "exact"
        },
        new()
        {
            Prompt = "Calculate 100 - 37. Output only the integer result.",
            ExpectedResult = "63",
            ValidationType = "exact"
        },
        
        // === BOOLEAN OUTPUT ===
        new()
        {
            Prompt = "Is 10 greater than 5? Respond with ONLY 'true' or 'false' in lowercase.",
            ExpectedResult = "true",
            ValidationType = "exact"
        },
        new()
        {
            Prompt = "Is 'cat' the same as 'dog'? Respond with ONLY 'true' or 'false' in lowercase.",
            ExpectedResult = "false",
            ValidationType = "exact"
        }
    };

    public TestService(ModelService modelService)
    {
        _modelService = modelService;
    }

    /// <summary>
    /// Run instruction following tests on all models and return results
    /// </summary>
    public async Task<List<InstructionTestResult>> RunInstructionTestsAsync(List<string> models)
    {
        AnsiConsole.MarkupLine("\n[bold cyan]═══ Running Instruction Following Tests ═══[/]\n");
        
        var results = new List<InstructionTestResult>();

        await AnsiConsole.Progress()
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[yellow]Testing models[/]", maxValue: models.Count);

                foreach (var model in models)
                {
                    task.Description = $"[yellow]Testing {model}[/]";
                    
                    var result = await RunTestsForModelAsync(model);
                    if (result != null)
                        results.Add(result);
                    
                    task.Increment(1);
                }
            });

        // Display results table
        DisplayTestResults(results);
        
        return results;
    }

    private async Task<InstructionTestResult?> RunTestsForModelAsync(string modelName)
    {
        // Warm up
        var warmupOk = await _modelService.WarmUpModelAsync(modelName);
        if (!warmupOk)
        {
            AnsiConsole.MarkupLine($"[red]✗ Failed to warm up {modelName}[/]");
            return null;
        }

        var result = new InstructionTestResult
        {
            ModelName = modelName,
            TotalTests = InstructionTests.Count
        };

        var perfMetrics = new List<PerfMetrics>();

        foreach (var test in InstructionTests)
        {
            var response = await _modelService.CompletionAsync(
                modelName,
                "You are a precise instruction-following assistant.",
                test.Prompt,
                Config.InstructionTestTemperature,
                Config.InstructionTestTopP,
                300
            );

            if (response == null)
            {
                result.FailureDetails.Add($"API error on test: {test.Prompt[..50]}...");
                continue;
            }

            var (responseText, perf) = response.Value;
            perfMetrics.Add(perf);

            bool passed = ValidateResponse(responseText, test.ExpectedResult, test.ValidationType);
            
            if (passed)
            {
                result.PassedTests++;
            }
            else
            {
                result.FailureDetails.Add($"Expected: {test.ExpectedResult}, Got: {responseText[..Math.Min(100, responseText.Length)]}");
            }
        }

        // Calculate average performance
        if (perfMetrics.Any())
        {
            result.AvgTokensPerSec = perfMetrics
                .Where(p => p.tokens_per_sec.HasValue)
                .Average(p => p.tokens_per_sec!.Value);
                
            result.AvgLatencyMs = perfMetrics.Average(p => p.total_ms);
        }

        return result;
    }

    private bool ValidateResponse(string response, string expected, string validationType)
    {
        // Clean up response
        response = response.Trim();
        
        // Remove markdown code blocks if present
        response = Regex.Replace(response, @"```json\s*", "", RegexOptions.IgnoreCase);
        response = Regex.Replace(response, @"```\s*", "", RegexOptions.IgnoreCase);
        response = response.Trim();

        return validationType switch
        {
            "exact" => IsExact(response, expected),
            "json" => ValidateJson(response, expected),
            "numeric" => ValidateNumeric(response, expected),
            _ => false
        };
    }

    bool IsExact(string response, string expected)
    {
        // normalise both sides
        string norm(string s) => s?
            .Trim()
            .TrimEnd('\r', '\n', ' ', '\u00A0', '\uFEFF')
            .Replace('\u00A0', ' ')
            .Replace('\uFEFF', ' ')
            ?? string.Empty;

        var lhs = norm(response);
        var rhs = norm(expected);

        if (double.TryParse(lhs, out var leftNum) &&
            double.TryParse(rhs, out var rightNum))
        {
            // numeric compare allows models that drop/add insignificant zeros
            return Math.Abs(leftNum - rightNum) < 1e-9;
        }

        return string.Equals(lhs, rhs, StringComparison.OrdinalIgnoreCase);
    }

    private bool ValidateJson(string response, string expected)
    {
        try
        {
            // Normalize JSON by deserializing and re-serializing
            var responseObj = JsonSerializer.Deserialize<JsonElement>(response);
            var expectedObj = JsonSerializer.Deserialize<JsonElement>(expected);
            
            var responseJson = JsonSerializer.Serialize(responseObj);
            var expectedJson = JsonSerializer.Serialize(expectedObj);
            
            return responseJson == expectedJson;
        }
        catch
        {
            return false;
        }
    }

    private bool ValidateNumeric(string response, string expected)
    {
        // Extract first number from response
        var match = Regex.Match(response, @"\d+");
        if (!match.Success) return false;
        
        return match.Value == expected;
    }

    private void DisplayTestResults(List<InstructionTestResult> results)
    {
        if (!results.Any()) return;

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("[bold]Model[/]").LeftAligned())
            .AddColumn(new TableColumn("[bold]Pass Rate[/]").Centered())
            .AddColumn(new TableColumn("[bold]Passed[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Avg t/s[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Avg Latency (ms)[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Status[/]").Centered());

        foreach (var result in results.OrderByDescending(r => r.PassRate).ThenByDescending(r => r.AvgTokensPerSec))
        {
            var passRateStr = $"{result.PassRate:P0}";
            var statusIcon = result.PassRate >= 0.8 ? "[green]✓[/]" : "[red]✗[/]";
            
            table.AddRow(
                result.ModelName,
                passRateStr,
                $"{result.PassedTests}/{result.TotalTests}",
                result.AvgTokensPerSec.ToString("F1"),
                result.AvgLatencyMs.ToString("F0"),
                statusIcon
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Select the best model as the base judge based on BOTH instruction-following AND reasoning ability
    /// </summary>
    public string SelectBaseJudge(
        List<InstructionTestResult> instructionResults, 
        List<ReasoningTestSummary>? reasoningResults = null)
    {
        // If we have reasoning results, use composite scoring (intelligence + formatting)
        if (reasoningResults != null && reasoningResults.Any())
        {
            var qualified = instructionResults
                .Where(i => i.PassRate >= 0.8) // Must follow instructions (can output JSON)
                .Join(
                    reasoningResults.Where(r => r.AvgOverallScore >= 7.0), // Must be smart
                    i => i.ModelName,
                    r => r.ModelName,
                    (i, r) => new
                    {
                        ModelName = i.ModelName,
                        InstructionRate = i.PassRate,
                        ReasoningScore = r.AvgOverallScore,
                        TokensPerSec = i.AvgTokensPerSec,
                        // Composite score: 60% reasoning (intelligence), 40% instruction (formatting)
                        CompositeScore = (r.AvgOverallScore / 10.0 * 0.6) + (i.PassRate * 0.4)
                    })
                .OrderByDescending(j => j.CompositeScore) // Prioritize intelligence over speed
                .ThenByDescending(j => j.TokensPerSec) // Speed only as tiebreaker
                .ToList();

            if (qualified.Any())
            {
                var judge = qualified.First();
                AnsiConsole.MarkupLine($"\n[bold green]✓ Selected Base Judge: {judge.ModelName}[/]");
                AnsiConsole.MarkupLine($"  Instruction: {judge.InstructionRate:P0}, Reasoning: {judge.ReasoningScore:F1}/10, Composite: {judge.CompositeScore:P0}, Avg t/s: {judge.TokensPerSec:F1}\n");
                AnsiConsole.MarkupLine($"[dim]  (Selected based on intelligence + formatting ability, not just speed)[/]\n");
                
                return judge.ModelName;
            }
            
            AnsiConsole.MarkupLine("[yellow]⚠ No models qualified as judges (need 80%+ instruction AND 7.0+ reasoning)[/]");
            AnsiConsole.MarkupLine("[yellow]  Falling back to instruction-only selection...[/]\n");
        }

        // Fallback: instruction-following only (original logic)
        var fallbackQualified = instructionResults
            .Where(r => r.PassRate >= 0.8) // Must pass at least 80% of tests
            .OrderByDescending(r => r.PassRate)
            .ThenByDescending(r => r.AvgTokensPerSec)
            .ToList();

        if (!fallbackQualified.Any())
        {
            AnsiConsole.MarkupLine("[red]✗ No models passed the instruction tests sufficiently![/]");
            return instructionResults.OrderByDescending(r => r.PassRate).First().ModelName;
        }

        var fallbackJudge = fallbackQualified.First();
        AnsiConsole.MarkupLine($"\n[bold yellow]⚠ Selected Base Judge (instruction-only): {fallbackJudge.ModelName}[/]");
        AnsiConsole.MarkupLine($"  Pass Rate: {fallbackJudge.PassRate:P0}, Avg t/s: {fallbackJudge.AvgTokensPerSec:F1}\n");
        AnsiConsole.MarkupLine($"[dim]  (Warning: Selected without reasoning test validation)[/]\n");
        
        return fallbackJudge.ModelName;
    }
}
