using System.Security.Claims;
using System.Text;
using InstagramClone.Api.Data;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=Vibely.db";

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("JWT key was not configured.");

var databaseProvider = builder.Configuration["DatabaseProvider"]
    ?? (builder.Environment.IsDevelopment() ? "Sqlite" : "SqlServer");

if (databaseProvider.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
    connectionString = NormalizePostgresConnectionString(connectionString);

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (databaseProvider.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
        options.UseNpgsql(connectionString);
    else if (databaseProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
        options.UseSqlite(connectionString);
    else
        options.UseSqlServer(connectionString);
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme =
        JwtBearerDefaults.AuthenticationScheme;

    options.DefaultChallengeScheme =
        JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,

        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],

        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtKey)),

        ClockSkew = TimeSpan.Zero
    };

    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var userIdValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdValue, out var userId))
            {
                context.Fail("The session does not identify a valid user.");
                return;
            }

            var dbContext = context.HttpContext.RequestServices
                .GetRequiredService<AppDbContext>();
            var userExists = await dbContext.Users
                .AsNoTracking()
                .AnyAsync(user => user.Id == userId);

            if (!userExists)
                context.Fail("The account for this session no longer exists.");
        }
    };
});

builder.Services.AddAuthorization();
builder.Services.AddCors(options =>
{
    options.AddPolicy("LocalPreview", policy =>
    {
        policy.WithOrigins(
                "http://127.0.0.1:8765",
                "http://localhost:8765")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                               ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (databaseProvider.Equals("Postgres", StringComparison.OrdinalIgnoreCase) ||
        databaseProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
        dbContext.Database.EnsureCreated();
    else if (app.Environment.IsDevelopment())
    {
        dbContext.Database.EnsureCreated();
    }
    else
        dbContext.Database.Migrate();

    if (app.Environment.IsDevelopment() &&
        databaseProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        EnsureDevelopmentProfileColumns(dbContext);
        EnsureDevelopmentNotificationTable(dbContext);
        EnsureDevelopmentMessageTable(dbContext);
        EnsureDevelopmentFollowRequestTable(dbContext);
        EnsureDevelopmentStoryColumns(dbContext);
    }
}

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseForwardedHeaders();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors("LocalPreview");
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

static string NormalizePostgresConnectionString(string connectionString)
{
    if (!Uri.TryCreate(connectionString, UriKind.Absolute, out var uri) ||
        uri.Scheme is not ("postgres" or "postgresql"))
        return connectionString;

    var credentials = uri.UserInfo.Split(':', 2);
    return new NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.IsDefaultPort ? 5432 : uri.Port,
        Database = uri.AbsolutePath.Trim('/'),
        Username = Uri.UnescapeDataString(credentials[0]),
        Password = credentials.Length > 1 ? Uri.UnescapeDataString(credentials[1]) : string.Empty,
        SslMode = SslMode.Prefer
    }.ConnectionString;
}

static void EnsureDevelopmentProfileColumns(AppDbContext dbContext)
{
    var connection = dbContext.Database.GetDbConnection();
    connection.Open();

    var columns = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
    using (var command = connection.CreateCommand())
    {
        command.CommandText = "PRAGMA table_info('Users');";
        using var reader = command.ExecuteReader();
        while (reader.Read())
            columns[reader.GetString(1)] = reader.GetInt32(3) == 1;
    }

    if (columns.TryGetValue("PhoneNumber", out var phoneIsRequired) && phoneIsRequired)
    {
        using var dropPhoneIndex = connection.CreateCommand();
        dropPhoneIndex.CommandText = "DROP INDEX IF EXISTS IX_Users_PhoneNumber;";
        dropPhoneIndex.ExecuteNonQuery();

        using var dropPhoneColumn = connection.CreateCommand();
        dropPhoneColumn.CommandText = "ALTER TABLE Users DROP COLUMN PhoneNumber;";
        dropPhoneColumn.ExecuteNonQuery();
        columns.Remove("PhoneNumber");
    }

    foreach (var column in new[] { "ProfileLinkTitle", "ProfileLinkUrl", "PhoneNumber" })
    {
        if (columns.ContainsKey(column))
            continue;

        using var alter = connection.CreateCommand();
        alter.CommandText = column == "PhoneNumber"
            ? "ALTER TABLE Users ADD COLUMN PhoneNumber TEXT NULL;"
            : $"ALTER TABLE Users ADD COLUMN {column} TEXT NOT NULL DEFAULT '';";
        alter.ExecuteNonQuery();
    }

    using var otpTable = connection.CreateCommand();
    otpTable.CommandText = """
        CREATE TABLE IF NOT EXISTS PhoneOtpChallenges (
            Id INTEGER NOT NULL CONSTRAINT PK_PhoneOtpChallenges PRIMARY KEY AUTOINCREMENT,
            PhoneNumber TEXT NOT NULL,
            CodeHash TEXT NOT NULL,
            CreatedAtUtc TEXT NOT NULL,
            ExpiresAtUtc TEXT NOT NULL,
            ConsumedAtUtc TEXT NULL,
            Attempts INTEGER NOT NULL DEFAULT 0
        );
        CREATE INDEX IF NOT EXISTS IX_PhoneOtpChallenges_PhoneNumber_CreatedAtUtc
            ON PhoneOtpChallenges (PhoneNumber, CreatedAtUtc);
        CREATE UNIQUE INDEX IF NOT EXISTS IX_Users_PhoneNumber
            ON Users (PhoneNumber);
        """;
    otpTable.ExecuteNonQuery();
}

static void EnsureDevelopmentNotificationTable(AppDbContext dbContext)
{
    var connection = dbContext.Database.GetDbConnection();
    connection.Open();

    using var command = connection.CreateCommand();
    command.CommandText = """
        CREATE TABLE IF NOT EXISTS Notifications (
            Id INTEGER NOT NULL CONSTRAINT PK_Notifications PRIMARY KEY AUTOINCREMENT,
            RecipientId INTEGER NOT NULL,
            ActorId INTEGER NOT NULL,
            Type TEXT NOT NULL,
            PostId INTEGER NULL,
            Message TEXT NOT NULL,
            CreatedAtUtc TEXT NOT NULL,
            ReadAtUtc TEXT NULL,
            CONSTRAINT FK_Notifications_Users_RecipientId FOREIGN KEY (RecipientId) REFERENCES Users (Id) ON DELETE RESTRICT,
            CONSTRAINT FK_Notifications_Users_ActorId FOREIGN KEY (ActorId) REFERENCES Users (Id) ON DELETE RESTRICT
        );
        CREATE INDEX IF NOT EXISTS IX_Notifications_RecipientId_CreatedAtUtc
            ON Notifications (RecipientId, CreatedAtUtc);
        """;
    command.ExecuteNonQuery();
}

static void EnsureDevelopmentMessageTable(AppDbContext dbContext)
{
    var connection = dbContext.Database.GetDbConnection();
    connection.Open();

    using var command = connection.CreateCommand();
    command.CommandText = """
        CREATE TABLE IF NOT EXISTS DirectMessages (
            Id INTEGER NOT NULL CONSTRAINT PK_DirectMessages PRIMARY KEY AUTOINCREMENT,
            SenderId INTEGER NOT NULL,
            RecipientId INTEGER NOT NULL,
            Text TEXT NOT NULL,
            CreatedAtUtc TEXT NOT NULL,
            ReadAtUtc TEXT NULL,
            CONSTRAINT FK_DirectMessages_Users_SenderId FOREIGN KEY (SenderId) REFERENCES Users (Id) ON DELETE RESTRICT,
            CONSTRAINT FK_DirectMessages_Users_RecipientId FOREIGN KEY (RecipientId) REFERENCES Users (Id) ON DELETE RESTRICT
        );
        CREATE INDEX IF NOT EXISTS IX_DirectMessages_SenderId_RecipientId_CreatedAtUtc
            ON DirectMessages (SenderId, RecipientId, CreatedAtUtc);
        CREATE INDEX IF NOT EXISTS IX_DirectMessages_RecipientId_ReadAtUtc
            ON DirectMessages (RecipientId, ReadAtUtc);
        """;
    command.ExecuteNonQuery();
}

static void EnsureDevelopmentFollowRequestTable(AppDbContext dbContext)
{
    var connection = dbContext.Database.GetDbConnection();
    connection.Open();

    using var command = connection.CreateCommand();
    command.CommandText = """
        CREATE TABLE IF NOT EXISTS FollowRequests (
            FollowerId INTEGER NOT NULL,
            FollowingId INTEGER NOT NULL,
            CreatedAtUtc TEXT NOT NULL,
            CONSTRAINT PK_FollowRequests PRIMARY KEY (FollowerId, FollowingId),
            CONSTRAINT FK_FollowRequests_Users_FollowerId FOREIGN KEY (FollowerId) REFERENCES Users (Id) ON DELETE RESTRICT,
            CONSTRAINT FK_FollowRequests_Users_FollowingId FOREIGN KEY (FollowingId) REFERENCES Users (Id) ON DELETE RESTRICT
        );
        CREATE INDEX IF NOT EXISTS IX_FollowRequests_FollowingId_CreatedAtUtc
            ON FollowRequests (FollowingId, CreatedAtUtc);
        """;
    command.ExecuteNonQuery();
}

static void EnsureDevelopmentStoryColumns(AppDbContext dbContext)
{
    var connection = dbContext.Database.GetDbConnection();
    connection.Open();

    var hasMediaType = false;
    using (var inspect = connection.CreateCommand())
    {
        inspect.CommandText = "PRAGMA table_info('Stories');";
        using var reader = inspect.ExecuteReader();
        while (reader.Read())
            hasMediaType |= string.Equals(reader.GetString(1), "MediaType", StringComparison.OrdinalIgnoreCase);
    }

    if (hasMediaType)
        return;

    using var alter = connection.CreateCommand();
    alter.CommandText = "ALTER TABLE Stories ADD COLUMN MediaType TEXT NOT NULL DEFAULT 'image';";
    alter.ExecuteNonQuery();
}
