# FlowBridge

[![Build](https://github.com/HexHawkeye/FlowBridge/actions/workflows/ci.yml/badge.svg)](https://github.com/HexHawkeye/FlowBridge/actions/workflows/ci.yml)
![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4)
![Docker](https://img.shields.io/badge/Docker-ready-2496ED)

FlowBridge is a self-hosted API integration platform built with ASP.NET Core and .NET 10. It provides a central dashboard for creating, scheduling, monitoring and retrying REST and GraphQL integrations.

It is designed to replace isolated scripts and scheduled tasks with observable integration workflows that record every request, response and failure.

## Features

### REST and GraphQL connectors

- GET, POST, PUT, PATCH and DELETE REST requests
- Native GraphQL queries and mutations
- GraphQL operation names and variables
- Automatic detection of GraphQL `errors` responses
- Configurable headers and JSON request bodies
- Dynamic editor that displays only the fields required by the selected connector

### Templates and scheduling

- Custom JSON template variables
- Built-in `{{utcNow}}`, `{{date}}` and `{{guid}}` values
- Variable substitution in URLs, headers, REST bodies and GraphQL variables
- Manual **Run now** action
- Recurring execution schedules
- Last-run and next-run tracking

### Reliability

- Automatic retry queue
- Exponential retry backoff
- Configurable retry count and initial delay
- Attempt tracking in execution history
- Dead-letter state after retries are exhausted
- Manual replay of failed executions
- HTTP status, duration, request, response and error logging

### Security and operations

- Administrator authentication with HTTP-only cookies
- Encrypted secret request headers using ASP.NET Core Data Protection
- Secrets are never displayed again after saving
- Persistent encryption keys for Docker deployments
- Structured JSON console logging
- `/health` health-check endpoint
- SQLite storage with automatic schema upgrades
- Docker and Docker Compose deployment
- GitHub Actions build, test and container pipeline

## Example uses

FlowBridge can be used to:

- Send SQL or application data to REST services
- Run Shopify Admin GraphQL queries and mutations
- Synchronise orders, products or gift-card transactions
- Call warehouse, finance or internal business APIs
- Schedule recurring API jobs
- Monitor failures and replay unsuccessful requests

## Solution structure

| Project | Purpose |
|---|---|
| `FlowBridge.Core` | Integration and execution models plus service contracts |
| `FlowBridge.Infrastructure` | SQLite persistence, REST/GraphQL execution, scheduling and retry workers |
| `FlowBridge.Web` | Razor Pages dashboard, authentication, configuration and health endpoint |
| `FlowBridge.Tests` | Automated tests |

## Running locally

Requirements:

- .NET 10 SDK
- Visual Studio 2026 or another .NET-compatible editor

From the repository root:

```powershell
dotnet restore FlowBridge.slnx
dotnet build FlowBridge.slnx
dotnet run --project FlowBridge.Web
```

Open the address displayed after `Now listening on:`.

The development credentials are:

```text
Username: admin
Password: change-me-now
```

Change the password before exposing FlowBridge to a network.

## Administrator password

Generate a SHA-256 password hash in PowerShell:

```powershell
$password = Read-Host -AsSecureString
$plain = [System.Net.NetworkCredential]::new('', $password).Password
$bytes = [Text.Encoding]::UTF8.GetBytes($plain)
[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLower()
```

Store the hash using .NET user secrets:

```powershell
dotnet user-secrets init --project FlowBridge.Web
dotnet user-secrets set "Admin:PasswordSha256" "YOUR_HASH" --project FlowBridge.Web
```

The administrator username can be changed with `Admin:Username`.

## Creating a REST integration

Example configuration:

```text
Connector: REST
Method: POST
Endpoint: https://postman-echo.com/post
```

Request body:

```json
{
  "source": "{{source}}",
  "sentAt": "{{utcNow}}",
  "correlationId": "{{guid}}"
}
```

Template variables:

```json
{
  "source": "FlowBridge"
}
```

## Creating a GraphQL integration

Example query:

```graphql
query GetCountry($code: ID!) {
  country(code: $code) {
    name
    capital
    currency
  }
}
```

GraphQL variables:

```json
{
  "code": "GB"
}
```

FlowBridge considers a GraphQL response unsuccessful when it contains a non-empty `errors` array, even if the server returns HTTP status `200`.

## Headers and secrets

Non-sensitive headers can be entered in the standard headers field:

```json
{
  "X-Source": "FlowBridge"
}
```

Credentials and API tokens should be entered in **Secret headers**:

```json
{
  "Authorization": "Bearer your-token",
  "X-Shopify-Access-Token": "your-shopify-token"
}
```

Secret headers are encrypted before being stored. Keep the Data Protection keys together with the database backup; encrypted values cannot be recovered if those keys are lost.

## Docker

Copy `.env.example` to `.env` and enter an administrator password hash:

```text
FLOWBRIDGE_ADMIN_USERNAME=admin
FLOWBRIDGE_ADMIN_PASSWORD_SHA256=your_hash_here
```

Build and start FlowBridge:

```bash
docker compose up -d --build
```

Open [http://localhost:8080](http://localhost:8080).

The health endpoint is available at [http://localhost:8080/health](http://localhost:8080/health).

Docker Compose retains application data and encryption keys in separate persistent volumes:

- `flowbridge-data`
- `flowbridge-keys`

Stop the application without removing those volumes:

```bash
docker compose down
```

## Configuration

| Environment variable | Purpose |
|---|---|
| `Admin__Username` | Administrator username |
| `Admin__PasswordSha256` | SHA-256 administrator password hash |
| `ConnectionStrings__FlowBridge` | SQLite database connection string |
| `DataProtection__KeyPath` | Persistent encryption-key directory |
| `ASPNETCORE_URLS` | Addresses and ports used by the web application |

Never commit `.env`, SQLite databases, Data Protection keys or real credentials.

## Build and test

```powershell
dotnet restore FlowBridge.slnx
dotnet build FlowBridge.slnx -c Release
dotnet test FlowBridge.Tests/FlowBridge.Tests.csproj -c Release
```

The GitHub Actions workflow performs the build and tests and verifies that the Docker image can be created.

## Current limitations

- SQLite is currently the only FlowBridge configuration database.
- Secret headers cannot be viewed after saving; enter replacements to change them.
- Back up the Data Protection keys whenever encrypted secrets are used.
- Integration deletion also removes its execution and retry history.

