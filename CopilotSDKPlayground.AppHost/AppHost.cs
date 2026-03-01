var builder = DistributedApplication.CreateBuilder(args);

// Add the Copilot SDK API project
var api = builder.AddProject<Projects.CopilotSDKPlayground_Api>("copilot-api")
    .WithExternalHttpEndpoints();

builder.Build().Run();
