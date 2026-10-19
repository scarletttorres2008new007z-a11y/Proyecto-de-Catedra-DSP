using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    NombreCompleto = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    CreadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Auditoria",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FechaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Usuario = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Accion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Entidad = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    EntidadId = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Resultado = table.Column<int>(type: "int", nullable: false),
                    Detalle = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Auditoria", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Clientes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    NombreComercial = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    TipoDocumento = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    NumeroDocumento = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Nrc = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    CodActividad = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: true),
                    DescActividad = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Departamento = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    Municipio = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    Complemento = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Telefono = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Correo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    CreadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificadoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clientes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConfiguracionEmisor",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nit = table.Column<string>(type: "nvarchar(14)", maxLength: 14, nullable: false),
                    Nrc = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    CodActividad = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: false),
                    DescActividad = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NombreComercial = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    TipoEstablecimiento = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Departamento = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Municipio = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Complemento = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Telefono = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Correo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CodEstable = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    CodPuntoVenta = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    ModificadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificadoPor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracionEmisor", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Correlativos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Anio = table.Column<int>(type: "int", nullable: false),
                    Ultimo = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Correlativos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Productos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(800)", maxLength: 800, nullable: true),
                    PrecioSinIva = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UnidadMedida = table.Column<int>(type: "int", nullable: false),
                    ControlaInventario = table.Column<bool>(type: "bit", nullable: false),
                    Existencias = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    CreadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificadoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Productos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Respaldos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IniciadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FinalizadoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Usuario = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NombreArchivo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    RutaArchivo = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    TamanoBytes = table.Column<long>(type: "bigint", nullable: true),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    Verificado = table.Column<bool>(type: "bit", nullable: false),
                    Mensaje = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Respaldos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Documentos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    CodigoGeneracion = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NumeroControl = table.Column<string>(type: "nvarchar(31)", maxLength: 31, nullable: false),
                    VersionEsquema = table.Column<int>(type: "int", nullable: false),
                    Ambiente = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Modo = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    EmitidoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaEmision = table.Column<DateOnly>(type: "date", nullable: false),
                    HoraEmision = table.Column<TimeOnly>(type: "time", nullable: false),
                    ClienteId = table.Column<int>(type: "int", nullable: false),
                    ReceptorTipoCliente = table.Column<int>(type: "int", nullable: false),
                    ReceptorNombre = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    ReceptorNombreComercial = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ReceptorTipoDocumento = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ReceptorNumDocumento = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ReceptorNrc = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    ReceptorCodActividad = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: true),
                    ReceptorDescActividad = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ReceptorDepartamento = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ReceptorMunicipio = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ReceptorComplemento = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReceptorTelefono = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ReceptorCorreo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EmisorJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CondicionOperacion = table.Column<int>(type: "int", nullable: false),
                    FormaPago = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    TotalGravada = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalDescuento = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SubTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalIva = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MontoTotalOperacion = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalPagar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalLetras = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DocumentoRelacionadoId = table.Column<int>(type: "int", nullable: true),
                    RelacionadoTipo = table.Column<int>(type: "int", nullable: true),
                    RelacionadoCodigoGeneracion = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RelacionadoFechaEmision = table.Column<DateOnly>(type: "date", nullable: true),
                    Observaciones = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UsuarioEmisor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ClaveIdempotencia = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Documentos_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Documentos_Documentos_DocumentoRelacionadoId",
                        column: x => x.DocumentoRelacionadoId,
                        principalTable: "Documentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Anulaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentoId = table.Column<int>(type: "int", nullable: false),
                    CodigoGeneracion = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    DocumentoReemplazoId = table.Column<int>(type: "int", nullable: true),
                    CodigoGeneracionReemplazo = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NombreResponsable = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TipoDocResponsable = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    NumDocResponsable = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NombreSolicita = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TipoDocSolicita = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    NumDocSolicita = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    Modo = table.Column<int>(type: "int", nullable: false),
                    SolicitadaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResueltaUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MensajeResultado = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Usuario = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Anulaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Anulaciones_Documentos_DocumentoId",
                        column: x => x.DocumentoId,
                        principalTable: "Documentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ArchivosGenerados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentoId = table.Column<int>(type: "int", nullable: false),
                    AnulacionId = table.Column<int>(type: "int", nullable: true),
                    Proposito = table.Column<int>(type: "int", nullable: false),
                    NombreArchivo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    RutaRelativa = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    TamanoBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    GeneradoUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchivosGenerados", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArchivosGenerados_Documentos_DocumentoId",
                        column: x => x.DocumentoId,
                        principalTable: "Documentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DetallesDocumento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentoId = table.Column<int>(type: "int", nullable: false),
                    NumItem = table.Column<int>(type: "int", nullable: false),
                    ProductoId = table.Column<int>(type: "int", nullable: true),
                    Codigo = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    UnidadMedida = table.Column<int>(type: "int", nullable: false),
                    Cantidad = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    PrecioUnitario = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    MontoDescuento = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    VentaGravada = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IvaItem = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DetalleRelacionadoId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DetallesDocumento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DetallesDocumento_Documentos_DocumentoId",
                        column: x => x.DocumentoId,
                        principalTable: "Documentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DetallesDocumento_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EnviosCorreo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentoId = table.Column<int>(type: "int", nullable: false),
                    AnulacionId = table.Column<int>(type: "int", nullable: true),
                    TipoDocumento = table.Column<int>(type: "int", nullable: false),
                    Motivo = table.Column<int>(type: "int", nullable: false),
                    Destinatario = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NumeroIntento = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    CreadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcesadoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MensajeError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IdProveedor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Usuario = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnviosCorreo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnviosCorreo_Documentos_DocumentoId",
                        column: x => x.DocumentoId,
                        principalTable: "Documentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Anulaciones_CodigoGeneracion",
                table: "Anulaciones",
                column: "CodigoGeneracion",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Anulaciones_DocumentoId_Vigente",
                table: "Anulaciones",
                column: "DocumentoId",
                unique: true,
                filter: "[Estado] <> 3");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivosGenerados_AnulacionId",
                table: "ArchivosGenerados",
                column: "AnulacionId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivosGenerados_DocumentoId",
                table: "ArchivosGenerados",
                column: "DocumentoId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivosGenerados_RutaRelativa",
                table: "ArchivosGenerados",
                column: "RutaRelativa",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_Accion",
                table: "Auditoria",
                column: "Accion");

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_EntidadId",
                table: "Auditoria",
                column: "EntidadId");

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_FechaUtc",
                table: "Auditoria",
                column: "FechaUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_Nombre",
                table: "Clientes",
                column: "Nombre");

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_TipoDocumento_NumeroDocumento",
                table: "Clientes",
                columns: new[] { "TipoDocumento", "NumeroDocumento" },
                unique: true,
                filter: "[NumeroDocumento] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Correlativos_Tipo_Anio",
                table: "Correlativos",
                columns: new[] { "Tipo", "Anio" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DetallesDocumento_DetalleRelacionadoId",
                table: "DetallesDocumento",
                column: "DetalleRelacionadoId");

            migrationBuilder.CreateIndex(
                name: "IX_DetallesDocumento_DocumentoId_NumItem",
                table: "DetallesDocumento",
                columns: new[] { "DocumentoId", "NumItem" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DetallesDocumento_ProductoId",
                table: "DetallesDocumento",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_Documentos_ClaveIdempotencia",
                table: "Documentos",
                column: "ClaveIdempotencia",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documentos_ClienteId",
                table: "Documentos",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Documentos_CodigoGeneracion",
                table: "Documentos",
                column: "CodigoGeneracion",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documentos_DocumentoRelacionadoId",
                table: "Documentos",
                column: "DocumentoRelacionadoId");

            migrationBuilder.CreateIndex(
                name: "IX_Documentos_EmitidoUtc",
                table: "Documentos",
                column: "EmitidoUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Documentos_NumeroControl",
                table: "Documentos",
                column: "NumeroControl",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documentos_Tipo_FechaEmision",
                table: "Documentos",
                columns: new[] { "Tipo", "FechaEmision" });

            migrationBuilder.CreateIndex(
                name: "IX_EnviosCorreo_DocumentoId_NumeroIntento",
                table: "EnviosCorreo",
                columns: new[] { "DocumentoId", "NumeroIntento" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EnviosCorreo_Estado",
                table: "EnviosCorreo",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_Productos_Codigo",
                table: "Productos",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Productos_Nombre",
                table: "Productos",
                column: "Nombre");

            migrationBuilder.CreateIndex(
                name: "IX_Respaldos_IniciadoUtc",
                table: "Respaldos",
                column: "IniciadoUtc");

            // El historial de operaciones es de solo inserción: la base de datos rechaza UPDATE y DELETE.
            if (migrationBuilder.IsSqlServer())
            {
                migrationBuilder.Sql("""
                    CREATE TRIGGER [dbo].[TR_Auditoria_SoloInsercion]
                    ON [dbo].[Auditoria]
                    INSTEAD OF UPDATE, DELETE
                    AS
                    BEGIN
                        SET NOCOUNT ON;
                        THROW 51000, 'El historial de operaciones es de solo lectura: no se permite modificar ni eliminar registros.', 1;
                    END
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.IsSqlServer())
                migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_Auditoria_SoloInsercion];");

            migrationBuilder.DropTable(
                name: "Anulaciones");

            migrationBuilder.DropTable(
                name: "ArchivosGenerados");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "Auditoria");

            migrationBuilder.DropTable(
                name: "ConfiguracionEmisor");

            migrationBuilder.DropTable(
                name: "Correlativos");

            migrationBuilder.DropTable(
                name: "DetallesDocumento");

            migrationBuilder.DropTable(
                name: "EnviosCorreo");

            migrationBuilder.DropTable(
                name: "Respaldos");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "Productos");

            migrationBuilder.DropTable(
                name: "Documentos");

            migrationBuilder.DropTable(
                name: "Clientes");
        }
    }
}
