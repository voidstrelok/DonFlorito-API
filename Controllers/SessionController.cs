using AutoMapper;
using DonFlorito.DTO;
using DonFlorito.Models;
using DonFlorito.Models.Enum;
using DonFlorito.Util;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Transactions;

namespace DonFlorito.Controllers
{
    [ApiController]
    [Route("api/session")]
    public class SessionController : ControllerBase
    {

        private readonly ILogger<SessionController> _logger;
        private readonly DonFloritoContext BD;
        private readonly IMapper Mapper;
        private readonly Utils Util;
        private readonly IConfiguration Config;

        public SessionController(ILogger<SessionController> logger, DonFloritoContext context, IMapper mapper, Utils utils, IConfiguration config)
        {
            BD = context;
            _logger = logger;
            Mapper = mapper;
            Util = utils;
            Config = config;
        }

        [AllowAnonymous]
        [Route("GetParametros")]
        [HttpGet]
        public ParametrosDTO GetParametros()        
        {
            Parametros Parametros = BD.Parametros.FirstOrDefault();
            return Mapper.Map<ParametrosDTO>(Parametros);
        }

        [AllowAnonymous]
        [Route("AdminLogin")]
        [HttpPost]
        public ActionResult<string> AdminLogin([FromForm] string usuario, [FromForm] string password)
        {
            // Credenciales del panel: sección "Admin" de appsettings (o variables de entorno Admin__Usuario / Admin__Password).
            var adminUsuario = Config.GetValue<string>("Admin:Usuario");
            var adminPassword = Config.GetValue<string>("Admin:Password");

            if (string.IsNullOrEmpty(adminUsuario) || string.IsNullOrEmpty(adminPassword))
            {
                _logger.LogWarning("Login de administración deshabilitado: falta Admin:Usuario o Admin:Password.");
                return NotFound("Credenciales incorrectas o el usuario no existe");
            }

            // Se comparan ambos siempre (sin cortar en el primero) y en tiempo constante.
            var usuarioOk = IgualesEnTiempoConstante(usuario, adminUsuario);
            var passwordOk = IgualesEnTiempoConstante(password, adminPassword);
            if (!(usuarioOk & passwordOk))
            {
                return NotFound("Credenciales incorrectas o el usuario no existe");
            }

            return Util.getToken(adminUsuario);
        }

        private static bool IgualesEnTiempoConstante(string? a, string b)
        {
            var ha = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(a ?? ""));
            var hb = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(b));
            return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(ha, hb);
        }

        /// <summary>Precios habilitados de los tipos que cambian por temporada (piscinas), para editarlos todos juntos.</summary>
        [Authorize]
        [Route("GetPreciosTemporada")]
        [HttpGet]
        public List<PrecioTemporadaDTO> GetPreciosTemporada()
        {
            return BD.PrecioServicio
                .Include(p => p.IdTipoServicioNavigation)
                .Where(p => p.IsEnabled && p.TemporadaDesde != null)
                .OrderBy(p => p.IdTipoServicio).ThenBy(p => p.TemporadaDesde)
                .Select(p => new PrecioTemporadaDTO
                {
                    Id = p.Id,
                    IdTipoServicio = p.IdTipoServicio,
                    NombreTipoServicio = p.IdTipoServicioNavigation.Nombre ?? "",
                    Precio = p.Precio,
                    TemporadaDesde = p.TemporadaDesde,
                    TemporadaHasta = p.TemporadaHasta
                })
                .ToList();
        }

        /// <summary>Cambia precios por temporada. Como en GuardarConfig, el precio anterior se inhabilita y se crea uno nuevo (las reservas hechas conservan el suyo).</summary>
        [Authorize]
        [Route("GuardarPreciosTemporada")]
        [HttpPost]
        public ActionResult<bool> GuardarPreciosTemporada([FromBody] List<CambioPrecioDTO> cambios)
        {
            if (cambios == null || cambios.Any(c => c.Precio < 0))
            {
                return BadRequest("Los precios deben ser números enteros mayores o iguales a 0.");
            }

            foreach (var cambio in cambios)
            {
                var actual = BD.PrecioServicio.FirstOrDefault(p => p.Id == cambio.Id && p.IsEnabled && p.TemporadaDesde != null);
                if (actual == null)
                {
                    return BadRequest("Uno de los precios ya no está vigente. Recarga la pantalla e intenta de nuevo.");
                }
                if (actual.Precio == cambio.Precio) continue;

                BD.PrecioServicio.Add(new PrecioServicio
                {
                    IdTipoServicio = actual.IdTipoServicio,
                    Minutos = actual.Minutos,
                    TemporadaDesde = actual.TemporadaDesde,
                    TemporadaHasta = actual.TemporadaHasta,
                    IsEnabled = true,
                    Precio = cambio.Precio
                });
                actual.IsEnabled = false;
            }

            BD.SaveChanges();
            return true;
        }

        [Authorize]
        [Route("SessionIsValid")]
        [HttpGet]
        public async Task<ActionResult<bool>> SessionIsValid()
        {
            return true;
        }

        [Authorize]
        [Route("GetConfig")]
        [HttpGet]
        public async Task<ActionResult<ConfigDTO>> GetConfig()
        {
            var parametros = BD.Parametros.FirstOrDefault();

            var Servicios = BD.Servicio 
                .Include(s=>s.IdTipoServicioNavigation.PrecioServicio)
                .ToList();

            var PiscinasEnabled = BD.Servicio.Any(s => TiposServicio.Piscinas.Contains(s.IdTipoServicio) && s.IsEnabled);

            List<ServicioDTO> ServiciosDTO = Servicios.Select(s => Mapper.Map<ServicioDTO>(s)).ToList();

            var config = new ConfigDTO {
                HApertura = parametros.HoraApertura.Hour,
                MApertura = parametros.HoraApertura.Minute,
                HCierre = parametros.HoraCierre.Hour,
                MCierre = parametros.HoraCierre.Minute,
                PiscinasEnabled = PiscinasEnabled,
                ReservasEnabled = parametros.ReservasEnabled,
                Servicios = ServiciosDTO
            };

            return config;
        }

        [Authorize]
        [Route("GuardarConfig")]
        [HttpPost]
        public async Task<ActionResult<bool>> GuardarConfig([FromBody] ConfigDTO config)
        {
            
            var parametros = BD.Parametros.Where(p=>p.Id==0).First();

            parametros.HoraApertura = parametros.HoraApertura.Date.AddHours(config.HApertura).AddMinutes(config.MApertura);
            parametros.HoraCierre = parametros.HoraCierre.Date.AddHours(config.HCierre).AddMinutes(config.MCierre);
            parametros.ReservasEnabled = config.ReservasEnabled;

            var servicios = BD.Servicio.ToList();

            foreach(var servicio in config.Servicios)
            {
                var serv = servicios.Where(s => s.Id == servicio.Id).FirstOrDefault();
                serv.IsEnabled = servicio.IsEnabled;

                if(servicio.CambiaPrecio)
                {
                    var ps = TiposServicio.PrecioVigente(BD.PrecioServicio.Where(ps => ps.IdTipoServicio == servicio.IdTipoServicio).ToList(), DateTime.Today);
                    BD.PrecioServicio.Add(new PrecioServicio
                    {
                        IdTipoServicio = servicio.IdTipoServicio,
                        Minutos = ps.Minutos,
                        TemporadaDesde = ps.TemporadaDesde,
                        TemporadaHasta = ps.TemporadaHasta,
                        IsEnabled = true,
                        Precio = servicio.Precio
                    });
                    ps.IsEnabled = false;
                }
                
            }
            var Piscinas = BD.Servicio.Where(s => TiposServicio.Piscinas.Contains(s.IdTipoServicio)).ToList();

            foreach(var pisc in Piscinas)
            {
                pisc.IsEnabled = config.PiscinasEnabled;
            }

            BD.SaveChanges();

            return true;
        }
    }
}
