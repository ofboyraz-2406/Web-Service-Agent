using Microsoft.AspNetCore.Mvc;
using damirest.api.Soap;

namespace damirest.api.Controllers
{
    [ApiController]
    [Route("api/soaptest")]
    public class SoapTestController : ControllerBase
    {
        [HttpGet("products")]
        public async Task<IActionResult> GetProductsFromSoap()
        {
            try
            {
                var url = "https://localhost:44366/ProductsService.asmx";
                var xml = await SoapClient.CallGetTop5ProductsAsync(url);
                return Content(xml, "text/xml");
            }
            catch (Exception ex)
            {
                return Problem(ex.ToString()); 
            }
        }
    }
}