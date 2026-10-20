#!/usr/bin/env bash
# Runs the ExampleConsole executable with a range of command lines and checks the exit code and output of each.
# Usage: scripts/smoke-test.sh <path to ExampleConsole executable>
#
# Each expected output entry must match a whole output line, or, when prefixed with "~", appear anywhere in the output.
# An entry prefixed with "!" must NOT appear. The first argument after the expectations selects the scenario in
# ExampleConsole/Program.cs (lenient, strict, required) or, when absent, the default MyCommandLineArguments.

set -u

BIN="${1:?usage: smoke-test.sh <path to ExampleConsole executable>}"
if [ ! -x "$BIN" ]; then
	echo "executable not found: $BIN"
	exit 1
fi

# make the output independent of the machine it runs on
export TZ=UTC LC_ALL=C DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

total=0
failed=0

# check <description> <expected exit code> <expected output entries separated by newlines> [arguments...]
check() {
	local description="$1" expected_exit="$2" expectations="$3"
	shift 3
	total=$((total + 1))

	local output actual_exit
	output="$("$BIN" "$@" 2>&1)"
	actual_exit=$?

	local problems=""
	if [ "$actual_exit" -ne "$expected_exit" ]; then
		problems+="  exit code: expected $expected_exit, got $actual_exit"$'\n'
	fi
	local entry
	while IFS= read -r entry; do
		[ -z "$entry" ] && continue
		case "$entry" in
			'~'*)
				printf '%s\n' "$output" | grep -qF -e "${entry:1}" || problems+="  missing text: ${entry:1}"$'\n'
				;;
			'!'*)
				if printf '%s\n' "$output" | grep -qF -e "${entry:1}"; then problems+="  unexpected text: ${entry:1}"$'\n'; fi
				;;
			*)
				printf '%s\n' "$output" | grep -qxF -e "$entry" || problems+="  missing line: $entry"$'\n'
				;;
		esac
	done <<< "$expectations"

	if [ -n "$problems" ]; then
		failed=$((failed + 1))
		echo "FAIL: $description"
		echo "  command: $(basename "$BIN") $*"
		printf '%s' "$problems"
		echo "  output:"
		printf '%s\n' "$output" | head -n 40 | sed 's/^/    /'
	else
		echo "ok:   $description"
	fi
}

echo "== default scenario: MyCommandLineArguments (displays help and exits on error)"
check "no arguments shows the problem and help, exits with 2" 2 \
$'~Missing required argument\n~Example usage'
check "--Help shows the help text and exits with 0" 0 \
$'~Example usage\n~Required arguments\n~Option: -t | --TimeStamp : The timestamp to use' \
	--Help
check "-h shows the help text and exits with 0" 0 \
$'~Example usage\n!Verbose:' \
	-h
check "long option, positional and flag cluster ending in an option" 0 \
$'Repeat: 15\nOutput: target\nVerbose: True\nLevel: Level2\nLevelOption: Level0' \
	--Target target 15 -llvt 2025-05-05T12:00:00Z
check "positional before the option gives the same result" 0 \
$'Repeat: 15\nOutput: target' \
	15 --Target target
check "enum option by long name" 0 \
$'Output: t\nLevelOption: Level3' \
	--Target t --Level Level3
check "enum flag counts its repeats" 0 \
$'Level: Level3' \
	--Target t -lll
check "second positional uses the custom parse method" 0 \
$'Repeat: 4\nOutput: t' \
	--Target t 4 custom
check "invalid positional value shows the error and help, exits with 2" 2 \
$'~Invalid value for RepeatTimes: notanumber\n~Example usage' \
	--Target t notanumber
check "unknown option shows the error and help, exits with 2" 2 \
$'~Unknown argument \'--bogus\'\n~Example usage' \
	--Target t 1 --bogus
check "empty value for the custom parse method is rejected" 2 \
$'~Invalid value for MyVeryOwnType' \
	--Target t 1 ""

echo "== lenient scenario: every supported type, errors are collected and reported"
check "every option and positional given" 0 \
$'Name=bob\nCount=5\nBig=9999999999\nRatio=0.25\nPrice=12.50\nId=3f2504e0-4f89-11d3-9a0c-0305e82c3301\nSite=https://example.com/a\nWait=01:02:03\nWhen=2025-05-05T12:00:00.0000000Z\nLetter=x\nEnabled=True\nMode=Level2\nPoint=3,4\nSource=src\nCopies=7\n!Error:' \
	lenient -n bob --count 5 --big 9999999999 -r 0.25 --price 12.50 --id 3f2504e0-4f89-11d3-9a0c-0305e82c3301 --site https://example.com/a --wait 01:02:03 -t 2025-05-05T12:00:00Z --letter x --enabled true -m Level2 --point 3,4 src 7
check "only the required arguments given leaves the rest at their defaults" 0 \
$'Name=bob\nSource=src\nCount=0\nSite=about:blank\nEnabled=False\nPoint=\nCopies=0\n!Error:' \
	lenient -n bob src
check "short names" 0 \
$'Name=bob\nCount=2\nRatio=1.5\nWait=00:00:05\nMode=Level1' \
	lenient -n bob -c 2 -r 1.5 -w 00:00:05 -m Level1 src
check "negative number as an option value" 0 \
$'Count=-5\n!Error:' \
	lenient -n bob src --count -5
check "option value containing spaces" 0 \
$'Name=two words' \
	lenient -n "two words" src
check "option value that looks like a flag is taken as the value" 0 \
$'Name=-weird\nSource=src' \
	lenient -n -weird src
check "option value that looks like a long option is taken as the value" 0 \
$'Name=--count\nSource=src' \
	lenient --name --count src
check "positionals before the options" 0 \
$'Source=src\nCopies=3\nName=bob' \
	lenient src 3 -n bob
check "options before the positionals" 0 \
$'Source=src\nCopies=3\nName=bob' \
	lenient -n bob src 3
check "positionals around an option" 0 \
$'Source=src\nCopies=3\nName=bob' \
	lenient src -n bob 3
check "flag cluster" 0 \
$'Verbose=True\nQuiet=True\nDebug=Level0' \
	lenient -vq -n bob src
check "repeated flags in a cluster" 0 \
$'Verbose=True\nQuiet=True\nDebug=Level0' \
	lenient -vvq -n bob src
check "enum flag cluster counts only its own repeats" 0 \
$'Verbose=True\nQuiet=True\nDebug=Level1' \
	lenient -vqd -n bob src
check "enum flag repeated" 0 \
$'Debug=Level3' \
	lenient -n bob -ddd src
check "long flags" 0 \
$'Verbose=True\nQuiet=True\nDebug=Level1' \
	lenient --verbose --quiet --debug -n bob src
check "missing required option" 1 \
$'Source=src\nError: MissingRequiredArgumentException: Missing required argument: n | name' \
	lenient src
check "missing required positional" 1 \
$'Name=bob\nError: MissingRequiredArgumentException: Missing required argument: Source' \
	lenient -n bob
check "no arguments reports both required arguments" 1 \
$'Error: MissingRequiredArgumentException: Missing required argument: n | name\nError: MissingRequiredArgumentException: Missing required argument: Source' \
	lenient
check "invalid int" 1 \
$'Name=bob\nCount=0\nError: InvalidArgumentValueException: Invalid value for Count: abc' \
	lenient -n bob src --count abc
check "invalid positional int" 1 \
$'Copies=0\nError: InvalidArgumentValueException: Invalid value for Copies: 1x' \
	lenient -n bob src 1x
check "invalid bool, enum, guid and timestamp are all reported" 1 \
$'Error: InvalidArgumentValueException: Invalid value for Enabled: maybe\nError: InvalidArgumentValueException: Invalid value for Mode: Level9\nError: InvalidArgumentValueException: Invalid value for Id: nope\nError: InvalidArgumentValueException: Invalid value for When: yesterday' \
	lenient -n bob src --enabled maybe --mode Level9 --id nope --when yesterday
check "invalid char" 1 \
$'Error: InvalidArgumentValueException: Invalid value for Letter: xy' \
	lenient -n bob src --letter xy
check "invalid value for the custom parse method" 1 \
$'Point=\nError: InvalidArgumentValueException: Invalid value for Point: nope' \
	lenient -n bob src --point nope
check "surplus positional" 1 \
$'Error: UnexpectedArgumentException: Unexpected positional argument \'more\'.' \
	lenient -n bob src 1 more
check "unknown long option" 1 \
$'Name=bob\nError: UnexpectedArgumentException: Unknown argument \'--unknown\'.' \
	lenient -n bob src --unknown
check "unknown flag" 1 \
$'Error: UnexpectedArgumentException: Unknown flag \'-x\'.' \
	lenient -n bob src -x
check "option without a value" 1 \
$'Error: MissingRequiredArgumentException: Option \'--count\' requires a value.' \
	lenient -n bob src --count
check "valid arguments are still applied when another one is invalid" 1 \
$'Name=bob\nVerbose=True\nCount=0\nError: InvalidArgumentValueException: Invalid value for Count: abc' \
	lenient -v -n bob --count abc src
check "help is available in a collecting scenario too" 0 \
$'~Example usage\n~Position: 0: The source\n~Option: -n | --name : Name to use' \
	lenient -h

echo "== strict scenario: throws on any error"
check "valid arguments" 0 \
$'Number=5\nLabel=label' \
	strict -n 5 label
check "nothing required, nothing given" 0 \
$'Number=0\nLabel=' \
	strict
check "invalid value throws" 3 \
$'Threw: AggregateException: InvalidArgumentValueException' \
	strict -n abc
check "unknown option throws" 3 \
$'Threw: AggregateException: UnexpectedArgumentException' \
	strict --bogus
check "no help argument is registered when only the text is generated" 3 \
$'Threw: AggregateException: UnexpectedArgumentException' \
	strict --Help

echo "== required scenario: throws only when something required is missing"
check "valid arguments" 0 \
$'Key=abc\nNumber=4\nErrors=0' \
	required -k abc -n 4
check "other errors are collected instead of thrown" 0 \
$'Key=abc\nNumber=0\nErrors=1' \
	required -k abc -n zzz
check "unknown option is collected instead of thrown" 0 \
$'Key=abc\nErrors=1' \
	required -k abc --bogus
check "missing required option throws" 3 \
$'Threw: AggregateException: MissingRequiredArgumentException' \
	required -n 4
check "no arguments throws" 3 \
$'Threw: AggregateException: MissingRequiredArgumentException' \
	required

echo
echo "$((total - failed)) of $total smoke checks passed"
[ "$failed" -eq 0 ]
