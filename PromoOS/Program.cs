
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

namespace PromoOS
{
    record TaskCreateRequest(string Title);

    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

            builder.Services.AddSingleton<IRabbitMqPublisher>(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var connectionString = config.GetValue<string>("RabbitMq:ConnectionString") ?? "amqp://guest:guest@localhost:5672";
                var logger = sp.GetRequiredService<ILogger<RabbitMqPublisher>>();
                return new RabbitMqPublisher(connectionString, logger);
            });

            builder.Services.AddControllers();
            builder.Services.AddOpenApi();

            var app = builder.Build();

            // Graceful shutdown RabbitMQ
            var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
            lifetime.ApplicationStopping.Register(() =>
            {
                var publisher = app.Services.GetRequiredService<IRabbitMqPublisher>();
                if (publisher is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            });

            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();

            app.MapPost("/tasks", async (TaskCreateRequest request, AppDbContext db) =>
            {
                if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 200)
                    return Results.BadRequest("Валидация названия задачи не превышает 200 символов.");

                var task = new TaskItem
                {
                    Id = Guid.NewGuid(),
                    Title = request.Title,
                    IsCompleted = false,
                    CreatedAt = DateTimeOffset.UtcNow,
                    Priority = Priority.Medium
                };
                db.Tasks.Add(task);
                await db.SaveChangesAsync();
                return Results.Created($"/tasks/{task.Id}", task);
            });

            app.MapGet("/tasks", async (AppDbContext db) =>
            {
                var tasks = await db.Tasks.ToListAsync();
                return Results.Ok(tasks);
            });

            app.MapDelete("/tasks/{id:guid}", async (Guid id, AppDbContext db) =>
            {
                var task = await db.Tasks.FindAsync(id);
                if (task == null)
                    return Results.NotFound();
                db.Tasks.Remove(task);
                await db.SaveChangesAsync();
                return Results.NoContent();
            });

            app.MapPut("/tasks/{id:guid}/complete", async (Guid id, AppDbContext db, IRabbitMqPublisher publisher) =>
            {
                var task = await db.Tasks.FindAsync(id);
                if (task == null)
                    return Results.NotFound();
                if (task.IsCompleted)
                    return Results.Conflict("Задача уже выполнена.");
                task.IsCompleted = true;
                task.CompletedAt = DateTimeOffset.UtcNow;
                try
                {
                    await db.SaveChangesAsync();
                    publisher.PublishTaskCompleted(task);
                    return Results.NoContent();
                }
                catch (DbUpdateConcurrencyException)
                {
                    return Results.Conflict("Задача была изменена другим процессом.");
                }
            });

            app.Run();
        }
    }
}
