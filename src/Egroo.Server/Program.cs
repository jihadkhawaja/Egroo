using Serilog;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

//logger
builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration));

//change to your liking
#if DEBUG
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", builder =>
    builder
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());
});
#else
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", builder =>
    builder
    .WithOrigins("https://egroo.org", "https://www.egroo.org")
    .AllowAnyMethod()
    .AllowAnyHeader());
});
#endif

//mobile chat service
builder.Services.AddChatServices(
    builder.Configuration,
    typeof(Program),
    Register.DatabaseEnum.Postgres
    );

WebApplication app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseCors("CorsPolicy");

//chat hub
app.UseMobileChatServices();

app.Run();