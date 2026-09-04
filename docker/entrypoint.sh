#!/usr/bin/env bash
#
# Command surface of the reproduction image. Every subcommand is one of the
# procedures documented in the thesis; the exact underlying command line is
# echoed before it runs, so what the container did stays checkable against the
# text rather than hidden behind this script.
set -euo pipefail

readonly REPO=/src
OUT=${OUT_DIR:-/out}
mkdir -p "$OUT"

# --no-build: the image already built Release at image build time.
eval_harness() { dotnet run --project "$REPO/DesignPatternDetection.Evaluation" -c Release --no-build -- "$@"; }
detector()     { dotnet run --project "$REPO/DesignPatternDetection"            -c Release --no-build -- "$@"; }

# Bold only on a terminal
if [ -t 1 ]; then bold=$'\033[1m'; plain=$'\033[0m'; else bold=; plain=; fi
say() { printf '\n%s==> %s%s\n' "$bold" "$*" "$plain"; }
echo_cmd() { printf '    $ %s\n\n' "$*"; }

# The reviewer named in the thesis. Override with VERIFY_MODEL to review with a different provider
VERIFY_MODEL=${VERIFY_MODEL:-gemini-3.7-flash}

# A fresh cache per run by default
default_cache() { printf '%s/razsodbe-%s.json' "$OUT" "$(date -u +%Y%m%dT%H%M%SZ)"; }

require_key() {
    local model=$1 cache=$2 names var
    case $model in
        gemini-*) names="GEMINI_API_KEY GOOGLE_API_KEY" ;;
        *)        names="ANTHROPIC_API_KEY" ;;
    esac

    for var in $names; do
        [ -n "${!var:-}" ] && return 0
    done
    var=${names%% *}

    # A populated cache answers from disk
    if [ -s "$cache" ]; then
        printf 'warning: $%s is not set; answering from the cache at %s.\n' "$var" "$cache" >&2
        printf '         Candidates it does not hold stay unreviewed and are kept.\n\n' >&2
        return 0
    fi

    cat >&2 <<MSG
error: reviewing with '$model' needs \$$var, which is not set in the container.

  docker compose run --rm -e $var=... semantic

or put it in a .env file next to docker-compose.yml:

  $var=...

To replay a finished run rather than pay for a new one, point VERIFY_CACHE at
that run's verdict cache; a full cache needs no key.
MSG
    exit 2
}

usage() {
    cat <<'MSG'
Reproduction image for the design-pattern-detection thesis results.

  structural            unverified run over every corpus in the manifest
                        -> $OUT/nepreverjeno.json   (tables 7.1 and 7.2)
  semantic              the same run with LLM verification enabled
                        -> $OUT/preverjeno.json     (table 7.3); needs an API key,
                        or a verdict cache to replay
  all                   structural, then semantic
  analyze [report]      what review changed - precision, recall and F1 for both
                        stages, with a 95% confidence interval on each change
                        (default report: $OUT/preverjeno.json)
  examples              the textbook demo: scan the bundled DesignPatternExamples
                        and write every output format
                        -> $OUT/examples.{json,sarif,ttl} + graph.ttl
                        (to score them instead: eval examples)
  tests                 dotnet test over the solution
  eval    [args...]     the evaluation harness, raw
  scan    [args...]     the detector CLI, raw
  shell                 an interactive shell in /src

Extra arguments to structural/semantic/all are appended to the harness command
line (e.g. --baseline prev.json, --verify-parallelism 16).

Environment: VERIFY_MODEL (default gemini-3.7-flash), VERIFY_CACHE,
GEMINI_API_KEY (or GOOGLE_API_KEY) / ANTHROPIC_API_KEY, OUT_DIR.
MSG
}

structural() {
    say "Structural run - all corpora, no verification"
    echo_cmd "dotnet run --project DesignPatternDetection.Evaluation -- --corpora --report $OUT/nepreverjeno.json${*:+ $*}"
    eval_harness --corpora --report "$OUT/nepreverjeno.json" "$@"
}

semantic() {
    local cache=${VERIFY_CACHE:-$(default_cache)}
    require_key "$VERIFY_MODEL" "$cache"
    say "Semantic run - all corpora, reviewed by $VERIFY_MODEL"
    echo_cmd "dotnet run --project DesignPatternDetection.Evaluation -- --corpora --verify --verify-model $VERIFY_MODEL --verify-cache $cache --report $OUT/preverjeno.json${*:+ $*}"
    eval_harness --corpora --verify --verify-model "$VERIFY_MODEL" \
        --verify-cache "$cache" --report "$OUT/preverjeno.json" "$@"
}

# Reads a finished reviewed report
analyze() {
    local report=${1:-$OUT/preverjeno.json}
    say "Effect of review, with confidence intervals"
    echo_cmd "dotnet run --project DesignPatternDetection.Evaluation -- --analyze $report"
    eval_harness --analyze "$report"
}

examples() {
    say "Detector over the bundled examples - one file per output format"
    detector --report "$OUT/examples.json" --sarif "$OUT/examples.sarif" \
        --findings "$OUT/examples-findings.ttl" --turtle "$OUT/examples-graph.ttl" "$@"
}

case ${1-structural} in
    structural|unverified|nepreverjeno) shift || true; structural "$@" ;;
    semantic|verified|preverjeno)       shift || true; semantic "$@" ;;
    all)                                shift || true; structural "$@"; semantic "$@" ;;
    analyze|analyse|compare)            shift || true; analyze "$@" ;;
    examples)                           shift || true; examples "$@" ;;
    tests|test)                         shift || true; say "Test suite"; dotnet test "$REPO/DesignPatternDetection.slnx" -c Release --no-build "$@" ;;
    eval)                               shift; eval_harness "$@" ;;
    scan)                               shift; detector "$@" ;;
    shell|bash)                         shift || true; exec bash "$@" ;;
    help|--help|-h)                     usage ;;
    *)                                  usage >&2; printf '\nerror: unknown command %s\n' "$1" >&2; exit 2 ;;
esac
