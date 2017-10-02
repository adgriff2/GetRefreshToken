using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Net;
//using Newtonsoft.Json.Converters;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GetRefreshToken
{
    public partial class Form1 : Form
    {
        private HttpClient SsoClient = new HttpClient();

        public Form1()
        {
            InitializeComponent();
            SsoClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            string callbackURI = "http%3A%2F%2Flocalhost%3A8080%2F";
            string scopes = Uri.EscapeUriString(string.Join(" ", checkedListBox1.CheckedItems.OfType<string>().ToArray()));
            //Uri scopes = new Uri();
            System.Diagnostics.Process.Start("https://login.eveonline.com/oauth/authorize/" +
                "?response_type=code&" +
                $"redirect_uri={callbackURI}&" +
                $"client_id={textBox1.Text}&" +
                $"scope={scopes}");
            string AuthCode;
            using (HttpListener SSOListener = new HttpListener())
            {
                SSOListener.Prefixes.Add(@"http://localhost:8080/");
                SSOListener.Start();
                var context = SSOListener.GetContext();
                AuthCode = context.Request.QueryString["code"];
                var response = context.Response;
                string responseString = "<html><body>GetRefreshToken Token Received</body></html>";
                var buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                response.ContentLength64 = buffer.Length;
                var output = response.OutputStream;
                output.Write(buffer, 0, buffer.Length);
                output.Close();
                SSOListener.Stop();
            }

            AccessToken accessToken = JsonConvert.DeserializeObject<AccessToken>(SsoPost("authorization_code","code",AuthCode).Result);
            textBox3.Text = accessToken.refresh_token;
            textBox4.Text = accessToken.access_token;

            SsoClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.access_token);

            string Response = await SsoClient.GetStringAsync("https://login.eveonline.com/oauth/verify");
            TokenVerify verify = JsonConvert.DeserializeObject<TokenVerify>(Response);

            textBox5.Text = verify.CharacterID.ToString();
            textBox6.Text = verify.CharacterName;
            textBox7.Text = verify.ExpiresOn.ToString("o");
        }

        private string Base64Encode(string PlainText)
        {
            var PlainTextBytes = Encoding.UTF8.GetBytes(PlainText);
            return Convert.ToBase64String(PlainTextBytes);
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            //populate checkedListBox with current scopes
            using (HttpClient scopesClient = new HttpClient())
            {
                string returnString = await scopesClient.GetStringAsync("https://esi.tech.ccp.is/latest/swagger.json?datasource=tranquility");
                List<string> scopes = ((Dictionary<string, object>)((Dictionary<string, object>)((Dictionary<string, object>)((Dictionary<string, object>)
                    JsonHelper.Deserialize(returnString))
                    .Where(o => o.Key == "securityDefinitions").FirstOrDefault().Value)
                    .Where(o => o.Key == "evesso").FirstOrDefault().Value)
                    .Where(o => o.Key == "scopes").FirstOrDefault().Value)
                    .Select(o => o.Key).ToList();
                checkedListBox1.Items.AddRange(scopes.ToArray());
            }
        }

        private async Task<string> SsoPost(string Grant, string CodeType, string Code)
        {
            string ResponseString;
            SsoClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Base64Encode(textBox1.Text + ":" + textBox2.Text));
            HttpResponseMessage Response = await SsoClient.PostAsync(new Uri("https://login.eveonline.com/oauth/token"), new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", Grant),
                    new KeyValuePair<string, string>(CodeType, Code)
                })).ConfigureAwait(false);
            ResponseString = await Response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return ResponseString;
        }
    }

    public static class JsonHelper
    {
        public static object Deserialize(string json)
        {
            return ToObject(JToken.Parse(json));
        }

        private static object ToObject(JToken token)
        {
            switch (token.Type)
            {
                case JTokenType.Object:
                    return token.Children<JProperty>()
                                .ToDictionary(prop => prop.Name,
                                              prop => ToObject(prop.Value));

                case JTokenType.Array:
                    return token.Select(ToObject).ToList();

                default:
                    return ((JValue)token).Value;
            }
        }
    }

    class AccessToken
    {
        public string access_token { get; set; }
        public string token_type { get; set; }
        public int expires_in { get; set; }
        public string refresh_token { get; set; }
    }

    public class TokenVerify
    {
        public int CharacterID;
        public string CharacterName;
        public DateTime ExpiresOn;
        public string Scopes;
        public string TokenType;
        public string CharacterOwnerHash;
        public string IntellectualProperty;
    }
}
