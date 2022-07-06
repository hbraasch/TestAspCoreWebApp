using EasyMinutesServer.Shared;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Net.Sockets;
using static EasyMinutesServer.Models.DbaseContext;
using static EasyMinutesServer.Shared.Dbase;

namespace EasyMinutesServer.Models
{
    public class DbaseContext : DbContext
    {


        public DbaseContext(DbContextOptions<DbaseContext> options) : base(options)
        {
            this.ChangeTracker.LazyLoadingEnabled = true;
            Meetings = Set<MeetingCx>();
            Topics = Set<TopicCx>();
            Sessions = Set<TopicSessionCx>();
            Participants = Set<ParticipantCx>();
            Pins= Set<PinCx>();
        }

        public DbSet<MeetingCx> Meetings { get; set; }
        public DbSet<TopicCx> Topics { get; set; }
        public DbSet<TopicSessionCx> Sessions { get; set; }
        public DbSet<ParticipantCx> Participants { get; set; }
        public DbSet<PinCx> Pins { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<MeetingCx>()
                .HasOne(m => m.Author);

            modelBuilder.Entity<MeetingCx>()
                        .HasMany(p => p.Delegates)
                        .WithMany(e => e.EditableMeetings);

            modelBuilder.Entity<TopicSessionCx>()
                .HasOne<TopicCx>(s => s.Topic)
                .WithMany(g => g.Sessions)
                .HasForeignKey(s => s.TopicId);

            modelBuilder.Entity<TopicSessionCx>()
                .HasMany<ParticipantCx>(s => s.AllocatedParticipants)
                .WithMany(g => g.AllocatedSessions);


            modelBuilder.Entity<ParticipantMasterSlave>()
                .HasKey(ms => new { ms.SlaveId, ms.MasterId });

            modelBuilder.Entity<ParticipantMasterSlave>()
               .HasOne(c => c.Slave)
               .WithMany() // <-- one of this must be empty
               .HasForeignKey(pc => pc.SlaveId)
               .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ParticipantMasterSlave>()
               .HasOne(c => c.Master)
               .WithMany(p => p.Masters)
               .HasForeignKey(pc => pc.MasterId);


        }



        public class MeetingCx
        {
            [Key]
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public ParticipantCx? Author { get; set; }
            public bool IsDeleted { get; set; }
            public int DisplayOrder { get; set; }
            public bool IsChecked { get; set; }

            public virtual List<TopicCx> Topics { get; set; } = new List<TopicCx>();

            public virtual List<ParticipantCx> Delegates { get; set; } = new List<ParticipantCx>();

            public Meeting FromDb()
            {
                return new Meeting { Id = Id, Name = Name, IsDeleted = IsDeleted, IsChecked = IsChecked, DisplayOrder = DisplayOrder, Author = Author?.FromDb(), Topics = Topics.FromDb(), Delegates = Delegates.FromDb() };
            }

            public override bool Equals(object? obj)
            {
                if (obj == null) return false;
                if (obj is not MeetingCx) return false;
                return this.Id.Equals(((MeetingCx)obj).Id);
            }

            public override int GetHashCode()
            {
                return this.Id.GetHashCode();
            }
        }

        public class TopicCx
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public bool IsDeleted { get; set; }
            public int DisplayOrder { get; set; }
            public bool IsChecked { get; set; }


            public virtual List<TopicSessionCx> Sessions { get; set; } = new List<TopicSessionCx>();

            public Topic FromDb()
            {
                return new Topic { Id = Id, Name = Name, IsDeleted = IsDeleted, DisplayOrder = DisplayOrder, IsChecked = IsChecked, Sessions = Sessions.FromDb() };
            }
        }

        public class TopicSessionCx
        {
            public int Id { get; set; }
            public int Version { get; set; }
            public DateTimeOffset DateTimeStamp { get; set; } = ConstantsGlobal.DateMinValue;

            public string Details { get; set; } = "";
            public DateTimeOffset ToBeCompletedDate { get; set; }
            public bool IsCompleted { get; set; }

            public bool IsDeleted { get; set; }

            public int TopicId { get; set; }
            public TopicCx? Topic {get;set;}

            public virtual List<ParticipantCx> AllocatedParticipants { get; set; } = new List<ParticipantCx>();

            public TopicSession FromDb()
            {
                return new TopicSession { Id = Id, Version = Version, DateTimeStamp = DateTimeStamp, Details = Details, ToBeCompletedDate = ToBeCompletedDate, IsCompleted = IsCompleted, IsDeleted = IsDeleted, AllocatedParticipants = AllocatedParticipants.FromDb() };
            }

        }

        public class ParticipantCx
        {


            public int Id { get; set; }
            public string Name { get; set; } = "";
            public string Password { get; set; } = "";
            public string Email { get; set; } = "";
            public bool IsEmailConfirmed { get; set; } = false;
            public bool IsDeleted { get; set; }
            public int AccessPin { get; set; }
            public int DisplayOrder { get; set; }
            public bool IsChecked { get; set; }
            public bool IsUnsubscribed { get; set; } = false;
            public virtual ICollection<ParticipantMasterSlave> Masters { get; set; } = new List<ParticipantMasterSlave>();
            public virtual ICollection<ParticipantMasterSlave> Slaves { get; set; } = new List<ParticipantMasterSlave>();
            public virtual ICollection<MeetingCx> EditableMeetings { get; set; } = new List<MeetingCx>();
            public virtual ICollection<TopicSessionCx> AllocatedSessions { get; set; } = new List<TopicSessionCx>();
            public virtual ICollection<PinCx> Pins { get; set; } = new List<PinCx>();

            public Participant FromDb()
            {
                return new Participant { Id = Id, Name = Name, Password = Password, Email = Email, IsDeleted = IsDeleted, AccessPin = AccessPin, IsChecked = IsChecked, DisplayOrder = DisplayOrder, IsEmailConfirmed = IsEmailConfirmed, IsUnsubscribed = IsUnsubscribed};
            }

            public override bool Equals(object? obj)
            {
                if (obj == null) return false;
                if (obj is not ParticipantCx) return false;

                return this.Id.Equals(((ParticipantCx)obj).Id);
            }

            public override int GetHashCode()
            {
                return this.Id.GetHashCode();
            }

        }

        public class ParticipantMasterSlave
        {
            public int SlaveId { get; set; }
            public int MasterId { get; set; }

            public virtual ParticipantCx Slave { get; set; }
            public virtual ParticipantCx Master { get; set; }
        }

        public class PinCx
        {
            public int Id { get; set; }
            public string Value { get; set; } = "";
            public DateTimeOffset DateTimeStamp { get; set; }
            public bool IsValid => DateTimeStamp > DateTimeOffset.Now.AddDays(-ConstantsGlobal.PinTimeoutInDays);
            public int ParticipantId { get; set; }
            public virtual ParticipantCx? Participant { get; set; }
        }

    }

    public static class DbExtensions
    {
        public static List<Meeting> FromDb(this List<MeetingCx> list)
        {
            var convert = new List<Meeting>();
            list.ForEach(o => convert.Add(o.FromDb()));
            return convert;
        }

        public static List<Topic> FromDb(this List<TopicCx> list)
        {
            var convert = new List<Topic>();
            list.ForEach(o => convert.Add(o.FromDb()));
            return convert;
        }

        public static List<TopicSession> FromDb(this List<TopicSessionCx> list)
        {
            var convert = new List<TopicSession>();
            list.ForEach(o => convert.Add(o.FromDb()));
            return convert;
        }

        public static List<Participant> FromDb(this List<ParticipantCx> list)
        {
            var convert = new List<Participant>();
            list.ForEach(o => convert.Add(o.FromDb()));
            return convert;
        }

    }
}
