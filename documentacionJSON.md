# Documentación de Endpoints del Catálogo y Administración

Esta documentación describe cómo consumir los endpoints en el backend agrupados por su nivel de acceso (Rol), y categorizados por método HTTP y función.

---

# 🌍 1. PÚBLICO (Sin autenticación)

## GET

### 1.1. Categorías (Filtros)
**Endpoint:** `GET /api/categories`
**Descripción:** Retorna una lista de todas las categorías activas que se pueden usar para los filtros públicos.
**Respuesta Exitosa (200 OK):**
```json
[
  {
    "id": "e30c4e72-6a84-4d89-9828-569b9b47d1fc",
    "nombre": "Ramos",
    "descripcion": "Ramos de flores naturales"
  }
]
```

### 1.2. Colecciones (Festividades/Temáticas)
**Endpoint:** `GET /api/collections`
**Descripción:** Retorna una lista de colecciones activas, típicamente festividades o temáticas especiales.
**Respuesta Exitosa (200 OK):**
```json
[
  {
    "id": "2bcbd7f1-7c98-4c07-b27e-851a0dc0bd1e",
    "nombre": "Día de las Madres",
    "descripcion": "Arreglos especiales para el 10 de Mayo"
  }
]
```

### 1.3. CMS (Información de la Tienda)
**Endpoint:** `GET /api/store/info`
**Descripción:** Retorna la información general de contacto y ubicación de la tienda.
**Respuesta Exitosa (200 OK):**
```json
{
  "name": "Florería Bautista",
  "phone": "+52 123 456 7890",
  "businessHours": "Lunes a Domingo 9:00 am - 8:00 pm"
}
```

**Ejemplo de uso (Frontend):**
```javascript
const storeResponse = await fetch('/api/store/info');
const storeInfo = await storeResponse.json();
```

---

# 🔐 2. PRIVADO / EMPLEADO (Requiere Token)

## GET

### 2.1. Inventario (Alertas)
**Endpoint:** `GET /api/admin/inventory/alerts`
**Descripción:** Obtiene los insumos cuyo stock está en estado de alarma (Stock Actual <= Stock Minimo).
**Ejemplo de Petición:**
```javascript
const res = await fetch('/api/admin/inventory/alerts?sucursal=PRINCIPAL', {
  headers: { 'Authorization': 'Bearer TU_TOKEN_AQUI' }
});
```

---

# 👑 3. ADMINISTRADOR (Requiere Token ADMIN)

## GET

### 3.1. Recetas (Costo Referencial)
**Endpoint:** `GET /api/admin/recipes/{productId}/suggested-price`
**Descripción:** Realiza un barrido en base a la receta del producto y sus insumos que sumen costo y aplica margen referencial.
**Respuesta (200 OK):**
```json
{
  "productId": "99bbcf00-e22b-41c4-a100-334455883390",
  "costoMateriales": 75.0,
  "precioSugerido": 112.5
}
```

## POST

### 3.2. Categorías (Creación/Modificación)
**Endpoint:** `POST /api/admin/categories`
**Descripción:** Crea una nueva categoría o actualiza una existente (si se envía su `id`).
**Body (JSON):**
```json
{
  "nombre": "Novedades",
  "estado": "ACTIVA"
}
```

### 3.3. Colecciones (Creación/Modificación)
**Endpoint:** `POST /api/admin/collections`
**Descripción:** Crea una nueva colección o actualiza una existente.
**Body (JSON):**
```json
{
  "nombre": "Primavera 2026",
  "estado": "ACTIVA"
}
```

### 3.4. Complementos (Opción de Personalización)
**Endpoint:** `POST /api/admin/customization-options`
**Descripción:** Agrega complementos para acompañar el producto (Globos, etc).
**Body (JSON):**
```json
{
  "clave": "GLOBO_HELIO",
  "nombre": "Globo Metálico con Helio",
  "tipo": "BOOLEANO"
}
```


### 3.6. Recetas (Explosión de Materiales)
**Endpoint:** `POST /api/admin/recipes/{productId}`
**Descripción:** Actualiza la receta de configuración de insumos requeridos (elimina previos y aplica nuevos).
**Body (JSON):**
```json
[
  {
    "inventoryItemId": "b50f7500-a29d-40e5-a330-996655440001",
    "cantidadRequerida": 12
  }
]
```

### 3.7. CMS (Guardar Configuración)
**Endpoint:** `POST /api/admin/cms`
**Descripción:** Guarda las variables de texto y contacto para la web pública en el Administrador.
**Body (JSON):**
```json
{
  "direccion": "Av. Principal 123",
  "horario_semanal": "8 AM - 6 PM"
}
```
