using Serilog;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

//logger
builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration));

// Add Egroo chat services
builder.Services.AddChatServices()
    .WithConfiguration(builder.Configuration)
    .WithExecutionClassType(typeof(Program))
    .WithDatabase(DatabaseEnum.Postgres)
    .WithAutoMigrateDatabase(true)
    .WithDbConnectionStringKey("DefaultConnection")
    .Build();

WebApplication app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();

    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHttpsRedirection();
}

app.UseRouting();

// Use Egroo chat services
app.UseChatServices();

app.Run();