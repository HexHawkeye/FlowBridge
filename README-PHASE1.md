# FlowBridge Phase 1

Copy the three project folders over the projects created by `dotnet new`. Existing matching files can be replaced.

Then run:

```powershell
dotnet restore FlowBridge.sln
dotnet build FlowBridge.sln
dotnet run --project FlowBridge.Web
```

Open the HTTPS address displayed in the terminal. Create an integration, using a harmless test endpoint such as `https://httpbin.org/anything`, and select **Run now**.

Phase 1 intentionally stores header values as plain text. Do not enter production API keys yet. Encrypted secrets will be introduced before production deployment.
