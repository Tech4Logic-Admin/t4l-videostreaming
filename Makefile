.PHONY: up down build test lint format seed clean logs api-logs web-logs help install dev

# Default target
help:
	@echo "Tech4Logic Video Search - Development Commands"
	@echo ""
	@echo "Usage: make [target]"
	@echo ""
	@echo "Docker Commands:"
	@echo "  up          Start all services in Docker"
	@echo "  down        Stop all services"
	@echo "  build       Build all Docker images"
	@echo "  logs        Show logs from all services"
	@echo "  api-logs    Show API service logs"
	@echo "  web-logs    Show Web service logs"
	@echo "  clean       Remove all containers, volumes, and images"
	@echo ""
	@echo "Development Commands:"
	@echo "  install     Install all dependencies"
	@echo "  dev         Start development servers (requires local deps)"
	@echo "  dev-api     Start API in development mode"
	@echo "  dev-web     Start Web in development mode"
	@echo ""
	@echo "Testing Commands:"
	@echo "  test        Run all tests"
	@echo "  test-api    Run API unit tests"
	@echo "  test-web    Run Web unit tests"
	@echo "  test-e2e    Run Playwright E2E tests"
	@echo ""
	@echo "Code Quality:"
	@echo "  lint        Run all linters"
	@echo "  format      Format all code"
	@echo ""
	@echo "Database:"
	@echo "  seed        Seed the database with sample data"
	@echo "  db-reset    Reset database to clean state"

# Docker commands
up:
	docker compose up -d
	@echo ""
	@echo "Services starting..."
	@echo "  Web:     http://localhost:3000"
	@echo "  API:     http://localhost:5000"
	@echo "  Swagger: http://localhost:5000/swagger"
	@echo ""
	@echo "Run 'make logs' to view logs"

down:
	docker compose down

build:
	docker compose build

logs:
	docker compose logs -f

api-logs:
	docker compose logs -f api

web-logs:
	docker compose logs -f web

clean:
	docker compose down -v --rmi local
	rm -rf apps/api/bin apps/api/obj
	rm -rf apps/worker/bin apps/worker/obj
	rm -rf apps/web/.next apps/web/node_modules
	rm -rf node_modules

# Development commands
install:
	pnpm install
	cd apps/api && dotnet restore
	cd apps/worker && dotnet restore

dev:
	@echo "Starting development servers..."
	@echo "Use 'make dev-api' and 'make dev-web' in separate terminals"

dev-api:
	cd apps/api && dotnet watch run

dev-web:
	cd apps/web && pnpm dev

dev-worker:
	cd apps/worker && func start

# Testing commands
test: test-api test-web
	@echo "All tests completed"

test-api:
	cd apps/api && dotnet test

test-web:
	cd apps/web && pnpm test

test-e2e:
	cd packages/e2e && pnpm test

# Code quality
lint: lint-api lint-web
	@echo "Linting completed"

lint-api:
	cd apps/api && dotnet format --verify-no-changes

lint-web:
	cd apps/web && pnpm lint

format: format-api format-web
	@echo "Formatting completed"

format-api:
	cd apps/api && dotnet format

format-web:
	pnpm format

# Database commands
seed:
	@echo "Seeding database..."
	docker compose exec -T postgres psql -U postgres -d t4l_videosearch -f /docker-entrypoint-initdb.d/init.sql

db-reset:
	docker compose down postgres
	docker volume rm t4l-videostreaming_pgdata || true
	docker compose up -d postgres
	@echo "Database reset complete. Run 'make seed' to add sample data."

# Health check
health:
	@echo "Checking service health..."
	@curl -s http://localhost:5000/healthz | jq . || echo "API not responding"
	@curl -s http://localhost:3000 > /dev/null && echo "Web: OK" || echo "Web: Not responding"
