using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyMinutesServer.Data
{
    public partial class First1 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Password = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsEmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    AccessPin = table.Column<int>(type: "int", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsChecked = table.Column<bool>(type: "bit", nullable: false),
                    IsUnsubscribed = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Meetings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AuthorId = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsChecked = table.Column<bool>(type: "bit", nullable: false),
                    CreationDateTimeStamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Meetings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Meetings_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Pins",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateTimeStamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pins", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pins_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserMasterSlave",
                columns: table => new
                {
                    SlaveId = table.Column<int>(type: "int", nullable: false),
                    MasterId = table.Column<int>(type: "int", nullable: false),
                    UserCxId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMasterSlave", x => new { x.SlaveId, x.MasterId });
                    table.ForeignKey(
                        name: "FK_UserMasterSlave_Users_MasterId",
                        column: x => x.MasterId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserMasterSlave_Users_SlaveId",
                        column: x => x.SlaveId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserMasterSlave_Users_UserCxId",
                        column: x => x.UserCxId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MeetingCxUserCx",
                columns: table => new
                {
                    DelegatesId = table.Column<int>(type: "int", nullable: false),
                    EditableMeetingsId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingCxUserCx", x => new { x.DelegatesId, x.EditableMeetingsId });
                    table.ForeignKey(
                        name: "FK_MeetingCxUserCx_Meetings_EditableMeetingsId",
                        column: x => x.EditableMeetingsId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MeetingCxUserCx_Users_DelegatesId",
                        column: x => x.DelegatesId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Topics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsChecked = table.Column<bool>(type: "bit", nullable: false),
                    ParentId = table.Column<int>(type: "int", nullable: false),
                    MeetingCxId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Topics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Topics_Meetings_MeetingCxId",
                        column: x => x.MeetingCxId,
                        principalTable: "Meetings",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Sessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Version = table.Column<int>(type: "int", nullable: false),
                    DateTimeStamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Details = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ToBeCompletedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TopicId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sessions_Topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TopicSessionCxUserCx",
                columns: table => new
                {
                    AllocatedParticipantsId = table.Column<int>(type: "int", nullable: false),
                    AllocatedSessionsId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TopicSessionCxUserCx", x => new { x.AllocatedParticipantsId, x.AllocatedSessionsId });
                    table.ForeignKey(
                        name: "FK_TopicSessionCxUserCx_Sessions_AllocatedSessionsId",
                        column: x => x.AllocatedSessionsId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TopicSessionCxUserCx_Users_AllocatedParticipantsId",
                        column: x => x.AllocatedParticipantsId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MeetingCxUserCx_EditableMeetingsId",
                table: "MeetingCxUserCx",
                column: "EditableMeetingsId");

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_AuthorId",
                table: "Meetings",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Pins_UserId",
                table: "Pins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_TopicId",
                table: "Sessions",
                column: "TopicId");

            migrationBuilder.CreateIndex(
                name: "IX_Topics_MeetingCxId",
                table: "Topics",
                column: "MeetingCxId");

            migrationBuilder.CreateIndex(
                name: "IX_TopicSessionCxUserCx_AllocatedSessionsId",
                table: "TopicSessionCxUserCx",
                column: "AllocatedSessionsId");

            migrationBuilder.CreateIndex(
                name: "IX_UserMasterSlave_MasterId",
                table: "UserMasterSlave",
                column: "MasterId");

            migrationBuilder.CreateIndex(
                name: "IX_UserMasterSlave_UserCxId",
                table: "UserMasterSlave",
                column: "UserCxId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MeetingCxUserCx");

            migrationBuilder.DropTable(
                name: "Pins");

            migrationBuilder.DropTable(
                name: "TopicSessionCxUserCx");

            migrationBuilder.DropTable(
                name: "UserMasterSlave");

            migrationBuilder.DropTable(
                name: "Sessions");

            migrationBuilder.DropTable(
                name: "Topics");

            migrationBuilder.DropTable(
                name: "Meetings");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
