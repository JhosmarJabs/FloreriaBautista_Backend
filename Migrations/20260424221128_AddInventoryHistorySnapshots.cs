using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FloreriaBautista.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryHistorySnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.CreateTable(
                name: "catalogos",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "ACTIVA"),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    imagen_url = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    actualizado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_catalogos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "categories",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "ACTIVA"),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    imagen_url = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    actualizado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "customization_options",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    clave = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    opciones = table.Column<string>(type: "text", nullable: true),
                    precio_adicional = table.Column<decimal>(type: "numeric", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customization_options", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "inventory_items",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    stock_actual = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    stock_minimo = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    sucursal = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    precio_costo = table.Column<decimal>(type: "numeric(10,2)", nullable: false, defaultValue: 0m),
                    es_flor_primaria = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    suma_al_costo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    unidad_medida = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    imagen_url = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventory_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "products",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    descripcion = table.Column<string>(type: "text", nullable: false),
                    precio_base = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    tipo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    es_personalizable = table.Column<bool>(type: "boolean", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "ACTIVO"),
                    visibilidad = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "AMBOS"),
                    imagen_url = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    actualizado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_products", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "scheduler_settings",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    backup_automatico_activo = table.Column<bool>(type: "boolean", nullable: false),
                    frecuencia = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "SEMANAL"),
                    dia_semana = table.Column<int>(type: "integer", nullable: false),
                    hora = table.Column<int>(type: "integer", nullable: false),
                    mantenimiento_activo = table.Column<bool>(type: "boolean", nullable: false),
                    frecuencia_mantenimiento = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "SEMANAL"),
                    dia_semana_mantenimiento = table.Column<int>(type: "integer", nullable: false),
                    hora_mantenimiento = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    actualizado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scheduler_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    apellido = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    correo = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    telefono = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    sexo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    fecha_nacimiento = table.Column<DateOnly>(type: "date", nullable: true),
                    password_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    es_cliente = table.Column<bool>(type: "boolean", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "ACTIVO"),
                    correo_verificado = table.Column<bool>(type: "boolean", nullable: false),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    actualizado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "inventory_daily_snapshot",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    inventory_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    stock_final = table.Column<int>(type: "integer", nullable: false),
                    cantidad_vendida = table.Column<int>(type: "integer", nullable: false),
                    cantidad_recibida = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventory_daily_snapshot", x => x.id);
                    table.ForeignKey(
                        name: "fk_inventory_daily_snapshot_inventory_item_inventory_item_id",
                        column: x => x.inventory_item_id,
                        principalSchema: "public",
                        principalTable: "inventory_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_catalogos",
                schema: "public",
                columns: table => new
                {
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    catalogo_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_product_catalogos", x => new { x.product_id, x.catalogo_id });
                    table.ForeignKey(
                        name: "fk_product_catalogos_catalogos_catalogo_id",
                        column: x => x.catalogo_id,
                        principalSchema: "public",
                        principalTable: "catalogos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_product_catalogos_products_product_id",
                        column: x => x.product_id,
                        principalSchema: "public",
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_categories",
                schema: "public",
                columns: table => new
                {
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_product_categories", x => new { x.product_id, x.category_id });
                    table.ForeignKey(
                        name: "fk_product_categories_categories_category_id",
                        column: x => x.category_id,
                        principalSchema: "public",
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_product_categories_products_product_id",
                        column: x => x.product_id,
                        principalSchema: "public",
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_customization_options",
                schema: "public",
                columns: table => new
                {
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customization_option_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_product_customization_options", x => new { x.product_id, x.customization_option_id });
                    table.ForeignKey(
                        name: "fk_product_customization_options_customization_options_customi",
                        column: x => x.customization_option_id,
                        principalSchema: "public",
                        principalTable: "customization_options",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_product_customization_options_products_product_id",
                        column: x => x.product_id,
                        principalSchema: "public",
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_recipes",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inventory_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cantidad_requerida = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_product_recipes", x => x.id);
                    table.ForeignKey(
                        name: "fk_product_recipes_inventory_items_inventory_item_id",
                        column: x => x.inventory_item_id,
                        principalSchema: "public",
                        principalTable: "inventory_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_product_recipes_products_product_id",
                        column: x => x.product_id,
                        principalSchema: "public",
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    accion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    entidad = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    entidad_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    detalles = table.Column<string>(type: "text", nullable: true),
                    fecha_hora = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_audit_logs_user_usuario_id",
                        column: x => x.usuario_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "auth_tokens",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    expira_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    usado = table.Column<bool>(type: "boolean", nullable: false),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auth_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_auth_tokens_user_user_id",
                        column: x => x.user_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "backup_jobs",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "PENDIENTE"),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nombre_tabla = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    formato = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "BACKUP"),
                    drive_file_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    tamano_bytes = table.Column<long>(type: "bigint", nullable: true),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    completado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    mensaje_error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_backup_jobs", x => x.id);
                    table.ForeignKey(
                        name: "fk_backup_jobs_user_usuario_id",
                        column: x => x.usuario_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "customers",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tipo_cliente = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    apellido = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    telefono = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    correo = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    sexo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    fecha_nacimiento = table.Column<DateOnly>(type: "date", nullable: true),
                    rfc = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    razon_social = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    cp_fiscal = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    regimen_fiscal = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customers", x => x.id);
                    table.ForeignKey(
                        name: "fk_customers_user_user_id",
                        column: x => x.user_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "import_jobs",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tipo_importacion = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "PENDIENTE"),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    completado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resumen = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_import_jobs", x => x.id);
                    table.ForeignKey(
                        name: "fk_import_jobs_user_usuario_id",
                        column: x => x.usuario_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "inventory_movements",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    inventory_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_movimiento = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    cantidad = table.Column<int>(type: "integer", nullable: false),
                    motivo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha_hora = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventory_movements", x => x.id);
                    table.ForeignKey(
                        name: "fk_inventory_movements_inventory_items_inventory_item_id",
                        column: x => x.inventory_item_id,
                        principalSchema: "public",
                        principalTable: "inventory_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_inventory_movements_user_usuario_id",
                        column: x => x.usuario_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                schema: "public",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_roles", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "fk_user_roles_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "public",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_roles_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "addresses",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    etiqueta = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    calle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    colonia = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    municipio = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    estado = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    cp = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    referencias = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_addresses", x => x.id);
                    table.ForeignKey(
                        name: "fk_addresses_customer_customer_id",
                        column: x => x.customer_id,
                        principalSchema: "public",
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "orders",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    id_local_offline = table.Column<Guid>(type: "uuid", nullable: true),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_pedido = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    canal = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    estado_pedido = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "PENDIENTE_VALIDACION"),
                    direccion_entrega_calle = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    direccion_entrega_colonia = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    direccion_entrega_municipio = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    direccion_entrega_estado = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    direccion_entrega_cp = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    direccion_entrega_referencias = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    costo_envio = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    fecha_creacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    fecha_entrega = table.Column<DateOnly>(type: "date", nullable: false),
                    hora_entrega = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    total = table.Column<decimal>(type: "numeric(10,2)", nullable: false, defaultValue: 0m),
                    saldo_pendiente = table.Column<decimal>(type: "numeric(10,2)", nullable: false, defaultValue: 0m),
                    sincronizado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    notas = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_orders", x => x.id);
                    table.ForeignKey(
                        name: "fk_orders_customers_customer_id",
                        column: x => x.customer_id,
                        principalSchema: "public",
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "deliveries",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    repartidor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fecha_programada = table.Column<DateOnly>(type: "date", nullable: false),
                    hora_programada = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    estado_entrega = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    fecha_real = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    evidencia_fotografica = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    firma_receptor = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    notas = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_deliveries", x => x.id);
                    table.ForeignKey(
                        name: "fk_deliveries_order_order_id",
                        column: x => x.order_id,
                        principalSchema: "public",
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_deliveries_user_repartidor_id",
                        column: x => x.repartidor_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "order_items",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cantidad = table.Column<int>(type: "integer", nullable: false),
                    precio_unitario = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(10,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_order_items_orders_order_id",
                        column: x => x.order_id,
                        principalSchema: "public",
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_order_items_product_product_id",
                        column: x => x.product_id,
                        principalSchema: "public",
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    monto = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    tipo_pago = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    metodo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fecha_pago = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "REGISTRADO")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payments", x => x.id);
                    table.ForeignKey(
                        name: "fk_payments_orders_order_id",
                        column: x => x.order_id,
                        principalSchema: "public",
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "order_item_customizations",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customization_option_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order_item_customizations", x => x.id);
                    table.ForeignKey(
                        name: "fk_order_item_customizations_customization_options_customizati",
                        column: x => x.customization_option_id,
                        principalSchema: "public",
                        principalTable: "customization_options",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_order_item_customizations_order_items_order_item_id",
                        column: x => x.order_item_id,
                        principalSchema: "public",
                        principalTable: "order_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_addresses_customer_id",
                schema: "public",
                table: "addresses",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_entidad_entidad_id",
                schema: "public",
                table: "audit_logs",
                columns: new[] { "entidad", "entidad_id" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_fecha_hora",
                schema: "public",
                table: "audit_logs",
                column: "fecha_hora");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_usuario_id",
                schema: "public",
                table: "audit_logs",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_auth_tokens_token",
                schema: "public",
                table: "auth_tokens",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_auth_tokens_user_id",
                schema: "public",
                table: "auth_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_backup_jobs_usuario_id",
                schema: "public",
                table: "backup_jobs",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_customers_telefono",
                schema: "public",
                table: "customers",
                column: "telefono");

            migrationBuilder.CreateIndex(
                name: "ix_customers_user_id",
                schema: "public",
                table: "customers",
                column: "user_id",
                unique: true,
                filter: "user_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_deliveries_order_id",
                schema: "public",
                table: "deliveries",
                column: "order_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_deliveries_repartidor_id",
                schema: "public",
                table: "deliveries",
                column: "repartidor_id");

            migrationBuilder.CreateIndex(
                name: "ix_import_jobs_usuario_id",
                schema: "public",
                table: "import_jobs",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_daily_snapshot_inventory_item_id",
                schema: "public",
                table: "inventory_daily_snapshot",
                column: "inventory_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_movements_inventory_item_id",
                schema: "public",
                table: "inventory_movements",
                column: "inventory_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_movements_usuario_id",
                schema: "public",
                table: "inventory_movements",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_item_customizations_customization_option_id",
                schema: "public",
                table: "order_item_customizations",
                column: "customization_option_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_item_customizations_order_item_id",
                schema: "public",
                table: "order_item_customizations",
                column: "order_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_items_order_id",
                schema: "public",
                table: "order_items",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_items_product_id",
                schema: "public",
                table: "order_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_orders_customer_id",
                schema: "public",
                table: "orders",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_orders_id_local_offline",
                schema: "public",
                table: "orders",
                column: "id_local_offline",
                unique: true,
                filter: "id_local_offline IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_payments_order_id",
                schema: "public",
                table: "payments",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_catalogos_catalogo_id",
                schema: "public",
                table: "product_catalogos",
                column: "catalogo_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_categories_category_id",
                schema: "public",
                table: "product_categories",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_customization_options_customization_option_id",
                schema: "public",
                table: "product_customization_options",
                column: "customization_option_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_recipes_inventory_item_id",
                schema: "public",
                table: "product_recipes",
                column: "inventory_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_recipes_product_id_inventory_item_id",
                schema: "public",
                table: "product_recipes",
                columns: new[] { "product_id", "inventory_item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_roles_nombre",
                schema: "public",
                table: "roles",
                column: "nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_roles_role_id",
                schema: "public",
                table: "user_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_correo",
                schema: "public",
                table: "users",
                column: "correo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "addresses",
                schema: "public");

            migrationBuilder.DropTable(
                name: "audit_logs",
                schema: "public");

            migrationBuilder.DropTable(
                name: "auth_tokens",
                schema: "public");

            migrationBuilder.DropTable(
                name: "backup_jobs",
                schema: "public");

            migrationBuilder.DropTable(
                name: "deliveries",
                schema: "public");

            migrationBuilder.DropTable(
                name: "import_jobs",
                schema: "public");

            migrationBuilder.DropTable(
                name: "inventory_daily_snapshot",
                schema: "public");

            migrationBuilder.DropTable(
                name: "inventory_movements",
                schema: "public");

            migrationBuilder.DropTable(
                name: "order_item_customizations",
                schema: "public");

            migrationBuilder.DropTable(
                name: "payments",
                schema: "public");

            migrationBuilder.DropTable(
                name: "product_catalogos",
                schema: "public");

            migrationBuilder.DropTable(
                name: "product_categories",
                schema: "public");

            migrationBuilder.DropTable(
                name: "product_customization_options",
                schema: "public");

            migrationBuilder.DropTable(
                name: "product_recipes",
                schema: "public");

            migrationBuilder.DropTable(
                name: "scheduler_settings",
                schema: "public");

            migrationBuilder.DropTable(
                name: "user_roles",
                schema: "public");

            migrationBuilder.DropTable(
                name: "order_items",
                schema: "public");

            migrationBuilder.DropTable(
                name: "catalogos",
                schema: "public");

            migrationBuilder.DropTable(
                name: "categories",
                schema: "public");

            migrationBuilder.DropTable(
                name: "customization_options",
                schema: "public");

            migrationBuilder.DropTable(
                name: "inventory_items",
                schema: "public");

            migrationBuilder.DropTable(
                name: "roles",
                schema: "public");

            migrationBuilder.DropTable(
                name: "orders",
                schema: "public");

            migrationBuilder.DropTable(
                name: "products",
                schema: "public");

            migrationBuilder.DropTable(
                name: "customers",
                schema: "public");

            migrationBuilder.DropTable(
                name: "users",
                schema: "public");
        }
    }
}
