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

        public SessionController(ILogger<SessionController> logger, DonFloritoContext context, IMapper mapper, Utils utils)
        {
            BD = context;
            _logger = logger;
            Mapper = mapper;
            Util = utils;
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
        public async Task<ActionResult<string>> AdminLogin([FromForm] string usuario, [FromForm] string password)
        {
            var Usuario = await BD.Usuario.Where(u => u.Usuario1.Equals(usuario) && u.Contraseña.Equals(Util.Saltear(password))).Include(u => u.IdPersonaNavigation).FirstOrDefaultAsync();
            Console.WriteLine(Util.Saltear(password));
            if (Usuario == null)
            {
                return NotFound("Credenciales incorrectas o el usuario no existe");
            }

            if (!Usuario.IsEnabled)
            {
                return Forbid("Acceso no autorizado");
            }
            return Util.getToken(Usuario);

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

            var PiscinasEnabled = BD.Servicio.Any(s => (s.IdTipoServicio == (long)EnumTipoServicio.PiscinaGeneral || s.IdTipoServicio == (long)EnumTipoServicio.PiscinaAM) && s.IsEnabled);

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
                    var ps = BD.PrecioServicio.Where(ps => ps.IdTipoServicio == servicio.IdTipoServicio).FirstOrDefault(ps => ps.IsEnabled);
                    BD.PrecioServicio.Add(new PrecioServicio
                    {
                        IdTipoServicio = servicio.IdTipoServicio,
                        Minutos = ps.Minutos,
                        IsEnabled = true,
                        Precio = servicio.Precio
                    });
                    ps.IsEnabled = false;
                }
                
            }
            var Piscinas = BD.Servicio.Where(s => (s.IdTipoServicio == (long)EnumTipoServicio.PiscinaGeneral || s.IdTipoServicio == (long)EnumTipoServicio.PiscinaAM)).ToList();

            foreach(var pisc in Piscinas)
            {
                pisc.IsEnabled = config.PiscinasEnabled;
            }

            BD.SaveChanges();

            return true;
        }
        [Route("Test")]
        [HttpPost]
        public async Task<IActionResult> Test(int reserva)
        {
            //var NReserva = BD.Reserva.Where(r => r.Id == reserva)
            //            .Include(r => r.ReservaServicio).ThenInclude(rs => rs.IdPrecioServicioNavigation)
            //            .Include(r => r.ReservaServicio).ThenInclude(rs => rs.IdServicioNavigation)
            //            .Include(r => r.IdPersonaNavigation)
            //            .Include(r => r.OrdenCompra).ThenInclude(od => od.Voucher)
            //            .FirstOrDefault();

            //var vc = NReserva.OrdenCompra.FirstOrDefault().Voucher.FirstOrDefault();
            //Util.EnviarCorreoPagarReserva(NReserva);
            //Util.EnviarCorreoReservaPagada(NReserva,vc);
            Console.WriteLine("comienza metodo");
            return Ok();

        }

    }
}