# Connectly

## Setup
1. Install dotnet 
2. Setup Postgres
3. Copy .env.example to .env then replace needed values
4. Install Entity Framework Tool
   1. <code>dotnet tool install --global dotnet-ef</code>
5. Run migrations
    1. <code>dotnet ef database update</code>