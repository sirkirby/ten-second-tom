.PHONY: install build dev clean test test-watch coverage lint format check link-dev unlink-dev tom setup

# ─── Setup ───────────────────────────────────────────────────────────────────

install: ## Install all dependencies
	pnpm install

clean: ## Remove build artifacts and node_modules
	rm -rf packages/*/dist packages/*/.tsbuildinfo
	@echo "Build artifacts cleaned. Run 'make clean-all' to also remove node_modules."

clean-all: clean ## Remove build artifacts and node_modules
	rm -rf node_modules packages/*/node_modules pnpm-lock.yaml
	@echo "Full clean. Run 'make install' to reinstall."

# ─── Build ───────────────────────────────────────────────────────────────────

build: ## Build all packages
	pnpm -r build

dev: ## Watch mode — rebuild on changes
	pnpm --filter @ten-second-tom/core dev &
	pnpm --filter @ten-second-tom/cli dev

# ─── Quality ─────────────────────────────────────────────────────────────────

test: ## Run all tests
	pnpm vitest run

test-watch: ## Run tests in watch mode
	pnpm vitest

coverage: ## Run tests with coverage report
	pnpm vitest run --coverage

lint: ## Run ESLint
	pnpm eslint packages/

format: ## Format code with Prettier
	pnpm prettier --write "packages/**/*.{ts,tsx,json}"

format-check: ## Check formatting without writing
	pnpm prettier --check "packages/**/*.{ts,tsx,json}"

check: lint format-check test ## Run all checks (lint, format, tests)

# ─── Dev Convenience ─────────────────────────────────────────────────────────

link-dev: build ## Link 'tom' binary globally for local testing
	cd packages/cli && pnpm link --global
	@echo ""
	@echo "  'tom' is now linked to your local build."
	@echo "  Run 'tom setup' to get started."
	@echo "  Run 'make unlink-dev' when done."

unlink-dev: ## Remove global 'tom' link
	cd packages/cli && pnpm unlink --global
	@echo "  'tom' global link removed."

tom: build ## Run tom CLI directly (usage: make tom ARGS="record")
	@node packages/cli/dist/cli.js $(ARGS)

setup: build ## Shortcut for 'tom setup'
	@node packages/cli/dist/cli.js setup

# ─── Help ────────────────────────────────────────────────────────────────────

help: ## Show this help
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | sort | \
		awk 'BEGIN {FS = ":.*?## "}; {printf "  \033[36m%-15s\033[0m %s\n", $$1, $$2}'

.DEFAULT_GOAL := help
