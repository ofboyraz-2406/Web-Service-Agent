using Microsoft.AspNetCore.Mvc;

namespace dummyweb.api.Controllers;

[ApiController]
[Route("web")]
public class WebDataController : ControllerBase
{
    [HttpGet("data")]
    public IActionResult GetData()
    {
        var html = $@"<!DOCTYPE html>
<html>
<head><title>Web Data</title></head>
<body>
    <ul>
        <li>
            <h2><a href='/web/item/item-1'>Item 1</a></h2>
            <ul>
                <li>Deger1</li>
                <li>Deger2</li>
                <li>Deger3</li>
            </ul>
        </li>
        <li>
            <h2><a href='/web/item/item-2'>Item 2</a></h2>
            <ul>
                <li>Deger4</li>
                <li>Deger5</li>
                <li>Deger6</li>
            </ul>
        </li>
    </ul>
</body>
</html>";

        return Content(html, "text/html");
    }
}