using Microsoft.AspNetCore.Mvc;

namespace FloreriaBautista.Controllers;

[ApiController]
[Tags("Público")]
[Route("api/[controller]")]
public class StoreController : ControllerBase
{
    [HttpGet("info")]
    public IActionResult GetStoreInfo()
    {
        // Información simulada del CMS hasta que tenga modelo base
        var storeInfo = new
        {
            Name = "Florería Bautista",
            Address = "Av. Madero 321, Centro",
            Phone = "+52 123 456 7890",
            Email = "contacto@floreriabautista.com",
            BusinessHours = "Lunes a Domingo 9:00 am - 8:00 pm",
            SocialMedia = new
            {
                Facebook = "https://facebook.com/floreriabautista",
                Instagram = "https://instagram.com/floreriabautista"
            }
        };

        return Ok(storeInfo);
    }
}
