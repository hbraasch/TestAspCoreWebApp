﻿// <auto-generated />
using System;
using EasyMinutesServer.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace EasyMinutesServer.Data
{
    [DbContext(typeof(DbaseContext))]
    [Migration("20220724230459_Next2")]
    partial class Next2
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "6.0.7")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder, 1L, 1);

            modelBuilder.Entity("EasyMinutesServer.Models.DbaseContext+MeetingCx", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"), 1L, 1);

                    b.Property<int>("AuthorId")
                        .HasColumnType("int");

                    b.Property<DateTimeOffset>("CreationDateTimeStamp")
                        .HasColumnType("datetimeoffset");

                    b.Property<int>("DisplayOrder")
                        .HasColumnType("int");

                    b.Property<bool>("IsChecked")
                        .HasColumnType("bit");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("bit");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.HasIndex("AuthorId");

                    b.ToTable("Meetings");
                });

            modelBuilder.Entity("EasyMinutesServer.Models.DbaseContext+PinCx", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"), 1L, 1);

                    b.Property<DateTimeOffset>("DateTimeStamp")
                        .HasColumnType("datetimeoffset");

                    b.Property<int>("UserId")
                        .HasColumnType("int");

                    b.Property<string>("Value")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.ToTable("Pins");
                });

            modelBuilder.Entity("EasyMinutesServer.Models.DbaseContext+TopicCx", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"), 1L, 1);

                    b.Property<int>("DisplayOrder")
                        .HasColumnType("int");

                    b.Property<bool>("IsChecked")
                        .HasColumnType("bit");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("bit");

                    b.Property<int?>("MeetingCxId")
                        .HasColumnType("int");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("ParentId")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.HasIndex("MeetingCxId");

                    b.ToTable("Topics");
                });

            modelBuilder.Entity("EasyMinutesServer.Models.DbaseContext+TopicSessionCx", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"), 1L, 1);

                    b.Property<DateTimeOffset>("DateTimeStamp")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("Details")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<bool>("IsCompleted")
                        .HasColumnType("bit");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("bit");

                    b.Property<string>("Notes")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTimeOffset>("ToBeCompletedDate")
                        .HasColumnType("datetimeoffset");

                    b.Property<int>("TopicId")
                        .HasColumnType("int");

                    b.Property<int>("Version")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.HasIndex("TopicId");

                    b.ToTable("Sessions");
                });

            modelBuilder.Entity("EasyMinutesServer.Models.DbaseContext+UserCx", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"), 1L, 1);

                    b.Property<int>("AccessPin")
                        .HasColumnType("int");

                    b.Property<int>("DisplayOrder")
                        .HasColumnType("int");

                    b.Property<string>("Email")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<bool>("IsChecked")
                        .HasColumnType("bit");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("bit");

                    b.Property<bool>("IsEmailConfirmed")
                        .HasColumnType("bit");

                    b.Property<bool>("IsSignUpUser")
                        .HasColumnType("bit");

                    b.Property<bool>("IsUnsubscribed")
                        .HasColumnType("bit");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Password")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.ToTable("Users");
                });

            modelBuilder.Entity("EasyMinutesServer.Models.DbaseContext+UserMasterSlave", b =>
                {
                    b.Property<int>("SlaveId")
                        .HasColumnType("int");

                    b.Property<int>("MasterId")
                        .HasColumnType("int");

                    b.Property<int>("Id")
                        .HasColumnType("int");

                    b.Property<int?>("UserCxId")
                        .HasColumnType("int");

                    b.HasKey("SlaveId", "MasterId");

                    b.HasIndex("MasterId");

                    b.HasIndex("UserCxId");

                    b.ToTable("UserMasterSlave");
                });

            modelBuilder.Entity("MeetingCxUserCx", b =>
                {
                    b.Property<int>("DelegatesId")
                        .HasColumnType("int");

                    b.Property<int>("EditableMeetingsId")
                        .HasColumnType("int");

                    b.HasKey("DelegatesId", "EditableMeetingsId");

                    b.HasIndex("EditableMeetingsId");

                    b.ToTable("MeetingCxUserCx");
                });

            modelBuilder.Entity("TopicSessionCxUserCx", b =>
                {
                    b.Property<int>("AllocatedParticipantsId")
                        .HasColumnType("int");

                    b.Property<int>("AllocatedSessionsId")
                        .HasColumnType("int");

                    b.HasKey("AllocatedParticipantsId", "AllocatedSessionsId");

                    b.HasIndex("AllocatedSessionsId");

                    b.ToTable("TopicSessionCxUserCx");
                });

            modelBuilder.Entity("EasyMinutesServer.Models.DbaseContext+MeetingCx", b =>
                {
                    b.HasOne("EasyMinutesServer.Models.DbaseContext+UserCx", "Author")
                        .WithMany()
                        .HasForeignKey("AuthorId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.Navigation("Author");
                });

            modelBuilder.Entity("EasyMinutesServer.Models.DbaseContext+PinCx", b =>
                {
                    b.HasOne("EasyMinutesServer.Models.DbaseContext+UserCx", "User")
                        .WithMany("Pins")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("User");
                });

            modelBuilder.Entity("EasyMinutesServer.Models.DbaseContext+TopicCx", b =>
                {
                    b.HasOne("EasyMinutesServer.Models.DbaseContext+MeetingCx", null)
                        .WithMany("Topics")
                        .HasForeignKey("MeetingCxId");
                });

            modelBuilder.Entity("EasyMinutesServer.Models.DbaseContext+TopicSessionCx", b =>
                {
                    b.HasOne("EasyMinutesServer.Models.DbaseContext+TopicCx", "Topic")
                        .WithMany("Sessions")
                        .HasForeignKey("TopicId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Topic");
                });

            modelBuilder.Entity("EasyMinutesServer.Models.DbaseContext+UserMasterSlave", b =>
                {
                    b.HasOne("EasyMinutesServer.Models.DbaseContext+UserCx", "Master")
                        .WithMany("Masters")
                        .HasForeignKey("MasterId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("EasyMinutesServer.Models.DbaseContext+UserCx", "Slave")
                        .WithMany()
                        .HasForeignKey("SlaveId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("EasyMinutesServer.Models.DbaseContext+UserCx", null)
                        .WithMany("Slaves")
                        .HasForeignKey("UserCxId");

                    b.Navigation("Master");

                    b.Navigation("Slave");
                });

            modelBuilder.Entity("MeetingCxUserCx", b =>
                {
                    b.HasOne("EasyMinutesServer.Models.DbaseContext+UserCx", null)
                        .WithMany()
                        .HasForeignKey("DelegatesId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("EasyMinutesServer.Models.DbaseContext+MeetingCx", null)
                        .WithMany()
                        .HasForeignKey("EditableMeetingsId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("TopicSessionCxUserCx", b =>
                {
                    b.HasOne("EasyMinutesServer.Models.DbaseContext+UserCx", null)
                        .WithMany()
                        .HasForeignKey("AllocatedParticipantsId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("EasyMinutesServer.Models.DbaseContext+TopicSessionCx", null)
                        .WithMany()
                        .HasForeignKey("AllocatedSessionsId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("EasyMinutesServer.Models.DbaseContext+MeetingCx", b =>
                {
                    b.Navigation("Topics");
                });

            modelBuilder.Entity("EasyMinutesServer.Models.DbaseContext+TopicCx", b =>
                {
                    b.Navigation("Sessions");
                });

            modelBuilder.Entity("EasyMinutesServer.Models.DbaseContext+UserCx", b =>
                {
                    b.Navigation("Masters");

                    b.Navigation("Pins");

                    b.Navigation("Slaves");
                });
#pragma warning restore 612, 618
        }
    }
}
