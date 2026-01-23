# PaperAPI - Self-Hosted HTML to PDF Converter

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Docker](https://img.shields.io/badge/Docker-Ready-blue.svg)](https://www.docker.com/)
[![Status: Active](https://img.shields.io/badge/Status-Active-brightgreen.svg)](#)

A lightweight, free, self-hosted HTML-to-PDF conversion API powered by [wkhtmltopdf](https://wkhtmltopdf.org/). Perfect for developers who need PDF generation without external dependencies or costs.

## 📋 Table of Contents

- [Features](#features)
- [Requirements](#requirements)
- [Quick Start](#quick-start)
- [First Login & Admin Setup](#first-login--admin-setup)
- [API Usage](#api-usage)
  - [Create an API Key](#create-an-api-key)
  - [Generate PDF](#generate-pdf)
  - [Test PDF Generation](#test-pdf-generation)
- [Configuration](#configuration)
- [Architecture](#architecture)
- [Data & Persistence](#data--persistence)
- [Security Notes](#security-notes)
- [Troubleshooting](#troubleshooting)
- [Contributing](#contributing)
- [License](#license)

## ✨ Features

- **Free & Open Source** - No billing, no subscriptions, no hidden costs
- **Self-Hosted** - Complete control over your data and infrastructure
- **REST API** - Simple HTTP API for PDF generation
- **Web Dashboard** - User-friendly UI for managing API keys and testing
- **Authentication** - Built-in API key management system
- **Persistent Storage** - PostgreSQL database for jobs, keys, and logs
- **Docker Ready** - One-command setup with Docker Compose
- **wkhtmltopdf-Powered** - Reliable HTML-to-PDF conversion engine

## 📦 What's Included

- **pdfapi**: The PDF rendering API powered by wkhtmltopdf
- **dashboard**: Web UI for API key management and quick testing
- **postgres**: PostgreSQL database for persistent storage

## 🔧 Requirements

- Docker Desktop (or Docker Engine + Compose plugin)
- Approximately 500MB disk space for images and initial data

## 🚀 Quick Start

```bash
# Navigate to the project directory
cd /path/to/paper-api

# Start all services
docker compose up -d --build
```

### Access the Services

| Service | URL | Purpose |
|---------|-----|---------|
| Dashboard | http://localhost:3001 | Manage API keys, test PDF generation |
| PDF API | http://localhost:8087 | REST API endpoint |
| Health Check | http://localhost:8087/health | API status verification |

## 👤 First Login & Admin Setup

On first run, you'll be prompted to create admin credentials:

1. Open http://localhost:3001 in your browser
2. Create a username and password for the admin account
3. These credentials are securely stored in the PostgreSQL database
4. Log in with your new credentials

## 🔑 API Usage

### Create an API Key

1. Log in to the dashboard at http://localhost:3001
2. Navigate to the **API Keys** section
3. Click **Create New Key**
4. Copy the generated key immediately (displayed only once, in plaintext)
5. Store it securely - the server only stores a hashed version

⚠️ **Important**: Keep your API key confidential. Anyone with your key can generate PDFs.

### Generate PDF

**Endpoint**: `POST /v1/generate`

**Headers**:
```
Authorization: Bearer YOUR_API_KEY
Content-Type: application/json
```

**Request Body**:
```json
{
  "html": "<html><body><h1>Hello PaperAPI</h1></body></html>"
}
```

**Response**: Binary PDF file

### Test PDF Generation

#### Using cURL:
```bash
curl -X POST http://localhost:8087/v1/generate \
  -H "Authorization: Bearer YOUR_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"html":"<html><body><h1>Hello PaperAPI</h1></body></html>"}' \
  --output document.pdf
```

#### Using the Dashboard:
1. Log in to http://localhost:3001
2. Go to the **Test Panel**
3. Enter your HTML
4. Click **Generate PDF**
5. Download the generated PDF

## ⚙️ Configuration

You can override default settings using environment variables. Create a `.env` file or set them before running `docker compose up`:

### Available Options

| Variable | Default | Description |
|----------|---------|-------------|
| `PDF_API_PUBLIC_URL` | `http://localhost:8087` | Public URL for the PDF API |
| `DASHBOARD_ORIGIN` | `http://localhost:3001` | Dashboard URL for CORS configuration |
| `POSTGRES_DB` | `paperapi_selfhosted` | PostgreSQL database name |
| `POSTGRES_USER` | `paperapi` | PostgreSQL username |
| `POSTGRES_PASSWORD` | `paperapi` | PostgreSQL password |

### Example Configuration

```bash
# Using environment variables
export PDF_API_PUBLIC_URL=http://127.0.0.1:8087
export DASHBOARD_ORIGIN=http://127.0.0.1:3001
export POSTGRES_PASSWORD=your_secure_password

docker compose up -d --build
```

Or create a `.env` file:
```env
PDF_API_PUBLIC_URL=http://127.0.0.1:8087
DASHBOARD_ORIGIN=http://127.0.0.1:3001
POSTGRES_PASSWORD=your_secure_password
```

## 🏗️ Architecture

```
┌─────────────────────────────────────────┐
│         Docker Compose Stack            │
├─────────────────────────────────────────┤
│  Dashboard (Next.js)   :3001            │
│  PDF API (C# .NET)     :8087            │
│  PostgreSQL            :5432            │
└─────────────────────────────────────────┘
```

### Service Details

- **Dashboard**: Next.js-based web interface for key management and testing
- **PDF API**: C# .NET Core API handling PDF generation via wkhtmltopdf
- **Database**: PostgreSQL for persistent storage of users, API keys, and job history

## 💾 Data & Persistence

### Storage Locations

- **Postgres Data**: `paper-api-db-data` (Docker volume)
- **Data Protection Keys**: `./pdfapi/dp-keys/` (directory mount)

### Default Ports

| Service | Port |
|---------|------|
| Dashboard | 3001 |
| PDF API | 8087 |
| PostgreSQL | 5432 |

### Reset Everything

To completely reset the installation (removes all data):

```bash
docker compose down -v
```

This removes all containers and volumes. **Warning: This is irreversible!**

## 🔒 Security Notes

- ⚠️ **Local/Private Network Use Only**: This setup is designed for local development or private network deployment
- 🔐 **Production Deployment**: If you expose this API publicly, implement additional security measures:
  - Use HTTPS with valid SSL certificates
  - Implement rate limiting
  - Add firewall rules
  - Use strong, unique passwords
  - Regularly rotate API keys
  - Monitor API usage
  - Keep Docker images updated

- **API Keys**: The server only stores hashed API keys; the plaintext version is never persisted
- **Dashboard Authentication**: Admin credentials are hashed before storage

## 🐛 Troubleshooting

### "relation "pdf_jobs" does not exist" Error

The database is still initializing. This is normal on first run.

**Solution**: Wait 10-15 seconds and retry your request. The migration will complete automatically.

### API Returns 401 Unauthorized

Your API key is invalid, expired, or not created in this instance.

**Solution**: 
1. Verify you're using a key created by this dashboard
2. Log in to http://localhost:3001
3. Check that your key is listed in API Keys
4. Regenerate a new key if necessary

### Dashboard Won't Load

The dashboard service may still be starting.

**Solution**:
```bash
# Check container status
docker compose ps

# View logs
docker compose logs dashboard

# Restart if needed
docker compose restart dashboard
```

### High Memory/CPU Usage

Large PDF generation or queue processing may consume resources.

**Solution**: Monitor with `docker compose stats` and adjust resource limits in `docker-compose.yml` if needed.

### Database Connection Refused

PostgreSQL may not have started yet.

**Solution**:
```bash
docker compose logs postgres
docker compose restart postgres
```

## 📝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request. For major changes:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 🤝 Support

- 📖 Check the [Troubleshooting](#troubleshooting) section
- 💬 Open an issue on GitHub
- 📧 Please provide logs and configuration details when reporting issues

---

**Happy PDF generating! 🎉**
