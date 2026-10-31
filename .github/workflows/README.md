# ArgumentParser GitHub Workflows

This directory contains GitHub Actions workflows that automate building and testing the ArgumentParser project.

## Workflows Overview

Releases (version bump and tagging) are done locally via the Makefile, see the main README.

### PR-checks.yaml

This workflow runs on pull requests to the `main` branch and:
- Builds the ArgumentParser analyzer
- Runs unit tests
- Builds the example project
- Performs integration tests by executing the example console app with various parameters

Purpose: Ensures that any changes in a pull request meet quality standards before merging.
