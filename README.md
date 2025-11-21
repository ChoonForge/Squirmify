# Model Evaluator - Conversation, Reasoning & Context Window Tests

## 📦 New Files Created

### 1. **ConversationTest.cs**
Models for multi-turn conversation testing:
- `ConversationTest` - Test definition with turns and judging criteria
- `ConversationTestResult` - Results with full exchange history
- `ConversationExchange` - Single turn (user + model response)
- `ConversationRating` - Judge's detailed scoring
- `ConversationTestSummary` - Per-model aggregated results

### 2. **ConversationTestService.cs**
Service that runs and scores multi-turn conversations:
- **8 conversation scenarios** across 4 categories:
  - **Code** (2): Debugging null reference, refactoring nested loops
  - **Support** (2): Password reset, product feature explanation
  - **Chat** (2): Hobby discussion, weekend plans
  - **Instruction** (2): Todo list modifications, email refinement

**Key Features:**
- Maintains conversation context across 3-4 turns
- Builds context-aware prompts (since ModelService doesn't support message history)
- Judge scores on 4 dimensions: topic coherence, conversational tone, context retention, helpfulness
- Each conversation gets ONE overall score (1-10)
- Detailed category performance breakdown

### 3. **ReasoningTestService.cs** (Refactored)
**MAJOR CHANGE:** Removed brittle validators, now uses judge-based scoring!

**11 reasoning tests** across 5 categories:
- **multi-step** (3): Sheep problem, widget rate, age puzzle
- **context** (2): Multi-number calc, relationship ordering
- **logic** (2): Syllogism flaw, prime number definition
- **math** (2): Shopping calc, percentage
- **pattern** (2): Number sequence, letter sequence

**Scoring Approach:**
- Judge evaluates: correct answer, logical steps, clarity
- Flexible - doesn't require exact formatting
- Each dimension scored 1-10
- Overall score = holistic assessment

### 4. **TestService.cs** (Enhanced)
**14 instruction tests** (was 6):

**Added:**
- More JSON tests (object with name/age)
- More tool calling scenarios (calculate function)
- Format constraints (color list, SUCCESS output)
- Simple calculations (multiplication, subtraction)
- Boolean outputs (true/false)

**Now shows:** Pass count (e.g., "12/14") in results table

### 5. **Config.cs** (Updated)
Added:
```csharp
// Conversation Test Settings
public const double ConversationTestTemperature = 0.7;
public const double ConversationTestTopP = 0.9;
```

### 6. **ContextWindowTestService.cs** (NEW!)
**The marketing team's worst nightmare!** 🔥

Tests the gap between **claimed** vs **actual usable** context window:

**4 Stress Test Patterns:**
- **Needle in Haystack** (8k tokens): Single anchor word + 3 checkpoints, mixed content
- **Instruction Retention** (6k tokens): Can it remember instructions buried deep?
- **Code Context Stress** (10k tokens): Heavy code content with checkpoints
- **Degradation Mapping** (12k tokens): 7 checkpoints to map where failure happens

**How it works:**
1. Injects **ANCHOR_WORD** at start (e.g., "ZEPHYR_PRIME_7734")
2. Floods with code/prose/technical content
3. Plants **checkpoint words** at specific token positions
4. Probes at 25%, 50%, 75%, 100% of target length
5. Asks: "What was the anchor?" and "What was checkpoint X?"

**Tracks:**
- **Max Reliable Tokens**: Last probe where everything was correct
- **First Hallucination At**: When it starts making up confident wrong answers
- **First Anchor Failure**: When it forgets the very first thing
- **Checkpoint Accuracy**: % of checkpoints recalled correctly
- **Degradation Pattern**: "graceful", "sudden", "moderate", or "catastrophic"

**Exposes models that:**
- Claim 32k context but fail at 8k
- Hallucinate confidently instead of admitting uncertainty
- Only remember recent stuff (recency bias)
- Can't maintain instructions through long context

This will separate the wheat from the chaff! 😈

---

## 🔌 Integration Points

### In Program.cs, add after reasoning tests:

```csharp
// ═══ Step 2.6: Context Window Stress Tests ═══
AnsiConsole.MarkupLine("[bold cyan]═══ Step 2.6: Context Window Stress Tests ═══[/]\n");

var contextWindowService = new ContextWindowTestService(modelService);
var contextResults = await contextWindowService.RunContextWindowTestsAsync(
    models.Where(m => testResults.Any(t => t.ModelName == m && t.PassRate >= 0.8)).ToList()
);

// Generate and display summaries
var contextSummaries = contextWindowService.GenerateContextSummaries(contextResults);
contextWindowService.DisplayContextWindowResults(contextSummaries);

// Save results
var contextFile = Path.Combine(Config.OutputDir, "context_window_results.json");
await contextWindowService.SaveContextWindowResultsAsync(contextResults, contextFile);
```

### For Conversation Tests:

```csharp
// ═══ Step 2.7: Conversation Tests ═══
AnsiConsole.MarkupLine("[bold cyan]═══ Step 2.7: Conversation Tests ═══[/]\n");

var conversationService = new ConversationTestService(modelService);
var conversationResults = await conversationService.RunConversationTestsAsync(
    models.Where(m => testResults.Any(t => t.ModelName == m && t.PassRate >= 0.8)).ToList()
);

// Score conversations with base judge
await conversationService.ScoreConversationsAsync(baseJudge, conversationResults);

// Generate and display summaries
var conversationSummaries = conversationService.GenerateConversationSummaries(conversationResults);
conversationService.DisplayConversationResults(conversationSummaries);

// Save results
var conversationFile = Path.Combine(Config.OutputDir, "conversation_results.json");
await conversationService.SaveConversationResultsAsync(conversationResults, conversationFile);
```

### For Reasoning Tests (replace existing):

```csharp
// ═══ Step 2.5: Reasoning Tests ═══
AnsiConsole.MarkupLine("[bold cyan]═══ Step 2.5: Reasoning Tests ═══[/]\n");

var reasoningService = new ReasoningTestService(modelService);
var reasoningResponses = await reasoningService.RunReasoningTestsAsync(
    models.Where(m => testResults.Any(t => t.ModelName == m && t.PassRate >= 0.8)).ToList()
);

// Score with base judge
await reasoningService.ScoreReasoningTestsAsync(baseJudge, reasoningResponses);

// Generate and display summaries
var reasoningSummaries = reasoningService.GenerateReasoningSummaries(reasoningResponses);
reasoningService.DisplayReasoningResults(reasoningSummaries);

// Save results
var reasoningFile = Path.Combine(Config.OutputDir, "reasoning_results.json");
await reasoningService.SaveReasoningResultsAsync(reasoningResponses, reasoningFile);
```

---

## 🎯 What Changed & Why

### **Context Window Tests - NEW** 🚨
**Problem:** Models claim huge context windows (32k+) but marketing != reality
**Solution:**
- 4 different stress patterns (needle in haystack, instruction retention, code-heavy, degradation mapping)
- Plants anchor word at start + checkpoints throughout
- Probes at 25%, 50%, 75%, 100% of target length
- Tracks when hallucination starts vs when it admits confusion
- Measures degradation pattern (graceful vs catastrophic failure)
- **Exposes the gap between claimed and actual usable context**

### **Conversation Tests - NEW**
**Problem:** Needed to test multi-turn conversational ability
**Solution:** 
- 8 realistic scenarios with 3-4 turn exchanges
- Context management via prompt engineering (since no message history support)
- Judge evaluates entire conversation holistically
- Separate scoring by category (code, support, chat, instruction)

### **Reasoning Tests - REFACTORED**
**Problem:** Brittle validators failing on good answers with wrong format
**Solution:**
- Removed hardcoded validators entirely
- Judge evaluates correctness, logic, and clarity
- Flexible scoring (1-10 scale) instead of pass/fail
- Still measures what matters: can they reason?

### **Instruction Tests - ENHANCED**
**Problem:** Only 6 tests, limited coverage
**Solution:**
- 14 tests covering: JSON, tool calling, format constraints, calculations, booleans
- More comprehensive coverage of instruction-following ability
- Added pass count to results display

---

## 📊 Output Files

The pipeline will now generate:
- `context_window_results.json` - Context window stress test results with degradation tracking
- `conversation_results.json` - Full conversation exchanges with ratings
- `reasoning_results.json` - Reasoning responses with judge scores
- Enhanced instruction test results

---

## 🚀 Key Benefits

1. **Context Window Tests:**
   - **Calls out bullshit marketing claims** 📢
   - Shows where models actually fail vs claimed limits
   - Identifies hallucination vs honest confusion
   - Maps degradation curves (graceful vs catastrophic)
   - Exposes recency bias

2. **Conversation Tests:**
   - Measures real conversational ability over multiple turns
   - Identifies models that maintain context
   - Separate scores per category (see who excels where)

3. **Reasoning Tests (Refactored):**
   - No more false negatives from formatting
   - Actual reasoning quality measured
   - Judge explains WHY scores were given

4. **Instruction Tests (Enhanced):**
   - Better coverage of instruction-following
   - More tool-calling scenarios
   - Clearer pass/fail feedback

---

## 🔧 Technical Notes

### Context Management in Conversations
Since `ModelService.CompletionAsync` doesn't support passing message history:
- Built `BuildContextPrompt()` helper
- Injects conversation history into prompt
- Format: "Previous conversation:\nUser: ...\nAssistant: ...\n\nUser: [current]\n"

### Judge-Based Scoring
All three test types now use judge models for evaluation:
- **Instruction**: Still uses exact/JSON validation (appropriate for rigid requirements)
- **Conversation**: Judge evaluates holistic conversation quality
- **Reasoning**: Judge evaluates correctness, logic, clarity

---

## 🎨 Usage Example

```csharp
// In your evaluator pipeline:
var conversationService = new ConversationTestService(modelService);
var results = await conversationService.RunConversationTestsAsync(models);
await conversationService.ScoreConversationsAsync(judgeModel, results);

var summaries = conversationService.GenerateConversationSummaries(results);
conversationService.DisplayConversationResults(summaries);
```

---

**All files ready in `/mnt/user-data/outputs/`** 🎉
