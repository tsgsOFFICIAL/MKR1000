using ArduinoApiHTTP.DAL;
using ArduinoApiHTTP.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Text.Json;

namespace ArduinoApiHTTP.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TempController : ControllerBase
    {
        private DBManager _dbManager = new DBManager();

        // GET: api/<TempController>
        [Route("GetAll")]
        [HttpGet]
        public string Get()
        {
            return JsonSerializer.Serialize(_dbManager.GetMeasurements());
        }

        // POST api/<TempController>
        [Route("Post")]
        [HttpPost]
        public bool Post([FromBody] Measurement measurement)
        {
            EventManager.InvokeApi("GetData");
            return _dbManager.AddTemp(measurement);
        }

        [Route("Clear")]
        [HttpDelete]
        public bool Delete()
        {
            return _dbManager.ClearTemps();
        }
    }
}
