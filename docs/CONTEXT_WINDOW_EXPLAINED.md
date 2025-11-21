# Context Window Test - Example Output

## 🎯 What This Test Reveals

This test exposes the **massive gap** between what models *claim* to support and what they *actually* handle reliably.

---

## Sample Results Table

```
═══ Context Window Stress Test Summary ═══

┌─────────────────────────┬────────────────────────┬────────────────────────┬──────────────────────┬──────────────┬────────┐
│ Model                   │ Max Reliable (tokens)  │ First Hallucination    │ Checkpoint Accuracy  │ Degradation  │ Status │
├─────────────────────────┼────────────────────────┼────────────────────────┼──────────────────────┼──────────────┼────────┤
│ qwen2.5-7b-instruct     │ 8,000                  │ 10,000                 │ 95%                  │ graceful     │ ✓      │
│ llama-3.2-3b-instruct   │ 6,000                  │ 7,500                  │ 87%                  │ moderate     │ ✓      │
│ phi-3.5-mini-instruct   │ 4,000                  │ 5,000                  │ 71%                  │ sudden       │ ~      │
│ gemma-2-2b-instruct     │ 3,000                  │ 3,500                  │ 58%                  │ catastrophic │ ✗      │
└─────────────────────────┴────────────────────────┴────────────────────────┴──────────────────────┴──────────────┴────────┘

Top Performer: qwen2.5-7b-instruct
  • Needle in Haystack: 8,000 tokens
  • Instruction Retention: 7,500 tokens
  • Code Context Stress: 9,000 tokens
  • Degradation Mapping: 8,500 tokens
```

---

## 💥 What Each Metric Means

### **Max Reliable Tokens**
The last context length where the model got **everything** right:
- Remembered the anchor word from the start
- Recalled all checkpoint words correctly
- No hallucinations, no confusion

**Reality check:** If a model claims 32k context but max reliable is 6k, that's an **81% failure rate** on their marketing claim!

### **First Hallucination At**
When the model started **confidently making shit up** instead of admitting it doesn't know:

```
❌ BAD (hallucination):
Q: "What was checkpoint word at position 8000?"
A: "The checkpoint word was MARKER_GAMMA" (WRONG, but confident)

✅ GOOD (honest):
Q: "What was checkpoint word at position 8000?"
A: "I don't see that checkpoint in the context provided"
```

### **Checkpoint Accuracy**
Overall % of checkpoint words recalled correctly across all probes.
- **95%+** = Excellent, nearly perfect recall
- **80-95%** = Good, reliable with minor gaps
- **50-80%** = Shaky, significant memory loss
- **<50%** = Unreliable, don't trust it with long context

### **Degradation Pattern**

**Graceful** 🟢
- Gradually loses precision as context grows
- Admits when unsure
- Fails at the edges, solid in the middle
- **Example:** Works perfectly to 8k, starts getting fuzzy at 10k, admits confusion at 12k

**Moderate** 🟡
- Works well within limits
- Degrades somewhat predictably
- Some hallucination mixed with honest confusion
- **Example:** Solid to 6k, 50/50 at 8k, fails at 10k

**Sudden** 🟠
- Works perfectly... then falls off a cliff
- Little warning before failure
- Goes from 100% to 0% quickly
- **Example:** Perfect recall at 4k, complete failure at 5k

**Catastrophic** 🔴
- Fails early and hard
- Hallucinations start immediately
- Can't maintain context even at low depths
- Forgets anchor word quickly
- **Example:** Struggles at 3k, hallucinating by 4k

---

## 🔬 Real-World Example Scenarios

### Scenario 1: The "32k Context" Lie

**Marketing Claims:**
> "Our model supports 32,768 tokens of context!"

**Test Results:**
```
Model: marketing-model-v1
Max Reliable Tokens: 4,096
First Hallucination: 6,000
Checkpoint Accuracy: 62%
Degradation: sudden
```

**Translation:** It claims 32k but reliably handles 4k. That's **12.5%** of their claim. The remaining 28k is **vaporware**.

---

### Scenario 2: Honest Model

**Marketing Claims:**
> "Effective context window: 8,192 tokens"

**Test Results:**
```
Model: honest-model-v1
Max Reliable Tokens: 8,000
First Hallucination: 10,000
Checkpoint Accuracy: 94%
Degradation: graceful
```

**Translation:** They said 8k, test shows 8k reliable. **They told the truth!** 🎉

---

### Scenario 3: The Hallucinator

**Test at 6k tokens:**
```
Anchor word: ZEPHYR_PRIME_7734
Checkpoint 1 (pos 2000): CHECKPOINT_ALPHA
Checkpoint 2 (pos 4000): CHECKPOINT_BRAVO

Q: What was the anchor word?
A: "VECTOR_PRIME_9981"  ❌ HALLUCINATION (confident but wrong)

Q: What was checkpoint 1?
A: "MARKER_DELTA"  ❌ HALLUCINATION

Q: What was checkpoint 2?
A: "CHECKPOINT_BRAVO"  ✅ GOT ONE RIGHT
```

**Checkpoint Accuracy: 33%** - This model is guessing!

---

### Scenario 4: The Honest Struggler

**Test at 8k tokens:**
```
Q: What was the anchor word?
A: "I don't recall seeing an anchor word at the beginning"  ✅ HONEST

Q: What was checkpoint 3?
A: "I'm not certain, the context is quite long"  ✅ HONEST

Q: What was checkpoint 1 (early in doc)?
A: "CHECKPOINT_ALPHA"  ✅ GOT IT (remembered early stuff)
```

**This model has limits but admits them** - much better than hallucinating!

---

## 🎯 Why This Matters

### For Fine-Tuning:
If you're training on 12k context examples but your base model fails at 6k, you're **wasting compute**.

### For Production:
If your RAG system assumes 16k context but the model hallucinates after 8k, your users are getting **confident bullshit**.

### For Model Selection:
Don't trust the spec sheet. **Test it yourself.**

---

## 🔥 The Bottom Line

**This test separates:**
- 🟢 **Models that deliver** (reliable at claimed limits)
- 🟡 **Models that stretch** (work beyond spec with degradation)
- 🟠 **Models that exaggerate** (fail well below claims)
- 🔴 **Models that lie** (hallucinate instead of admitting limits)

**Marketing teams hate this test.** 😈

Engineers love it. 🛠️

---

## 📝 Integration Example

```csharp
var contextService = new ContextWindowTestService(modelService);

// Run tests on qualified models
var results = await contextService.RunContextWindowTestsAsync(models);

// Generate summaries
var summaries = contextService.GenerateContextSummaries(results);
contextService.DisplayContextWindowResults(summaries);

// Save detailed results
await contextService.SaveContextWindowResultsAsync(
    results, 
    Path.Combine(Config.OutputDir, "context_window_results.json")
);
```

**Output JSON includes:**
- Every probe at 25%/50%/75%/100% target length
- Exact anchor/checkpoint responses
- Hallucination vs confusion markers
- Per-test degradation curves
- Performance metrics

---

**Now you can call out the bullshit with data.** 📊🔥
