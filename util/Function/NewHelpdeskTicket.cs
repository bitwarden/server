using System;
using System.Configuration;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;

namespace Bit.Function
{
    public static class NewHelpdeskTicket
    {
        [FunctionName("NewHelpdeskTicket")]
        public static HttpResponseMessage Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/newhelpdeskticket")]HttpRequestMessage req,
            TraceWriter log)
        {
            //ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls | 
            //    SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;

            var data = req.Content.ReadAsFormDataAsync().Result;
            if(data == null)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest, "No data provided.");
            }

            if(string.IsNullOrWhiteSpace(data["name"]))
            {
                return req.CreateResponse(HttpStatusCode.BadRequest, "Name is required.");
            }

            if(data["name"].Length > 50)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest, "Name must be less than 50 characters.");
            }

            if(string.IsNullOrWhiteSpace(data["email"]))
            {
                return req.CreateResponse(HttpStatusCode.BadRequest, "Email is required.");
            }

            if(data["email"].Length > 50)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest, "Email must be less than 50 characters.");
            }

            if(!data["email"].Contains("@") || !data["email"].Contains("."))
            {
                return req.CreateResponse(HttpStatusCode.BadRequest, "Email is not valid.");
            }

            if(string.IsNullOrWhiteSpace(data["message"]))
            {
                return req.CreateResponse(HttpStatusCode.BadRequest, "Message is required.");
            }

            //if(!await SubmitApiAsync(data["name"], data["email"], data["message"], log))
            //{
            //    return req.CreateResponse(HttpStatusCode.BadRequest, "Ticket failed to create.");
            //}

            SubmitEmail(data["name"], data["email"], data["message"], log);

            return req.CreateResponse(HttpStatusCode.OK, "Ticket created.");
        }

        private async static Task<bool> SubmitApiAsync(string name, string email, string message, TraceWriter log)
        {
            using(var client = new HttpClient())
            {
                client.BaseAddress = new Uri("https://bitwarden.freshdesk.com/api/v2");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Add("Authorization", MakeFreshdeskApiAuthHeader(log));

                var response = await client.PostAsJsonAsync("tickets",
                    new
                    {
                        name = name,
                        email = email,
                        status = 2,
                        priority = 2,
                        source = 1,
                        subject = "bitwarden.com Website Contact",
                        description = FormatMessage(message)
                    });

                return response.IsSuccessStatusCode;
            }
        }

        private static void SubmitEmail(string name, string email, string message, TraceWriter log)
        {
            var sendgridApiKey = ConfigurationManager.AppSettings["SendgridApiKey"];
            var client = new SmtpClient("smtp.sendgrid.net", /*465*/ 587)
            {
                //EnableSsl = true,
                Credentials = new NetworkCredential("apikey", sendgridApiKey)
            };

            var fromAddress = new MailAddress(email, name, Encoding.UTF8);
            var mailMessage = new MailMessage(fromAddress, new MailAddress("bitwardencomsupport@bitwarden.freshdesk.com"))
            {
                Subject = "bitwarden.com Website Contact",
                Body = FormatMessage(message),
                IsBodyHtml = true
            };

            client.SendCompleted += (s, e) =>
            {
                client.Dispose();
                mailMessage.Dispose();
            };
            client.SendAsync(mailMessage, null);
        }

        private static string FormatMessage(string message)
        {
            return message.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "<br>");
        }

        private static string MakeFreshdeskApiAuthHeader(TraceWriter log)
        {
            var freshdeskApiKey = ConfigurationManager.AppSettings["FreshdeskApiKey"];
            var b64Creds = Convert.ToBase64String(
                Encoding.GetEncoding("ISO-8859-1").GetBytes(freshdeskApiKey + ":X"));
            return b64Creds;
        }
    }
}
