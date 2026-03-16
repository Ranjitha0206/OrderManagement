# OrderManagement API

A production-ready REST API built with **C# / ASP.NET Core 8** for managing customer orders.

## Tech Stack
- C# / ASP.NET Core 8
- SQLite (native P/Invoke — no ORM)
- xUnit (21 unit tests)
- Docker + Render.com deployment

## Features
- Full order lifecycle management (Pending → InProgress → Completed → Cancelled)
- RESTful API with validation and error handling
- Frontend dashboard (HTML/CSS/JS)
- Repository pattern + Dependency Injection

## Run Locally
```bash
cd VS_Project/OrdersManagement
dotnet run
```
Open http://localhost:5167

## Run Tests
```bash
cd VS_Project/OrdersManagement.Tests
dotnet test
```
