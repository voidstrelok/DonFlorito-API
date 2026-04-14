using AutoMapper;
using DonFlorito.DTO;
using DonFlorito.Models;
using DonFlorito;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DonFlorito.Models.Enum;
using Transbank.Webpay.WebpayPlus;
using Transbank.Common;
using Transbank.Webpay.Common;
using System.Transactions;
using Transaction = Transbank.Webpay.WebpayPlus.Transaction;
using DonFlorito.Util;
using System.Resources;
using Rut;
using MimeKit;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Mvc.Razor;
using DonFlorito.Services;

namespace DonFlorito.Controllers
{
    [ApiController]
    [Route("api/reservas")]
    public class ReservasController : ControllerBase
    {

        private readonly ILogger<SessionController> _logger;
        private readonly DonFloritoContext BD;
        private readonly IMapper Mapper;
        private readonly Utils Util;
        private readonly PersonaService PersonaService;

        public ReservasController(ILogger<SessionController> logger, DonFloritoContext context, IMapper mapper, Utils util, PersonaService personaService)
        {
            BD = context;
            _logger = logger;
            Mapper = mapper;
            Util = util;
            PersonaService = personaService;
        }

        [HttpPost]
        public async Task<ActionResult<ReservaDTO>> NuevaReserva([FromBody] ReservaCreacionDTO reserva)
        {
            //reservas deshabilitadas al momento
            if(!BD.Parametros.FirstOrDefault().ReservasEnabled)
            {
                return BadRequest("El sistema de reservas est� deshabilitado");
            }

            if (reserva == null)
            {
                return BadRequest("Sin datos");
            }
            //TODO postear persona aca
            if (reserva.IdPersona == null && reserva.PersonaCreacion == null)
            {
                return BadRequest("Sin datos de persona.");
            }

            var resultadoPersona = await PersonaService.ObtenerOCrearPersonaAsync(reserva.IdPersona, reserva.PersonaCreacion);
            if (resultadoPersona.Error != null)
            {
                if (resultadoPersona.Error is ObjectResult errorConDetalle)
                {
                    return StatusCode(errorConDetalle.StatusCode ?? 400, errorConDetalle.Value);
                }
                return BadRequest();
            }
            var IdPersona = resultadoPersona.IdPersona;
            using (TransactionScope transactionScope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                try
                {
                    

                    List<ReservaServicio> reservaServicios = new List<ReservaServicio>();
                    foreach (var serv in reserva.ReservaServicio)
                    {
                        reservaServicios.Add(new ReservaServicio()
                        {
                            IdServicio = serv.IdServicio,
                            IdPrecioServicio = serv.IdPrecioServicio,
                            Cantidad = serv.Cantidad,
                            HoraComienzo = serv.HoraComienzo,

                        });
                    }

                    Reserva NReserva = new Reserva()
                    {
                        IdEstadoReserva = (long)EnumEstadoReserva.PagoPendiente,
                        IdPersona = IdPersona,
                        FechaReserva = reserva.FechaReserva,
                        FechaIngreso = DateTime.Now,
                        FechaCancelacion = null,
                        FechaConfirmacion = null,
                        Comentario = "",
                        IsEnabled = true,
                        ReservaServicio = reservaServicios,
                    };
                    
                    BD.Reserva.Add(NReserva);
                    await BD.SaveChangesAsync();

                    NReserva = BD.Reserva.Where(r => r.Id == NReserva.Id)
                        .Include(r => r.ReservaServicio).ThenInclude(rs => rs.IdPrecioServicioNavigation)
                        .Include(r => r.ReservaServicio).ThenInclude(rs => rs.IdServicioNavigation)
                        .Include(r=> r.IdEstadoReservaNavigation)
                        .FirstOrDefault();

                    var Fecha = NReserva.FechaReserva;
                    
                    
                    
                        
                    //comprueba reserva valida
                    foreach (var ReservaServicio in NReserva.ReservaServicio)
                    {
                        var Servicio = ReservaServicio.IdServicioNavigation;

                        //servicio deshabilitado
                        if (!ReservaServicio.IdServicioNavigation.IsEnabled)
                        {
                            return BadRequest("Uno o m�s servicios no est�n disponibles. Favor intente reservar nuevamente.");
                        }
                        //servicio con fecha ocupada
                        if (Servicio.IdTipoServicio != (long)EnumTipoServicio.Quincho && Servicio.IdTipoServicio != (long)EnumTipoServicio.PiscinaGeneral && Servicio.IdTipoServicio != (long)EnumTipoServicio.PiscinaAM)
                        {

                            
                            var reservas = BD.ReservaServicio.Where(rs =>
                            rs.IdReserva != NReserva.Id &&
                            rs.IdServicio == Servicio.Id
                            && rs.IdReservaNavigation.FechaReserva.Date == Fecha
                            && rs.IdReservaNavigation.IsEnabled
                            && (rs.IdReservaNavigation.IdEstadoReserva == (long)EnumEstadoReserva.Confirmada))
                                .OrderBy(rs => rs.HoraComienzo)
                                .ToList();

                            var reservasEsp = BD.ReservasEspeciales.Where(re => (re.FechaComienzo.Date <= Fecha || re.FechaTermino >= Fecha) && (re.IsCanchas || re.IsCamping || re.IdServicio == Servicio.Id) && re.IsEnabled).ToList();

                            var ListaReservas = Util.ObtenerEventos(reservas, reservasEsp, Fecha);
                            if (ListaReservas.Count > 0)
                            {
                                var InicioReserva = Fecha.AddMinutes(ReservaServicio.HoraComienzo.Value.TimeOfDay.TotalMinutes);
                                var FinReserva = InicioReserva.AddMinutes((double)(ReservaServicio.IdPrecioServicioNavigation.Minutos * ReservaServicio.Cantidad));

                                foreach (var evento in ListaReservas)
                                {
                                    if ((InicioReserva.TimeOfDay >= evento.HoraComienzo.TimeOfDay && InicioReserva.TimeOfDay < evento.HoraFinal.TimeOfDay) || //reserva comienza en medio de evento
                                        (InicioReserva.TimeOfDay < evento.HoraComienzo.TimeOfDay && FinReserva.TimeOfDay > evento.HoraComienzo.TimeOfDay) || //reserva tiene evento en medio
                                        (InicioReserva.TimeOfDay >= evento.HoraComienzo.TimeOfDay && FinReserva.TimeOfDay <= evento.HoraFinal.TimeOfDay))   //reserva entre evento
                                    {
                                        return BadRequest("El horario de su reserva dej� de estar disponible. Favor intente reservar en otro horario.");
                                    }
                                }

                            }
                        }

                    }
                    
                    //Util.EnviarCorreoPagarReserva(NReserva);                    

                    ReservaDTO ReservaDTO = Mapper.Map<ReservaDTO>(NReserva);
                    return ReservaDTO;
                }
                catch (Exception ex)
                {
                    transactionScope.Dispose();
                    return BadRequest(ex.Message);
                }
            } 
            
        }

        [Route("ConfirmarReserva/{token_ws}")]
        [HttpPost]
        public async Task<ActionResult<ReservaDTO>> ConfirmarReserva([FromBody] ReservaDTO reserva, string token_ws)
        {
            if (reserva == null || string.IsNullOrEmpty(token_ws))
            {
                return BadRequest("Sin datos");
            }
            //TODO postear persona aca
            if (reserva.Persona == null)
            {
                return BadRequest("Sin datos de persona.");
            }

            var Orden = BD.OrdenCompra.Where(o => o.Id == reserva.IdOrdenCompra).FirstOrDefault();

            if (Orden == null)
            {
                return BadRequest("Error en la orden de compra");

            }

            if (!Orden.Token.Equals(token_ws))
            {
                return BadRequest("Error en la orden de compra");

            }

            using (TransactionScope transactionScope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                var DetalleTX = Util.Check(token_ws);
                var personaCreacion = new PersonaCreacionDTO
                {
                    Nombre = reserva.Persona.Nombre,
                    SegundoNombre = reserva.Persona.SegundoNombre,
                    ApellidoPaterno = reserva.Persona.ApellidoPaterno,
                    ApellidoMaterno = reserva.Persona.ApellidoMaterno,
                    Email = reserva.Persona.Email,
                    Telefono = reserva.Persona.Telefono,
                    Rut = reserva.Persona.Rut
                };
                var resultadoPersona = await PersonaService.ObtenerOCrearPersonaAsync(null, personaCreacion);
                if (resultadoPersona.Error != null)
                {
                    if (resultadoPersona.Error is ObjectResult errorConDetalle)
                    {
                        return StatusCode(errorConDetalle.StatusCode ?? 400, errorConDetalle.Value);
                    }
                    return BadRequest();
                }
                var IdPersona = resultadoPersona.IdPersona;

                try
                {


                    List<ReservaServicio> reservaServicios = new List<ReservaServicio>();
                    foreach (var serv in reserva.ReservaServicio)
                    {
                        reservaServicios.Add(new ReservaServicio()
                        {
                            IdServicio = serv.IdServicio,
                            IdPrecioServicio = serv.IdPrecioServicio,
                            Cantidad = serv.Cantidad,
                            HoraComienzo = serv.HoraComienzo,

                        });
                    }

                    Reserva NReserva = new Reserva()
                    {
                        IdEstadoReserva = (long)EnumEstadoReserva.Confirmada,
                        IdPersona = IdPersona,
                        FechaReserva = reserva.FechaReserva,
                        FechaIngreso = DateTime.Now,
                        FechaCancelacion = null,
                        FechaConfirmacion = DateTime.Now,
                        Comentario = "",
                        IsEnabled = true,
                        ReservaServicio = reservaServicios,
                    };

                    BD.Reserva.Add(NReserva);
                    BD.SaveChanges();

                    

                    NReserva = BD.Reserva.Where(r => r.Id == NReserva.Id)
                        .Include(r => r.ReservaServicio).ThenInclude(rs => rs.IdPrecioServicioNavigation)
                        .Include(r => r.ReservaServicio).ThenInclude(rs => rs.IdServicioNavigation)
                        .FirstOrDefault();

                    long TotalCarro = 0;
                    foreach (var serv in NReserva.ReservaServicio)
                    {
                        TotalCarro += serv.IdPrecioServicioNavigation.Precio * serv.Cantidad;
                    }

                    if (TotalCarro != DetalleTX.Amount)
                    {
                        return BadRequest("Error en la orden de compra. (Amount mismatch)");
                    }

                    var Fecha = NReserva.FechaReserva;

                    //comprueba reserva valida
                    foreach (var ReservaServicio in NReserva.ReservaServicio)
                    {
                        var Servicio = ReservaServicio.IdServicioNavigation;

                        //servicio deshabilitado
                        if (!ReservaServicio.IdServicioNavigation.IsEnabled)
                        {
                            return BadRequest("Uno o m�s servicios no est�n disponibles. Favor intente reservar nuevamente. (Services not available anymore)");
                        }
                        //servicio con fecha ocupada
                        if (Servicio.IdTipoServicio != (long)EnumTipoServicio.Quincho && Servicio.IdTipoServicio != (long)EnumTipoServicio.PiscinaGeneral && Servicio.IdTipoServicio != (long)EnumTipoServicio.PiscinaAM)
                        {


                            var reservas = BD.ReservaServicio.Where(rs =>
                            rs.IdReserva != NReserva.Id &&
                            rs.IdServicio == Servicio.Id
                            && rs.IdReservaNavigation.FechaReserva.Date == Fecha
                            && rs.IdReservaNavigation.IsEnabled
                            && (rs.IdReservaNavigation.IdEstadoReserva == (long)EnumEstadoReserva.Confirmada))
                                .OrderBy(rs => rs.HoraComienzo)
                                .ToList();

                            var reservasEsp = BD.ReservasEspeciales.Where(re => (re.FechaComienzo.Date <= Fecha || re.FechaTermino >= Fecha) && (re.IsCanchas|| re.IsCamping || re.IdServicio == Servicio.Id) && re.IsEnabled).ToList();

                            var ListaReservas = Util.ObtenerEventos(reservas, reservasEsp, Fecha);
                            if (ListaReservas.Count > 0)
                            {
                                var InicioReserva = Fecha.AddMinutes(ReservaServicio.HoraComienzo.Value.TimeOfDay.TotalMinutes);
                                var FinReserva = InicioReserva.AddMinutes((double)(ReservaServicio.IdPrecioServicioNavigation.Minutos * ReservaServicio.Cantidad));

                                foreach (var evento in ListaReservas)
                                {
                                    if ((InicioReserva.TimeOfDay >= evento.HoraComienzo.TimeOfDay && InicioReserva.TimeOfDay < evento.HoraFinal.TimeOfDay) || //reserva comienza en medio de evento
                                        (InicioReserva.TimeOfDay < evento.HoraComienzo.TimeOfDay && FinReserva.TimeOfDay > evento.HoraComienzo.TimeOfDay) || //reserva tiene evento en medio
                                        (InicioReserva.TimeOfDay >= evento.HoraComienzo.TimeOfDay && FinReserva.TimeOfDay <= evento.HoraFinal.TimeOfDay))   //reserva entre evento
                                    {
                                        return BadRequest("El horario de su reserva dej� de estar disponible. Favor intente reservar en otro horario. (Schedule already taken)");
                                    }
                                }

                            }
                        }

                    }

                    var ConfirmaTX = Util.Commit(token_ws);

                    Voucher vc = Mapper.Map<Voucher>(ConfirmaTX);
                    vc.IdOrdenCompra = Orden.Id;
                    vc.Fecha = DateTime.Now;
                    Orden.IdReserva = NReserva.Id;
                    BD.Voucher.Add(vc);

                    switch (vc.ResponseCode)
                    {
                        case 0:
                            NReserva.IdEstadoReserva = (long)EnumEstadoReserva.Confirmada;
                            NReserva.FechaConfirmacion = DateTime.Now;
                            BD.SaveChanges();
                            transactionScope.Complete();
                            await Util.EnviarCorreoReservaPagada(NReserva, vc);
                            ReservaDTO ReservaDTO = Mapper.Map<ReservaDTO>(NReserva);
                            return ReservaDTO;
                        default:
                            // fallo del pago
                            return BadRequest("El pago ha fallado o ha sido rechazado, favor intente nuevamente. (Payment Rejected or Failed)");
                    }
                }
                catch (Exception ex)
                {
                    transactionScope.Dispose();
                    return BadRequest(ex.Message);
                }
            }
        }

        [Route("getReservacionesByServicio")]
        [HttpPost]
        //traer reservaciones por servicio para elegir el horario????
        public List<ReservaDTO> getReservacionesByServicio([FromForm] long IdTipoServicio, [FromForm] string FechaReserva)
        {
            var Fecha = DateTime.Parse(FechaReserva);

            var Reservas = BD.ReservaServicio
                .Where(rs => rs.IdServicioNavigation.IdTipoServicio == IdTipoServicio)
                .Select(r => r.IdReservaNavigation)
                .Where(r => r.FechaReserva.Date == Fecha.Date).ToList();

            List<ReservaDTO> NReserva = Reservas.Select(r => Mapper.Map<ReservaDTO>(r)).ToList();
            return NReserva;
        }

        [Route("getById/{IdReserva:long}")]
        [HttpGet]
        public async Task<ActionResult<ReservaDTO>> getById(long IdReserva)
        {

            Reserva reserva = BD.Reserva.Where(r => r.Id == IdReserva)
                .Include(r => r.IdEstadoReservaNavigation)
                .Include(r => r.IdPersonaNavigation)
                .Include(r => r.ReservaServicio).ThenInclude(rs => rs.IdPrecioServicioNavigation).ThenInclude(rs => rs.ReservaServicio).ThenInclude(rs => rs.IdServicioNavigation)
                .Include(r => r.OrdenCompra).ThenInclude(oc => oc.Voucher)
                .FirstOrDefault();

           
            if (reserva == null)
            {
                return NotFound("No se encontr� la reserva :"+IdReserva);
            }
            ReservaDTO NReserva = Mapper.Map<ReservaDTO>(reserva);

            return NReserva;
        }

        [Route("getLinkQR/{IdReserva:long}")]
        [HttpGet]
        public FileContentResult getQR(long IdReserva)
        {
            return File(Util.GenLinkQr(IdReserva), "image/png");
        }

        [Authorize]
        [Route("getReservas")]
        [HttpGet]
        public async Task<ActionResult<List<ReservaDTO>>> getReservas(int anio, int mes, int pagina = 1, int porPagina = 50)
        {
            pagina = pagina < 1 ? 1 : pagina;
            porPagina = porPagina < 1 ? 50 : porPagina;
            var reservas = await BD.Reserva.Where(r => r.FechaReserva.Month == mes && r.FechaReserva.Year == anio)
                .Include(r => r.IdEstadoReservaNavigation)
                .Include(r => r.IdPersonaNavigation)
                .Include(r => r.ReservaServicio).ThenInclude(rs => rs.IdPrecioServicioNavigation).ThenInclude(rs => rs.ReservaServicio).ThenInclude(rs => rs.IdServicioNavigation)
                .Include(r => r.OrdenCompra).ThenInclude(oc => oc.Voucher)
                .OrderByDescending(r => r.FechaIngreso)
                .Skip((pagina - 1) * porPagina)
                .Take(porPagina)
                .ToListAsync();

            List<ReservaDTO> ReservasDTO = reservas.Select(r=> Mapper.Map<ReservaDTO>(r)).ToList();

            return ReservasDTO;
        }

        [Authorize]
        [Route("getReservasEspeciales")]
        [HttpGet]
        public ActionResult<List<ReservaEspecialDTO>> getReservasEspeciales(int anio, int mes)
        {
            var reservas = BD.ReservasEspeciales.Where(r => r.IsEnabled && (r.FechaComienzo.Month == mes && r.FechaComienzo.Year == anio) || (r.FechaTermino.Month == mes && r.FechaTermino.Year == anio))
                .Include(r => r.IdServicioNavigation)
                .Include(r => r.IdTipoServicioNavigation)
                .OrderByDescending(r=> r.Id)
                .ToList();

            List<ReservaEspecialDTO> ReservasDTO = reservas.Select(r => Mapper.Map<ReservaEspecialDTO>(r)).ToList();
            ReservasDTO = ReservasDTO.Where(r => r.IsEnabled).ToList();
            return ReservasDTO;
        }

        [Authorize]
        [Route("CancelarReserva")]
        [HttpPatch]
        public async Task<ActionResult> CancelarReserva(long IdReserva)
        {
            var reserva = BD.Reserva.Where(r => r.Id == IdReserva).Include(r=>r.IdPersonaNavigation).FirstOrDefault();

            if (reserva == null)
            {
                return BadRequest("Reserva inv�lida.");
            }
            if (reserva.FechaReserva.Date < DateTime.Now.Date)
            {
                return BadRequest("La fecha de la reserva ya ha pasado.");
            }
            reserva.IdEstadoReserva = (long)EnumEstadoReserva.Anulada;
            reserva.FechaCancelacion = DateTime.Now;
            BD.SaveChanges();
            await Util.EnviarCorreoReservaCancelada(reserva);
            return Ok();
        }

        [Authorize]
        [Route("IngresarReservaEspecial")]
        [HttpPost]
        public ActionResult IngresarReservaEspecial([FromBody] ReservaEspecialCreacionDTO reserva)
        {

            if(reserva == null)
            {
                return BadRequest("Datos no v�lidos.");
            }

            if(reserva.FechaComienzo >= reserva.FechaTermino)
            {
                return BadRequest("Fechas no v�lidas.");
            }

            var NReserva = new ReservasEspeciales
            {
                FechaComienzo = reserva.FechaComienzo,
                FechaTermino = reserva.FechaTermino,
                IdServicio =(reserva.IsCamping || reserva.IsCanchas) ? null:reserva.IdServicio,
                IdTipoServicio = (reserva.IsCamping || reserva.IsCanchas) ? null : reserva.IdTipoServicio,
                IsCamping= reserva.IsCamping,
                IsCanchas=reserva.IsCanchas,
                IsEnabled = true
            };

            BD.ReservasEspeciales.Add(NReserva);
            BD.SaveChanges();
            return Ok();
        }

        [Authorize]
        [Route("CancelarReservaEspecial")]
        [HttpPatch]
        public ActionResult CancelarReservaEspecial(long IdReserva)
        {
            var reserva = BD.ReservasEspeciales.Where(r => r.Id == IdReserva).FirstOrDefault();
            if (reserva == null)
            {
                return BadRequest("Reserva inv�lida.");
            }
            reserva.IsEnabled = false;
            BD.SaveChanges();

            return Ok();
        }
    }
}
