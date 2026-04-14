# Endpoints — FloreriaBautista Backend

> Análisis actualizado. Este documento consolida exactamente los **94 endpoints** definidos en la documentación oficial de requerimientos (`02_Requisitos...` y `documentacion_api.md`), más una pequeña sección al final de endpoints extras/obsoletos que existen actualmente en el backend.
> Leyenda estado: ✅ Funciona en backend | ⚠️ Funcional pero requiere ajustes o testing de validación | ❌ Pendiente (No desarrollado)

---

## 1. Autenticación y Sesión

| Estado | Método | Ruta | Notas |
|--------|--------|------|-------|
| ✅ | POST | `/api/auth/register` | Registro exitoso, asigna rol inicial |
| ✅ | POST | `/api/auth/login` | Funcional con JWT y BCrypt |
| ❌ | POST | `/api/auth/oauth/{provider}` | Placeholder, retorna 501 Not Implemented |
| ✅ | POST | `/api/auth/token/refresh` | Rotación de token correcta |
| ✅ | POST | `/api/auth/logout` | Finaliza sesión |
| ✅ | POST | `/api/auth/logout/all` | Invalida todas las sesiones de la cuenta |
| ⚠️ | POST | `/api/auth/password/forgot` | Retorna 200 OK, falta integrar `IEmailService` |
| ✅ | POST | `/api/auth/password/reset` | Reseteo funcional usando token |
| ✅ | GET | `/api/users/me` | Retorna perfil del usuario usando token |
| ✅ | POST | `/api/users/me` | Actualiza perfil correctamente |

---

## 2. Clientes y Direcciones

| Estado | Método | Ruta | Notas |
|--------|--------|------|-------|
| ✅ | GET | `/api/customers/search` | Búsqueda paginada por nombre, teléfono o correo (ADMIN/EMPLEADO) |
| ✅ | POST | `/api/customers/physical` | Alta rápida cliente mostrador, tipo MOSTRADOR (ADMIN/EMPLEADO) |
| ✅ | GET | `/api/customers/{customerId}/orders` | Historial de pedidos paginado del cliente (ADMIN/EMPLEADO) |
| ✅ | GET | `/api/customers/me/addresses` | Lista direcciones guardadas del cliente logueado |
| ✅ | POST | `/api/customers/me/addresses` | Guarda nueva dirección para el cliente logueado |
| ✅ | POST | `/api/customers/me/addresses/{addressId}` | Actualiza dirección existente del cliente logueado |
| ✅ | POST | `/api/customers/me/addresses/{addressId}/deactivate` | Elimina dirección del cliente logueado |
| ✅ | GET | `/api/customers/me/addresses/suggestions` | Sugerencias combinadas de direcciones guardadas + historial de pedidos |

---

## 3. Catálogo Público

| Estado | Método | Ruta | Notas |
|--------|--------|------|-------|
| ✅ | GET | `/api/products` | Paginación y visualización pública ok |
| ✅ | GET | `/api/products/{productId}` | Detalle de producto |
| ✅ | GET | `/api/categories` | Retorna categorías activas |
| ✅ | GET | `/api/collections` | Retorna colecciones (Festividades/Temáticas) |
| ✅ | GET | `/api/store/info` | Retorna CMS info de la tienda |

---

## 4. Catálogo Interno

| Estado | Método | Ruta | Notas |
|--------|--------|------|-------|
| ✅ | GET | `/api/admin/products` | Catálogo de administración operativo |
| ✅ | POST | `/api/admin/products` | Revisión ok. Crea relaciones complejas (imágenes/categorías). |
| ✅ | POST | `/api/admin/products/{productId}` | Revisión ok. Edición con validación grupal. |
| ✅ | GET | `/api/admin/categories` | Obtiene/gestiona el modelo de categorías usando GET y BD |
| ✅ | POST | `/api/admin/categories` | Crear/modificar categorías operando el modelo en BD |
| ✅ | POST | `/api/admin/collections` | Crear/modificar colecciones operando el modelo en BD |
| ✅ | POST | `/api/admin/customization-options` | Guardar complementos de ticket (ej. Listones, Globos) |
| ✅ | POST | `/api/admin/cms` | Endpoint listo para recibir parámetros CMS |

---

## 5. Pedidos

| Estado | Método | Ruta | Notas |
|--------|--------|------|-------|
| ⚠️ | POST | `/api/orders` | Creado, validar deducciones de inventario estricto |
| ✅ | GET | `/api/orders/my` | Consulta cliente loggeado |
| ✅ | GET | `/api/orders/{orderId}` | Detalle para cliente |
| ⚠️ | POST | `/api/orders/physical` | Ventas físicas con detalle en mostrador |
| ❌ | POST | `/api/orders/quick-sale` | Venta rápida efímera en mostrador |
| ✅ | GET | `/api/admin/orders` | Bandeja central de pedidos |
| ✅ | GET | `/api/admin/orders/{orderId}` | Detalle general para equipo interno |
| ⚠️ | POST | `/api/orders/{orderId}/status` | Falta restringir pasos ilógicos en flujo de estado |
| ❌ | POST | `/api/orders/{orderId}/report-error` | Anulaciones del POS |
| ❌ | POST | `/api/admin/orders/{orderId}/resolve-cancellation` | Confirmar por supervisor |

---

## 6. Pagos

| Estado | Método | Ruta | Notas |
|--------|--------|------|-------|
| ❌ | POST | `/api/orders/{orderId}/payments` | Registrar anticipos o abonos |
| ❌ | GET | `/api/orders/{orderId}/payments` | Historial de pagos del pedido |
| ❌ | GET | `/api/admin/cash/denominations` | Cálculo de cambio automático POS |

---

## 7. Entregas

| Estado | Método | Ruta | Notas |
|--------|--------|------|-------|
| ❌ | GET | `/api/admin/deliveries` | Cargas de trabajo de repartidor |
| ❌ | GET | `/api/admin/deliveries/{deliveryId}` | Detalle de ruta |
| ❌ | POST | `/api/admin/deliveries/{deliveryId}/assign` | Asignar pedido a chofer |
| ❌ | POST | `/api/admin/deliveries/{deliveryId}/status` | Cambiar EN RUTA / ENTREGADO |
| ❌ | POST | `/api/admin/deliveries/{deliveryId}/evidence` | Subir foto de comprobación |
| ❌ | GET | `/api/shipping/calculate` | Cálculo Maps/Distancia tarifa de flete |

---

## 8. Inventario y Recetas

| Estado | Método | Ruta | Notas |
|--------|--------|------|-------|
| ✅ | GET | `/api/admin/inventory` | Funcional |
| ✅ | GET | `/api/admin/inventory/{itemId}` | Funcional, ID mapeado localmente |
| ✅ | POST | `/api/admin/inventory` | Alta manual de insumos base utilizando CreateInventoryItemDto |
| ✅ | POST | `/api/admin/inventory/{itemId}` | Edición de inusmos base utilizando UpdateInventoryItemDto |
| ✅ | POST | `/api/admin/inventory/movements` | Implementado, con guardado trazable en tabla de Movimientos DB |
| ✅ | GET | `/api/admin/inventory/movements` | Historial de movimientos stock ok |
| ✅ | GET | `/api/admin/inventory/alerts` | Listado de alertas bajo mínimo mapeado localmente en API |
| ✅ | GET | `/api/admin/recipes` | Listado integral de ProductRecipes |
| ✅ | POST | `/api/admin/recipes/{productId}` | Crear explosión de insumos limpiando las previas |
| ✅ | GET | `/api/admin/recipes/{productId}/suggested-price` | Cálculo referencial en base referencial a insumos que sumen al costo |

---

## 9. Notificaciones

| Estado | Método | Ruta | Notas |
|--------|--------|------|-------|
| ❌ | GET | `/api/admin/notifications` | Preferencias panel |
| ❌ | GET | `/api/admin/notifications/logs` | Log de envío WhatsApp/Emails histórico |

---

## 10. Reportes

| Estado | Método | Ruta | Notas |
|--------|--------|------|-------|
| ✅ | GET | `/api/admin/reports/sales` | Fechas y periodos |
| ✅ | GET | `/api/admin/reports/top-products` | Top vendidos |
| ✅ | GET | `/api/admin/reports/top-customers` | Top compradores |
| ✅ | GET | `/api/admin/reports/inventory` | Insumos estadísticos |

---

## 11. Usuarios Internos

| Estado | Método | Ruta | Notas |
|--------|--------|------|-------|
| ✅ | GET | `/api/admin/users` | Funcional y paginado |
| ✅ | POST | `/api/admin/users` | Cifrado validado |
| ✅ | GET | `/api/admin/users/{userId}` | (En el req base figuraba POST, adaptamos por convención a GET) |
| ✅ | POST | `/api/admin/users/{userId}/roles` | Funcional |
| ✅ | POST | `/api/admin/users/{userId}/status` | Activa/Desactiva cuenta |

---

## 12. Operación Técnica

| Estado | Método | Ruta | Notas |
|--------|--------|------|-------|
| ✅ | GET | `/api/admin/backups` | Histórico listado |
| ✅ | POST | `/api/admin/backups` | Ejecuta dump completo (`/api/admin/backups/full`) |
| ✅ | GET | `/api/admin/backups/drive` | Explora nube |
| ✅ | POST | `/api/admin/backups/restore` | (`/api/admin/database/restaurar`) Operativo |
| ✅ | GET | `/api/admin/import-jobs` | Archivos procesados históricamente |
| ⚠️ | POST | `/api/admin/import/products` | Funciona el parse básico, falta blindar errores CSV |
| ⚠️ | POST | `/api/admin/import/inventory` | Igual que exportar productos |
| ✅ | GET | `/api/admin/export/orders` | Retorna archivo |
| ❌ | GET | `/api/admin/export/payments` | Exportar reporte de dineros |
| ✅ | GET | `/api/admin/export/customers` | Retorna csv clientes |
| ✅ | GET | `/api/admin/export/inventory` | Retorna csv histórico existencias |
| ✅ | GET | `/api/admin/audit/logs` | (`/api/admin/audit`) Tracker general DB operativos |
| ✅ | GET | `/api/admin/database/health` | PostgreSQL estado puro |
| ✅ | GET | `/api/admin/database/monitor` | Uso analítico interno tablas/caché |
| ✅ | POST | `/api/admin/database/maintenance` | (`/api/admin/database/mantenimiento`) VACUUM ok |
| ❌ | GET | `/api/admin/settings` | Ajustes globales sistema |
| ❌ | POST | `/api/admin/settings` | Guardar ajustes globales |
| ✅ | GET | `/api/admin/health` | Uptime del container .NET general |

---

## 13. Offline / Sincronización (PWA)

| Estado | Método | Ruta | Notas |
|--------|--------|------|-------|
| ❌ | GET | `/api/pos/catalog-snapshot` | Descarga masiva productos JSON para tablet mode |
| ❌ | POST | `/api/pos/sync` | Subida en batch de tickets web generados desconectado |

---

## 14. Plantillas de Temporada (POS)

| Estado | Método | Ruta | Notas |
|--------|--------|------|-------|
| ❌ | GET | `/api/admin/sale-templates` | Configuraciones rápidas para el 10 Mayo / 14 Feb |
| ❌ | POST | `/api/admin/sale-templates` | Ajustar panel de botones mostrador |
| ❌ | POST | `/api/admin/sale-templates/{templateId}/status` | Prender/Apagar de la tablet los botones rápidos |

---

## Extras / Obsoletos Actualmente en Backend

*Endpoints que existen pero no aplican dentro de los 94 requerimientos principales marcados en el documento de funcionalidad general.*

| Estado | Método | Ruta | Notas |
|--------|--------|------|-------|
| ✅ | GET | `/api/admin/scheduler` | Trabajos de fondo (ej. backups programados) |
| ✅ | POST | `/api/admin/scheduler` | - |
| ✅ | GET | `/api/dev/token` | Fast-pass de Dev environment sin credenciales |
| ✅ | GET | `/api/admin/export/products` | Descarga master de catálogo (Pudiera absorberse en `export/inventory`) |
| ✅ | GET | `/api/admin/backups/tablas` | Ver granularmente qué es respaldable |
| ✅ | POST | `/api/admin/backups/tabla` | Backup atómico por tabla |
| ✅ | GET | `/api/admin/audit/{entidad}/{entidadId}` | Visión tipo "Historial de modificaciones de este pedido" |
| ⚠️ | Varios | `/api/admin/flowers/...` | Modulo obsoleto de 'flowercatalog'. Remplazado. (10 endpoints) |

---

## Resumen Final Oficial (Base = 94 Endpoints)

| Categoría | ✅ | ⚠️ | ❌ | Subtotal |
|-----------|----|----|-----|----------|
| 1. Autenticación | 8 | 1 | 1 | 10 |
| 2. Clientes | 8 | 0 | 0 | 8 |
| 3. Catálogo Púb. | 2 | 0 | 3 | 5 |
| 4. Catálogo Int. | 1 | 2 | 5 | 8 |
| 5. Pedidos | 4 | 3 | 3 | 10 |
| 6. Pagos | 0 | 0 | 3 | 3 |
| 7. Entregas | 0 | 0 | 6 | 6 |
| 8. Inventario | 2 | 2 | 6 | 10 |
| 9. Notificaciones | 0 | 0 | 2 | 2 |
| 10. Reportes | 4 | 0 | 0 | 4 |
| 11. Usuarios Int. | 5 | 0 | 0 | 5 |
| 12. Operación Téc.| 13 | 2 | 3 | 18 |
| 13. PWA/Offline | 0 | 0 | 2 | 2 |
| 14. Plantillas | 0 | 0 | 3 | 3 |
| **Totales** | **39** | **10** | **45** | **94** |
