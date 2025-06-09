# ArgumentParser GitHub Workflows

This directory contains GitHub Actions workflows that automate building, testing, and releasing the ArgumentParser project.

## Workflows Overview

### PR-checks.yaml

This workflow runs on pull requests to the `main` branch and:
- Builds the ArgumentParser analyzer
- Runs unit tests
- Builds the example project
- Performs integration tests by executing the example console app with various parameters

Purpose: Ensures that any changes in a pull request meet quality standards before merging.

### main-build-and-tag.yaml

This workflow runs when changes are pushed to the `main` branch and:
- Configures the Git environment
- Installs required .NET tools (GitVersion, ReportGenerator)
- Updates version numbers in source files
- Runs all tests
- Creates a Git tag with the version
- Pushes changes and tags back to the repository

Purpose: Automates versioning and release tagging after changes are merged to the main branch.