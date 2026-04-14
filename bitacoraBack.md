# Bitácora de Proyecto - Florería Bautista

Este archivo sirve como registro continuo de actividades, decisiones arquitectónicas, tareas completadas y deuda técnica detectada durante el desarrollo del proyecto. 

*Instrucción para Agentes: Lee este archivo antes de iniciar para recuperar el contexto y OBLIGATORIAMENTE actualízalo antes de finalizar tu sesión.*

---
## [2026-04-13] - Consolidación de Endpoints y Ajustes de Schema

### 🚀 Cambios y Tareas Realizadas

#### 🛠️ Refactorización de Catálogo de Productos
- **Simplificación de Endpoints**: Se eliminaron los endpoints específicos `/status` y `/delete` de `/api/admin/products/{productId}`. Ahora, tanto la visibilidad como el borrado lógico se gestionan exclusivamente a través del endpoint principal de actualización `POST /api/admin/products/{productId}` enviando las propiedades `Visibilidad` y `Activo` respectivamente.
- **Documentación**: Actualizados `ENDPOINTS.md` y `documentacionJSON.md` para reflejar la eliminación de los endpoints redundantes.

#### 🛡️ Seguridad y Mantenimiento
- **Fix Permisos de Mantenimiento**: Corregido el error `42501: permission denied for table addresses` que bloqueaba la tarea de `REINDEX`. Se otorgó explícitamente el privilegio `MAINTAIN` (PG17+) al rol `app_admin` en `02_floreria_roles_usuarios.sql`, permitiendo que la API realice limpieza, analyze y reindexado sin requerir ser el propietario de cada tabla.
- **Seguridad (Seeds)**: Se verificó y aseguró que todos los usuarios de prueba en `03_floreria_seeds_pruebas.sql` utilicen el hash de contraseña de prueba estándar establecido para desarrollo.

#### 🗄️ Estandarización de Base de Datos e Insumos
- **Schema SQL**: Se renombró la columna `url_imagen` a `imagen_url` en la tabla `inventory_items` en `01_floreria_schema.sql` para paridad con el código.
- **Fix EF Core Mapping**: Corregido el error `42703: column i.url_imagen does not exist`. Se actualizó `InventoryItemConfiguration.cs` para mapear la propiedad `ImagenUrl` a la nueva columna `imagen_url`.
- **Fix Concurrency Exception**: Corregido el error `DbUpdateConcurrencyException` al actualizar productos. La lógica se cambió de `RemoveRange` manual a una manipulación directa de las colecciones cargadas (`Clear()` seguido de `Add()`). Esto permite que Entity Framework gestione eficientemente el estado de las relaciones (detección de cambios) y evita conflictos de rastreo que causaban el error de "0 filas afectadas".
- **Estandarización de Propiedades**: Se ajustó la actualización para preservar el formato original en campos como `Tipo` y asegurar que `Visibilidad` y `Estado` se almacenen en mayúsculas consistentemente.
- **Privilegios**: Se incluyó el permiso `TRUNCATE` para el rol administrativo, habilitando operaciones de limpieza profunda de logs.
- **Git Commit**: Se realizó el commit `v 1.2.0 refactor: consolidacion de endpoints y correccion de estabilidad en base de datos` que consolida todos los cambios técnicos de la sesión.

### 📅 Próximos Pasos (Next Steps)
- Validar que el frontend consuma correctamente el endpoint de actualización unificado para cambios de visibilidad.
- Probar la generación de reportes usando los datos generados por el nuevo simulador.

---
## [2026-04-13] - Consolidación: Backups, Seguridad y Mejoras en Inventario

### 🚀 Cambios y Tareas Realizadas

#### 🛡️ Seguridad y Mantenimiento
- **Limpieza de Backups**: Implementada política de retención de 10 copias. El scheduler ahora limpia automáticamente archivos locales y en Google Drive tras cada backup exitoso.
- **Permisos de DB**: Ajustado el script `02_floreria_roles_usuarios.sql` para permitir que el rol operativo (`app_writer`) modifique el catálogo, evitando errores 500 por permisos denegados.

#### 📦 Módulo de Inventario (Insumos)
- **Borrado Lógico (Soft Delete)**: Se unificó la política de desactivación. En productos, se eliminó el endpoint específico `/delete` y ahora se gestiona directamente a través del campo `activo` en el endpoint de actualización general, simplificando el flujo.
- **Estandarización de Imágenes**: Se renombró el campo de `UrlImagen` a `ImagenUrl` en Entidad, DTOs y Servicio para mantener paridad con el modelo de Productos y peticiones del Frontend.
- **Campos Financieros en Inventario**: Se agregaron `PrecioCosto` y `EsFlorPrimaria` a `InventoryItem` para habilitar el cálculo automático de costos de productos basados en sus recetas.
- **Gestión Completa de Recetas**: Se habilitó la capacidad de obtener y actualizar la receta (insumos y cantidades) de un producto a través de los DTOs de creación y edición.
- **Búsqueda Robusta**: Se actualizó `InventoryService.ListarAsync` para que la búsqueda por nombre sea insensible a mayúsculas y minúsculas (`ToLower()`), mejorando la experiencia del usuario al buscar materiales.
- **Correcciones de CRUD**: 
    - Se habilitó la actualización de `StockActual` en `InventoryService.ActualizarAsync` y su correspondiente DTO.
    - Los métodos de actualización se migraron de `PUT` a `HttpPost` para alinearse con la política de comunicación establecida.

#### 📊 Reportes y Dashboard
- **Dashboard API**: Implementado `GET /api/admin/reports/dashboard`.
- **Cálculo de Métricas**: Nueva lógica en `ReportsService` para calcular en tiempo real: Ventas totales (30 días), Ticket promedio, Nuevos clientes (7 días), Histograma de ventas semanales e Inventario crítico por debajo del mínimo.
- **DTOs**: Creado `DashboardStatsDto` para transferencia eficiente de métricas consolidadas.

### 📅 Próximos Pasos (Next Steps)
- Probar la generación de reportes usando los datos generados por el nuevo simulador.
- Implementar la lógica de "Suma al costo" en el cálculo del total de piezas personalizadas usando las recetas del inventario.

---
## [2026-04-13] - Módulo de Clientes y Direcciones

### 🚀 Cambios y Tareas Realizadas
- **Nuevo controlador `CustomersController`**: Implementación completa del módulo de Clientes (sección 2 de `ENDPOINTS.md`). Los 8 endpoints pasaron de ❌ a ✅.
  - `GET /api/customers/search` → Búsqueda paginada por nombre/teléfono/correo. Solo ADMIN/EMPLEADO.
  - `POST /api/customers/physical` → Alta rápida de cliente tipo MOSTRADOR para ventas en mostrador. Solo ADMIN/EMPLEADO.
  - `GET /api/customers/{customerId}/orders` → Historial de pedidos paginado de un cliente específico. Solo ADMIN/EMPLEADO.
  - `GET /api/customers/me/addresses` → Lista de direcciones guardadas del cliente logueado.
  - `POST /api/customers/me/addresses` → Guarda nueva dirección para el cliente logueado.
  - `POST /api/customers/me/addresses/{addressId}` → Actualiza dirección existente del cliente.
  - `POST /api/customers/me/addresses/{addressId}/deactivate` → Elimina dirección de la BD.
  - `GET /api/customers/me/addresses/suggestions` → Combina direcciones guardadas + historial de pedidos para autocompletar. Deduplica por calle+colonia.
- **Nuevo archivo `Models/DTOs/Customers/CustomerDtos.cs`**: DTOs `CustomerSummaryDto`, `AddressDto`, `CreatePhysicalCustomerRequestDto`, `SaveAddressRequestDto`, `AddressSuggestionDto`.
- **`ENDPOINTS.md` actualizado**: Sección 2 completa (8/8 ✅). Tabla resumen corregida.

### 🚧 Problemas / Blockers
- N/A

### 📅 Próximos Pasos (Next Steps)
- Desarrollar módulo de Pagos (`/api/orders/{orderId}/payments`).
- Desarrollar módulo de Entregas (`/api/admin/deliveries`).

---
## [2026-04-13] - Desarrollo de Controladores Administrativos del Catálogo y CMS

### 🚀 Cambios y Tareas Realizadas
- Revisión cruzada de la estructura actual de los controladores frente a las definiciones en `ENDPOINTS.md` y `reglasGenerales.md`.
- **Revisión de Productos (`AdminProductsController`)**: Se validó el manejo complejo de relaciones cruzadas en `IProductService.cs` y su consumo para creación (`POST`) y actualización (`POST /{productId}`). Funcionan correctamente, gestionando la inserción de imágenes y categorías/colecciones.
- **Endpoint Visibilidad de Producto**: Implementación de `POST /api/admin/products/{productId}/status` en `AdminProductsController` para poder interactuar cómodamente con la propiedad Visibilidad (Ej. ocultar producto de la web).
- **Módulo CRUD de Categorías y Colecciones**: Desarrollo e integración en base de datos de los endpoints `POST /api/admin/categories` y `POST /api/admin/collections` con sus correspondientes Data Contexts (`AppDbContext`).
- **Complementos de Productos**: Desarrollo de `AdminCustomizationOptionsController` (POST `/api/admin/customization-options`) para operar con elementos como Listones, Globos, configurando el mapeo con Entity Framework hacia las entidades.
- **Mock de CMS**: Desarrollo preliminar de `AdminCmsController` con el método de inicialización (`POST /api/admin/cms`), preparando la recepción de la configuración global de textos.
- **Desarrollo de Inventario y Recetas**:
  - `AdminInventoryController`: Verificadas las rutas de registro de invetario CRUD, se documentó que `POST /movements` cubre los Logs en base de datos a nivel movimiento, y se añadió un endpoint en `/alerts` conectando con el servicio `bajoMinimo=true`. 
  - `AdminRecipesController`: Creado íntegramente. Incluye los endpoints de listar integradas todas las vistas `GET`, guardar la configuración masiva/explosión en base con un `POST /{productId}` que limpia en cascada, y finalmente `GET /{productId}/suggested-price` para retornar un costo paramétrico simple basado en la sumatoria condicional.
- Documento `ENDPOINTS.md` actualizado en su totalidad marcando como listos (✅) ambos bloques.
- Documento `documentacionJSON.md` ha sido reorganizado y agrupado en base a sus Roles correspondientes (Público, Privado, Administrador), por método (`GET`/`POST`) y por función, mejorando su legibilidad final.
- **Optimización de Swagger**: Se simplificaron los Tags de todos los controladores a 5 categorías principales (`Público`, `Privado o Cliente`, `Administrador`, `Reportes`, `Desarrollo`) y se configuró un ordenamiento fijo en `SwaggerExtensions.cs` para mejorar la experiencia de navegación en la documentación interactiva.

### 🚧 Problemas / Blockers
- N/A

### 📅 Próximos Pasos (Next Steps)
- Mapear el controlador del CMS hacia modelos de bases de datos definitivos.
- Continuar con el módulo de Usuarios y Entregas.

## [2026-04-06] - Revisión de Seguridad y Conexiones

### 🚀 Cambios y Tareas Realizadas
- Revisión de configuraciones de base de datos. Se comparó el archivo `.env` con el script de creación de roles `02_floreria_roles_usuarios.sql`.
- Se actualizó el archivo de configuración `.geminirules` para reforzar el registro obligatorio de cambios en la bitácora al final de cada intervención.

### 🚧 Problemas / Blockers
- **Desajuste de Roles y Contraseñas**: Se detectó una inconsistencia crítica entre los usuarios definidos en `.env` (`app_user`, `backup_user`, `db_admin_user`) y los creados realmente en el script SQL de PostgreSQL (`app_user_writer`, `app_user_backup`, `app_user_admin`). Asimismo, las contraseñas placeholder de ambos archivos no coinciden.

### 📅 Próximos Pasos (Next Steps)
- Actualizar el archivo `.env` para que los nombres de usuario y credenciales coincidan de manera exacta con el diseño de seguridad de la base de datos (Ej: `DB_USER=app_user_writer`, `BACKUP_DB_USER=app_user_backup`, etc).
- Validar las conexiones de la API contra la base de datos tras las modificaciones.

---
## [2026-04-06] - Revisión General del Proyecto (Claude Code)

### 🚀 Cambios y Tareas Realizadas
- Revisión integral del proyecto con exploración automatizada de toda la codebase.
- **Build verificado**: `dotnet build` exitoso con **0 errores, 0 advertencias**.
- **Migración de arquitectura confirmada como completa y limpia**:
  - Eliminadas las entidades `Flower`, `FlowerMovement`, `ProductFlower` y sus configuraciones/DTOs.
  - Ninguna referencia huérfana en código activo (solo en `/obj/Debug/` que se limpia en rebuild).
  - `AdminFlowersController` reutilizado correctamente para gestión de **Recetas de Productos**.
- **Nueva entidad `ProductRecipe`** integrada correctamente.
- **Todos los pares Servicio/Interfaz verificados**.
- **Fix (Entity Framework)**: Se actualizó el archivo `01_floreria_schema.sql` y `02_floreria_roles_usuarios.sql` para incluir la creación de la tabla faltante `scheduler_settings` que era solicitada por `BackupSchedulerService`.

### 🚧 Problemas / Blockers
- Ningún problema crítico ni de compilación detectado.
- Pendiente de sesiones anteriores: verificar credenciales `.env` vs `02_floreria_roles_usuarios.sql` (nombres de usuario `app_user` vs `app_user_writer`).

### 📅 Próximos Pasos (Next Steps)
- Generar y aplicar migración de EF Core para la tabla `product_recipes` (aún sin migración formal).
- Verificar conectividad de la API contra PostgreSQL tras corregir credenciales del `.env`.
- Considerar agregar `IRecipeService` para mejor testabilidad de las recetas.

---
## [2026-04-07] - Implementación de Endpoints Faltantes (Skill Supervisor)

### 🚀 Cambios y Tareas Realizadas (Claude/Gemini)
- Identificación de discrepancias de APIs entre ENDPOINTS.md y floreria-supervisor.skill
- **Implementación de Exportación:** Se agregaron `GET /api/admin/export/orders` y `GET /api/admin/export/customers` a `AdminImportExportController` y lógica completa CSV al `ExportService`.
- **Implementación de Historial:** Se agregó `GET /api/admin/import-jobs` al `AdminImportExportController` consumiendo desde el DB Set `ImportJobs`.
- **Implementación de Tablas de Respaldo:** Se agregó `GET /api/admin/backups/tablas` invocando a `ObtenerTablasAsync()` del servicio correspondiente.
- **Health Check Global:** Se creó el controlador `AdminHealthController` resolviendo la exigencia del endpoint `GET /api/admin/health` para la operación técnica global.
- Documentación principal (**ENDPOINTS.md**) actualizada para reflejar los nuevos cambios operacionales.
- **Fix (PostgreSQL):** Se modificó el script `02_floreria_roles_usuarios.sql` trasladando las tablas `users` y `user_roles` al bloque `CRUD`. Esto soluciona el `error 500: 42501 permission denied`.
- **Fix (PostgreSQL):** Se añadió la tabla `auth_tokens` al script `01_floreria_schema.sql` (y sus permisos en el script de roles) ya que había sido omitida originalmente pero es requerida por los tokens JWT (RefreshToken y PasswordRestore).


### 🚧 Problemas / Blockers
- N/A

### 📅 Próximos Pasos (Next Steps)
- Implementar validaciones completas estáticas de la "Receta" en creación de Órdenes (`/api/orders`).
- Implementar envío de correo funcional en `/api/auth/password/forgot` (`IEmailService`).

---
## [2026-04-03] - Sesión Actual

### 🚀 Cambios y Tareas Realizadas (Claude/Gemini)
- Revisión general del estado del espacio de trabajo.
- Verificación e identificación de 12 skills instaladas en el sistema local (`.claude/agents/skills`).
- Estandarización de reglas para Claude (modificación y ampliación del archivo `.clauderules,`).
- Creación de `.geminirules` para mantener coherencia en agentes proactivos.
- Creación de este archivo base de bitácora (`bitacora.md`) para el registro futuro de las modificaciones del proyecto.
<!-- Reemplazar/Añadir aquí abajo los detalles exactos del código modificado por Claude de ser necesario -->
- *[Claude: Añadir los endpoints o lógica desarrollada hoy...]*

### 🚧 Problemas / Blockers
- N/A

### 📅 Próximos Pasos (Next Steps)
- Emplear activamente la skill de `bitacora-floreria` en adelante.
- Confirmar que los *Conventional Commits* automáticos están funcionando.

---
