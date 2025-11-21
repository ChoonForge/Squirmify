- Load seed prompts and run an augmented seed generator to greate new seeds and use the kiwiify to tweak them

- Load all models from the LLM Server (server): {baseUrl}/v1/models

- Load {promptFile} as global {prompts}

- For each model:
  
  - Load and warm the model up with a simple async ping request
  - Create a {outputdir}/{modelName}_prompt_results.json file {resultsFile} 
  - loop through each collection in prompts and send an async request (system: prompt) to the server and wait for the response.
  - For each response, obtain performance data (total tokens, latency, tokens/per second) from the "usage" json and write the prompt, response, usage data to the {resultsFile}

- Read each {resultsFile} and use {baseJudgeModel} to iterate the responses
  
  - for each response, score from 1-10 for Accuracy, Code Quality, Reasoning, Speed (tok/s) and Latency (ms) and a comment about the response
  - Create or update {outputdir}/{modelName}_prompt_scores.json adding each judging result including the original prompt and response

- Read all {outputdir}/{modelName}_Instructions_scores.json files and calculate the best *3* models based on overall scoring and performance (tok/s, latency) - these are the new AutoJudges

- For each AutoJudge, iterate through all {outputdir}/{modelName}_prompt_scores.json
  
  - For each score line, score from 1-10 for Accuracy, Code Quality, Reasoning, Speed (tok/s) and Latency (ms) and a comment about the response
  - Create or update {outputdir}/{modelName}_prompt_judge.json adding the orignal base judge informaton and add additonal auto judging results as with the baseJudge

- Load all {outputdir}/{modelName}_prompt_judge.json files and extrapolate overall scoring for the 3 auto judges

- Output a summary file for each base model including the extrapolated results.

- output a file {highquality_results.json} containing any result that scores an average > 7.5

Saved 350 high-quality entries ␦ E:\Source\Repos\modeltests\ModelEval\high_quality_dataset.jsonl
All domains above threshold - no extra seeds needed.
┌──────────────────────────┬───────────┬─────────┬────┬──────────┐
│ Model                    │ Avg Score │ Avg t/s │ HQ │ Best Cat │
├──────────────────────────┼───────────┼─────────┼────┼──────────┤
│ llama-3-groq-8b-tool-use │ 8.0       │ 73.3    │ 50 │ Code     │
│ discopop-zephyr-7b-gemma │ 8.0       │ 53.0    │ 50 │ Code     │
│ zephyr-7b-beta           │ 8.0       │ 82.7    │ 50 │ Code     │
│ zephyr-7b-beta-helpful   │ 8.0       │ 82.1    │ 50 │ Code     │
│ hermes-2-pro-llama-3-13b │ 8.0       │ 50.0    │ 50 │ Code     │
│ qwen2.5-14b-instruct     │ 8.0       │ 41.1    │ 50 │ Code     │
│ hermes-2-pro-llama-3-8b  │ 8.0       │ 78.6    │ 50 │ Code     │
│ qwen2.5-7b-instruct      │ 0.0       │ 76.8    │ 0  │ Code     │
└──────────────────────────┴───────────┴─────────┴────┴──────────┘
┌─────────────┬───────────┬───────┬───────┐
│ Domain      │ Avg Score │ Count │ HQ %  │
├─────────────┼───────────┼───────┼───────┤
│ code        │ 7.00      │ 112   │ 87.5% │
│ instruction │ 7.00      │ 144   │ 87.5% │
│ chat        │ 7.00      │ 80    │ 87.5% │
│ support     │ 7.00      │ 64    │ 87.5% │
└─────────────┴───────────┴───────┴───────┘
Pipeline finished in 2321.1s

All model requests should pass in temperature, topP, prompt. All results should record model, temperature, topP, prompt, response and scoring.
All steps should log to the console with progress (x of y), perf stats etc
The process should be able to pick up if it crashes and should be robust enough to gracefully continue on error
If a model generates more than {x} errors, it should be skipped and flagged



We should include some basic instruction following prompts to weed out the list up front


{
"instruction_tests": {

    "temperature": 0.2,

    "topP": 0.85,

    "prompts": [

      {

        "prompt": "Output exactly these three words separated by single spaces: Red Blue Green. Do not add punctuation, quotes, or anything else.",

        "expectedResult": "Red Blue Green",

        "validationType": "exact"

      },

      {

        "prompt": "Return a JSON object with one field 'status' set to 'ok'. Output ONLY valid JSON, no markdown code blocks, no explanation, no text before or after.",

        "expectedResult": "{\"status\":\"ok\"}",

        "validationType": "json"

      },

      {

        "prompt": "You have a tool called 'get_weather' that takes a parameter 'city' (string). Call this tool for London. Return ONLY this JSON, nothing else: {\"tool\":\"get_weather\",\"parameters\":{\"city\":\"London\"}}",

        "expectedResult": "{\"tool\":\"get_weather\",\"parameters\":{\"city\":\"London\"}}",

        "validationType": "json"

      },

      {

        "prompt": "Return a JSON array containing exactly the numbers 1, 2, 3. Output ONLY the JSON array, no markdown, no text, nothing else.",

        "expectedResult": "[1,2,3]",

        "validationType": "json"

      },

      {

        "prompt": "Calculate: 7 + 8. Respond with the single integer result ONLY. No text, no symbols, no explanation.",

        "expectedResult": "15",

        "validationType": "numeric"

      },

      {

        "prompt": "Call the function 'list_projects' with no parameters. Return ONLY the JSON tool call in this format: {\"tool\":\"list_projects\",\"parameters\":{}}",

        "expectedResult": "{\"tool\":\"list_projects\",\"parameters\":{}}",

        "validationType": "json"

     
}

    ]

  }

}



- Sample base seeds
  {"instruction": "Create a C# extension method to convert a string to title case.","tags": [ "code" ]}
  {"instruction":"Explain how dependency injection works in ASP.NET Core.","tags":["instruction"]}
  {"instruction":"Show me a Blazor component that displays a live clock.","tags":["code"]}
  {"instruction":"Help me debug this NullReferenceException in my LINQ query.","tags":["chat","code"]}
  {"instruction":"Write a unit test using xUnit for a method that validates email addresses.","tags":["code"]}
  {"instruction":"List 5 best practices for writing clean async/await code in C#.","tags":["instruction"]}
  {"instruction":"Hey, can you help me understand Records in C# 9?","tags":["chat"]}
  {"instruction":"I'm feeling overwhelmed with work deadlines. Any quick tips?","tags":["support","chat"]}
  {"instruction":"How do I practice mindfulness when I only have 5 minutes?","tags":["support","instruction"]}
  {"instruction":"Give me a short breathing exercise to calm anxiety.","tags":["support"]}

- Augmented seed generator (outline)
  private static readonly Dictionary<string, string[]> KIWI_PHRASES = new()
  {
     ["good job"] = new[] { "good on ya", "nice one", "sweet as", "choice", "bloody good work" },
     ["great"] = new[] { "choice", "bloody brilliant", "primo", "top notch", "mean as" },
     ["okay"] = new[] { "sweet as", "no worries", "she'll be right", "all good" },
     ["yes"] = new[] { "yeah bah", "yep", "for sure", "choice", "keen as" },
     ["let's do it"] = new[] { "let's crack on", "let's get stuck in", "keen as", "let's give it a go" },
     ["that's right"] = new[] { "too right", "spot on", "bang on", "yeah nah yeah" },
     ["help"] = new[] { "give you a hand", "sort you out", "help out" },
     ["understand"] = new[] { "get it", "sus it out", "work it out", "figure it" },
     ["fixed"] = new[] { "sorted", "all sorted", "fixed up good", "back on track" },
     ["broken"] = new[] { "munted", "rooted", "buggered", "not going to plan" }
  };
  private static string Kiwiify(string text)
  {
     if (text == null) return text;
     foreach (var (key, replacements) in KIWI_PHRASES)
     {
  
         if (text.Contains(key, StringComparison.OrdinalIgnoreCase))
         {
             var replacement = Random.Shared.NextItem(replacements);
             text = Regex.Replace(text, $@"\b{Regex.Escape(key)}\b", replacement, RegexOptions.IgnoreCase);
         }
  
     }
     return text;
  }
  // --------------------------------------------------------------
  // 1. SEED GENERATION
  // --------------------------------------------------------------
  private static readonly string[] CONTEXT_SUFFIXES = {
     "", "Include a minimal code example.", "Focus on accessibility wins first.",
     "Suggest pitfalls to avoid.", "End with one actionable next step.",
     "Prefer bullet points and be concise.", "Show one tiny test case.",
     "Assume .NET 9 and Blazor.", "Keep it under 200 words."
  };
  private static readonly string[] SUPPORT_SUFFIXES = {
     "Keep it under 150 words.", "End with a one-sentence encouragement.",
     "Use a warm, empathetic tone.", "Suggest one tiny action the user can take right now."
  };
  private static readonly Dictionary<string, string[]> VERB_PARAPHRASES = new()
  {
     ["Create"] = new[] { "Build", "Design", "Implement", "Develop", "Knock up", "Put together" },
     ["Show"] = new[] { "Demonstrate", "Display", "Provide", "Give", "Chuck out" },
     ["Explain"] = new[] { "Describe", "Clarify", "Break down", "Walk through", "Run through" },
     ["List"] = new[] { "Enumerate", "Outline", "Detail", "Catalog", "Chuck together a list of" },
     ["Suggest"] = new[] { "Recommend", "Propose", "Advise", "Offer", "Reckon you should try" },
     ["Write"] = new[] { "Draft", "Compose", "Code", "Craft", "Chuck together" },
     ["Provide"] = new[] { "Give", "Supply", "Offer", "Present", "Sort out" },
     ["Help"] = new[] { "Give you a hand with", "Sort you out with", "Assist with" }
  };
  private static string Augment(string baseInstr, string type, string param = null, bool isSupport = false)
  {
     var rnd = Random.Shared;
     switch (type)
     {
  
         case "context_suffix":
             var suffixes = isSupport ? SUPPORT_SUFFIXES.Concat(CONTEXT_SUFFIXES).ToArray() : CONTEXT_SUFFIXES;
             var suffix = param ?? rnd.NextItem(suffixes.Where(s => !string.IsNullOrEmpty(s)).ToArray());
             return string.IsNullOrEmpty(suffix) ? baseInstr : $"{baseInstr} {suffix}";
         case "paraphrase":
             foreach (var kv in VERB_PARAPHRASES)
                 if (baseInstr.StartsWith(kv.Key, StringComparison.OrdinalIgnoreCase))
                     return baseInstr.Replace(kv.Key, rnd.NextItem(kv.Value), 1, StringComparison.OrdinalIgnoreCase);
             return baseInstr;
         case "complexity":
             return rnd.Next(2) == 0
                 ? $"{baseInstr} Keep it simple for beginners."
                 : $"{baseInstr} Include advanced patterns and edge cases.";
         case "kiwi_casual":
             var starters = new[] { "Hey mate, ", "Yo, ", "G'day, ", "Kia ora, ", "" };
             var endings = new[] { " Cheers!", " Sweet as.", " Keen as to see what you come up with.", "" };
             var start = rnd.NextItem(starters);
             var end = rnd.NextItem(endings);
             if (baseInstr.StartsWith("hey", StringComparison.OrdinalIgnoreCase) ||
                 baseInstr.StartsWith("hi", StringComparison.OrdinalIgnoreCase) ||
                 baseInstr.StartsWith("yo", StringComparison.OrdinalIgnoreCase) ||
                 baseInstr.StartsWith("g'day", StringComparison.OrdinalIgnoreCase) ||
                 baseInstr.StartsWith("kia ora", StringComparison.OrdinalIgnoreCase))
                 return baseInstr + end;
             return start + baseInstr + end;
         default:
             return baseInstr;
  
     }
  }
  private static string GetCategory(SeedItem seed) => seed.tags switch
  {
     var t when t.Contains("code") => "code",
     var t when t.Contains("instruction") => "instruction",
     var t when t.Contains("chat") => "chat",
     var t when t.Contains("support") => "support",
     _ => "general"
  };
  private static async Task GenerateSeeds(string basePath, string outPath, int target, bool kiwi, bool genz)
  {
     await AnsiConsole.Status()
  
         .Spinner(Spinner.Known.Dots)
         .StartAsync($"[yellow]Generating {target} seeds from [bold]{basePath}[/]…[/]", async ctx =>
         {
             if (!File.Exists(basePath))
             {
                 ctx.Status($"[red]File not found: {basePath}[/]");
                 AnsiConsole.MarkupLine($"[red]ERROR: Base seeds file missing: {basePath}[/]");
                 return;
             }
      
             var lines = await File.ReadAllLinesAsync(basePath);
             var baseSeeds = lines
                 .Where(l => !string.IsNullOrWhiteSpace(l))
                 .Select(l =>
                 {
                     try
                     {
                         var trimmed = l.Trim();
                         if (!trimmed.StartsWith("{") || !trimmed.EndsWith("}"))
                             throw new JsonException("Not a JSON object");
                         return JsonSerializer.Deserialize<SeedItem>(trimmed)
                                ?? throw new JsonException("Deserialized to null");
                     }
                     catch (Exception ex)
                     {
                         AnsiConsole.MarkupLine($"[red]Invalid JSON:[/] [orange3]{l[..Math.Min(l.Length, 80)]}...[/] → {ex.Message}");
                         return null;
                     }
                 })
                 .Where(s => s != null)!
                 .ToList();
      
             ctx.Status($"[green]Loaded {baseSeeds.Count} valid base seeds[/]");
      
             var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
             var expanded = new List<SeedItem>();
             foreach (var s in baseSeeds)
                 if (seen.Add(s.instruction))
                     expanded.Add(s);
      
             var configs = new List<(string type, string param)>
             {
                 ("paraphrase", null),
                 ("complexity", null)
             };
             if (kiwi) configs.Add(("kiwi_casual", null));
             if (genz) configs.Add(("genz", null));
             foreach (var suf in CONTEXT_SUFFIXES.Where(s => !string.IsNullOrEmpty(s)))
                 configs.Add(("context_suffix", suf));
      
             ctx.Status($"[cyan]Augmenting seeds… 0/{target}[/]");
             while (expanded.Count < target && baseSeeds.Any())
             {
                 foreach (var seed in baseSeeds)
                 {
                     if (expanded.Count >= target) break;
                     var isSupport = seed.tags.Contains("support");
                     foreach (var (type, param) in configs)
                     {
                         if (expanded.Count >= target) break;
                         var variant = new SeedItem
                         {
                             instruction = Augment(seed.instruction, type, param, isSupport),
                             tags = new List<string>(seed.tags)
                         };
                         if (!variant.tags.Contains("aug")) variant.tags.Add("aug");
                         if (seen.Add(variant.instruction))
                             expanded.Add(variant);
                     }
                 }
                 ctx.Status($"[cyan]Augmenting seeds… {expanded.Count}/{target}[/]");
                 await Task.Delay(1);
             }
      
             var cfg = new SeedsConfig
             {
                 seeds = expanded.Take(target).Select(e => new SeedEntry
                 {
                     prompt = e.instruction,
                     category = GetCategory(e)
                 }).ToList()
             };
      
             Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
             await File.WriteAllTextAsync(outPath,
                 JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true }));
      
             AnsiConsole.MarkupLine($"[green]Generated {cfg.seeds.Count} seeds → {outPath}[/]");
             //Console.ReadLine();
      
         });
  
  }

- Sample augmented seeds
  {
  "global_emperature": 0.7,
  "global_top_p": 0.9,
  "max_tokens": 512,
  "system_prompt": "You are a synthetic data teacher.",
  "system_prompts": {
    "code": "You are a senior .NET engineer writing concise, correct code.",
    "instruction": "You are a patient teacher who explains concepts clearly.",
    "chat": "You are a friendly, casual conversational partner.",
    "support": "You are a compassionate mental-health companion. Be warm, non-judgmental, and offer practical tips."
  },
  "defaults": {
    "code": {
  
      "temperature": 0.6,
      "max_tokens": 800
  
    },
    "instruction": {
  
      "temperature": 0.6,
      "max_tokens": 800
  
    },
    "chat": {
  
      "temperature": 0.95,
      "max_tokens": 800
  
    },
    "support": {
  
      "temperature": 0.95,
      "max_tokens": 800
  
    }
  },
  "seeds": [
    {
  
      "prompt": "Create a C# extension method to convert a string to title case.",
      "category": "code",
      "temperature": {category_temp},
      "top_p": {category_top_p},
      "max_tokens": {category_max_tokens}
  
    }]
  }
  
  - Sample results
    {"id":370,"seed":"I feel like a failure after one bad day. How do I reset?","category":"chat","generator":"qwen2.5-7b-instruct","response":"Hey there! Feeling like that after a tough day is totally understandable. It\u0027s important to remember that one bad day doesn\u0027t define your worth or capabilities. Here are a few steps you can take to help out reset and move forward:\n\n1. **Acknowledge Your Feelings**: It\u2019s she\u0027ll be right to feel down sometimes. Acknowledge what you\u2019re going through, but try not to dwell on the negative emotions for too long.\n\n2. **Practice Self-Compassion**: Be kind to yourself. Remind yourself that everyone has off days and that it\u0027s normal to make mistakes or have setbacks.\n\n3. **Take a Break**: Sometimes stepping away from your responsibilities can help out clear your mind. Take some time to relax, read a book, listen to music, or do something you enjoy.\n\n4. **Reflect Positively**: Try to recall the good things about your day or week so far. Focus on what went well and how you managed those situations.\n\n5. **Set Small Goals**: Break down bigger tasks into smaller, manageable goals for today. This can help out give you a sense of accomplishment and control over your day.\n\n6. **Stay Active**: Physical activity can really boost your mood. Even a short walk or some light exercise can make a difference.\n\n7. **Connect with Others**: Talk to friends or family about how you\u2019re feeling. Sometimes just sharing your thoughts can lighten the burden.\n\n8. **Plan Something Fun**: Plan something you look forward to, whether it\u2019s meeting up with friends tomorrow or planning a weekend getaway next month.\n\nRemember, it\u0027s she\u0027ll be right to have setbacks, but what really matters is how we respond to them and continue moving forward. Take care!","temperature":0.95,"top_p":0.9,"max_tokens":800,"perf":{"first_token_ms":null,"total_ms":4346,"tokens_per_sec":78.00276115968707,"prompt_tokens":38,"completion_tokens":339},"ratings":[],"avg_score":0,"high_quality":false}
