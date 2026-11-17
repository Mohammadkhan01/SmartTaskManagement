# SmartTask frontend

Angular 21 standalone, zoneless, signal-based client for the SmartTask API.
See the [root README](../README.md) for full setup (API, database, HTTPS
certificates) and the [architecture notes](../docs/architecture.md).

## Commands

```bash
npm ci                         # install exact dependency versions
npm start                      # HTTPS dev server at https://localhost:4200
npx ng test --watch=false      # Vitest unit/component tests
npx ng build                   # production build into dist/
```

The dev server expects `.certs/localhost.pem` and `.certs/localhost.key`
(git-ignored; generated with `dotnet dev-certs`, see the root README).

## Configuration

The API origin is defined per build configuration in
`src/environments/`: `environment.ts` is used by production builds and the
`development` configuration (used by `npm start`) swaps in
`environment.development.ts` via `fileReplacements`. Both currently point at
`https://localhost:7251`; set the production value to the deployed API origin.

## Structure

```text
src/app/
├── core/        API services, models, auth session store, guards, interceptor
├── features/    Routed pages (auth, dashboard, projects, tasks, admin)
├── layouts/     Authenticated application shell
└── shared/      Reusable pipes
```
