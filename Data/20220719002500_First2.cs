using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyMinutesServer.Data
{
    public partial class First2 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "UserMasterSlave",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Id",
                table: "UserMasterSlave");
        }
    }
}
