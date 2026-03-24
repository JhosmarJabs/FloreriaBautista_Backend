Moficaciones
-- ============================================================
-- FLORERÍA BAUTISTA - ROLES, USUARIOS Y PERMISOS COMPLETOS
-- Versión con backup_user y db_admin_user listos para pg_dump
-- ============================================================

-- 0. (Opcional) Limpiar si ya existían roles/usuarios
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_user') THEN
        REVOKE app_writer FROM app_user;
        DROP ROLE app_user;
    END IF;
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'db_admin_user') THEN
        REVOKE db_admin FROM db_admin_user;
        DROP ROLE db_admin_user;
    END IF;
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'backup_user') THEN
        REVOKE db_backup FROM backup_user;
        DROP ROLE backup_user;
    END IF;
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'import_user') THEN
        REVOKE db_import FROM import_user;
        DROP ROLE import_user;
    END IF;
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'reporting_user') THEN
        REVOKE app_readonly FROM reporting_user;
        DROP ROLE reporting_user;
    END IF;

    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_audit') THEN
        DROP ROLE app_audit;
    END IF;
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'db_import') THEN
        DROP ROLE db_import;
    END IF;
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'db_backup') THEN
        DROP ROLE db_backup;
    END IF;
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_readonly') THEN
        DROP ROLE app_readonly;
    END IF;
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_writer') THEN
        DROP ROLE app_writer;
    END IF;
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_admin') THEN
        DROP ROLE app_admin;
    END IF;
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'db_admin') THEN
        DROP ROLE db_admin;
    END IF;
END$$;


-- ============================================================
-- 1. ROLES TÉCNICOS
-- ============================================================

CREATE ROLE db_admin     INHERIT;   -- DBA
CREATE ROLE app_admin    NOINHERIT; -- Admin de negocio
CREATE ROLE app_writer   NOINHERIT; -- Backend normal
CREATE ROLE app_readonly NOINHERIT; -- Solo lectura
CREATE ROLE db_backup    INHERIT;   -- Respaldo (pg_dump)
CREATE ROLE db_import    NOINHERIT; -- Importaciones
CREATE ROLE app_audit    NOINHERIT; -- Auditoría


-- ============================================================
-- 2. USUARIOS TÉCNICOS
-- Sustituye los passwords por valores reales
-- ============================================================

-- Usuario principal de la aplicación (API)
CREATE USER app_user
  WITH PASSWORD 'TU_PASSWORD_APP_USER'
  NOINHERIT;
GRANT app_writer TO app_user;

-- Usuario DBA de negocio
CREATE USER db_admin_user
  WITH PASSWORD 'TU_PASSWORD_DB_ADMIN_USER'
  INHERIT;
GRANT db_admin TO db_admin_user;

-- Usuario para backups (solo lectura global)
CREATE USER backup_user
  WITH PASSWORD 'TU_PASSWORD_BACKUP_USER'
  INHERIT;
GRANT db_backup TO backup_user;

-- Usuario para procesos de importación
CREATE USER import_user
  WITH PASSWORD 'TU_PASSWORD_IMPORT_USER'
  NOINHERIT;
GRANT db_import TO import_user;

-- Usuario para reportes (Power BI / Metabase)
CREATE USER reporting_user
  WITH PASSWORD 'TU_PASSWORD_REPORTING_USER'
  NOINHERIT;
GRANT app_readonly TO reporting_user;


-- ============================================================
-- 3. PERMISOS – app_writer (backend normal)
-- ============================================================

GRANT SELECT, INSERT, UPDATE, DELETE ON
    users,
    user_roles,
    auth_tokens,
    customers,
    addresses,
    products,
    product_categories,
    product_collections,
    product_customization_options,
    orders,
    order_items,
    order_item_customizations,
    payments,
    deliveries,
    inventory_items,
    inventory_movements
TO app_writer;

GRANT SELECT ON
    roles,
    categories,
    collections,
    customization_options
TO app_writer;

GRANT SELECT, INSERT ON
    backup_jobs,
    import_jobs,
    audit_logs
TO app_writer;

GRANT USAGE ON SCHEMA public TO app_writer;


-- ============================================================
-- 4. PERMISOS – app_admin (admin de negocio)
-- ============================================================

GRANT SELECT, INSERT, UPDATE, DELETE ON
    users,
    user_roles,
    auth_tokens,
    roles,
    customers,
    addresses,
    products,
    categories,
    collections,
    customization_options,
    product_categories,
    product_collections,
    product_customization_options,
    orders,
    order_items,
    order_item_customizations,
    payments,
    deliveries,
    inventory_items,
    inventory_movements,
    backup_jobs,
    import_jobs,
    audit_logs
TO app_admin;

GRANT USAGE ON SCHEMA public TO app_admin;


-- ============================================================
-- 5. PERMISOS – db_admin (DBA)
-- ============================================================

GRANT CONNECT ON DATABASE floreria_bautista TO db_admin;

GRANT ALL PRIVILEGES ON ALL TABLES    IN SCHEMA public TO db_admin;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO db_admin;
GRANT ALL PRIVILEGES ON SCHEMA public TO db_admin;

ALTER ROLE db_admin CREATEDB;


-- ============================================================
-- 6. PERMISOS – db_backup (para pg_dump)
--    backup_user hereda de db_backup
-- ============================================================

GRANT CONNECT ON DATABASE floreria_bautista TO db_backup;

GRANT USAGE ON SCHEMA public TO db_backup;

GRANT SELECT ON ALL TABLES    IN SCHEMA public TO db_backup;
GRANT SELECT ON ALL SEQUENCES IN SCHEMA public TO db_backup;

ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT SELECT ON TABLES    TO db_backup;
ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT SELECT ON SEQUENCES TO db_backup;


-- ============================================================
-- 7. PERMISOS – db_import
-- ============================================================

GRANT SELECT, INSERT, UPDATE ON
    products,
    product_categories,
    product_collections,
    product_customization_options,
    categories,
    collections,
    customization_options,
    inventory_items,
    inventory_movements,
    import_jobs
TO db_import;

GRANT USAGE ON SCHEMA public TO db_import;


-- ============================================================
-- 8. PERMISOS – app_readonly (reportes)
-- ============================================================

GRANT SELECT ON
    products,
    categories,
    collections,
    customization_options,
    product_categories,
    product_collections,
    orders,
    order_items,
    order_item_customizations,
    payments,
    deliveries,
    inventory_items,
    inventory_movements,
    customers,
    backup_jobs,
    import_jobs
TO app_readonly;

GRANT USAGE ON SCHEMA public TO app_readonly;


-- ============================================================
-- 9. PERMISOS – app_audit
-- ============================================================

GRANT SELECT ON
    audit_logs,
    backup_jobs,
    import_jobs,
    orders,
    order_items,
    payments,
    inventory_movements,
    users,
    customers
TO app_audit;

GRANT USAGE ON SCHEMA public TO app_audit;


-- ============================================================
-- 10. SEGURIDAD – Revocar permisos del rol PUBLIC
-- ============================================================

REVOKE ALL ON ALL TABLES IN SCHEMA public FROM PUBLIC;
REVOKE ALL ON SCHEMA public FROM PUBLIC;
GRANT CONNECT ON DATABASE floreria_bautista TO PUBLIC;

-- (Opcional, si tu versión tiene estos roles predefinidos)
-- GRANT pg_read_all_data TO db_backup;
-- GRANT pg_read_all_data TO db_admin;

-- ============================================================
-- FIN DEL SCRIPT
-- ============================================================

-- Ejecutar en pgAdmin sobre floreria_bautista como superusuario

-- Permisos de schema para app_writer
GRANT USAGE  ON SCHEMA public TO app_writer;
GRANT CREATE ON SCHEMA public TO app_writer;

-- DELETE en tablas de limpieza
GRANT DELETE ON public.audit_logs   TO app_writer;
GRANT DELETE ON public.backup_jobs  TO app_writer;
GRANT DELETE ON public.import_jobs  TO app_writer;
GRANT DELETE ON public.auth_tokens  TO app_writer;

-- VACUUM lo puede hacer cualquier usuario con SELECT en la tabla
GRANT SELECT ON ALL TABLES IN SCHEMA public TO app_writer;

-- Permisos de schema para db_admin_user (REINDEX)
GRANT ALL PRIVILEGES ON SCHEMA public TO db_admin;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO db_admin;

SELECT 'OK: permisos aplicados' AS resultado;

-- ============================================================
--  FLORERÍA BAUTISTA — Inserción Masiva de Datos
--  Motor: PostgreSQL
--  Descripción: ~100+ registros por tabla principal
--  NOTA: Ejecutar DESPUÉS del script DDL + seed base.
-- ============================================================

-- ============================================================
-- BLOQUE 0: LIMPIAR DATOS DE PRUEBA PREVIOS (opcional)
-- Descomenta si quieres reiniciar los datos extra antes de reinsertar
-- ============================================================
-- TRUNCATE audit_logs, import_jobs, backup_jobs, inventory_movements,
--          inventory_items, deliveries, payments, order_item_customizations,
--          order_items, orders, addresses, customers RESTART IDENTITY CASCADE;


-- ============================================================
-- BLOQUE 1: CLIENTES FÍSICOS (100 registros)
-- ============================================================

INSERT INTO customers (id, user_id, tipo_cliente, nombre, apellido, telefono, correo, sexo, fecha_nacimiento, creado_en) VALUES
  (gen_random_uuid(), NULL, 'FISICO', 'Alejandro',  'Vargas',     '7710100001', 'alejandro.vargas@mail.com',    'M', '1988-01-15', now() - interval '400 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Valentina',  'Ríos',       '7710100002', 'valentina.rios@mail.com',      'F', '1993-02-20', now() - interval '398 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Rodrigo',    'Castillo',   '7710100003', 'rodrigo.castillo@mail.com',    'M', '1985-03-10', now() - interval '396 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Gabriela',   'Mendoza',    '7710100004', 'gabriela.mendoza@mail.com',    'F', '1991-04-25', now() - interval '394 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Óscar',      'Fuentes',    '7710100005', 'oscar.fuentes@mail.com',       'M', '1987-05-05', now() - interval '392 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Daniela',    'Navarro',    '7710100006', 'daniela.navarro@mail.com',     'F', '1994-06-30', now() - interval '390 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Miguel',     'Reyes',      '7710100007', 'miguel.reyes@mail.com',        'M', '1990-07-17', now() - interval '388 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Paola',      'Aguilar',    '7710100008', 'paola.aguilar@mail.com',       'F', '1989-08-22', now() - interval '386 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Andrés',     'Morales',    '7710100009', 'andres.morales@mail.com',      'M', '1992-09-11', now() - interval '384 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Lorena',     'Jiménez',    '7710100010', 'lorena.jimenez@mail.com',      'F', '1996-10-08', now() - interval '382 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Fernando',   'Gutiérrez',  '7710100011', 'fernando.gutierrez@mail.com',  'M', '1983-11-14', now() - interval '380 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Isabel',     'Flores',     '7710100012', 'isabel.flores@mail.com',       'F', '1997-12-03', now() - interval '378 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Enrique',    'Romero',     '7710100013', 'enrique.romero@mail.com',      'M', '1986-01-28', now() - interval '376 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Marcela',    'Espinoza',   '7710100014', 'marcela.espinoza@mail.com',    'F', '1995-02-16', now() - interval '374 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Javier',     'Leal',       '7710100015', 'javier.leal@mail.com',         'M', '1984-03-09', now() - interval '372 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Adriana',    'Paredes',    '7710100016', 'adriana.paredes@mail.com',     'F', '1998-04-21', now() - interval '370 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Roberto',    'Medina',     '7710100017', 'roberto.medina@mail.com',      'M', '1982-05-06', now() - interval '368 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Natalia',    'Vega',       '7710100018', 'natalia.vega@mail.com',        'F', '1993-06-13', now() - interval '366 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Sergio',     'Campos',     '7710100019', 'sergio.campos@mail.com',       'M', '1991-07-27', now() - interval '364 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Karla',      'Santos',     '7710100020', 'karla.santos@mail.com',        'F', '1988-08-01', now() - interval '362 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Hugo',       'Delgado',    '7710100021', 'hugo.delgado@mail.com',        'M', '1990-09-19', now() - interval '360 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Verónica',   'Ibáñez',     '7710100022', 'veronica.ibanez@mail.com',     'F', '1994-10-07', now() - interval '358 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Alberto',    'Acosta',     '7710100023', 'alberto.acosta@mail.com',      'M', '1985-11-23', now() - interval '356 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Mónica',     'Valencia',   '7710100024', 'monica.valencia@mail.com',     'F', '1997-12-12', now() - interval '354 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Ricardo',    'Serrano',    '7710100025', 'ricardo.serrano@mail.com',     'M', '1986-01-04', now() - interval '352 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Cecilia',    'Vázquez',    '7710100026', 'cecilia.vazquez@mail.com',     'F', '1992-02-18', now() - interval '350 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Marco',      'Peña',       '7710100027', 'marco.pena@mail.com',          'M', '1989-03-31', now() - interval '348 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Blanca',     'Herrera',    '7710100028', 'blanca.herrera@mail.com',      'F', '1995-04-14', now() - interval '346 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Raúl',       'Ramos',      '7710100029', 'raul.ramos@mail.com',          'M', '1983-05-26', now() - interval '344 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Ximena',     'Soto',       '7710100030', 'ximena.soto@mail.com',         'F', '1996-06-09', now() - interval '342 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Héctor',     'Montoya',    '7710100031', 'hector.montoya@mail.com',      'M', '1984-07-03', now() - interval '340 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Patricia',   'Contreras',  '7710100032', 'patricia.contreras@mail.com',  'F', '1991-08-15', now() - interval '338 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Ernesto',    'Gil',        '7710100033', 'ernesto.gil@mail.com',         'M', '1987-09-28', now() - interval '336 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Claudia',    'Tapia',      '7710100034', 'claudia.tapia@mail.com',       'F', '1993-10-20', now() - interval '334 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Alfonso',    'Guerrero',   '7710100035', 'alfonso.guerrero@mail.com',    'M', '1980-11-11', now() - interval '332 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Rebeca',     'Luna',       '7710100036', 'rebeca.luna@mail.com',         'F', '1998-12-24', now() - interval '330 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Ignacio',    'Sandoval',   '7710100037', 'ignacio.sandoval@mail.com',    'M', '1988-01-07', now() - interval '328 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Diana',      'Rojas',      '7710100038', 'diana.rojas@mail.com',         'F', '1990-02-22', now() - interval '326 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Arturo',     'Cervantes',  '7710100039', 'arturo.cervantes@mail.com',    'M', '1986-03-18', now() - interval '324 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Esmeralda',  'Palma',      '7710100040', 'esmeralda.palma@mail.com',     'F', '1994-04-05', now() - interval '322 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Rubén',      'Mena',       '7710100041', 'ruben.mena@mail.com',          'M', '1982-05-29', now() - interval '320 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Angélica',   'Salas',      '7710100042', 'angelica.salas@mail.com',      'F', '1995-06-16', now() - interval '318 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Víctor',     'Cabrera',    '7710100043', 'victor.cabrera@mail.com',      'M', '1989-07-08', now() - interval '316 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Martha',     'Cordero',    '7710100044', 'martha.cordero@mail.com',      'F', '1991-08-30', now() - interval '314 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Jorge',      'Alvarado',   '7710100045', 'jorge.alvarado@mail.com',      'M', '1985-09-14', now() - interval '312 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Susana',     'Miranda',    '7710100046', 'susana.miranda@mail.com',      'F', '1997-10-25', now() - interval '310 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Manuel',     'Cisneros',   '7710100047', 'manuel.cisneros@mail.com',     'M', '1983-11-02', now() - interval '308 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Silvia',     'Bravo',      '7710100048', 'silvia.bravo@mail.com',        'F', '1996-12-19', now() - interval '306 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Esteban',    'Ponce',      '7710100049', 'esteban.ponce@mail.com',       'M', '1987-01-31', now() - interval '304 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Yolanda',    'Ochoa',      '7710100050', 'yolanda.ochoa@mail.com',       'F', '1992-02-10', now() - interval '302 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Tomás',      'Arellano',   '7710100051', 'tomas.arellano@mail.com',      'M', '1984-03-22', now() - interval '300 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Lucía',      'Palacios',   '7710100052', 'lucia.palacios@mail.com',      'F', '1993-04-11', now() - interval '298 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Gustavo',    'Montes',     '7710100053', 'gustavo.montes@mail.com',      'M', '1981-05-03', now() - interval '296 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Pilar',      'Ángeles',    '7710100054', 'pilar.angeles@mail.com',       'F', '1998-06-27', now() - interval '294 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Dante',      'Huerta',     '7710100055', 'dante.huerta@mail.com',        'M', '1990-07-15', now() - interval '292 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Miriam',     'Solano',     '7710100056', 'miriam.solano@mail.com',       'F', '1989-08-04', now() - interval '290 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Armando',    'Fuente',     '7710100057', 'armando.fuente@mail.com',      'M', '1986-09-18', now() - interval '288 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Estela',     'Zamora',     '7710100058', 'estela.zamora@mail.com',       'F', '1994-10-30', now() - interval '286 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Ramón',      'Téllez',     '7710100059', 'ramon.tellez@mail.com',        'M', '1982-11-08', now() - interval '284 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Liliana',    'Bernal',     '7710100060', 'liliana.bernal@mail.com',      'F', '1995-12-21', now() - interval '282 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Joel',       'Esquivel',   '7710100061', 'joel.esquivel@mail.com',       'M', '1988-01-09', now() - interval '280 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Brenda',     'Valdez',     '7710100062', 'brenda.valdez@mail.com',       'F', '1991-02-26', now() - interval '278 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Gerardo',    'Nájera',     '7710100063', 'gerardo.najera@mail.com',      'M', '1987-03-13', now() - interval '276 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Esperanza',  'Domínguez',  '7710100064', 'esperanza.dominguez@mail.com', 'F', '1993-04-02', now() - interval '274 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Iván',       'Quiroz',     '7710100065', 'ivan.quiroz@mail.com',         'M', '1985-05-20', now() - interval '272 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Rosa',       'Linares',    '7710100066', 'rosa.linares@mail.com',        'F', '1997-06-07', now() - interval '270 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Norberto',   'Salinas',    '7710100067', 'norberto.salinas@mail.com',    'M', '1983-07-24', now() - interval '268 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Amparo',     'Bustamante', '7710100068', 'amparo.bustamante@mail.com',   'F', '1996-08-16', now() - interval '266 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Cristóbal',  'Lozano',     '7710100069', 'cristobal.lozano@mail.com',    'M', '1984-09-05', now() - interval '264 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Consuelo',   'Reina',      '7710100070', 'consuelo.reina@mail.com',      'F', '1990-10-23', now() - interval '262 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Salvador',   'Becerra',    '7710100071', 'salvador.becerra@mail.com',    'M', '1986-11-10', now() - interval '260 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Nadia',      'Estrada',    '7710100072', 'nadia.estrada@mail.com',       'F', '1994-12-28', now() - interval '258 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Felipe',     'Montiel',    '7710100073', 'felipe.montiel@mail.com',      'M', '1982-01-06', now() - interval '256 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Leticia',    'Portillo',   '7710100074', 'leticia.portillo@mail.com',    'F', '1995-02-14', now() - interval '254 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Braulio',    'Cano',       '7710100075', 'braulio.cano@mail.com',        'M', '1989-03-01', now() - interval '252 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Gloria',     'Tello',      '7710100076', 'gloria.tello@mail.com',        'F', '1992-04-17', now() - interval '250 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Leandro',    'Ávila',      '7710100077', 'leandro.avila@mail.com',       'M', '1981-05-09', now() - interval '248 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Norma',      'Calderón',   '7710100078', 'norma.calderon@mail.com',      'F', '1998-06-23', now() - interval '246 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Gerónimo',   'Cuevas',     '7710100079', 'geronimo.cuevas@mail.com',     'M', '1987-07-12', now() - interval '244 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Elena',      'Trejo',      '7710100080', 'elena.trejo@mail.com',         'F', '1991-08-20', now() - interval '242 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Marcial',    'Olvera',     '7710100081', 'marcial.olvera@mail.com',      'M', '1985-09-06', now() - interval '240 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Araceli',    'Nieto',      '7710100082', 'araceli.nieto@mail.com',       'F', '1993-10-18', now() - interval '238 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Benito',     'Vergara',    '7710100083', 'benito.vergara@mail.com',      'M', '1980-11-30', now() - interval '236 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Esther',     'Aranda',     '7710100084', 'esther.aranda@mail.com',       'F', '1996-12-15', now() - interval '234 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Aurelio',    'Quiñones',   '7710100085', 'aurelio.quinones@mail.com',    'M', '1984-01-27', now() - interval '232 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Isidora',    'Pedraza',    '7710100086', 'isidora.pedraza@mail.com',     'F', '1990-02-08', now() - interval '230 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Oswaldo',    'Barrera',    '7710100087', 'oswaldo.barrera@mail.com',     'M', '1988-03-25', now() - interval '228 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Matilde',    'Escobedo',   '7710100088', 'matilde.escobedo@mail.com',    'F', '1995-04-09', now() - interval '226 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Eugenio',    'Zepeda',     '7710100089', 'eugenio.zepeda@mail.com',      'M', '1983-05-17', now() - interval '224 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Graciela',   'Ibarra',     '7710100090', 'graciela.ibarra@mail.com',     'F', '1997-06-01', now() - interval '222 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Álvaro',     'Osuna',      '7710100091', 'alvaro.osuna@mail.com',        'M', '1986-07-19', now() - interval '220 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Celia',      'Hidalgo',    '7710100092', 'celia.hidalgo@mail.com',       'F', '1992-08-07', now() - interval '218 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Mauricio',   'Sarabia',    '7710100093', 'mauricio.sarabia@mail.com',    'M', '1984-09-21', now() - interval '216 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Graciela',   'Macías',     '7710100094', 'graciela.macias@mail.com',     'F', '1991-10-13', now() - interval '214 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Demetrio',   'Zenteno',    '7710100095', 'demetrio.zenteno@mail.com',    'M', '1982-11-24', now() - interval '212 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Inés',       'Correa',     '7710100096', 'ines.correa@mail.com',         'F', '1996-12-06', now() - interval '210 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Pompeyo',    'Varela',     '7710100097', 'pompeyo.varela@mail.com',      'M', '1985-01-18', now() - interval '208 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Fabiola',    'Pulido',     '7710100098', 'fabiola.pulido@mail.com',      'F', '1993-02-05', now() - interval '206 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Celestino',  'Leiva',      '7710100099', 'celestino.leiva@mail.com',     'M', '1981-03-14', now() - interval '204 days'),
  (gen_random_uuid(), NULL, 'FISICO', 'Wendy',      'Cardenas',   '7710100100', 'wendy.cardenas@mail.com',      'F', '1999-04-28', now() - interval '202 days');


-- ============================================================
-- BLOQUE 2: DIRECCIONES (1 por cliente nuevo, 100 registros)
-- ============================================================

INSERT INTO addresses (id, customer_id, etiqueta, calle, colonia, municipio, estado, cp, referencias, creado_en)
SELECT
    gen_random_uuid(),
    c.id,
    CASE (row_number() OVER (ORDER BY c.creado_en) % 3)
        WHEN 0 THEN 'Casa'
        WHEN 1 THEN 'Trabajo'
        ELSE         'Otro'
    END,
    'Calle Hidalgo #' || (row_number() OVER (ORDER BY c.creado_en) * 3),
    CASE (row_number() OVER (ORDER BY c.creado_en) % 6)
        WHEN 0 THEN 'Centro'
        WHEN 1 THEN 'Las Flores'
        WHEN 2 THEN 'El Paraíso'
        WHEN 3 THEN 'Lomas Verdes'
        WHEN 4 THEN 'San Marcos'
        ELSE         'Revolución'
    END,
    CASE (row_number() OVER (ORDER BY c.creado_en) % 4)
        WHEN 0 THEN 'Huejutla de Reyes'
        WHEN 1 THEN 'Ixhuatlán de Madero'
        WHEN 2 THEN 'Atlapexco'
        ELSE         'Yahualica'
    END,
    'Hidalgo',
    LPAD(((row_number() OVER (ORDER BY c.creado_en) % 10) + 43000)::TEXT, 5, '0'),
    'Entre calle ' || (row_number() OVER (ORDER BY c.creado_en)) || ' y calle ' || (row_number() OVER (ORDER BY c.creado_en) + 1),
    c.creado_en
FROM customers c
WHERE c.telefono LIKE '771010%'   -- sólo los 100 recién insertados
ORDER BY c.creado_en;


-- ============================================================
-- BLOQUE 3: PRODUCTOS (30 adicionales)
-- ============================================================

INSERT INTO products (id, nombre, descripcion, precio_base, tipo, es_personalizable, estado, imagen_url, creado_en) VALUES
    (gen_random_uuid(), 'Bouquet nupcial blanco',      'Rosas blancas y hortensias para novia.',        1200.00, 'ARREGLO_FLORAL', TRUE,  'ACTIVO', NULL, now() - interval '180 days'),
    (gen_random_uuid(), 'Ramo de peonías',              '8 peonías rosadas de temporada.',               750.00,  'ARREGLO_FLORAL', TRUE,  'ACTIVO', NULL, now() - interval '178 days'),
    (gen_random_uuid(), 'Arreglo tropical',             'Heliconias, anturios y follaje exótico.',        680.00,  'ARREGLO_FLORAL', FALSE, 'ACTIVO', NULL, now() - interval '176 days'),
    (gen_random_uuid(), 'Ramo de lavanda',              '15 tallos de lavanda aromática.',                320.00,  'ARREGLO_FLORAL', FALSE, 'ACTIVO', NULL, now() - interval '174 days'),
    (gen_random_uuid(), 'Caja de 24 rosas premium',    '24 rosas en caja de lujo con listón.',          1800.00,  'ARREGLO_FLORAL', TRUE,  'ACTIVO', NULL, now() - interval '172 days'),
    (gen_random_uuid(), 'Arreglo en canasta de mimbre', 'Flores mixtas en canasta artesanal.',           580.00,  'ARREGLO_FLORAL', TRUE,  'ACTIVO', NULL, now() - interval '170 days'),
    (gen_random_uuid(), 'Pompones de crisantemo',       '20 pompones en tonos cálidos.',                 390.00,  'ARREGLO_FLORAL', FALSE, 'ACTIVO', NULL, now() - interval '168 days'),
    (gen_random_uuid(), 'Ramo campestre silvestre',     'Flores de campo y hierbas aromáticas.',         420.00,  'ARREGLO_FLORAL', TRUE,  'ACTIVO', NULL, now() - interval '166 days'),
    (gen_random_uuid(), 'Corona fúnebre con listón',   'Corona tradicional con dedicatoria.',           1700.00,  'ARREGLO_FLORAL', TRUE,  'ACTIVO', NULL, now() - interval '164 days'),
    (gen_random_uuid(), 'Arreglo de San Valentín rojo', 'Rosas rojas en corazón de icopor.',            1350.00,  'ARREGLO_FLORAL', TRUE,  'ACTIVO', NULL, now() - interval '162 days'),
    (gen_random_uuid(), 'Planta de cactus decorativo', 'Cactus en maceta de barro pintada.',             280.00,  'FLORERO',        FALSE, 'ACTIVO', NULL, now() - interval '160 days'),
    (gen_random_uuid(), 'Suculentas en caja regalo',   'Mix de 5 suculentas en caja kraft.',             450.00,  'FLORERO',        FALSE, 'ACTIVO', NULL, now() - interval '158 days'),
    (gen_random_uuid(), 'Florero de cristal alto',      'Florero cilíndrico alto transparente.',         350.00,  'FLORERO',        FALSE, 'ACTIVO', NULL, now() - interval '156 days'),
    (gen_random_uuid(), 'Arreglo aniversario dorado',  'Rosas doradas spray con detalles dorados.',     2200.00,  'ARREGLO_FLORAL', TRUE,  'ACTIVO', NULL, now() - interval '154 days'),
    (gen_random_uuid(), 'Gladiolas de temporada',       '10 gladiolas en ramo sencillo.',                340.00,  'ARREGLO_FLORAL', FALSE, 'ACTIVO', NULL, now() - interval '152 days'),
    (gen_random_uuid(), 'Ramo de lisianthus morado',   '12 lisianthus en tonos morados.',                580.00,  'ARREGLO_FLORAL', TRUE,  'ACTIVO', NULL, now() - interval '150 days'),
    (gen_random_uuid(), 'Bouquet graduación colorido', 'Rosas y girasoles con birretes decorativos.',   690.00,  'ARREGLO_FLORAL', TRUE,  'ACTIVO', NULL, now() - interval '148 days'),
    (gen_random_uuid(), 'Arreglo Día de la Madre',     'Rosas y alstroemerias con lazo rosa.',          820.00,  'ARREGLO_FLORAL', TRUE,  'ACTIVO', NULL, now() - interval '146 days'),
    (gen_random_uuid(), 'Ramo de iris azul',            '8 iris azules con eucalipto.',                  490.00,  'ARREGLO_FLORAL', FALSE, 'ACTIVO', NULL, now() - interval '144 days'),
    (gen_random_uuid(), 'Canasta de Navidad',           'Pino, poinsettia y esferas en canasta.',        760.00,  'ARREGLO_FLORAL', FALSE, 'ACTIVO', NULL, now() - interval '142 days'),
    (gen_random_uuid(), 'Arreglo baby shower rosa',    'Flores pastel con decoración de bebé.',         720.00,  'ARREGLO_FLORAL', TRUE,  'ACTIVO', NULL, now() - interval '140 days'),
    (gen_random_uuid(), 'Arreglo baby shower azul',    'Flores azul cielo con decoración de bebé.',     720.00,  'ARREGLO_FLORAL', TRUE,  'ACTIVO', NULL, now() - interval '138 days'),
    (gen_random_uuid(), 'Ramo de amapolas silvestres', '15 amapolas de temporada.',                      410.00,  'ARREGLO_FLORAL', FALSE, 'ACTIVO', NULL, now() - interval '136 days'),
    (gen_random_uuid(), 'Decoración mesa boda rústica','Centro de mesa con flores silvestres.',          1100.00, 'ARREGLO_FLORAL', TRUE,  'ACTIVO', NULL, now() - interval '134 days'),
    (gen_random_uuid(), 'Ramo zen minimalista',         'Callas blancas con bambú y musgo.',              870.00,  'ARREGLO_FLORAL', TRUE,  'ACTIVO', NULL, now() - interval '132 days'),
    (gen_random_uuid(), 'Arreglo aniversario de plata','Flores blancas con detalles plateados.',        1950.00,  'ARREGLO_FLORAL', TRUE,  'ACTIVO', NULL, now() - interval '130 days'),
    (gen_random_uuid(), 'Ramo de gerberas multicolor', '12 gerberas en colores vivos.',                  460.00,  'ARREGLO_FLORAL', FALSE, 'ACTIVO', NULL, now() - interval '128 days'),
    (gen_random_uuid(), 'Set de 3 orquídeas',           '3 orquídeas phalaenopsis en macetas blancas.', 2100.00,  'FLORERO',        FALSE, 'ACTIVO', NULL, now() - interval '126 days'),
    (gen_random_uuid(), 'Ramo de neblina (gypsophila)', '1 ramo grande de gypsophila blanca.',            300.00,  'ARREGLO_FLORAL', FALSE, 'ACTIVO', NULL, now() - interval '124 days'),
    (gen_random_uuid(), 'Arreglo corporativo minimalista','Flores blancas y verdes para oficina.',      1300.00,  'ARREGLO_FLORAL', TRUE,  'ACTIVO', NULL, now() - interval '122 days');


-- ============================================================
-- BLOQUE 4: PEDIDOS (100 registros)
-- ============================================================

INSERT INTO orders (
    id, customer_id, tipo_pedido, canal, estado_pedido,
    direccion_entrega_calle, direccion_entrega_colonia,
    direccion_entrega_municipio, direccion_entrega_estado,
    direccion_entrega_cp, direccion_entrega_referencias,
    fecha_creacion, fecha_entrega, hora_entrega,
    total, saldo_pendiente, notas
)
SELECT
    gen_random_uuid(),
    c.id,
    CASE (rn % 2) WHEN 0 THEN 'INSTANTANEO' ELSE 'ANTICIPADO' END,
    CASE (rn % 3) WHEN 0 THEN 'FISICO' WHEN 1 THEN 'WEB' ELSE 'TELEFONO' END,
    CASE (rn % 7)
        WHEN 0 THEN 'PENDIENTE_VALIDACION'
        WHEN 1 THEN 'CONFIRMADO'
        WHEN 2 THEN 'EN_PREPARACION'
        WHEN 3 THEN 'LISTO_PARA_ENTREGA'
        WHEN 4 THEN 'EN_RUTA'
        WHEN 5 THEN 'ENTREGADO'
        ELSE         'CANCELADO'
    END,
    a.calle,
    a.colonia,
    a.municipio,
    a.estado,
    a.cp,
    a.referencias,
    now() - (rn * interval '2 days'),
    (now() + ((rn % 14) * interval '1 day'))::date,
    CASE (rn % 4)
        WHEN 0 THEN '09:00:00'::time
        WHEN 1 THEN '12:00:00'::time
        WHEN 2 THEN '15:00:00'::time
        ELSE         '18:00:00'::time
    END,
    (300 + (rn * 47)) % 2500 + 300,
    CASE WHEN rn % 3 = 0 THEN ((300 + (rn * 47)) % 2500 + 300) * 0.5 ELSE 0 END,
    'Pedido masivo #' || rn
FROM (
    SELECT c.id, a.calle, a.colonia, a.municipio, a.estado, a.cp, a.referencias,
           row_number() OVER (ORDER BY c.creado_en) AS rn
    FROM customers c
    JOIN addresses a ON a.customer_id = c.id
    WHERE c.telefono LIKE '771010%'
    ORDER BY c.creado_en
    LIMIT 100
) sub
JOIN customers c ON c.id = sub.id;


-- ============================================================
-- BLOQUE 5: ORDER_ITEMS (2 ítems por pedido = ~200 registros)
-- ============================================================

-- Ítem 1 por cada pedido nuevo (producto aleatorio del catálogo)
INSERT INTO order_items (id, order_id, product_id, cantidad, precio_unitario, subtotal)
SELECT
    gen_random_uuid(),
    o.id,
    p.id,
    1 + (row_number() OVER () % 3),          -- cantidad 1, 2 o 3
    p.precio_base,
    p.precio_base * (1 + (row_number() OVER () % 3))
FROM (
    SELECT o.id, row_number() OVER (ORDER BY o.fecha_creacion) AS rn
    FROM orders o
    WHERE o.notas LIKE 'Pedido masivo%'
    ORDER BY o.fecha_creacion
) o
JOIN (
    SELECT p.id, p.precio_base, row_number() OVER (ORDER BY p.creado_en) AS prn
    FROM products p WHERE p.estado = 'ACTIVO'
) p ON p.prn = (o.rn % (SELECT count(*) FROM products WHERE estado='ACTIVO')) + 1;

-- Ítem 2 por cada pedido nuevo (producto diferente, ciclo desplazado)
INSERT INTO order_items (id, order_id, product_id, cantidad, precio_unitario, subtotal)
SELECT
    gen_random_uuid(),
    o.id,
    p.id,
    1,
    p.precio_base,
    p.precio_base
FROM (
    SELECT o.id, row_number() OVER (ORDER BY o.fecha_creacion) AS rn
    FROM orders o
    WHERE o.notas LIKE 'Pedido masivo%'
    ORDER BY o.fecha_creacion
) o
JOIN (
    SELECT p.id, p.precio_base, row_number() OVER (ORDER BY p.creado_en DESC) AS prn
    FROM products p WHERE p.estado = 'ACTIVO'
) p ON p.prn = (o.rn % (SELECT count(*) FROM products WHERE estado='ACTIVO')) + 1;


-- ============================================================
-- BLOQUE 6: PAGOS (100 registros, 1 por pedido nuevo)
-- ============================================================

INSERT INTO payments (id, order_id, monto, tipo_pago, metodo, fecha_pago, estado)
SELECT
    gen_random_uuid(),
    o.id,
    CASE WHEN o.saldo_pendiente > 0 THEN o.total * 0.5 ELSE o.total END,
    CASE WHEN o.saldo_pendiente > 0 THEN 'ANTICIPO' ELSE 'TOTAL' END,
    CASE (row_number() OVER (ORDER BY o.fecha_creacion) % 4)
        WHEN 0 THEN 'EFECTIVO'
        WHEN 1 THEN 'TRANSFERENCIA'
        WHEN 2 THEN 'TARJETA'
        ELSE         'OTRO'
    END,
    o.fecha_creacion + interval '1 hour',
    'REGISTRADO'
FROM orders o
WHERE o.notas LIKE 'Pedido masivo%'
ORDER BY o.fecha_creacion;

-- Pagos de liquidación para pedidos con anticipo
INSERT INTO payments (id, order_id, monto, tipo_pago, metodo, fecha_pago, estado)
SELECT
    gen_random_uuid(),
    o.id,
    o.saldo_pendiente,
    'LIQUIDACION',
    'EFECTIVO',
    o.fecha_creacion + interval '1 day',
    'REGISTRADO'
FROM orders o
WHERE o.notas LIKE 'Pedido masivo%'
  AND o.saldo_pendiente > 0
ORDER BY o.fecha_creacion;


-- ============================================================
-- BLOQUE 7: ENTREGAS (100 registros, 1 por pedido nuevo)
-- ============================================================

INSERT INTO deliveries (id, order_id, repartidor_id, fecha_programada, hora_programada, estado_entrega, fecha_real, notas)
SELECT
    gen_random_uuid(),
    o.id,
    NULL,
    o.fecha_entrega,
    o.hora_entrega,
    CASE o.estado_pedido
        WHEN 'ENTREGADO'  THEN 'ENTREGADA'
        WHEN 'EN_RUTA'    THEN 'EN_RUTA'
        WHEN 'CANCELADO'  THEN 'CANCELADA'
        WHEN 'LISTO_PARA_ENTREGA' THEN 'ASIGNADA'
        ELSE 'PROGRAMADA'
    END,
    CASE WHEN o.estado_pedido = 'ENTREGADO' THEN o.fecha_creacion + interval '6 hours' ELSE NULL END,
    'Entrega programada automáticamente'
FROM orders o
WHERE o.notas LIKE 'Pedido masivo%'
ORDER BY o.fecha_creacion;


-- ============================================================
-- BLOQUE 8: INVENTORY_ITEMS para productos nuevos (30 registros)
-- ============================================================

INSERT INTO inventory_items (id, product_id, stock_actual, stock_minimo, sucursal)
SELECT
    gen_random_uuid(),
    p.id,
    (row_number() OVER (ORDER BY p.creado_en) % 50) + 10,
    5,
    CASE (row_number() OVER (ORDER BY p.creado_en) % 3)
        WHEN 0 THEN 'Sucursal Centro'
        WHEN 1 THEN 'Sucursal Norte'
        ELSE         'Sucursal Sur'
    END
FROM products p
WHERE p.nombre IN (
    'Bouquet nupcial blanco','Ramo de peonías','Arreglo tropical','Ramo de lavanda',
    'Caja de 24 rosas premium','Arreglo en canasta de mimbre','Pompones de crisantemo',
    'Ramo campestre silvestre','Corona fúnebre con listón','Arreglo de San Valentín rojo',
    'Planta de cactus decorativo','Suculentas en caja regalo','Florero de cristal alto',
    'Arreglo aniversario dorado','Gladiolas de temporada','Ramo de lisianthus morado',
    'Bouquet graduación colorido','Arreglo Día de la Madre','Ramo de iris azul',
    'Canasta de Navidad','Arreglo baby shower rosa','Arreglo baby shower azul',
    'Ramo de amapolas silvestres','Decoración mesa boda rústica','Ramo zen minimalista',
    'Arreglo aniversario de plata','Ramo de gerberas multicolor','Set de 3 orquídeas',
    'Ramo de neblina (gypsophila)','Arreglo corporativo minimalista'
);


-- ============================================================
-- BLOQUE 9: INVENTORY_MOVEMENTS (100 registros)
-- ============================================================

INSERT INTO inventory_movements (id, inventory_item_id, tipo_movimiento, cantidad, motivo, usuario_id, fecha_hora)
SELECT
    gen_random_uuid(),
    ii.id,
    CASE (row_number() OVER (ORDER BY ii.id) % 3)
        WHEN 0 THEN 'ENTRADA'
        WHEN 1 THEN 'SALIDA'
        ELSE         'AJUSTE'
    END,
    CASE (row_number() OVER (ORDER BY ii.id) % 3)
        WHEN 0 THEN  10 + (row_number() OVER (ORDER BY ii.id) % 20)
        WHEN 1 THEN -(5  + (row_number() OVER (ORDER BY ii.id) % 10))
        ELSE          1 + (row_number() OVER (ORDER BY ii.id) % 5)
    END,
    CASE (row_number() OVER (ORDER BY ii.id) % 3)
        WHEN 0 THEN 'Reposición de inventario'
        WHEN 1 THEN 'Venta registrada'
        ELSE         'Ajuste por conteo físico'
    END,
    u.id,
    now() - (row_number() OVER (ORDER BY ii.id) * interval '12 hours')
FROM inventory_items ii
CROSS JOIN (SELECT id FROM users WHERE correo = 'admin@floreriabautista.com' LIMIT 1) u
ORDER BY ii.id
LIMIT 100;


-- ============================================================
-- BLOQUE 10: ORDER_ITEM_CUSTOMIZATIONS (100 registros)
-- ============================================================

INSERT INTO order_item_customizations (id, order_item_id, customization_option_id, valor)
SELECT
    gen_random_uuid(),
    oi.id,
    co.id,
    CASE co.clave
        WHEN 'color_flores'    THEN (ARRAY['rojo','rosa','blanco','amarillo','naranja','morado','mixto'])[((row_number() OVER (ORDER BY oi.id)) % 7) + 1]
        WHEN 'tamano'          THEN (ARRAY['pequeño','mediano','grande'])[((row_number() OVER (ORDER BY oi.id)) % 3) + 1]
        WHEN 'incluye_florero' THEN (ARRAY['true','false'])[((row_number() OVER (ORDER BY oi.id)) % 2) + 1]
        WHEN 'mensaje_tarjeta' THEN (ARRAY[
            'Con todo mi amor','Feliz cumpleaños','Te quiero mucho',
            'Gracias por todo','En tu día especial','Con cariño para ti'
        ])[((row_number() OVER (ORDER BY oi.id)) % 6) + 1]
        WHEN 'tipo_envoltura'  THEN (ARRAY['kraft','celofán','tela','sin envoltura'])[((row_number() OVER (ORDER BY oi.id)) % 4) + 1]
        ELSE NULL
    END
FROM (
    SELECT oi.id
    FROM order_items oi
    JOIN orders o ON o.id = oi.order_id
    WHERE o.notas LIKE 'Pedido masivo%'
    ORDER BY oi.id
    LIMIT 100
) oi
JOIN customization_options co ON co.clave IN ('color_flores','tamano','incluye_florero','mensaje_tarjeta','tipo_envoltura')
LIMIT 100;


-- ============================================================
-- BLOQUE 11: AUDIT_LOGS (100 registros)
-- ============================================================

INSERT INTO audit_logs (id, usuario_id, accion, entidad, entidad_id, detalles, fecha_hora)
SELECT
    gen_random_uuid(),
    u.id,
    CASE (gs % 8)
        WHEN 0 THEN 'LOGIN_EXITO'
        WHEN 1 THEN 'CREAR_PEDIDO'
        WHEN 2 THEN 'ACTUALIZAR_PEDIDO'
        WHEN 3 THEN 'CREAR_PRODUCTO'
        WHEN 4 THEN 'ACTUALIZAR_PRODUCTO'
        WHEN 5 THEN 'REGISTRAR_PAGO'
        WHEN 6 THEN 'AJUSTE_INVENTARIO'
        ELSE         'CREAR_CLIENTE'
    END,
    CASE (gs % 5)
        WHEN 0 THEN 'users'
        WHEN 1 THEN 'orders'
        WHEN 2 THEN 'products'
        WHEN 3 THEN 'payments'
        ELSE         'inventory_items'
    END,
    gen_random_uuid()::text,
    json_build_object(
        'ip',      '192.168.' || (gs % 256) || '.' || ((gs * 3) % 256),
        'agente',  CASE (gs % 3) WHEN 0 THEN 'Chrome/Windows' WHEN 1 THEN 'Firefox/Linux' ELSE 'Safari/macOS' END,
        'mensaje', 'Acción registrada automáticamente'
    )::text,
    now() - (gs * interval '6 hours')
FROM generate_series(1, 100) AS gs
CROSS JOIN (SELECT id FROM users WHERE correo = 'admin@floreriabautista.com' LIMIT 1) u;


-- ============================================================
-- BLOQUE 12: BACKUP_JOBS (30 registros)
-- ============================================================

INSERT INTO backup_jobs (id, tipo, estado, usuario_id, creado_en, completado_en, mensaje_error)
SELECT
    gen_random_uuid(),
    CASE (gs % 2) WHEN 0 THEN 'BD' ELSE 'BD_ARCHIVOS' END,
    CASE (gs % 5)
        WHEN 0 THEN 'PENDIENTE'
        WHEN 1 THEN 'EN_PROCESO'
        WHEN 2 THEN 'COMPLETADO'
        WHEN 3 THEN 'COMPLETADO'
        ELSE         'ERROR'
    END,
    u.id,
    now() - (gs * interval '3 days'),
    CASE WHEN (gs % 5) IN (2,3) THEN now() - (gs * interval '3 days') + interval '15 minutes' ELSE NULL END,
    CASE WHEN (gs % 5) = 4 THEN 'Error de conexión al disco destino.' ELSE NULL END
FROM generate_series(1, 30) AS gs
CROSS JOIN (SELECT id FROM users WHERE correo = 'admin@floreriabautista.com' LIMIT 1) u;


-- ============================================================
-- BLOQUE 13: IMPORT_JOBS (30 registros)
-- ============================================================

INSERT INTO import_jobs (id, tipo_importacion, estado, usuario_id, creado_en, completado_en, resumen)
SELECT
    gen_random_uuid(),
    CASE WHEN gs % 2 = 0 THEN 'PRODUCTS' ELSE 'INVENTORY' END,
    CASE (gs % 4)
        WHEN 0 THEN 'PENDIENTE'
        WHEN 1 THEN 'EN_PROCESO'
        WHEN 2 THEN 'COMPLETADO'
        ELSE         'ERROR'
    END,
    u.id,
    now() - (gs * interval '2 days'),
    CASE WHEN (gs % 4) = 2 THEN now() - (gs * interval '2 days') + interval '8 minutes' ELSE NULL END,
    CASE (gs % 4)
        WHEN 2 THEN json_build_object('creados', 10 + gs, 'actualizados', gs % 5, 'errores', 0)::text
        WHEN 3 THEN json_build_object('creados', 0, 'actualizados', 0, 'errores', 1, 'detalle', 'Archivo con formato inválido')::text
        ELSE NULL
    END
FROM generate_series(1, 30) AS gs
CROSS JOIN (SELECT id FROM users WHERE correo = 'admin@floreriabautista.com' LIMIT 1) u;


-- ============================================================
-- FIN DEL SCRIPT DE INSERCIÓN MASIVA
-- Registros insertados por tabla:
--   customers             : 100
--   addresses             : 100
--   products              : 30
--   orders                : 100
--   order_items           : ~200 (2 por pedido)
--   payments              : ~133 (anticipo + liquidación parcial)
--   deliveries            : 100
--   inventory_items       :  30
--   inventory_movements   : 100
--   order_item_customizations: 100
--   audit_logs            : 100
--   backup_jobs           :  30
--   import_jobs           :  30
-- ============================================================


-- Verificar estado actual
SELECT rolname, member::regrole 
FROM pg_auth_members 
JOIN pg_roles ON pg_roles.oid = roleid 
WHERE member::regrole::text = 'app_user';

-- Asignar rol directamente
GRANT app_writer TO app_user;

-- Dar permisos DIRECTAMENTE a app_user (sin depender del rol)
GRANT USAGE ON SCHEMA public TO app_user;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO app_user;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO app_user;

SELECT 'OK: permisos directos aplicados a app_user' AS resultado;



-- ══════════════════════════════════════════════════════════════
-- Módulo de Flores — Materia Prima Interna
-- Ejecutar en pgAdmin sobre floreria_bautista
-- ══════════════════════════════════════════════════════════════

-- Tabla de flores (materia prima)
CREATE TABLE IF NOT EXISTS flowers (
    id              UUID          NOT NULL DEFAULT gen_random_uuid(),
    nombre          VARCHAR(100)  NOT NULL,
    color           VARCHAR(50)   NOT NULL,
    precio_costo    DECIMAL(10,2) NOT NULL DEFAULT 0,
    unidad_medida   VARCHAR(30)   NOT NULL DEFAULT 'TALLO', -- TALLO, PIEZA, RAMO, DOCENA
    stock_actual    INT           NOT NULL DEFAULT 0,
    stock_minimo    INT           NOT NULL DEFAULT 0,
    estado          VARCHAR(20)   NOT NULL DEFAULT 'ACTIVA',
    creado_en       TIMESTAMP     NOT NULL DEFAULT NOW(),
    actualizado_en  TIMESTAMP     NOT NULL DEFAULT NOW(),

    CONSTRAINT pk_flowers PRIMARY KEY (id),
    CONSTRAINT ck_flowers_precio   CHECK (precio_costo >= 0),
    CONSTRAINT ck_flowers_stock    CHECK (stock_actual >= 0),
    CONSTRAINT ck_flowers_estado   CHECK (estado IN ('ACTIVA', 'INACTIVA')),
    CONSTRAINT ck_flowers_unidad   CHECK (unidad_medida IN ('TALLO', 'PIEZA', 'RAMO', 'DOCENA'))
);

-- Movimientos de inventario de flores
CREATE TABLE IF NOT EXISTS flower_movements (
    id          UUID         NOT NULL DEFAULT gen_random_uuid(),
    flower_id   UUID         NOT NULL,
    tipo        VARCHAR(20)  NOT NULL, -- ENTRADA, SALIDA, AJUSTE
    cantidad    INT          NOT NULL,
    motivo      VARCHAR(255),
    usuario_id  UUID         NOT NULL,
    fecha_hora  TIMESTAMP    NOT NULL DEFAULT NOW(),

    CONSTRAINT pk_flower_movements  PRIMARY KEY (id),
    CONSTRAINT fk_fm_flower         FOREIGN KEY (flower_id)  REFERENCES flowers(id) ON DELETE CASCADE,
    CONSTRAINT fk_fm_usuario        FOREIGN KEY (usuario_id) REFERENCES users(id),
    CONSTRAINT ck_fm_tipo           CHECK (tipo IN ('ENTRADA', 'SALIDA', 'AJUSTE')),
    CONSTRAINT ck_fm_cantidad       CHECK (cantidad > 0)
);

-- Relación flores ↔ productos (cuántas flores necesita cada arreglo)
CREATE TABLE IF NOT EXISTS product_flowers (
    product_id  UUID NOT NULL,
    flower_id   UUID NOT NULL,
    cantidad    INT  NOT NULL DEFAULT 1,

    CONSTRAINT pk_product_flowers   PRIMARY KEY (product_id, flower_id),
    CONSTRAINT fk_pf_product        FOREIGN KEY (product_id) REFERENCES products(id) ON DELETE CASCADE,
    CONSTRAINT fk_pf_flower         FOREIGN KEY (flower_id)  REFERENCES flowers(id)  ON DELETE CASCADE,
    CONSTRAINT ck_pf_cantidad       CHECK (cantidad > 0)
);

-- Índices
CREATE INDEX IF NOT EXISTS idx_flowers_nombre    ON flowers(nombre);
CREATE INDEX IF NOT EXISTS idx_flowers_estado    ON flowers(estado);
CREATE INDEX IF NOT EXISTS idx_fm_flower         ON flower_movements(flower_id);
CREATE INDEX IF NOT EXISTS idx_fm_fecha          ON flower_movements(fecha_hora);
CREATE INDEX IF NOT EXISTS idx_pf_product        ON product_flowers(product_id);
CREATE INDEX IF NOT EXISTS idx_pf_flower         ON product_flowers(flower_id);

-- Permisos
GRANT SELECT, INSERT, UPDATE, DELETE ON flowers          TO app_writer;
GRANT SELECT, INSERT, UPDATE, DELETE ON flower_movements TO app_writer;
GRANT SELECT, INSERT, UPDATE, DELETE ON product_flowers  TO app_writer;
GRANT SELECT ON flowers, flower_movements, product_flowers TO app_readonly;

SELECT 'OK: tablas de flores creadas' AS resultado;

-- ══════════════════════════════════════════════════════════════
-- Actualización: Lógica de costos por flores primarias
-- Ejecutar en pgAdmin sobre floreria_bautista
-- ══════════════════════════════════════════════════════════════
 
-- 1. Agregar esFlorPrimaria a flowers
ALTER TABLE flowers
    ADD COLUMN IF NOT EXISTS es_flor_primaria BOOLEAN NOT NULL DEFAULT false;
 
-- 2. Agregar costo_unitario_snapshot a product_flowers
--    (guarda el costo en el momento que se definió la receta)
ALTER TABLE product_flowers
    ADD COLUMN IF NOT EXISTS costo_unitario_snapshot DECIMAL(10,2) NOT NULL DEFAULT 0;
 
-- 3. Agregar campos de costo al producto
ALTER TABLE products
    ADD COLUMN IF NOT EXISTS costo_base      DECIMAL(10,2) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS margen_factor   DECIMAL(5,2)  NOT NULL DEFAULT 2.5,
    ADD COLUMN IF NOT EXISTS precio_sugerido DECIMAL(10,2) NOT NULL DEFAULT 0;
 
SELECT 'OK: columnas de costo agregadas' AS resultado;







-- ══════════════════════════════════════════════════════════════
-- Permisos definitivos — ejecutar una sola vez en floreria_bautista
-- Cubre tablas actuales Y futuras automáticamente
-- ══════════════════════════════════════════════════════════════

-- Permisos en todas las tablas ACTUALES
GRANT ALL PRIVILEGES ON ALL TABLES    IN SCHEMA public TO app_user;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO app_user;
GRANT USAGE ON SCHEMA public TO app_user;

-- Permisos en todas las tablas FUTURAS (cualquier tabla que se cree después)
ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT ALL PRIVILEGES ON TABLES TO app_user;

ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT ALL PRIVILEGES ON SEQUENCES TO app_user;

-- Lo mismo para app_writer por si acaso
GRANT ALL PRIVILEGES ON ALL TABLES    IN SCHEMA public TO app_writer;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO app_writer;
GRANT USAGE ON SCHEMA public TO app_writer;

ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT ALL PRIVILEGES ON TABLES TO app_writer;

ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT ALL PRIVILEGES ON SEQUENCES TO app_writer;

SELECT 'OK: permisos definitivos aplicados' AS resultado;

-- Dar todos los permisos a db_admin sobre tablas actuales y futuras
GRANT ALL PRIVILEGES ON ALL TABLES    IN SCHEMA public TO db_admin;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO db_admin;
GRANT ALL PRIVILEGES ON SCHEMA public TO db_admin;
 
ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT ALL PRIVILEGES ON TABLES TO db_admin;
 
ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT ALL PRIVILEGES ON SEQUENCES TO db_admin;
 
SELECT 'OK: permisos db_admin actualizados' AS resultado;