using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Acity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.CreateTable(
                name: "Rol",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rol", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Usuario",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: false),
                    RolId = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuario", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Usuario_Rol",
                        column: x => x.RolId,
                        principalSchema: "dbo",
                        principalTable: "Rol",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CargaArchivo",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NombreArchivo = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    FechaRecepcion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RutaArchivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Estado = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    FechaFin = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IntentosProceso = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                        .Annotation("Relational:DefaultConstraintName", "DF_CargaArchivo_IntentosProceso")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CargaArchivo", x => x.Id);
                    table.CheckConstraint("CK_CargaArchivo_Estado", "[Estado] IN ('Pendiente','En proceso','Cargado','Finalizado','Notificado','Fallido')");
                    table.ForeignKey(
                        name: "FK_CargaArchivo_Usuario",
                        column: x => x.UsuarioId,
                        principalSchema: "dbo",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CargaEstadoHistorial",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CargaId = table.Column<long>(type: "bigint", nullable: false),
                    Estado = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    FechaCambio = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CargaEstadoHistorial", x => x.Id);
                    table.CheckConstraint("CK_CargaEstadoHistorial_Estado", "[Estado] IN ('Pendiente','En proceso','Cargado','Finalizado','Notificado','Fallido')");
                    table.ForeignKey(
                        name: "FK_CargaEstadoHistorial_Carga",
                        column: x => x.CargaId,
                        principalSchema: "dbo",
                        principalTable: "CargaArchivo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CargaPeriodo",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CargaId = table.Column<long>(type: "bigint", nullable: false),
                    Periodo = table.Column<string>(type: "char(7)", unicode: false, fixedLength: true, maxLength: 7, nullable: false),
                    Resultado = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    PeriodoReservado = table.Column<string>(type: "char(7)", unicode: false, fixedLength: true, maxLength: 7, nullable: true),
                    FechaResultado = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CargaPeriodo", x => x.Id);
                    table.CheckConstraint("CK_CargaPeriodo_Periodo", "[Periodo] LIKE '[0-9][0-9][0-9][0-9]-[0-9][0-9]' AND SUBSTRING([Periodo], 6, 2) BETWEEN '01' AND '12'");
                    table.CheckConstraint("CK_CargaPeriodo_Reserva", "([Resultado] IN ('Admitido','Completado') AND [PeriodoReservado] IS NOT NULL AND [PeriodoReservado] = [Periodo]) OR ([Resultado] NOT IN ('Admitido','Completado') AND [PeriodoReservado] IS NULL)");
                    table.CheckConstraint("CK_CargaPeriodo_Resultado", "[Resultado] IN ('Admitido','Completado','Rechazado','Bloqueado','Fallido','NoProcesado')");
                    table.ForeignKey(
                        name: "FK_CargaPeriodo_Carga",
                        column: x => x.CargaId,
                        principalSchema: "dbo",
                        principalTable: "CargaArchivo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Incidencia",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CargaId = table.Column<long>(type: "bigint", nullable: false),
                    CargaPeriodoId = table.Column<long>(type: "bigint", nullable: true),
                    Tipo = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    NumeroFila = table.Column<int>(type: "int", nullable: true),
                    CodigoProducto = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PeriodoTexto = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Motivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Incidencia", x => x.Id);
                    table.CheckConstraint("CK_Incidencia_Tipo", "[Tipo] IN ('PeriodoRechazado','PeriodoBloqueado','FilaOmitida','Existente','NoProcesado','ErrorTecnico','ErrorNotificacion')");
                    table.ForeignKey(
                        name: "FK_Incidencia_Carga",
                        column: x => x.CargaId,
                        principalSchema: "dbo",
                        principalTable: "CargaArchivo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Incidencia_CargaPeriodo",
                        column: x => x.CargaPeriodoId,
                        principalSchema: "dbo",
                        principalTable: "CargaPeriodo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Producto",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CodigoProducto = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_CI_AS_WS"),
                    NombreProducto = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Precio = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CargaPeriodoId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Producto", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Producto_CargaPeriodo",
                        column: x => x.CargaPeriodoId,
                        principalSchema: "dbo",
                        principalTable: "CargaPeriodo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "Rol",
                columns: new[] { "Id", "Nombre" },
                values: new object[,]
                {
                    { (short)1, "Administrador" },
                    { (short)2, "Operador" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_CargaArchivo_Usuario_FechaRecepcion",
                schema: "dbo",
                table: "CargaArchivo",
                columns: new[] { "UsuarioId", "FechaRecepcion" });

            migrationBuilder.CreateIndex(
                name: "IX_CargaEstadoHistorial_Carga_Id",
                schema: "dbo",
                table: "CargaEstadoHistorial",
                columns: new[] { "CargaId", "Id" });

            migrationBuilder.CreateIndex(
                name: "UQ_CargaPeriodo_Carga_Periodo",
                schema: "dbo",
                table: "CargaPeriodo",
                columns: new[] { "CargaId", "Periodo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_CargaPeriodo_PeriodoReservado",
                schema: "dbo",
                table: "CargaPeriodo",
                column: "PeriodoReservado",
                unique: true,
                filter: "[PeriodoReservado] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Incidencia_Carga_Id",
                schema: "dbo",
                table: "Incidencia",
                columns: new[] { "CargaId", "Id" });

            migrationBuilder.CreateIndex(
                name: "UX_Producto_CodigoProducto",
                schema: "dbo",
                table: "Producto",
                column: "CodigoProducto",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Rol_Nombre",
                schema: "dbo",
                table: "Rol",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Usuario_Email",
                schema: "dbo",
                table: "Usuario",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CargaEstadoHistorial",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Incidencia",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Producto",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CargaPeriodo",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CargaArchivo",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Usuario",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Rol",
                schema: "dbo");
        }
    }
}
