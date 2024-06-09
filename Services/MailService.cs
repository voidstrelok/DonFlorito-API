using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit.Text;
using MimeKit;
using DonFlorito.Util;
using MailKit.Net.Smtp;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using MimeKit.Utils;

namespace DonFlorito.Services
{ 

    public class MailService
    {
        private static IConfiguration Config;
        private static IWebHostEnvironment webHostEnvironment;

        public MailService(IConfiguration config, IWebHostEnvironment _webh)
        {
            Config = config;
            webHostEnvironment = _webh;
        }

        public async void Send(List<string> destinatarios, string asunto, string contenido, byte[] QRCode)
        {
            var email = new MimeMessage();

            var From = Config.GetSection("Email").GetValue<string>("From");
            var Host = Config.GetSection("Email").GetValue<string>("Host");
            var Puerto = Config.GetSection("Email").GetValue<int>("Port");
            var User = Config.GetSection("Email").GetValue<string>("User");
            var Pass = Config.GetSection("Email").GetValue<string>("Password");
            var URL = Config.GetValue<string>("FrontendURL");

            email.From.Add(MailboxAddress.Parse(From));
            foreach(var dest in destinatarios)
            {
                email.To.Add(MailboxAddress.Parse(dest));
            }
            email.Subject = asunto;
            BodyBuilder builder = new BodyBuilder();

            var Logo = builder.LinkedResources.Add(webHostEnvironment.ContentRootPath + "/Resources/Email/logo-head.png");
            var Florito = builder.LinkedResources.Add(webHostEnvironment.ContentRootPath + "/Resources/Email/florito.png");
            var Instagram = builder.LinkedResources.Add(webHostEnvironment.ContentRootPath + "/Resources/Email/instagram.png");
            var Whatsapp = builder.LinkedResources.Add(webHostEnvironment.ContentRootPath + "/Resources/Email/whatsapp.png");

            Logo.ContentId = MimeUtils.GenerateMessageId();
            Florito.ContentId = MimeUtils.GenerateMessageId();
            Instagram.ContentId = MimeUtils.GenerateMessageId();
            Whatsapp.ContentId = MimeUtils.GenerateMessageId();

            contenido = contenido.Replace("{logo-head}", $"cid:{Logo.ContentId}");
            contenido = contenido.Replace("{florito}", $"cid:{Florito.ContentId}");
            contenido = contenido.Replace("{logo-instagram}", $"cid:{Instagram.ContentId}");
            contenido = contenido.Replace("{logo-wsp}", $"cid:{Whatsapp.ContentId}");

            if(QRCode != null)
            {
                var QR = builder.LinkedResources.Add("QR", new MemoryStream(QRCode));
                QR.ContentId = MimeUtils.GenerateMessageId();
                contenido = contenido.Replace("{qr}", $"cid:{QR.ContentId}");
            }

            contenido = contenido.Replace("{url}",URL);

            builder.HtmlBody = contenido;

            email.Body = builder.ToMessageBody();

            // send email
            using var smtp = new MailKit.Net.Smtp.SmtpClient();
            smtp.Connect(Host, Puerto, SecureSocketOptions.StartTls);
            smtp.Authenticate(User, Pass);
            smtp.Send(email);
            smtp.Disconnect(true);
        }
    }
}
