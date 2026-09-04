# Obtaining the results in a container

This document describes how to run the evaluation in a container.

The container carries the .NET 10 SDK, `git`, and this repository already built.
`docker compose up` prints the command list and exits — every other service sits
in the `cli` profile, so nothing runs until you name it. `docker compose run`
enables that profile itself, so the commands below need no extra flag.

## Structural run — no key needed

```powershell
docker compose run --rm structural
```

Builds the image on first use, clones
each corpus in turn, runs all 23 detectors over every labeled unit, prints the
per-corpus and per-pattern scoreboard, and writes `out/nepreverjeno.json`.

## Semantic run — needs a reviewer key

```powershell
docker compose run --rm -e GEMINI_API_KEY=... semantic
```

or put `GEMINI_API_KEY=...` in a `.env` file beside `docker-compose.yml`
(see `.env.example`; `.env` and `out/` are git-ignored) and drop the `-e`.

Same corpora, same detectors, every candidate match adjudicated by
`gemini-3.7-flash` before it is scored. Writes `out/preverjeno.json` plus a
fresh verdict cache `out/razsodbe-<timestamp>.json`.

To make repeat runs free, save the verification cache with `-e VERIFY_CACHE=/out/razsodbe.json`.

A **full** cache needs no API key at all: point `VERIFY_CACHE` at a finished
run's cache and every candidate is answered from disk, which replays that run's
verdicts exactly and for free.

A different reviewer is one variable: `-e VERIFY_MODEL=claude-opus-5` (with `ANTHROPIC_API_KEY`)

Extra arguments are appended to the harness command line:

```powershell
docker compose run --rm semantic --verify-parallelism 16
docker compose run --rm structural --baseline /out/nepreverjeno.json
```

## What review changed, with confidence intervals

```powershell
docker compose run --rm analyze                    # reads out/preverjeno.json
docker compose run --rm analyze /out/other.json    # or any reviewed report
```

Prints, per corpus and pooled, the precision, recall and F1 of the detectors
alone beside the same three after review, then bounds each change with a 95%
interval (p = 0.05) from a leave-one-unit-out jackknife.

It reads a finished reviewed report and calls no model, so it is free and
repeatable.

You can also run both runs in one go:

```powershell
docker compose run --rm all
```

## Everything else the image can do

```powershell
docker compose run --rm tests        # the xUnit suite
docker compose run --rm examples     # textbook demo: detector over DesignPatternExamples,
                                     # writing JSON + SARIF + findings + graph Turtle to out/
docker compose run --rm eval examples --report /out/examples-scores.json   # ...or score them (F1 1.000)
docker compose run --rm scan https://github.com/owner/repo --report /out/scan.json
docker compose run --rm eval nlog-v6.1.4 --report /out/nlog.json
docker compose run --rm shell        # a prompt in /src
docker compose run --rm help         # every subcommand and variable
```

Write file outputs under `/out` so they land in `./out` on the host.
