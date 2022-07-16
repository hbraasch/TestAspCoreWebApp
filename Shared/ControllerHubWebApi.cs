

#if __CLIENT__
using EasyMinutes.Helpers;
using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Text;
using TreeApps.Maui.Helpers;
using static EasyMinutesServer.Shared.ControllerHubProxy;
using static EasyMinutesServer.Shared.Dbase;
#else
using EasyMinutesServer.Models;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using TreeApps.Maui.Helpers;
using static EasyMinutesServer.Models.DbaseContext;
using static EasyMinutesServer.Shared.Dbase;
#endif

namespace EasyMinutesServer.Shared
{
#if __CLIENT__

#nullable enable
    public class ControllerHubProxy
    {
        bool isServerSecure = true;
        string serverName = "";
        string serverPort = "";
        int serverTimeoutInSec = 100;

        public ControllerHubProxy(bool isServerSecure, string serverName, string serverPort, int serverTimeoutInSec)
        {
            this.isServerSecure = isServerSecure;
            this.serverName = serverName;
            this.serverPort = serverPort;   
            this.serverTimeoutInSec = serverTimeoutInSec;
        }

        public void SetServerUrl(bool isServerSecure, string serverName, string serverPort)
        {
            this.isServerSecure = isServerSecure;
            this.serverName = serverName;
            this.serverPort = serverPort;
        }

        /// <summary>
        /// Used to send command to server using WebApi.
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <param name="command"></param>
        /// <param name="commandName"></param>
        /// <returns>Object of type[T2]</returns>
        /// <exception cref="BusinessException"></exception>
        /// <exception cref="Exception"></exception>
        public async Task<T2?> SendMessage<T1, T2>(T1 command, string commandName, CancellationTokenSource cts) 
        {
            try
            {
                if (Connectivity.NetworkAccess != NetworkAccess.Internet) throw new BusinessException("You are not connected to the internet");

                cts.CancelAfter(new TimeSpan(0, 0, serverTimeoutInSec));
                cts.Token.ThrowIfCancellationRequested();

                HttpClientHandler handler = new ();

                handler.ServerCertificateCustomValidationCallback += (sender, cert, chaun, ssPolicyError) =>
                {
                    return true;
                };

                var client = new HttpClient(handler, false);
                var commandJson = JsonConvert.SerializeObject(command);
                var content = new StringContent(commandJson, Encoding.UTF8, "application/json");
                string url = $"{UrlHelper.GenerateServerAddress(isServerSecure, serverName,serverPort)}/api/Minutes/{commandName}";
                Debug.WriteLine($"SendMessage to url: {url}");
                var response = await client.PostAsync(url, content, cts.Token);
                if (response != null)
                {
                    if (response.IsSuccessStatusCode)
                    {
                        var responseJson = await response.Content.ReadAsStringAsync();
                        var statusResponse = JsonConvert.DeserializeObject<ResponseBase>(responseJson);
                        if (statusResponse == null) throw new Exception($"Server exception: Response is null");
                        if (statusResponse.HasError)
                        {
                            if (!string.IsNullOrEmpty(statusResponse.BusinessErrorMessage)) throw new BusinessException(statusResponse.BusinessErrorMessage);
                            if (!string.IsNullOrEmpty(statusResponse.ExceptionMessage)) throw new Exception($"Server exception: {statusResponse.ExceptionMessage}");
                        }
                        return JsonConvert.DeserializeObject<T2>(responseJson);
                    }
                    else
                    {
                        throw new ApplicationException($"Notice in {nameof(SendMessage)}: {response.ReasonPhrase}");
                    }
                }
                else
                {
                    throw new ApplicationException("Response is null");
                }
            }
            catch (BusinessException)
            {
                throw;
            }
            catch (Exception) when (cts.IsCancellationRequested)
            {
                throw new BusinessException("Cancel requested");
            }
            catch (Exception ex) when (ex is TaskCanceledException)
            {
                throw new BusinessException("Timed out");
            }
            catch (Exception ex) when (ex is OperationCanceledException)
            {
                throw new BusinessException("Operation cancelled");
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"Notice in {nameof(SendMessage)}", ex);
            }
        }

#else
    [Route("api/[controller]/{action}")]
    [ApiController]
    public class MinutesController : ControllerBase
    {
        readonly MinutesModel minutesModel;
        public MinutesController(MinutesModel minutesModel)
        {
            this.minutesModel = minutesModel;
        }

        private static async Task<T2> ReceiveMessage<T1, T2>(T1 command, Func<T1, T2> handler) where T2: ResponseBase, new()
        {
            try
            {
                if (command == null) throw new Exception("Null command received");

                var response = handler.Invoke(command);

                return await Task.FromResult(response);
            }
            catch (Exception ex) when (ex is BusinessException)
            {
                return (T2) new T2 { BusinessErrorMessage = ex.Message, ExceptionMessage = "" };
            }
            catch (Exception ex)
            {
                return (T2) new T2 { ExceptionMessage = ex.Message, BusinessErrorMessage = "" };
            }
        }

        /// <summary>
        /// Used for when the handler is async
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <param name="command"></param>
        /// <param name="handler"></param>
        /// <returns></returns>
        private static async Task<T2> ReceiveMessage<T1, T2>(T1 command, Func<T1, Task<T2>> handler) where T2 : ResponseBase
        {
            try
            {
                if (command == null) throw new Exception("Null command received");

                var response = await handler.Invoke(command);

                return await Task.FromResult(response);
            }
            catch (Exception ex) when (ex is BusinessException)
            {
                return (T2) new ResponseBase { BusinessErrorMessage = ex.Message };
            }
            catch (Exception ex)
            {
                return (T2) new ResponseBase { ExceptionMessage = ex.Message };
            }
        }
#endif
        public class ResponseBase
        {
            public string BusinessErrorMessage { get; set; } = "";
            public string ExceptionMessage { get; set; } = "";
            public bool HasError => !(string.IsNullOrEmpty(BusinessErrorMessage) && string.IsNullOrEmpty(ExceptionMessage));
            public string ErrorMessage => GenerateErorMessage();

            private string GenerateErorMessage()
            {
                if (!string.IsNullOrEmpty(BusinessErrorMessage))
                {
                    return BusinessErrorMessage;
                }
                else if (!string.IsNullOrEmpty(ExceptionMessage))
                {
                    return ExceptionMessage;
                }
                else
                {
                    return "";
                }
            }
        }



        public class PingMC
        {
            public class Command
            {

            }

            public class Response : ResponseBase
            {

            }
        }



#if __CLIENT__
        public async Task<bool> Ping(CancellationTokenSource cts)
        {
            try
            {
                var result = await SendMessage<PingMC.Command, PingMC.Response>(new PingMC.Command(), nameof(Ping), cts);
                if (result == null) return false;
                if (result.HasError)
                {
                    Debug.WriteLine(result.ErrorMessage);
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return false;
            }
        }
#else
        [HttpPost]
        public async Task<ActionResult<PingMC.Response>> Ping([FromBody] PingMC.Command command)
        {
            var response = await ReceiveMessage(command, (command) => {
                return new PingMC.Response();
            });

            return AcceptedAtAction(nameof(Ping), response);
        }
#endif
        public class GetMeetingsMC
        {
            public class Command
            {
                public int UserId { get; set; }
            }

            public class Response : ResponseBase
            {
                public List<Meeting> Meetings { get; set; } = new();
            }
        }
#if __CLIENT__
        internal async Task<List<Meeting>> GetMeetings(int userId, CancellationTokenSource cts)
        {
            return  (await SendMessage<GetMeetingsMC.Command, GetMeetingsMC.Response>(new GetMeetingsMC.Command { UserId = userId }, nameof(GetMeetings), cts))?.Meetings ?? new();
        }
#else
        [HttpPost]
        public async Task<ActionResult<GetMeetingsMC.Response>> GetMeetings([FromBody] GetMeetingsMC.Command command)
        {
            var response = await ReceiveMessage(command, (command) => {
                var meetings = minutesModel.GetMeetings(command.UserId).FromDb();
                return new GetMeetingsMC.Response { Meetings = meetings };
            });

            return AcceptedAtAction(nameof(GetMeetings), response);
        }
#endif
        public class GetMeetingMC
        {
            public class Command
            {
                public int MeetingId { get; set; }
            }

            public class Response : ResponseBase
            {
                public Meeting? Meeting { get; set; }
            }
        }
#if __CLIENT__
        internal async Task<Meeting?> GetMeeting(int meetingId, CancellationTokenSource cts)
        {
            return (await SendMessage<GetMeetingMC.Command, GetMeetingMC.Response>(new GetMeetingMC.Command { MeetingId = meetingId }, nameof(GetMeeting), cts))?.Meeting;
        }
#else
        [HttpPost]
        public async Task<ActionResult<GetMeetingMC.Response>> GetMeeting([FromBody] GetMeetingMC.Command command)
        {
            var response = await ReceiveMessage(command, (command) => {
                var meeting = minutesModel.GetMeeting(command.MeetingId)?.FromDb();
                return new GetMeetingMC.Response { Meeting = meeting };
            });

            return AcceptedAtAction(nameof(GetMeeting), response);
        }
#endif
        public class GetTopicsMC
        {
            public class Command
            {
                public int MeetingId { get; set; }
            }

            public class Response : ResponseBase
            {
                public List<Topic> Topics { get; set; } = new ();
            }
        }

#if __CLIENT__
        internal async Task<List<Topic>> GetTopics(int meetingId, CancellationTokenSource cts)
        {
            return (await SendMessage<GetTopicsMC.Command, GetTopicsMC.Response>(new GetTopicsMC.Command { MeetingId = meetingId }, nameof(GetTopics), cts))?.Topics??new();
        }
#else
        [HttpPost]
        public async Task<ActionResult<GetTopicsMC.Response>> GetTopics([FromBody] GetTopicsMC.Command command)
        {
            var response = await ReceiveMessage(command, (command) => {
                var topics = minutesModel.GetTopics(command.MeetingId).FromDb();
                return new GetTopicsMC.Response { Topics = topics };
            });

            return AcceptedAtAction(nameof(GetTopics), response);
        }
#endif

        public class GetTopicMC
        {
            public class Command
            {
                public int TopicId { get; set; }
            }

            public class Response : ResponseBase
            {
                public Topic? Topic { get; set; }
            }
        }
#if __CLIENT__
        internal async Task<Topic?> GetTopic(int topicId, CancellationTokenSource cts)
        {
            return (await SendMessage<GetTopicMC.Command, GetTopicMC.Response>(new GetTopicMC.Command { TopicId = topicId }, nameof(GetTopic), cts))?.Topic;
        }
#else
        [HttpPost]
        public async Task<ActionResult<GetTopicMC.Response>> GetTopic([FromBody] GetTopicMC.Command command)
        {
            var response = await ReceiveMessage(command, (command) => {
                var topic = minutesModel.GetTopic(command.TopicId)?.FromDb();
                return new GetTopicMC.Response { Topic = topic };
            });

            return AcceptedAtAction(nameof(GetTopic), response);
        }
#endif

        public class GetTopicSessionsMC
        {
            public class Command
            {
                public int TopicSessionsId { get; set; }
            }

            public class Response : ResponseBase
            {
                public List<TopicSession> TopicSessions { get; set; } = new();
            }
        }
#if __CLIENT__
        internal async Task<List<TopicSession>> GetTopicSessions(int topicSessionsId, CancellationTokenSource cts)
        {
            return (await SendMessage<GetTopicSessionsMC.Command, GetTopicSessionsMC.Response>(new GetTopicSessionsMC.Command { TopicSessionsId = topicSessionsId }, nameof(GetTopicSessions), cts))?.TopicSessions??new();
        }
#else
        [HttpPost]
        public async Task<ActionResult<GetTopicSessionsMC.Response>> GetTopicSessions([FromBody] GetTopicSessionsMC.Command command)
        {
            var response = await ReceiveMessage(command, (command) => {
                var topicSessions = minutesModel.GetTopicSessions(command.TopicSessionsId).FromDb();
                return new GetTopicSessionsMC.Response { TopicSessions = topicSessions };
            });

            return AcceptedAtAction(nameof(GetTopicSessions), response);
        }
#endif
        public class GetTopicSessionMC
        {
            public class Command
            {
                public int TopicSessionId { get; set; }
            }

            public class Response : ResponseBase
            {
                public TopicSession? TopicSession { get; set; }
            }
        }

#if __CLIENT__
        internal async Task<TopicSession?> GetTopicSession(int topicSessionId, CancellationTokenSource cts)
        {
            return (await SendMessage<GetTopicSessionMC.Command, GetTopicSessionMC.Response>(new GetTopicSessionMC.Command { TopicSessionId = topicSessionId }, nameof(GetTopicSession), cts))?.TopicSession;
        }
#else
        [HttpPost]
        public async Task<ActionResult<GetTopicSessionMC.Response>> GetTopicSession([FromBody] GetTopicSessionMC.Command command)
        {
            var response = await ReceiveMessage(command, (command) => {
                var topicSession = minutesModel.GetTopicSession(command.TopicSessionId)?.FromDb();
                return new GetTopicSessionMC.Response { TopicSession = topicSession };
            });

            return AcceptedAtAction(nameof(GetTopicSession), response);
        }
#endif

        public class AddTopicSessionMC
        {
            public class Command
            {
                public int CurrentTopicSessionId { get; set; }
            }

            public class Response : ResponseBase
            {

            }
        }

#if __CLIENT__
        internal async Task AddTopicSession(int currentTopicSessionId, CancellationTokenSource cts)
        {
            await SendMessage<AddTopicSessionMC.Command, AddTopicSessionMC.Response>(new AddTopicSessionMC.Command { CurrentTopicSessionId = currentTopicSessionId }, nameof(AddTopicSession), cts);
        }
#else
        [HttpPost]
        public async Task<ActionResult<AddTopicSessionMC.Response>> AddTopicSession([FromBody] AddTopicSessionMC.Command command)
        {
            var response = await ReceiveMessage(command, (command) => {
                minutesModel.AddTopicSession(command.CurrentTopicSessionId, DateTimeOffset.UtcNow);
                return new AddTopicSessionMC.Response();
            });

            return AcceptedAtAction(nameof(AddTopicSession), response);
        }
#endif

        public class AddNewSessionForAllTopicsMC
        {
            public class Command
            {
                public int MeetingId { get; set; }
            }

            public class Response : ResponseBase
            {

            }
        }

#if __CLIENT__
        internal async Task AddNewSessionForAllTopics(int meetingId, CancellationTokenSource cts)
{
            await SendMessage<AddNewSessionForAllTopicsMC.Command, AddNewSessionForAllTopicsMC.Response>(new AddNewSessionForAllTopicsMC.Command { MeetingId = meetingId }, nameof(AddNewSessionForAllTopics), cts);
        }
#else
        [HttpPost]
        public async Task<ActionResult<AddNewSessionForAllTopicsMC.Response>> AddNewSessionForAllTopics([FromBody] AddNewSessionForAllTopicsMC.Command command)
        {
            var response = await ReceiveMessage(command, (command) => {
                minutesModel.AddNewSessionForAllTopics(command.MeetingId);
                return new AddNewSessionForAllTopicsMC.Response();
            });

            return AcceptedAtAction(nameof(AddNewSessionForAllTopics), response);
        }
#endif


        public class UpdateTopicMC
        {
            public class Command
            {
                public int CurrentTopicSessionId { get; set; }
                public string Title { get; set; } = "";
                public string Details { get; set; } = "";
                public List<int> ParticipantIds { get; set; } = new();
                public DateTimeOffset ToBeCompletedDate { get; set; }
            }

            public class Response : ResponseBase
            {

            }
        }
#if __CLIENT__
        internal async Task UpdateTopic(int currentTopicSessionId, string title, string details, List<int> participantIds, DateTimeOffset toBeCompletedDate, CancellationTokenSource cts)
        {
            await SendMessage<UpdateTopicMC.Command, UpdateTopicMC.Response>(new UpdateTopicMC.Command { 
                CurrentTopicSessionId = currentTopicSessionId,
                Title = title,
                Details = details,
                ParticipantIds = participantIds,
                ToBeCompletedDate = toBeCompletedDate
            }, nameof(UpdateTopic), cts);
        }
#else
        [HttpPost]
        public async Task<ActionResult<UpdateTopicMC.Response>> UpdateTopic([FromBody] UpdateTopicMC.Command command)
        {
            var response = await ReceiveMessage(command, (command) => {
                minutesModel.UpdateTopic(command.CurrentTopicSessionId, command.Title, command.Details, command.ParticipantIds, command.ToBeCompletedDate);
                return new UpdateTopicMC.Response();
            });

            return AcceptedAtAction(nameof(UpdateTopic), response);
        }
#endif

        public class GetParticipantMC
        {
            public class Command
            {
                public int ParticipantId { get; set; }
            }

            public class Response : ResponseBase
            {
                public Participant? Participant { get; set; }
            }
        }

#if __CLIENT__
        internal async Task<Participant?> GetParticipant(int participantId, CancellationTokenSource cts)
        {
            return (await SendMessage<GetParticipantMC.Command, GetParticipantMC.Response>(new GetParticipantMC.Command { ParticipantId = participantId }, nameof(GetParticipant), cts))?.Participant;
        }
#else
        [HttpPost]
        public async Task<ActionResult<GetParticipantMC.Response>> GetParticipant([FromBody] GetParticipantMC.Command command)
        {
            var response = await ReceiveMessage(command, (command) => {
                var participant = minutesModel.GetParticipant(command.ParticipantId)?.FromDb();
                return new GetParticipantMC.Response { Participant = participant };
            });

            return AcceptedAtAction(nameof(GetParticipant), response);
        }
#endif

        public class GetParticipantsMC
        {
            public class Command
            {
                public int AuthorId { get; set; }
            }

            public class Response : ResponseBase
            {
                public List<Participant> Participants { get; set; } = new();
            }
        }

#if __CLIENT__
        internal async Task<List<Participant>> GetParticipants(int authorId, CancellationTokenSource cts)
        {
            return (await SendMessage<GetParticipantsMC.Command, GetParticipantsMC.Response>(new GetParticipantsMC.Command { AuthorId = authorId }, nameof(GetParticipants), cts))?.Participants??new();
        }
#else
        [HttpPost]
        public async Task<ActionResult<GetParticipantsMC.Response>> GetParticipants([FromBody] GetParticipantsMC.Command command)
        {
            var response = await ReceiveMessage(command, (command) => {
                var participants = minutesModel.GetParticipants(command.AuthorId)?.FromDb();
                return new GetParticipantsMC.Response { Participants = participants??new() };
            });

            return AcceptedAtAction(nameof(GetParticipants), response);
        }
#endif
        public class GetSessionParticipantsMC
        {
            public class Command
            {
                public int SessionId { get; set; }
            }

            public class Response : ResponseBase
            {
                public List<Participant> Participants { get; set; } = new ();
            }
        }
#if __CLIENT__
        internal async Task<List<Participant>> GetSessionParticipants(int sessionId, CancellationTokenSource cts)
        {
            return (await SendMessage<GetSessionParticipantsMC.Command, GetSessionParticipantsMC.Response>(new GetSessionParticipantsMC.Command { SessionId = sessionId }, nameof(GetSessionParticipants), cts))?.Participants??new();
        }
#else
        [HttpPost]
        public async Task<ActionResult<GetSessionParticipantsMC.Response>> GetSessionParticipants([FromBody] GetSessionParticipantsMC.Command command)
        {
            var response = await ReceiveMessage(command, (command) => {
                var participants = minutesModel.GetSessionParticipants(command.SessionId).FromDb();
                return new GetSessionParticipantsMC.Response { Participants = participants };
            });

            return AcceptedAtAction(nameof(GetSessionParticipants), response);
        }
#endif

        public class SetParticipantsMC
        {
            public class Command
            {
                public int SessionId { get; set; }
                public List<int> ParticipantIds { get; set; } =  new();
            }

            public class Response : ResponseBase
            {

            }
        }

#if __CLIENT__
        internal async Task SetParticipants(int sessionId, List<int> participantIds, CancellationTokenSource cts)
        {
            await SendMessage<SetParticipantsMC.Command, SetParticipantsMC.Response>(new SetParticipantsMC.Command { SessionId = sessionId, ParticipantIds = participantIds }, nameof(SetParticipants), cts);
        }
#else
        [HttpPost]
        public async Task<ActionResult<SetParticipantsMC.Response>> SetParticipants([FromBody] SetParticipantsMC.Command command)
        {
            var response = await ReceiveMessage(command, (command) => {
                minutesModel.SetParticipants(command.SessionId, command.ParticipantIds);
                return new SetParticipantsMC.Response();
            });

            return AcceptedAtAction(nameof(SetParticipants), response);
        }
#endif

        public class CreateMeetingMC
        {
            public class Command
            {
                public int AuthorId { get; set; }
                public string MeetingName { get; set; } = "";
            }

            public class Response : ResponseBase
            {
                public Meeting? Meeting { get; set; }
            }
        }

#if __CLIENT__
        internal async Task<Meeting?> CreateMeeting(int authorId, string meetingName, CancellationTokenSource cts)
        {
            var response =  await SendMessage<CreateMeetingMC.Command, CreateMeetingMC.Response>(new CreateMeetingMC.Command { AuthorId = authorId, MeetingName = meetingName }, nameof(CreateMeeting), cts);
            var meeting = response?.Meeting;
            return meeting;
        }
#else
        [HttpPost]
        public async Task<ActionResult<CreateMeetingMC.Response>> CreateMeeting([FromBody] CreateMeetingMC.Command command)
        {
            var response = await ReceiveMessage(command, (command) => {
                var meeting = minutesModel.CreateMeeting(command.AuthorId, command.MeetingName)?.FromDb();
                return new CreateMeetingMC.Response { Meeting = meeting };
            });

            return AcceptedAtAction(nameof(CreateMeeting), response);
        }
#endif

        public class DeleteMeetingMC
        {
            public class Command
            {
                public int MeetingId { get; set; }
            }

            public class Response : ResponseBase
            {

            }
        }
#if __CLIENT__
        internal async Task DeleteMeeting(int meetingId, CancellationTokenSource cts)
        {
            await SendMessage<DeleteMeetingMC.Command, DeleteMeetingMC.Response>(new DeleteMeetingMC.Command { MeetingId = meetingId, }, nameof(DeleteMeeting), cts);
        }
#else
        [HttpPost]
        public async Task<ActionResult<DeleteMeetingMC.Response>> DeleteMeeting([FromBody] DeleteMeetingMC.Command command)
        {
            var response = await ReceiveMessage(command, (command) => {
                minutesModel.DeleteMeeting(command.MeetingId);
                return new DeleteMeetingMC.Response();
            });

            return AcceptedAtAction(nameof(DeleteMeeting), response);
        }
#endif
        public class CreateTopicMC
        {
            public class Command
            {
                public int MeetingId { get; set; } = 0;
                public int ParentTopicId { get; set; } = 0;
                public int BeforeTopicId { get; set; } = 0;
                public string Title { get; set; } = "";
            }

            public class Response : ResponseBase
            {
                public Topic? Topic { get; set; }
            }
        }

#if __CLIENT__
        internal async Task<Topic?> CreateTopic(int meetingId, int parentTopicId, int beforeTopicId, string title, CancellationTokenSource cts)
        {
            return (await SendMessage<CreateTopicMC.Command, CreateTopicMC.Response>(new CreateTopicMC.Command { MeetingId = meetingId, ParentTopicId = parentTopicId, BeforeTopicId = beforeTopicId, Title = title }, nameof(CreateTopic), cts))?.Topic;
        }
#else
        [HttpPost]
        public async Task<ActionResult<CreateTopicMC.Response>> CreateTopic([FromBody] CreateTopicMC.Command command)
        {
            var response = await ReceiveMessage(command, (command) => {
                var topic = minutesModel.CreateTopic(command.MeetingId, command.ParentTopicId, command.BeforeTopicId, command.Title).FromDb();
                return new CreateTopicMC.Response { Topic = topic };
            });

            return AcceptedAtAction(nameof(CreateTopic), response);
        }
#endif
        public class DeleteTopicMC
        {
            public class Command
            {
                public int TopicId { get; set; }
            }

            public class Response : ResponseBase
            {
            }
        }

#if __CLIENT__
        internal async Task DeleteTopic(int topicId, CancellationTokenSource cts)
        {
            await SendMessage<DeleteTopicMC.Command, DeleteTopicMC.Response>(new DeleteTopicMC.Command { TopicId = topicId }, nameof(DeleteTopic), cts);
        }
#else
        [HttpPost]
        public async Task<ActionResult<DeleteTopicMC.Response>> DeleteTopic([FromBody] DeleteTopicMC.Command command)
        {
            var response = await ReceiveMessage(command, (command) => {
                minutesModel.DeleteTopic(command.TopicId);
                return new DeleteTopicMC.Response { };
            });

            return AcceptedAtAction(nameof(DeleteTopic), response);
        }
#endif

        public class AddParticipantMC
        {
            public class Command
            {
                public string Email { get; set; } = "";
                public string Name { get; set; } = "";
                public string Password { get; set; } = "";
            }

            public class Response : ResponseBase
            {
                public Participant? Participant { get; set; }
            }
        }

#if __CLIENT__
        internal async Task<Participant?> AddParticipant(string email, string name, string password, CancellationTokenSource cts)
        {
            return (await SendMessage<AddParticipantMC.Command, AddParticipantMC.Response>(new AddParticipantMC.Command { Email = email, Name = name, Password = password }, nameof(AddParticipant), cts))?.Participant;
        }
#else
        [HttpPost]
        public async Task<ActionResult<AddParticipantMC.Response>> AddParticipant([FromBody] AddParticipantMC.Command command)
        {
            var response = await ReceiveMessage(command, (command) => {
                var participant = minutesModel.AddParticipant(command.Email, command.Name, command.Password).FromDb();
                return new AddParticipantMC.Response { Participant = participant };
            });

            return AcceptedAtAction(nameof(AddParticipant), response);
        }
#endif

        public class UpdateParticipantMC
        {
            public class Command
            {
                public int ParticipantId { get; set; }
                public string Email { get; set; } = "";
                public string Name { get; set; } = "";
                public string Password { get; set; } = "";
            }

            public class Response : ResponseBase
            {

            }
        }

#if __CLIENT__
        internal async Task UpdateParticipant(int participantId, string email, string name, string password, CancellationTokenSource cts)
        {
            await SendMessage<UpdateParticipantMC.Command, UpdateParticipantMC.Response>(new UpdateParticipantMC.Command { ParticipantId = participantId, Email = email, Name = name, Password = password }, nameof(UpdateParticipant), cts);
        }
#else
        [HttpPost]
        public async Task<ActionResult<UpdateParticipantMC.Response>> UpdateParticipant([FromBody] UpdateParticipantMC.Command command)
        {
            var response = await ReceiveMessage(command, (command) => {
                minutesModel.UpdateParticipant(command.ParticipantId, command.Email, command.Name, command.Password);
                return new UpdateParticipantMC.Response { };
            });

            return AcceptedAtAction(nameof(UpdateParticipant), response);
        }
#endif

        public class DeleteParticipantMC
        {
            public class Command
            {
                public int ParticipantId { get; set; }
            }

            public class Response : ResponseBase
            {

            }
        }

#if __CLIENT__
        internal async Task DeleteParticipant(int participantId, CancellationTokenSource cts)
        {
            await SendMessage<DeleteParticipantMC.Command, DeleteParticipantMC.Response>(new DeleteParticipantMC.Command { ParticipantId = participantId }, nameof(DeleteParticipant), cts);
        }
#else
        [HttpPost]
        public async Task<ActionResult<DeleteParticipantMC.Response>> DeleteParticipant([FromBody] DeleteParticipantMC.Command command)
        {
            var response = await ReceiveMessage(command, (command) => {
                minutesModel.DeleteParticipant(command.ParticipantId);
                return new DeleteParticipantMC.Response { };
            });

            return AcceptedAtAction(nameof(DeleteParticipant), response);
        }
#endif

        public class UpdateMeetingMC
        {
            public class Command
            {
                public int MeetingId { get; set; }
                public int AuthorId { get; set; }
                public List<int> DelegateIds { get; set; } = new();
                public string MeetingName { get; set; } = "";
            }

            public class Response : ResponseBase
            {

            }
        }

#if __CLIENT__
        internal async Task UpdateMeeting(int meetingId, int authorId, List<int> delegateIds, string meetingName, CancellationTokenSource cts)
        {
            await SendMessage<UpdateMeetingMC.Command, UpdateMeetingMC.Response>(new UpdateMeetingMC.Command { MeetingId = meetingId, AuthorId = authorId, DelegateIds = delegateIds, MeetingName = meetingName }, nameof(UpdateMeeting), cts);
        }
#else
        [HttpPost]
        public async Task<ActionResult<UpdateMeetingMC.Response>> UpdateMeeting([FromBody] UpdateMeetingMC.Command command)
        {
            var response = await ReceiveMessage(command, (command) => {
                minutesModel.UpdateMeeting(command.MeetingId, command.AuthorId, command.DelegateIds, command.MeetingName);
                return new UpdateMeetingMC.Response { };
            });

            return AcceptedAtAction(nameof(UpdateMeeting), response);
        }
#endif

        public class SignInMC
        {
            public class Command
            {
                public string Email { get; set; } = "";
                public string Password { get; set; } = "";
            }

            public class Response : ResponseBase
            {
                public Participant? Participant { get; set; }
                public string Instruction { get; set; } = "";
            }
        }

#if __CLIENT__
        internal async Task<(Participant? participant, string errorMessage)> SignIn(string email, string password, CancellationTokenSource cts)
        {
            var result = await SendMessage<SignInMC.Command, SignInMC.Response>(new SignInMC.Command { Email = email, Password = password }, nameof(SignIn), cts);
            return (result?.Participant, result?.Instruction??"Low level error");
        }
#else
        [HttpPost]
        public async Task<ActionResult<SignInMC.Response>> SignIn([FromBody] SignInMC.Command command)
        {
            var response = await ReceiveMessage(command, (command) => {
                var (participant, errorMessage) = minutesModel.SignIn(command.Email, command.Password);
                return new SignInMC.Response { Participant = participant?.FromDb(), Instruction = errorMessage };
            });

            return AcceptedAtAction(nameof(SignIn), response);
        }
#endif
        public class SignInUsingPinMC
        {
            public class Command
            {
                public string Pin { get; set; } = "";
            }

            public class Response : ResponseBase
            {
                public Participant? Participant { get; set; }
                public string Instruction { get; set; } = "";
            }
        }

#if __CLIENT__
        internal async Task<(Participant? participant, string errorMessage)> SignInUsingPin(string pin, CancellationTokenSource cts)
        {
            var result = await SendMessage<SignInUsingPinMC.Command, SignInUsingPinMC.Response>(new SignInUsingPinMC.Command { Pin = pin}, nameof(SignInUsingPin), cts);
            return (result?.Participant, result?.Instruction ?? "Low level error");
        }
#else
        [HttpPost]
        public async Task<ActionResult<SignInUsingPinMC.Response>> SignInUsingPin([FromBody] SignInUsingPinMC.Command command)
        {
            var response = await ReceiveMessage(command, (command) => {
                var (participant, errorMessage) = minutesModel.SignIn(command.Pin);
                return new SignInUsingPinMC.Response { Participant = participant?.FromDb(), Instruction = errorMessage };
            });

            return AcceptedAtAction(nameof(SignInUsingPin), response);
        }
#endif

        public class UpdatePasswordMC
        {
            public class Command
            {
                public string Email { get; set; } = "";
                public string Password { get; set; } = "";
            }

            public class Response : ResponseBase
            {

            }
        }

#if __CLIENT__
        internal async Task UpdatePassword(string email, string password, CancellationTokenSource cts)
        {
            await SendMessage<UpdatePasswordMC.Command, UpdatePasswordMC.Response>(new UpdatePasswordMC.Command { Email = email, Password = password }, nameof(UpdatePassword), cts);
        }
#else
        [HttpPost]
        public async Task<ActionResult<UpdatePasswordMC.Response>> UpdatePassword([FromBody] UpdatePasswordMC.Command command)
        {
            var response = await ReceiveMessage(command, (command) => {
                minutesModel.UpdatePasswordMC(command.Email, command.Password);
                return new UpdatePasswordMC.Response { };
            });

            return AcceptedAtAction(nameof(UpdatePassword), response);
        }
#endif

        public class SendRecoveryPinMC
        {
            public class Command
            {
                public string Email { get; set; } = "";
            }

            public class Response : ResponseBase
            {
                public string Pin { get; set; } = "";
            }
        }

#if __CLIENT__
        internal async Task<string> SendRecoveryPin(string email, CancellationTokenSource cts)
        {
            return (await SendMessage<SendRecoveryPinMC.Command, SendRecoveryPinMC.Response>(new SendRecoveryPinMC.Command { Email = email }, nameof(SendRecoveryPin), cts))?.Pin??"Undefined";
        }
#else
        [HttpPost]
        public async Task<ActionResult<SendRecoveryPinMC.Response>> SendRecoveryPin([FromBody] SendRecoveryPinMC.Command command)
        {
            var response = await ReceiveMessage<SendRecoveryPinMC.Command, SendRecoveryPinMC.Response>(command, async (command) => {
                var pin = await minutesModel.SendRecoveryPin(command.Email);
                return new SendRecoveryPinMC.Response { Pin = pin };
            });

            return AcceptedAtAction(nameof(SendRecoveryPin), response);
        }
#endif

        public class DistributeMeetingMC
        {
            public class Command
            {
                public int MeetingId { get; set; } = 0;
                public DistributeFilterOptions DistributeFilterOption { get; set; } = DistributeFilterOptions.All;
                public List<int> ParticipantIds { get; set; } = new();
            }

            public class Response : ResponseBase
            {

            }
        }

#if __CLIENT__
        internal async Task DistributeMeeting(int meetingId, DistributeFilterOptions distributeFilterOption, List<int> participantIds, CancellationTokenSource cts)
        {
            await SendMessage<DistributeMeetingMC.Command, DistributeMeetingMC.Response>(new DistributeMeetingMC.Command { MeetingId = meetingId,DistributeFilterOption = distributeFilterOption, ParticipantIds = participantIds  }, nameof(DistributeMeeting), cts);
        }
#else
        [HttpPost]
        public async Task<ActionResult<DistributeMeetingMC.Response>> DistributeMeeting([FromBody] DistributeMeetingMC.Command command)
        {
            var response = await ReceiveMessage(command, (command) => {
                minutesModel.DistributeMeeting(command.MeetingId, command.DistributeFilterOption, command.ParticipantIds);
                return new DistributeMeetingMC.Response { };
            });

            return AcceptedAtAction(nameof(DistributeMeeting), response);
        }
#endif

        public class PreviewDistributedMeetingMC
        {
            public class Command
            {
                public int MeetingId { get; set; } = 0;
                public DistributeFilterOptions DistributeFilterOption { get; set; } = DistributeFilterOptions.All;
                public List<int> ParticipantIds { get; set; } = new();
            }

            public class Response : ResponseBase
            {
                public string MailHtml { get; set; } = "";
            }
        }

#if __CLIENT__
        internal async Task<string> PreviewDistributedMeeting(int meetingId, DistributeFilterOptions distributeFilterOption, List<int> participantIds, CancellationTokenSource cts)
        {
            return (await SendMessage<PreviewDistributedMeetingMC.Command, PreviewDistributedMeetingMC.Response>(new PreviewDistributedMeetingMC.Command { MeetingId = meetingId, DistributeFilterOption = distributeFilterOption, ParticipantIds = participantIds }, nameof(PreviewDistributedMeeting), cts))?.MailHtml??"";
        }
#else
        [HttpPost]
        public async Task<ActionResult<PreviewDistributedMeetingMC.Response>> PreviewDistributedMeeting([FromBody] PreviewDistributedMeetingMC.Command command)
        {
            var response = await ReceiveMessage(command, (command) => {
                var mailHtml = minutesModel.PreviewDistributedMeeting(command.MeetingId, command.DistributeFilterOption, command.ParticipantIds);
                return new PreviewDistributedMeetingMC.Response { MailHtml = mailHtml };
            });

            return AcceptedAtAction(nameof(PreviewDistributedMeeting), response);
        }
#endif


        public class UpdateDelegatesMC
        {
            public class Command
            {
                public int MeetingId { get; set; } = 0;
                public List<int> DelegateIds { get; set; } = new();
            }

            public class Response : ResponseBase
            {

            }
        }

#if __CLIENT__
        internal async Task UpdateDelegates(int meetingId, List<int> delegateIds, CancellationTokenSource cts)
        {
            await SendMessage<UpdateDelegatesMC.Command, UpdateDelegatesMC.Response>(new UpdateDelegatesMC.Command { MeetingId = meetingId, DelegateIds = delegateIds }, nameof(UpdateDelegates), cts);
        }
#else
        [HttpPost]
        public async Task<ActionResult<UpdateDelegatesMC.Response>> UpdateDelegates([FromBody] UpdateDelegatesMC.Command command)
        {
            var response = await ReceiveMessage(command, (command) => {
                minutesModel.UpdateDelegates(command.MeetingId, command.DelegateIds);
                return new UpdateDelegatesMC.Response { };
            });

            return AcceptedAtAction(nameof(UpdateDelegates), response);
        }
#endif

        public class ChangeMeetingsDisplayOrderMC
        {
            public class Command
            {
                public int MeetingId { get; set; } = 0;
                public bool IsMoveUp { get; set; } = true;
            }

            public class Response : ResponseBase
            {
            }
        }

#if __CLIENT__
        internal async Task ChangeMeetingsDisplayOrder(int meetingId, bool isMoveUp, CancellationTokenSource cts)
        {
            await SendMessage<ChangeMeetingsDisplayOrderMC.Command, ChangeMeetingsDisplayOrderMC.Response>(new ChangeMeetingsDisplayOrderMC.Command { MeetingId = meetingId, IsMoveUp = isMoveUp }, nameof(ChangeMeetingsDisplayOrder), cts);
        }



#else
        [HttpPost]
        public async Task<ActionResult<ChangeMeetingsDisplayOrderMC.Response>> ChangeMeetingsDisplayOrder([FromBody] ChangeMeetingsDisplayOrderMC.Command command)
        {
            var response = await ReceiveMessage(command, (command) => {
                minutesModel.ChangeMeetingsDisplayOrder(command.MeetingId, command.IsMoveUp);
                return new ChangeMeetingsDisplayOrderMC.Response { };
            });

            return AcceptedAtAction(nameof(ChangeMeetingsDisplayOrder), response);
        }
#endif
        public class SetMeetingCheckedMC
        {
            public class Command
            {
                public int MeetingId { get; set; } = 0;
                public bool IsChecked { get; set; } = false;
            }

            public class Response : ResponseBase
            {
            }
        }

#if __CLIENT__
        internal async Task SetMeetingChecked(int meetingId, bool isChecked, CancellationTokenSource cts)
        {
            await SendMessage<SetMeetingCheckedMC.Command, SetMeetingCheckedMC.Response>(new SetMeetingCheckedMC.Command { MeetingId = meetingId, IsChecked = isChecked }, nameof(SetMeetingChecked), cts);
        }
#else
        [HttpPost]
        public async Task<ActionResult<SetMeetingCheckedMC.Response>> SetMeetingChecked([FromBody] SetMeetingCheckedMC.Command command)
        {
            var response = await ReceiveMessage(command, (command) => {
                minutesModel.SetMeetingChecked(command.MeetingId, command.IsChecked);
                return new SetMeetingCheckedMC.Response { };
            });

            return AcceptedAtAction(nameof(SetMeetingChecked), response);
        }
#endif

        public class ChangeTopicsDisplayOrderMC
        {
            public class Command
            {
                public int MeetingId { get; set; } = 0;
                public int TopicId { get; set; } = 0;
                public bool IsMoveUp { get; set; } = true;
            }

            public class Response : ResponseBase
            {
                public List<Topic> Topics { get; set; } = new();
            }
        }

#if __CLIENT__
        internal async Task<List<Topic>> ChangeTopicsDisplayOrder(int meetingId, int topicId, bool isMoveUp, CancellationTokenSource cts)
        {
            return (await SendMessage<ChangeTopicsDisplayOrderMC.Command, ChangeTopicsDisplayOrderMC.Response>(new ChangeTopicsDisplayOrderMC.Command { MeetingId = meetingId, TopicId = topicId, IsMoveUp = isMoveUp }, nameof(ChangeTopicsDisplayOrder), cts))?.Topics??new();
        }

#else
        [HttpPost]
        public async Task<ActionResult<ChangeTopicsDisplayOrderMC.Response>> ChangeTopicsDisplayOrder([FromBody] ChangeTopicsDisplayOrderMC.Command command)
        {
            var response = await ReceiveMessage(command, (command) => {
                var topics = minutesModel.ChangeTopicsDisplayOrder(command.MeetingId, command.TopicId, command.IsMoveUp);
                return new ChangeTopicsDisplayOrderMC.Response { Topics = topics.FromDb() };
            });

            return AcceptedAtAction(nameof(ChangeTopicsDisplayOrder), response);
        }
#endif
        public class SetTopicCheckedMC
        {
            public class Command
            {
                public int TopicId { get; set; } = 0;
                public bool IsChecked { get; set; } = false;
            }

            public class Response : ResponseBase
            {
            }
        }

#if __CLIENT__
        internal async Task SetTopicChecked(int TopicId, bool isChecked, CancellationTokenSource cts)
        {
            await SendMessage<SetTopicCheckedMC.Command, SetTopicCheckedMC.Response>(new SetTopicCheckedMC.Command { TopicId = TopicId, IsChecked = isChecked }, nameof(SetTopicChecked), cts);
        }


#else
        [HttpPost]
        public async Task<ActionResult<SetTopicCheckedMC.Response>> SetTopicChecked([FromBody] SetTopicCheckedMC.Command command)
        {
            var response = await ReceiveMessage(command, (command) => {
                minutesModel.SetTopicChecked(command.TopicId, command.IsChecked);
                return new SetTopicCheckedMC.Response { };
            });

            return AcceptedAtAction(nameof(SetTopicChecked), response);
        }
#endif
        public class GetParticipantIdMC
        {
            public class Command
            {
                public string ParticipantEmail { get; set; } = "";
            }

            public class Response : ResponseBase
            {
                public int Id { get; set; }
            }
        }

#if __CLIENT__
        internal async Task<int> GetParticipantId(string participantEmail, CancellationTokenSource cts)
        {
            return (await SendMessage<GetParticipantIdMC.Command, GetParticipantIdMC.Response>(new GetParticipantIdMC.Command { ParticipantEmail = participantEmail }, nameof(GetParticipantId), cts))?.Id??0;
        }




#else
        [HttpPost]
        public async Task<ActionResult<GetParticipantIdMC.Response>> GetParticipantId([FromBody] GetParticipantIdMC.Command command)
        {
            var response = await ReceiveMessage(command, (command) => {
                var id = minutesModel.GetParticipantId(command.ParticipantEmail);
                return new GetParticipantIdMC.Response { Id = id };
            });

            return AcceptedAtAction(nameof(GetParticipantId), response);
        }
#endif

        public class GetParticipantIdFromNameMC
        {
            public class Command
            {
                public string ParticipantName { get; set; } = "";
            }

            public class Response : ResponseBase
            {
                public int Id { get; set; }
            }
        }

#if __CLIENT__
        internal async Task<int> GetParticipantIdFromName(string participantName, CancellationTokenSource cts)
        {
            return (await SendMessage<GetParticipantIdFromNameMC.Command, GetParticipantIdFromNameMC.Response>(new GetParticipantIdFromNameMC.Command { ParticipantName = participantName }, nameof(GetParticipantIdFromName), cts))?.Id ?? 0;
        }




#else
        [HttpPost]
        public async Task<ActionResult<GetParticipantIdFromNameMC.Response>> GetParticipantIdFromName([FromBody] GetParticipantIdFromNameMC.Command command)
        {
            var response = await ReceiveMessage(command, (command) => {
                var id = minutesModel.GetParticipantIdFromName(command.ParticipantName);
                return new GetParticipantIdFromNameMC.Response { Id = id };
            });

            return AcceptedAtAction(nameof(GetParticipantIdFromName), response);
        }
#endif

        public class AddParticipantWithIdMC
        {
            public class Command
            {
                public int OwnerId { get; set; } = 0;
                public int ParticipantId { get; set; } = 0;
            }

            public class Response : ResponseBase
            {
            }
        }

#if __CLIENT__
        internal async Task AddParticipantWithId(int ownerId, int participantId, CancellationTokenSource cts)
        {
            await SendMessage<AddParticipantWithIdMC.Command, AddParticipantWithIdMC.Response>(new AddParticipantWithIdMC.Command { OwnerId = ownerId, ParticipantId = participantId }, nameof(AddParticipantWithId), cts);
        }
#else
        [HttpPost]
        public async Task<ActionResult<AddParticipantWithIdMC.Response>> AddParticipantWithId([FromBody] AddParticipantWithIdMC.Command command)
        {
            var response = await ReceiveMessage(command, (command) => {
                minutesModel.AddParticipant(command.OwnerId, command.ParticipantId);
                return new AddParticipantWithIdMC.Response { };
            });

            return AcceptedAtAction(nameof(AddParticipantWithId), response);
        }
#endif

        public class ServerTestMC
        {
            public class Command
            {

            }

            public class Response : ResponseBase
            {
            }
        }

#if __CLIENT__
        internal async Task ServerTest(CancellationTokenSource cts)
        {
            await SendMessage<ServerTestMC.Command, ServerTestMC.Response>(new ServerTestMC.Command(), nameof(ServerTest), cts);
        }


#else
        [HttpPost]
        public async Task<ActionResult<ServerTestMC.Response>> ServerTest([FromBody] ServerTestMC.Command command)
        {
            var response = await ReceiveMessage(command, (command) => {
                MinutesModel.ServerTest();
                return new ServerTestMC.Response { };
            });

            return AcceptedAtAction(nameof(ServerTest), response);
        }
#endif
        public class ChangeTopicHierarchyMC
        {
            public class Command
            {
                public int MeetingId { get; set; } = 0;
                public int AboveTopicId { get; set; } = 0;
                public int ChangeTopicId { get; set; } = 0;
                public bool Demote { get; set; } = false;
            }

            public class Response : ResponseBase
            {
                public List<Topic> Topics { get; set; } = new();
            }
        }

#if __CLIENT__
        internal async Task<List<Topic>> ChangeTopicHierarchy(int meetingId, int aboveTopicId, int changeTopicId, bool demote, CancellationTokenSource cts)
        {
            return (await SendMessage<ChangeTopicHierarchyMC.Command, ChangeTopicHierarchyMC.Response>(new ChangeTopicHierarchyMC.Command { MeetingId = meetingId, AboveTopicId = aboveTopicId, ChangeTopicId = changeTopicId, Demote = demote }, nameof(ChangeTopicHierarchy), cts))?.Topics??new();
        }




#else
        [HttpPost]
        public async Task<ActionResult<ChangeTopicHierarchyMC.Response>> ChangeTopicHierarchy([FromBody] ChangeTopicHierarchyMC.Command command)
        {
            var response = await ReceiveMessage(command, (command) => {
                var topics = minutesModel.ChangeTopicHierarchy(command.MeetingId, command.AboveTopicId, command.ChangeTopicId, command.Demote).FromDb();
                return new ChangeTopicHierarchyMC.Response { Topics = topics };
            });

            return AcceptedAtAction(nameof(ChangeTopicHierarchy), response);
        }
#endif

        public class SetTopicsCheckedMC
        {
            public class Command
            {
                public int MeetingId { get; set; } = 0;
                public List<int> TopicIds { get; set; } = new();
            }

            public class Response : ResponseBase
            {

            }
        }

#if __CLIENT__
        internal async Task SetTopicsChecked(int meetingId, List<int> topicIds, CancellationTokenSource cts)
        {
            await SendMessage<SetTopicsCheckedMC.Command, SetTopicsCheckedMC.Response>(new SetTopicsCheckedMC.Command { MeetingId = meetingId, TopicIds = topicIds  }, nameof(SetTopicsChecked), cts);
        }






#else
        [HttpPost]
        public async Task<ActionResult<SetTopicsCheckedMC.Response>> SetTopicsChecked([FromBody] SetTopicsCheckedMC.Command command)
        {
            var response = await ReceiveMessage(command, (command) => {
                minutesModel.SetTopicsChecked(command.MeetingId, command.TopicIds);
                return new ChangeTopicHierarchyMC.Response { };
            });

            return AcceptedAtAction(nameof(SetTopicsChecked), response);
        }
#endif






#if __CLIENT__

#else

#endif

#if __CLIENT__

#else

#endif

#if __CLIENT__

#else

#endif

    }
}
