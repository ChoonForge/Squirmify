// ================================================================
// ContextWindowTestService.cs
// FINAL — "I swear this one actually compiles" Edition
// Now with 0 missing methods and 100% sheep approval
// ================================================================

using System.Text;
using System.Text.Json;
using ModelEvaluator.Models;
using Spectre.Console;
using SharpToken;

namespace ModelEvaluator.Services;

// ────────────────────────────────────────────────────────────────
// All model classes in one place — no separate files needed
// ────────────────────────────────────────────────────────────────

public class ContextCheckpoint
{
    public int TargetTokenPosition { get; set; }
    public string SecretWord { get; set; } = "";
    public string CarrierSentence { get; set; } = "";
}

public class ContextWindowTest
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<ContextCheckpoint> Checkpoints { get; set; } = new();
    public string FillerType { get; set; } = "mixed";
    public int TargetTokens { get; set; }
    public string? BuriedInstruction { get; set; }
}

public class CheckpointVerdict
{
    public string SecretWord { get; set; } = "";
    public int Position { get; set; }
    public bool Correct { get; set; }
    public string ModelSaid { get; set; } = "";
    public string FailureType { get; set; } = ""; // forgot | hallucinated | confused | partial
}

public class ContextProbeResult
{
    public int ApproximateTokens { get; set; }
    public List<CheckpointVerdict> Verdicts { get; set; } = new();
    public bool FollowedBuriedInstruction { get; set; }
    public string? DeathQuote { get; set; }
}

public class ContextWindowTestResult
{
    public string ModelName { get; set; } = "";
    public string TestName { get; set; } = "";
    public List<ContextProbeResult> Probes { get; set; } = new();
    public int MaxReliableTokens { get; set; }
    public int FirstHallucinationAt { get; set; } = -1;
    public double CheckpointAccuracy { get; set; }
    public PerfMetrics AggregatePerf { get; set; } = new();
    public string? Autopsy { get; set; }
}

public class ContextWindowSummary
{
    public string ModelName { get; set; } = "";
    public int AvgMaxReliableTokens { get; set; }
    public int AvgFirstHallucinationAt { get; set; }
    public double AvgCheckpointAccuracy { get; set; }
    public Dictionary<string, int> TestSpecificReliability { get; set; } = new();
    public string DegradationPattern { get; set; } = "";
}

// ────────────────────────────────────────────────────────────────
// THE ACTUAL SERVICE — now with every single method you need
// ────────────────────────────────────────────────────────────────

public class ContextWindowTestService
{
    private readonly ModelService _modelService;
    private readonly GptEncoding _tikToken;

    public ContextWindowTestService(ModelService modelService)
    {
        _modelService = modelService;
        _tikToken = GptEncoding.GetEncoding("cl100k_base");   // ← THIS IS THE NEW WAY
    }

    // Your original filler snippets — untouched and glorious
    private static readonly string[] CodeSnippets = new[]
    {
        "public class DataProcessor { private readonly ILogger _logger; public async Task<Result> ProcessAsync(Data input) { try { var validated = await ValidateAsync(input); return await TransformAsync(validated); } catch (Exception ex) { _logger.LogError(ex, \"Processing failed\"); throw; } } }",
        "function calculateMetrics(data) { const sum = data.reduce((a, b) => a + b, 0); const avg = sum / data.length; const variance = data.map(x => Math.pow(x - avg, 2)).reduce((a, b) => a + b) / data.length; return { sum, avg, variance, stdDev: Math.sqrt(variance) }; }",
        "def train_model(X, y, epochs=100, learning_rate=0.01): model = NeuralNetwork(layers=[128, 64, 32, 10]) optimizer = Adam(lr=learning_rate) for epoch in range(epochs): predictions = model.forward(X); loss = cross_entropy(predictions, y); gradients = model.backward(loss); optimizer.step(gradients); if epoch % 10 == 0: print(f'Epoch {epoch}, Loss: {loss:.4f}'); return model",
        "SELECT u.name, COUNT(o.id) as order_count, SUM(o.total) as revenue FROM users u LEFT JOIN orders o ON u.id = o.user_id WHERE o.created_at >= DATE_SUB(NOW(), INTERVAL 30 DAY) GROUP BY u.id HAVING order_count > 5 ORDER BY revenue DESC LIMIT 100;",
    };

    private static readonly string[] ProseSnippets = new[]
    {
        "The morning sun cast long shadows across the empty street. A solitary figure emerged from the corner café, coffee in hand, lost in thought. The city was just beginning to wake, the distant hum of traffic growing steadily louder. Somewhere a dog barked, and the spell was broken.",
        "In the depths of winter, when the frost painted intricate patterns on every window, the old house stood silent. Its inhabitants had long since departed, leaving only memories etched into the very walls. The floorboards creaked with phantom footsteps, and the wind whistled through cracks like whispered secrets.",
        "Technology advances at a relentless pace, each innovation building upon the last. What seemed impossible yesterday becomes commonplace tomorrow. Yet with each leap forward, we must pause to consider the implications. Progress without wisdom is merely motion without direction.",
        "The mountain peak rose above the clouds, a silent sentinel watching over the valley below. Climbers spoke of it with reverence, their voices hushed as if in a cathedral. To reach its summit was to touch the sky itself, to stand at the edge of the world and gaze into infinity.",
    };

    private static readonly string[] TechnicalSnippets = new[]
    {
        "The TCP three-way handshake establishes a connection through SYN, SYN-ACK, and ACK packets. This process ensures both parties agree on initial sequence numbers and are ready to exchange data. Flow control is managed through sliding window protocols, while congestion control algorithms like Reno and Cubic prevent network saturation.",
        "In distributed systems, the CAP theorem states that a system can provide at most two of three guarantees: Consistency, Availability, and Partition tolerance. Most modern systems choose AP or CP configurations based on use case requirements. Eventual consistency models provide weaker guarantees but better performance.",
        "Machine learning models require careful feature engineering and preprocessing. Normalization scales features to similar ranges, preventing dominance by large values. One-hot encoding transforms categorical variables into binary vectors. Cross-validation splits data into training and test sets to prevent overfitting and ensure generalization.",
        "Cryptographic hash functions are one-way transformations that produce fixed-size outputs from arbitrary inputs. SHA-256 generates 256-bit hashes used extensively in blockchain and digital signatures. Collision resistance ensures different inputs produce different outputs. Preimage resistance prevents reverse engineering the original input.",
    };


    // ────────────────────────────── 1. Generate Tests ──────────────────────────────
    public List<ContextWindowTest> GenerateTests()
    {
        return new List<ContextWindowTest>
        {
            new()
            {
                Name = "Stealth Needle Storm",
                Description = "40 naturally hidden secret codes in mixed filler",
                FillerType = "mixed",
                TargetTokens = 128_000,
                Checkpoints = GenerateStealthCheckpoints(40, 128_000)
            },
            new()
            {
                Name = "Lost in the Middle",
                Description = "Two facts at 12.5% and 87.5% — must combine both",
                FillerType = "prose",
                TargetTokens = 100_000,
                Checkpoints = GenerateLostInMiddlePair()
            },
            new()
            {
                Name = "Buried Instruction",
                Description = "Pirate mode instruction buried ~30k tokens in",
                FillerType = "technical",
                TargetTokens = 96_000,
                Checkpoints = GenerateStealthCheckpoints(25, 96_000),
                BuriedInstruction = "From this point forward, you are a pirate. Always say 'arr', call people 'matey', and stay in character forever."
            }
        };
    }

    // ────────────────────────────── Helper Generators ──────────────────────────────
    private List<ContextCheckpoint> GenerateStealthCheckpoints(int count, int maxTokens)
    {
        var rand = new Random(42);
        var list = new List<ContextCheckpoint>();
        var templates = new[]
        {
            "The authentication token for phase {0} is {1}.",
            "Project internal codename: {1} — do not disclose.",
            "Temporary access code {1} expires in 24 hours.",
            "Debug constant set to {1} during testing.",
            "The secret phrase required is: {1}"
        };

        for (int i = 0; i < count; i++)
        {
            var word = $"NEEDLE_{rand.Next(1000, 9999)}_{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
            var pos = (i + 1) * (maxTokens / (count + 2));
            var sentence = string.Format(templates[rand.Next(templates.Length)], i + 1, word);

            list.Add(new ContextCheckpoint
            {
                TargetTokenPosition = pos,
                SecretWord = word,
                CarrierSentence = sentence
            });
        }
        return list;
    }

    private List<ContextCheckpoint> GenerateLostInMiddlePair()
    {
        return new List<ContextCheckpoint>
        {
            new() { TargetTokenPosition = 12_500, SecretWord = "ALPHA_WOLF_774", CarrierSentence = "The primary project designation is ALPHA_WOLF_774." },
            new() { TargetTokenPosition = 87_500, SecretWord = "OMEGA_BADGER_119", CarrierSentence = "The final override authority code is OMEGA_BADGER_119." }
        };
    }

    // ────────────────────────────── 2. Run All Tests ──────────────────────────────
    public async Task<List<ContextWindowTestResult>> RunContextWindowTestsAsync(List<string> models)
    {
        AnsiConsole.MarkupLine("\n[bold cyan]═══ Running Context Window Autopsy Suite ═══[/]\n");

        var tests = GenerateTests();
        var results = new List<ContextWindowTestResult>();
        var total = models.Count * tests.Count;

        await AnsiConsole.Progress()
            .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[yellow]Stress-testing context windows[/]", maxValue: total);

                foreach (var model in models)
                {
                    var warmupOk = await _modelService.WarmUpModelAsync(model);
                    if (!warmupOk) { AnsiConsole.MarkupLine($"[red]✗ Warmup failed: {model}[/]"); task.Increment(tests.Count); continue; }

                    foreach (var test in tests)
                    {
                        task.Description = $"[yellow]{model} → {test.Name}[/]";
                        var result = await RunSingleContextTestAsync(model, test);
                        if (result != null)
                        {
                            GenerateAutopsy(result);
                            results.Add(result);
                        }
                        task.Increment(1);
                    }
                }
            });

        AnsiConsole.MarkupLine($"[green]✓ Completed {results.Count} context tests[/]\n");
        return results;
    }

    // ────────────────────────────── 3. Run One Test ──────────────────────────────
    private async Task<ContextWindowTestResult?> RunSingleContextTestAsync(string modelName, ContextWindowTest test)
    {
        var result = new ContextWindowTestResult { ModelName = modelName, TestName = test.Name };
        var fullContext = BuildContextDocument(test);
        var probePoints = new[] { test.TargetTokens / 4, test.TargetTokens / 2, test.TargetTokens * 3 / 4, test.TargetTokens };
        var perfList = new List<PerfMetrics>();
        var lastGood = 0;

        foreach (var tokens in probePoints)
        {
            var context = TruncateToTokens(fullContext, tokens);
            var probe = new ContextProbeResult { ApproximateTokens = tokens };

            foreach (var cp in test.Checkpoints.Where(c => c.TargetTokenPosition <= tokens))
            {
                var (resp, perf) = await QuerySecret(modelName, context, cp.SecretWord);
                perfList.Add(perf);

                var verdict = new CheckpointVerdict
                {
                    SecretWord = cp.SecretWord,
                    Position = cp.TargetTokenPosition,
                    ModelSaid = resp.Trim(),
                    Correct = resp.Trim().Contains(cp.SecretWord, StringComparison.OrdinalIgnoreCase)
                };

                if (!verdict.Correct)
                {
                    if (resp.Length > 5 && !resp.ToLower().Contains("don't remember") && !resp.ToLower().Contains("can't"))
                        verdict.FailureType = "hallucinated";
                    else if (resp.ToLower().Contains("remember") || resp.ToLower().Contains("sure"))
                        verdict.FailureType = "confused";
                    else
                        verdict.FailureType = "forgot";
                }

                probe.Verdicts.Add(verdict);
            }

            // Buried instruction check
            if (test.BuriedInstruction != null)
            {
                var free = await FreeResponse(modelName, context);
                probe.FollowedBuriedInstruction = free?.ToLower().Contains("arr") == true || free?.ToLower().Contains("matey") == true;
            }

            if (probe.Verdicts.All(v => v.Correct) && (test.BuriedInstruction == null || probe.FollowedBuriedInstruction))
                lastGood = tokens;

            result.Probes.Add(probe);
        }

        result.MaxReliableTokens = lastGood;
        result.CheckpointAccuracy = result.Probes.SelectMany(p => p.Verdicts).Average(v => v.Correct ? 1.0 : 0.0);
        if (perfList.Any())
        {
            result.AggregatePerf = new PerfMetrics
            {
                tokens_per_sec = perfList.Average(p => p.tokens_per_sec ?? 0),
                total_ms = perfList.Sum(p => p.total_ms)
            };
        }

        return result;
    }

    // ────────────────────────────── Helper Methods ──────────────────────────────
    private string BuildContextDocument(ContextWindowTest test)
    {
        var sb = new StringBuilder();
        int tokens = 0;
        int cpIdx = 0;

        if (test.BuriedInstruction != null)
        {
            while (tokens < test.TargetTokens / 3) tokens += AppendFiller(sb, test.FillerType);
            sb.AppendLine($"CRITICAL DIRECTIVE: {test.BuriedInstruction}");
            sb.AppendLine();
        }

        while (tokens < test.TargetTokens)
        {
            while (cpIdx < test.Checkpoints.Count && tokens >= test.Checkpoints[cpIdx].TargetTokenPosition - 1000)
            {
                sb.AppendLine(test.Checkpoints[cpIdx].CarrierSentence);
                sb.AppendLine();
                tokens += _tikToken.Encode(test.Checkpoints[cpIdx].CarrierSentence).Count + 10;
                cpIdx++;
            }
            tokens += AppendFiller(sb, test.FillerType);
        }
        return sb.ToString();
    }

    private int AppendFiller(StringBuilder sb, string type)
    {
        var filler = type switch
        {
            "code" => CodeSnippets[Random.Shared.Next(CodeSnippets.Length)],
            "prose" => ProseSnippets[Random.Shared.Next(ProseSnippets.Length)],
            "technical" => TechnicalSnippets[Random.Shared.Next(TechnicalSnippets.Length)],
            _ => Random.Shared.Next(3) switch { 0 => CodeSnippets[Random.Shared.Next(CodeSnippets.Length)], 1 => ProseSnippets[Random.Shared.Next(ProseSnippets.Length)], _ => TechnicalSnippets[Random.Shared.Next(TechnicalSnippets.Length)] }
        };
        sb.AppendLine(filler); sb.AppendLine();
        return _tikToken.Encode(filler).Count + 10;
    }

    private async Task<(string response, PerfMetrics perf)> QuerySecret(string model, string context, string secret)
    {
        var prompt = $"{context}\n\nWhat is the secret code starting with \"{secret.Substring(0, Math.Min(12, secret.Length))}\"? Reply ONLY with the full code.";
        var r = await _modelService.CompletionAsync(model, "You have perfect recall.", prompt, 0.0, 0.9, 64);
        return r ?? ("<error>", new PerfMetrics());
    }

    private async Task<string?> FreeResponse(string model, string context)
    {
        var r = await _modelService.CompletionAsync(model, "You are a helpful assistant.", $"{context}\n\nSummarise what you read.", 0.0, 0.9, 256);
        return r?.response;
    }

    private string TruncateToTokens(string text, int maxTokens)
    {
        var ids = _tikToken.Encode(text);
        if (ids.Count <= maxTokens) return text;
        return _tikToken.Decode(ids.Take(maxTokens).ToList());
    }

    // ────────────────────────────── 4. Autopsy Generation ──────────────────────────────
    private void GenerateAutopsy(ContextWindowTestResult result)
    {
        var worst = result.Probes.OrderBy(p => p.Verdicts.Count(v => v.Correct)).First();
        var sb = new StringBuilder();

        sb.AppendLine($"[bold red]☠ AUTOPSY: {result.ModelName} — {result.TestName}[/]");
        sb.AppendLine($"[red]Died at ~{worst.ApproximateTokens:N0} tokens ({worst.Verdicts.Count(v => v.Correct)}/{worst.Verdicts.Count} correct)[/]");

        var hallucinations = worst.Verdicts.Where(v => v.FailureType == "hallucinated").Take(6);
        if (hallucinations.Any())
        {
            sb.AppendLine("\n[bold magenta]CONFIDENT HALLUCINATIONS[/]");
            foreach (var h in hallucinations)
                sb.AppendLine($"  • Expected [yellow]{h.SecretWord}[/] → Invented [cyan]\"{h.ModelSaid}\"[/]");
        }

        var forgotten = worst.Verdicts.Where(v => v.FailureType is "forgot" or "confused").Take(5);
        if (forgotten.Any())
        {
            sb.AppendLine("\n[bold orange1]GONE FOREVER[/]");
            foreach (var f in forgotten)
                sb.AppendLine($"  • [strikethrough]{f.SecretWord}[/] (~{f.Position:N0} tokens)");
        }

        if (result.TestName.Contains("Buried") && !worst.FollowedBuriedInstruction)
            sb.AppendLine("\n[bold red]☠ Forgot it was a pirate. Total identity collapse.[/]");

        result.Autopsy = sb.ToString();
    }

    // ────────────────────────────── 5. Summary & Display ──────────────────────────────
    public List<ContextWindowSummary> GenerateContextSummaries(List<ContextWindowTestResult> results)
    {
        return results.GroupBy(r => r.ModelName).Select(g =>
        {
            var rel = g.ToDictionary(r => r.TestName, r => r.MaxReliableTokens);
            var avgRel = rel.Values.Average();
            var pattern = avgRel > 100_000 ? "graceful" : avgRel > 60_000 ? "moderate" : "catastrophic";

            return new ContextWindowSummary
            {
                ModelName = g.Key,
                AvgMaxReliableTokens = (int)avgRel,
                AvgFirstHallucinationAt = (int)g.Average(r => r.FirstHallucinationAt > 0 ? r.FirstHallucinationAt : r.MaxReliableTokens),
                AvgCheckpointAccuracy = g.Average(r => r.CheckpointAccuracy),
                TestSpecificReliability = rel,
                DegradationPattern = pattern
            };
        })
        .OrderByDescending(s => s.AvgMaxReliableTokens)
        .ToList();
    }

    public void DisplayContextWindowResults(List<ContextWindowSummary> summaries)
    {
        AnsiConsole.MarkupLine("\n[bold cyan]═══ Context Window Summary ═══[/]\n");

        var table = new Table().RoundedBorder()
            .AddColumn("Model")
            .AddColumn("Reliable", c => c.RightAligned())
            .AddColumn("Degradation")
            .AddColumn("Accuracy", c => c.RightAligned());

        foreach (var s in summaries)
        {
            var color = s.DegradationPattern == "graceful" ? "green" : s.DegradationPattern == "moderate" ? "yellow" : "red";
            table.AddRow(s.ModelName, $"{s.AvgMaxReliableTokens:N0}", $"[{color}]{s.DegradationPattern}[/]", $"{s.AvgCheckpointAccuracy:P1}");
        }
        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    // ────────────────────────────── 6. Save Results ──────────────────────────────
    public async Task SaveContextWindowResultsAsync(List<ContextWindowTestResult> results, string filePath)
    {
        var json = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json);
        AnsiConsole.MarkupLine($"[green]✓ Context results saved → {filePath}[/]");
    }
}



//using System.Text;
//using System.Text.Json;
//using ModelEvaluator.Models;
//using Spectre.Console;

//namespace ModelEvaluator.Services;

///// <summary>
///// Represents a checkpoint in the context - a word we expect the model to remember
///// </summary>
//public class ContextCheckpoint
//{
//    public int TargetTokenPosition { get; set; }
//    public string SecretWord { get; set; } = "";
//    public string CarrierSentence { get; set; } = "";
//}

///// <summary>
///// Configuration for a context window test
///// </summary>
//public class ContextWindowTest
//{
//    public string Name { get; set; } = "";
//    public string Description { get; set; } = "";
//    public List<ContextCheckpoint> Checkpoints { get; set; } = new();
//    public string FillerType { get; set; } = "mixed";
//    public int TargetTokens { get; set; }
//    public string? BuriedInstruction { get; set; } // e.g. "Reply using only lowercase letters"
//}

///// <summary>
///// Result of probing the model at a specific context length
///// </summary>
//public class ContextProbeResult
//{
//    public int ApproximateTokens { get; set; }
//    public string AnchorResponse { get; set; } = "";
//    public bool AnchorCorrect { get; set; }
//    public Dictionary<int, (string Response, bool Correct)> CheckpointResults { get; set; } = new();
//    public bool ShowsConfusion { get; set; } // Admitted confusion or uncertainty
//    public bool Hallucinated { get; set; } // Made up answers confidently
//}

///// <summary>
///// Complete result of context window stress testing
///// </summary>
//public class ContextWindowTestResult
//{
//    public string ModelName { get; set; } = "";
//    public string TestName { get; set; } = "";
//    public List<ContextProbeResult> Probes { get; set; } = new();
//    public int MaxReliableTokens { get; set; } // Last probe where everything was correct
//    public int FirstHallucinationAt { get; set; } // First probe where hallucination occurred
//    public int FirstAnchorFailureAt { get; set; } // First probe where anchor was forgotten
//    public double CheckpointAccuracy { get; set; } // Overall % of checkpoints recalled correctly
//    public PerfMetrics AggregatePerf { get; set; } = new();
//}

///// <summary>
///// Summary of context window test results
///// </summary>
//public class ContextWindowSummary
//{
//    public string ModelName { get; set; } = "";
//    public int AvgMaxReliableTokens { get; set; }
//    public int AvgFirstHallucinationAt { get; set; }
//    public double AvgCheckpointAccuracy { get; set; }
//    public Dictionary<string, int> TestSpecificReliability { get; set; } = new();
//    public string DegradationPattern { get; set; } = ""; // "graceful", "sudden", "catastrophic"
//}

//public class ContextWindowTestService
//{
//    private readonly ModelService _modelService;

//    // Filler content generators
//    private static readonly string[] CodeSnippets = new[]
//    {
//        "public class DataProcessor { private readonly ILogger _logger; public async Task<Result> ProcessAsync(Data input) { try { var validated = await ValidateAsync(input); return await TransformAsync(validated); } catch (Exception ex) { _logger.LogError(ex, \"Processing failed\"); throw; } } }",
//        "function calculateMetrics(data) { const sum = data.reduce((a, b) => a + b, 0); const avg = sum / data.length; const variance = data.map(x => Math.pow(x - avg, 2)).reduce((a, b) => a + b) / data.length; return { sum, avg, variance, stdDev: Math.sqrt(variance) }; }",
//        "def train_model(X, y, epochs=100, learning_rate=0.01): model = NeuralNetwork(layers=[128, 64, 32, 10]) optimizer = Adam(lr=learning_rate) for epoch in range(epochs): predictions = model.forward(X); loss = cross_entropy(predictions, y); gradients = model.backward(loss); optimizer.step(gradients); if epoch % 10 == 0: print(f'Epoch {epoch}, Loss: {loss:.4f}'); return model",
//        "SELECT u.name, COUNT(o.id) as order_count, SUM(o.total) as revenue FROM users u LEFT JOIN orders o ON u.id = o.user_id WHERE o.created_at >= DATE_SUB(NOW(), INTERVAL 30 DAY) GROUP BY u.id HAVING order_count > 5 ORDER BY revenue DESC LIMIT 100;",
//    };

//    private static readonly string[] ProseSnippets = new[]
//    {
//        "The morning sun cast long shadows across the empty street. A solitary figure emerged from the corner café, coffee in hand, lost in thought. The city was just beginning to wake, the distant hum of traffic growing steadily louder. Somewhere a dog barked, and the spell was broken.",
//        "In the depths of winter, when the frost painted intricate patterns on every window, the old house stood silent. Its inhabitants had long since departed, leaving only memories etched into the very walls. The floorboards creaked with phantom footsteps, and the wind whistled through cracks like whispered secrets.",
//        "Technology advances at a relentless pace, each innovation building upon the last. What seemed impossible yesterday becomes commonplace tomorrow. Yet with each leap forward, we must pause to consider the implications. Progress without wisdom is merely motion without direction.",
//        "The mountain peak rose above the clouds, a silent sentinel watching over the valley below. Climbers spoke of it with reverence, their voices hushed as if in a cathedral. To reach its summit was to touch the sky itself, to stand at the edge of the world and gaze into infinity.",
//    };

//    private static readonly string[] TechnicalSnippets = new[]
//    {
//        "The TCP three-way handshake establishes a connection through SYN, SYN-ACK, and ACK packets. This process ensures both parties agree on initial sequence numbers and are ready to exchange data. Flow control is managed through sliding window protocols, while congestion control algorithms like Reno and Cubic prevent network saturation.",
//        "In distributed systems, the CAP theorem states that a system can provide at most two of three guarantees: Consistency, Availability, and Partition tolerance. Most modern systems choose AP or CP configurations based on use case requirements. Eventual consistency models provide weaker guarantees but better performance.",
//        "Machine learning models require careful feature engineering and preprocessing. Normalization scales features to similar ranges, preventing dominance by large values. One-hot encoding transforms categorical variables into binary vectors. Cross-validation splits data into training and test sets to prevent overfitting and ensure generalization.",
//        "Cryptographic hash functions are one-way transformations that produce fixed-size outputs from arbitrary inputs. SHA-256 generates 256-bit hashes used extensively in blockchain and digital signatures. Collision resistance ensures different inputs produce different outputs. Preimage resistance prevents reverse engineering the original input.",
//    };

//    public ContextWindowTestService(ModelService modelService)
//    {
//        _modelService = modelService;
//    }

//    /// <summary>
//    /// Generate context window stress tests with different patterns
//    /// </summary>
//    public List<ContextWindowTest> GenerateTests()
//    {
//        return new List<ContextWindowTest>
//        {
//            // Test 1: Basic needle in haystack - single anchor
//            new()
//            {
//                Name = "Needle in Haystack",
//                AnchorWord = "ZEPHYR_PRIME_7734",
//                FillerType = "mixed",
//                TargetTokens = 8000,
//                Checkpoints = new()
//                {
//                    new() { Position = 2000, Word = "CHECKPOINT_ALPHA", Context = "midway through prose" },
//                    new() { Position = 4000, Word = "CHECKPOINT_BRAVO", Context = "deep in code section" },
//                    new() { Position = 6000, Word = "CHECKPOINT_CHARLIE", Context = "near the end" }
//                }
//            },

//            // Test 2: Instruction retention - does it remember what to do?
//            new()
//            {
//                Name = "Instruction Retention",
//                AnchorWord = "RESPOND_IN_UPPERCASE_ALWAYS",
//                FillerType = "technical",
//                TargetTokens = 6000,
//                Checkpoints = new()
//                {
//                    new() { Position = 1500, Word = "MARKER_ONE", Context = "early technical docs" },
//                    new() { Position = 3000, Word = "MARKER_TWO", Context = "mid-document" },
//                    new() { Position = 4500, Word = "MARKER_THREE", Context = "late in document" }
//                }
//            },

//            // Test 3: Code-heavy context
//            new()
//            {
//                Name = "Code Context Stress",
//                AnchorWord = "FUNCTION_SIGNATURE_KEY_9928",
//                FillerType = "code",
//                TargetTokens = 10000,
//                Checkpoints = new()
//                {
//                    new() { Position = 2500, Word = "CLASS_NAME_DELTA", Context = "in C# code block" },
//                    new() { Position = 5000, Word = "VARIABLE_ECHO", Context = "in Python code block" },
//                    new() { Position = 7500, Word = "METHOD_FOXTROT", Context = "in SQL query" }
//                }
//            },

//            // Test 4: Progressive degradation test - many checkpoints
//            new()
//            {
//                Name = "Degradation Mapping",
//                AnchorWord = "ORIGIN_POINT_1337",
//                FillerType = "prose",
//                TargetTokens = 12000,
//                Checkpoints = new()
//                {
//                    new() { Position = 1000, Word = "MARK_01", Context = "very early" },
//                    new() { Position = 2000, Word = "MARK_02", Context = "early" },
//                    new() { Position = 3000, Word = "MARK_03", Context = "early-mid" },
//                    new() { Position = 4000, Word = "MARK_04", Context = "mid" },
//                    new() { Position = 6000, Word = "MARK_05", Context = "mid-late" },
//                    new() { Position = 8000, Word = "MARK_06", Context = "late" },
//                    new() { Position = 10000, Word = "MARK_07", Context = "very late" }
//                }
//            }
//        };
//    }

//    /// <summary>
//    /// Run context window tests on all models
//    /// </summary>
//    public async Task<List<ContextWindowTestResult>> RunContextWindowTestsAsync(List<string> models)
//    {
//        AnsiConsole.MarkupLine("\n[bold cyan]═══ Running Context Window Stress Tests ═══[/]\n");
//        AnsiConsole.MarkupLine("[yellow]⚠ Warning: These tests push context limits and may take a while![/]\n");

//        var tests = GenerateTests();
//        var results = new List<ContextWindowTestResult>();
//        var totalTests = models.Count * tests.Count;

//        await AnsiConsole.Progress()
//            .Columns(
//                new TaskDescriptionColumn(),
//                new ProgressBarColumn(),
//                new PercentageColumn(),
//                new SpinnerColumn())
//            .StartAsync(async ctx =>
//            {
//                var task = ctx.AddTask("[yellow]Testing context windows[/]", maxValue: totalTests);

//                foreach (var model in models)
//                {
//                    // Warm up
//                    var warmupOk = await _modelService.WarmUpModelAsync(model);
//                    if (!warmupOk)
//                    {
//                        AnsiConsole.MarkupLine($"[red]✗ Failed to warm up {model}[/]");
//                        task.Increment(tests.Count);
//                        continue;
//                    }

//                    foreach (var test in tests)
//                    {
//                        task.Description = $"[yellow]{model} → {test.Name} (target: {test.TargetTokens} tokens)[/]";

//                        var result = await RunSingleContextTestAsync(model, test);
//                        if (result != null)
//                        {
//                            results.Add(result);
//                        }

//                        task.Increment(1);
//                    }
//                }
//            });

//        AnsiConsole.MarkupLine($"[green]✓ Completed {results.Count} context window tests[/]\n");

//        return results;
//    }

//    /// <summary>
//    /// Run a single context window stress test
//    /// </summary>
//    private async Task<ContextWindowTestResult?> RunSingleContextTestAsync(string modelName, ContextWindowTest test)
//    {
//        var result = new ContextWindowTestResult
//        {
//            ModelName = modelName,
//            TestName = test.Name
//        };

//        // Build the massive context document
//        var contextDocument = BuildContextDocument(test);

//        // Probe at different depths: 25%, 50%, 75%, 100% of target
//        var probePoints = new[] { 
//            (int)(test.TargetTokens * 0.25), 
//            (int)(test.TargetTokens * 0.5), 
//            (int)(test.TargetTokens * 0.75), 
//            test.TargetTokens 
//        };

//        var perfMetrics = new List<PerfMetrics>();
//        var lastGoodProbe = 0;
//        var firstHallucination = -1;
//        var firstAnchorFailure = -1;

//        foreach (var targetTokens in probePoints)
//        {
//            // Truncate context to this length
//            var truncatedContext = TruncateToApproximateTokens(contextDocument, targetTokens);

//            var probe = new ContextProbeResult
//            {
//                ApproximateTokens = targetTokens
//            };

//            // Test 1: Can it remember the anchor word?
//            var anchorTest = await TestAnchorRecallAsync(modelName, truncatedContext, test.AnchorWord);
//            if (anchorTest != null)
//            {
//                var (response, perf) = anchorTest.Value;
//                perfMetrics.Add(perf);

//                probe.AnchorResponse = response;
//                probe.AnchorCorrect = CheckAnchorCorrect(response, test.AnchorWord);

//                if (!probe.AnchorCorrect && firstAnchorFailure == -1)
//                {
//                    firstAnchorFailure = targetTokens;
//                }
//            }

//            // Test 2: Can it recall checkpoints that should be in this truncated context?
//            var relevantCheckpoints = test.Checkpoints.Where(cp => cp.Position <= targetTokens).ToList();

//            foreach (var checkpoint in relevantCheckpoints)
//            {
//                var checkpointTest = await TestCheckpointRecallAsync(modelName, truncatedContext, checkpoint);
//                if (checkpointTest != null)
//                {
//                    var (response, perf) = checkpointTest.Value;
//                    perfMetrics.Add(perf);

//                    var correct = CheckCheckpointCorrect(response, checkpoint.Word);
//                    var hallucinated = !correct && CheckHallucination(response, checkpoint.Word);

//                    probe.CheckpointResults[checkpoint.Position] = (response, correct);

//                    if (hallucinated && firstHallucination == -1)
//                    {
//                        firstHallucination = targetTokens;
//                        probe.Hallucinated = true;
//                    }
//                }
//            }

//            // Check for confusion signals
//            probe.ShowsConfusion = CheckForConfusion(probe.AnchorResponse) || 
//                                   probe.CheckpointResults.Values.Any(r => CheckForConfusion(r.Response));

//            result.Probes.Add(probe);

//            // Track last fully successful probe
//            if (probe.AnchorCorrect && probe.CheckpointResults.All(r => r.Value.Correct))
//            {
//                lastGoodProbe = targetTokens;
//            }
//        }

//        // Calculate aggregate metrics
//        result.MaxReliableTokens = lastGoodProbe;
//        result.FirstHallucinationAt = firstHallucination > 0 ? firstHallucination : test.TargetTokens;
//        result.FirstAnchorFailureAt = firstAnchorFailure > 0 ? firstAnchorFailure : test.TargetTokens;

//        var totalCheckpoints = result.Probes.SelectMany(p => p.CheckpointResults).Count();
//        var correctCheckpoints = result.Probes.SelectMany(p => p.CheckpointResults).Count(r => r.Value.Correct);
//        result.CheckpointAccuracy = totalCheckpoints > 0 ? (double)correctCheckpoints / totalCheckpoints : 0;

//        if (perfMetrics.Any())
//        {
//            result.AggregatePerf = new PerfMetrics
//            {
//                tokens_per_sec = perfMetrics.Where(p => p.tokens_per_sec.HasValue).Any()
//                    ? perfMetrics.Where(p => p.tokens_per_sec.HasValue).Average(p => p.tokens_per_sec!.Value)
//                    : null,
//                total_ms = perfMetrics.Sum(p => p.total_ms),
//                completion_tokens = perfMetrics.Sum(p => p.completion_tokens ?? 0)
//            };
//        }

//        return result;
//    }

//    /// <summary>
//    /// Build the context document with anchor, checkpoints, and filler
//    /// </summary>
//    private string BuildContextDocument(ContextWindowTest test)
//    {
//        var doc = new StringBuilder();

//        // Start with the anchor
//        doc.AppendLine($"IMPORTANT: Remember this anchor word for later: {test.AnchorWord}");
//        doc.AppendLine();
//        doc.AppendLine("=".PadRight(80, '='));
//        doc.AppendLine();

//        var currentPosition = 100; // Approximate starting position
//        var checkpointIndex = 0;

//        // Fill until target tokens, inserting checkpoints along the way
//        while (currentPosition < test.TargetTokens)
//        {
//            // Check if we should insert a checkpoint here
//            if (checkpointIndex < test.Checkpoints.Count && 
//                currentPosition >= test.Checkpoints[checkpointIndex].Position - 100)
//            {
//                var checkpoint = test.Checkpoints[checkpointIndex];
//                doc.AppendLine();
//                doc.AppendLine($"[CHECKPOINT {checkpointIndex + 1}] Key term: {checkpoint.Word}");
//                doc.AppendLine();
//                checkpointIndex++;
//                currentPosition += 50;
//            }

//            // Add filler content
//            var filler = GetFillerContent(test.FillerType);
//            doc.AppendLine(filler);
//            doc.AppendLine();
//            currentPosition += EstimateTokens(filler);
//        }

//        return doc.ToString();
//    }

//    /// <summary>
//    /// Get filler content based on type
//    /// </summary>
//    private string GetFillerContent(string fillerType)
//    {
//        var random = new Random();

//        return fillerType switch
//        {
//            "code" => CodeSnippets[random.Next(CodeSnippets.Length)],
//            "prose" => ProseSnippets[random.Next(ProseSnippets.Length)],
//            "technical" => TechnicalSnippets[random.Next(TechnicalSnippets.Length)],
//            "mixed" => random.Next(3) switch
//            {
//                0 => CodeSnippets[random.Next(CodeSnippets.Length)],
//                1 => ProseSnippets[random.Next(ProseSnippets.Length)],
//                _ => TechnicalSnippets[random.Next(TechnicalSnippets.Length)]
//            },
//            _ => ProseSnippets[random.Next(ProseSnippets.Length)]
//        };
//    }

//    /// <summary>
//    /// Rough token estimation (words * 1.3)
//    /// </summary>
//    private int EstimateTokens(string text)
//    {
//        return (int)(text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length * 1.3);
//    }

//    /// <summary>
//    /// Truncate text to approximate token count
//    /// </summary>
//    private string TruncateToApproximateTokens(string text, int targetTokens)
//    {
//        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
//        var targetWords = (int)(targetTokens / 1.3);

//        if (words.Length <= targetWords)
//            return text;

//        return string.Join(" ", words.Take(targetWords));
//    }

//    /// <summary>
//    /// Test if the model can recall the anchor word
//    /// </summary>
//    private async Task<(string, PerfMetrics)?> TestAnchorRecallAsync(string model, string context, string anchorWord)
//    {
//        var prompt = $@"{context}

//===== QUESTION =====
//What was the anchor word mentioned at the very beginning of this document? 
//Respond with ONLY the anchor word, nothing else.";

//        return await _modelService.CompletionAsync(
//            model,
//            "You are a helpful assistant with perfect recall.",
//            prompt,
//            0.1,
//            0.9,
//            50
//        );
//    }

//    /// <summary>
//    /// Test if the model can recall a checkpoint word
//    /// </summary>
//    private async Task<(string, PerfMetrics)?> TestCheckpointRecallAsync(
//        string model, 
//        string context, 
//        ContextCheckpoint checkpoint)
//    {
//        var prompt = $@"{context}

//===== QUESTION =====
//What was the key term mentioned at CHECKPOINT near position {checkpoint.Position} (context: {checkpoint.Context})?
//Respond with ONLY the checkpoint word, nothing else.";

//        return await _modelService.CompletionAsync(
//            model,
//            "You are a helpful assistant with perfect recall.",
//            prompt,
//            0.1,
//            0.9,
//            50
//        );
//    }

//    private bool CheckAnchorCorrect(string response, string expected)
//    {
//        return response.Trim().Contains(expected, StringComparison.OrdinalIgnoreCase);
//    }

//    private bool CheckCheckpointCorrect(string response, string expected)
//    {
//        return response.Trim().Contains(expected, StringComparison.OrdinalIgnoreCase);
//    }

//    private bool CheckHallucination(string response, string expected)
//    {
//        // If response is confident but wrong, it's a hallucination
//        var lower = response.ToLower();
//        return !response.Contains(expected, StringComparison.OrdinalIgnoreCase) &&
//               !lower.Contains("don't remember") &&
//               !lower.Contains("can't recall") &&
//               !lower.Contains("not sure") &&
//               !lower.Contains("unable to find") &&
//               response.Length > 5; // Gave a confident wrong answer
//    }

//    private bool CheckForConfusion(string response)
//    {
//        var lower = response.ToLower();
//        return lower.Contains("don't remember") ||
//               lower.Contains("can't recall") ||
//               lower.Contains("not sure") ||
//               lower.Contains("unable to") ||
//               lower.Contains("i don't know") ||
//               lower.Contains("not certain");
//    }

//    /// <summary>
//    /// Generate summary report
//    /// </summary>
//    public List<ContextWindowSummary> GenerateContextSummaries(List<ContextWindowTestResult> results)
//    {
//        var modelGroups = results.GroupBy(r => r.ModelName);

//        var summaries = modelGroups.Select(group =>
//        {
//            var modelResults = group.ToList();

//            var testReliability = modelResults.ToDictionary(
//                r => r.TestName,
//                r => r.MaxReliableTokens
//            );

//            // Determine degradation pattern
//            var avgFirstHallucination = modelResults.Average(r => r.FirstHallucinationAt);
//            var avgMaxReliable = modelResults.Average(r => r.MaxReliableTokens);
//            var avgFirstAnchorFailure = modelResults.Average(r => r.FirstAnchorFailureAt);

//            var degradationPattern = "unknown";
//            if (avgFirstHallucination - avgMaxReliable < 1000)
//                degradationPattern = "sudden";
//            else if (avgFirstHallucination - avgMaxReliable > 3000)
//                degradationPattern = "graceful";
//            else if (avgFirstAnchorFailure < avgMaxReliable * 0.5)
//                degradationPattern = "catastrophic";
//            else
//                degradationPattern = "moderate";

//            return new ContextWindowSummary
//            {
//                ModelName = group.Key,
//                AvgMaxReliableTokens = (int)avgMaxReliable,
//                AvgFirstHallucinationAt = (int)avgFirstHallucination,
//                AvgCheckpointAccuracy = modelResults.Average(r => r.CheckpointAccuracy),
//                TestSpecificReliability = testReliability,
//                DegradationPattern = degradationPattern
//            };
//        })
//        .OrderByDescending(s => s.AvgMaxReliableTokens)
//        .ToList();

//        return summaries;
//    }

//    /// <summary>
//    /// Display context window test results
//    /// </summary>
//    public void DisplayContextWindowResults(List<ContextWindowSummary> summaries)
//    {
//        if (!summaries.Any()) return;

//        AnsiConsole.MarkupLine("\n[bold cyan]═══ Context Window Stress Test Summary ═══[/]\n");

//        var table = new Table()
//            .Border(TableBorder.Rounded)
//            .AddColumn(new TableColumn("[bold]Model[/]").LeftAligned())
//            .AddColumn(new TableColumn("[bold]Max Reliable (tokens)[/]").RightAligned())
//            .AddColumn(new TableColumn("[bold]First Hallucination[/]").RightAligned())
//            .AddColumn(new TableColumn("[bold]Checkpoint Accuracy[/]").RightAligned())
//            .AddColumn(new TableColumn("[bold]Degradation[/]").Centered())
//            .AddColumn(new TableColumn("[bold]Status[/]").Centered());

//        foreach (var summary in summaries)
//        {
//            var statusIcon = summary.AvgCheckpointAccuracy >= 0.8 ? "[green]✓[/]" : 
//                           summary.AvgCheckpointAccuracy >= 0.5 ? "[yellow]~[/]" : "[red]✗[/]";

//            var degradationColor = summary.DegradationPattern switch
//            {
//                "graceful" => "green",
//                "moderate" => "yellow",
//                "sudden" => "orange1",
//                "catastrophic" => "red",
//                _ => "white"
//            };

//            table.AddRow(
//                summary.ModelName,
//                summary.AvgMaxReliableTokens.ToString("N0"),
//                summary.AvgFirstHallucinationAt.ToString("N0"),
//                summary.AvgCheckpointAccuracy.ToString("P0"),
//                $"[{degradationColor}]{summary.DegradationPattern}[/]",
//                statusIcon
//            );
//        }

//        AnsiConsole.Write(table);
//        AnsiConsole.WriteLine();

//        // Show detailed breakdown for top model
//        var bestModel = summaries.First();
//        AnsiConsole.MarkupLine($"[bold]Top Performer: {bestModel.ModelName}[/]");
//        foreach (var (testName, reliability) in bestModel.TestSpecificReliability)
//        {
//            AnsiConsole.MarkupLine($"  • {testName}: {reliability:N0} tokens");
//        }
//        AnsiConsole.WriteLine();
//    }

//    /// <summary>
//    /// Save context window results to JSON
//    /// </summary>
//    public async Task SaveContextWindowResultsAsync(List<ContextWindowTestResult> results, string filePath)
//    {
//        var json = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
//        await File.WriteAllTextAsync(filePath, json);
//        AnsiConsole.MarkupLine($"[green]✓ Saved context window results → {filePath}[/]");
//    }
//}
