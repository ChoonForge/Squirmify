# LLM Context Window Stress Testing: Reliability Under Load

**Date:** November 2025  
**Researcher:** Rich Jeffries, ChoonForge / aiMate.nz  
**Location:** Auckland, New Zealand

---

## Executive Summary

Standard LLM benchmarks fail to measure **reliability under context stress** - the ability to maintain accuracy and avoid hallucination as context windows fill. We developed a stress testing methodology that reveals catastrophic failures in popular models that score well on conventional benchmarks.

**Key Finding:** LiquidAI's LFM2-8B, despite strong benchmark performance, achieved only **0.3% accuracy** under context stress with catastrophic degradation patterns. In contrast, Qwen3-30B maintained **96.9% accuracy** with graceful degradation across 108,000 tokens.

---

## Methodology: "Squirmify" Context Stress Testing

### Test Design

Three stress test scenarios designed to measure real-world failure modes:

**1. Stealth Needle Storm**

- 40 secret codes hidden naturally in 128K tokens of mixed content (code, prose, technical writing)
- Tests: Can the model recall specific facts buried throughout a maximally-filled context?
- Measures: Checkpoint accuracy, hallucination onset, failure patterns

**2. Lost in the Middle**

- Two critical facts placed at 12.5% and 87.5% positions in 100K token context
- Tests: Can the model combine information from early and late context?
- Measures: Multi-hop reasoning under context stress

**3. Buried Instruction**

- Task instruction hidden ~30K tokens deep in 96K token technical document
- Tests: Can the model follow instructions that aren't at the prompt boundaries?
- Measures: Instruction following degradation, behavioral drift

### Content Generation

- **Mixed filler:** Code snippets (C#, JavaScript, Python, SQL)
- **Prose filler:** Natural language narratives
- **Technical filler:** System architecture, protocols, ML concepts
- **Token counting:** GPT cl100k_base encoding for consistency

### Failure Classification

Models classified by degradation pattern:

- **Graceful:** Accuracy declines slowly, admits uncertainty before hallucinating
- **Catastrophic:** Sudden failure with confident hallucination
- **Reliable token threshold:** Last checkpoint before accuracy drops below 80%

---

## Results

| Model                   | Reliable | Degradation  | Accuracy |
| ----------------------- | -------- | ------------ | -------- |
| qwen/qwen3-30b-a3b-2507 | 108,000  | graceful     | 96.9%    |
| hermes-3-llama-3.2-3b   | 54,666   | catastrophic | 90.4%    |
| baidu/ernie-4.5-21b-a3b | 16,000   | catastrophic | 50.0%    |
| qwen2.5-3b-instruct     | 0        | catastrophic | 0.0%     |
| google/gemma-3n-e4b     | 0        | catastrophic | 0.0%     |
| lfm2-8b-a1b             | 0        | catastrophic | 0.3%     |

### Key Observations

**Qwen3-30B (Winner):**

- Maintained accuracy across 108K tokens (84% of claimed 128K window)
- Graceful degradation: Admits uncertainty rather than hallucinating
- No catastrophic failure mode detected
- Suitable for production safety-critical applications

**LFM2-8B (Benchmark Darling, Production Disaster):**

- 0.3% accuracy despite strong MMLU/HumanEval scores
- Catastrophic failure: Confident hallucination from first checkpoint
- Explains field reports of victim-blaming in crisis scenarios
- **Never use in production for any safety-critical task**

**Model Size ≠ Reliability:**

- ERNIE-4.5 (21B parameters): 50% accuracy, catastrophic failure
- Hermes-3 (3B parameters): 90.4% accuracy, but unstable
- Size alone does not predict context reliability

**Smaller Models Fail Completely:**

- Both 3B models (Qwen2.5, Gemma) showed 0% reliability
- Immediate catastrophic failure on all checkpoints
- Not viable for long-context tasks regardless of speed advantages

---

## Implications for AI Safety

### Why This Matters

Standard benchmarks (MMLU, HellaSwag, HumanEval) measure:

- Short-context reasoning
- Knowledge retrieval
- Code generation

They **do not measure:**

- Behavior under context stress
- Hallucination onset patterns
- Graceful vs catastrophic degradation
- Long-context instruction following

**This gap kills people.** A model that scores 95% on benchmarks but hallucinates crisis hotlines under load is **fundamentally unsafe** for mental health applications.

### Case Study: Guardian AI Safety System

We discovered these reliability issues while building Guardian, an AI crisis detection system for New Zealand:

**Problem:** Popular models (including LFM2) provided:

- Fake crisis hotline numbers (hallucinated)
- US resources instead of NZ resources (regional confusion)
- Victim-blaming responses in domestic violence scenarios

**Root Cause:** Context stress + fine-tuning on US-biased data = catastrophic failure

**Solution:** Selected Qwen 7B (same family as Qwen3-30B) based on:

- Proven graceful degradation pattern
- No hallucination of resources under stress
- Regional resource accuracy maintained under load

**Guardian Results:** 90.9% offline accuracy, 66.7% live accuracy, **100% safe failures** (over-cautious, never under-cautious)

---

## Recommendations

### For Model Selection

1. **Always stress test** models for your specific use case, especially if:
   
   - Context windows approach model limits
   - Safety-critical information must be recalled
   - Hallucination has real-world consequences

2. **Don't trust benchmarks alone** - they measure capability, not reliability

3. **Test degradation patterns** - catastrophic failure is worse than low capability

### For AI Safety

1. **Operational safety ≠ benchmark performance**
2. **Test failure modes, not just success rates**
3. **Measure hallucination onset** as a safety metric
4. **Regional validation** is critical for global deployment

### For Researchers

1. **Publish degradation patterns** alongside accuracy scores
2. **Context stress testing** should be standard evaluation
3. **Failure classification** (graceful vs catastrophic) matters more than average performance

---

## Methodology Availability

**Squirmify test framework:** Open source (C#/.NET 9)  
**Test scenarios:** Reproducible with provided seed (42)  
**Contact:** Rich Jeffries, Auckland NZ  

---

## Conclusion

**LLM reliability under context stress is poorly understood and rarely tested.** Our methodology reveals that popular models with strong benchmark scores can fail catastrophically in production scenarios, while less-hyped models may offer superior reliability.

**For safety-critical applications:** Qwen family models demonstrate graceful degradation and high reliability under stress. LFM2 and other "benchmark leaders" should be avoided until stress testing confirms production safety.

**The industry needs better evaluation metrics.** Benchmarks that ignore context stress and degradation patterns are insufficient for production deployment decisions.

---

**Key Takeaway:** An extra 10ms of latency is negligible compared to hallucinating crisis resources. Optimize for reliability, not speed.
