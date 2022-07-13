using EasyMinutesServer.Helpers;
using EasyMinutesServer.Shared;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using TreeApps.Maui.Helpers;
using static EasyMinutesServer.Models.DbaseContext;
using static EasyMinutesServer.Shared.Dbase;
using static EasyMinutesServer.Shared.MinutesController;

namespace EasyMinutesServer.Models
{
    public class MinutesModel
    {
        readonly DbaseContext dbase;
        readonly IMailWorker mailWorker;

        public MinutesModel(DbaseContext dbase, IMailWorker mailWorker)
        {
            this.dbase = dbase;
            dbase.Database.EnsureCreated();
            this.mailWorker = mailWorker;
        }

        #region *// Meeting and topics
        internal List<MeetingCx> GetMeetings(int participantId)
        {
            if (participantId == 0) throw new Exception("Participant id is zero");
            if (dbase.Meetings == null) return new ();
            var participant = GetParticipant(participantId);
            if (participant == null) throw new Exception("Participant does not exist");
            var meetings = dbase.Meetings.Include(o => o.Topics)
                .Include(o => o.Delegates)
                .Include(o => o.Author)
                .Include(o => o.Topics).ThenInclude(t => t.Sessions).ThenInclude(a => a.AllocatedParticipants).ToList();
            var allAuthorMeetings = meetings.Where(o => o.Author == participant).ToList();
            var allDelegateMeetings = meetings.Where(o=>o.Delegates.Contains(participant)).ToList();
            var allAllocatedMeetings = meetings.Where(meeting => meeting.Topics.Where(topic => topic.Sessions.Where(session => session.AllocatedParticipants.Contains(participant)).ToList().Count != 0).ToList().Count != 0).ToList();
            var allMeetings = new List<MeetingCx>();
            allMeetings.AddRange(allAuthorMeetings);
            allMeetings.AddRange(allDelegateMeetings);
            allMeetings.AddRange(allAllocatedMeetings);
            return allMeetings.Where(o => !o.IsDeleted).Distinct().OrderBy(o=>o.DisplayOrder).ToList();
        }

        internal MeetingCx? GetMeeting(int meetingId)
        {
            if (dbase.Meetings == null) return null;
            var meetings = dbase.Meetings
                .Include(o => o.Delegates)
                .Include(o => o.Author)
                .Include(o => o.Topics).ThenInclude(t => t.Sessions).ThenInclude(a => a.AllocatedParticipants);
            var meeting = meetings.FirstOrDefault(o => o.Id == meetingId);
            return meetings.FirstOrDefault(o => o.Id == meetingId);
        }

        internal MeetingCx CreateMeeting(int authorId, string meetingName)
        {
            var author = GetParticipant(authorId);
            if (author == null) throw new Exception("Author does not exist");
            var meetings = GetMeetings(authorId);
            var meeting = new MeetingCx
            {
                Name = meetingName,
                Author = author,
                DisplayOrder = meetings.Count
            };
            dbase.Meetings.Add(meeting);
            dbase.SaveChanges();
            return meeting;
        }

        public void UpdateMeeting(int meetingId, int authorId, List<int>delegateIds, string meetingName)
        {
            if (dbase == null) throw new Exception("Server dbase is null");
            if (dbase.Meetings == null) throw new Exception("Server dbase table [Meetings] is null");
            var meeting = GetMeeting(meetingId);
            if (meeting == null) throw new Exception("Meeting does not exist");
            var author = GetParticipant(authorId);
            if (author == null) throw new Exception("Author does not exist");
            meeting.Author = author;
            ChangeMeetingDelegates(meeting, delegateIds);
            meeting.Name = meetingName;
            dbase.SaveChanges();
        }

        private void ChangeMeetingDelegates(MeetingCx meeting, List<int> delegateIds)
{
            var updateDelegates = dbase.Participants.Where(p => delegateIds.Contains(p.Id)).ToList();
            var currentDelegates = meeting.Delegates;

            // Remove missing ones
            var missingDelegates = currentDelegates.Except(updateDelegates).ToList();
            currentDelegates.RemoveAll(o => missingDelegates.Contains(o));

            // Add new ones
            var addDelegates = updateDelegates.Except(currentDelegates);
            currentDelegates.AddRange(addDelegates);

            meeting.Delegates = currentDelegates;

            dbase.SaveChanges();
        }

        internal void SetMeetingChecked(int meetingId, bool isChecked)
        {
            var meeting = dbase.Meetings.FirstOrDefault(o => o.Id == meetingId);
            if (meeting == null) throw new Exception("Meeting does not exist");
            meeting.IsChecked = isChecked;
            dbase.SaveChanges();
        }

        internal void ChangeMeetingsDisplayOrder(int meetingId, bool isMoveUp)
        {
            var meeting = GetMeeting(meetingId);
            if (meeting == null) throw new Exception("Meeting is null");
            var author = meeting.Author;
            if (author == null) throw new Exception("Author is null"); ;
            var meetings = GetMeetings(author.Id).OrderBy(o => o.DisplayOrder).ToList();
            if (meetings.Count == 0) return;
            // Ensure displayorder integrity
            var displayOrder = 0;
            meetings.ForEach(o => o.DisplayOrder = displayOrder++);
            var moveItem = meetings.FirstOrDefault(o => o.Id == meetingId);
            if (moveItem == null) throw new Exception("Move item is null");
            // Move
            if (isMoveUp)
            {
                var moveItemIndex = moveItem.DisplayOrder;
                var newMoveItemIndex = moveItemIndex - 1;
                newMoveItemIndex = Math.Max(newMoveItemIndex, 0);
                meetings[newMoveItemIndex].DisplayOrder = moveItemIndex;
                meetings[moveItemIndex].DisplayOrder = newMoveItemIndex;
            }
            else
            {
                var moveItemIndex = moveItem.DisplayOrder;
                var newMoveItemIndex = moveItemIndex + 1;
                newMoveItemIndex = Math.Min(newMoveItemIndex, meetings.Count - 1);
                meetings[newMoveItemIndex].DisplayOrder = moveItemIndex;
                meetings[moveItemIndex].DisplayOrder = newMoveItemIndex;
            }
            dbase.SaveChanges();

        }

        internal void DeleteMeeting(int meetingId)
        {
            if (dbase == null) throw new Exception("Server dbase is null");
            if (dbase.Meetings == null) throw new Exception("Server dbase table [Meetings] is null");
            var meeting = GetMeeting(meetingId);
            if (meeting == null) return;
            var topics = GetTopics(meeting.Id);
            foreach (var topic in topics)
            {
                var sessions = GetTopicSessions(topic.Id);
                foreach (var session in sessions)
                {
                    session.IsDeleted = true;
                }
                topic.IsDeleted = true;
            }
            meeting.IsDeleted = true;
            dbase.SaveChanges();
        }



        #region *// Topics
        internal List<TopicCx> GetTopics(int meetingId)
        {
            var meeting = GetMeeting(meetingId);
            if (meeting == null) return new();
            return meeting.Topics;
        }

        internal TopicCx? GetTopic(int topicId)
        {
            if (dbase.Topics == null) return null;
            return dbase.Topics.Include(o => o.Sessions).ThenInclude(session => session.AllocatedParticipants).FirstOrDefault(o => o.Id == topicId);
        }

        internal TopicCx CreateTopic(int meetingId, int parentTopicId, int beforeTopicId,  string title)
        {
            TopicCx topic;
            var meeting = GetMeeting(meetingId);
            if (meeting == null) throw new Exception("Meeting is null");
            if ((parentTopicId == 0) && (beforeTopicId == 0))
            {
                // Brand new topic
                topic = new TopicCx { Name = title, ParentId = 0, DisplayOrder = 0 };
            }
            else if ((parentTopicId == 0) && (beforeTopicId != 0))
            {
                // New topic is root, inbetween topic. Place new one after the [BeforeTopic]
                var beforeTopic = GetTopic(beforeTopicId);
                if (beforeTopic == null) throw new Exception("BeforeTopic is null");
                topic = new TopicCx { Name = title, ParentId = 0 };
                InsertAfterTopic(meeting, beforeTopic, topic);
            }
            else 
            {
                // New topic is child topic, place after beforeTopic
                if (beforeTopicId == 0) throw new Exception("BeforeTopicId is zero");
                var beforeTopic = GetTopic(beforeTopicId);
                if (beforeTopic == null) throw new Exception("BeforeTopic is null");
                topic = new TopicCx { Name = title, ParentId = parentTopicId };
                InsertAfterTopic(meeting, beforeTopic, topic);
            }

            meeting.Topics.Add(topic);
            topic.Sessions.Add(new TopicSessionCx { Version = 1, DateTimeStamp = DateTimeOffset.Now, ToBeCompletedDate = ConstantsGlobal.DateMinValue });
            dbase.SaveChanges();

            return topic;
        }

        private static void InsertAfterTopic(MeetingCx meeting, TopicCx beforeTopic, TopicCx topic)
        {
            topic.DisplayOrder = beforeTopic.DisplayOrder + 1;
            var afterTopics = GetChildTopics(meeting, beforeTopic.ParentId).Where(o => o.DisplayOrder > beforeTopic.DisplayOrder).ToList();
            if (afterTopics.Count != 0)
            {
                afterTopics.ForEach(o => o.DisplayOrder += 1);
            }
        }

        private static List<TopicCx> GetChildTopics(MeetingCx meeting, int parentTopicId)
        {
            return meeting.Topics.Where(o=>o.ParentId == parentTopicId && !o.IsDeleted).OrderBy(o=>o.DisplayOrder).ToList() ;
        }

        /// <summary>
        /// User to update topic title
        /// </summary>
        /// <param name="topicId"></param>
        /// <param name="title"></param>
        /// <exception cref="Exception"></exception>
        internal void UpdateTopic(int topicId, string title)
        {
            if (dbase.Topics == null) throw new Exception("Server dbase table [Topics] is null");
            var topic = dbase.Topics.FirstOrDefault(o => o.Id == topicId);
            if (topic == null) throw new Exception("Topic is null");
            topic.Name = title;
            dbase.SaveChanges();
        }

        internal void SetTopicChecked(int TopicId, bool isChecked)
        {
            var topic = dbase.Topics.FirstOrDefault(o => o.Id == TopicId);
            if (topic == null) throw new Exception("Topic does not exist");
            topic.IsChecked = isChecked;
            dbase.SaveChanges();
        }

        internal List<TopicCx> ChangeTopicsDisplayOrder(int meetingId, int topicId, bool isMoveUp)
        {
            var topic = GetTopic(topicId);
            if (topic == null) throw new Exception("Topic is null");
            var meeting = GetMeeting(meetingId);
            if (meeting == null) throw new Exception("Meeting is null");
            var topics = GetTopics(meeting.Id).OrderBy(o => o.DisplayOrder).ToList();
            if (topics.Count == 0) return new();
            // Ensure displayorder integrity
            var displayOrder = 0;
            topics.ForEach(o => o.DisplayOrder = displayOrder++);
            var moveItem = topics.FirstOrDefault(o => o.Id == topicId);
            if (moveItem == null) throw new Exception("Move item is null");
            // Move
            if (isMoveUp)
            {
                var moveItemIndex = moveItem.DisplayOrder;
                var newMoveItemIndex = moveItemIndex - 1;
                newMoveItemIndex = Math.Max(newMoveItemIndex, 0);
                topics[newMoveItemIndex].DisplayOrder = moveItemIndex;
                topics[moveItemIndex].DisplayOrder = newMoveItemIndex;
            }
            else
            {
                var moveItemIndex = moveItem.DisplayOrder;
                var newMoveItemIndex = moveItemIndex + 1;
                newMoveItemIndex = Math.Min(newMoveItemIndex, topics.Count - 1);
                topics[newMoveItemIndex].DisplayOrder = moveItemIndex;
                topics[moveItemIndex].DisplayOrder = newMoveItemIndex;
            }
            dbase.SaveChanges();

            return topics;

        }

        internal List<TopicCx> ChangeTopicHierarchy(int meetingId, int aboveTopicId, int changeTopicId, int belowTopicId, bool isLevelDown)
        {
            var meeting = GetMeeting(meetingId);
            if (meeting == null) throw new Exception("Meeting is null");

            var changeTopic = GetTopic(changeTopicId);
            if (changeTopic == null) throw new Exception("Change topic is null");
            if (isLevelDown)
            {
                if (aboveTopicId == 0) throw new Exception("First item cannot move down");
                var aboveTopic = GetTopic(aboveTopicId);

                if (changeTopic.ParentId == aboveTopic?.ParentId)
                {
                    // Above and changed is on the same level
                    // Above becomes changed one's parent
                    #region *// Renumber old peer children since item has been removed
                    var currentChangePeerChildrenTail = GetChildTopics(meeting, changeTopic.ParentId).Where(o => o.DisplayOrder > changeTopic.DisplayOrder).ToList();
                    currentChangePeerChildrenTail.ForEach(o => o.DisplayOrder -= 1);
                    #endregion

                    // Changed one becomed first one of own children
                    var changeTopicChildren = GetChildTopics(meeting, changeTopic.Id);
                    changeTopic.DisplayOrder = 0;
                    changeTopicChildren.ForEach(o => o.DisplayOrder += 1);

                    // Change hierarchy
                    changeTopic.ParentId = aboveTopicId;
                }
                else
                {
                    // [Above] and [changed] is on the different levels ([above] can only be lower level at this point)
                    // [Changed] becomes peer child as [above], last one
                    #region *// Renumber old peer children since item has been removed
                    var currentChangePeerChildrenTail = GetChildTopics(meeting, changeTopic.ParentId).Where(o => o.DisplayOrder > changeTopic.DisplayOrder).ToList();
                    currentChangePeerChildrenTail.ForEach(o => o.DisplayOrder -= 1); 
                    #endregion

                    #region *// Renumber new peer children [change] became part of
                    var newParentPeerChildren = GetChildTopics(meeting, aboveTopic?.ParentId ?? 0);
                    changeTopic.DisplayOrder = newParentPeerChildren.Count - 1;
                    #endregion

                    // Change hierarchy
                    changeTopic.ParentId = aboveTopic?.ParentId ?? 0;
                }               
            }
            else
            {

            }
            dbase.SaveChanges();
            return GetTopics(meetingId);
        }

        internal void DeleteTopic(int topicId)
        {
            if (dbase.Topics == null) throw new Exception("Server dbase table [Topics] is null");
            if (dbase.Sessions == null) throw new Exception("Server dbase table [Sessions] is null");
            var topic = GetTopic(topicId);
            if (topic == null) throw new Exception("Topic is null");
            var sessions = GetTopicSessions(topicId);
            dbase.Sessions.RemoveRange(sessions);
            dbase.Topics.Remove(topic);
            dbase.SaveChanges();
        }
        #endregion

        #region *// Sessions
        internal List<TopicSessionCx> GetTopicSessions(int topicId)
        {
            if (dbase.Sessions == null) return new();
            var topic = GetTopic(topicId);
            if (topic == null) return new();
            return topic.Sessions;
        }

        internal TopicSessionCx? GetTopicSession(int topicSessionId)
        {
            if (dbase.Sessions == null) return null;
            return dbase.Sessions.Include(o => o.AllocatedParticipants).Include(o => o.Topic).FirstOrDefault(o => o.Id == topicSessionId && !o.IsDeleted);
        }

        internal void AddTopicSession(int currentTopicSessionId, DateTimeOffset dateTimeStamp)
        {
            var currentTopicSession = GetTopicSession(currentTopicSessionId);
            if (currentTopicSession == null) throw new Exception("Current session is null");
            var topicSessionParticipants = GetSessionParticipants(currentTopicSessionId);
            var newTopicSession = new TopicSessionCx
            {
                DateTimeStamp = dateTimeStamp,
                ToBeCompletedDate = currentTopicSession.ToBeCompletedDate,
                Version = currentTopicSession.Version + 1
            };
            var topic = currentTopicSession.Topic;
            if (topic == null) throw new Exception("Topic is null");
            topic.Sessions.Add(newTopicSession);

            SetParticipants(newTopicSession, topicSessionParticipants);
            dbase.SaveChanges();
        }

        internal void AddNewSessionForAllTopics(int meetingId)
        {
            #region *// Add
            var meeting = GetMeeting(meetingId);
            if (meeting == null) throw new Exception("Meeting is null");
            var topics = meeting.Topics;
            List<TopicSessionCx> sessions = new();
            foreach (var topic in topics)
            {
                sessions.Add(topic.Sessions.OrderBy(o => o.DateTimeStamp).Last());
            }
            var timestamp = DateTimeOffset.UtcNow;
            foreach (var session in sessions)
            {
                AddTopicSession(session.Id, timestamp);
            }
            dbase.SaveChanges();
            #endregion
        }

        public void UpdateTopic(int currentTopicSessionId, string title, string details, List<int>? participantIds, DateTimeOffset toBeCompletedDate)
        {
            List<ParticipantCx> participants = new();
            if (participantIds != null && participantIds.Count != 0)
            {
                if (dbase.Participants != null) participants = dbase.Participants.Where(o => participantIds.Contains(o.Id)).ToList();
            }
            var currentTopicSession = GetTopicSession(currentTopicSessionId);
            if (currentTopicSession == null) throw new Exception("Current topic session is null");
            UpdateTopic(currentTopicSession.TopicId, title);
            UpdateTopicSession(currentTopicSession, details, participants, toBeCompletedDate);
        }

        private void UpdateTopicSession(TopicSessionCx currentTopicSession, string details, List<ParticipantCx> participants, DateTimeOffset toBeCompletedDate)
        {
            currentTopicSession.Details = details;
            currentTopicSession.ToBeCompletedDate = toBeCompletedDate;
            SetParticipants(currentTopicSession, participants);
            dbase.SaveChanges();
        } 
        #endregion


        #endregion

        #region *// Participants
        public ParticipantCx? GetParticipant(int? participantId)
        {
            if (participantId == null) throw new Exception("Author id is null");
            if (dbase.Participants == null) return null;
            return dbase.Participants.Include(o=>o.Slaves).ThenInclude(p=>p.Slave).FirstOrDefault(o => o.Id == participantId && !o.IsDeleted);
        }

        internal int GetParticipantId(string participantEmail)
        {
            return dbase.Participants.FirstOrDefault(o => o.Email == participantEmail)?.Id??0;
        }

        internal int GetParticipantIdFromName(string participantName)
        {
            return dbase.Participants.FirstOrDefault(o => o.Name == participantName)?.Id ?? 0;
        }

        public List<ParticipantCx> GetParticipants(int ownerId)
        {
            var list = new List<ParticipantCx>();
            var owner = GetParticipant(ownerId);
            if (owner == null) return new();
            var participants = owner.Slaves.Select(o => o.Slave).Where(o=>!o.IsDeleted).ToList();
            return participants;
        }

        /// <summary>
        /// Used to get Author, Delegates and Allocated participants
        /// </summary>
        /// <param name="meeting"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public List<ParticipantCx>? GetAllParticipants(MeetingCx meeting)
        {
            if (meeting == null) throw new Exception("Meeting is null");
            var participants = meeting.Delegates;
            if (meeting.Author != null) participants.Add(meeting.Author);
            participants.AddRange(GetAllocatedParticipants(meeting) ?? new());
            return participants.Distinct().ToList();
        }

        /// <summary>
        /// Used to only get the participants allocated to topics
        /// </summary>
        /// <param name="meeting"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public List<ParticipantCx>? GetAllocatedParticipants(MeetingCx meeting)
        {
            if (meeting == null) throw new Exception("Meeting is null");
            var participants = new List<ParticipantCx>();
            foreach (var topic in meeting.Topics)
            {
                foreach (var session in topic.Sessions)
                {
                    participants.AddRange(session.AllocatedParticipants);
                }
            }
            return participants.Distinct().ToList();
        }

        /// <summary>
        /// Used as a converter from list of participant ids to list of participants
        /// </summary>
        /// <param name="participantIds"></param>
        /// <returns></returns>
        public List<ParticipantCx>? GetParticipants(List<int> participantIds)
        {
            if (participantIds == null) return new();
            if (dbase.Participants == null) return new();
            return dbase.Participants.Where(o=>participantIds.Contains(o.Id)).ToList();
        }

        internal List<ParticipantCx> GetMeetingParticipants(int meetingId)
        {
            var totalParticipants = new List<ParticipantCx>();
            if (dbase.Participants == null) return new();
            var meeting = GetMeeting(meetingId);
            if (meeting == null) return new();
            var topics = GetTopics(meeting.Id);
            foreach (var topic in topics)
            {
                var sessions = GetTopicSessions(topic.Id);
                foreach (var session in sessions)
                {
                    var sessionParticipants = session.AllocatedParticipants;
                    totalParticipants.AddRange(sessionParticipants);
                }
            }
            return totalParticipants;
        }

        internal List<ParticipantCx> GetSessionParticipants(int sessionId)
        {
            var session = GetTopicSession(sessionId);
            if (session == null) throw new Exception("Session is null");
            return session.AllocatedParticipants.Where(o=>!o.IsDeleted).ToList();
        }

        internal ParticipantCx AddParticipant(string email, string name, string password)
        {
            email = email.Trim();
            name = name.Trim();
            password = password.Trim();

            if (email == "" && name == "") throw new Exception("Email or name must be defined");

            // Creation of top-level participant
            if (email != "")
            {
                var participantId = GetParticipantId(email);
                if (participantId != 0) throw new Exception("Email is already in use"); // Duplicate top-level accounts not allowed 
            }
            if (name != "")
            {
                var participantId = GetParticipantIdFromName(name);
                if (participantId != 0) throw new Exception("Name is already in use"); // Duplicate top-level accounts not allowed 
            }
            var participant = new ParticipantCx() { Email = email, Name = name, Password = password };
            dbase.Participants.Add(participant);

            dbase.SaveChanges();
            return participant;
        }

        internal void AddParticipant(int ownerId, int participantId)
        {
            if (ownerId == 0) throw new Exception("Owner must be defined");
            var owner = GetParticipant(ownerId);
            if (owner == null) throw new Exception("Owner is null");
            if (participantId == 0) throw new Exception("Participant must be defined");
            var participant = GetParticipant(participantId);
            if (participant == null) throw new Exception("Participant is null");
            var slaves = owner.Slaves.Select(o => o.Slave);
            if (slaves.Select(o=>o.Id).Contains(participantId)) return;
            owner.Slaves.Add(new ParticipantMasterSlave { Master = owner, Slave = participant });
            dbase.SaveChanges();
        }

        internal void UpdateParticipant(int participantId, string email, string name, string password)
        {
            var participant = dbase.Participants.FirstOrDefault(o => o.Id == participantId);
            if (participant == null) throw new BusinessException("Participant does not exists");
            participant.Email = email;
            participant.Name = name;    
            participant.Password = password;
            dbase.SaveChanges();
        }

        internal void UpdatePasswordMC(string email, string password)
        {
            var participant = dbase.Participants.FirstOrDefault(o => o.Email == email);
            if (participant == null) throw new BusinessException($"Participant with email {email} does not exists");
            participant.Password = password;
            dbase.SaveChanges();
        }

        internal void DeleteParticipant(int participantId)
        {
            if (dbase.Participants == null) throw new Exception("Dbase table [Participants] is null");
            var participant = dbase.Participants.FirstOrDefault(o => o.Id == participantId);
            if (participant == null) throw new BusinessException("Participant does not exists");
            participant.IsDeleted = true;
            dbase.SaveChanges();
        }

        internal void SetParticipants(int sessionId, List<int>? participantIds)
        {
            if (dbase == null) throw new Exception("Server dbase is null");
            if (dbase.Participants == null) throw new Exception("Dbase table [Participants] is null");
            if (participantIds == null) throw new Exception("Participant Ids are null");
            var session = GetTopicSession(sessionId);
            if (session == null) throw new Exception("Session is null");

            var participants = dbase.Participants.Where(o => participantIds.Contains(o.Id)).ToList();
            SetParticipants(session, participants);
        }

        internal void SetParticipants(TopicSessionCx session, List<ParticipantCx>? updateParticipants)
        {
            if (updateParticipants == null) throw new Exception("Participants are null");
            var currentParticipants = session.AllocatedParticipants.ToList();

            // Remove missing ones
            var missingParticipants = currentParticipants.Except(updateParticipants).ToList();
            currentParticipants.RemoveAll(o=> missingParticipants.Contains(o));

            // Add new ones
            var addParticipants = updateParticipants.Except(currentParticipants);
            currentParticipants.AddRange(addParticipants);

            session.AllocatedParticipants = currentParticipants;

            dbase.SaveChanges();

        }

        internal void UpdateDelegates(int meetingId, List<int> delegateIds)
        {
            var meeting = GetMeeting(meetingId);
            if (meeting == null) throw new Exception("Meeting is null");
            var newDelegates = GetParticipants(delegateIds);
            meeting.Delegates = newDelegates??new();
            dbase.SaveChanges();
        }


        #endregion

        #region *// Login
        internal (ParticipantCx? participant, string errorMessage) SignIn(string email, string password)
        {
            var participant = dbase.Participants.FirstOrDefault(o=>o.Email.ToLower() == email.ToLower() && o.Password == password);
            if (participant == null) {
                if (!dbase.Participants.ToList().Exists(o=>o.Email == email))
                {
                    return (null, $"User with email [{email}] does not exist");
                }
                else
                {
                    return (null, "Incorrect password");
                }
            }
            return (participant, "");
        }

        internal (ParticipantCx? participant, string errorMessage) SignIn(string pin)
        {
            var foundPin = dbase.Pins.Include(o=>o.Participant).FirstOrDefault(o => o.Value == pin);
            if (foundPin != null)
            {
                if (!foundPin.IsValid)
                {
                    return (null, $"The entered pin has expired ({ConstantsGlobal.PinTimeoutInDays} days validity)");
                }
                return (foundPin.Participant, "");
            }

            return (null, $"The entered pin does not exist");
        }

        internal PinCx CreatePin(ParticipantCx participant)
        {
            var pin = new PinCx { Value = new Random().Next(1, 999999).ToString("######"), Participant = participant, DateTimeStamp = DateTimeOffset.Now };
            dbase.Pins.Add(pin);
            dbase.SaveChanges();
            return pin;
        }

        // https://mailtrap.io/inboxes
        public async Task<string> SendRecoveryPin(string toEmail)
        {
            var pin = new Random().Next(1, 999999);
            var client = new SmtpClient("smtp.mailtrap.io", 2525)
            {
                Credentials = new NetworkCredential("9dc87946ebe6cb", "38ff9c59f93713"),
                EnableSsl = true
            };

            await client.SendMailAsync("treeapps.develop@gmail.com", toEmail, "EasyMinutes: Recover password", $"In the EasyMinutes app, please enter the following pin where prompted: {pin}");
            Debug.WriteLine($"Recovery pin: {pin}");
            return pin.ToString();
        }

        public void ConfirmEmail(string participantId)
        {
            if (participantId == "") throw new Exception("Id is empty");
            var success = int.TryParse(participantId, out int id);
            if (success) throw new Exception("Id is not a number");
            var participant = GetParticipant(id);
            if (participant == null) throw new Exception("Participant does not exist");
            participant.IsEmailConfirmed = true;
            dbase.SaveChanges();
        }
        public void Subscribe(string participantId)
        {
            if (participantId == "") throw new Exception("Id is empty");
            var success = int.TryParse(participantId, out int id);
            if (!success) throw new Exception("Id is not a number");
            var participant = GetParticipant(id);
            if (participant == null) throw new Exception("Participant does not exist");
            participant.IsUnsubscribed = false;
            dbase.SaveChanges();
        }

        public void UnSubscribe(string participantId)
        {
            if (participantId == "") throw new Exception("Id is empty");
            var success = int.TryParse(participantId, out int id);
            if (!success) throw new Exception("Id is not a number");
            var participant = GetParticipant(id);
            if (participant == null) throw new Exception("Participant does not exist");
            participant.IsUnsubscribed = true;
            dbase.SaveChanges();
        }

        #endregion
        internal void DistributeMeeting(int meetingId, DistributeFilterOptions distributeFilterOption, List<int> participantIds)
        {
            List<ParticipantCx> filteredParticipants = new();

            var meeting = GetMeeting(meetingId);
            if (meeting == null) throw new Exception("Meeting is null");

            switch (distributeFilterOption)
            {
                case DistributeFilterOptions.All:
                    filteredParticipants = GetAllParticipants(meeting) ?? new();
                    break;
                case DistributeFilterOptions.Allocated:
                    filteredParticipants = GetAllocatedParticipants(meeting) ?? new();
                    break;
                case DistributeFilterOptions.Selective:
                    filteredParticipants = GetParticipants(participantIds) ?? new();
                    break;
                default:
                    break;
            }

            filteredParticipants = filteredParticipants.Where(o => o.Email != "" && !o.IsUnsubscribed).ToList();

            if (filteredParticipants == null || filteredParticipants.Count == 0) throw new BusinessException("Meeting has no participants with email adresses. There is no need to distribute");
            Debug.WriteLine("Filtered participants");
            filteredParticipants.ForEach(o => Debug.WriteLine($"Id: {o.Id}, Email: {o.Email}, Name: {o.FromDb().Name}"));

            foreach (var filteredParticipant in filteredParticipants)
            {
                var minutesHtmlTable = GenerateMinutesForAllParticipantHtmlTable(meeting, filteredParticipant);
                var body = GenerateDistributionHtmlBodyTemplate();
                body = body.Replace("<<USERNAME>>", filteredParticipant.FromDb().Name);
                body = body.Replace("<<MEETING_NAME>>", meeting.Name);
                body = body.Replace("<<TABLE>>", minutesHtmlTable);
                var pin = CreatePin(filteredParticipant);
                body = body.Replace("<<PIN>>", pin.Value);
                body = body.Replace("<<UNSUBSCRIBE_URL>>", $"{Constants.ServerUrl}/api/signup/UnSubscribe?id={filteredParticipant.Id}"); 
                mailWorker.ScheduleMail(meeting.Name, filteredParticipant.Email, body);
            }

            Debug.WriteLine($"Meeting succesfully distributed");
        }

        private static string GenerateDistributionHtmlBodyTemplate()
        {
            var template = @$"
                Hi <<USERNAME>><br><br>
                Here is the latest meeting minutes for date: {DateTime.Now:d}<br><br>
                Meeting: <<MEETING_NAME>><br><br>
                The topics allocated to you are highlited in <span style='color: Tomato; '>RED</span><br>
                <br>
                <<TABLE>><br>
                <br>
                Regards<br>
                The EasyMinutes administrator<br>
                <br>
                Note: You can view the same information in the mobile app by downloading it from the app store. If not yet signed up, just sign in using PIN: <<PIN>><br>
                To unsubscribe receiving these emails, just click on this <a href=""<<UNSUBSCRIBE_URL>>"">link</a>
                ";
            return template;
        }

        class CellData
        {
            public int TopicId;
            public DateTimeOffset SessionDataTimeStamp;
            public string? SessionDetails;
            public bool IsFiltered;
        }

        // https://stackoverflow.com/questions/36475679/dynamically-create-html-table-in-c-sharp
        private static string GenerateMinutesForAllParticipantHtmlTable(MeetingCx meeting, ParticipantCx filteredParticipant)
        {
            var topics = meeting.Topics;


            #region *// Generate cell data
            List<CellData> cellDatas = new();
            List<DateTimeOffset> headingDateTimeStamps = new();
            foreach (var topic in topics)
            {
                foreach (var session in topic.Sessions)
                {
                    bool isFiltered = session.AllocatedParticipants.Exists(o=>o.Id == filteredParticipant.Id);

                    cellDatas.Add(new CellData { TopicId = topic.Id, SessionDataTimeStamp = session.DateTimeStamp, SessionDetails = session.Details, IsFiltered = isFiltered });
                }
            }
            headingDateTimeStamps = cellDatas.OrderByDescending(o => o.SessionDataTimeStamp).Select(o=>o.SessionDataTimeStamp).Distinct().Take(7).ToList();
            #endregion

            var sb = new StringBuilder();
            using (var table = new Html.Table(sb, id: "some-id"))
            {
                table.StartHead();
                using (var thead = table.AddRow())
                {
                    thead.AddCell("Topic");
                    bool IsFirstCell = true;
                    headingDateTimeStamps.ForEach(o => {
                        if (IsFirstCell)
                        {
                            thead.AddCell($"{o:d}{Environment.NewLine}(Latest)");
                            IsFirstCell = false;
                        }
                        else
                        {
                            thead.AddCell($"{o:d}");
                        }
 
                    });
                }
                table.EndHead();
                table.StartBody();
                foreach (var topic in topics)
                {
                    var tr = table.AddRow(classAttributes: "someattributes");
                    tr.AddCell(topic.Name);
                    foreach (var cellDateTimeStamp in headingDateTimeStamps)
                    {
                        var cell = cellDatas.FirstOrDefault(o => o.TopicId == topic.Id && o.SessionDataTimeStamp == cellDateTimeStamp);
                        if (cell == null)
                        {
                            tr.AddCell("");
                        }
                        else
                        {
                            if (!cell.IsFiltered)
                            {
                                tr.AddCell(cell.SessionDetails ?? "");
                            }
                            else
                            {
                                tr.AddCell(cell.SessionDetails ?? "", id: "selected");
                            }
                        }
                    }
                }
                table.EndBody();
            }
            return sb.ToString();
        }

        internal static void ServerTest()
        {
            try
            {
                var client = new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential("treeapps.develop@gmail.com", "uxgisiktlesjcfkd"),
                    EnableSsl = true,
                };
                //create the mail message 
                var mail = new MailMessage
                {
                    From = new MailAddress("treeapps.develop@gmail.com") //IMPORTANT: This must be same as your smtp authentication address.
                };
                mail.To.Add("hrbraasch@gmail.com");

                //set the content 
                mail.Subject = "This is an email";
                mail.Body = "This is from system.net.mail using C# with smtp authentication.";
                client.Send(mail);
            }
            catch (Exception ex)
            {

                Debug.WriteLine(ex.Message);
            }
        }

        internal static object ChangeTopicHierarchy()
        {
            throw new NotImplementedException();
        }


    }


}
