using System.Net;
using System.Net.Mail;

namespace EasyMinutesServer.Models
{
    public class BackgroundMailer : BackgroundService
    {
        private readonly IMailWorker worker;

        public BackgroundMailer(IMailWorker worker)
        {
            this.worker = worker;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await worker.DoWork(stoppingToken);
        }
    }

    public interface IMailWorker
    {
        Task DoWork(CancellationToken cancellationToken);
        void ScheduleMail(string meetingName, string destinationEmailAddress, string emailBody);
    }

    public class MailWorker : IMailWorker
    {

        private class MailData
        {
            public string MeetingName { get; set; } = "";
            public string DestinationEmailAddress { get; set; } = "";
            public string EmailBody { get; set; } = "";
        }

        private readonly List<MailData> MailDatas = new();

        private readonly ILogger<MailWorker> logger;

        public MailWorker(ILogger<MailWorker> logger)
        {
            this.logger = logger;
        }

        public async Task DoWork(CancellationToken cancellationToken)
        {
            SmtpClient client;
            MailData mailData;

            if (Constants.IsMailServerReal)
            {
                // Send actual mail using Gmail SMTP - https://stackoverflow.com/questions/67950293/how-to-fix-gmail-smtp-error-the-smtp-server-requires-a-secure-connection-or-th
                client = new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential("treeapps.develop@gmail.com", "uxgisiktlesjcfkd"), // Need to generate an App Password for [Mail] on {Windows Machine] for gmail account treeapps.develop@gmail.com
                    EnableSsl = true,
                };
            }
            else
            {
                // Use MailTrap for testing
                client = new SmtpClient("smtp.mailtrap.io", 2525)
                {
                    Credentials = new NetworkCredential("9dc87946ebe6cb", "38ff9c59f93713"),
                    EnableSsl = true
                };
            }


            while (!cancellationToken.IsCancellationRequested)
            {
                if (MailDatas.Count != 0)
                {
                    lock (MailDatas)
                    {
                        mailData = MailDatas[0];
                    }

                    MailMessage message = new()
                    {
                        From = new MailAddress("treeapps.develop@gmail.com")
                    };
                    message.To.Add(mailData.DestinationEmailAddress);
                    message.Subject = $"EasyMinutes - Minutes of meeting: {mailData.MeetingName}";
                    message.IsBodyHtml = true;
                    message.Body = mailData.EmailBody;

                    try
                    {
                        await client.SendMailAsync(message, cancellationToken);

                        logger.LogInformation($"Email successfully sent to {mailData.DestinationEmailAddress}");
                    }
                    catch (Exception ex)
                    {
                        logger.LogInformation($"Email worker exception: {ex.Message}");
                    }
                    finally
                    {
                        lock (MailDatas)
                        {
                            MailDatas.Remove(mailData);
                        }
                    }
                }

                await Task.Delay(1000, cancellationToken);
            }
        }

        public void ScheduleMail(string meetingName, string destinationEmailAddress, string emailBody)
        {
            lock (MailDatas)
            {
                MailDatas.Add(new MailData { MeetingName = meetingName, DestinationEmailAddress = destinationEmailAddress, EmailBody = emailBody });
            }
        }
    }
}


