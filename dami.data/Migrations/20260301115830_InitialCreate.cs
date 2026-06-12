using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace dami.data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CUSTOMERS",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FullName = table.Column<string>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CUSTOMERS", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PRODUCTS",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PRODUCTS", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "CUSTOMERS",
                columns: new[] { "Id", "Email", "FullName" },
                values: new object[,]
                {
                    { 1, "", "customer1" },
                    { 2, "", "custmer2" },
                    { 3, "", "customer3" },
                    { 4, "", "customer4" },
                    { 5, "", "customer5" }
                });

            migrationBuilder.InsertData(
                table: "PRODUCTS",
                columns: new[] { "Id", "Name", "Price" },
                values: new object[,]
                {
                    { 1, "Product1", 0m },
                    { 2, "Product2", 0m },
                    { 3, "Product3", 0m },
                    { 4, "Product4", 0m },
                    { 5, "Product5", 0m }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CUSTOMERS");

            migrationBuilder.DropTable(
                name: "PRODUCTS");
        }
    }
}
