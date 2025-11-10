using Microsoft.EntityFrameworkCore;
using RestApi.Services;
using RestApi.Utils;
using AutoMapper;
using RestApi.Mappings;
using RestApi.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Swagger + JWT desteği
builder.Services.AddSwaggerWithJwt();

// DBContext
builder.Services.AddDbContext<ApplicationDbContext>(option =>
{
    var path = builder.Configuration.GetConnectionString("DefaultConnection");
    option.UseSqlite(path);
});

// JWT Authentication
builder.Services.AddJwtAuthentication(builder.Configuration);

//Add Class Scoped
builder.Services.AddScoped<UserService>();//UserService ' i injekte ediyoruz.
builder.Services.AddScoped<CategoryService>();//CategoryService ' i injekte ediyoruz.
builder.Services.AddScoped<AppointmentService>();

// Add Mapper Class
builder.Services.AddAutoMapper(typeof(AppProfile));//typeOf:hangi tür için çalıştırılması gerektiğini ifade ediyor.

// Controllers Class Add Container
builder.Services.AddControllers();

var app = builder.Build();

// Swagger UI Active
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Rest API v1");
        options.RoutePrefix = string.Empty; // http://localhost:5185
    });
}

// Middleware


app.UseAuthentication();
app.UseAuthorization();

//app.UseHttpsRedirection(); //Uygulama yayına alındığında aktif edilir

// App Add Middleware
app.UseMiddleware<GlobalExceptionHandler>();

// Controllers Class Maps
app.MapControllers();//Controllerların maplenmesi
app.Run();