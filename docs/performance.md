# Performance Smoke Measurement

Handbook references: Chapter 17 (Performance Engineering) and Appendix G (Performance Targets).

This is a **lightweight, single-client latency smoke test** against the locally running
Development API. It is not a load or capacity test, and it does not validate how queries
scale with large data volumes (see [Limitations](#limitations)).

## Method

- Script: [`scripts/perf-smoke.sh`](../scripts/perf-smoke.sh) (bash + `curl` + `python3`; no extra tooling).
- Requests are sequential (one client, no concurrency). Latency is curl's `time_total`
  (full request including TLS handshake, since each curl invocation opens a new connection).
- Every read endpoint gets 3 unmeasured warm-up requests followed by 50 measured requests.
- Percentiles use the nearest-rank method over the measured samples.
- Login is limited by the `Auth` rate-limit policy (10 requests / 60 s / IP), so the script
  measures 8 logins and uses a 9th to obtain the access token. Logins are not warmed up, so
  the first login includes cold-start costs (JIT, Identity password-hasher, EF model).
- Authenticated reads use the Team Member test account `demo@smarttask.local`. The password
  is supplied through `SMARTTASK_PERF_PASSWORD` and is never stored in the script or printed.

Run it again with:

```bash
SMARTTASK_PERF_PASSWORD='<demo password>' scripts/perf-smoke.sh
# optional: SMARTTASK_API_URL, SMARTTASK_PERF_EMAIL, PERF_ITERATIONS, PERF_WARMUP, PERF_LOGIN_ITERATIONS
```

## Environment

| Item | Value |
|---|---|
| Date (UTC) | 2026-11-17T11:10:53Z |
| Host | Apple M5, 10 cores, 16 GB RAM, macOS 26.5 |
| API | ASP.NET Core (.NET SDK 10.0.400), `dotnet run --launch-profile https`, **Development** environment, Debug build, console Serilog + EF command logging enabled |
| Database | SQL Server 2022 (`mcr.microsoft.com/mssql/server:2022-latest`) in local Docker, database `SmartTaskDb` |
| Base URL | `https://localhost:7251` |
| Data visible to the test user | 1 project, 0 tasks |

## Results

| Endpoint | n | Non-2xx | Min ms | p50 ms | p95 ms | Max ms | Target (p95) | Result |
|---|---|---|---|---|---|---|---|---|
| GET /health | 50 | 0 | 26.4 | 29.1 | 33.1 | 33.2 | - | n/a |
| POST /api/v1/auth/login | 8 | 0 | 106.0 | 111.9 | 794.7 | 794.7 | < 1000 ms | PASS |
| GET /api/v1/projects (page 1, size 20) | 50 | 0 | 31.8 | 43.5 | 49.6 | 50.8 | < 500 ms | PASS |
| GET /api/v1/projects?search=a | 50 | 0 | 37.1 | 41.8 | 85.6 | 114.0 | < 500 ms | PASS |
| GET /api/v1/projects/{id}/tasks (page 1, size 20) | 50 | 0 | 36.3 | 42.9 | 56.7 | 66.9 | < 500 ms | PASS |
| GET /api/v1/projects/{id}/tasks?search=a | 50 | 0 | 34.4 | 44.2 | 89.6 | 184.3 | < 500 ms | PASS |
| GET /api/v1/dashboard/summary | 50 | 0 | 34.9 | 40.7 | 81.8 | 113.4 | < 800 ms | PASS |
| GET /api/v1/dashboard/task-statistics | 50 | 0 | 27.3 | 37.6 | 42.4 | 46.6 | < 800 ms | PASS |

### Comparison with Appendix G

| Appendix G metric | Target | Measured | Status |
|---|---|---|---|
| Standard API read p95 | < 500 ms | 49.6–56.7 ms (project list, task list) | Met on this dataset |
| Search p95 | < 500 ms | 85.6–89.6 ms (project and task search) | Met on this dataset |
| Dashboard p95 | < 800 ms | 42.4–81.8 ms (summary, task statistics) | Met on this dataset |
| Login p95 | < 1 s | 794.7 ms (n = 8; the p95 sample is the cold first login; warm logins are ~106–112 ms) | Met, with a small sample |
| Angular initial load | < 3 s | Not measured in a browser. Production build initial bundle: 285.23 kB raw / 80.34 kB transferred (gzip estimate from `ng build`); features are lazy-loaded. | **Not measured** |

## Limitations

- **Small dataset.** The test user can see one project and no tasks, so the task-list and
  dashboard numbers mostly measure framework, authentication, and round-trip overhead rather
  than query cost. They do not prove that the indexes in handbook §8.5 keep large task lists
  under target. A volume test with seeded data (for example thousands of tasks per project)
  remains outstanding.
- **Single client.** No concurrent load was applied; throughput and contention are unmeasured.
- **Development configuration.** Debug build, verbose EF Core command logging and the local
  development certificate; a Release build behind a production host would be expected to be
  faster, but that was not measured.
- **Login sample size** is capped at 8 by the authentication rate limit, so its p95 is the
  maximum of 8 samples.
- **Query plans** were not captured (Appendix J "Important query plans reviewed" remains open).
- **Angular initial load** was not measured with browser timing.
