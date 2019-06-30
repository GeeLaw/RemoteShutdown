using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Http;

namespace RemoteShutdown.Controllers
{
    public class ValuesController : ApiController
    {
        static Regex safeHost = new Regex("^localhost$|^127\\.0\\.0\\.1$|^172\\.20\\.10\\.2[0-4][0-9]$|^172\\.20\\.10\\.25[0-5]$|^172\\.20\\.10\\.1[0-9][0-9]$|^172\\.20\\.10\\.[1-9][0-9]$|^172\\.20\\.10\\.[1-9]$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        static HttpResponseMessage MakeTextualResponse(HttpRequestMessage msg,
            HttpStatusCode statusCode,
            string text)
        {
            var codeNumber = ((int)statusCode).ToString();
            var codeName = statusCode.ToString();
            text = "HTTP " + codeNumber
                + " (" + codeName + ")\n\n"
                + text;
            var htmlPayload = "<!DOCTYPE html><html><head>"
                + "<meta charset=\"utf-8\" />"
                + "<meta http-equiv=\"X-UA-Compatible\" content=\"IE=Edge\" />"
                + "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />"
                + "<meta name=\"SKYPE_TOOLBAR\" content=\"SKYPE_TOOLBAR_PARSER_COMPATIBLE\" />"
                + "<meta name=\"IE_RM_OFF\" content=\"true\" />"
                + "<meta name=\"format-detection\" content=\"telephone=no\" />"
                + "<title>HTTP " + codeNumber + "</title></head><body style=\"font-size: 20px;\">"
                + "<pre style=\"overflow-wrap: break-word; word-wrap: break-word; white-space: pre-wrap;\">"
                + "<code style=\"overflow-wrap: break-word; word-wrap: break-word; white-space: pre-wrap;\">"
                + WebUtility.HtmlEncode(text)
                + "</code></pre></body></html>";
            HttpResponseMessage rsp = new HttpResponseMessage(statusCode);
            rsp.Content = new StringContent(htmlPayload, Encoding.UTF8);
            rsp.RequestMessage = msg;
            rsp.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");
            return rsp;
        }

        static HttpResponseMessage MakeExceptionResponse(HttpRequestMessage msg,
            Exception ex)
        {
            var text = "Exception details are suppressed.";
            if (msg.ShouldIncludeErrorDetail())
            {
                text = "An exception has occurred: "
                    + ex.GetType().FullName + "\n\n"
                    + "Message: " + ex.Message + "\n\n"
                    + "Stack:\n" + ex.StackTrace + "\n";
            }
            return MakeTextualResponse(msg, HttpStatusCode.InternalServerError, text);
        }

        static DateTime lastRequest = DateTime.UtcNow.AddSeconds(-60);

        public async Task<HttpResponseMessage> Post()
        {
            try
            {
                if (!safeHost.IsMatch(Request.RequestUri.Host ?? "?"))
                {
                    return MakeTextualResponse(Request, HttpStatusCode.Forbidden,
                        "Request must be sent to localhost, 127.0.0.1 or 172.20.10.*.");
                }
                if ((DateTime.UtcNow - lastRequest).TotalSeconds < 20)
                {
                    return MakeTextualResponse(Request, (HttpStatusCode)429,
                        "To prevent brute-forcing the passwords and DoS, please try again later.");
                }
                var httpForm = await Request.Content.ReadAsFormDataAsync();
                var form = new Dictionary<string, string>();
                form["domain"] = null;
                form["user"] = null;
                form["password"] = null;
                form["program"] = null;
                form["arguments"] = null;
                for (int i = 0, j = httpForm.Count; i != j; ++i)
                {
                    var key = httpForm.GetKey(i);
                    if (key == null)
                    {
                        return MakeTextualResponse(Request, HttpStatusCode.BadRequest,
                            "Unknown parameter <null>.");
                    }
                    if (!form.ContainsKey(key))
                    {
                        return MakeTextualResponse(Request, HttpStatusCode.BadRequest,
                            "Unknown parameter \"" + key + "\".");
                    }
                    if (form[key] != null)
                    {
                        return MakeTextualResponse(Request, HttpStatusCode.BadRequest,
                            "Multiple values for parameter \"" + key + "\".");
                    }
                    form[key] = httpForm.Get(i);
                }
                foreach (var kvp in form)
                {
                    if (kvp.Value == null)
                    {
                        return MakeTextualResponse(Request, HttpStatusCode.BadRequest,
                            "No value for parameter \"" + kvp.Key + "\".");
                    }
                    if (kvp.Value.Length > 120)
                    {
                        return MakeTextualResponse(Request, HttpStatusCode.RequestEntityTooLarge,
                            "Value for parameter \"" + kvp.Key + "\" is too long.");
                    }
                }
                string domain = form["domain"].Trim();
                string user = form["user"].Trim();
                string password = form["password"];
                string program = form["program"];
                string arguments = form["arguments"];
                if (string.IsNullOrWhiteSpace(program))
                {
                    return MakeTextualResponse(Request, HttpStatusCode.BadRequest,
                        "Parameter \"program\" must not be empty.");
                }
                if (user == "")
                {
                    return MakeTextualResponse(Request, HttpStatusCode.BadRequest,
                        "Parameter \"user\" must not be empty.");
                }
                if (password == "")
                {
                    return MakeTextualResponse(Request, HttpStatusCode.Forbidden,
                        "Empty password is not allowed.");
                }
                lastRequest = DateTime.UtcNow;
                var psi = new ProcessStartInfo();
                if (domain != "")
                {
                    psi.Domain = domain;
                }
                psi.UserName = user;
                psi.PasswordInClearText = password;
                psi.LoadUserProfile = false;
                psi.UseShellExecute = false;
                psi.WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
                psi.FileName = program;
                psi.Arguments = arguments;
                psi.CreateNoWindow = true;
                var ps = new Process();
                ps.StartInfo = psi;
                ps.Start();
                return MakeTextualResponse(Request, HttpStatusCode.Accepted,
                    "Started the program with the provided credential.");
            }
            catch (Exception ex)
            {
                var ex32 = ex as Win32Exception;
                if (ex32 != null && ex32.NativeErrorCode == 1326)
                {
                    return MakeTextualResponse(Request, HttpStatusCode.Unauthorized,
                        "User name or password is incorrect.");
                }
                return MakeExceptionResponse(Request, ex);
            }
        }
    }
}
