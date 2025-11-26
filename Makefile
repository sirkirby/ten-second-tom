.PHONY: all sidecar dotnet clean test-sidecar

all: extensions dotnet

extensions:
	@echo "Building macOS Extensions..."
	@mkdir -p bin
	@cd src/Extensions/MacOS && ./build.sh

dotnet: extensions
	@echo "Building .NET Application..."
	@dotnet build -f net10.0 src/TenSecondTom.csproj

clean:
	@echo "Cleaning..."
	@rm -rf bin
	@dotnet clean src/TenSecondTom.csproj

test-extension: extensions
	@echo "Testing Extension..."
	@./.scripts/test-notifier.sh "Testing from Makefile"
