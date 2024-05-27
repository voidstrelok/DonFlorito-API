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

        public ReservasController(ILogger<SessionController> logger, DonFloritoContext context, IMapper mapper, Utils util)
        {
            BD = context;
            _logger = logger;
            Mapper = mapper;
            Util = util;
        }

        [HttpPost]
        public async Task<ActionResult<ReservaDTO>> NuevaReserva([FromBody] ReservaCreacionDTO reserva)
        {
            //reservas deshabilitadas al momento
            if(!BD.Parametros.FirstOrDefault().ReservasEnabled)
            {
                return BadRequest("El sistema de reservas está deshabilitado");
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


            //refresca las reservas del día
            foreach (var item in BD.Reserva.Where(r => r.FechaReserva.Date == reserva.FechaReserva && r.IdEstadoReserva == (long)EnumEstadoReserva.PagoPendiente).Include(r => r.OrdenCompra).ToList())
            {
                Util.RefrescarEstadoReserva(item.Id);
            }

            long IdPersona = 0;
            if (reserva.IdPersona != null)
            {
                var Persona = BD.Persona.Where(p => p.Id == reserva.IdPersona).FirstOrDefault();
                if (Persona == null)
                {
                    return BadRequest("La persona no existe.");
                }
                else
                {
                    IdPersona = Persona.Id;
                }

            }
            else if (reserva.PersonaCreacion != null)
            {
                var PersonaExiste = BD.Persona.Any(p => p.Rut == reserva.PersonaCreacion.Rut);

                if (PersonaExiste)
                {
                    return BadRequest("Ya existe la persona que se intenta ingresar.");
                }
                else
                {
                    var PersonaCreacion = reserva.PersonaCreacion;
                    var RutValido = (new Rut.Rut(PersonaCreacion.Rut)).IsValid;
                    if (!RutValido)
                    {
                        return BadRequest("RUT no válido");
                    }

                    if (!MailboxAddress.TryParse(PersonaCreacion.Email, out var EmailValido))
                    {
                        return BadRequest("Email no válido");

                    }

                    var NPersona = new Persona()
                    {
                        Nombre = PersonaCreacion.Nombre,
                        SegundoNombre = PersonaCreacion.SegundoNombre,
                        ApellidoPaterno = PersonaCreacion.ApellidoPaterno,
                        ApellidoMaterno = PersonaCreacion.ApellidoMaterno,
                        Email = PersonaCreacion.Email,
                        Telefono = PersonaCreacion.Telefono,
                        Rut = PersonaCreacion.Rut,
                        IsEnabled = true,
                    };
                    BD.Persona.Add(NPersona);
                    await BD.SaveChangesAsync();
                    IdPersona = NPersona.Id;
                }
            }
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
                            return BadRequest("Uno o más servicios no están disponibles. Favor intente reservar nuevamente.");
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

                            var reservasEsp = BD.ReservasEspeciales.Where(re => (re.FechaComienzo.Date <= Fecha || re.FechaTermino >= Fecha) && (re.IsRecinto || re.IdServicio == Servicio.Id) && re.IsEnabled).ToList();

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
                                        return BadRequest("El horario de su reserva dejó de estar disponible. Favor intente reservar en otro horario.");
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
        public ActionResult<ReservaDTO> ConfirmarReserva([FromBody] ReservaDTO reserva, string token_ws)
        {
            if (reserva == null || token_ws.IsNullOrEmpty())
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

            var PersonaExiste = BD.Persona.Where(p => p.Rut == reserva.Persona.Rut).FirstOrDefault();

            using (TransactionScope transactionScope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                long IdPersona = 0;
                var DetalleTX = Util.Check(token_ws);


                if (PersonaExiste != null)
                {
                    IdPersona = PersonaExiste.Id;
                }
                else
                {
                    var PersonaCreacion = reserva.Persona;
                    var RutValido = (new Rut.Rut(PersonaCreacion.Rut)).IsValid;
                    if (!RutValido)
                    {
                        return BadRequest("RUT no válido");
                    }

                    if (!MailboxAddress.TryParse(PersonaCreacion.Email, out var EmailValido))
                    {
                        return BadRequest("Email no válido");

                    }

                    var NPersona = new Persona()
                    {
                        Nombre = PersonaCreacion.Nombre,
                        SegundoNombre = PersonaCreacion.SegundoNombre,
                        ApellidoPaterno = PersonaCreacion.ApellidoPaterno,
                        ApellidoMaterno = PersonaCreacion.ApellidoMaterno,
                        Email = PersonaCreacion.Email,
                        Telefono = PersonaCreacion.Telefono,
                        Rut = PersonaCreacion.Rut,
                        IsEnabled = true,
                    };
                    BD.Persona.Add(NPersona);
                    BD.SaveChanges();
                    IdPersona = NPersona.Id;
                }

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
                            return BadRequest("Uno o más servicios no están disponibles. Favor intente reservar nuevamente.");
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

                            var reservasEsp = BD.ReservasEspeciales.Where(re => (re.FechaComienzo.Date <= Fecha || re.FechaTermino >= Fecha) && (re.IsRecinto || re.IdServicio == Servicio.Id) && re.IsEnabled).ToList();

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
                                        return BadRequest("El horario de su reserva dejó de estar disponible. Favor intente reservar en otro horario.");
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
                            Util.EnviarCorreoReservaPagada(NReserva, vc);
                            ReservaDTO ReservaDTO = Mapper.Map<ReservaDTO>(NReserva);
                            return ReservaDTO;
                        default:
                            // fallo del pago
                            return BadRequest("El pago ha fallado o ha sido rechazado, favor intente nuevamente.");
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
                return NotFound("No se encontró la reserva :"+IdReserva);
            }

            reserva = Util.RefrescarEstadoReserva(reserva.Id);

            ReservaDTO NReserva = Mapper.Map<ReservaDTO>(reserva);

            NReserva.OrdenCompra = NReserva.OrdenCompra.Where(o => !o.IsUsed).ToList();

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
        public async Task<ActionResult<List<ReservaDTO>>> getReservas(int anio, int mes)
        {
            var reservas = await BD.Reserva.Where(r => r.FechaReserva.Month == mes && r.FechaReserva.Year == anio)
                .Include(r => r.IdEstadoReservaNavigation)
                .Include(r => r.IdPersonaNavigation)
                .Include(r => r.ReservaServicio).ThenInclude(rs => rs.IdPrecioServicioNavigation).ThenInclude(rs => rs.ReservaServicio).ThenInclude(rs => rs.IdServicioNavigation)
                .Include(r => r.OrdenCompra).ThenInclude(oc => oc.Voucher)
                .OrderByDescending(r => r.FechaIngreso)
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
        [HttpGet]
        public ActionResult CancelarReserva(long IdReserva)
        {
            var reserva = BD.Reserva.Where(r => r.Id == IdReserva).FirstOrDefault();
            if (reserva == null)
            {
                return BadRequest("Reserva inválida.");
            }
            //TODO correo anular reserva 
            reserva.IdEstadoReserva = (long)EnumEstadoReserva.Anulada;
            reserva.FechaCancelacion = DateTime.Now;
            BD.SaveChanges();

            return Ok();
        }

        [Authorize]
        [Route("IngresarReservaEspecial")]
        [HttpPost]
        public ActionResult IngresarReservaEspecial([FromBody] ReservaEspecialCreacionDTO reserva)
        {

            if(reserva == null)
            {
                return BadRequest("Datos no válidos.");
            }

            if(reserva.FechaComienzo >= reserva.FechaTermino)
            {
                return BadRequest("Fechas no válidas.");
            }

            var NReserva = new ReservasEspeciales
            {
                FechaComienzo = reserva.FechaComienzo,
                FechaTermino = reserva.FechaTermino,
                IdServicio = reserva.IdServicio,
                IdTipoServicio = reserva.IdTipoServicio,
                IsRecinto = reserva.IsRecinto,
                IsEnabled = true
            };

            BD.ReservasEspeciales.Add(NReserva);
            BD.SaveChanges();
            return Ok();
        }

        [Authorize]
        [Route("CancelarReservaEspecial")]
        [HttpGet]
        public ActionResult CancelarReservaEspecial(long IdReserva)
        {
            var reserva = BD.ReservasEspeciales.Where(r => r.Id == IdReserva).FirstOrDefault();
            if (reserva == null)
            {
                return BadRequest("Reserva inválida.");
            }
            reserva.IsEnabled = false;
            BD.SaveChanges();

            return Ok();
        }
    }
}