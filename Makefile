.DEFAULT_GOAL := help
# stamp files definition
STAMP_DIR := .build-stamps
ANALYZER_STAMP := $(STAMP_DIR)/analyzer_built
RESTORE_EXAMPLE_STAMP := $(STAMP_DIR)/example_restored
EXAMPLE_STAMP := $(STAMP_DIR)/example_built
TESTPROJ_STAMP := $(STAMP_DIR)/testproj_built

# sources
ANALYZER_SOURCES := $(wildcard ArgumentParser/*.cs) $(wildcard ArgumentParser/**/*.cs) $(wildcard ArgumentParser.Analyzer/*.cs) $(wildcard ArgumentParser.Analyzer/**/*.cs)
EXAMPLE_SOURCES := $(wildcard ExampleConsole/*.cs) $(wildcard ExampleConsole/**/*.cs)
TEST_SOURCES := $(wildcard ArgumentParser.Tests/*.cs) $(wildcard ArgumentParser.Tests/**/*.cs)

# stamps
$(STAMP_DIR):
	mkdir -p $(STAMP_DIR)

$(ANALYZER_STAMP): $(ANALYZER_SOURCES) | $(STAMP_DIR)
	dotnet build ArgumentParser -c Release
	touch $@

$(RESTORE_EXAMPLE_STAMP): $(ANALYZER_STAMP) | $(STAMP_DIR)
	dotnet remove ExampleConsole package Aot.ArgumentParser || true
	dotnet nuget locals all --clear
	dotnet restore
	dotnet add ExampleConsole package Aot.ArgumentParser --source ArgumentParser/bin/nupkg
	touch $@

$(EXAMPLE_STAMP): $(EXAMPLE_SOURCES) $(RESTORE_EXAMPLE_STAMP) | $(STAMP_DIR)
	dotnet build ExampleConsole -v:d
	touch $@

$(TESTPROJ_STAMP): $(TEST_SOURCES) $(ANALYZER_STAMP) | $(STAMP_DIR)
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
	@echo "  coverage           - Generate a coverage report for ArgumentParser.Tests"
	@echo "  set-version        - Set the version in the analyzer and example project based on git version"
	@echo "  release            - Performs some checks and cuts a release"
	@echo "  help               - Show this help message"

all: build-analyzer build-example build-tests test

clean:
	rm -rf $(STAMP_DIR)
	dotnet clean
	rm -rf coveragereports
	rm -f ArgumentParser.Tests/coverage.cobertura.xml

build-analyzer: $(ANALYZER_STAMP)

restore-example: $(RESTORE_EXAMPLE_STAMP)

build-example: $(EXAMPLE_STAMP)

build-tests: $(TESTPROJ_STAMP)

test: build-tests
	dotnet test ArgumentParser.Tests --no-build /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura

coverage: build-tests
	reportgenerator -reports:ArgumentParser.Tests/coverage.cobertura.xml -targetdir:coveragereports -reporttypes:Html
	@echo "Coverage report generated in coveragereports/index.html"

set-version:
	VERSION=$$(dotnet-gitversion | jq -r '.FullSemVer'); \
	if [ -z "$$VERSION" ]; then \
		echo "Versioning check failed: Unable to determine version"; \
		exit 1; \
	fi; \
	echo "Version to be used: $$VERSION"; \
	sed -i 's/public const string FullSemVer = "[^"]*";/public const string FullSemVer = "'$$VERSION'";/' ArgumentParser.Analyzer/CodeProviders/AssemblyVersionProvider.cs; \
	sed -i 's|<Version>[^<]*</Version>|<Version>'"$$VERSION"'</Version>|' ArgumentParser/ArgumentParser.csproj

release: clean set-version
	@echo "Checking git working tree status..."
	@if [ -n "$$(git status --porcelain)" ]; then \
		echo "Error: Working tree is not clean. Please commit or stash changes before releasing."; \
		exit 1; \
	fi
	@echo "Running tests..."
	@if ! make test; then \
		echo "Error: Tests failed. Aborting release."; \
		exit 1; \
	fi
	@echo "Building example project..."
	@if ! make build-example; then \
		echo "Error: Example project build failed. Aborting release."; \
		exit 1; \
	fi
	@echo "Running example console to verify it works..."
	@if ! ExampleConsole/bin/Debug/net9.0/ExampleConsole --Help; then \
		echo "Error: Example console execution failed. Aborting release."; \
		exit 1; \
	fi
	@echo "Creating git tag..."
	VERSION=$$(dotnet-gitversion | jq -r '.FullSemVer'); \
	echo "Enter tag message for v$$VERSION (press Enter to start editor):"; \
	git tag -a "v$$VERSION" -m "$$(read message; echo $$message)"
	@echo "Release v$$VERSION completed successfully."
	@echo "Remember to push the tag with: git push origin v$$VERSION"