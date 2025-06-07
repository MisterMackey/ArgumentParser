.DEFAULT_GOAL := help
# stamp files definition
STAMP_DIR := .build-stamps
ANALYZER_STAMP := $(STAMP_DIR)/analyzer_built
EXAMPLE_STAMP := $(STAMP_DIR)/example_built
TESTPROJ_STAMP := $(STAMP_DIR)/testproj_built

# sources
ANALYZER_SOURCES := $(wildcard ArgumentParser/**/*.cs) $(wildcard ArgumentParser.Analyzer/**/*.cs)
EXAMPLE_SOURCES := $(wildcard ExampleConsole/**/*.cs)
TEST_SOURCES := $(wildcard ArgumentParser.Tests/**/*.cs)

# stamps
$(STAMP_DIR):
	mkdir -p $(STAMP_DIR)

$(ANALYZER_STAMP): $(ANALYZER_SOURCES) | $(STAMP_DIR)
	dotnet build ArgumentParser -c Release
	touch $@

$(EXAMPLE_STAMP): $(EXAMPLE_SOURCES) restore-example | $(STAMP_DIR)
	dotnet build ExampleConsole -v:d
	touch $@

$(TESTPROJ_STAMP): $(TEST_SOURCES) build-analyzer | $(STAMP_DIR)
	dotnet build ArgumentParser.Tests -c Debug # never build in Release, we want stacktraces!

# targets
.PHONY: help

help:
	@echo "Makefile targets:"
	@echo "  all                - Build everything and run tests"
	@echo "  clean              - Clean the build artifacts"
	@echo "  build-analyzer     - Build the ArgumentParser analyzer"
	@echo "  restore-example    - Add locally built Aot.ArgumentParser package to ExampleConsole"
	@echo "  build-example      - Build the ExampleConsole project"
	@echo "  build-tests        - Build the ArgumentParser.Tests project"
	@echo "  test               - Run the tests in ArgumentParser.Tests"
	@echo "  help               - Show this help message"

all: build-analyzer build-example build-tests test

clean:
	rm -rf $(STAMP_DIR)
	dotnet clean

build-analyzer: $(ANALYZER_STAMP)

restore-example: build-analyzer
	dotnet remove ExampleConsole package Aot.ArgumentParser || true
	dotnet nuget locals all --clear
	dotnet restore
	dotnet add ExampleConsole package Aot.ArgumentParser --source ArgumentParser/bin/nupkg

build-example: $(EXAMPLE_STAMP)

build-tests: $(TESTPROJ_STAMP)

test: build-tests
	dotnet test ArgumentParser.Tests --no-build