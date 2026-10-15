# Auth API (InnoAuthApi)

Auth API is a backend service responsible for authentication, authorization, token issuance, and user identity management within the platform.

## Prerequisites

Before building and running this project locally, ensure you have the following installed:

- [.NET SDK](https://dotnet.microsoft.com/download) (version 10.0 or later)
- [Git](https://git-scm.com/downloads)
- An IDE such as [Visual Studio 2022](https://visualstudio.microsoft.com/), [Visual Studio Code](https://code.visualstudio.com/), or [JetBrains Rider](https://www.jetbrains.com/rider/)

## Local Setup & Run

1. **Clone the repository**

   ```bash
   git clone https://github.com/EvgeniyaInternship/InnoAuthApi.git
   cd InnoAuthApi
   ```

2. **Restore dependencies**

   ```bash
   dotnet restore
   ```

3. **Build the project**

   ```bash
   dotnet build
   ```

4. **Run the API**

   Run the API project directly using the `.NET CLI`:

   ```bash
   dotnet run --project InnoAuthApi.Api
   ```

   *(If your startup project directory is named differently, adjust `InnoAuthApi.Api` to match your startup project folder).*

5. **Verify the service**

   Once started, the service will be accessible locally at:

   ```text
   https://localhost:5001
   http://localhost:5000
   ```

## Development & CI/CD Pipeline

This repository uses GitHub Actions for continuous security and code analysis:

- **Gitleaks**: Scans commits for hardcoded secrets and credentials.
- **SonarQube**: Static code analysis for quality, security vulnerabilities, and code smells.
- **CodeQL**: Automated security analysis for C# code.