# Transaction Fraud Detection API

A learning-focused REST API built with C# and ASP.NET Core to explore rule-based transaction fraud detection, risk scoring, and fraud monitoring workflows.

The project applies anti-fraud fundamentals to a practical backend application using simulated payment transactions.

> **Project status:** In development.

## Objectives

- Apply fraud domain concepts through testable business rules.
- Detect suspicious transaction patterns using rule-based logic.
- Calculate risk scores and produce review decisions.
- Persist transactions, fraud evaluations, and triggered rules.
- Practice clean backend design, database migrations, and automated testing.

## Technology Stack

- **Language:** C#
- **Framework:** ASP.NET Core Web API
- **Database:** PostgreSQL
- **ORM:** Entity Framework Core
- **Database Provider:** Npgsql
- **Schema Management:** EF Core Migrations
- **Database Environment:** Docker Compose
- **Testing:** xUnit
- **API Documentation:** Swagger / OpenAPI

The API will run locally, while PostgreSQL will run in a separate Docker container and be accessed through host port mapping.

A named Docker volume will persist PostgreSQL data across container restarts and recreation.

## Core Features

The following features are planned and will be implemented incrementally.

- Transaction submission and validation.
- Rule-based fraud risk evaluation.
- Risk scoring and decision generation.
- Recording triggered fraud rules.
- Retrieving transactions and their evaluations.
- Listing transactions flagged for review.
- Automated unit and integration tests.

## Initial Fraud Detection Rules

The first version will use three configurable-in-code business rules.

| Rule | Description | Risk Points |
|---|---|---:|
| R-01 | Transaction amount exceeds SAR 1,000 | 30 |
| R-02 | More than five eligible transactions occur within a rolling 10-minute window | 40 |
| R-03 | A matching transaction from the same customer and merchant, with the same amount and currency, occurs within two minutes | 50 |

These thresholds are educational assumptions, not universal banking standards.

### Risk Scoring

The risk score is the sum of the points assigned to triggered rules, capped at 100.

| Risk Score | Decision |
|---|---|
| 0–49 | `Allow` |
| 50–100 | `FlagForReview` |

A triggered rule does not automatically mean that the transaction is fraudulent. The system identifies suspicious activity for further review.

The initial version will not automatically block transactions.

## Data Model

The planned data model includes three main entities.

- **Transaction:** Stores the submitted transaction details.
- **FraudEvaluation:** Stores the risk score, decision, and evaluation timestamp.
- **TriggeredRule:** Records each rule triggered during a specific evaluation, including its rule code and awarded points.

Each transaction will have an evaluation in the initial workflow. An evaluation can have zero or more triggered rules.

## Architecture

The application will separate HTTP handling, fraud evaluation logic, and database persistence.

The fraud rules engine will be independently testable without requiring an HTTP request.

PostgreSQL will store the application data. Entity Framework Core migrations will manage database schema changes.

## Testing Strategy

The project will include:

- Unit tests for individual fraud rules.
- Tests for risk scoring and decision thresholds.
- Boundary tests for transaction amounts and time windows.
- Tests for multiple rules triggering together.
- Integration tests for database persistence and API behavior.

Tests will use simulated transactions and will not interact with real payment systems.

## Scope and Limitations

This project is an educational prototype.

It will not initially include:

- Real payment gateway integration.
- Real card data or payment credentials.
- Machine learning-based fraud detection.
- Automated AML reporting.
- Production-grade fraud prevention decisions.

Rule-based alerts are indicators for investigation, not proof of fraud.

## Development Roadmap

1. Define requirements and domain rules.
2. Create the .NET solution and Web API.
3. Configure PostgreSQL using Docker Compose.
4. Implement domain entities and EF Core migrations.
5. Build and test the fraud rules engine.
6. Implement transaction processing and persistence.
7. Add API endpoints.
8. Complete integration testing and documentation.

## Learning Goals

This project is intended to strengthen practical understanding of:

- Fraud versus error and abuse.
- Fraud prevention versus detection.
- Payment fraud patterns.
- Rule-based risk scoring.
- ASP.NET Core API development.
- Entity Framework Core and database migrations.
- PostgreSQL and Docker.
- Unit testing and integration testing.

