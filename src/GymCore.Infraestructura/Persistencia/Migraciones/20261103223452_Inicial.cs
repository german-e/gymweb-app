using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymCore.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AvisosRecepcion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Tipo = table.Column<int>(type: "INTEGER", nullable: false),
                    Titulo = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Texto = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    ImagenUrl = table.Column<string>(type: "TEXT", maxLength: 260, nullable: true),
                    FechaEvento = table.Column<DateTime>(type: "TEXT", nullable: true),
                    VigenteDesde = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    VigenteHasta = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Activo = table.Column<bool>(type: "INTEGER", nullable: false),
                    Orden = table.Column<int>(type: "INTEGER", nullable: false),
                    FechaAlta = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AvisosRecepcion", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CategoriasProducto",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Descripcion = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Icono = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Activa = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoriasProducto", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConfiguracionGimnasio",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NombreComercial = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Eslogan = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    LogoUrl = table.Column<string>(type: "TEXT", maxLength: 260, nullable: true),
                    LogoReducidoUrl = table.Column<string>(type: "TEXT", maxLength: 260, nullable: true),
                    FaviconUrl = table.Column<string>(type: "TEXT", maxLength: 260, nullable: true),
                    ImagenBienvenidaUrl = table.Column<string>(type: "TEXT", maxLength: 260, nullable: true),
                    ColorPrincipal = table.Column<string>(type: "TEXT", maxLength: 7, nullable: false),
                    ColorSecundario = table.Column<string>(type: "TEXT", maxLength: 7, nullable: false),
                    Direccion = table.Column<string>(type: "TEXT", maxLength: 160, nullable: true),
                    Telefono = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    SitioWeb = table.Column<string>(type: "TEXT", maxLength: 160, nullable: true),
                    TextoCredencial = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    KioscoMensaje = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    KioscoSegundosResultado = table.Column<int>(type: "INTEGER", nullable: false),
                    KioscoSonido = table.Column<bool>(type: "INTEGER", nullable: false),
                    KioscoPantallaEspera = table.Column<bool>(type: "INTEGER", nullable: false),
                    KioscoSegundosInactividad = table.Column<int>(type: "INTEGER", nullable: false),
                    KioscoSegundosRotacion = table.Column<int>(type: "INTEGER", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UsuarioModifico = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracionGimnasio", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Entrenadores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Apellido = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Dni = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    FechaNacimiento = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Sexo = table.Column<int>(type: "INTEGER", nullable: false),
                    FotoUrl = table.Column<string>(type: "TEXT", maxLength: 260, nullable: true),
                    Telefono = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    Celular = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 160, nullable: true),
                    Direccion = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Especialidad = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    Certificaciones = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    InstitucionFormadora = table.Column<string>(type: "TEXT", maxLength: 160, nullable: true),
                    AniosExperiencia = table.Column<int>(type: "INTEGER", nullable: true),
                    DescripcionProfesional = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Observaciones = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    DiasTrabajo = table.Column<int>(type: "INTEGER", nullable: false),
                    HorarioDesde = table.Column<TimeOnly>(type: "TEXT", nullable: true),
                    HorarioHasta = table.Column<TimeOnly>(type: "TEXT", nullable: true),
                    Turno = table.Column<int>(type: "INTEGER", nullable: true),
                    Activo = table.Column<bool>(type: "INTEGER", nullable: false),
                    FechaAlta = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaBaja = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Entrenadores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EventosSistema",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FechaUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Usuario = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    UsuarioId = table.Column<int>(type: "INTEGER", nullable: true),
                    Tipo = table.Column<int>(type: "INTEGER", nullable: false),
                    Entidad = table.Column<int>(type: "INTEGER", nullable: false),
                    EntidadId = table.Column<int>(type: "INTEGER", nullable: true),
                    EntidadNombre = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SocioId = table.Column<int>(type: "INTEGER", nullable: true),
                    Resumen = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Detalle = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventosSistema", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Planes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Descripcion = table.Column<string>(type: "TEXT", nullable: true),
                    Tipo = table.Column<int>(type: "INTEGER", nullable: false),
                    Precio = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Moneda = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    MaxDiasPorSemana = table.Column<int>(type: "INTEGER", nullable: true),
                    CuentaFeriados = table.Column<bool>(type: "INTEGER", nullable: false),
                    DuracionDias = table.Column<int>(type: "INTEGER", nullable: false),
                    RequiereRenovacionAutomatica = table.Column<bool>(type: "INTEGER", nullable: false),
                    PermiteAccesoSabado = table.Column<bool>(type: "INTEGER", nullable: false),
                    PermiteAccesoDomingo = table.Column<bool>(type: "INTEGER", nullable: false),
                    Activo = table.Column<bool>(type: "INTEGER", nullable: false),
                    FechaAlta = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Planes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Descripcion = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    EsSistema = table.Column<bool>(type: "INTEGER", nullable: false),
                    AccesoTotal = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Socios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Dni = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    TarjetaCodigo = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    Apellido = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    FechaAlta = table.Column<DateTime>(type: "TEXT", nullable: false),
                    NumeroSocio = table.Column<int>(type: "INTEGER", nullable: true),
                    FotoUrl = table.Column<string>(type: "TEXT", maxLength: 260, nullable: true),
                    FechaNacimiento = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Sexo = table.Column<int>(type: "INTEGER", nullable: true),
                    Telefono = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 160, nullable: true),
                    Direccion = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ContactoEmergenciaNombre = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    ContactoEmergenciaTelefono = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    FechaBaja = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Socios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Productos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Codigo = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    CodigoBarras = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    CategoriaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Marca = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    Descripcion = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    PrecioCompra = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PrecioVenta = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    StockMinimo = table.Column<int>(type: "INTEGER", nullable: false),
                    Activo = table.Column<bool>(type: "INTEGER", nullable: false),
                    Destacado = table.Column<bool>(type: "INTEGER", nullable: false),
                    FechaAlta = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StockActual = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Productos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Productos_CategoriasProducto_CategoriaId",
                        column: x => x.CategoriaId,
                        principalTable: "CategoriasProducto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EntrenadorEventos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EntrenadorId = table.Column<int>(type: "INTEGER", nullable: false),
                    Fecha = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Tipo = table.Column<int>(type: "INTEGER", nullable: false),
                    Titulo = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Detalle = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Usuario = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntrenadorEventos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntrenadorEventos_Entrenadores_EntrenadorId",
                        column: x => x.EntrenadorId,
                        principalTable: "Entrenadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RolPermisos",
                columns: table => new
                {
                    RolId = table.Column<int>(type: "INTEGER", nullable: false),
                    Permiso = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolPermisos", x => new { x.RolId, x.Permiso });
                    table.ForeignKey(
                        name: "FK_RolPermisos_Roles_RolId",
                        column: x => x.RolId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NombreCompleto = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    NombreUsuario = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 160, nullable: true),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    RolId = table.Column<int>(type: "INTEGER", nullable: false),
                    EntrenadorId = table.Column<int>(type: "INTEGER", nullable: true),
                    Activo = table.Column<bool>(type: "INTEGER", nullable: false),
                    FechaAlta = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UltimoAcceso = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AccesoAnterior = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IntentosFallidos = table.Column<int>(type: "INTEGER", nullable: false),
                    BloqueadoHasta = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DebeCambiarPassword = table.Column<bool>(type: "INTEGER", nullable: false),
                    SelloSeguridad = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Usuarios_Entrenadores_EntrenadorId",
                        column: x => x.EntrenadorId,
                        principalTable: "Entrenadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Usuarios_Roles_RolId",
                        column: x => x.RolId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EntrenadorSocios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EntrenadorId = table.Column<int>(type: "INTEGER", nullable: false),
                    SocioId = table.Column<int>(type: "INTEGER", nullable: false),
                    FechaAsignacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaFin = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Activa = table.Column<bool>(type: "INTEGER", nullable: false),
                    Objetivo = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntrenadorSocios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntrenadorSocios_Entrenadores_EntrenadorId",
                        column: x => x.EntrenadorId,
                        principalTable: "Entrenadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EntrenadorSocios_Socios_SocioId",
                        column: x => x.SocioId,
                        principalTable: "Socios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MedicionesFisicas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SocioId = table.Column<int>(type: "INTEGER", nullable: false),
                    Fecha = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PesoKg = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    AlturaCm = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    MasaMuscularKg = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    PorcentajeGraso = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    Observaciones = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    EntrenadorId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicionesFisicas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MedicionesFisicas_Entrenadores_EntrenadorId",
                        column: x => x.EntrenadorId,
                        principalTable: "Entrenadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MedicionesFisicas_Socios_SocioId",
                        column: x => x.SocioId,
                        principalTable: "Socios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SocioPlanes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SocioId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlanId = table.Column<int>(type: "INTEGER", nullable: false),
                    FechaInicio = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaFin = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ProximoVencimiento = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Estado = table.Column<int>(type: "INTEGER", nullable: false),
                    ConteoSemanalUsos = table.Column<int>(type: "INTEGER", nullable: true),
                    SemanaConteoDesde = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SocioPlanes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SocioPlanes_Planes_PlanId",
                        column: x => x.PlanId,
                        principalTable: "Planes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SocioPlanes_Socios_SocioId",
                        column: x => x.SocioId,
                        principalTable: "Socios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Ventas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Fecha = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SocioId = table.Column<int>(type: "INTEGER", nullable: true),
                    ClienteNombre = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    Medio = table.Column<int>(type: "INTEGER", nullable: false),
                    Canal = table.Column<int>(type: "INTEGER", nullable: false),
                    Observaciones = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Usuario = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    UsuarioId = table.Column<int>(type: "INTEGER", nullable: true),
                    TotalProductos = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Anulada = table.Column<bool>(type: "INTEGER", nullable: false),
                    FechaAnulacion = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MotivoAnulacion = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    UsuarioAnulo = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ventas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ventas_Socios_SocioId",
                        column: x => x.SocioId,
                        principalTable: "Socios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Asistencias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SocioId = table.Column<int>(type: "INTEGER", nullable: false),
                    FechaHoraIngreso = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SocioPlanId = table.Column<int>(type: "INTEGER", nullable: true),
                    Observaciones = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Asistencias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Asistencias_SocioPlanes_SocioPlanId",
                        column: x => x.SocioPlanId,
                        principalTable: "SocioPlanes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Asistencias_Socios_SocioId",
                        column: x => x.SocioId,
                        principalTable: "Socios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MovimientosStock",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Fecha = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Tipo = table.Column<int>(type: "INTEGER", nullable: false),
                    Cantidad = table.Column<int>(type: "INTEGER", nullable: false),
                    StockResultante = table.Column<int>(type: "INTEGER", nullable: false),
                    CostoUnitario = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Motivo = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    Usuario = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    UsuarioId = table.Column<int>(type: "INTEGER", nullable: true),
                    VentaId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientosStock", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimientosStock_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimientosStock_Ventas_VentaId",
                        column: x => x.VentaId,
                        principalTable: "Ventas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Pagos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SocioId = table.Column<int>(type: "INTEGER", nullable: false),
                    SocioPlanId = table.Column<int>(type: "INTEGER", nullable: true),
                    Importe = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Moneda = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    FechaPago = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Medio = table.Column<int>(type: "INTEGER", nullable: false),
                    ReferenciaExterna = table.Column<string>(type: "TEXT", nullable: true),
                    Observaciones = table.Column<string>(type: "TEXT", nullable: true),
                    UsuarioCajero = table.Column<string>(type: "TEXT", nullable: true),
                    VentaId = table.Column<int>(type: "INTEGER", nullable: true),
                    Anulado = table.Column<bool>(type: "INTEGER", nullable: false),
                    FechaAnulacion = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MotivoAnulacion = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    UsuarioAnulo = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pagos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pagos_SocioPlanes_SocioPlanId",
                        column: x => x.SocioPlanId,
                        principalTable: "SocioPlanes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Pagos_Socios_SocioId",
                        column: x => x.SocioId,
                        principalTable: "Socios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pagos_Ventas_VentaId",
                        column: x => x.VentaId,
                        principalTable: "Ventas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VentaItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    VentaId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Descripcion = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Cantidad = table.Column<int>(type: "INTEGER", nullable: false),
                    PrecioUnitario = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CostoUnitario = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VentaItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VentaItems_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VentaItems_Ventas_VentaId",
                        column: x => x.VentaId,
                        principalTable: "Ventas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Asistencias_FechaHoraIngreso",
                table: "Asistencias",
                column: "FechaHoraIngreso");

            migrationBuilder.CreateIndex(
                name: "IX_Asistencias_SocioId_FechaHoraIngreso",
                table: "Asistencias",
                columns: new[] { "SocioId", "FechaHoraIngreso" });

            migrationBuilder.CreateIndex(
                name: "IX_Asistencias_SocioPlanId",
                table: "Asistencias",
                column: "SocioPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_AvisosRecepcion_Activo_Orden",
                table: "AvisosRecepcion",
                columns: new[] { "Activo", "Orden" });

            migrationBuilder.CreateIndex(
                name: "IX_CategoriasProducto_Nombre",
                table: "CategoriasProducto",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Entrenadores_Activo",
                table: "Entrenadores",
                column: "Activo");

            migrationBuilder.CreateIndex(
                name: "IX_Entrenadores_Dni",
                table: "Entrenadores",
                column: "Dni",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EntrenadorEventos_EntrenadorId_Fecha",
                table: "EntrenadorEventos",
                columns: new[] { "EntrenadorId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_EntrenadorSocios_EntrenadorId_SocioId",
                table: "EntrenadorSocios",
                columns: new[] { "EntrenadorId", "SocioId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EntrenadorSocios_SocioId",
                table: "EntrenadorSocios",
                column: "SocioId");

            migrationBuilder.CreateIndex(
                name: "IX_EventosSistema_Entidad_EntidadId",
                table: "EventosSistema",
                columns: new[] { "Entidad", "EntidadId" });

            migrationBuilder.CreateIndex(
                name: "IX_EventosSistema_FechaUtc",
                table: "EventosSistema",
                column: "FechaUtc");

            migrationBuilder.CreateIndex(
                name: "IX_EventosSistema_SocioId",
                table: "EventosSistema",
                column: "SocioId");

            migrationBuilder.CreateIndex(
                name: "IX_EventosSistema_UsuarioId",
                table: "EventosSistema",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicionesFisicas_EntrenadorId",
                table: "MedicionesFisicas",
                column: "EntrenadorId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicionesFisicas_SocioId_Fecha",
                table: "MedicionesFisicas",
                columns: new[] { "SocioId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosStock_Fecha",
                table: "MovimientosStock",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosStock_ProductoId_Fecha",
                table: "MovimientosStock",
                columns: new[] { "ProductoId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosStock_VentaId",
                table: "MovimientosStock",
                column: "VentaId");

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_FechaPago",
                table: "Pagos",
                column: "FechaPago");

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_SocioId",
                table: "Pagos",
                column: "SocioId");

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_SocioPlanId",
                table: "Pagos",
                column: "SocioPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_VentaId",
                table: "Pagos",
                column: "VentaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Productos_CategoriaId",
                table: "Productos",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_Productos_Codigo",
                table: "Productos",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Productos_CodigoBarras",
                table: "Productos",
                column: "CodigoBarras",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Nombre",
                table: "Roles",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SocioPlanes_PlanId",
                table: "SocioPlanes",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_SocioPlanes_SocioId",
                table: "SocioPlanes",
                column: "SocioId");

            migrationBuilder.CreateIndex(
                name: "IX_Socios_Dni",
                table: "Socios",
                column: "Dni");

            migrationBuilder.CreateIndex(
                name: "IX_Socios_NumeroSocio",
                table: "Socios",
                column: "NumeroSocio",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Socios_TarjetaCodigo",
                table: "Socios",
                column: "TarjetaCodigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_EntrenadorId",
                table: "Usuarios",
                column: "EntrenadorId");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_NombreUsuario",
                table: "Usuarios",
                column: "NombreUsuario",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_RolId",
                table: "Usuarios",
                column: "RolId");

            migrationBuilder.CreateIndex(
                name: "IX_VentaItems_ProductoId",
                table: "VentaItems",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_VentaItems_VentaId",
                table: "VentaItems",
                column: "VentaId");

            migrationBuilder.CreateIndex(
                name: "IX_Ventas_Fecha",
                table: "Ventas",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_Ventas_SocioId",
                table: "Ventas",
                column: "SocioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Asistencias");

            migrationBuilder.DropTable(
                name: "AvisosRecepcion");

            migrationBuilder.DropTable(
                name: "ConfiguracionGimnasio");

            migrationBuilder.DropTable(
                name: "EntrenadorEventos");

            migrationBuilder.DropTable(
                name: "EntrenadorSocios");

            migrationBuilder.DropTable(
                name: "EventosSistema");

            migrationBuilder.DropTable(
                name: "MedicionesFisicas");

            migrationBuilder.DropTable(
                name: "MovimientosStock");

            migrationBuilder.DropTable(
                name: "Pagos");

            migrationBuilder.DropTable(
                name: "RolPermisos");

            migrationBuilder.DropTable(
                name: "Usuarios");

            migrationBuilder.DropTable(
                name: "VentaItems");

            migrationBuilder.DropTable(
                name: "SocioPlanes");

            migrationBuilder.DropTable(
                name: "Entrenadores");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Productos");

            migrationBuilder.DropTable(
                name: "Ventas");

            migrationBuilder.DropTable(
                name: "Planes");

            migrationBuilder.DropTable(
                name: "CategoriasProducto");

            migrationBuilder.DropTable(
                name: "Socios");
        }
    }
}
