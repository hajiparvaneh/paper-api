# Contributing to PaperAPI

Thank you for your interest in contributing to PaperAPI! We welcome contributions from the community. This document provides guidelines and instructions for contributing.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [Development Setup](#development-setup)
- [Making Changes](#making-changes)
- [Submitting Changes](#submitting-changes)
- [Code Style](#code-style)
- [Commit Messages](#commit-messages)
- [Testing](#testing)
- [Pull Request Process](#pull-request-process)

## Code of Conduct

This project adheres to a Code of Conduct. By participating, you are expected to uphold this code. Please report unacceptable behavior to the project maintainers.

## Getting Started

1. **Fork the repository** on GitHub
2. **Clone your fork** locally:
   ```bash
   git clone https://github.com/YOUR_USERNAME/paper-api.git
   cd paper-api
   ```
3. **Add upstream remote**:
   ```bash
   git remote add upstream https://github.com/hajiparvaneh/paper-api.git
   ```
4. **Create a branch** for your changes:
   ```bash
   git checkout -b feature/your-feature-name
   ```

## Development Setup

### Prerequisites
- Docker Desktop (or Docker Engine + Compose plugin)
- Node.js 18+ (for dashboard development)
- .NET 8+ (for PDF API development)

### Local Development

1. **Clone and navigate to the project**:
   ```bash
   cd paper-api
   ```

2. **Start the development environment**:
   ```bash
   docker compose up -d --build
   ```

3. **Access the services**:
   - Dashboard: http://localhost:3001
   - PDF API: http://localhost:8087

4. **View logs**:
   ```bash
   docker compose logs -f
   ```

### Working on Dashboard (Next.js)

```bash
cd dashboard
npm install
npm run dev
```

The dashboard will be available at http://localhost:3000 in development mode.

### Working on PDF API (.NET)

```bash
cd pdfapi/src/PdfApi
dotnet run
```

## Making Changes

### Before You Start

1. **Check existing issues** - Avoid duplicating work
2. **Create/claim an issue** - Let others know you're working on it
3. **Create a feature branch** - Never work on `main`

### Guidelines

- **Keep changes focused** - One feature or fix per pull request
- **Write clear commit messages** - Follow the [commit message guidelines](#commit-messages)
- **Update documentation** - Keep README and docs in sync with changes
- **Add tests** - Include tests for new functionality
- **Test your changes** - Run tests and verify the application works

## Submitting Changes

### Before Submitting

1. **Fetch upstream changes**:
   ```bash
   git fetch upstream
   git rebase upstream/main
   ```

2. **Run tests locally**:
   ```bash
   # For dashboard
   cd dashboard && npm run lint && npm test
   
   # For PDF API
   cd pdfapi && dotnet build && dotnet test
   ```

3. **Build Docker images** (optional):
   ```bash
   docker compose build
   ```

### Push Your Changes

```bash
git push origin feature/your-feature-name
```

## Code Style

### TypeScript/JavaScript (Dashboard)

- Follow ESLint configuration in the project
- Use TypeScript for type safety
- Format with Prettier (configured in project)

```bash
cd dashboard
npm run lint
npm run lint:fix  # Auto-fix issues
```

### C# (.NET - PDF API)

- Follow Microsoft C# coding conventions
- Use PascalCase for class names and public members
- Use camelCase for local variables
- Add XML documentation comments for public APIs

## Commit Messages

Write clear and descriptive commit messages:

```
<type>(<scope>): <subject>

<body>

<footer>
```

### Type
- `feat` - A new feature
- `fix` - A bug fix
- `docs` - Documentation changes
- `style` - Code style changes (formatting, missing semicolons, etc.)
- `refactor` - Code refactoring without feature changes
- `test` - Adding or updating tests
- `chore` - Dependency updates, build changes, etc.

### Examples

```
feat(api): add support for custom PDF options

Add ability to customize PDF generation options including page size,
margins, and headers/footers.

Closes #123
```

```
fix(dashboard): resolve API key display issue

API keys were being truncated in the dashboard. Now displaying full
key with copy-to-clipboard functionality.

Fixes #456
```

## Testing

### Adding Tests

- Add tests for new features
- Fix bugs with a test that reproduces the issue first
- Aim for good coverage of critical paths

### Running Tests

```bash
# Dashboard tests
cd dashboard && npm test

# PDF API tests
cd pdfapi && dotnet test
```

## Pull Request Process

1. **Create a Pull Request** on GitHub from your fork to the upstream repository
2. **Fill out the PR template** with relevant information
3. **Ensure checks pass locally**:
   - Code quality checks pass
   - Tests pass
4. **Respond to feedback** - Be receptive to suggestions and code review
5. **Ensure your branch is updated** - Rebase against `main` if needed

### PR Guidelines

- **Title**: Clear and descriptive (e.g., "Add PDF encryption support")
- **Description**: Explain what, why, and how
- **Link issues**: Reference related issues with `Closes #123` or `Fixes #456`
- **Keep it focused**: If the PR is too large, split it into smaller ones

## Review Process

- Maintainers will review your PR
- Feedback will be constructive and collaborative
- We may request changes before merging
- All discussions should be respectful and professional

## Questions?

- Check the [README](README.md) for general information
- Review [existing issues](https://github.com/hajiparvaneh/paper-api/issues) for similar questions
- Open a new issue with the `question` label

## Thank You!

Your contributions help make PaperAPI better for everyone. Thank you for investing your time in this project! 🎉
