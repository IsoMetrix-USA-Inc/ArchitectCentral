var builder = DistributedApplication.CreateBuilder(args);

// Add PostgreSQL with pgvector support and V2 enhanced schema initialization
var postgres = builder.AddPostgres("postgres", port: 5433)
    .WithImage("pgvector/pgvector", "pg17")
    .WithEnvironment("POSTGRES_INITDB_ARGS", "-E UTF8")
    .WithDataVolume("architect-central-postgres-data")
    .WithInitFiles("init-db-v2.sql")  // Initialize schema
    .WithPgAdmin();

// Add the architect_central database
var architectDb = postgres.AddDatabase("architect-central");

// Add KnowledgebaseVectoriser - runs on startup to populate database if empty
var vectoriser = builder.AddProject<Projects.ArchitectCentral_KnowledgebaseVectoriser>("knowledgebase-vectoriser")
    .WithReference(architectDb)
    .WithEnvironment("RUN_MODE", "AutoPopulate") // Only run if database is empty
    .WaitFor(architectDb);

// Add the AgentStudio API - depends on database being initialized
var agentStudio = builder.AddProject<Projects.ArchitectCentral_AgentStudio>("agentstudio")
    .WithReference(architectDb)
    .WaitFor(architectDb)
    .WaitFor(vectoriser);

builder.Build().Run();
