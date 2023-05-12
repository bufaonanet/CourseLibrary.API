using CourseLibrary.API;

var builder = WebApplication.CreateBuilder(args);


var app = builder
            .ConfigureServices()
            .ConfigurePipeline();

// delete the database & migrate on startup so
// we can start with a clean slate
await app.ResetDatabaseAsync();

// run the app
app.Run();