@echo off
REM Tech4Logic Video Search - Windows Development Scripts
REM Run from repository root

SET COMMAND=%1

IF "%COMMAND%"=="" (
    GOTO :help
)

IF "%COMMAND%"=="up" GOTO :up
IF "%COMMAND%"=="down" GOTO :down
IF "%COMMAND%"=="build" GOTO :build
IF "%COMMAND%"=="logs" GOTO :logs
IF "%COMMAND%"=="test" GOTO :test
IF "%COMMAND%"=="lint" GOTO :lint
IF "%COMMAND%"=="format" GOTO :format
IF "%COMMAND%"=="seed" GOTO :seed
IF "%COMMAND%"=="install" GOTO :install
IF "%COMMAND%"=="clean" GOTO :clean
IF "%COMMAND%"=="dev-api" GOTO :dev-api
IF "%COMMAND%"=="dev-web" GOTO :dev-web
IF "%COMMAND%"=="help" GOTO :help

echo Unknown command: %COMMAND%
GOTO :help

:up
docker compose up -d
echo.
echo Services starting...
echo   Web:     http://localhost:3000
echo   API:     http://localhost:5000
echo   Swagger: http://localhost:5000/swagger
echo.
GOTO :end

:down
docker compose down
GOTO :end

:build
docker compose build
GOTO :end

:logs
docker compose logs -f
GOTO :end

:test
cd apps\api && dotnet test
cd ..\web && pnpm test
GOTO :end

:lint
cd apps\api && dotnet format --verify-no-changes
cd ..\web && pnpm lint
GOTO :end

:format
cd apps\api && dotnet format
pnpm format
GOTO :end

:seed
docker compose exec -T postgres psql -U postgres -d t4l_videosearch -f /docker-entrypoint-initdb.d/init.sql
GOTO :end

:install
pnpm install
cd apps\api && dotnet restore
cd ..\worker && dotnet restore
GOTO :end

:clean
docker compose down -v --rmi local
rmdir /s /q apps\api\bin 2>nul
rmdir /s /q apps\api\obj 2>nul
rmdir /s /q apps\worker\bin 2>nul
rmdir /s /q apps\worker\obj 2>nul
rmdir /s /q apps\web\.next 2>nul
rmdir /s /q apps\web\node_modules 2>nul
rmdir /s /q node_modules 2>nul
GOTO :end

:dev-api
cd apps\api && dotnet watch run
GOTO :end

:dev-web
cd apps\web && pnpm dev
GOTO :end

:help
echo Tech4Logic Video Search - Development Commands
echo.
echo Usage: dev.cmd [command]
echo.
echo Docker Commands:
echo   up          Start all services in Docker
echo   down        Stop all services
echo   build       Build all Docker images
echo   logs        Show logs from all services
echo   clean       Remove all containers, volumes, and images
echo.
echo Development Commands:
echo   install     Install all dependencies
echo   dev-api     Start API in development mode
echo   dev-web     Start Web in development mode
echo.
echo Testing Commands:
echo   test        Run all tests
echo   lint        Run all linters
echo   format      Format all code
echo.
echo Database:
echo   seed        Seed the database with sample data
echo.
GOTO :end

:end
