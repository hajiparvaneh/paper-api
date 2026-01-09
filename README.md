# PaperAPI Self-Hosted

Run PaperAPI locally with Docker Compose. This bundle includes:
- `pdfapi`: the PDF rendering API (wkhtmltopdf)
- `dashboard`: a local web UI for API keys and quick testing
- `postgres`: storage for jobs, keys, and logs

This is a self-hosted, free tier setup. No billing or subscriptions.

## Requirements
- Docker Desktop (or Docker Engine + Compose plugin)

## Quick start
```bash
cd self-hosted
docker compose up -d --build
```

Open the dashboard:
- http://localhost:3001

Open the PDF API health check:
- http://localhost:8087/health

## First login and admin setup
On first run, the dashboard asks you to create a single admin username and password.
These credentials are stored in the Postgres database for this instance.

## Create an API key
1) Log in to the dashboard.
2) Go to the API keys section and create a new key.
3) Copy the key once it appears (plaintext is stored only in your browser).

The server stores only a hash of the key, so keep the plaintext value somewhere safe.

## Test PDF generation
Use the test panel in the dashboard, or call the API directly:
```bash
curl -X POST http://localhost:8087/v1/generate \
  -H "Authorization: Bearer YOUR_KEY" \
  -H "Content-Type: application/json" \
  -d '{"html":"<html><body><h1>Hello PaperAPI</h1></body></html>"}' \
  --output document.pdf
```

## Default ports
- Dashboard: 3001
- PDF API: 8087
- Postgres: 5432

## Configuration (optional)
You can override defaults with environment variables.

Common options:
- `PDF_API_PUBLIC_URL` (default `http://localhost:8087`)
- `DASHBOARD_ORIGIN` (default `http://localhost:3001`)
- `POSTGRES_DB` (default `paperapi_selfhosted`)
- `POSTGRES_USER` (default `paperapi`)
- `POSTGRES_PASSWORD` (default `paperapi`)

Example:
```bash
PDF_API_PUBLIC_URL=http://127.0.0.1:8087 \
DASHBOARD_ORIGIN=http://127.0.0.1:3001 \
docker compose up -d --build
```

## Data and persistence
Data is stored in Docker volumes:
- Postgres data: `selfhosted-db-data`
- Data protection keys: `self-hosted/pdfapi/dp-keys`

To reset everything:
```bash
docker compose down -v
```

## Notes
- This setup is intended for local or private network use.
- The dashboard uses the self-hosted API to create and revoke keys.
- If you expose the API publicly, add your own auth and network protections.
- Docker Compose project name is set to `paper-api`, so containers and volumes are prefixed with that name.

## Troubleshooting
- If you see `relation "pdf_jobs" does not exist`, the database is still migrating. Wait and retry.
- If the API returns 401, make sure you are using a key created by this dashboard.
