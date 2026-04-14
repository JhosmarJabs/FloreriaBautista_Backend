using Microsoft.OpenApi.Models;

namespace FloreriaBautista.Extensions;

public static class SwaggerExtensions
{
    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title       = "Florera Bautista API",
                Version     = "v1",
                Description = "API REST — Sistema de gestión de inventarios y ventas."
            });

            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name         = "Authorization",
                Type         = SecuritySchemeType.Http,
                Scheme       = "bearer",
                BearerFormat = "JWT",
                In           = ParameterLocation.Header,
                Description  = "Ingresa el token JWT. Ejemplo: Bearer {token}"
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {{
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                },
                Array.Empty<string>()
            }});

            // Ordenar por grupo de acceso definido y luego por método
            c.OrderActionsBy(api =>
            {
                var tag = api.ActionDescriptor.EndpointMetadata
                    .OfType<Microsoft.AspNetCore.Http.TagsAttribute>()
                    .FirstOrDefault()?.Tags.FirstOrDefault() ?? "Público";

                var tagOrder = tag switch
                {
                    "Público"            => "1",
                    "Privado o Cliente"  => "2",
                    "Administrador"      => "3",
                    "Desarrollo"         => "4",
                    "Reportes"           => "5",
                    _                    => "9"
                };

                var methodOrder = (api.HttpMethod?.ToUpper()) switch
                {
                    "GET"    => "0",
                    "POST"   => "1",
                    "PUT"    => "2",
                    "PATCH"  => "3",
                    "DELETE" => "4",
                    _        => "9"
                };

                return $"{tagOrder}_{methodOrder}_{api.RelativePath}";
            });
        });

        return services;
    }

    public static IApplicationBuilder UseSwaggerDocumentation(this IApplicationBuilder app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Florera Bautista API v1");
            c.RoutePrefix = "swagger";

            // En Development: inyecta el token automáticamente via JS
            // Llama a /api/dev/token y lo pone en Swagger sin intervención manual
            c.HeadContent = @"
<script>
window.addEventListener('load', function() {
    setTimeout(async function() {
        try {
            const res = await fetch('/api/dev/token');
            if (!res.ok) return;
            const token = await res.text();
            const clean = token.replace(/^""|""$/g, '').trim();
            if (!clean) return;
            // Guardar en localStorage de Swagger UI
            const authKey = 'swagger_' + window.location.origin + '_Bearer';
            localStorage.setItem(authKey, JSON.stringify({ name: 'Bearer', schema: 'bearer', value: clean }));
            // Aplicar al cliente de Swagger UI
            if (window.ui) {
                window.ui.preauthorizeApiKey('Bearer', clean);
                console.log('[Dev] Token JWT aplicado automáticamente en Swagger');
            }
        } catch(e) {
            console.warn('[Dev] No se pudo obtener token dev:', e);
        }
    }, 1500);
});
</script>";
        });
        return app;
    }
}
