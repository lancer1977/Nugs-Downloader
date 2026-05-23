#!/usr/bin/env bash
set -euo pipefail

# Restore dependencies for the active C# solution.
dotnet restore csharp/NugsDownloader.sln

# Build and test the active solution.
dotnet build csharp/NugsDownloader.sln
dotnet test csharp/NugsDownloader.sln

# Clean build artifacts when you're done.
dotnet clean csharp/NugsDownloader.sln
