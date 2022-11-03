using System;

namespace smart_local
{
    public static class Program
    {
        private const string _defaultFhirServerUrl = "https://launch.smarthealthit.org/v/r4/sim/eyJoIjoiMSIsImUiOiI2MGM5ZmU2My1kOWU2LTRlNWUtOGQ1Yy1mOTFiN2ZjNzU0MTkifQ/fhir/";
        //Programa para acceder a un servidor FHIR SMART con un servidor local para redireccionar. 
        static int Main(
            string fhirServerUrl    
        )
        {
            
            if(string.IsNullOrEmpty(fhirServerUrl))
            {
                fhirServerUrl= _defaultFhirServerUrl;
            }

            System.Console.WriteLine($"FHIR Server: {fhirServerUrl}");

            Hl7.Fhir.Rest.FhirClient fhirClient= new Hl7.Fhir.Rest.FhirClient(fhirServerUrl);

            if(!FhirUtils.TryGetSmartUrls(fhirClient, out string authorizeUrl, out string tokenUrl))
            {
                System.Console.WriteLine($"Fallo al descubrir SMART Urls");
                return -1;
            }
            
            System.Console.WriteLine($"  FHIR Server: {fhirServerUrl}");
            System.Console.WriteLine($"Authorize URL: {authorizeUrl}");
            System.Console.WriteLine($"    Token URL: {tokenUrl}");
            return 0;
        }
    }
}


