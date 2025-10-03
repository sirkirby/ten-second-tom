# Code Coverage Strategy

## Overview

This document explains our code coverage approach for CI/CD, focusing on preventing regression rather than enforcing arbitrary thresholds.

## Coverage Metrics Explained

The test suite generates several coverage metrics:

### 1. **Line Coverage** (Primary Metric)
- **What it measures**: Percentage of executable code lines that are hit by tests
- **Why it's primary**: Most widely understood, industry standard, easy to reason about
- **Current baseline**: ~54%

### 2. **Branch Coverage** (Tracked, Not Enforced)
- **What it measures**: Percentage of decision paths (if/else, switch cases) tested
- **Why tracked**: Shows how thoroughly conditional logic is tested
- **Current baseline**: ~42%

### 3. **Method Coverage** (Tracked, Not Enforced)
- **What it measures**: Percentage of methods with at least one line executed
- **Current baseline**: ~83%

### 4. **Full Method Coverage** (Tracked, Not Enforced)
- **What it measures**: Percentage of methods where every line is covered
- **Current baseline**: ~63%

## PR Validation Strategy

### ✅ What We Enforce

**Regression Prevention**: PRs cannot decrease line coverage by more than 0.5 percentage points.

**Why this approach?**
- Prevents "coverage decay" over time
- Allows legitimate PRs even when overall coverage is below aspirational targets
- Encourages gradual improvement without blocking work
- Avoids "gaming" the system with meaningless tests just to hit arbitrary thresholds

### ❌ What We Don't Enforce

**Absolute thresholds** (e.g., "must be 80%"): These are problematic because:
- Current codebase may be below the threshold
- Forces coverage improvement even for unrelated changes
- Can lead to low-quality tests written just to increase numbers
- Blocks legitimate PRs unnecessarily

## How It Works

### On Every PR:

1. **Run tests with coverage**: Generate line/branch/method metrics
2. **Compare to baseline**: Retrieve cached coverage from target branch (usually `main`)
3. **Check for regression**: Fail if line coverage decreased by >0.5 percentage points
4. **Post comment**: If coverage changed by ≥1 percentage point, post PR comment with details
5. **Upload artifacts**: Full HTML report with all metrics available for 30 days

### Coverage Diff Comments

PRs automatically get a comment when line coverage changes significantly:

```markdown
## 📈 Code Coverage Report

✅ **Current Line Coverage:** 54.2%
📊 **Baseline Coverage:** 53.8%
📈 **Change:** +0.4 percentage points

**Regression Check:** ✅ Passed

*Line coverage is the primary metric. See artifacts for branch and method coverage details.*
```

## Future Improvements

As test coverage improves, we can:

1. **Tighten regression tolerance**: Reduce from 0.5 to 0.1 percentage points
2. **Add diff coverage**: Measure coverage of only changed/new code (requires additional tooling)
3. **Track branch coverage**: Once line coverage is consistently high (80%+), enforce branch coverage
4. **Add coverage goals**: Set aspirational targets (80% line, 70% branch) without blocking PRs

## Philosophy

> **Better to prevent coverage from getting worse than to force it to be perfect from day one.**

- Test quality > test quantity
- Gradual improvement > blocking PRs
- Meaningful tests > gaming metrics
- Transparency > hidden decisions

## Viewing Coverage Reports

### Locally
```bash
dotnet test --collect:"XPlat Code Coverage"
dotnet tool install --global dotnet-reportgenerator-globaltool
reportgenerator \
  -reports:"**/coverage.cobertura.xml" \
  -targetdir:"./coverage-report" \
  -reporttypes:"Html"
open ./coverage-report/index.html
```

### In CI
Download the `coverage-report` artifact from the GitHub Actions workflow run. Open `index.html` to see:
- Line-by-line coverage visualization
- Branch coverage details
- Method coverage breakdown
- Per-class and per-file metrics

## Questions?

**Q: Why not enforce 80% coverage immediately?**  
A: Current codebase is at 54%. Enforcing 80% would block all PRs until extensive backfill testing is done. Regression prevention allows gradual improvement.

**Q: Why 0.5 percentage points tolerance?**  
A: Allows for minor fluctuations from refactoring or removing dead code. Prevents accidental regression while avoiding false positives.

**Q: What about branch coverage?**  
A: It's tracked and available in reports. Once line coverage is consistently high, we can add branch coverage enforcement.

**Q: Should I write tests just to increase coverage?**  
A: No. Write meaningful tests for important behavior. Coverage will increase naturally as features are tested properly.

---

**Last Updated**: 2025-10-03  
**See Also**: [CICD.md](./CICD.md) for complete CI/CD documentation
