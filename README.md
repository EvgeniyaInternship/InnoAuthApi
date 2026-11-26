# Auth API (InnoAuthApi)

Auth API signs users in through Keycloak and creates or deletes Keycloak users for the Profiles API. It does not store accounts. Passwords stay in Keycloak.

The executable project is `AuthApi/AuthApi.Api`. Target framework: **.NET 10**.

## What it does

| Method | Path | Result |
| --- | --- | --- |
| `GET` | `/Auth/login` | Redirects the browser to Keycloak and stores the PKCE cookies |
| `GET` | `/Auth/callback` | Exchanges the authorization code and sets `accessToken` and `refreshToken` cookies |
| `POST` | `/Auth/refresh-token` | Replaces those cookies |
| `GET` | `/Auth/logout` | Revokes the refresh token and clears the cookies |
| `POST` | `/Auth/register` | Creates a Keycloak user and assigns the realm role |
| `DELETE` | `/Auth/users/{id}` | Deletes that Keycloak user |

`GET /Auth/login` must be opened in the browser address bar. Swagger's `Try it out` cannot follow the Keycloak redirect.

The callback redirect is `Keycloak:RedirectUri` (`http://localhost:5119/Auth/callback` in `appsettings.json`). After login the browser goes to `Keycloak:PostLoginRedirectUri`.

## Logging

Serilog writes to the console and to MongoDB. The sink is `MongoDBBson`, database `logs`, collection `auth`. Compose sets `Serilog__WriteTo__0__Args__databaseUrl` from `x-mongo-logs-url`, using `MONGO_USER`, `MONGO_PASSWORD`, and `MONGO_DB`.

Logged events include startup and one Serilog request line per call with method, path, status, and elapsed time. `UseHttpLogging` adds one combined record with headers, query, duration, and the request and response bodies. Authorization and cookie headers are redacted by the framework. Each Auth service method logs its outcome. Warnings and errors from `GlobalExceptionHandler` go to the same collection.

## Local setup

Prerequisites: .NET SDK 10, Git, and a running Keycloak realm.

```powershell
cd AuthApi
dotnet restore
dotnet build
dotnet run --project AuthApi.Api --launch-profile http
```

The `http` profile listens on `http://localhost:5119`. Swagger is at `http://localhost:5119/swagger`. The `https` profile also listens on `https://localhost:7165`.

Set `Keycloak:BaseUrl`, `Keycloak:Realm`, `Keycloak:ClientId`, and `Keycloak:ClientSecret` for the confidential client. User secrets override `appsettings.json`. The API user-secrets id is `68db47a0-4471-4b47-ac8e-38c9bdcff29c`.

## Docker

This repository has a `Dockerfile` and no Compose file. The shared stack is `InnoInfrastructure`, checked out next to this repository. Compose sets the Keycloak settings and the MongoDB URL. Inside the image, `127.0.0.1:8080` is forwarded to the Keycloak container.

## CI

GitHub Actions runs Gitleaks, SonarQube, and CodeQL.
