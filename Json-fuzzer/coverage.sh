#!/usr/bin/env bash
# Line/branch coverage of System.Text.Json, driven by the grammar-as-data tests.
#
# Usage: ./coverage.sh [test-filter]
#   test-filter  dotnet test --filter expression (default: the grammar-as-data test classes)
#
# Output: coverage-out/report/index.html (HTML report) and coverage-out/report/Summary.txt
#
# Needs network access to api.nuget.org (packages, tools), msdl.microsoft.com (the System.Text.Json PDB)
# and, for source lines in the report, raw.githubusercontent.com.
set -euo pipefail

cd "$(dirname "$0")"
FILTER="${1:-FullyQualifiedName~Fuzzers.Tests.GrammarAsData}"
PROJECT=Fuzzers.Tests/Fuzzers.Tests.csproj
OUT=coverage-out
export DOTNET_CLI_TELEMETRY_OPTOUT=1

echo "==> Restoring tools and packages (coverage mode)"
dotnet tool restore >/dev/null
dotnet restore "$PROJECT" -p:StjCoverage=true -v q

# Where the restore put the System.Text.Json package, as MSBuild sees it.
STJ_VERSION="$(dotnet msbuild "$PROJECT" -p:StjCoverage=true -getProperty:StjPackageVersion)"
STJ_DIR="$(dotnet msbuild "$PROJECT" -p:StjCoverage=true -getProperty:StjPackageDir)"
SYMBOLS="$(dotnet msbuild "$PROJECT" -p:StjCoverage=true -getProperty:StjSymbolsDir)"
STJ_DLL="$STJ_DIR/System.Text.Json.dll"
echo "    System.Text.Json $STJ_VERSION"
[[ -f "$STJ_DLL" ]] || { echo "System.Text.Json package DLL not found at $STJ_DLL" >&2; exit 1; }

if [[ ! -f "$SYMBOLS/System.Text.Json.pdb" ]]; then
  echo "==> Downloading System.Text.Json.pdb from the Microsoft symbol server"
  dotnet dotnet-symbol --symbols --output "$SYMBOLS" "$STJ_DLL"
fi

echo "==> Running tests with coverage (filter: $FILTER)"
rm -rf "$OUT"
# A failing test should not throw away the coverage of the run; report it at the end instead.
TEST_EXIT=0
dotnet test "$PROJECT" --no-restore -p:StjCoverage=true \
  --settings stj-coverage.runsettings \
  --collect:"XPlat Code Coverage" \
  --results-directory "$OUT/raw" \
  --filter "$FILTER" || TEST_EXIT=$?

mapfile -t REPORTS < <(find "$OUT/raw" -name coverage.cobertura.xml)
[[ ${#REPORTS[@]} -gt 0 ]] || { echo "No coverage file produced" >&2; exit 1; }

# Coverlet skips a module it cannot instrument without failing the run, which gives an empty report.
if ! grep -q 'package name="System.Text.Json"' "${REPORTS[@]}"; then
  echo "ERROR: System.Text.Json was not instrumented, so the coverage report is empty." >&2
  echo "  Usual cause: $SYMBOLS/System.Text.Json.pdb does not match $STJ_DLL." >&2
  echo "  Delete $SYMBOLS and rerun. For details, run dotnet test with --diag:log.txt and search the" >&2
  echo "  datacollector log for 'Excluding module from instrumentation'." >&2
  exit 1
fi

echo "==> Building report"
dotnet reportgenerator \
  "-reports:$(IFS=';'; echo "${REPORTS[*]}")" \
  "-targetdir:$OUT/report" \
  "-reporttypes:Html;TextSummary" \
  "-title:System.Text.Json coverage ($FILTER)" \
  -verbosity:Warning

# Most of System.Text.Json is the serializer, which these tests never touch, so the assembly-wide number
# is mostly noise. This second summary is just the reading/parsing side.
dotnet reportgenerator \
  "-reports:$(IFS=';'; echo "${REPORTS[*]}")" \
  "-targetdir:$OUT/report/parser" \
  "-reporttypes:TextSummary" \
  "-classfilters:+System.Text.Json.Utf8JsonReader*;+System.Text.Json.JsonReaderHelper*;+System.Text.Json.JsonDocument*;+System.Text.Json.JsonElement*;+System.Text.Json.JsonProperty;+System.Text.Json.ThrowHelper" \
  -verbosity:Warning

echo
echo "---- Parser classes only (reader, document, nodes) ----"
sed -n '/^Summary/,/^$/p' "$OUT/report/parser/Summary.txt" | grep -E "Line coverage|Branch coverage|Method coverage"
sed -n '/^System.Text.Json /,$p' "$OUT/report/parser/Summary.txt" | sort -t' ' -k1,1 | head -n 25
echo
echo "---- Whole assembly ----"
sed -n '/^Summary/,/^$/p' "$OUT/report/Summary.txt" | grep -E "Line coverage|Branch coverage|Method coverage"
echo
echo "Full report: $PWD/$OUT/report/index.html"
if [[ $TEST_EXIT -ne 0 ]]; then
  echo "WARNING: some tests failed (exit $TEST_EXIT); coverage covers the run up to each failure." >&2
fi
exit $TEST_EXIT
