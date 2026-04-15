.PHONY: install build dev clean clean-all test test-watch coverage lint format format-check check link-dev unlink-dev tom setup help

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
	pnpm --filter ten-second-tom-core dev &
	pnpm --filter ten-second-tom dev

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


link-dev: build ## Link 'tom-dev' binary globally for local testing
	@mkdir -p $(HOME)/.local/bin
	@ln -sf $(PWD)/packages/cli/dist/cli.js $(HOME)/.local/bin/tom-dev
	@chmod +x $(HOME)/.local/bin/tom-dev
	@echo ""
	@echo "  'tom-dev' is now linked to your local build."
	@echo "  Run 'tom-dev setup' to get started."
	@echo "  Run 'make unlink-dev' when done."

unlink-dev: ## Remove global 'tom-dev' link
	@rm -f $(HOME)/.local/bin/tom-dev
	@echo "  'tom-dev' link removed."

tom: build ## Run tom CLI directly (usage: make tom ARGS="record")
	@node packages/cli/dist/cli.js $(ARGS)

setup: build ## Shortcut for 'tom setup'
	@node packages/cli/dist/cli.js setup

# ─── Help ────────────────────────────────────────────────────────────────────

help: ## Show this help
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | sort | \
		awk 'BEGIN {FS = ":.*?## "}; {printf "  \033[36m%-15s\033[0m %s\n", $$1, $$2}'

.DEFAULT_GOAL := help
