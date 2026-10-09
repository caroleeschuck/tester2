# SOAP tester

A small Windows desktop tool for sending requests to the Dynamicweb `DWService` in a Dynamics 365 Finance and Operations environment. It signs in as you, so no client secret is needed.

## Features
- Environment dropdown with a Manage form (name, URL, tenant ID, company). Saved to `%AppData%\SoapTester\environments.json`.
- Sign-in: uses your existing `az login` session if there is one, otherwise opens a browser sign-in (MFA supported). Nothing secret is stored.
- Request pane (editable) and response pane, with one back/forward pair that steps through each request and its response together.
- Pretty-printed XML responses, an optional raw view, copy and save.
- Notepad++-style double-click word selection.

## Requirements
- Windows, .NET 9 SDK.
- An F&O user with access to the service in the target environment.

## Run
```
dotnet run --project .
```
Or publish a standalone copy:
```
dotnet publish -c Release -o publish
```

## Configure
1. Click **Manage…** and add an environment (for example `https://<env>.operations.dynamics.com/`, your tenant ID, and a company).
2. Paste the request XML into the left pane, for example `<GetEcomData Qty="1"><tables><Products type="all"/></tables></GetEcomData>`, and press Ctrl+Enter.

Service details (transport, REST path, SOAP namespace, operation, parameter name) are in `%AppData%\SoapTester\settings.json`, created on first run. The default transport is REST (`/api/services/DWService/DWService/process`), which sends the XML as `{"_request": "<xml>"}`. Set `Transport` to `Soap` to use the SOAP endpoint instead.

## Notes
- Responses come from live environments and may contain real data. Treat saved responses accordingly.
- `dw.ico` is Dynamicweb's logo, included for convenience. It is not covered by any license for this code.
