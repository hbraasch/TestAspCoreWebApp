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
            public User? Author { get; set; }
            public bool IsDeleted { get; set; }
            public int DisplayOrder { get; set; }
            public bool IsChecked { get; set; }

            public List<Topic> Topics { get; set; } = new List<Topic>();
            public List<User> Delegates { get; set; } = new List<User>(); 
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

            internal List<User> CurrentAllocatedParticipants()
            {
                var currentSession = Sessions.OrderBy(o=>o.DateTimeStamp).Last();
                return currentSession.AllocatedParticipants ?? new();
            }
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

            public List<User> AllocatedParticipants { get; set; } = new List<User>();

        }

        public class User
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
                if (obj is not User) return false;

                return this.Id.Equals(((User)obj).Id);
            }

            public override int GetHashCode()
            {
                return this.Id.GetHashCode();
            }
        }




    }
}
