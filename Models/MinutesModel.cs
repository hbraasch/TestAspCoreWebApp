using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using EasyMinutesServer.Helpers;
using EasyMinutesServer.Shared;
using MatBlazor;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Net;
using System.Net.Mail;
using System.Reflection.Metadata;
using System.Text;
using TreeApps.Maui.Helpers;
using static EasyMinutesServer.Models.DbaseContext;
using static EasyMinutesServer.Shared.Dbase;
using static EasyMinutesServer.Shared.MinutesController;
using static System.Net.Mime.MediaTypeNames;
using Document = DocumentFormat.OpenXml.Wordprocessing.Document;
using TableRow = DocumentFormat.OpenXml.Wordprocessing.TableRow;
using Text = DocumentFormat.OpenXml.Wordprocessing.Text;

namespace EasyMinutesServer.Models
{
    public class MinutesModel
    {

        public bool IsServerRemote => dbase?.Database?.GetConnectionString()?.Contains("Data Source=SQL5080") ?? false;

        readonly DbaseContext dbase;
        readonly IMailWorker mailWorker;

        public string WorkPath = "";

        public MinutesModel(DbaseContext dbase, IMailWorker mailWorker)
        {
            this.dbase = dbase;
            this.mailWorker = mailWorker;
        }

        public void ClearDatabase()
        {
            dbase.Pins.RemoveRange(dbase.Pins);
            dbase.Sessions.RemoveRange(dbase.Sessions);
            dbase.Topics.RemoveRange(dbase.Topics);
            dbase.Meetings.RemoveRange(dbase.Meetings);
            dbase.SaveChanges();
            var masters = dbase.Users.Where(o => o.Slaves.Any()).ToList();
            masters.ForEach(o => o.Slaves.Clear());
            dbase.Users.RemoveRange(dbase.Users);
            dbase.SaveChanges();
        }

        #region *// Meeting and topics
        internal List<MeetingCx> GetMeetings(int userId)
        {
            if (userId == 0) throw new Exception("User id is zero");
            var user = GetUser(userId);
            if (user == null) throw new Exception("User does not exist");
            var userProxies = GetUserProxies(user);

            var meetings = dbase.Meetings
                .Include(o => o.Delegates.Where(d => !d.IsDeleted))
                .Include(o => o.Author)
                .Include(o => o.Topics.Where(t => !t.IsDeleted)).ThenInclude(t => t.Sessions.Where(s => !s.IsDeleted)).ThenInclude(a => a.AllocatedParticipants.Where(p => !p.IsDeleted)).Where(o=>!o.IsDeleted).ToList();
            
            var allAuthorMeetings = meetings.Where(o => userProxies.Contains(o.Author)).ToList();
            var allDelegatedMeetings = meetings.Where(o=>userProxies.Intersect(o.Delegates).Any()).ToList();
            var allAllocatedMeetings = meetings.Where(meeting => meeting.Topics.Where(topic => topic.Sessions.Where(session => session.AllocatedParticipants.Intersect(userProxies).Any()).ToList().Any()).ToList().Any()).ToList();
            var allMeetings = new List<MeetingCx>();
            allMeetings.AddRange(allAuthorMeetings);
            allMeetings.AddRange(allDelegatedMeetings);
            allMeetings.AddRange(allAllocatedMeetings);
            return allMeetings.Distinct().OrderBy(o=>o.DisplayOrder).ToList();
        }



        internal MeetingCx? GetMeeting(int meetingId)
        {
            if (dbase.Meetings == null) return null;
            var meetings = dbase.Meetings
                .Include(o => o.Delegates.Where(d => !d.IsDeleted))
                .Include(o => o.Author)
                .Include(o => o.Topics.Where(t=>!t.IsDeleted)).ThenInclude(t => t.Sessions.Where(s=>!s.IsDeleted)).ThenInclude(a => a.AllocatedParticipants.Where(p=>!p.IsDeleted)).Where(o => !o.IsDeleted);
            return meetings.FirstOrDefault(o => o.Id == meetingId);
        }

        internal MeetingCx CreateMeeting(int authorId, string meetingName)
        {
            var user = GetUser(authorId);
            if (user == null) throw new Exception("User does not exist");
            if (!user.IsSignUpUser) throw new Exception("User is not signed up");
            var meetings = GetMeetings(authorId);
            var meeting = new MeetingCx
            {
                Name = meetingName,
                Author = user,
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
            var author = GetUser(authorId);
            if (author == null) throw new Exception("Author does not exist");
            meeting.Author = author;
            ChangeMeetingDelegates(meeting, delegateIds);
            meeting.Name = meetingName;
            dbase.SaveChanges();
        }

        private void ChangeMeetingDelegates(MeetingCx meeting, List<int> delegateIds)
{
            var updateDelegates = dbase.Users.Where(p => delegateIds.Contains(p.Id)).ToList();
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
            return meeting.Topics.Where(o=>!o.IsDeleted).ToList();
        }

        internal TopicCx? GetTopic(int topicId)
        {
            if (dbase.Topics == null) return null;
            if (topicId == 0) return null;
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
            var newestSession = GetNewestSession(meeting);
            topic.Sessions.Add(new TopicSessionCx { Version = 1, DateTimeStamp = newestSession?.DateTimeStamp??DateTimeOffset.Now, ToBeCompletedDate = ConstantsGlobal.DateMinValue });
            dbase.SaveChanges();

            return topic;
        }

        private static TopicSessionCx? GetNewestSession(MeetingCx meeting)
        {
            var sessions = meeting.Topics.SelectMany(o => o.Sessions);
            sessions = sessions.OrderBy(o => o.DateTimeStamp);
            if (!sessions.Any()) return null;
            return sessions.OrderBy(o => o.DateTimeStamp).Last();


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

        internal void SetTopicChecked(int topicId, bool isChecked)
        {
            var topic = dbase.Topics.FirstOrDefault(o => o.Id == topicId);
            if (topic == null) throw new Exception("Topic does not exist");
            topic.IsChecked = isChecked;
            dbase.SaveChanges();
        }

        internal void SetTopicsChecked(int meetingId, List<int> topicIds)
        {
            var meetingTopics = GetTopics(meetingId);
            foreach (var topic in meetingTopics)
            {
                if (topicIds.Contains(topic.Id)) topic.IsChecked = true;
                else topic.IsChecked = false;
            }
            dbase.SaveChanges();
        }

        internal List<TopicCx> ChangeTopicsDisplayOrder(int meetingId, int topicId, bool isMoveUp)
        {
            var topic = GetTopic(topicId);
            if (topic == null) throw new Exception("Topic is null");
            var meeting = GetMeeting(meetingId);
            if (meeting == null) throw new Exception("Meeting is null");

            var changeTopics = GetChildTopics(meeting, topic.ParentId);
            if (changeTopics.Count == 0) return new();

            // Ensure displayorder integrity
            var displayOrder = 0;
            changeTopics.ForEach(o => o.DisplayOrder = displayOrder++);
            var moveItem = changeTopics.FirstOrDefault(o => o.Id == topicId);
            if (moveItem == null) throw new Exception("Move item is null");
            // Move
            if (isMoveUp)
            {
                var moveItemIndex = moveItem.DisplayOrder;
                var newMoveItemIndex = moveItemIndex - 1;
                newMoveItemIndex = Math.Max(newMoveItemIndex, 0);
                changeTopics[newMoveItemIndex].DisplayOrder = moveItemIndex;
                changeTopics[moveItemIndex].DisplayOrder = newMoveItemIndex;
            }
            else
            {
                var moveItemIndex = moveItem.DisplayOrder;
                var newMoveItemIndex = moveItemIndex + 1;
                newMoveItemIndex = Math.Min(newMoveItemIndex, changeTopics.Count - 1);
                changeTopics[newMoveItemIndex].DisplayOrder = moveItemIndex;
                changeTopics[moveItemIndex].DisplayOrder = newMoveItemIndex;
            }
            dbase.SaveChanges();

            return changeTopics;

        }

        internal List<TopicCx> ChangeTopicHierarchy(int meetingId, int aboveTopicId, int changeTopicId, bool demote)
        {
            var meeting = GetMeeting(meetingId);
            if (meeting == null) throw new Exception("Meeting is null");

            var changeTopic = GetTopic(changeTopicId);
            if (changeTopic == null) throw new Exception("Change topic is null");
            if (demote)
            {
                // [change] becomes last child of [above]

                if (aboveTopicId == 0) throw new Exception("First item cannot move down");
                var aboveTopic = GetTopic(aboveTopicId);
                if (aboveTopic == null) throw new Exception("Above topic is null");

                #region *// Remove from old parent's children and renumber
                List<TopicCx> removeFromChildren = new();
                if (aboveTopic.ParentId == 0)
                {
                    removeFromChildren = meeting.Topics;
                }
                else
                {
                    removeFromChildren = GetChildTopics(meeting, aboveTopic.ParentId);
                }
                var removeFromChildrenTail = removeFromChildren.Where(o => o.DisplayOrder > changeTopic.DisplayOrder).ToList();
                removeFromChildrenTail.ForEach(o => o.DisplayOrder -= 1);
                #endregion

                #region *// Add to new parent's children. Number new addition as list item
                List<TopicCx> addToChildren = GetChildTopics(meeting, aboveTopicId);
                changeTopic.DisplayOrder = addToChildren.Count;
                #endregion


                // Change hierarchy
                changeTopic.ParentId = aboveTopicId;
          
            }
            else
            {
                // Promote

                var changeParent = GetTopic(changeTopic.ParentId);
                if (changeParent == null)
                {
                    // [change] becomes new root topic

                    // All old root children gets demoted
                    var oldRootChildren = GetChildTopics(meeting, 0).Where(o=>o.Id != changeTopicId).ToList();
                    oldRootChildren.ForEach(o => o.ParentId = changeTopicId);

                    // Old root children loses [change] and gets renumbered
                    var newChangeChildren = GetChildTopics(meeting, changeTopicId).Where(o=>o.Id != changeTopicId).ToList();
                    int order = 0;
                    newChangeChildren.ForEach(o=>o.DisplayOrder = order++);

                    // Promote [change]
                    changeTopic.ParentId = 0;

                    // [change] is first of all future root children
                    changeTopic.DisplayOrder = 0;
                }
                else
                {
                    // Only [change](and its children) gets promoted
                    // [change] becomes peer of parent by being inserted just after current parent
                    // Current parent's children loses ONLY [change] 
                    #region *// Make changes to parent children
                    List<TopicCx> peerChildren = GetChildTopics(meeting, changeParent.ParentId); ;
                    var peerChildrenTail = peerChildren.Where(o => o.DisplayOrder > changeParent.DisplayOrder).ToList();
                    peerChildrenTail.ForEach(o => o.DisplayOrder += 1);

                    changeTopic.ParentId = changeParent.ParentId;
                    changeTopic.DisplayOrder = changeParent.DisplayOrder + 1;
                    #endregion

                    #region *// Remove [change] from previous parent's children by correcting their display order
                    var oldParentChildren = GetChildTopics(meeting, changeParent.Id);
                    int order = 0;
                    oldParentChildren.ForEach(o => o.DisplayOrder = order++);
                    #endregion
                }


            }
            dbase.SaveChanges();
            return GetTopics(meetingId);
        }

        internal void DeleteTopic(int topicId)
        {
            var topic = GetTopic(topicId);
            if (topic == null) throw new Exception("Topic is null");
            var sessions = GetTopicSessions(topicId);
            sessions.ForEach(o => o.IsDeleted = true);
            topic.IsDeleted = true;
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
                Version = currentTopicSession.Version + 1,
                Notes = currentTopicSession.Notes
            };
            var topic = currentTopicSession.Topic;
            if (topic == null) throw new Exception("Topic is null");
            topic.Sessions.Add(newTopicSession);

            SetTopicParticipants(newTopicSession, topicSessionParticipants);
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

        public void UpdateTopic(int sessionId, string title, string details, List<int>? participantIds, DateTimeOffset toBeCompletedDate, string notes)
        {
            List<UserCx> participants = new();
            if (participantIds != null && participantIds.Count != 0)
            {
                if (dbase.Users != null) participants = dbase.Users.Where(o => participantIds.Contains(o.Id)).ToList();
            }
            var session = GetTopicSession(sessionId);
            if (session == null) throw new Exception("Current topic session is null");
            UpdateTopic(session.TopicId, title);
            UpdateTopicSession(session, details, participants, toBeCompletedDate, notes);
        }

        private void UpdateTopicSession(TopicSessionCx session, string details, List<UserCx> participants, DateTimeOffset toBeCompletedDate, string notes)
        {
            session.Details = details;
            session.ToBeCompletedDate = toBeCompletedDate;
            SetTopicParticipants(session, participants);
            session.Notes = notes;
            dbase.SaveChanges();
        } 
        #endregion


        #endregion

        #region *// Users
        public UserCx? GetUser(int? userId)
        {
            if (userId == null) throw new Exception("User id is null");
            if (dbase.Users == null) return null;
            return dbase.Users.Include(o=>o.Slaves).ThenInclude(p=>p.Slave).FirstOrDefault(o => o.Id == userId && !o.IsDeleted);
        }

        /// <summary>
        /// Get id of all users (compact and signedUp) that shares the same email address
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public List<UserCx> GetUserProxies(int userId)
        {
            var user = GetUser(userId);
            if (user == null) throw new Exception("User  is null");
            return GetUserProxies(user);
        }

        public List<UserCx> GetUserProxies(UserCx user)
        {
            return dbase.Users.Where(o => !o.IsDeleted && o.Email == user.Email).ToList();
        }

        internal int GetUserId(string email)
        {
            return dbase.Users.FirstOrDefault(o => o.Email == email && o.IsSignUpUser)?.Id??0;
        }


        public List<UserCx> GetOwnerUsers(int ownerId)
        {
            var list = new List<UserCx>();
            var owner = GetUser(ownerId);
            if (owner == null) return new();
            var users = owner.Slaves.Select(o => o.Slave).Where(o=>!o.IsDeleted).ToList();
            return users;
        }

        /// <summary>
        /// Used to get Author, Delegates and Allocated participants
        /// </summary>
        /// <param name="meeting"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public List<UserCx>? GetAllUsers(MeetingCx meeting)
        {
            if (meeting == null) throw new Exception("Meeting is null");
            var users = new List<UserCx>();
            if (meeting.Author != null) users.Add(meeting.Author);
            users.AddRange(meeting.Delegates);
            users.AddRange(GetAllocatedParticipants(meeting) ?? new());

            return users.Distinct().ToList();
        }

        /// <summary>
        /// Used to only get the allocated participants to meeting topics
        /// </summary>
        /// <param name="meeting"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public List<UserCx>? GetAllocatedParticipants(MeetingCx meeting)
        {
            if (meeting == null) throw new Exception("Meeting is null");
            var participants = new List<UserCx>();
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
        /// Used as a converter from list of user ids to list of users
        /// </summary>
        /// <param name="userIds"></param>
        /// <returns></returns>
        public List<UserCx>? GetUsers(List<int> userIds)
        {
            if (userIds == null) return new();
            if (dbase.Users == null) return new();
            return dbase.Users.Where(o=>userIds.Contains(o.Id)).ToList();
        }

        internal List<UserCx> GetSessionParticipants(int sessionId)
        {
            var session = GetTopicSession(sessionId);
            if (session == null) throw new Exception("Session is null");
            return session.AllocatedParticipants.Where(o=>!o.IsDeleted).ToList();
        }


        internal UserCx AddCompactUser(string name, string email)
        {
            email = email.Trim();
            name = name.Trim();

            if (email == "" && name == "") throw new Exception("Email or name must be defined");

            var user = new UserCx() { Email = email, Name = name, IsSignUpUser = false };
            dbase.Users.Add(user);

            dbase.SaveChanges();
            return user;
        }

        internal UserCx AddSignUpUser(string email, string password)
        {
            email = email.Trim();
            password = password.Trim();

            if (email == "") throw new Exception("Email must be defined");
            if (password == "") throw new Exception("Password must be defined");

            if (dbase.Users.ToList().Exists(o => o.IsSignUpUser && o.Email == email)) throw new Exception("User with that email already exists");

            var user = new UserCx() { Email = email, Password = password, IsSignUpUser = true };
            dbase.Users.Add(user);

            dbase.SaveChanges();
            return user;
        }

        internal void AddOwnerUser(int ownerId, int userId)
        {
            if (ownerId == 0) throw new Exception("Owner must be defined");
            var owner = GetUser(ownerId);
            if (owner == null) throw new Exception("Owner is null");
            if (userId == 0) throw new Exception("User must be defined");
            var user = GetUser(userId);
            if (user == null) throw new Exception("User is null");
            var slaves = owner.Slaves.Select(o => o.Slave);
            if (slaves.Select(o=>o.Id).Contains(userId)) return;
            owner.Slaves.Add(new UserMasterSlave { Master = owner, Slave = user });
            dbase.SaveChanges();
        }

        internal void UpdateUser(int userId, string email, string name, string password)
        {
            var user = dbase.Users.FirstOrDefault(o => o.Id == userId);
            if (user == null) throw new BusinessException("User does not exists");
            user.Email = email;
            user.Name = name;    
            if (password != "") user.Password = password;
            dbase.SaveChanges();
        }

        internal void UpdatePasswordMC(string email, string password)
        {
            var user = dbase.Users.FirstOrDefault(o => o.Email == email);
            if (user == null) throw new BusinessException($"User with email {email} does not exists");
            user.Password = password;
            dbase.SaveChanges();
        }

        internal void DeleteUser(int userId)
        {
            var user = dbase.Users.FirstOrDefault(o => o.Id == userId);
            if (user == null) throw new BusinessException("User does not exists");
            user.IsDeleted = true;
            dbase.SaveChanges();
        }

        internal void SetParticipants(int sessionId, List<int>? participantIds)
        {
            if (participantIds == null) throw new Exception("ParticipantIds are null");
            var session = GetTopicSession(sessionId);
            if (session == null) throw new Exception("Session is null");

            var participants = dbase.Users.Where(o => participantIds.Contains(o.Id)).ToList();
            SetTopicParticipants(session, participants);
        }

        internal void SetTopicParticipants(TopicSessionCx session, List<UserCx>? updateParticipants)
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
            var newDelegates = GetUsers(delegateIds);
            meeting.Delegates = newDelegates??new();
            dbase.SaveChanges();
        }


        #endregion

        #region *// Login
        internal (UserCx? user, string errorMessage) SignIn(string email, string password)
        {
            var user = dbase.Users.FirstOrDefault(o=>o.Email.ToLower() == email.ToLower() && o.Password == password && o.IsSignUpUser);
            if (user == null) {
                if (!dbase.Users.ToList().Exists(o=>o.Email == email))
                {
                    return (null, $"User with email [{email}] does not exist");
                }
                else
                {
                    return (null, "Incorrect password");
                }
            }
            return (user, "");
        }


        internal (UserCx? user, string errorMessage) SignIn(string pin)
        {
            var foundPin = dbase.Pins.Include(o=>o.User).FirstOrDefault(o => o.Value == pin);
            if (foundPin != null)
            {
                if (!foundPin.IsValid)
                {
                    return (null, $"The entered pin has expired ({ConstantsGlobal.PinTimeoutInDays} days validity)");
                }
                return (foundPin.User, "");
            }

            return (null, $"The entered pin does not exist");
        }

        internal PinCx CreatePin(UserCx user)
        {
            var pin = new PinCx { Value = new Random().Next(1, 999999).ToString("######"), UserId = user.Id, DateTimeStamp = DateTimeOffset.Now };
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

        public void ConfirmEmail(string userId)
        {
            if (userId == "") throw new Exception("Id is empty");
            var success = int.TryParse(userId, out int id);
            if (success) throw new Exception("Id is not a number");
            var user = GetUser(id);
            if (user == null) throw new Exception("User does not exist");
            user.IsEmailConfirmed = true;
            dbase.SaveChanges();
        }
        public void Subscribe(string userId)
        {
            if (userId == "") throw new Exception("UserId is empty");
            var success = int.TryParse(userId, out int id);
            if (!success) throw new Exception("Id is not a number");
            var user = GetUser(id);
            if (user == null) throw new Exception("User does not exist");
            user.IsUnsubscribed = false;
            dbase.SaveChanges();
        }

        public void UnSubscribe(string userId)
        {
            if (userId == "") throw new Exception("Id is empty");
            var success = int.TryParse(userId, out int id);
            if (!success) throw new Exception("Id is not a number");
            var user = GetUser(id);
            if (user == null) throw new Exception("User does not exist");
            user.IsUnsubscribed = true;
            dbase.SaveChanges();
        }

        #endregion
        internal List<PreviewData> DistributeMeeting(int meetingId, DistributeFilterOptions distributeFilterOption, List<int> userIds, bool isPreview = false)
        {
            List<PreviewData> previewDatas = new ();
            List<UserCx> filteredUsers = new();

            var meeting = GetMeeting(meetingId);
            if (meeting == null) throw new Exception("Meeting is null");

            switch (distributeFilterOption)
            {
                case DistributeFilterOptions.All:
                    filteredUsers = GetAllUsers(meeting) ?? new();
                    break;
                case DistributeFilterOptions.Allocated:
                    filteredUsers = GetAllocatedParticipants(meeting) ?? new();
                    break;
                case DistributeFilterOptions.Selective:
                    filteredUsers = GetUsers(userIds) ?? new();
                    break;
                default:
                    break;
            }

            filteredUsers = filteredUsers.Where(o => o.Email != "" && !o.IsUnsubscribed).ToList();

            if (filteredUsers == null || filteredUsers.Count == 0) throw new BusinessException("Meeting has no users with email adresses. There is no need to distribute");
            Debug.WriteLine("Filtered users");
            filteredUsers.ForEach(o => Debug.WriteLine($"Id: {o.Id}, Email: {o.Email}, Name: {o.FromDb().Name}"));

            foreach (var filteredUser in filteredUsers)
            {
                var minutesHtmlTable = GenerateMinutesHtmlTable(meeting, filteredUser);
                var minutesWordDocFullFilename = GenerateMinutesWordDoc(meeting, filteredUser);
                var body = GenerateDistributionHtmlBodyTemplate();
                body = body.Replace("<<USERNAME>>", filteredUser.FromDb().Name);
                body = body.Replace("<<MEETING_NAME>>", meeting.Name);
                body = body.Replace("<<TABLE>>", minutesHtmlTable);
                var pin = CreatePin(filteredUser);
                body = body.Replace("<<PIN>>", pin.Value);
                var serverUrl = IsServerRemote ? Constants.RemoteServerUrl : Constants.LocalServerUrl;
                body = body.Replace("<<UNSUBSCRIBE_URL>>", $"{serverUrl}/api/signup/UnSubscribe?id={filteredUser.Id}");
                if (isPreview)
                {
                    previewDatas.Add(new PreviewData { Message = $"Minutes to recipient {filteredUser.Email}", Html = body });
                }
                else
                {
                    mailWorker.ScheduleMail(meeting.Name, filteredUser.Email, body, minutesWordDocFullFilename);
                }
            }

            Debug.WriteLine(isPreview? $"Meeting preview succesfully generated" : $"Meeting succesfully distributed");
            return previewDatas;
        }

        private static string GenerateDistributionHtmlBodyTemplate()
        {
            var template = @$"
                Hi <<USERNAME>><br><br>
                Meeting: <<MEETING_NAME>>.<br><br>
                Here is the latest minutes dated {DateTime.Now:d}.<br><br>
                The topics allocated to you are highlited in <span style='color: Tomato; '>RED</span>.<br>
                <br>
                <<TABLE>><br>
                <br>
                Regards.<br><br>
                The EasyMinutes administrator.<br>
                <br><br>
                Notes:<br><br> 
                You can view the same information in the mobile app by downloading it from the app store. If not yet signed up, just sign in using PIN: <<PIN>>.<br><br>
                To unsubscribe receiving these emails, just click on this <a href=""<<UNSUBSCRIBE_URL>>"">link</a>
                ";
            return template;
        }
        internal List<PreviewData> GetDistributedMeetingPreviews(int meetingId, DistributeFilterOptions distributeFilterOption, List<int> userIds)
        {
            return DistributeMeeting(meetingId, distributeFilterOption, userIds, true);
        }
        class CellData
        {
            public int TopicId;
            public DateTimeOffset SessionDataTimeStamp;
            public string? SessionText;
            public bool IsFiltered;
        }

        // https://stackoverflow.com/questions/36475679/dynamically-create-html-table-in-c-sharp
        private static string GenerateMinutesHtmlTable(MeetingCx meeting, UserCx filteredUser)
        {
            var topics = meeting.Topics;


            #region *// Generate cell data
            List<CellData> cellDatas = new();
            List<DateTimeOffset> headingDateTimeStamps = new();
            foreach (var topic in topics)
            {
                foreach (var session in topic.Sessions)
                {
                    bool isFiltered = session.AllocatedParticipants.Exists(o=>o.Id == filteredUser.Id);

                    var partipantsText = "";
                    foreach (var participant in session.AllocatedParticipants)
                    {
                        if (partipantsText == "")
                        {
                            partipantsText = $"<small>({participant.Name}";
                        }
                        else
                        {
                            partipantsText += $",<br>{participant.Name}";
                        }
                    }
                    var sessionText = "";
                    if (partipantsText != "") {
                        partipantsText += ")</small>";
                        sessionText = $"<div>{session.Details}<br>{partipantsText}</div>";
                    }
                    else
                    {
                        sessionText = session.Details;
                    }

                    cellDatas.Add(new CellData { TopicId = topic.Id, SessionDataTimeStamp = session.DateTimeStamp, SessionText = sessionText, IsFiltered = isFiltered });
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
                table.StartBody(id: "fontsize");
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
                                tr.AddCell(cell.SessionText ?? "");
                            }
                            else
                            {
                                tr.AddCell(cell.SessionText ?? "", id: "selected");
                            }
                        }
                    }
                }
                table.EndBody();
            }
            return sb.ToString();
        }

        class WordCellData
        {
            public int TopicId;
            public string? Details;
            public DateTimeOffset? DateTimeStamp;
            public List<string>? Participants;
            public bool? IsFiltered;
        }

        private string GenerateMinutesWordDoc(MeetingCx meeting, UserCx filteredUser)
        {
            var topics = meeting.Topics;


            #region *// Generate cell data
            List<WordCellData> cellDatas = new();
            TopicSessionCx? currentSession = null;
            TopicSessionCx? previousSession = null;
            foreach (var topic in topics)
            {
                currentSession = null;
                previousSession = null;

                foreach (var session in topic.Sessions.OrderByDescending(o => o.DateTimeStamp))
                {
                    if ((currentSession == null) && (previousSession == null))
                    {
                        if (!session.Details.IsNullOrEmpty())
                        {
                            currentSession = session;
                        }
                    }
                    else if ((currentSession != null) && (previousSession == null))
                    {
                        if (!session.Details.IsNullOrEmpty())
                        {
                            previousSession = session;
                            break;
                        }
                    }
                }
                if (currentSession != null)
                {
                    var isFiltered = currentSession.AllocatedParticipants.Exists(o => o.Id == filteredUser.Id);
                    cellDatas.Add(new WordCellData { TopicId = topic.Id, DateTimeStamp = currentSession.DateTimeStamp, Details = currentSession.Details, Participants = currentSession.AllocatedParticipants.Select(o=>o.Name).ToList(), IsFiltered = isFiltered });
                }
                else
                {
                    cellDatas.Add(new WordCellData { TopicId = topic.Id, DateTimeStamp = null, Details = null, Participants = null, IsFiltered = null });
                }
                if (previousSession != null)
                {
                     var isFiltered = previousSession.AllocatedParticipants.Exists(o => o.Id == filteredUser.Id);
                    cellDatas.Add(new WordCellData { TopicId = topic.Id, DateTimeStamp = previousSession.DateTimeStamp, Details = previousSession.Details, Participants = previousSession.AllocatedParticipants.Select(o => o.Name).ToList(), IsFiltered = isFiltered });
                }
                else
                {
                    cellDatas.Add(new WordCellData { TopicId = topic.Id, DateTimeStamp = null, Details = null, Participants = null, IsFiltered = null });
                }

            }
            #endregion

            var (targetPath, wordDoc, document, body) = GenerateWordDocStart(meeting.Name, filteredUser.Id);

            #region *// Add table to body
            Table table = new Table();
            TableProperties tableProperties = new TableProperties();
            tableProperties.Append(new TableWidth() { Width = "5000", Type = TableWidthUnitValues.Pct });
            table.Append(tableProperties);

            TableProperties props = new TableProperties(
                new TableBorders(
                new TopBorder
                {
                    Val = new EnumValue<BorderValues>(BorderValues.Single),
                    Size = 1
                },
                new BottomBorder
                {
                    Val = new EnumValue<BorderValues>(BorderValues.Single),
                    Size = 1
                },
                new LeftBorder
                {
                    Val = new EnumValue<BorderValues>(BorderValues.Single),
                    Size = 1
                },
                new RightBorder
                {
                    Val = new EnumValue<BorderValues>(BorderValues.Single),
                    Size = 1
                },
                new InsideHorizontalBorder
                {
                    Val = new EnumValue<BorderValues>(BorderValues.Single),
                    Size = 1
                },
                new InsideVerticalBorder
                {
                    Val = new EnumValue<BorderValues>(BorderValues.Single),
                    Size = 1
                }));
            table.AppendChild<TableProperties>(props);

            var tr = new TableRow();
            tr.Append(GenerateWordTextTableCell("Topic"));
            tr.Append(GenerateWordTextTableCell("Latest"));
            tr.Append(GenerateWordTextTableCell("Previous"));
            table.Append(tr);


            foreach (var topic in topics)
            {
                var cellData = cellDatas.Where(o => o.TopicId == topic.Id).ToList();
                tr = new TableRow();
                tr.Append(GenerateWordTextTableCell(topic.Name));
                tr.Append(GenerateWordDetailsTableCell(cellData[0]));
                tr.Append(GenerateWordDetailsTableCell(cellData[1]));
                table.Append(tr);
            }

            body.Append(table); 
            #endregion

            document.Append(body);

            wordDoc.MainDocumentPart.Document = document;

            wordDoc.Close();

            Debug.WriteLine(targetPath);

            return targetPath;
        }

        private static TableCell GenerateWordTextTableCell(string topicName)
        {
            var tc = new TableCell();

            RunProperties rp = new RunProperties();
            // Add the Color object for your run into the RunProperties
            rp.Append(new DocumentFormat.OpenXml.Wordprocessing.Color() { Val = "000000" });
            // Create the Run object
            Run run = new Run();
            // Assign your RunProperties to your Run
            run.RunProperties = rp;
            run.Append(new Text(topicName));

            var tcp = new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Auto });
            // Create the Shading object
            DocumentFormat.OpenXml.Wordprocessing.Shading shading =
                new DocumentFormat.OpenXml.Wordprocessing.Shading()
                {
                    Color = "auto",
                    Fill = "FFFFFF",
                    Val = ShadingPatternValues.Clear
                };
            // Add the Shading object to the TableCellProperties object
            tcp.Append(shading);

            tc.Append(new Paragraph(run));

            tc.Append(tcp);

            return tc;
        }
        private static TableCell GenerateWordDetailsTableCell(WordCellData wordCellData)
        {
            var tc = new TableCell();

            Run runDetails = new Run();
            Run runDateTime = new Run();
            Run runParticipants = new Run();

            // Add your text to your Run
            if (wordCellData.Details != null)
            {
                RunProperties runDetailsProperties = new RunProperties();
                runDetailsProperties.Append(new DocumentFormat.OpenXml.Wordprocessing.Color() { Val = "000000" });
                runDetails.RunProperties = runDetailsProperties;
                runDetails.Append(new Text(wordCellData.Details));
                //runDetails.Append(new Break());

                RunProperties runDateTimeProperties = new RunProperties();
                runDateTimeProperties.Append(new DocumentFormat.OpenXml.Wordprocessing.Color() { Val = "000000" });
                runDateTimeProperties.Append(new FontSize() { Val = "10" });
                runDateTime.RunProperties = runDateTimeProperties;
                runDateTime.Append(new Text($"{wordCellData.DateTimeStamp:d}"));
                //runDateTime.Append(new Break());


                RunProperties runParticipantsProperties = new RunProperties();
                runParticipantsProperties.Append(new DocumentFormat.OpenXml.Wordprocessing.Color() { Val = "000000" });
                runParticipantsProperties.Append(new FontSize() { Val = "10" });
                runParticipants.RunProperties = runParticipantsProperties;

                var participantString = "";
                foreach (var participant in wordCellData.Participants??new())
                {
                    if (participantString == "")
                    {
                        participantString = participant;
                    }
                    else
                    {
                        participantString += ", " + participant;
                    }
                }
                runParticipants.Append(new Text(participantString));
            }
            else
            {
                runDetails.Append(new Text(""));
            }


            var tcp = new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Auto });
            // Create the Shading object
            if (wordCellData.IsFiltered??false)
            {
                DocumentFormat.OpenXml.Wordprocessing.Shading shading = new DocumentFormat.OpenXml.Wordprocessing.Shading()
            {
                Color = "auto",
                Fill = "FF6347",
                Val = ShadingPatternValues.Clear
            };
                // Add the Shading object to the TableCellProperties object
                tcp.Append(shading); 
            }

            tc.Append(new Paragraph(runDetails));
            tc.Append(new Paragraph(runDateTime));
            tc.Append(new Paragraph(runParticipants));

            tc.Append(tcp);

            return tc;
        }

        private (string, WordprocessingDocument, Document, Body) GenerateWordDocStart(string meetingName, int filterUserId)
        {

            string targetFile = System.IO.Path.Combine(WorkPath, $"{filterUserId}_meeting.docx");
            var wordDoc = WordprocessingDocument.Create(targetFile, DocumentFormat.OpenXml.WordprocessingDocumentType.Document, true);
            wordDoc.AddMainDocumentPart();

            Document doc = new Document();
            Body body = new Body();

            // Title
            Paragraph para = new Paragraph();

            ParagraphProperties paragraphProperties1 = new ParagraphProperties();
            ParagraphStyleId paragraphStyleId1 = new ParagraphStyleId() { Val = "Normal" };
            Justification justification1 = new Justification() { Val = JustificationValues.Center };
            ParagraphMarkRunProperties paragraphMarkRunProperties1 = new ParagraphMarkRunProperties();

            paragraphProperties1.Append(paragraphStyleId1);
            paragraphProperties1.Append(justification1);
            paragraphProperties1.Append(paragraphMarkRunProperties1);

            Run run = new Run();
            RunProperties runProperties1 = new RunProperties();

            Text text = new Text() { Text = $"Minutes of Meeting: {meetingName}" };

            run.Append(runProperties1);
            run.Append(text);
            para.Append(paragraphProperties1);
            para.Append(run);

            // Intro
            Paragraph para2 = new Paragraph();

            ParagraphProperties paragraphProperties2 = new ParagraphProperties();
            ParagraphStyleId paragraphStyleId2 = new ParagraphStyleId() { Val = "Normal" };
            Justification justification2 = new Justification() { Val = JustificationValues.Start };
            ParagraphMarkRunProperties paragraphMarkRunProperties2 = new ParagraphMarkRunProperties();

            paragraphProperties2.Append(paragraphStyleId2);
            paragraphProperties2.Append(justification2);
            paragraphProperties2.Append(paragraphMarkRunProperties2);

            Run run2 = new Run();
            RunProperties runProperties3 = new RunProperties();

            run2.AppendChild(new Break());
            run2.AppendChild(new Text($"This is the latest minutes dated {DateTime.Now:d}"));

            para2.Append(paragraphProperties2);
            para2.Append(run2);

            body.Append(para);
            body.Append(para2);

            return (targetFile, wordDoc, doc, body);

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
