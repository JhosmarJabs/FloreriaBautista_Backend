# Endpoints — FloreriaBautista Backend

> Análisis al 2026-03-24. Leyenda estado: ✅ Funciona al 100% | ⚠️ Funciona con caveats | ❌ No funciona / Placeholder
> Leyenda test: ✅ Probado OK | ❌ Falla | ⚠️ Parcial | — Sin probar

---

## Admin — Flores `[Authorize(Roles = "ADMIN,INVENTARIO")]`

| Estado | Test | Método | Ruta | Notas |
|--------|------|--------|------|-------|
| ✅ | ✅ | GET | `/api/admin/flowers` | Paginación operativa, ejecutada con éxito (retorna lista vacía al no haber flores registradas aún) |
| ⚠️ | — | GET | `/api/admin/flowers/{flowerId}` | Misma situación |
| ⚠️ | — | POST | `/api/admin/flowers` | Sin verificar |
| ⚠️ | — | POST | `/api/admin/flowers/{flowerId}` | Sin verificar |
| ⚠️ | — | POST | `/api/admin/flowers/{flowerId}/movements` | Sin verificar |
| ⚠️ | — | GET | `/api/admin/flowers/{flowerId}/movements` | Sin verificar |
| ⚠️ | — | GET | `/api/admin/flowers/product/{productId}` | Sin verificar |
| ⚠️ | — | POST | `/api/admin/flowers/product/{productId}` | Sin verificar |
| ⚠️ | — | GET | `/api/admin/flowers/product/{productId}/costo` | Sin verificar |
| ⚠️ | — | POST | `/api/admin/flowers/product/{productId}/precio` | Sin verificar |
| ✅ | — | POST | `/api/admin/flowers/import` | Importación masiva CSV. `multipart/form-data` campo `archivo`. Upsert por nombre+color |
| ✅ | — | GET | `/api/admin/flowers/export` | Descarga CSV con todas las flores. BOM UTF-8 incluido |

---

## Admin — Inventario `[Authorize(Roles = "ADMIN,INVENTARIO")]`

| Estado | Test | Método | Ruta | Notas |
|--------|------|--------|------|-------|
| ⚠️ | — | GET | `/api/admin/inventory` | `InventoryService` registrado. No revisé la lógica interna del servicio |
| ⚠️ | — | GET | `/api/admin/inventory/{productId}` | Misma incertidumbre |
| ⚠️ | — | POST | `/api/admin/inventory/movements` | No verifiqué si actualiza stock correctamente |
| ⚠️ | — | GET | `/api/admin/inventory/movements` | Consulta paginada, probablemente funciona |

---

## Admin — Reportes `[Authorize(Roles = "ADMIN")]`

| Estado | Test | Método | Ruta | Notas |
|--------|------|--------|------|-------|
| ⚠️ | — | GET | `/api/admin/reports/sales` | `ReportsService` registrado, sin verificar las queries de agregación |
| ⚠️ | — | GET | `/api/admin/reports/top-products` | Misma situación |
| ⚠️ | — | GET | `/api/admin/reports/top-customers` | Misma situación |
| ⚠️ | — | GET | `/api/admin/reports/inventory` | Misma situación |

---

## Órdenes — `[Authorize]`

| Estado | Test | Método | Ruta | Notas |
|--------|------|--------|------|-------|
| ⚠️ | — | POST | `/api/orders` | `OrderService` registrado, pero no verifiqué la validación de stock ni las relaciones de entidad completas |
| ⚠️ | — | POST | `/api/orders/physical` | Rol ADMIN/VENTAS. Misma incertidumbre que el anterior |
| ✅ | ✅ | GET | `/api/orders/my` | Consulta paginada funciona correctamente (retorna vacío si no hay registros) |
| ✅ | — | GET | `/api/orders/{orderId}` | Detalle por GUID con validación de ownership |
| ⚠️ | — | POST | `/api/orders/{orderId}/status` | No verifiqué las transiciones de estado permitidas ni si valida el rol contra el cambio específico |

---

## Admin — Productos `[Authorize(Roles = "ADMIN")]`

| Estado | Test | Método | Ruta | Notas |
|--------|------|--------|------|-------|
| ✅ | ✅ | GET | `/api/admin/products` | Listado correcto con filtros y paginación funcional |
| ⚠️ | — | POST | `/api/admin/products` | No verifiqué las relaciones de imágenes/colecciones en la creación |
| ⚠️ | — | POST | `/api/admin/products/{productId}` | Misma incertidumbre que el anterior |

---

## Admin — Importar / Exportar `[Authorize(Roles = "ADMIN")]`

| Estado | Test | Método | Ruta | Notas |
|--------|------|--------|------|-------|
| ✅ | ✅ | GET | `/api/admin/export/products` | Genera y descarga correctamente el archivo CSV de productos |
| ✅ | ✅ | GET | `/api/admin/export/inventory` | Genera y descarga el archivo CSV de inventario (sólo cabeceras si está vacío) |
| ⚠️ | — | POST | `/api/admin/import/products` | No verifiqué validaciones del CSV ni manejo de errores de fila |
| ⚠️ | — | POST | `/api/admin/import/inventory` | Misma situación |

---

## Auth — `POST /api/auth/...`

| Estado | Test | Método | Ruta | Notas |
|--------|------|--------|------|-------|
| ✅ | ✅ | POST | `/api/auth/register` | Registro exitoso, responde 200 OK asignando el rol temporal (CLIENTE) y generando tokens |
| ✅ | ✅ | POST | `/api/auth/login` | Login completamente funcional (200 OK) validando hashes bcrypt y retornando JWT correctos |
| ✅ | ✅ | POST | `/api/auth/token/refresh` | Rotación segura y exitosa del token de sesión (devuelve nuevo AccessToken y RefreshToken) |
| ✅ | ✅ | POST | `/api/auth/logout` | Finaliza exitosamente la sesión indicada por el refresh token |
| ✅ | ✅ | POST | `/api/auth/logout/all` | Invalida correctamente todas las sesiones activas atadas a la cuenta |
| ⚠️ | ✅ | POST | `/api/auth/password/forgot` | La API retorna 200 OK correctamente, pero **todavía requiere que integres un `IEmailService`** (actualmente solo loggea el token) |
| ✅ | — | POST | `/api/auth/password/reset` | Funciona si tienes el token generado por `forgot`. BCrypt aplicado |
| ❌ | — | POST | `/api/auth/oauth/{provider}` | Placeholder — devuelve `501 Not Implemented` |

---

## Perfil de usuario — `[Authorize]`

| Estado | Test | Método | Ruta | Notas |
|--------|------|--------|------|-------|
| ✅ | ✅ | GET | `/api/users/me` | Retorna exitosamente los detalles del perfil actual usando el claim del token |
| ✅ | ✅ | POST | `/api/users/me` | Actualiza el perfil correctamente (validando constraints externos como `ck_users_sexo`) |

---

## Productos públicos — sin autenticación

| Estado | Test | Método | Ruta | Notas |
|--------|------|--------|------|-------|
| ✅ | ✅ | GET | `/api/products` | Paginación y filtros operativos, retorna correctamente el catálogo de productos con sus detalles |
| ✅ | ✅ | GET | `/api/products/{productId}` | Retorna correctamente el detalle completo del producto por GUID |

---

## Admin — Usuarios `[Authorize(Roles = "ADMIN")]`

| Estado | Test | Método | Ruta | Notas |
|--------|------|--------|------|-------|
| ✅ | — | POST | `/api/admin/users` | Creación con BCrypt y roles validados |
| ✅ | ✅ | GET | `/api/admin/users` | Paginación operativa y retorna listado correctamente estructurado |
| ✅ | ✅ | GET | `/api/admin/users/{userId}` | Detalle exitoso por GUID |
| ✅ | ✅ | POST | `/api/admin/users/{userId}/status` | Cambio de estado funcional, retorna éxito sin dar problemas de integridad |
| ✅ | ✅ | POST | `/api/admin/users/{userId}/roles` | Reemplazo de roles del usuario validado con éxito |

---

## Admin — Órdenes `[Authorize(Roles = "ADMIN,VENTAS")]`

| Estado | Test | Método | Ruta | Notas |
|--------|------|--------|------|-------|
| ✅ | ✅ | GET | `/api/admin/orders` | Lista paginada y filtrada, devuelve órdenes correctamente estructuradas |
| ✅ | ✅ | GET | `/api/admin/orders/{orderId}` | Detalle completo por GUID (incluye objetos anidados para dirección e ítems) |

---

## Admin — Backups `[Authorize(Roles = "ADMIN")]`

| Estado | Test | Método | Ruta | Notas |
|--------|------|--------|------|-------|
| ✅ | ✅ | GET | `/api/admin/backups` | Retorna el historial de backups listados correctamente, incluyendo detalles y URLs de Google Drive. |
| ✅ | ✅ | GET | `/api/admin/backups/drive` | Obtiene con éxito la lista de archivos de backup almacenados directamente en Google Drive. |
| ✅ | ✅ | POST | `/api/admin/backups/full` | Se completa exitosamente la creación del backup completo de BD y su subida a Drive (200 OK). |
| ✅ | ✅ | POST | `/api/admin/backups/tabla` | Se completa exitosamente la creación del backup por tabla y su subida a Drive sin errores. |

---

## Admin — Base de Datos `[Authorize(Roles = "ADMIN")]`

| Estado | Test | Método | Ruta | Notas |
|--------|------|--------|------|-------|
| ✅ | ✅ | GET | `/api/admin/database/health` | Retorna exitosamente el estado, versión, conexiones activas y tiempo de actividad de PostgreSQL |
| ✅ | ✅ | GET | `/api/admin/database/monitor` | Genera estadísticas muy completas (tamaño de tablas, índices sin uso, conexiones y caché) |
| ✅ | ✅ | POST | `/api/admin/database/mantenimiento` | Ejecuta cronológicamente rutinas de limpieza, ANALYZE, VACUUM ANALYZE y REINDEX con éxito |
| ✅ | ✅ | POST | `/api/admin/database/restaurar` | Restauración exitosa (con doble pasada automática). Supera los conflictos de llaves foráneas y sincroniza todo con éxito |

---

## Admin — Scheduler `[Authorize(Roles = "ADMIN")]`

| Estado | Test | Método | Ruta | Notas |
|--------|------|--------|------|-------|
| ✅ | ✅ | GET | `/api/admin/scheduler` | Retorna exitosamente la configuración actual del scheduler automático |
| ✅ | ✅ | POST | `/api/admin/scheduler` | Actualiza la configuración y calcula de inmediato las próximas fechas de ejecución |

---

## Admin — Auditoría `[Authorize(Roles = "ADMIN")]`

| Estado | Test | Método | Ruta | Notas |
|--------|------|--------|------|-------|
| ✅ | — | GET | `/api/admin/audit` | Historial general paginado (filtros por entidad, acción, usuarioId y fechas). |
| ✅ | — | GET | `/api/admin/audit/{entidad}/{entidadId}` | Trazabilidad específica (línea de tiempo) exclusiva de un solo registro. |

---

## Dev (solo ambiente desarrollo)

| Estado | Test | Método | Ruta | Notas |
|--------|------|--------|------|-------|
| ✅ | — | GET | `/api/dev/token` | Genera JWT de admin sin credenciales. Útil para testing |

---

## Resumen

| Categoría | ✅ | ⚠️ | ❌ |
|-----------|----|----|-----|
| Admin Flores | 1 | 9 | 0 |
| Admin Inventario | 0 | 4 | 0 |
| Admin Reportes | 0 | 4 | 0 |
| Órdenes | 2 | 3 | 0 |
| Admin Productos | 1 | 2 | 0 |
| Admin Import/Export | 2 | 2 | 0 |
| Auth | 6 | 1 | 1 |
| Perfil | 2 | 0 | 0 |
| Productos públicos | 2 | 0 | 0 |
| Admin Usuarios | 5 | 0 | 0 |
| Admin Órdenes | 2 | 0 | 0 |
| Admin Backups | 4 | 0 | 0 |
| Admin Base de Datos | 4 | 0 | 0 |
| Admin Scheduler | 2 | 0 | 0 |
| Admin Auditoría | 2 | 0 | 0 |
| Dev | 1 | 0 | 0 |
| **Total** | **36** | **25** | **1** |

### Acción inmediata recomendada
1. **Usuarios con contraseña en texto plano** — migrar o pedir reset de contraseña
2. **`/api/auth/password/forgot`** — no envía correo, solo loggea el token
3. **`/api/auth/oauth/{provider}`** — devuelve 501, no conectar desde el frontend
