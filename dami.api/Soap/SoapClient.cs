using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;

namespace damirest.api.Soap
{
    public static class SoapClient
    {
        public static async Task<string> CallGetTop5ProductsAsync(string asmxUrl)
        {
            // asmxUrl örnek: "http://localhost:44366/ProductsService.asmx"
            var soapAction = "http://tempuri.org/GetTop5Products";

            var envelope = @"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
               xmlns:xsd=""http://www.w3.org/2001/XMLSchema""
               xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Body>
    <GetTop5Products xmlns=""http://tempuri.org/"" />
  </soap:Body>
</soap:Envelope>";

            using var http = new HttpClient();

            var content = new StringContent(envelope, Encoding.UTF8, "text/xml");
            content.Headers.ContentType = new MediaTypeHeaderValue("text/xml") { CharSet = "utf-8" };
            content.Headers.Add("SOAPAction", $"\"{soapAction}\"");

            var res = await http.PostAsync(asmxUrl, content);
            res.EnsureSuccessStatusCode();

            return await res.Content.ReadAsStringAsync();
        }

    }
}