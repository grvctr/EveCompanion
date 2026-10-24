# EVE Companion

Open-source Windows companion for EVE Online built with .NET 8 and WPF, without third-party runtime dependencies.

The project focuses on transparent, read-only assistance. It does not read game memory, scrape the cache, inspect network traffic, inject into the client, send input, or automate gameplay.

## Features

- Dashboard with current session totals and ISK/hour.
- Manual income and expense tracking with session history.
- Local JSON persistence in `%LOCALAPPDATA%\EveCompanionMvp\state.json`.
- A small anomaly catalog with conservative ship-role guidance.
- Public ESI market lookup by Region ID and Type ID.
- EVE SSO settings and OAuth2 PKCE authorization URL generation.
- Transparent click-through overlay showing the current ISK/hour.

## Requirements

- Windows 10 or 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## Build and run

```powershell
dotnet build .\EveCompanion.sln
dotnet run --project .\src\EveCompanion\EveCompanion.csproj
```

Run the built-in smoke checks for ISK/hour calculations, amount parsing, and PKCE:

```powershell
dotnet run --project .\src\EveCompanion\EveCompanion.csproj -- --self-test
```

## EVE developer setup

1. Register an application at [EVE Developers](https://developers.eveonline.com/).
2. Copy its **Client ID**.
3. Register the exact callback URL used by the application. The MVP default is `http://localhost:53682/callback/`.
4. Enable only the required read-only scopes:
   - `esi-assets.read_assets.v1`
   - `esi-wallet.read_character_wallet.v1`
   - `esi-markets.read_character_orders.v1`
5. Add a contact email to the ESI User-Agent setting.

Do not embed a client secret in a desktop build. The planned login flow uses PKCE, a local callback listener, and Windows-protected refresh-token storage.

## Roadmap

- Complete browser login, callback handling, token refresh, and JWT validation.
- Load character assets, wallet transactions, and market orders.
- Add EVE static data for item-name search instead of manual Type IDs.
- Replace the MVP anomaly heuristics with a versioned, sourced catalog.
- Add overlay position and opacity controls.

## Contributing

Issues and focused pull requests are welcome. Please keep dependencies and abstractions to a minimum, request only necessary ESI scopes, and never add client automation.

## License and disclaimer

Licensed under the [MIT License](LICENSE).

EVE Online and the EVE logo are trademarks of CCP hf. This project is not affiliated with, endorsed by, or sponsored by CCP hf. Use third-party tools at your own risk and review the [CCP third-party policies](https://support.eveonline.com/hc/en-us/articles/8564030965660-Third-Party-Policies).

Documentation: [EVE SSO](https://developers.eveonline.com/docs/services/sso/) · [ESI best practices](https://developers.eveonline.com/docs/services/esi/best-practices/)
