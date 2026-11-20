#!/usr/bin/env zsh

set -euo pipefail

# Report: Find throttling via Task.Delay in auth domain (Identity project and Api/Auth)
# Output: Markdown suitable for Confluence paste

# Determine workspace root (repo root) based on this script location
SCRIPT_DIR=$(cd -- "$(dirname -- "$0")" && pwd)
REPO_ROOT=$(cd -- "${SCRIPT_DIR}/.." && pwd)

# Optional: set REPORT_FILE env var to write directly to a file; otherwise prints to stdout
REPORT_FILE=${REPORT_FILE:-}
# Output format: 'markdown' (default) or 'text' (Confluence-friendly plain text)
# Respect externally provided FORMAT if set; otherwise set later (defaults to markdown)
FORMAT=${FORMAT-}

# Track if user explicitly set format via args
FORMAT_SET=0

# Basic arg parsing for format
if [[ $# -gt 0 ]]; then
  case "$1" in
    --format=*) FORMAT="${1#--format=}"; shift; FORMAT_SET=1 ;;
    --format) FORMAT="$2"; shift 2; FORMAT_SET=1 ;;
    markdown|md) FORMAT="markdown"; shift; FORMAT_SET=1 ;;
    text|txt|confluence) FORMAT="text"; shift; FORMAT_SET=1 ;;
  esac
fi

# Interactive chooser for format and destination when running in a TTY and not explicitly set
interactive_choose_options() {
  local choice out path ext default_path

  echo "Choose output format:" > /dev/tty
  echo "  1) Markdown (GitHub)" > /dev/tty
  echo "  2) Text (Confluence-friendly)" > /dev/tty
  printf "Enter choice [1]: " > /dev/tty
  read -r choice < /dev/tty || choice=""
  case "$choice" in
    2) FORMAT="text"; ext="txt" ;;
    *) FORMAT="markdown"; ext="md" ;;
  esac

  echo > /dev/tty
  echo "Output destination:" > /dev/tty
  echo "  1) Print to stdout" > /dev/tty
  echo "  2) Write to file" > /dev/tty
  printf "Enter choice [1]: " > /dev/tty
  read -r out < /dev/tty || out=""
  if [[ "$out" == "2" ]]; then
    default_path="${REPO_ROOT}/util/auth_throttling_report.${ext}"
    printf "Enter output path [%s]: " "$default_path" > /dev/tty
    read -r path < /dev/tty || path=""
    REPORT_FILE=${path:-$default_path}
  fi
}

# Targets (absolute paths)
TARGET_DIRS=(
  "${REPO_ROOT}/src/Identity"
  "${REPO_ROOT}/src/Api/Auth"
)

# Prefer ripgrep if available; otherwise fall back to grep
if command -v rg >/dev/null 2>&1; then
  SEARCH_TOOL="rg"
else
  SEARCH_TOOL="grep"
fi

# Utility: trim whitespace
trim() { print -r -- "$1" | sed -E 's/^\s+//; s/\s+$//' }

# Utility: safe sed print of a specific line
sed_line() {
  local file="$1"; shift
  local line="$1"; shift
  sed -n "${line}p" "$file" 2>/dev/null || true
}

# Find the last occurrence at or before a target line matching a regex; prints "line:text" or empty
last_line_before() {
  local file="$1"; shift
  local line="$1"; shift
  local regex="$1"; shift
  awk -v ln="$line" -v re="$regex" '
  NR<=ln { lines[NR] = $0; last = NR }
  END {
    for (i = last; i >= 1; i--) {
      if (lines[i] ~ re) { printf("%d:%s\n", i, lines[i]); exit }
    }
  }' -- "$file" 2>/dev/null || true
}

# Extractors from attribute/method/class lines
extract_http_method() {
  local line="$1"
  local attr
  attr=$(print -r -- "$line" | sed -nE 's/.*\[(Http[A-Za-z]+).*$/\1/p')
  case "$attr" in
    HttpGet) echo GET;;
    HttpPost) echo POST;;
    HttpPut) echo PUT;;
    HttpDelete) echo DELETE;;
    HttpPatch) echo PATCH;;
    *) echo "";;
  esac
}

extract_attr_route() {
  local line="$1"
  # First quoted string inside attribute, if present
  print -r -- "$line" | sed -nE 's/.*\[[A-Za-z]+\s*\(\s*"([^"]+)".*$/\1/p'
}

extract_method_name() {
  local line="$1"
  # Heuristic: last identifier before the opening parenthesis (BSD sed friendly)
  print -r -- "$line" | sed -nE 's/.*[^A-Za-z0-9_]([A-Za-z_][A-Za-z0-9_]*)[[:space:]]*\(.*/\1/p'
}

extract_class_name() {
  local line="$1"
  print -r -- "$line" | sed -nE 's/.*class[[:space:]]+([A-Za-z_][A-Za-z0-9_]*).*/\1/p'
}

extract_controller_name() {
  local class_name="$1"
  # Strip trailing Controller suffix if present
  print -r -- "$class_name" | sed -E 's/Controller$//'
}

combine_paths() {
  local class_route="$1"
  local method_route="$2"
  local combined

  if [[ -z "$class_route" && -z "$method_route" ]]; then
    echo ""
    return
  fi
  if [[ -z "$class_route" ]]; then
    combined="$method_route"
  elif [[ -z "$method_route" ]]; then
    combined="$class_route"
  else
    # Trim slashes and join
    local a b
    a=$(print -r -- "$class_route" | sed -E 's#^/+##; s#/+$##')
    b=$(print -r -- "$method_route" | sed -E 's#^/+##; s#/+$##')
    combined="$a/$b"
  fi
  # Ensure leading '/'
  if [[ "$combined" != /* && "$combined" != "~/*" ]]; then
    combined="/$combined"
  fi
  print -r -- "$combined"
}

print_header() {
  local now
  now=$(date '+%Y-%m-%d %H:%M:%S %Z')
  local rel_scope1 rel_scope2
  rel_scope1=${TARGET_DIRS[1]#${REPO_ROOT}/}
  rel_scope2=${TARGET_DIRS[2]#${REPO_ROOT}/}
  if [[ "$FORMAT" == "markdown" ]]; then
    {
      echo "## Auth Throttling (Task.Delay) Report"
      echo
      echo "- **Generated**: $now"
      echo "- **Scope**: \`${rel_scope1}\`, \`${rel_scope2}\`"
      echo
      echo "### Findings"
      echo
      echo "| HTTP | Path | File | Line | Controller | Method | Snippet |"
      echo "|---|---|---|---:|---|---|---|"
    } >>"$REPORT_OUT"
  else
    {
      echo "Auth Throttling (Task.Delay) Report"
      echo "Generated: $now"
      echo "Scope: ${rel_scope1}, ${rel_scope2}"
      echo
      echo "Findings:"
      echo
    } >>"$REPORT_OUT"
  fi
}

# Use associative array to dedupe by file+method signature line
typeset -A DEDUPE

process_match() {
  local file="$1"; shift
  local line="$1"; shift

  # Scan the file up to the matched line to find nearest relevant markers
  local httpPair routePair methodPair classPair httpL httpTxt routeL routeTxt methodL methodTxt classL classTxt
  httpPair=$(last_line_before "$file" "$line" '^[[:space:]]*\[Http(Get|Post|Put|Delete|Patch)')
  # Find the method signature line: visibility + optionally async, return type, then method name and (
  methodPair=$(last_line_before "$file" "$line" '^[[:space:]]*(public|protected|private|internal)[[:space:]]+(async[[:space:]]+)?[A-Za-z0-9_<>\[\]\.\?]+[[:space:]]+[A-Za-z_][A-Za-z0-9_]*[[:space:]]*\(')
  classPair=$(last_line_before "$file" "$line" '^[[:space:]]*(public|protected|private|internal)[[:space:]]*class[[:space:]]+[A-Za-z_][A-Za-z0-9_]*')
  httpL=$(print -r -- "$httpPair" | cut -d: -f1)
  httpTxt=$(print -r -- "$httpPair" | cut -d: -f2-)
  methodL=$(print -r -- "$methodPair" | cut -d: -f1)
  methodTxt=$(print -r -- "$methodPair" | cut -d: -f2-)
  classL=$(print -r -- "$classPair" | cut -d: -f1)
  classTxt=$(print -r -- "$classPair" | cut -d: -f2-)

  # Fallbacks using sed+nl+grep if any are empty
  if [[ -z "$methodTxt" ]]; then
    local mPair
    mPair=$(sed -n "1,${line}p" "$file" | nl -ba -w1 -s: | grep -E '^[0-9]+:[[:space:]]*(public|protected|private|internal)[[:space:]].*\(' | tail -n 1 || true)
    if [[ -n "$mPair" ]]; then
      methodL=$(print -r -- "$mPair" | cut -d: -f1)
      methodTxt=$(print -r -- "$mPair" | cut -d: -f2-)
    fi
  fi
  if [[ -z "$classTxt" ]]; then
    local cPair
    cPair=$(sed -n "1,${line}p" "$file" | nl -ba -w1 -s: | grep -E '^[0-9]+:[[:space:]]*(public|protected|private|internal)[[:space:]]*class[[:space:]]+[A-Za-z_][A-Za-z0-9_]*' | tail -n 1 || true)
    if [[ -n "$cPair" ]]; then
      classL=$(print -r -- "$cPair" | cut -d: -f1)
      classTxt=$(print -r -- "$cPair" | cut -d: -f2-)
    fi
  fi
  if [[ -z "$httpTxt" ]]; then
    local hPairLimit
    local limit
    limit=${methodL:-$line}
    hPairLimit=$(sed -n "1,${limit}p" "$file" | nl -ba -w1 -s: | grep -E '^[0-9]+:[[:space:]]*\[Http(Get|Post|Put|Delete|Patch)' | tail -n 1 || true)
    if [[ -n "$hPairLimit" ]]; then
      httpL=$(print -r -- "$hPairLimit" | cut -d: -f1)
      httpTxt=$(print -r -- "$hPairLimit" | cut -d: -f2-)
    fi
  fi
  # For class-level route, search before the class declaration if found; otherwise before the delay line
  if [[ -n "$classL" ]]; then
    routePair=$(last_line_before "$file" "$classL" '^[[:space:]]*\[Route[[:space:]]*\(')
  else
    routePair=$(last_line_before "$file" "$line" '^[[:space:]]*\[Route[[:space:]]*\(')
  fi
  routeL=$(print -r -- "$routePair" | cut -d: -f1)
  routeTxt=$(print -r -- "$routePair" | cut -d: -f2-)

  local httpVerb methodRoute classRoute methodName className controllerName endpointPath snippet
  httpVerb=$(extract_http_method "$httpTxt")
  methodRoute=$(extract_attr_route "$httpTxt")
  classRoute=$(extract_attr_route "$routeTxt")
  methodName=$(extract_method_name "$methodTxt")
  className=$(extract_class_name "$classTxt")
  controllerName=$(extract_controller_name "$className")
  endpointPath=$(combine_paths "$classRoute" "$methodRoute")
  snippet=$(sed_line "$file" "$line" | sed -E 's/^[[:space:]]+//; s/[[:space:]]+$//')

  local key
  if [[ -n "$methodL" ]]; then
    key="$file:$methodL"
  else
    key="$file:$line"
  fi

  # Only print once per method
  if [[ -z "${DEDUPE["$key"]:-}" ]]; then
    DEDUPE["$key"]=1
    # Escape pipe characters in snippet
    local escSnippet
    escSnippet=$(print -r -- "$snippet" | sed 's/|/\\|/g')
    # Emit table row
    # Show file path relative to repo root (hide personal absolute paths)
    local relFile
    relFile=${file#$REPO_ROOT/}
    if [[ "$FORMAT" == "markdown" ]]; then
      print -r -- "| ${httpVerb:-N/A} | ${endpointPath:-N/A} | \`${relFile}\` | ${line} | ${controllerName:-N/A} | ${methodName:-N/A} | \`${escSnippet}\` |" >>"$REPORT_OUT"
    else
      {
        echo "- ${httpVerb:-N/A} ${endpointPath:-N/A}"
        echo "  - File: ${relFile}:${line}"
        echo "  - Controller: ${controllerName:-N/A}"
        echo "  - Method: ${methodName:-N/A}"
        echo "  - Snippet: ${escSnippet}"
        echo
      } >>"$REPORT_OUT"
    fi
  fi
}

main() {
  local matches tmpfile
  tmpfile=$(mktemp)
  # If no FORMAT was provided and we are attached to a TTY, ask the user for options
  if [[ -z "${FORMAT-}" && -t 1 ]]; then
    interactive_choose_options
  fi

  # Default FORMAT if still empty
  if [[ -z "${FORMAT-}" ]]; then
    FORMAT="markdown"
  fi

  if [[ -n "$REPORT_FILE" ]]; then
    REPORT_OUT="$REPORT_FILE"
    : >"$REPORT_OUT"
    trap '[[ -n ${tmpfile-} ]] && rm -f "$tmpfile"' EXIT
  else
    REPORT_OUT=$(mktemp)
    trap '[[ -n ${tmpfile-} ]] && rm -f "$tmpfile"; [[ -n ${REPORT_OUT-} ]] && rm -f "$REPORT_OUT"' EXIT
  fi

  print_header

  if [[ "$SEARCH_TOOL" == "rg" ]]; then
    # ripgrep: filename:line:match
    for dir in "${TARGET_DIRS[@]}"; do
      if [[ -d "$dir" ]]; then
        rg --no-heading --line-number --glob '*.cs' -F 'Task.Delay(' "$dir" >>"$tmpfile" || true
      fi
    done
  else
    # grep fallback
    for dir in "${TARGET_DIRS[@]}"; do
      if [[ -d "$dir" ]]; then
        # Portable: find .cs files then grep for Task.Delay(
        find "$dir" -type f -name '*.cs' -print0 | xargs -0 grep -nH -F 'Task.Delay(' >>"$tmpfile" || true
      fi
    done
  fi

  # Process each match
  while IFS= read -r line; do
    # Expect format: /abs/path/file.cs:123:....
    local file lnum
    file=${line%%:*}
    lnum=${line#*:}
    lnum=${lnum%%:*}
    # zsh-friendly numeric check
    if [[ -f "$file" && "$lnum" == <-> ]]; then
      process_match "$file" "$lnum"
    fi
  done <"$tmpfile"

  # If writing to file, ensure only markdown lines are written (defensive: strip any debug lines)
  if [[ -n "$REPORT_FILE" ]]; then
    # Overwrite file in place with filtered content
    local cleaned
    cleaned=$(mktemp)
    grep -Ev '^(file=|lnum=)$' "$REPORT_OUT" >"$cleaned" || true
    mv "$cleaned" "$REPORT_OUT"
  else
    cat "$REPORT_OUT"
  fi
}

main "$@"


