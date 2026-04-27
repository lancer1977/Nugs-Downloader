.PHONY: test test-verbose test-coverage test-race build clean

# Test commands
test:
	go test ./src/pkg/...

test-verbose:
	go test ./src/pkg/... -v

test-coverage:
	go test ./src/pkg/... -coverprofile=coverage.out
	go tool cover -html=coverage.out -o coverage.html
	@echo "Coverage report generated: coverage.html"

test-race:
	go test ./src/pkg/... -race

# Build commands
build-ui:
	cd ui && npm run build

build: build-ui
	go build -o bin/nugs-downloader ./src

build-all:
	GOOS=linux GOARCH=amd64 go build -o bin/nugs-downloader-linux-amd64 ./src
	GOOS=windows GOARCH=amd64 go build -o bin/nugs-downloader-windows-amd64.exe ./src
	GOOS=darwin GOARCH=amd64 go build -o bin/nugs-downloader-darwin-amd64 ./src
	GOOS=darwin GOARCH=arm64 go build -o bin/nugs-downloader-darwin-arm64 ./src

# Development
clean:
	go clean
	rm -f coverage.out coverage.html
	rm -rf bin/

# CI/CD
ci-test:
	go test ./src/pkg/... -v -race -coverprofile=coverage.out

# Linting (requires golangci-lint)
lint:
	golangci-lint run

# Dependencies
deps:
	go mod download
	go mod tidy
