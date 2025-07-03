using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Annotations;

namespace PersonalCloudApi.Configuration;

public static class SwaggerConfig
{
    public static void AddSwaggerConfiguration(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Nube Personal API",
                Version = "v1",
                Description = "API para subir, listar y descargar archivos en tu PC desde cualquier dispositivo.",
                Contact = new OpenApiContact
                {
                    Name = "Tu Nombre",
                    Email = "tucorreo@example.com"
                }
            });

            options.EnableAnnotations();
            // JWT Authorization
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Description = "Ingrese el token como: Bearer {token}",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });

            

            options.TagActionsBy(api => new[] { api.GroupName ?? api.ActionDescriptor.RouteValues["controller"] });
            options.DocInclusionPredicate((_, _) => true);
        });
    }

    public static void UseSwaggerConfiguration(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.DocumentTitle = "Mi Nube Personal";
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Nube Personal API v1");
            options.RoutePrefix = "docs"; // /docs
        });
    }
}
