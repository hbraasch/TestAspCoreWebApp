using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyMinutesServer.Data
{
    public partial class Initial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Participants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Password = table.Column<string>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", nullable: false),
                    IsEmailConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    AccessPin = table.Column<int>(type: "INTEGER", nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsChecked = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsUnsubscribed = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Participants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Meetings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    AuthorId = table.Column<int>(type: "INTEGER", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsChecked = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Meetings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Meetings_Participants_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Participants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ParticipantMasterSlave",
                columns: table => new
                {
                    SlaveId = table.Column<int>(type: "INTEGER", nullable: false),
                    MasterId = table.Column<int>(type: "INTEGER", nullable: false),
                    ParticipantCxId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParticipantMasterSlave", x => new { x.SlaveId, x.MasterId });
                    table.ForeignKey(
                        name: "FK_ParticipantMasterSlave_Participants_MasterId",
                        column: x => x.MasterId,
                        principalTable: "Participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ParticipantMasterSlave_Participants_ParticipantCxId",
                        column: x => x.ParticipantCxId,
                        principalTable: "Participants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ParticipantMasterSlave_Participants_SlaveId",
                        column: x => x.SlaveId,
                        principalTable: "Participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Pins",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Value = table.Column<string>(type: "TEXT", nullable: false),
                    DateTimeStamp = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ParticipantId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pins", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pins_Participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "Participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeetingCxParticipantCx",
                columns: table => new
                {
                    DelegatesId = table.Column<int>(type: "INTEGER", nullable: false),
                    EditableMeetingsId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingCxParticipantCx", x => new { x.DelegatesId, x.EditableMeetingsId });
                    table.ForeignKey(
                        name: "FK_MeetingCxParticipantCx_Meetings_EditableMeetingsId",
                        column: x => x.EditableMeetingsId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MeetingCxParticipantCx_Participants_DelegatesId",
                        column: x => x.DelegatesId,
                        principalTable: "Participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Topics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsChecked = table.Column<bool>(type: "INTEGER", nullable: false),
                    ParentId = table.Column<int>(type: "INTEGER", nullable: false),
                    MeetingCxId = table.Column<int>(type: "INTEGER", nullable: true)
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
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    DateTimeStamp = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Details = table.Column<string>(type: "TEXT", nullable: false),
                    ToBeCompletedDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsCompleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    TopicId = table.Column<int>(type: "INTEGER", nullable: false)
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
                name: "ParticipantCxTopicSessionCx",
                columns: table => new
                {
                    AllocatedParticipantsId = table.Column<int>(type: "INTEGER", nullable: false),
                    AllocatedSessionsId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParticipantCxTopicSessionCx", x => new { x.AllocatedParticipantsId, x.AllocatedSessionsId });
                    table.ForeignKey(
                        name: "FK_ParticipantCxTopicSessionCx_Participants_AllocatedParticipantsId",
                        column: x => x.AllocatedParticipantsId,
                        principalTable: "Participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ParticipantCxTopicSessionCx_Sessions_AllocatedSessionsId",
                        column: x => x.AllocatedSessionsId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MeetingCxParticipantCx_EditableMeetingsId",
                table: "MeetingCxParticipantCx",
                column: "EditableMeetingsId");

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_AuthorId",
                table: "Meetings",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_ParticipantCxTopicSessionCx_AllocatedSessionsId",
                table: "ParticipantCxTopicSessionCx",
                column: "AllocatedSessionsId");

            migrationBuilder.CreateIndex(
                name: "IX_ParticipantMasterSlave_MasterId",
                table: "ParticipantMasterSlave",
                column: "MasterId");

            migrationBuilder.CreateIndex(
                name: "IX_ParticipantMasterSlave_ParticipantCxId",
                table: "ParticipantMasterSlave",
                column: "ParticipantCxId");

            migrationBuilder.CreateIndex(
                name: "IX_Pins_ParticipantId",
                table: "Pins",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_TopicId",
                table: "Sessions",
                column: "TopicId");

            migrationBuilder.CreateIndex(
                name: "IX_Topics_MeetingCxId",
                table: "Topics",
                column: "MeetingCxId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MeetingCxParticipantCx");

            migrationBuilder.DropTable(
                name: "ParticipantCxTopicSessionCx");

            migrationBuilder.DropTable(
                name: "ParticipantMasterSlave");

            migrationBuilder.DropTable(
                name: "Pins");

            migrationBuilder.DropTable(
                name: "Sessions");

            migrationBuilder.DropTable(
                name: "Topics");

            migrationBuilder.DropTable(
                name: "Meetings");

            migrationBuilder.DropTable(
                name: "Participants");
        }
    }
}
