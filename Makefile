.PHONY: test test-verbose test-coverage test-race build clean lint ci-test

SOLUTION := csharp/NugsDownloader.sln

# Test commands
test:
	dotnet test $(SOLUTION) --logger 'console;verbosity=minimal'

test-verbose:
	dotnet test $(SOLUTION) --logger 'console;verbosity=normal'

test-coverage:
	dotnet test $(SOLUTION) --collect:"XPlat Code Coverage" --results-directory TestResults
	@echo "Coverage results written to TestResults/"

test-race:
	@echo "No race detector is available for .NET; running the standard test suite instead."
	$(MAKE) test

# Build commands
build:
	dotnet build $(SOLUTION)

# Development
clean:
	dotnet clean $(SOLUTION)
	rm -rf TestResults/

# CI/CD
ci-test:
	dotnet test $(SOLUTION) --logger 'console;verbosity=minimal'

# Linting
lint:
	dotnet format $(SOLUTION) --verify-no-changes
