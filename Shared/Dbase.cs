namespace EasyMinutesServer.Shared
{
#nullable enable
    public class Dbase
    {

        public enum DistributeFilterOptions
        {
            All, Allocated, Selective
        }
        public class Meeting
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public Participant? Author { get; set; }
            public bool IsDeleted { get; set; }
            public int DisplayOrder { get; set; }
            public bool IsChecked { get; set; }

            public List<Topic> Topics { get; set; } = new List<Topic>();
            public List<Participant> Delegates { get; set; } = new List<Participant>(); 
        }

        public class Topic
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public bool IsDeleted { get; set; }
            public int DisplayOrder { get; set; }
            public bool IsChecked { get; set; }
            public bool IsDirty { get; set; }
            public int ParentId { get; set; }

            public List<TopicSession> Sessions { get; set; } = new List<TopicSession>();
        }

        public class TopicSession
        {
            public int Id { get; set; }
            public int Version { get; set; }
            public DateTimeOffset DateTimeStamp { get; set; } = ConstantsGlobal.DateMinValue;

            public string Details { get; set; } = "";
            public DateTimeOffset ToBeCompletedDate { get; set; }
            public bool IsCompleted { get; set; }

            public bool IsDeleted { get; set; }

            public List<Participant> AllocatedParticipants { get; set; } = new List<Participant>();

        }

        public class Participant
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public string Password { get; set; } = "";
            public string Email { get; set; } = "";
            public bool IsDeleted { get; set; }
            public int AccessPin { get; set; }
            public int DisplayOrder { get; set; }
            public bool IsChecked { get; set; }
            public bool IsUnsubscribed { get; set; }
            public bool IsEmailConfirmed { get; set; }

            public override string ToString()
            {
                return String.IsNullOrEmpty(Name) ? Email: Name;
            }

            public override bool Equals(object? obj)
            {
                if (obj == null) return false;
                if (obj is not Participant) return false;

                return this.Id.Equals(((Participant)obj).Id);
            }

            public override int GetHashCode()
            {
                return this.Id.GetHashCode();
            }
        }




    }
}
