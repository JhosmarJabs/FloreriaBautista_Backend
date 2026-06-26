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
                Title       = "Florería Bautista — API UNIFICADA",
                Version     = "v1",
                Description = "Documentación técnica unificada con todos los endpoints del sistema (Públicos, Privados y Administrativos)."
            });

            // Mostrar TODOS los endpoints sin filtros de ruta
            c.DocInclusionPredicate((docName, api) => true);

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
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });

            // Ordenar por grupo de acceso definido y luego por método
            c.OrderActionsBy(api =>
            {
                var tag = api.ActionDescriptor.EndpointMetadata
                    .Where(m => m.GetType().Name == "TagsAttribute")
                    .Select(m => (m.GetType().GetProperty("Tags")?.GetValue(m) as string[])?.FirstOrDefault())
                    .FirstOrDefault(t => t != null) ?? "Público";

                var tagOrder = tag switch
                {
                    string t when t.Contains("1.") => "1",
                    string t when t.Contains("2.") => "2",
                    string t when t.Contains("3.") => "3",
                    "Privado o Cliente"           => "4",
                    "Público"                     => "5",
                    "Desarrollo"                  => "6",
                    _                             => "9"
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
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "API Unificada (Todos los Endpoints)");
            c.RoutePrefix = "swagger";
            
            // Abrir por defecto los tags en el nivel de administración
            c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.List);
            c.DefaultModelsExpandDepth(-1);

            // En Development: inyecta el token automáticamente via JS y aplica Modo Oscuro
            c.HeadContent = @"
<style>
  /* Swagger Muted Deep-Navy (Comfort Optimized) */
  body, html { background-color: #23272e !important; color: #9da5b4 !important; font-family: ""Segoe UI"", Tahoma, Geneva, Verdana, sans-serif; }
  .swagger-ui { background-color: #23272e; color: #9da5b4; }
  .swagger-ui .topbar { background-color: #1e2227; border-bottom: 2px solid #4d78cc; }
  
  /* Secciones e Info (Muted) */
  .swagger-ui .info .title, .swagger-ui .info p, .swagger-ui .info li, .swagger-ui .opblock-tag { color: #abb2bf !important; }
  .swagger-ui .scheme-container { background: #2c313a; box-shadow: none; border: 1px solid #3e4451; border-radius: 8px; }
  
  /* ELIMINAR EL COLOR CLARO d0d2d4 DE CABECERAS */
  .swagger-ui .opblock-section-header { 
    background: #2c313a !important; 
    border-bottom: 1px solid #3e4451; 
    color: #abb2bf !important; 
    box-shadow: none !important;
    padding: 10px 20px !important;
  }
  .swagger-ui .opblock-section-header h4 { color: #abb2bf !important; font-size: 14px; }
  .swagger-ui .responses-wrapper, .swagger-ui .responses-inner, .swagger-ui .opblock-body { background: #23272e !important; }
  
  /* Bloques de Operación (Tonos Mate/Pastel no fosforescentes) */
  .swagger-ui .opblock.opblock-get { background: rgba(77, 120, 204, 0.05); border-color: #4d78cc; }
  .swagger-ui .opblock.opblock-post { background: rgba(86, 126, 89, 0.05); border-color: #567e59; }
  .swagger-ui .opblock-summary-method { border-radius: 4px; background: #21252b; color: #fff !important; min-width: 60px; }
  .swagger-ui .opblock-summary-path { color: #abb2bf !important; font-weight: 500; font-size: 14px; }
  
  /* Inputs y Selección (Bajo contraste) */
  .swagger-ui input[type=text], .swagger-ui textarea, .swagger-ui select { background: #1e2227 !important; color: #abb2bf !important; border: 1px solid #3e4451 !important; border-radius: 4px; }
  .swagger-ui .tabheader .tab-item.active { border-bottom: 2px solid #4d78cc; color: #fff !important; }
  
  /* Respuestas y Tablas */
  .swagger-ui table thead tr th { color: #5c6370 !important; border-bottom: 2px solid #3e4451; }
  .swagger-ui .response-col_status, .swagger-ui .response-col_links { color: #9da5b4 !important; }
  
  /* Botones (Satinados, no brillantes) */
  .swagger-ui .btn.authorize { background-color: #567e59; border-color: #567e59; color: #fff; opacity: 0.9; }
  .swagger-ui .btn.execute { background-color: #4d78cc; border-color: #4d78cc; color: #fff; border-radius: 4px; }
  .swagger-ui .btn.execute:hover { background-color: #3d60a3; }
  
  /* Bloques de Código y Modelos */
  .swagger-ui .model-box { background: #2c313a; border: 1px solid #3e4451; border-radius: 4px; }
  .swagger-ui section.models { border: 1px solid #3e4451; background: #2c313a; border-radius: 8px; }
  .swagger-ui section.models .model-container { background: #21252b; margin: 5px; }
  .swagger-ui .microlight { background: #1e2227 !important; color: #98c379 !important; border: 1px solid #3e4451; }
  
  /* Scrollbar */
  ::-webkit-scrollbar { width: 8px; }
  ::-webkit-scrollbar-track { background: #23272e; }
  ::-webkit-scrollbar-thumb { background: #3e4451; border-radius: 10px; }
</style>
<script>
window.addEventListener('load', function() {
    setTimeout(async function() {
        try {
            const res = await fetch('/api/dev/token');
            if (!res.ok) return;
            const token = await res.text();
            const clean = token.replace(/^""|""$/g, '').trim();
            if (!clean) return;
            const authKey = 'swagger_' + window.location.origin + '_Bearer';
            localStorage.setItem(authKey, JSON.stringify({ name: 'Bearer', schema: 'bearer', value: clean }));
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
