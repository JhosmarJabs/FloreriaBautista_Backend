# Entorno `play_review` (revisión de Google Play)

Google Play necesita entrar a la app móvil con una cuenta de prueba. Este entorno le da
acceso a **toda** la app con **datos 100 % sintéticos**, sin tocar producción y sin
cambiar la URL que usan la app móvil y la web.

## 1. Cómo funciona

```
App móvil / web ──HTTPS──► PROD (Render, el servicio de siempre)
                            │
                            ├─ petición normal ──► pipeline de siempre ──► Neon PROD
                            │
                            └─ petición de revisión ──(YARP, firmado)──► REVIEW (Render, servicio nuevo)
                                                                          └─► Neon REVIEW (rama schema-only)
```

- **Mismo código y misma imagen**, desplegados dos veces.
- En PROD, `PlayReviewEdgeRouter` es el **primer middleware** (antes de Datadog,
  Exception, RequestLogging, Rasp, Alexa y la autenticación). Si la petición es de
  revisión la reenvía a REVIEW y termina: PROD no la autentica, no la registra con datos
  y no toca su base. Se puso antes de Datadog y RequestLogging a propósito, para que en
  PROD no quede ni la traza con la URL ni el log de una petición de revisión.
- Una petición es de revisión **solo** por datos que emite el servidor:
  1. login/registro/recuperación con un correo que termina en alguno de los dominios de
     `REVIEW_LOGIN_EMAIL_DOMAIN` (lista separada por comas; por defecto `@playreview.invalid`);
  2. un JWT (Authorization Bearer, o `access_token` en `/hubs`) cuyo `iss` es
     `REVIEW_JWT_ISSUER` (solo se lee el payload; la firma la valida REVIEW);
  3. un refresh token que empieza con `rv_` (REVIEW los emite así).
  Cualquier header del cliente para "elegir entorno" se ignora.
- PROD firma cada reenvío con el header `X-Play-Review-Edge`
  (`ts.HMAC-SHA256(REVIEW_EDGE_SECRET, ts\nMÉTODO\nruta)`, base64url). REVIEW rechaza
  con 403 todo lo que no traiga una firma válida de menos de 300 s, salvo `GET /api/ping`.
- REVIEW (`APP_ENTORNO=play_review`):
  - bloquea con 403 (mensaje en español e inglés) respaldos, restauración, mantenimiento
    de base, scheduler, WhatsApp, alta/edición de usuarios y responsable de turno,
    importación/exportación, Mercado Pago, Alexa, `DevController` y el webhook interno
    de WhatsApp (atributo `[BloqueadoEnRevision]`);
  - apaga WhatsApp (notificador no-op, sin monitor ni despachador), el push de Expo
    (no llama a `exp.host`; SignalR sí funciona, es interno a REVIEW), los respaldos
    programados y el middleware de Alexa;
  - **Mercado Pago se rechaza** (403 con mensaje claro) en vez de simularse: es lo más
    seguro y la app móvil no lo usa (solo el checkout web);
  - los modelos predictivos se quedan: trabajan sobre la base de revisión y, si existe
    `ML_SERVICE_URL`, solo mandan agregados sintéticos al sidecar;
  - registra cada petición con el prefijo `[play_review]` (usuario, método, ruta sin
    query, estado, IP de `X-Forwarded-For`, user-agent);
  - **no arranca** si está en `Development`, si falta `REVIEW_EDGE_SECRET` o si tiene
    alguna variable de producción (lista en la sección 2.3).
- Sin las variables nuevas, PROD se comporta exactamente igual que antes.

## 2. Variables de entorno

Los valores secretos están en `D:\temporal\play-review\credenciales-revision.local.json`
(fuera del repositorio). **Nunca** se copian a archivos versionados.

### 2.1 PROD (servicio actual): agregar

| Variable | Valor |
|---|---|
| `REVIEW_UPSTREAM_URL` | Origen del servicio REVIEW, sin ruta: `https://<servicio-review>.onrender.com` |
| `REVIEW_EDGE_SECRET` | `floreria.render_prod.REVIEW_EDGE_SECRET` del JSON (≥ 32 caracteres; **igual** al de REVIEW) |
| `REVIEW_JWT_ISSUER` | El `JWT_ISSUER` de REVIEW: `floreria-play-review` (debe ser distinto del `JWT_ISSUER` de PROD) |
| `REVIEW_LOGIN_EMAIL_DOMAIN` | Opcional. Uno o varios dominios separados por coma. Por defecto `@playreview.invalid`. Con testers de Google Play: `@playreview.invalid,@test.com` (sección 9) |

El router solo se activa con `REVIEW_UPSTREAM_URL` y `REVIEW_EDGE_SECRET`. Si algo está
mal (secreto corto, URL con ruta, falta el issuer, issuer igual al de PROD) queda
**inactivo** y el log de arranque dice por qué: PROD nunca se cae por esto.
Todas las variables actuales de PROD se quedan igual.

### 2.2 REVIEW (servicio nuevo): todas las que necesita

| Variable | Valor |
|---|---|
| `APP_ENTORNO` | `play_review` |
| `ASPNETCORE_ENVIRONMENT` | `Production` (nunca `Development`) |
| `REVIEW_EDGE_SECRET` | `floreria.render_review.REVIEW_EDGE_SECRET` del JSON (el mismo valor que en PROD) |
| `JWT_KEY` | `floreria.render_review.JWT_KEY` del JSON (llave propia, distinta de PROD) |
| `JWT_ISSUER` | `floreria.render_review.JWT_ISSUER` (`floreria-play-review`) |
| `JWT_AUDIENCE` | `floreria.render_review.JWT_AUDIENCE` (`floreria-play-review-app`) |
| `JWT_ACCESS_EXPIRATION_MINUTES` | Opcional (60 por defecto) |
| `JWT_REFRESH_EXPIRATION_DAYS` | Opcional (365 por defecto: las cuentas no caducan) |
| `DB_HOST` | Host de la rama de revisión en Neon |
| `DB_PORT` | `5432` |
| `DB_NAME` | Base de la rama de revisión |
| `DB_USER` | `floreria_review` (rol propio, sección 3) |
| `DB_PASSWORD` | Contraseña de `floreria_review` |
| `STORE_TIMEZONE` | Opcional (`America/Mexico_City` por defecto) |
| `ML_SERVICE_URL` | Opcional. Sin ella, la predicción de surtido responde "no disponible" |
| `DD_ENV` | Opcional, solo si se usa Datadog: `play_review` |

### 2.3 Prohibidas en REVIEW (si existen, REVIEW no arranca)

`MERCADOPAGO_ACCESS_TOKEN`, `EVOLUTION_API_URL`, `EVOLUTION_API_KEY`,
`EVOLUTION_INSTANCE_NAME`, `WHATSAPP_WEBHOOK_TOKEN`, `GOOGLE_CLIENT_ID`,
`GOOGLE_CLIENT_SECRET`, `GOOGLE_DRIVE_FOLDER_ID`, `GOOGLE_TOKEN_PATH`,
`CLOUDINARY_API_KEY`, `CLOUDINARY_API_SECRET`, `ALEXA_API_KEY`, `BACKUP_DB_USER`,
`BACKUP_DB_PASSWORD`, `DB_ADMIN_USER`, `DB_ADMIN_PASSWORD`, `REVIEW_UPSTREAM_URL`.

Tampoco copies `FRONTEND_URL`, `BACKEND_PUBLIC_URL`, `WHATSAPP_WEBHOOK_URL_INTERNA` ni
`BACKUP_*`/`MANT_*`: no hacen falta.

## 3. Base de datos (Neon)

1. En el proyecto de Neon, **Branches → New branch** desde `main`, con la opción
   **Schema only** (sin datos). Nómbrala, por ejemplo, `play-review`.
   - Si la rama se crea con datos, la siembra **aborta** (guarda 3: hay usuarios y no
     existe `_entorno = 'play_review'`). Bórrala y créala otra vez como schema-only.
   - Alternativa: una base vacía. La herramienta crea el esquema desde el modelo EF.
2. Neon copia a la rama **los roles de `main` con sus contraseñas**. Por eso REVIEW usa
   un rol propio, que solo existe en la rama de revisión. En el **SQL Editor** de Neon,
   con la **rama `play-review` seleccionada** (verifícalo arriba) y su base:

   ```sql
   -- Rol propio de la instancia de revisión. Contraseña larga y aleatoria (gestor de contraseñas).
   CREATE ROLE floreria_review WITH LOGIN PASSWORD '<contraseña-larga-aleatoria>';

   GRANT CONNECT ON DATABASE <nombre_bd> TO floreria_review;
   -- CREATE solo hace falta si la base está vacía (la siembra crea tablas y la tabla _entorno).
   GRANT USAGE, CREATE ON SCHEMA public TO floreria_review;
   GRANT SELECT, INSERT, UPDATE, DELETE, TRUNCATE ON ALL TABLES IN SCHEMA public TO floreria_review;
   GRANT USAGE, SELECT, UPDATE ON ALL SEQUENCES IN SCHEMA public TO floreria_review;
   -- Para tablas futuras creadas por el dueño de la rama:
   ALTER DEFAULT PRIVILEGES FOR ROLE neondb_owner IN SCHEMA public
     GRANT SELECT, INSERT, UPDATE, DELETE, TRUNCATE ON TABLES TO floreria_review;
   ALTER DEFAULT PRIVILEGES FOR ROLE neondb_owner IN SCHEMA public
     GRANT USAGE, SELECT, UPDATE ON SEQUENCES TO floreria_review;
   ```

   Sin superusuario, sin `CREATEDB`/`CREATEROLE`, sin acceso a otras bases. Cambia
   `neondb_owner` si el dueño de las tablas en tu proyecto se llama distinto.
3. Recomendado: en la rama `play-review`, **Roles → Reset password** de los roles
   copiados de `main` (`neondb_owner` y los de la app). Neon guarda los roles por rama;
   el cambio solo afecta a la rama seleccionada. **[VERIFICAR]** en la consola que la
   rama de arriba sea `play-review` antes de restablecer.
4. La cadena de conexión para REVIEW y para la siembra es la de la rama `play-review`
   con el rol `floreria_review`.

**Esquema.** Las migraciones EF no están versionadas (el esquema real se mantiene con SQL
a mano), así que la herramienta hace esto:
- **Base sin tablas:** ejecuta `GenerateCreateScript()` del mismo `AppDbContext` que usa la
  app (tablas, índices, CHECK y filas semilla del modelo).
- **Base con tablas (rama schema-only):** las usa tal cual (es el esquema real de PROD) y
  antes **verifica** que existan todas las tablas y columnas del modelo de este código.
  Si falta alguna, aborta sin escribir y lista lo que falta: aplica en la rama de revisión
  los SQL pendientes (los mismos que se aplicaron o se aplicarán en PROD) o usa una base vacía.
- REVIEW debe desplegar **la misma rama que PROD**, para que código y esquema coincidan.

## 4. Siembra (`tools/PlayReviewSeed`)

En PowerShell, desde la carpeta del backend (la conexión sale **solo** de
`REVIEW_DB_URL`; la herramienta no lee `.env`, `DB_*` ni `DefaultConnection`):

```powershell
Set-Location "D:\FloreriaBautista_Dev\Backend"
$env:REVIEW_DB_URL = "<cadena de la rama play-review con el rol floreria_review>"
$env:PROD_DB_HOST = "<host de la base de producción>"
dotnet run --project tools/PlayReviewSeed -- --credenciales "D:\temporal\play-review\credenciales-revision.local.json"
```

Después, borra la variable de la sesión:

```powershell
Remove-Item Env:REVIEW_DB_URL
```

- Pide escribir el nombre de la base para confirmar (o `--confirmar <nombre_bd>`).
- Para regenerar todo: agrega `--reiniciar`. Sin esa opción, una base de revisión que ya
  tiene datos no se toca.
- Guardas (si una falla, aborta **antes de escribir**): `REVIEW_DB_URL` definida; destino
  distinto de `PROD_DB_HOST` (se compara sin `-pooler`, antes de conectarse); base con
  usuarios, clientes o pedidos y sin `_entorno = 'play_review'` → aborta; confirmación del
  nombre de la base.
- Todo va en **una transacción**: o queda todo, o nada.
- Qué genera (semilla fija, fechas relativas a hoy en `America/Mexico_City`, ids que
  empiezan con `5eed`): 7 categorías, 5 catálogos de temporada, 22 productos con
  recetas, 25 insumos con costos y cantidades inventadas (4 por debajo del mínimo),
  150 clientes (25 con cuenta web) y sus direcciones, ~1,500 pedidos en los últimos
  120 días + pedidos de hoy + 45 anticipados para los próximos 14 días, pagos, entregas,
  movimientos e historial diario de inventario, órdenes de compra, demanda no cubierta,
  gastos y cortes de caja de 30 días, reportes de error, plantillas de venta rápida,
  historial de ventas instantáneas, solicitudes de evento, ofertas, descuentos,
  promociones con códigos inventados, notificaciones, bitácora, segmentos de clientes,
  contenido legal por defecto y datos de la tienda ficticios.
- Cuentas: las dos del JSON (`floreria.cuentas`): ADMIN y EMPLEADO, `ACTIVO`, correo
  verificado, contraseña con BCrypt. La cuenta EMPLEADO es la **responsable de turno** y
  tiene 7 ventas de mostrador hoy, cobros en efectivo, 2 gastos abiertos y su corte de hoy
  **sin cerrar**, para que "Mi día", "Gastos" y "Corte" del móvil tengan datos. Hay dos
  empleados ficticios más que no pueden iniciar sesión (contraseña aleatoria).
  Además, mientras dure la prueba cerrada, las 15 cuentas de testers `@test.com` y un
  reabasto de existencias (sección 9); `--sin-testers` las omite y borra las que haya.
- `--reiniciar` vacía todas las tablas del modelo **salvo** `users`, `roles`,
  `user_roles`, `auth_tokens` y `push_token_devices`: las cuentas de revisión conservan su
  id y su sesión. Los usuarios que no son del dataset (p. ej. los que registra el robot de
  Google) se borran. Si cambió la contraseña de una cuenta de revisión, sus sesiones se
  revocan.
- El "día" del empleado se calcula al sembrar. Para que no amanezca vacío, resiembra cada
  mañana: workflow `.github/workflows/play-review-resiembra.yml` (apagado hasta crear la
  variable de repositorio `PLAY_REVIEW_RESEED = true` y los secretos que lista el archivo)
  o a mano con `--reiniciar`. Ojo: resembrar borra lo que el revisor haya creado.

## 5. Render

1. **New → Web Service**, el **mismo repositorio, la misma rama y el mismo Dockerfile**
   que PROD. Nombre sugerido: `floreria-review`. Misma región que PROD.
2. Variables: las de la sección 2.2 (y ninguna de la 2.3).
3. **Health Check Path:** `/api/ping` (es la única ruta que REVIEW responde sin firma).
4. Auto-deploy activado en la misma rama que PROD: los dos servicios siempre llevan el
   mismo código.
5. Cuando REVIEW esté arriba, copia su URL (`https://floreria-review.onrender.com`) y en
   PROD agrega las variables de la sección 2.1. Guarda: PROD se redespliega.
6. **Keep-alive:** en el plan gratuito REVIEW se duerme a los 15 min y el primer reenvío
   tardaría ~50 s (el router espera hasta 100 s). El workflow `keep-alive.yml` ya hace ping
   a REVIEW si existe el secreto de repositorio `REVIEW_PING_URL`
   (`https://floreria-review.onrender.com/api/ping`). Alternativa: plan pagado.
7. La URL de REVIEW no se publica en ningún lado; aun así, sin la firma del borde no sirve.

En **Play Console → App content → App access**: "All or some functionality is
restricted", con el correo y la contraseña de la cuenta ADMIN (y la EMPLEADO si quieres que
revisen ese rol), e instrucciones: "Inicia sesión con estas credenciales; los datos son
de demostración".

## 6. Rotación y revocación

| Qué | Cómo | Efecto |
|---|---|---|
| Contraseña de una cuenta de revisión | Cambia `password` en el JSON → siembra con `--reiniciar` → actualiza Play Console | Las sesiones de esa cuenta se revocan |
| Revocar ya una cuenta | En la base de revisión: `UPDATE users SET estado = 'INACTIVO' WHERE correo = '<correo>';` | No puede iniciar sesión ni renovar; su access token vive ≤ 60 min |
| Revocar todo de inmediato | Cambia `JWT_KEY` de REVIEW en Render | Todos los tokens de revisión dejan de valer. PROD no se entera |
| `REVIEW_EDGE_SECRET` | Genera uno nuevo y cámbialo en PROD y REVIEW a la vez | Mientras uno de los dos va atrás, REVIEW responde 403 |
| Contraseña del rol `floreria_review` | `ALTER ROLE floreria_review WITH PASSWORD '<nueva>';` en la rama de revisión → `DB_PASSWORD` en Render | — |
| Apagar el entorno | Borra `REVIEW_UPSTREAM_URL` en PROD | PROD vuelve a ser exactamente el de antes |

Rota después de cada aprobación de Google.

## 7. Checklist de aislamiento (antes de enviar a revisión)

| # | Prueba | Esperado |
|---|---|---|
| 1 | Login con la cuenta de revisión contra la URL de PROD | Token con `iss=floreria-play-review` y refresh `rv_…`; en los logs de PROD solo aparece `[play_review-borde] … reenviada`, sin consultas |
| 2 | Cualquier endpoint con ese token | Solo datos sintéticos (ids `5eed…`) |
| 3 | Token de revisión directo contra un endpoint de PROD saltándose el borde (p. ej. con las variables del router quitadas en una copia local) | 401 |
| 4 | Token de PROD contra REVIEW (por el borde se queda en PROD; directo, con una firma de borde hecha a mano con el secreto) | 401 |
| 5 | Token con `iss=floreria-play-review` y firma falsa contra PROD | PROD lo reenvía; REVIEW responde 401 |
| 6 | Header `X-Env: play_review` (o `X-Play-Review-Edge` inventado) con un usuario normal | Se ignora; la petición sigue en PROD |
| 7 | Llamar directo a la URL de REVIEW sin firma (cualquier ruta salvo `GET /api/ping`) | 403 |
| 8 | 50 peticiones concurrentes intercaladas (mitad revisión, mitad normales) | Ninguna respuesta mezclada (comparar ids) |
| 9 | Desde revisión, crear una venta o aceptar una venta instantánea | Log `[play_review] Push Expo omitido`; ningún WhatsApp; el SignalR de PROD no recibe nada |
| 10 | Desde revisión: respaldos, restauración, scheduler, WhatsApp, exportar CSV, crear usuario o cambiar responsable | 403 con mensaje bilingüe |
| 11 | Pago con Mercado Pago desde revisión | 403 con mensaje bilingüe; sin cargo |
| 12 | Siembra con `REVIEW_DB_URL` apuntando al host de `PROD_DB_HOST` | Aborta antes de conectarse |
| 12b | Siembra contra una rama copiada **con** datos | Aborta (guarda 3) sin escribir |
| 13 | Resembrar dos veces con `--reiniciar` el mismo día | Mismos ids y montos; sin errores de llaves foráneas |
| 14 | Recorrido completo del móvil (viewport de teléfono en el navegador) con ADMIN y con EMPLEADO | Todos los módulos con datos y sin errores |
| 15 | Pre-launch report en el track interno | El robot entra y no hay crashes |
| 16 | Revocar la cuenta (`estado = 'INACTIVO'` o rotar `JWT_KEY` de REVIEW) | El siguiente login o refresh da 401; PROD no se ve afectado |

Las pruebas automatizadas (`dotnet test`) ya cubren: router inactivo sin variables,
detección por login, `iss` y `rv_`, headers del cliente ignorados, firma válida /
inválida / caducada, REVIEW rechaza sin firma, endpoints bloqueados con 403, arranque
de REVIEW con variables prohibidas, guardas de la siembra, determinismo e integridad
referencial del dataset, varios dominios de login (`@playreview.invalid,@test.com`),
cuentas de testers y que los hashes de `docs/testers-cuentas.sql` validen `prueba1234`.

## 8. Fuera del alcance (pendientes relacionados)

- `mobile-admin`: quitar `DEV_CUENTAS` de `src/app/login.tsx` y que ninguna build que
  vaya a Play apunte a la LAN.
- Rotar `ALEXA_API_KEY` de PROD (da ADMIN y está en el repositorio de la skill).
- Si PROD usa la instrumentación automática de Datadog (profiler), ésta traza a nivel de
  host también las peticiones reenviadas (ruta y estado; sin cuerpo).
- Render registra a nivel de plataforma la línea HTTP de cada petición de PROD, incluidas
  las reenviadas (sin cuerpo).

## 9. Testers de Google Play (TEMPORAL)

> TEMPORAL-TESTERS: todo lo de esta sección se retira cuando Google Play apruebe la app
> para producción.

La prueba cerrada de Google Play (12+ testers durante 14 días) usa este mismo entorno de
revisión: los testers inician sesión y registran ventas de mostrador en la base de
REVISIÓN, nunca en la de producción.

**Cuentas** (contraseña de todas: `prueba1234`, pública a propósito; `ACTIVO`, correo
verificado): ADMIN `gadiel.admin@test.com`; EMPLEADO `liz.prueba`, `angelito.prueba`,
`abi.prueba`, `alan.prueba`, `sabino.prueba`, `domi.prueba`, `diana.prueba`,
`gadiel.empleado`, `leo.prueba`, `jordan.prueba`, `magdiel.prueba`, `luz.prueba`,
`javier.prueba`, `paco.prueba` (todas `@test.com`). La lista está en
`tools/PlayReviewSeed/CuentasTesters.cs` (ids fijos `5eed0101-0000-4000-8000-<n>`; solo se
agrega al final).

**Variables.** En PROD: `REVIEW_LOGIN_EMAIL_DOMAIN=@playreview.invalid,@test.com`. Sin
`@test.com` en la lista, el login de un tester se queda en PROD y falla (no existe ahí).
Antes de activarlo, comprueba en PROD que ningún usuario real use ese dominio:
`SELECT count(*) FROM users WHERE correo ILIKE '%@test.com';` (debe dar 0; si no, esos
usuarios quedarían enviados a REVISIÓN). REVIEW no necesita variables nuevas.

**Sembrar.** La siembra de la sección 4 ya incluye a los testers (crea o actualiza por
correo, con su rol) y, solo cuando hay testers, agrega un reabasto ficticio de existencias
(≥ 500 por insumo, salvo dos que se dejan bajo mínimo para que se vea la alerta) para
que las ventas con receta no se rechacen durante los 14 días:

```powershell
Set-Location "D:\FloreriaBautista_Dev\Backend"
$env:REVIEW_DB_URL = "<cadena de la rama play-review con el rol floreria_review>"
$env:PROD_DB_HOST = "<host de la base de producción>"
dotnet run --project tools/PlayReviewSeed -- --credenciales "D:\temporal\play-review\credenciales-revision.local.json" --reiniciar
```

```powershell
Remove-Item Env:REVIEW_DB_URL
```

Alternativa sin resembrar (solo agrega o actualiza las 15 cuentas, no toca los datos):
`docs/testers-cuentas.sql` (UPSERT por correo, hashes BCrypt reales de `prueba1234`,
aborta si la base no tiene `_entorno = 'play_review'`):

```powershell
psql "$env:REVIEW_DB_URL" -v ON_ERROR_STOP=1 -f docs/testers-cuentas.sql
```

**Qué pueden hacer.** Todo lo de negocio: iniciar sesión, ver catálogo e inventario,
registrar ventas de mostrador (`POST /api/orders/physical`, efectivo/tarjeta/transferencia
solo se registran), gastos y corte del día; el ADMIN además pedidos, inventario
(incluido registrar entradas si se acaban existencias) y reportes. Siguen bloqueados o
apagados: WhatsApp, Mercado Pago, respaldos, Google/Cloudinary, Alexa, alta/edición de
usuarios y responsable de turno. El push de Expo no se envía (el registro del token del
teléfono sí se guarda y no falla); SignalR funciona dentro de REVIEW.

**Ojo con la resiembra diaria.** `--reiniciar` vacía pedidos, pagos, cortes y gastos: lo
que vendieron los testers se pierde (las cuentas y sus sesiones se conservan). Si se
activa el workflow `play-review-resiembra.yml` durante la prueba cerrada, conviene
moverlo a la madrugada de México (p. ej. `0 11 * * *` = 05:00) para no borrar ventas a
media jornada.

**Retirar todo cuando Google apruebe la app:**
1. En PROD: `REVIEW_LOGIN_EMAIL_DOMAIN` de vuelta a `@playreview.invalid` (o bórrala).
   Desde ese momento ningún login `@test.com` llega a REVISIÓN.
2. En la base de REVISIÓN: resembrar con `--reiniciar --sin-testers` (borra las cuentas
   de testers, sus sesiones y todo lo que registraron).
3. En el código: borrar `tools/PlayReviewSeed/CuentasTesters.cs`, `docs/testers-cuentas.sql`,
   esta sección y los bloques marcados `TEMPORAL-TESTERS` (buscar ese texto), y las pruebas
   de testers de `PlayReviewSiembraTests`.
