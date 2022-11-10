using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;


namespace smart_local
{
    public static class Program
    {
        private const string _clientId = "fhir_demo_id";
        private const string _defaultFhirServerUrl = "https://launch.smarthealthit.org/v/r4/sim/eyJoIjoiMSIsImUiOiJlNDQzYWM1OC04ZWNlLTQzODUtOGQ1NS03NzVjMWI4ZjNhMzcifQ/fhir";

        private static string _authCode = string.Empty;
        private static string _clientState = string.Empty;

        private static string _redirectUrl = string.Empty;

        private static string _tokenUrl = string.Empty;

        private static string _fhirServerUrl = string.Empty;

        /// <summary>
        /// Programa para acceder a un servidor FHIR SMART con un servidor local para redireccionar. 
        /// </summary>
        /// <param name="fhirServerUrl"> FHIR R4 endpoint URL</param>
        /// <returns></returns>

        static int Main(string fhirServerUrl)
        {
            
            if(string.IsNullOrEmpty(fhirServerUrl))
            {
                fhirServerUrl= _defaultFhirServerUrl;
            }

            System.Console.WriteLine($"FHIR Server: {fhirServerUrl}");
            _fhirServerUrl = fhirServerUrl;

            Hl7.Fhir.Rest.FhirClient fhirClient= new Hl7.Fhir.Rest.FhirClient(fhirServerUrl);

            if(!FhirUtils.TryGetSmartUrls(fhirClient, out string authorizeUrl, out string tokenUrl))
            {
                System.Console.WriteLine($"Fallo al descubrir SMART Urls");
                return -1;
            }
            
            System.Console.WriteLine($"  FHIR Server: {fhirServerUrl}");
            System.Console.WriteLine($"Authorize URL: {authorizeUrl}");
            System.Console.WriteLine($"    Token URL: {tokenUrl}");
            _tokenUrl = tokenUrl;

            Task.Run(() => CreateHostBuilder().Build().Start());

            int listenPort = GetListenPort().Result;

            System.Console. WriteLine($"Listening on port: {listenPort}");
            _redirectUrl = $"http://127.0.0.1:{listenPort}";

            string url =
                $"{authorizeUrl}" +
                $"?response_type=code" +
                $"&client_id={_clientId}" +
                $"&redirect_uri={HttpUtility.UrlEncode(_redirectUrl)}" +
                $"&scope={HttpUtility.UrlEncode("openid fhirUser profile launch/patient patient/*.read")}" +
                $"&state=local_state" +
                $"&aud={fhirServerUrl}"+
                $"&code_challenge=YPXe7B8ghKrj8PsT4L6ltupgI12NQJ5vblB07F4rGaw=" +
                $"&code_challenge_method=S256";

            LaunchUrl(url);
            for (int loops = 0; loops < 30; loops++)
            {
                System.Threading.Thread.Sleep(1000);
            }
                
            return 0;
        }


        /// <summary>
        /// Set the authorization code and state
        /// </summary>
        /// <param name="code"></param>
        /// <param name="state"></param>
        public static async void SetAuthcode(string code, string state)
        {
            _authCode = code;
            _clientState = state;

            System.Console.WriteLine($"Code received: {code}");

            Dictionary<string, string> requestValues = new Dictionary<string, string>()
            {
                {"grant_type","authorization_code"},
                {"code",code},
                {"redirect_uri",_redirectUrl},
                {"client_id",_clientId},
                {"code_verifier","o28xyrYY7-lGYfnKwRjHEZWlFIPlzVnFPYMWbH-g_BsNnQNem-IAg9fDh92X0KtvHCPO5_C-RJd2QhApKQ-2cRp-S_W3qmTidTEPkeWyniKQSF9Q_k10Q5wMc8fGzoyF"}
            };

            HttpRequestMessage request = new HttpRequestMessage()
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(_tokenUrl),
                Content = new FormUrlEncodedContent(requestValues),
            };

            HttpClient client = new HttpClient();

            HttpResponseMessage response = await client.SendAsync(request); 

            if(!response.IsSuccessStatusCode)
            {
                System.Console.WriteLine($"Failed to exchange code for token!");
                throw new Exception($"Unauthorized: {response.StatusCode}");
            }

            string json = await response.Content.ReadAsStringAsync();

            System.Console.WriteLine($"---- Authorization Response ----");
            System.Console.WriteLine(json);
            System.Console.WriteLine($"---- Authorization Response----");

            SmartResponse smartResponse = JsonSerializer.Deserialize<SmartResponse>(json);
            Task.Run(() => DoSomethingWithToken(smartResponse));
        }

        public static void DoSomethingWithToken(SmartResponse smartResponse)
        {
            if (smartResponse == null)
            {
                throw new ArgumentNullException(nameof(smartResponse));
            }

            if(string.IsNullOrEmpty(smartResponse.AccessToken))
            {
                throw new ArgumentNullException($"SMART Access Token is required!");
            }



            var handler = new AuthorizationMessageHandler();
            handler.Authorization = new AuthenticationHeaderValue("Bearer", smartResponse.AccessToken);
            Hl7.Fhir.Rest.FhirClient fhirClient = new Hl7.Fhir.Rest.FhirClient(_fhirServerUrl, Hl7.Fhir.Rest.FhirClientSettings.CreateDefault(), handler);
            
            Hl7.Fhir.Model.Patient patient = fhirClient.Read<Hl7.Fhir.Model.Patient>($"Patient/{smartResponse.PatientId}");

            System.Console.WriteLine($"Read back patient: {patient.Name[0].ToString()}");
        }
        /// <summary>
        /// Launch a Url in the user's default web browser.
        /// </summary>
        /// <param name="url"></param>
        /// <returns>true if succesful, false otherwise</returns>
        public static bool LaunchUrl(string url)
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo()
                {
                    FileName = url,
                    UseShellExecute = true,
                };

                Process.Start(startInfo);
                return true;
            }
            catch (Exception)
            {
                //ignorar
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    url = url.Replace("&", "^&");
                    Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
                    return true;
                }
                catch(Exception)
                {
                    //ignorar
                }
                
            }
            else if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                string[] allowedProgramsToRun = { "xdg-open", "gnome-open", "kfmclient" };
                foreach(string helper in allowedProgramsToRun)
                {  
                    try
                    {
                        Process.Start(helper, url);
                        return true;
                    }
                    catch (Exception)
                    {
                        //ignorar
                    }
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                try
                {
                    Process.Start("open", url);
                    return true;
                }
                catch (Exception)
                {
                    //ignorar
                }
            }

            System.Console.WriteLine($"Failed to Launch URL");
            return false;
        }

        /// <summary>
        /// Determine the listening port of the web server.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static async Task<int> GetListenPort()
        {
            for (int loops = 0; loops < 100; loops++)
            {
                await Task.Delay(100);
                if(Startup.Addresseses == null)
                {
                    continue;
                }
                string address = Startup.Addresseses.Addresses.FirstOrDefault();

                if (string.IsNullOrEmpty(address))
                {
                    continue;
                }

                if (address.Length < 18)
                {
                    continue;
                }

                if ((int.TryParse(address.Substring(17), out int port)) &&
                    (port != 0))
                {
                    return port;
                }
            }

            throw new Exception($"Failed to get listen port!"); 
        }

        public static IHostBuilder CreateHostBuilder() =>
            Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseUrls("http://127.0.0.1:0");
                    webBuilder.UseKestrel();
                    webBuilder.UseStartup<Startup>();
                });
    }
}


