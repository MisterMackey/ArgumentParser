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
	dotnet restore
	dotnet add ExampleConsole package Aot.ArgumentParser --source ArgumentParser/bin/nupkg --version $$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' ArgumentParser/ArgumentParser.csproj)
	touch $@

$(EXAMPLE_STAMP): $(EXAMPLE_SOURCES) $(RESTORE_EXAMPLE_STAMP) | $(STAMP_DIR)
	dotnet publish ExampleConsole -c Release
	touch $@

$(TESTPROJ_STAMP): $(TEST_SOURCES) $(ANALYZER_STAMP) | $(STAMP_DIR)
	dotnet build ArgumentParser.Tests -c Debug # never build in Release, we want stacktraces!

# targets
.PHONY: help

help:
	@echo "Makefile targets:"
	@echo "  all                    - Build everything and run tests"
	@echo "  clean                  - Clean the build artifacts and coverage reports"
	@echo "  build-analyzer         - Build the ArgumentParser analyzer (Release)"
	@echo "  restore-example        - Add locally built Aot.ArgumentParser package to ExampleConsole"
	@echo "  build-example          - Publish the ExampleConsole project as a native AOT executable"
	@echo "  build-tests            - Build the ArgumentParser.Tests project (Debug)"
	@echo "  test                   - Run the tests in ArgumentParser.Tests with coverage"
	@echo "  coverage               - Generate a coverage report for ArgumentParser.Tests"
	@echo "  set-pre-release-version- Set the pre-release version in the analyzer and example project based on git version"
	@echo "  set-version-stable     - Set the stable version in the analyzer and example project based on git version"
	@echo "  pre-release            - Performs checks and cuts a pre-release (FullSemVer tag)"
	@echo "  release-stable         - Performs checks and cuts a stable release (MajorMinorPatch tag)"
	@echo "  smoke-test             - Run smoke tests on the ExampleConsole executable"
	@echo "  format                 - Fix formatting and import order with dotnet format"
	@echo "  format-check           - Fail if dotnet format would change anything (needs the example restored, so run build-example first)"
	@echo "  help                   - Show this help message"

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

.PHONY: format format-check

format:
	dotnet format ArgumentParser.sln

format-check:
	dotnet format ArgumentParser.sln --verify-no-changes

coverage: build-tests
	reportgenerator -reports:ArgumentParser.Tests/coverage.cobertura.xml -targetdir:coveragereports -reporttypes:Html
	@echo "Coverage report generated in coveragereports/index.html"

set-pre-release-version:
	VERSION=$$(dotnet-gitversion | jq -r '.FullSemVer'); \
	if [ -z "$$VERSION" ]; then \
		echo "Versioning check failed: Unable to determine version"; \
		exit 1; \
	fi; \
	echo "Version to be used: $$VERSION"; \
	sed -i 's/public const string FullSemVer = "[^"]*";/public const string FullSemVer = "'$$VERSION'";/' ArgumentParser.Analyzer/CodeProviders/AssemblyVersionProvider.cs; \
	sed -i 's|<Version>[^<]*</Version>|<Version>'"$$VERSION"'</Version>|' ArgumentParser/ArgumentParser.csproj
	git commit -a --amend --no-edit

set-version-stable:
	VERSION=$$(dotnet-gitversion | jq -r '.MajorMinorPatch'); \
	if [ -z "$$VERSION" ]; then \
		echo "Versioning check failed: Unable to determine version"; \
		exit 1; \
	fi; \
	echo "Version to be used: $$VERSION"; \
	sed -i 's/public const string FullSemVer = "[^"]*";/public const string FullSemVer = "'$$VERSION'";/' ArgumentParser.Analyzer/CodeProviders/AssemblyVersionProvider.cs; \
	sed -i 's|<Version>[^<]*</Version>|<Version>'"$$VERSION"'</Version>|' ArgumentParser/ArgumentParser.csproj
	git commit -a --amend --no-edit

pre-release: clean set-pre-release-version test smoke-test
	@echo "Checking git working tree status..."
	@if [ -n "$$(git status --porcelain)" ]; then \
		echo "Error: Working tree is not clean. Please commit or stash changes before releasing."; \
		exit 1; \
	fi
	@echo "Creating git tag..."
	VERSION=$$(dotnet-gitversion | jq -r '.FullSemVer'); \
	echo "Enter tag message for v$$VERSION (press Enter to start editor):"; \
	git tag -a "v$$VERSION" -m "Automated build for $$VERSION"
	@echo "Release $$VERSION completed successfully."
	@echo "Remember to push the tag with: git push origin v$$VERSION"

release-stable: clean set-version-stable test smoke-test
	@echo "Checking git working tree status..."
	@if [ -n "$$(git status --porcelain)" ]; then \
		echo "Error: Working tree is not clean. Please commit or stash changes before releasing."; \
		exit 1; \
	fi
	@echo "Creating git tag..."
	VERSION=$$(dotnet-gitversion | jq -r '.MajorMinorPatch'); \
	echo "Enter tag message for v$$VERSION (press Enter to start editor):"; \
	git tag -a "v$$VERSION" -m "Automated build for $$VERSION"
	@echo "Release $$VERSION completed successfully."

smoke-test: build-example
	@echo "running smoke tests..."
	@scripts/smoke-test.sh ExampleConsole/bin/Release/net10.0/linux-x64/publish/ExampleConsole
