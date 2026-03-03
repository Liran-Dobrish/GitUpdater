var builder = DistributedApplication.CreateBuilder(args);

var QueueManager = builder.AddRedis("QueueManager")
    .WithPersistence();

builder.AddProject<Projects.GitUpdater>("gitupdater")
    .WithReference(QueueManager)
    .WaitFor(QueueManager);

builder.Build().Run();
