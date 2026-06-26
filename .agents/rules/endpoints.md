---
trigger: always_on
---

### REGLAS ESTRICTAS DE CRUD, MÉTODOS HTTP 
**1. RESTRICCIÓN DE MÉTODOS HTTP**
- Tienes ESTRICTAMENTE PROHIBIDO generar código, sugerir o utilizar métodos como `PUT`, `PATCH`, `DELETE` u `OPTIONS`.
- Se usarán EXCLUSIVAMENTE dos métodos:
  - `GET`: Únicamente para consultas y lectura de datos.
  - `POST`: Para cualquier operación que implique creación o modificación de estado en la base de datos.

**2. ESTRUCTURA DE ENDPOINTS (ENFOQUE UNIFICADO)**
El diseño del CRUD debe seguir esta estructura exacta:
- **Crear:** `POST /api/[modulo]/[recurso]`
- **Leer (Colección):** `GET /api/[modulo]/[recurso]`
- **Leer (Individual):** `GET /api/[modulo]/[recurso]/{id}`
- **Actualizar y Eliminar:** `POST /api/[modulo]/[recurso]/{id}`

**3. LÓGICA DE NEGOCIO: ACTUALIZACIÓN PARCIAL Y BORRADO LÓGICO**
- **Cero borrado físico:** NUNCA se deben eliminar registros de la base de datos PostgreSQL.
- **Manejo unificado:** El endpoint `POST .../{id}` maneja tanto las actualizaciones regulares como las eliminaciones.
- **Actualización parcial en C#:** El controlador debe leer el payload JSON y utilizar Entity Framework Core para modificar únicamente los campos que vengan explícitamente en la petición. 
- **Borrado lógico:** Si el frontend envía un JSON indicando un cambio de estado (ej. `{"activo": false}`), el controlador simplemente procesará esto como una actualización parcial de ese campo específico, logrando así el borrado lógico sin necesidad de rutas adicionales.

## Regla extricta
Todas las modificaciones que hagas tienen que documentarse en la vitacora que esta ../Documentacion/Bitacora_Backend.md